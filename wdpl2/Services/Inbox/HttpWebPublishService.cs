using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Wdpl2.Services.Inbox;

/// <summary>
/// Publishes the current season's teams, players and fixtures to the wdpl.uk
/// captain portal so captains can see their fixtures and roster online.
/// </summary>
public interface IWebPublishService
{
    Task<(int teams, int players, int fixtures)> PublishLeagueAsync(CancellationToken ct = default);
}

public sealed class HttpWebPublishService : IWebPublishService
{
    private readonly ISeasonService _seasons;
    private readonly IDataStore _data;

    public HttpWebPublishService(ISeasonService seasons, IDataStore data)
    {
        _seasons = seasons;
        _data    = data;
    }

    public async Task<(int teams, int players, int fixtures)> PublishLeagueAsync(CancellationToken ct = default)
    {
        var seasonId = _seasons.CurrentSeasonId
            ?? throw new InvalidOperationException("No current season selected.");

        var settings = await WebInboxSettings.LoadAsync();

        var teams     = await _data.GetTeamsAsync(seasonId, ct);
        var players   = await _data.GetPlayersAsync(seasonId, ct);
        var fixtures  = await _data.GetFixturesAsync(seasonId, ct);
        var divisions = await _data.GetDivisionsAsync(seasonId, ct);
        var venues    = await _data.GetVenuesAsync(seasonId, ct);

        string? DivisionName(Guid? id) => id is null ? null : divisions.FirstOrDefault(d => d.Id == id)?.Name;
        string? VenueName(Guid? id)    => id is null ? null : venues.FirstOrDefault(v => v.Id == id)?.Name;
        string? TeamName(Guid id)      => teams.FirstOrDefault(t => t.Id == id)?.Name;

        var payload = new
        {
            season_id = seasonId.ToString(),
            teams = teams.Select(t => new {
                team_id       = t.Id.ToString(),
                name          = t.Name ?? "",
                division_id   = t.DivisionId?.ToString(),
                division_name = DivisionName(t.DivisionId),
                venue_name    = VenueName(t.VenueId)
            }),
            players = players.Select(p => new {
                player_id = p.Id.ToString(),
                team_id   = p.TeamId?.ToString(),
                full_name = p.FullName,
                is_active = p.IsActive
            }),
            fixtures = fixtures.Select(f => new {
                fixture_id     = f.Id.ToString(),
                division_id    = f.DivisionId?.ToString(),
                home_team_id   = f.HomeTeamId.ToString(),
                away_team_id   = f.AwayTeamId.ToString(),
                home_team_name = TeamName(f.HomeTeamId),
                away_team_name = TeamName(f.AwayTeamId),
                venue_name     = VenueName(f.VenueId),
                fixture_date   = f.Date.ToString("yyyy-MM-dd HH:mm:ss")
            })
        };

        using var http = BuildClient(settings);
        var url = BuildUrl(settings, "admin/publish-league.php");

        using var req = new HttpRequestMessage(HttpMethod.Post, url)
        {
            Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json")
        };
        AddAuth(req, settings);

        using var resp = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            throw new HttpRequestException($"{(int)resp.StatusCode} {resp.ReasonPhrase}: {body}");
        }

        return (teams.Count, players.Count, fixtures.Count);
    }

    private static HttpClient BuildClient(WebInboxSettings s)
    {
        if (!s.IgnoreSslErrors)
            return new HttpClient { Timeout = TimeSpan.FromMinutes(2) };

        var handler = new SocketsHttpHandler
        {
            SslOptions = new System.Net.Security.SslClientAuthenticationOptions
            {
                RemoteCertificateValidationCallback = (_, _, _, _) => true
            }
        };
        return new HttpClient(handler) { Timeout = TimeSpan.FromMinutes(2) };
    }

    private static Uri BuildUrl(WebInboxSettings s, string relative)
    {
        var baseUrl = string.IsNullOrWhiteSpace(s.BaseUrl) ? WebInboxSettings.DefaultBaseUrl : s.BaseUrl;
        if (!baseUrl.EndsWith("/")) baseUrl += "/";
        return new Uri(new Uri(baseUrl), relative);
    }

    private static void AddAuth(HttpRequestMessage req, WebInboxSettings s)
    {
        if (string.IsNullOrEmpty(s.AdminUser)) return;
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{s.AdminUser}:{s.AdminPassword}"));
        req.Headers.Authorization = new AuthenticationHeaderValue("Basic", token);
    }
}
