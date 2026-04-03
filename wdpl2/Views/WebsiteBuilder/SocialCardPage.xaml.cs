using System.Collections.ObjectModel;
using System.Text;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Helpers;
using Wdpl2.Models;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class SocialCardPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<Division> _divisions = new();
    private readonly List<Fixture> _seasonFixtures = new();
    private readonly List<Team> _seasonTeams = new();
    private readonly List<Player> _seasonPlayers = new();
    private readonly List<Venue> _seasonVenues = new();
    private string? _generatedHtml;
    private string? _cachedShareText;
    private string? _lastSavedImagePath;

    public SocialCardPage()
    {
        InitializeComponent();

        SeasonPicker.ItemsSource = _seasons;
        SeasonPicker.ItemDisplayBinding = new Binding("Name");

        DivisionPicker.ItemsSource = _divisions;
        DivisionPicker.ItemDisplayBinding = new Binding("Name");

        CardStylePicker.SelectedIndex = 0;
        CardSizePicker.SelectedIndex = 0;

        LoadSeasons();
    }

    // ────────────────────── Data Loading ──────────────────────

    private void LoadSeasons()
    {
        _seasons.Clear();
        foreach (var s in League.Seasons.OrderByDescending(s => s.StartDate))
            _seasons.Add(s);

        var active = _seasons.FirstOrDefault(s => s.IsActive) ?? _seasons.FirstOrDefault();
        if (active != null) SeasonPicker.SelectedItem = active;
    }

    private void OnSeasonChanged(object? sender, EventArgs e)
    {
        if (SeasonPicker.SelectedItem is not Season season) return;

        var id = season.Id;
        _seasonFixtures.Clear();
        _seasonFixtures.AddRange(League.Fixtures.Where(f => f.SeasonId == id));

        _seasonTeams.Clear();
        _seasonTeams.AddRange(League.Teams.Where(t => t.SeasonId == id));

        _seasonPlayers.Clear();
        _seasonPlayers.AddRange(League.Players.Where(p => p.SeasonId == id));

        _seasonVenues.Clear();
        _seasonVenues.AddRange(League.Venues.Where(v => v.SeasonId == id));

        _divisions.Clear();
        _divisions.Add(new Division { Name = "All Divisions", Id = Guid.Empty });
        foreach (var d in League.Divisions.Where(d => d.SeasonId == id))
            _divisions.Add(d);
        DivisionPicker.SelectedIndex = 0;

        PopulateMatchPicker();
        PopulatePlayerPicker();
    }

    private void OnCardTypeChanged(object? sender, EventArgs e)
    {
        var cardType = CardTypePicker.SelectedItem?.ToString() ?? "";
        MatchSelectionFrame.IsVisible = cardType is "Result Card" or "Fixture Card";
        PlayerSelectionFrame.IsVisible = cardType == "Player Highlight";
        PopulateMatchPicker();
    }

    private void PopulateMatchPicker()
    {
        var cardType = CardTypePicker.SelectedItem?.ToString() ?? "";
        var divId = (DivisionPicker.SelectedItem as Division)?.Id;
        var fixtures = _seasonFixtures.AsEnumerable();
        if (divId.HasValue && divId.Value != Guid.Empty)
            fixtures = fixtures.Where(f => f.DivisionId == divId.Value);

        List<FixtureDisplayItem> items;
        if (cardType == "Result Card")
        {
            items = fixtures.Where(f => f.Frames.Count > 0)
                .OrderByDescending(f => f.Date)
                .Select(f => new FixtureDisplayItem(f, _seasonTeams))
                .ToList();
        }
        else
        {
            items = fixtures.Where(f => f.Frames.Count == 0 && f.Date >= DateTime.Today.AddDays(-1))
                .OrderBy(f => f.Date)
                .Select(f => new FixtureDisplayItem(f, _seasonTeams))
                .ToList();
        }

        MatchPicker.ItemsSource = items;
        MatchPicker.ItemDisplayBinding = new Binding("Display");
        if (items.Count > 0) MatchPicker.SelectedIndex = 0;
    }

    private void PopulatePlayerPicker()
    {
        var items = _seasonPlayers
            .OrderBy(p => p.Name)
            .Select(p => new PlayerDisplayItem(p, _seasonTeams))
            .ToList();
        PlayerPicker.ItemsSource = items;
        PlayerPicker.ItemDisplayBinding = new Binding("Display");
        if (items.Count > 0) PlayerPicker.SelectedIndex = 0;
    }

    private void OnStyleChanged(object? sender, EventArgs e) { }
    private void OnStyleChanged(object? sender, CheckedChangedEventArgs e) { }
    private void OnStyleChanged(object? sender, TextChangedEventArgs e) { }

    // ────────────────────── Preview ──────────────────────

    private void OnPreviewClicked(object? sender, EventArgs e)
    {
        var cardType = CardTypePicker.SelectedItem?.ToString();
        if (string.IsNullOrEmpty(cardType))
        {
            ShowStatus("Please select a card type", true);
            return;
        }
        if (SeasonPicker.SelectedItem is not Season)
        {
            ShowStatus("Please select a season", true);
            return;
        }

        try
        {
            _generatedHtml = GenerateCardHtml(cardType);
            _cachedShareText = BuildShareText(includeUrl: false);
            PreviewPlaceholder.IsVisible = false;
            PreviewWebView.IsVisible = true;
            PreviewWebView.Source = new HtmlWebViewSource { Html = _generatedHtml };
            ShowStatus("Card generated — ready to share!");
        }
        catch (Exception ex)
        {
            ShowStatus($"Error: {ex.Message}", true);
        }
    }

    // ────────────────────── Share / Post To ──────────────────────

    private async void OnShareClicked(object? sender, EventArgs e)
    {
        var path = await SaveCardToFile();
        if (path == null) return;

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Share Social Card",
            File = new ShareFile(path)
        });
    }

    private async void OnSaveImageClicked(object? sender, EventArgs e)
    {
        var path = await SaveCardToFile();
        if (path == null) return;

        ShowStatus($"Saved to {Path.GetFileName(path)}");
        await DisplayAlert("Saved", $"Card saved to:\n{path}", "OK");
    }

    private async void OnPostFacebookClicked(object? sender, EventArgs e)
    {
        EnsureCardGenerated();
        var shareText = GetShareText(includeUrl: true);

        // Show the user exactly what will be copied, then open Facebook
        var confirmed = await DisplayAlert(
            "Post to Facebook",
            $"This text will be copied to your clipboard:\n\n{shareText}\n\nClick 'Copy & Open' then paste (Ctrl+V) into your Facebook post.",
            "Copy & Open Facebook",
            "Cancel");

        if (!confirmed) return;

        await Clipboard.Default.SetTextAsync(shareText);

        var fbPageUrl = League.WebsiteSettings.FacebookUrl;
        var fbUrl = !string.IsNullOrWhiteSpace(fbPageUrl)
            ? fbPageUrl
            : "https://www.facebook.com/";

        try
        {
            await Browser.Default.OpenAsync(new Uri(fbUrl), BrowserLaunchMode.SystemPreferred);
            ShowStatus("\u2705 Post text copied — paste it into your Facebook post!");
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open Facebook: {ex.Message}", true);
        }
    }

    private async void OnPostTwitterClicked(object? sender, EventArgs e)
    {
        EnsureCardGenerated();
        var shareText = GetShareText(includeUrl: false);
        var websiteUrl = League.WebsiteSettings.WebsiteUrl;

        var tweetUrl = string.IsNullOrWhiteSpace(websiteUrl)
            ? $"https://twitter.com/intent/tweet?text={Uri.EscapeDataString(shareText)}"
            : $"https://twitter.com/intent/tweet?text={Uri.EscapeDataString(shareText)}&url={Uri.EscapeDataString(websiteUrl)}";

        await Clipboard.Default.SetTextAsync(GetShareText(includeUrl: true));

        try
        {
            await Browser.Default.OpenAsync(new Uri(tweetUrl), BrowserLaunchMode.SystemPreferred);
            ShowStatus("\u2705 Opening X / Twitter — text also copied to clipboard!");
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open X / Twitter: {ex.Message}", true);
        }
    }

    private async void OnPostInstagramClicked(object? sender, EventArgs e)
    {
        var path = await SaveCardToFile();
        if (path == null) return;

        var shareText = GetShareText(includeUrl: true);
        await Clipboard.Default.SetTextAsync(shareText);

        ShowStatus("Card saved & text copied — paste into Instagram!");
        await DisplayAlert("Instagram",
            "Instagram doesn't support direct posting from desktop apps.\n\n" +
            "Your card has been saved and the post text copied to your clipboard.\n\n" +
            "1. Open Instagram on your phone\n" +
            "2. Create a new post\n" +
            "3. Upload the saved card image\n" +
            "4. Paste the copied text as your caption\n\n" +
            $"Card saved to:\n{path}", "OK");
    }

    private async void OnPostWhatsAppClicked(object? sender, EventArgs e)
    {
        EnsureCardGenerated();
        var shareText = GetShareText(includeUrl: true);

        await Clipboard.Default.SetTextAsync(shareText);

        var waUrl = $"https://api.whatsapp.com/send?text={Uri.EscapeDataString(shareText)}";

        try
        {
            await Browser.Default.OpenAsync(new Uri(waUrl), BrowserLaunchMode.SystemPreferred);
            ShowStatus("\u2705 Opening WhatsApp — text also copied to clipboard!");
        }
        catch (Exception ex)
        {
            ShowStatus($"Could not open WhatsApp: {ex.Message}", true);
        }
    }

    private async void OnCopyClipboardClicked(object? sender, EventArgs e)
    {
        var text = GetShareText(includeUrl: true);
        await Clipboard.Default.SetTextAsync(text);
        ShowStatus("Copied to clipboard!");
    }

    private void EnsureCardGenerated()
    {
        if (string.IsNullOrEmpty(_generatedHtml))
            OnPreviewClicked(null, EventArgs.Empty);
    }

    /// <summary>
    /// Returns the cached share text from when the card was generated,
    /// falling back to BuildShareText if no cache exists.
    /// </summary>
    private string GetShareText(bool includeUrl)
    {
        var baseText = _cachedShareText ?? BuildShareText(includeUrl: false);
        if (!includeUrl)
            return baseText;

        var settings = League.WebsiteSettings;
        var season = SeasonPicker.SelectedItem as Season;
        var sb = new StringBuilder(baseText);

        if (season != null)
        {
            sb.AppendLine();
            sb.AppendLine();
            sb.AppendLine($"\U0001F3C1 {season.Name}");
        }

        if (!string.IsNullOrWhiteSpace(settings.WebsiteUrl))
        {
            sb.AppendLine();
            sb.AppendLine(settings.WebsiteUrl);
        }

        return sb.ToString().TrimEnd();
    }

    private async Task<string?> SaveCardToFile()
    {
        if (string.IsNullOrEmpty(_generatedHtml))
        {
            OnPreviewClicked(null, EventArgs.Empty);
            if (string.IsNullOrEmpty(_generatedHtml))
            {
                ShowStatus("Generate a card first", true);
                return null;
            }
        }

        try
        {
            var cardType = CardTypePicker.SelectedItem?.ToString() ?? "card";
            var safeName = string.Join("_", cardType.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"SocialCard_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.html";
            var dir = Path.Combine(FileSystem.CacheDirectory, "SocialCards");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, fileName);
            await File.WriteAllTextAsync(filePath, _generatedHtml);
            _lastSavedImagePath = filePath;
            return filePath;
        }
        catch (Exception ex)
        {
            ShowStatus($"Save error: {ex.Message}", true);
            return null;
        }
    }

    private string BuildShareText(bool includeUrl = false)
    {
        var cardType = CardTypePicker.SelectedItem?.ToString() ?? "";
        var settings = League.WebsiteSettings;
        var sb = new StringBuilder();

        if (cardType is "Result Card" && MatchPicker.SelectedItem is FixtureDisplayItem resultItem)
        {
            var f = resultItem.Fixture;
            var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
            var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
            sb.AppendLine($"\U0001F3B1 {settings.LeagueName} \u2014 Result");
            sb.AppendLine();
            sb.AppendLine($"{home} {f.HomeScore} - {f.AwayScore} {away}");
            var venue = _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId)?.Name;
            if (!string.IsNullOrEmpty(venue))
                sb.AppendLine($"\U0001F4CD {venue}");
        }
        else if (cardType is "Fixture Card" && MatchPicker.SelectedItem is FixtureDisplayItem fixItem)
        {
            var f = fixItem.Fixture;
            var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
            var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
            sb.AppendLine($"\U0001F4C5 {settings.LeagueName} \u2014 Upcoming");
            sb.AppendLine();
            sb.AppendLine($"{home} vs {away}");
            sb.AppendLine($"\U0001F552 {f.Date:ddd dd MMM, h:mm tt}");
            var venue = _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId)?.Name;
            if (!string.IsNullOrEmpty(venue))
                sb.AppendLine($"\U0001F4CD {venue}");
        }
        else if (cardType is "League Table")
        {
            sb.AppendLine($"\U0001F3C6 {settings.LeagueName} \u2014 League Table");
        }
        else if (cardType is "Player Highlight" && PlayerPicker.SelectedItem is PlayerDisplayItem playerItem)
        {
            sb.AppendLine($"\u2B50 {settings.LeagueName} \u2014 Player Spotlight");
            sb.AppendLine();
            sb.AppendLine($"\U0001F3B1 {playerItem.Player.Name}");
        }
        else if (cardType is "Weekly Results")
        {
            sb.AppendLine($"\U0001F4CA {settings.LeagueName} \u2014 This Week's Results");
        }
        else if (cardType is "Upcoming Fixtures")
        {
            sb.AppendLine($"\U0001F4C5 {settings.LeagueName} \u2014 Upcoming Fixtures");
        }
        else
        {
            sb.AppendLine($"\U0001F3B1 {settings.LeagueName}");
            if (!string.IsNullOrEmpty(cardType)) sb.AppendLine(cardType);
        }

        return sb.ToString().TrimEnd();
    }

    private void ShowStatus(string text, bool isError = false)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = Color.FromArgb(isError ? "#EF4444" : "#10B981");
        StatusLabel.IsVisible = true;
    }

    // ────────────────────── HTML Card Generation ──────────────────────

    private string GenerateCardHtml(string cardType)
    {
        var style = GetCardStyle();
        var (width, height) = GetCardSize();
        var content = cardType switch
        {
            "Result Card" => GenerateResultCardContent(style),
            "Fixture Card" => GenerateFixtureCardContent(style),
            "League Table" => GenerateLeagueTableContent(style),
            "Player Highlight" => GeneratePlayerHighlightContent(style),
            "Weekly Results" => GenerateWeeklyResultsContent(style),
            "Upcoming Fixtures" => GenerateUpcomingFixturesContent(style),
            _ => "<p>Select a card type</p>"
        };

        return WrapInCardHtml(content, style, width, height);
    }

    private CardStyle GetCardStyle()
    {
        var schemeName = CardStylePicker.SelectedItem?.ToString() ?? "League Branding";
        var ws = League.WebsiteSettings;
        return schemeName switch
        {
            "Dark" => new CardStyle("#0F172A", "#1E293B", "#F8FAFC", "#94A3B8", "#3B82F6", "#F59E0B"),
            "Light" => new CardStyle("#FFFFFF", "#F8FAFC", "#1E293B", "#64748B", "#3B82F6", "#10B981"),
            "Vibrant" => new CardStyle("#1E1B4B", "#312E81", "#FFFFFF", "#C7D2FE", "#818CF8", "#F472B6"),
            "Minimal" => new CardStyle("#FFFFFF", "#F1F5F9", "#334155", "#94A3B8", "#475569", "#475569"),
            _ => new CardStyle(ws.PrimaryColor, ws.SecondaryColor, "#FFFFFF", "#E2E8F0", ws.AccentColor, ws.PrimaryColor)
        };
    }

    private (int w, int h) GetCardSize()
    {
        return (CardSizePicker.SelectedItem?.ToString() ?? "") switch
        {
            "Portrait (1080×1350)" => (1080, 1350),
            "Landscape (1200×628)" => (1200, 628),
            "Story (1080×1920)" => (1080, 1920),
            _ => (1080, 1080)
        };
    }

    private string WrapInCardHtml(string content, CardStyle style, int width, int height)
    {
        var leagueName = League.WebsiteSettings.LeagueName;
        var headline = HeadlineEntry?.Text ?? "";
        var footer = FooterTextEntry?.Text ?? "";
        var showLogo = ShowLogoCheck.IsChecked;
        var showWebsite = ShowWebsiteCheck.IsChecked;
        var websiteUrl = League.WebsiteSettings.WebsiteUrl;

        var logoHtml = "";
        if (showLogo)
        {
            var logoData = League.WebsiteSettings.GetEffectiveLogoData();
            if (logoData != null && logoData.Length > 0)
            {
                var b64 = Convert.ToBase64String(logoData);
                logoHtml = $"<img src='data:image/png;base64,{b64}' style='max-height:60px;max-width:180px;margin-bottom:8px;'/>";
            }
        }

        var headerSection = $@"
            <div style='text-align:center;padding:24px 20px 12px;'>
                {logoHtml}
                <div style='font-size:22px;font-weight:800;color:{style.TextColor};letter-spacing:0.5px;'>{Escape(leagueName)}</div>
                {(string.IsNullOrWhiteSpace(headline) ? "" : $"<div style='font-size:14px;color:{style.SubTextColor};margin-top:4px;'>{Escape(headline)}</div>")}
            </div>";

        var footerSection = "";
        if (!string.IsNullOrWhiteSpace(footer) || (showWebsite && !string.IsNullOrWhiteSpace(websiteUrl)))
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(footer)) parts.Add(Escape(footer));
            if (showWebsite && !string.IsNullOrWhiteSpace(websiteUrl)) parts.Add(Escape(websiteUrl));
            footerSection = $@"
            <div style='text-align:center;padding:12px 20px 20px;font-size:12px;color:{style.SubTextColor};'>
                {string.Join(" &bull; ", parts)}
            </div>";
        }

        return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1'/>
