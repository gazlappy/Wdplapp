using Wdpl2.Models;

namespace Wdpl2.Services.Web;

/// <summary>
/// Captain access: which teams can sign in to the website, and with what PIN.
/// </summary>
public sealed class CaptainsWebModule : IWebModule
{
    public string Id => "captains";
    public string Title => "Captains";
    public string Description => "Let team captains sign in to see their fixtures, roster and contacts.";
    public string Icon => "\U0001F511"; // key
    public int SchemaVersion => 3;

    public IReadOnlyList<string> ServerFiles { get; } = new[]
    {
        "api/modules/captains/Module.php",

        // Sends a captain who arrived on http to the secure page before the
        // sign-in they came for is refused. See the file itself.
        "captain/.htaccess",
    };
}

/// <summary>
/// Builds the captain PIN payload.
/// </summary>
/// <remarks>
/// PINs are hashed here, in the app, and only the hash is sent. The plaintext
/// PIN never crosses the network and is never stored on the server, so a
/// database disclosure does not hand out working logins.
/// <para>
/// This replaces a scheme that published an unsalted SHA-256 of each PIN in a
/// public JSON file for the browser to check. Four digits hashed that way is
/// ten thousand guesses.
/// </para>
/// </remarks>
public static class CaptainPins
{
    public sealed class Summary
    {
        public int WithPin { get; init; }
        public int WithoutPin { get; init; }
        public List<string> Weak { get; init; } = new();
    }

    /// <summary>A PIN shorter than this is trivially guessable even with rate limiting.</summary>
    public const int RecommendedMinimumLength = 6;

    public static (object Payload, Summary Summary) Build(LeagueData league, Season season)
    {
        ArgumentNullException.ThrowIfNull(league);
        ArgumentNullException.ThrowIfNull(season);

        var teams = league.Teams.Where(t => t.SeasonId == season.Id).ToList();

        var pins = new List<object>();
        var weak = new List<string>();
        var without = 0;

        foreach (var team in teams.OrderBy(t => t.Name))
        {
            var pin = team.CaptainPin?.Trim();
            if (string.IsNullOrEmpty(pin))
            {
                without++;
                continue;
            }

            if (pin.Length < RecommendedMinimumLength)
                weak.Add(team.Name ?? "(unnamed team)");

            pins.Add(new
            {
                teamId = team.Id,
                pinHash = PasswordHash.Create(pin),
            });
        }

        var payload = new { seasonId = season.Id, pins };

        return (payload, new Summary
        {
            WithPin = pins.Count,
            WithoutPin = without,
            Weak = weak,
        });
    }
}