<style>
  * {{ margin:0; padding:0; box-sizing:border-box; }}
  body {{ 
    font-family: 'Segoe UI',Inter,-apple-system,BlinkMacSystemFont,sans-serif;
    background: #F1F5F9;
    display: flex; justify-content: center; align-items: center;
    min-height: 100vh; padding: 4px;
    overflow: hidden;
  }}
  .card-wrapper {{
    transform-origin: center center;
  }}
  .card {{
    width: {width}px; height: {height}px;
    background: linear-gradient(135deg, {style.BgColor} 0%, {style.BgColor2} 100%);
    border-radius: 20px; overflow: hidden;
    display: flex; flex-direction: column;
    box-shadow: 0 8px 32px rgba(0,0,0,0.15);
  }}
  .card-content {{ flex:1; display:flex; flex-direction:column; justify-content:center; padding:0 24px; }}
</style>
</head>
<body>
<div class='card-wrapper' id='cardWrap'>
  <div class='card'>
    {headerSection}
    <div class='card-content'>{content}</div>
    {footerSection}
  </div>
</div>
<script>
  function scaleCard() {{
    var wrap = document.getElementById('cardWrap');
    var pad = 8;
    var vw = window.innerWidth - pad;
    var vh = window.innerHeight - pad;
    var cw = {width}; var ch = {height};
    var s = Math.min(vw / cw, vh / ch, 1);
    wrap.style.transform = 'scale(' + s + ')';
    document.body.style.height = Math.max(ch * s + pad, vh + pad) + 'px';
  }}
  scaleCard();
  window.addEventListener('resize', scaleCard);
</script>
</body></html>";
    }

    // ── Result Card ──

    private string GenerateResultCardContent(CardStyle style)
    {
        if (MatchPicker.SelectedItem is not FixtureDisplayItem item)
            return $"<p style='text-align:center;color:{style.SubTextColor}'>Select a match first</p>";

        var f = item.Fixture;
        var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
        var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
        var venue = _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId)?.Name ?? "";
        var division = _divisions.FirstOrDefault(d => d.Id == f.DivisionId)?.Name ?? "";

        var showDate = ShowDateCheck.IsChecked;
        var showDiv = ShowDivisionCheck.IsChecked && !string.IsNullOrEmpty(division);
        var showVenue = ShowVenueCheck.IsChecked && !string.IsNullOrEmpty(venue);

        var homeWin = f.HomeScore > f.AwayScore;
        var awayWin = f.AwayScore > f.HomeScore;

        return $@"
        {(showDiv ? $"<div style='text-align:center;font-size:13px;color:{style.Accent};font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:12px;'>{Escape(division)}</div>" : "")}
        {(showDate ? $"<div style='text-align:center;font-size:13px;color:{style.SubTextColor};margin-bottom:16px;'>{f.Date:dddd dd MMMM yyyy}</div>" : "")}
        <div style='display:flex;align-items:center;justify-content:center;gap:20px;padding:16px 0;'>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:20px;font-weight:700;color:{style.TextColor};{(homeWin ? "text-shadow:0 0 8px " + style.Accent + "40;" : "")}'>{Escape(home)}</div>
            </div>
            <div style='text-align:center;'>
                <div style='font-size:48px;font-weight:900;color:{style.TextColor};letter-spacing:4px;'>
                    <span style='{(homeWin ? "color:" + style.Accent + ";" : "")}'>{f.HomeScore}</span>
                    <span style='color:{style.SubTextColor};margin:0 6px;'>-</span>
                    <span style='{(awayWin ? "color:" + style.Accent + ";" : "")}'>{f.AwayScore}</span>
                </div>
                <div style='font-size:11px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:2px;margin-top:4px;'>Final Score</div>
            </div>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:20px;font-weight:700;color:{style.TextColor};{(awayWin ? "text-shadow:0 0 8px " + style.Accent + "40;" : "")}'>{Escape(away)}</div>
            </div>
        </div>
        {(showVenue ? $"<div style='text-align:center;font-size:12px;color:{style.SubTextColor};margin-top:16px;'>&#128205; {Escape(venue)}</div>" : "")}";
    }

    // ── Fixture Card ──

    private string GenerateFixtureCardContent(CardStyle style)
    {
        if (MatchPicker.SelectedItem is not FixtureDisplayItem item)
            return $"<p style='text-align:center;color:{style.SubTextColor}'>Select a fixture first</p>";

        var f = item.Fixture;
        var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
        var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
        var venue = _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId)?.Name ?? "";
        var division = _divisions.FirstOrDefault(d => d.Id == f.DivisionId)?.Name ?? "";

        var showDate = ShowDateCheck.IsChecked;
        var showDiv = ShowDivisionCheck.IsChecked && !string.IsNullOrEmpty(division);
        var showVenue = ShowVenueCheck.IsChecked && !string.IsNullOrEmpty(venue);

        return $@"
        {(showDiv ? $"<div style='text-align:center;font-size:13px;color:{style.Accent};font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:12px;'>{Escape(division)}</div>" : "")}
        {(showDate ? $"<div style='text-align:center;font-size:13px;color:{style.SubTextColor};margin-bottom:20px;'>{f.Date:dddd dd MMMM yyyy}</div>" : "")}
        <div style='display:flex;align-items:center;justify-content:center;gap:20px;padding:20px 0;'>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:22px;font-weight:700;color:{style.TextColor};'>{Escape(home)}</div>
            </div>
            <div style='text-align:center;'>
                <div style='font-size:28px;font-weight:800;color:{style.Accent};'>VS</div>
                <div style='font-size:14px;font-weight:600;color:{style.TextColor};margin-top:4px;'>{f.Date:h:mm tt}</div>
            </div>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:22px;font-weight:700;color:{style.TextColor};'>{Escape(away)}</div>
            </div>
        </div>
        {(showVenue ? $"<div style='text-align:center;font-size:12px;color:{style.SubTextColor};margin-top:16px;'>&#128205; {Escape(venue)}</div>" : "")}";
    }

    // ── League Table ──

    private string GenerateLeagueTableContent(CardStyle style)
    {
        var season = SeasonPicker.SelectedItem as Season;
        if (season == null) return "";

        var divId = (DivisionPicker.SelectedItem as Division)?.Id;
        var teams = _seasonTeams.AsEnumerable();
        var fixtures = _seasonFixtures.AsEnumerable();
        if (divId.HasValue && divId.Value != Guid.Empty)
        {
            teams = teams.Where(t => t.DivisionId == divId.Value);
            fixtures = fixtures.Where(f => f.DivisionId == divId.Value);
        }

        var teamList = teams.ToList();
        var fixtureList = fixtures.ToList();
        if (teamList.Count == 0) return $"<p style='text-align:center;color:{style.SubTextColor}'>No teams found</p>";

        var appSettings = League.GetSettingsForSeason(season.Id);
        var standings = StandingsCalculator.Calculate(teamList, fixtureList, appSettings);
        var sorted = StandingsSorter.Sort(
            standings, appSettings,
            s => s.Points, s => s.FramesFor, s => s.FramesAgainst, s => s.Won, s => s.TeamId,
            fixtureList);

        for (int i = 0; i < sorted.Count; i++) sorted[i].Position = i + 1;

        var divName = _divisions.FirstOrDefault(d => d.Id == divId)?.Name ?? "";
        var showDiv = ShowDivisionCheck.IsChecked && !string.IsNullOrEmpty(divName) && divName != "All Divisions";

        var sb = new StringBuilder();
        if (showDiv) sb.Append($"<div style='text-align:center;font-size:13px;color:{style.Accent};font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:12px;'>{Escape(divName)}</div>");

        sb.Append($@"<table style='width:100%;border-collapse:collapse;font-size:13px;color:{style.TextColor};'>
            <tr style='border-bottom:2px solid {style.Accent};'>
                <th style='padding:8px 6px;text-align:center;width:30px;'>#</th>
                <th style='padding:8px 6px;text-align:left;'>Team</th>
                <th style='padding:8px 6px;text-align:center;'>P</th>
                <th style='padding:8px 6px;text-align:center;'>W</th>
                <th style='padding:8px 6px;text-align:center;'>L</th>
                <th style='padding:8px 6px;text-align:center;'>FD</th>
                <th style='padding:8px 6px;text-align:center;font-weight:800;'>Pts</th>
            </tr>");

        var maxRows = Math.Min(sorted.Count, 12);
        for (int i = 0; i < maxRows; i++)
        {
            var s = sorted[i];
            var rowBg = i % 2 == 0 ? "transparent" : style.BgColor2 + "40";
            var isTop3 = i < 3;
            var posIcon = i switch { 0 => "&#129351;", 1 => "&#129352;", 2 => "&#129353;", _ => s.Position.ToString() };
            sb.Append($@"<tr style='background:{rowBg};{(isTop3 ? "font-weight:600;" : "")}'>
                <td style='padding:7px 6px;text-align:center;'>{posIcon}</td>
                <td style='padding:7px 6px;text-align:left;{(isTop3 ? "color:" + style.Accent + ";" : "")}'>{Escape(s.TeamName)}</td>
                <td style='padding:7px 6px;text-align:center;'>{s.Played}</td>
                <td style='padding:7px 6px;text-align:center;'>{s.Won}</td>
                <td style='padding:7px 6px;text-align:center;'>{s.Lost}</td>
                <td style='padding:7px 6px;text-align:center;'>{s.FrameDifference:+0;-0;0}</td>
                <td style='padding:7px 6px;text-align:center;font-weight:800;'>{s.Points}</td>
            </tr>");
        }

        if (sorted.Count > maxRows)
            sb.Append($"<tr><td colspan='7' style='padding:8px;text-align:center;color:{style.SubTextColor};font-size:11px;'>+ {sorted.Count - maxRows} more teams</td></tr>");

        sb.Append("</table>");
        return sb.ToString();
    }

    // ── Player Highlight ──

    private string GeneratePlayerHighlightContent(CardStyle style)
    {
        if (PlayerPicker.SelectedItem is not PlayerDisplayItem item)
            return $"<p style='text-align:center;color:{style.SubTextColor}'>Select a player first</p>";

        var player = item.Player;
        var teamName = _seasonTeams.FirstOrDefault(t => t.Id == player.TeamId)?.Name ?? "";

        var playerFixtures = _seasonFixtures.Where(f => f.Frames.Count > 0);
        int played = 0, won = 0;
        foreach (var f in playerFixtures)
        {
            foreach (var frame in f.Frames)
            {
                bool isHome = frame.HomePlayerId == player.Id || frame.HomePlayer2Id == player.Id;
                bool isAway = frame.AwayPlayerId == player.Id || frame.AwayPlayer2Id == player.Id;
                if (!isHome && !isAway) continue;

                played++;
                if ((isHome && frame.Winner == FrameWinner.Home) || (isAway && frame.Winner == FrameWinner.Away))
                    won++;
            }
        }

        var lost = played - won;
        var winPct = played > 0 ? (won * 100.0 / played).ToString("0.0") : "0.0";

        return $@"
        <div style='text-align:center;padding:16px 0;'>
            <div style='width:80px;height:80px;border-radius:50%;background:{style.Accent};margin:0 auto 12px;display:flex;align-items:center;justify-content:center;font-size:36px;color:white;font-weight:800;'>
                {Escape(player.Name.Length > 0 ? player.Name[..1].ToUpper() : "?")}
            </div>
            <div style='font-size:24px;font-weight:800;color:{style.TextColor};'>{Escape(player.Name)}</div>
            {(!string.IsNullOrEmpty(teamName) ? $"<div style='font-size:14px;color:{style.SubTextColor};margin-top:4px;'>{Escape(teamName)}</div>" : "")}
        </div>
        <div style='display:flex;justify-content:center;gap:16px;margin-top:16px;'>
            <div style='text-align:center;background:{style.BgColor2};border-radius:12px;padding:14px 20px;min-width:90px;'>
                <div style='font-size:28px;font-weight:800;color:{style.Accent};'>{played}</div>
                <div style='font-size:11px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:1px;'>Played</div>
            </div>
            <div style='text-align:center;background:{style.BgColor2};border-radius:12px;padding:14px 20px;min-width:90px;'>
                <div style='font-size:28px;font-weight:800;color:{style.Accent};'>{won}</div>
                <div style='font-size:11px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:1px;'>Won</div>
            </div>
            <div style='text-align:center;background:{style.BgColor2};border-radius:12px;padding:14px 20px;min-width:90px;'>
                <div style='font-size:28px;font-weight:800;color:{style.Accent};'>{winPct}%</div>
                <div style='font-size:11px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:1px;'>Win Rate</div>
            </div>
        </div>";
    }

    // ── Weekly Results ──

    private string GenerateWeeklyResultsContent(CardStyle style)
    {
        var divId = (DivisionPicker.SelectedItem as Division)?.Id;
        var fixtures = _seasonFixtures.Where(f => f.Frames.Count > 0).AsEnumerable();
        if (divId.HasValue && divId.Value != Guid.Empty)
            fixtures = fixtures.Where(f => f.DivisionId == divId.Value);

        var latest = fixtures.OrderByDescending(f => f.Date).Take(8).ToList();
        if (latest.Count == 0) return $"<p style='text-align:center;color:{style.SubTextColor}'>No results found</p>";

        var showDate = ShowDateCheck.IsChecked;
        var sb = new StringBuilder();

        if (showDate && latest.Count > 0)
            sb.Append($"<div style='text-align:center;font-size:13px;color:{style.SubTextColor};margin-bottom:16px;'>{latest.First().Date:dddd dd MMMM yyyy}</div>");

        foreach (var f in latest)
        {
            var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
            var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
            var homeWin = f.HomeScore > f.AwayScore;
            var awayWin = f.AwayScore > f.HomeScore;

            sb.Append($@"<div style='display:flex;align-items:center;padding:8px 12px;margin:3px 0;background:{style.BgColor2}40;border-radius:8px;'>
                <div style='flex:1;text-align:right;font-size:14px;font-weight:{(homeWin ? "700" : "400")};color:{style.TextColor};padding-right:10px;'>{Escape(home)}</div>
                <div style='font-size:18px;font-weight:800;color:{style.Accent};min-width:60px;text-align:center;'>{f.HomeScore} - {f.AwayScore}</div>
                <div style='flex:1;text-align:left;font-size:14px;font-weight:{(awayWin ? "700" : "400")};color:{style.TextColor};padding-left:10px;'>{Escape(away)}</div>
            </div>");
        }

        return sb.ToString();
    }

    // ── Upcoming Fixtures ──

    private string GenerateUpcomingFixturesContent(CardStyle style)
    {
        var divId = (DivisionPicker.SelectedItem as Division)?.Id;
        var fixtures = _seasonFixtures.Where(f => f.Frames.Count == 0 && f.Date >= DateTime.Today.AddDays(-1)).AsEnumerable();
        if (divId.HasValue && divId.Value != Guid.Empty)
            fixtures = fixtures.Where(f => f.DivisionId == divId.Value);

        var upcoming = fixtures.OrderBy(f => f.Date).Take(8).ToList();
        if (upcoming.Count == 0) return $"<p style='text-align:center;color:{style.SubTextColor}'>No upcoming fixtures</p>";

        var showDate = ShowDateCheck.IsChecked;
        var showVenue = ShowVenueCheck.IsChecked;
        var sb = new StringBuilder();

        if (showDate && upcoming.Count > 0)
            sb.Append($"<div style='text-align:center;font-size:13px;color:{style.SubTextColor};margin-bottom:16px;'>{upcoming.First().Date:dddd dd MMMM yyyy}</div>");

        foreach (var f in upcoming)
        {
            var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
            var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
            var venue = showVenue ? _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId)?.Name : null;

            sb.Append($@"<div style='display:flex;align-items:center;padding:8px 12px;margin:3px 0;background:{style.BgColor2}40;border-radius:8px;'>
                <div style='flex:1;text-align:right;font-size:14px;font-weight:600;color:{style.TextColor};padding-right:10px;'>{Escape(home)}</div>
                <div style='text-align:center;min-width:50px;'>
                    <div style='font-size:14px;font-weight:700;color:{style.Accent};'>VS</div>
                    <div style='font-size:10px;color:{style.SubTextColor};'>{f.Date:h:mm tt}</div>
                </div>
                <div style='flex:1;text-align:left;font-size:14px;font-weight:600;color:{style.TextColor};padding-left:10px;'>{Escape(away)}</div>
            </div>");

            if (!string.IsNullOrEmpty(venue))
                sb.Append($"<div style='text-align:center;font-size:10px;color:{style.SubTextColor};margin-bottom:4px;'>&#128205; {Escape(venue)}</div>");
        }

        return sb.ToString();
    }

    // ────────────────────── Helpers ──────────────────────

    private static string Escape(string text) =>
        System.Net.WebUtility.HtmlEncode(text ?? "");

    // ── Display item wrappers for Pickers ──

    private sealed class FixtureDisplayItem
    {
        public Fixture Fixture { get; }
        public string Display { get; }

        public FixtureDisplayItem(Fixture fixture, List<Team> teams)
        {
            Fixture = fixture;
            var home = teams.FirstOrDefault(t => t.Id == fixture.HomeTeamId)?.Name ?? "?";
            var away = teams.FirstOrDefault(t => t.Id == fixture.AwayTeamId)?.Name ?? "?";
            if (fixture.Frames.Count > 0)
                Display = $"{fixture.Date:dd MMM} — {home} {fixture.HomeScore}-{fixture.AwayScore} {away}";
            else
                Display = $"{fixture.Date:dd MMM} — {home} vs {away}";
        }
    }

    private sealed class PlayerDisplayItem
    {
        public Player Player { get; }
        public string Display { get; }

        public PlayerDisplayItem(Player player, List<Team> teams)
        {
            Player = player;
            var team = teams.FirstOrDefault(t => t.Id == player.TeamId)?.Name;
            Display = string.IsNullOrEmpty(team) ? player.Name : $"{player.Name} ({team})";
        }
    }

    private sealed record CardStyle(
        string BgColor, string BgColor2,
        string TextColor, string SubTextColor,
        string Accent, string Accent2);
}
