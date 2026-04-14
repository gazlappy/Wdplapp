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
    private readonly List<Competition> _seasonCompetitions = new();
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

        _seasonCompetitions.Clear();
        _seasonCompetitions.AddRange(League.Competitions.Where(c => c.SeasonId == id));

        _divisions.Clear();
        _divisions.Add(new Division { Name = "All Divisions", Id = Guid.Empty });
        foreach (var d in League.Divisions.Where(d => d.SeasonId == id))
            _divisions.Add(d);
        DivisionPicker.SelectedIndex = 0;

        PopulateMatchPicker();
        PopulatePlayerPicker();
        PopulateCompetitionPicker();
    }

    private void OnCardTypeChanged(object? sender, EventArgs e)
    {
        var cardType = CardTypePicker.SelectedItem?.ToString() ?? "";
        MatchSelectionFrame.IsVisible = cardType is "Result Card" or "Fixture Card";
        PlayerSelectionFrame.IsVisible = cardType == "Player Highlight";
        CompetitionSelectionFrame.IsVisible = cardType is "Competition Results" or "Competition Fixtures";
        PopulateMatchPicker();
        PopulateCompetitionPicker();
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
            items = fixtures.Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None))
                .OrderByDescending(f => f.Date)
                .Select(f => new FixtureDisplayItem(f, _seasonTeams))
                .ToList();
        }
        else
        {
            items = fixtures.Where(f => !f.Frames.Any(fr => fr.Winner != FrameWinner.None))
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

    private void PopulateCompetitionPicker()
    {
        var items = _seasonCompetitions
            .Where(c => c.Status != CompetitionStatus.Draft)
            .OrderByDescending(c => c.StartDate)
            .Select(c => new CompetitionDisplayItem(c))
            .ToList();
        CompetitionPicker.ItemsSource = items;
        CompetitionPicker.ItemDisplayBinding = new Binding("Display");
        if (items.Count > 0) CompetitionPicker.SelectedIndex = 0;
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
            WebViewHelper.LoadHtml(PreviewWebView, _generatedHtml);
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
        var path = await SaveCardAsImage();
        if (path == null) return;

        var shareText = GetShareText(includeUrl: true);
        await Clipboard.Default.SetTextAsync(shareText);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Share Social Card",
            File = new ShareFile(path)
        });
        ShowStatus("\u2705 Card shared — caption text also copied to clipboard!");
    }

    private async void OnSaveImageClicked(object? sender, EventArgs e)
    {
        var path = await SaveCardAsImage();
        if (path == null) return;

        ShowStatus($"Saved to {Path.GetFileName(path)}");
        await DisplayAlert("Saved", $"Card image saved to:\n{path}", "OK");
    }

    private async void OnPostFacebookClicked(object? sender, EventArgs e)
    {
        var path = await SaveCardAsImage();
        if (path == null) return;

        var shareText = GetShareText(includeUrl: true);
        await Clipboard.Default.SetTextAsync(shareText);

        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Post to Facebook",
            File = new ShareFile(path)
        });
        ShowStatus("\u2705 Card shared — caption text also copied to clipboard!");
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
        var path = await SaveCardAsImage();
        if (path == null) return;

        var shareText = GetShareText(includeUrl: true);
        await Clipboard.Default.SetTextAsync(shareText);

        ShowStatus("Card saved & text copied — paste into Instagram!");
        await DisplayAlert("Instagram",
            "Instagram doesn't support direct posting from desktop apps.\n\n" +
            "Your card image has been saved and the post text copied to your clipboard.\n\n" +
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

    private async Task<string?> SaveCardAsImage()
    {
        EnsureCardGenerated();
        if (!PreviewWebView.IsVisible || string.IsNullOrEmpty(_generatedHtml))
        {
            ShowStatus("Generate a card first", true);
            return null;
        }

        try
        {
            ShowStatus("Capturing card image...");

            // Trigger html2canvas capture inside the WebView
            await PreviewWebView.EvaluateJavaScriptAsync("startCapture()");

            // Poll for the async result
            string result = "";
            for (int i = 0; i < 100; i++) // 10-second timeout
            {
                await Task.Delay(100);
                result = await PreviewWebView.EvaluateJavaScriptAsync("getCaptureResult()") ?? "";
                // EvaluateJavaScriptAsync returns JSON-encoded strings — strip surrounding quotes
                if (result.Length >= 2 && result[0] == '"' && result[^1] == '"')
                    result = result[1..^1];
                if (!string.IsNullOrEmpty(result) && result != "PENDING")
                    break;
            }

            if (string.IsNullOrEmpty(result) || result == "PENDING")
            {
                ShowStatus("Card capture timed out \u2014 try again", true);
                return null;
            }

            if (result.StartsWith("ERROR:"))
            {
                ShowStatus(result[6..], true);
                return null;
            }

            // Strip the data-URL prefix and decode the base64 PNG
            const string prefix = "data:image/png;base64,";
            if (result.StartsWith(prefix))
                result = result[prefix.Length..];

            var imageBytes = Convert.FromBase64String(result);

            var cardType = CardTypePicker.SelectedItem?.ToString() ?? "card";
            var safeName = string.Join("_", cardType.Split(Path.GetInvalidFileNameChars()));
            var fileName = $"SocialCard_{safeName}_{DateTime.Now:yyyyMMdd_HHmmss}.png";
            var dir = Path.Combine(FileSystem.CacheDirectory, "SocialCards");
            Directory.CreateDirectory(dir);
            var filePath = Path.Combine(dir, fileName);

            await File.WriteAllBytesAsync(filePath, imageBytes);
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
        else if (cardType is "Competition Fixtures" && CompetitionPicker.SelectedItem is CompetitionDisplayItem compFixItem)
        {
            var comp = compFixItem.Competition;
            sb.AppendLine($"\U0001F4C5 {settings.LeagueName} \u2014 {comp.Name}");
            sb.AppendLine();
            sb.AppendLine("Upcoming Matches");
        }
        else if (cardType is "Competition Results" && CompetitionPicker.SelectedItem is CompetitionDisplayItem compItem)
        {
            var comp = compItem.Competition;
            var formatLabel = comp.Format switch
            {
                CompetitionFormat.SinglesKnockout => "Singles Knockout",
                CompetitionFormat.DoublesKnockout => "Doubles Knockout",
                CompetitionFormat.TeamKnockout => "Team Knockout",
                CompetitionFormat.RoundRobin => "Round Robin",
                CompetitionFormat.Swiss => "Swiss",
                CompetitionFormat.SinglesGroupStage => "Singles Group Stage",
                CompetitionFormat.DoublesGroupStage => "Doubles Group Stage",
                _ => ""
            };
            sb.AppendLine($"\U0001F3C6 {settings.LeagueName} \u2014 {comp.Name}");
            if (!string.IsNullOrEmpty(formatLabel))
            {
                sb.AppendLine();
                sb.AppendLine($"\U0001F3B1 {formatLabel}");
            }
            if (comp.Status == CompetitionStatus.Completed)
            {
                var finalRound = comp.Rounds.OrderByDescending(r => r.RoundNumber).FirstOrDefault();
                var finalMatch = finalRound?.Matches.FirstOrDefault(m => m.IsComplete && m.WinnerId.HasValue);
                if (finalMatch != null)
                {
                    var winnerName = GetParticipantName(finalMatch.WinnerId, comp);
                    if (!string.IsNullOrEmpty(winnerName))
                    {
                        sb.AppendLine();
                        sb.AppendLine($"\U0001F947 Winner: {winnerName}");
                    }
                }
            }
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
            "Competition Results" => GenerateCompetitionCardContent(style),
            "Competition Fixtures" => GenerateCompetitionFixturesContent(style),
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
                logoHtml = $"<img src='data:image/png;base64,{b64}' style='max-height:120px;max-width:360px;margin-bottom:16px;'/>";
            }
        }

        var headerSection = $@"
            <div style='text-align:center;padding:48px 40px 24px;'>
                {logoHtml}
                <div style='font-size:44px;font-weight:800;color:{style.TextColor};letter-spacing:0.5px;'>{Escape(leagueName)}</div>
                {(string.IsNullOrWhiteSpace(headline) ? "" : $"<div style='font-size:28px;color:{style.SubTextColor};margin-top:8px;'>{Escape(headline)}</div>")}
            </div>";

        var footerSection = "";
        if (!string.IsNullOrWhiteSpace(footer) || (showWebsite && !string.IsNullOrWhiteSpace(websiteUrl)))
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(footer)) parts.Add(Escape(footer));
            if (showWebsite && !string.IsNullOrWhiteSpace(websiteUrl)) parts.Add(Escape(websiteUrl));
            footerSection = $@"
            <div style='text-align:center;padding:24px 40px 40px;font-size:24px;color:{style.SubTextColor};'>
                {string.Join(" &bull; ", parts)}
            </div>";
        }

        return $@"<!DOCTYPE html>
<html><head><meta charset='utf-8'/>
<meta name='viewport' content='width=device-width,initial-scale=1'/>
<script src='https://cdnjs.cloudflare.com/ajax/libs/html2canvas/1.4.1/html2canvas.min.js'></script>
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
  .card-content {{ flex:1; display:flex; flex-direction:column; justify-content:center; padding:0 48px; }}
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
  window._captureData = '';
  function startCapture() {{
    window._captureData = 'PENDING';
    if (typeof html2canvas === 'undefined') {{
      window._captureData = 'ERROR:Image capture library not loaded. Check your internet connection.';
      return;
    }}
    var wrap = document.getElementById('cardWrap');
    var saved = wrap.style.transform;
    wrap.style.transform = 'none';
    html2canvas(document.querySelector('.card'), {{ scale: 1, useCORS: true, logging: false }})
      .then(function(c) {{ wrap.style.transform = saved; window._captureData = c.toDataURL('image/png'); }})
      .catch(function(e) {{ wrap.style.transform = saved; window._captureData = 'ERROR:' + e.message; }});
  }}
  function getCaptureResult() {{ return window._captureData || ''; }}
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
        {(showDiv ? $"<div style='text-align:center;font-size:26px;color:{style.Accent};font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:20px;'>{Escape(division)}</div>" : "")}
        {(showDate ? $"<div style='text-align:center;font-size:26px;color:{style.SubTextColor};margin-bottom:24px;'>{f.Date:dddd dd MMMM yyyy}</div>" : "")}
        <div style='display:flex;align-items:center;justify-content:center;gap:30px;padding:24px 0;'>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:40px;font-weight:700;color:{style.TextColor};{(homeWin ? "text-shadow:0 0 8px " + style.Accent + "40;" : "")}'>{Escape(home)}</div>
            </div>
            <div style='text-align:center;'>
                <div style='font-size:96px;font-weight:900;color:{style.TextColor};letter-spacing:4px;'>
                    <span style='{(homeWin ? "color:" + style.Accent + ";" : "")}'>{f.HomeScore}</span>
                    <span style='color:{style.SubTextColor};margin:0 6px;'>-</span>
                    <span style='{(awayWin ? "color:" + style.Accent + ";" : "")}'>{f.AwayScore}</span>
                </div>
                <div style='font-size:22px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:2px;margin-top:8px;'>Final Score</div>
            </div>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:40px;font-weight:700;color:{style.TextColor};{(awayWin ? "text-shadow:0 0 8px " + style.Accent + "40;" : "")}'>{Escape(away)}</div>
            </div>
        </div>
        {(showVenue ? $"<div style='text-align:center;font-size:24px;color:{style.SubTextColor};margin-top:24px;'>&#128205; {Escape(venue)}</div>" : "")}";
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
        {(showDiv ? $"<div style='text-align:center;font-size:26px;color:{style.Accent};font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin-bottom:20px;'>{Escape(division)}</div>" : "")}
        {(showDate ? $"<div style='text-align:center;font-size:26px;color:{style.SubTextColor};margin-bottom:30px;'>{f.Date:dddd dd MMMM yyyy}</div>" : "")}
        <div style='display:flex;align-items:center;justify-content:center;gap:30px;padding:30px 0;'>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:44px;font-weight:700;color:{style.TextColor};'>{Escape(home)}</div>
            </div>
            <div style='text-align:center;'>
                <div style='font-size:56px;font-weight:800;color:{style.Accent};'>VS</div>
                <div style='font-size:28px;font-weight:600;color:{style.TextColor};margin-top:8px;'>{f.Date:h:mm tt}</div>
            </div>
            <div style='flex:1;text-align:center;'>
                <div style='font-size:44px;font-weight:700;color:{style.TextColor};'>{Escape(away)}</div>
            </div>
        </div>
        {(showVenue ? $"<div style='text-align:center;font-size:24px;color:{style.SubTextColor};margin-top:24px;'>&#128205; {Escape(venue)}</div>" : "")}";
    }

    // ── League Table ──

    private string GenerateLeagueTableContent(CardStyle style)
    {
        var season = SeasonPicker.SelectedItem as Season;
        if (season == null) return "";

        var divId = (DivisionPicker.SelectedItem as Division)?.Id;
        var isAllDivisions = !divId.HasValue || divId.Value == Guid.Empty;

        if (isAllDivisions)
        {
            // Generate a separate table for each division
            var realDivisions = _divisions.Where(d => d.Id != Guid.Empty).ToList();
            if (realDivisions.Count == 0)
                return $"<p style='text-align:center;color:{style.SubTextColor}'>No divisions found</p>";

            // Count divisions that actually have teams
            var activeDivs = realDivisions
                .Where(d => _seasonTeams.Any(t => t.DivisionId == d.Id))
                .ToList();
            var divCount = Math.Max(activeDivs.Count, 1);

            var sb = new StringBuilder();
            foreach (var div in activeDivs)
            {
                var divTeams = _seasonTeams.Where(t => t.DivisionId == div.Id).ToList();
                var divFixtures = _seasonFixtures.Where(f => f.DivisionId == div.Id).ToList();

                sb.Append(BuildLeagueTableHtml(style, season, divTeams, divFixtures, div.Name, divCount));
            }

            return sb.Length > 0
                ? sb.ToString()
                : $"<p style='text-align:center;color:{style.SubTextColor}'>No teams found</p>";
        }
        else
        {
            var teams = _seasonTeams.Where(t => t.DivisionId == divId!.Value).ToList();
            var fixtures = _seasonFixtures.Where(f => f.DivisionId == divId!.Value).ToList();
            if (teams.Count == 0)
                return $"<p style='text-align:center;color:{style.SubTextColor}'>No teams found</p>";

            var divName = _divisions.FirstOrDefault(d => d.Id == divId)?.Name ?? "";
            var showDiv = ShowDivisionCheck.IsChecked && !string.IsNullOrEmpty(divName) && divName != "All Divisions";
            return BuildLeagueTableHtml(style, season, teams, fixtures, showDiv ? divName : null, 1);
        }
    }

    private string BuildLeagueTableHtml(CardStyle style, Season season,
        List<Team> teamList, List<Fixture> fixtureList, string? divisionName, int totalDivisions)
    {
        var appSettings = League.GetSettingsForSeason(season.Id);
        var standings = StandingsCalculator.Calculate(teamList, fixtureList, appSettings);
        var sorted = StandingsSorter.Sort(
            standings, appSettings,
            s => s.Points, s => s.FramesFor, s => s.FramesAgainst, s => s.Won, s => s.TeamId,
            fixtureList);

        for (int i = 0; i < sorted.Count; i++) sorted[i].Position = i + 1;

        // Scale down sizing when multiple divisions must share the card
        var compact = totalDivisions > 1;
        var fontSize = compact ? "18px" : "26px";
        var headerPad = compact ? "8px 6px" : "14px 10px";
        var cellPad = compact ? "6px 6px" : "12px 10px";
        var divFontSize = compact ? "18px" : "26px";
        var divMargin = compact ? "14px 0 8px" : "0 0 20px";
        var moreSize = compact ? "15px" : "22px";
        var maxRows = compact
            ? Math.Min(sorted.Count, Math.Max(12 / totalDivisions, 5))
            : Math.Min(sorted.Count, 12);

        var sb = new StringBuilder();
        if (!string.IsNullOrEmpty(divisionName))
            sb.Append($"<div style='text-align:center;font-size:{divFontSize};color:{style.Accent};font-weight:700;letter-spacing:1.5px;text-transform:uppercase;margin:{divMargin};'>{Escape(divisionName)}</div>");

        sb.Append($@"<table style='width:100%;border-collapse:collapse;font-size:{fontSize};color:{style.TextColor};'>
            <tr style='border-bottom:2px solid {style.Accent};'>
                <th style='padding:{headerPad};text-align:center;width:40px;'>#</th>
                <th style='padding:{headerPad};text-align:left;'>Team</th>
                <th style='padding:{headerPad};text-align:center;'>P</th>
                <th style='padding:{headerPad};text-align:center;'>W</th>
                <th style='padding:{headerPad};text-align:center;'>L</th>
                <th style='padding:{headerPad};text-align:center;'>FD</th>
                <th style='padding:{headerPad};text-align:center;font-weight:800;'>Pts</th>
            </tr>");

        for (int i = 0; i < maxRows; i++)
        {
            var s = sorted[i];
            var rowBg = i % 2 == 0 ? "transparent" : style.BgColor2 + "40";
            var isTop3 = i < 3;
            var posIcon = i switch { 0 => "&#129351;", 1 => "&#129352;", 2 => "&#129353;", _ => s.Position.ToString() };
            sb.Append($@"<tr style='background:{rowBg};{(isTop3 ? "font-weight:600;" : "")}'>
                <td style='padding:{cellPad};text-align:center;'>{posIcon}</td>
                <td style='padding:{cellPad};text-align:left;{(isTop3 ? "color:" + style.Accent + ";" : "")}'>{Escape(s.TeamName)}</td>
                <td style='padding:{cellPad};text-align:center;'>{s.Played}</td>
                <td style='padding:{cellPad};text-align:center;'>{s.Won}</td>
                <td style='padding:{cellPad};text-align:center;'>{s.Lost}</td>
                <td style='padding:{cellPad};text-align:center;'>{s.FrameDifference:+0;-0;0}</td>
                <td style='padding:{cellPad};text-align:center;font-weight:800;'>{s.Points}</td>
            </tr>");
        }

        if (sorted.Count > maxRows)
            sb.Append($"<tr><td colspan='7' style='padding:{cellPad};text-align:center;color:{style.SubTextColor};font-size:{moreSize};'>+ {sorted.Count - maxRows} more teams</td></tr>");

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

        var playerFixtures = _seasonFixtures.Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None));
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
        <div style='text-align:center;padding:24px 0;'>
            <div style='width:140px;height:140px;border-radius:50%;background:{style.Accent};margin:0 auto 20px;display:flex;align-items:center;justify-content:center;font-size:64px;color:white;font-weight:800;'>
                {Escape(player.Name.Length > 0 ? player.Name[..1].ToUpper() : "?")}
            </div>
            <div style='font-size:44px;font-weight:800;color:{style.TextColor};'>{Escape(player.Name)}</div>
            {(!string.IsNullOrEmpty(teamName) ? $"<div style='font-size:28px;color:{style.SubTextColor};margin-top:8px;'>{Escape(teamName)}</div>" : "")}
        </div>
        <div style='display:flex;justify-content:center;gap:24px;margin-top:24px;'>
            <div style='text-align:center;background:{style.BgColor2};border-radius:16px;padding:24px 36px;min-width:160px;'>
                <div style='font-size:52px;font-weight:800;color:{style.Accent};'>{played}</div>
                <div style='font-size:22px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:1px;'>Played</div>
            </div>
            <div style='text-align:center;background:{style.BgColor2};border-radius:16px;padding:24px 36px;min-width:160px;'>
                <div style='font-size:52px;font-weight:800;color:{style.Accent};'>{won}</div>
                <div style='font-size:22px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:1px;'>Won</div>
            </div>
            <div style='text-align:center;background:{style.BgColor2};border-radius:16px;padding:24px 36px;min-width:160px;'>
                <div style='font-size:52px;font-weight:800;color:{style.Accent};'>{winPct}%</div>
                <div style='font-size:22px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:1px;'>Win Rate</div>
            </div>
        </div>";
    }

    // ── Weekly Results ──

    private string GenerateWeeklyResultsContent(CardStyle style)
    {
        var divId = (DivisionPicker.SelectedItem as Division)?.Id;
        var fixtures = _seasonFixtures.Where(f => f.Frames.Any(fr => fr.Winner != FrameWinner.None)).AsEnumerable();
        if (divId.HasValue && divId.Value != Guid.Empty)
            fixtures = fixtures.Where(f => f.DivisionId == divId.Value);

        var latest = fixtures.OrderByDescending(f => f.Date).Take(8).ToList();
        if (latest.Count == 0) return $"<p style='text-align:center;color:{style.SubTextColor}'>No results found</p>";

        var showDate = ShowDateCheck.IsChecked;
        var sb = new StringBuilder();

        if (showDate && latest.Count > 0)
            sb.Append($"<div style='text-align:center;font-size:26px;color:{style.SubTextColor};margin-bottom:24px;'>{latest.First().Date:dddd dd MMMM yyyy}</div>");

        foreach (var f in latest)
        {
            var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
            var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
            var homeWin = f.HomeScore > f.AwayScore;
            var awayWin = f.AwayScore > f.HomeScore;

            sb.Append($@"<div style='display:flex;align-items:center;padding:14px 20px;margin:4px 0;background:{style.BgColor2}40;border-radius:10px;'>
                <div style='flex:1;text-align:right;font-size:28px;font-weight:{(homeWin ? "700" : "400")};color:{style.TextColor};padding-right:16px;'>{Escape(home)}</div>
                <div style='font-size:34px;font-weight:800;color:{style.Accent};min-width:100px;text-align:center;'>{f.HomeScore} - {f.AwayScore}</div>
                <div style='flex:1;text-align:left;font-size:28px;font-weight:{(awayWin ? "700" : "400")};color:{style.TextColor};padding-left:16px;'>{Escape(away)}</div>
            </div>");
        }

        return sb.ToString();
    }

    // ── Upcoming Fixtures ──

    private string GenerateUpcomingFixturesContent(CardStyle style)
    {
        var divId = (DivisionPicker.SelectedItem as Division)?.Id;
        var unplayed = _seasonFixtures.Where(f => !f.Frames.Any(fr => fr.Winner != FrameWinner.None)).AsEnumerable();
        if (divId.HasValue && divId.Value != Guid.Empty)
            unplayed = unplayed.Where(f => f.DivisionId == divId.Value);

        // Find the nearest match date among all unplayed fixtures, then show that round
        var ordered = unplayed.OrderBy(f => f.Date).ToList();
        if (ordered.Count == 0) return $"<p style='text-align:center;color:{style.SubTextColor}'>No upcoming fixtures</p>";

        var nextDate = ordered.First().Date.Date;
        var upcoming = ordered.Where(f => f.Date.Date == nextDate).Take(8).ToList();
        if (upcoming.Count == 0) return $"<p style='text-align:center;color:{style.SubTextColor}'>No upcoming fixtures</p>";

        var showDate = ShowDateCheck.IsChecked;
        var showVenue = ShowVenueCheck.IsChecked;
        var sb = new StringBuilder();

        if (showDate && upcoming.Count > 0)
            sb.Append($"<div style='text-align:center;font-size:26px;color:{style.SubTextColor};margin-bottom:24px;'>{upcoming.First().Date:dddd dd MMMM yyyy}</div>");

        foreach (var f in upcoming)
        {
            var home = _seasonTeams.FirstOrDefault(t => t.Id == f.HomeTeamId)?.Name ?? "Home";
            var away = _seasonTeams.FirstOrDefault(t => t.Id == f.AwayTeamId)?.Name ?? "Away";
            var venue = showVenue ? _seasonVenues.FirstOrDefault(v => v.Id == f.VenueId)?.Name : null;

            sb.Append($@"<div style='display:flex;align-items:center;padding:14px 20px;margin:4px 0;background:{style.BgColor2}40;border-radius:10px;'>
                <div style='flex:1;text-align:right;font-size:28px;font-weight:600;color:{style.TextColor};padding-right:16px;'>{Escape(home)}</div>
                <div style='text-align:center;min-width:90px;'>
                    <div style='font-size:28px;font-weight:700;color:{style.Accent};'>VS</div>
                    <div style='font-size:20px;color:{style.SubTextColor};'>{f.Date:h:mm tt}</div>
                </div>
                <div style='flex:1;text-align:left;font-size:28px;font-weight:600;color:{style.TextColor};padding-left:16px;'>{Escape(away)}</div>
            </div>");

            if (!string.IsNullOrEmpty(venue))
                sb.Append($"<div style='text-align:center;font-size:20px;color:{style.SubTextColor};margin-bottom:6px;'>&#128205; {Escape(venue)}</div>");
        }

        return sb.ToString();
    }

    // ── Competition Results ──

    private string GenerateCompetitionCardContent(CardStyle style)
    {
        if (CompetitionPicker.SelectedItem is not CompetitionDisplayItem item)
            return $"<p style='text-align:center;color:{style.SubTextColor}'>Select a competition first</p>";

        var comp = item.Competition;
        var formatLabel = comp.Format switch
        {
            CompetitionFormat.SinglesKnockout => "Singles Knockout",
            CompetitionFormat.DoublesKnockout => "Doubles Knockout",
            CompetitionFormat.TeamKnockout => "Team Knockout",
            CompetitionFormat.RoundRobin => "Round Robin",
            CompetitionFormat.Swiss => "Swiss",
            CompetitionFormat.SinglesGroupStage => "Singles Group Stage",
            CompetitionFormat.DoublesGroupStage => "Doubles Group Stage",
            _ => ""
        };

        var sb = new StringBuilder();

        // Competition name and format badge
        sb.Append($"<div style='text-align:center;margin-bottom:20px;'>");
        sb.Append($"<div style='font-size:36px;font-weight:800;color:{style.TextColor};'>{Escape(comp.Name)}</div>");
        sb.Append($"<div style='display:inline-block;background:{style.Accent}30;color:{style.Accent};padding:6px 18px;border-radius:20px;font-size:20px;font-weight:600;margin-top:10px;'>{Escape(formatLabel)}</div>");
        if (comp.Status == CompetitionStatus.Completed)
            sb.Append($"<div style='display:inline-block;background:#10B98130;color:#10B981;padding:6px 18px;border-radius:20px;font-size:20px;font-weight:600;margin-top:10px;margin-left:8px;'>Completed</div>");
        sb.Append("</div>");

        // Show winner if completed
        if (comp.Status == CompetitionStatus.Completed)
        {
            var finalRound = comp.Rounds.OrderByDescending(r => r.RoundNumber).FirstOrDefault();
            var finalMatch = finalRound?.Matches.FirstOrDefault(m => m.IsComplete && m.WinnerId.HasValue);
            if (finalMatch != null)
            {
                var winnerName = GetParticipantName(finalMatch.WinnerId, comp);
                var runnerUpId = finalMatch.WinnerId == finalMatch.Participant1Id
                    ? finalMatch.Participant2Id
                    : finalMatch.Participant1Id;
                var runnerUpName = GetParticipantName(runnerUpId, comp);

                sb.Append($"<div style='text-align:center;padding:20px 0;'>");
                sb.Append($"<div style='font-size:52px;margin-bottom:8px;'>&#127942;</div>");
                sb.Append($"<div style='font-size:36px;font-weight:800;color:{style.Accent};'>{Escape(winnerName ?? "Winner")}</div>");
                sb.Append($"<div style='font-size:22px;color:{style.SubTextColor};margin-top:6px;'>defeated {Escape(runnerUpName ?? "?")} {finalMatch.Participant1Score} - {finalMatch.Participant2Score}</div>");
                sb.Append("</div>");
            }
        }

        // Show latest round matches
        var latestRound = comp.Rounds
            .Where(r => r.Matches.Any(m => m.IsComplete))
            .OrderByDescending(r => r.RoundNumber)
            .FirstOrDefault();

        if (latestRound != null)
        {
            var completedMatches = latestRound.Matches
                .Where(m => m.IsComplete && m.Participant1Id.HasValue && m.Participant2Id.HasValue)
                .Take(8)
                .ToList();

            if (completedMatches.Count > 0)
            {
                if (comp.Status != CompetitionStatus.Completed)
                    sb.Append($"<div style='text-align:center;font-size:24px;color:{style.Accent};font-weight:700;letter-spacing:1px;text-transform:uppercase;margin:16px 0 12px;'>{Escape(latestRound.Name)}</div>");

                foreach (var m in completedMatches)
                {
                    var p1 = GetParticipantName(m.Participant1Id, comp) ?? "?";
                    var p2 = GetParticipantName(m.Participant2Id, comp) ?? "?";
                    var p1Win = m.WinnerId == m.Participant1Id;
                    var p2Win = m.WinnerId == m.Participant2Id;

                    sb.Append($@"<div style='display:flex;align-items:center;padding:12px 16px;margin:4px 0;background:{style.BgColor2}40;border-radius:10px;'>
                        <div style='flex:1;text-align:right;font-size:24px;font-weight:{(p1Win ? "700" : "400")};color:{style.TextColor};padding-right:12px;{(p1Win ? "color:" + style.Accent + ";" : "")}'>{Escape(p1)}</div>
                        <div style='font-size:28px;font-weight:800;color:{style.Accent};min-width:80px;text-align:center;'>{m.Participant1Score} - {m.Participant2Score}</div>
                        <div style='flex:1;text-align:left;font-size:24px;font-weight:{(p2Win ? "700" : "400")};color:{style.TextColor};padding-left:12px;{(p2Win ? "color:" + style.Accent + ";" : "")}'>{Escape(p2)}</div>
                    </div>");
                }

                var totalInRound = latestRound.Matches.Count(m => m.IsComplete && m.Participant1Id.HasValue && m.Participant2Id.HasValue);
                if (totalInRound > 8)
                    sb.Append($"<div style='text-align:center;font-size:20px;color:{style.SubTextColor};margin-top:8px;'>+ {totalInRound - 8} more matches</div>");
            }
        }
        else if (comp.Status != CompetitionStatus.Completed)
        {
            // No completed matches yet — show participant count
            var count = comp.Format is CompetitionFormat.DoublesKnockout or CompetitionFormat.DoublesGroupStage
                ? comp.DoublesTeams.Count
                : comp.ParticipantIds.Count;
            sb.Append($"<div style='text-align:center;padding:30px 0;'>");
            sb.Append($"<div style='font-size:52px;font-weight:800;color:{style.Accent};'>{count}</div>");
            sb.Append($"<div style='font-size:24px;color:{style.SubTextColor};text-transform:uppercase;letter-spacing:1px;'>Participants</div>");
            sb.Append("</div>");
        }

        return sb.ToString();
    }

    // ── Competition Fixtures ──

    private string GenerateCompetitionFixturesContent(CardStyle style)
    {
        if (CompetitionPicker.SelectedItem is not CompetitionDisplayItem item)
            return $"<p style='text-align:center;color:{style.SubTextColor}'>Select a competition first</p>";

        var comp = item.Competition;
        var formatLabel = comp.Format switch
        {
            CompetitionFormat.SinglesKnockout => "Singles Knockout",
            CompetitionFormat.DoublesKnockout => "Doubles Knockout",
            CompetitionFormat.TeamKnockout => "Team Knockout",
            CompetitionFormat.RoundRobin => "Round Robin",
            CompetitionFormat.Swiss => "Swiss",
            CompetitionFormat.SinglesGroupStage => "Singles Group Stage",
            CompetitionFormat.DoublesGroupStage => "Doubles Group Stage",
            _ => ""
        };

        var sb = new StringBuilder();

        // Competition name and format badge
        sb.Append($"<div style='text-align:center;margin-bottom:20px;'>");
        sb.Append($"<div style='font-size:36px;font-weight:800;color:{style.TextColor};'>{Escape(comp.Name)}</div>");
        sb.Append($"<div style='display:inline-block;background:{style.Accent}30;color:{style.Accent};padding:6px 18px;border-radius:20px;font-size:20px;font-weight:600;margin-top:10px;'>{Escape(formatLabel)}</div>");
        sb.Append("</div>");

        // Find the earliest round with unplayed matches (where both participants are known)
        var upcomingRound = comp.Rounds
            .Where(r => r.Matches.Any(m => !m.IsComplete && m.Participant1Id.HasValue && m.Participant2Id.HasValue))
            .OrderBy(r => r.RoundNumber)
            .FirstOrDefault();

        if (upcomingRound != null)
        {
            var upcomingMatches = upcomingRound.Matches
                .Where(m => !m.IsComplete && m.Participant1Id.HasValue && m.Participant2Id.HasValue)
                .Take(8)
                .ToList();

            // Round name and date
            sb.Append($"<div style='text-align:center;font-size:24px;color:{style.Accent};font-weight:700;letter-spacing:1px;text-transform:uppercase;margin:16px 0 4px;'>{Escape(upcomingRound.Name)}</div>");
            if (upcomingRound.Date.HasValue)
                sb.Append($"<div style='text-align:center;font-size:20px;color:{style.SubTextColor};margin-bottom:12px;'>{upcomingRound.Date.Value:dddd dd MMMM yyyy}</div>");
            else
                sb.Append("<div style='margin-bottom:12px;'></div>");

            foreach (var m in upcomingMatches)
            {
                var p1 = GetParticipantName(m.Participant1Id, comp) ?? "?";
                var p2 = GetParticipantName(m.Participant2Id, comp) ?? "?";
                var venueHtml = !string.IsNullOrEmpty(m.VenueDisplay)
                    ? $"<div style='font-size:16px;color:{style.SubTextColor};margin-top:2px;'>&#128205; {Escape(m.VenueDisplay)}</div>"
                    : "";

                sb.Append($@"<div style='padding:12px 16px;margin:4px 0;background:{style.BgColor2}40;border-radius:10px;'>
                    <div style='display:flex;align-items:center;'>
                        <div style='flex:1;text-align:right;font-size:24px;font-weight:600;color:{style.TextColor};padding-right:12px;'>{Escape(p1)}</div>
                        <div style='font-size:28px;font-weight:700;color:{style.Accent};min-width:60px;text-align:center;'>VS</div>
                        <div style='flex:1;text-align:left;font-size:24px;font-weight:600;color:{style.TextColor};padding-left:12px;'>{Escape(p2)}</div>
                    </div>
                    {venueHtml}
                </div>");
            }

            var totalUpcoming = upcomingRound.Matches.Count(m => !m.IsComplete && m.Participant1Id.HasValue && m.Participant2Id.HasValue);
            if (totalUpcoming > 8)
                sb.Append($"<div style='text-align:center;font-size:20px;color:{style.SubTextColor};margin-top:8px;'>+ {totalUpcoming - 8} more matches</div>");
        }
        else
        {
            sb.Append($"<div style='text-align:center;padding:30px 0;'>");
            sb.Append($"<div style='font-size:36px;color:{style.SubTextColor};'>No upcoming matches</div>");
            sb.Append("</div>");
        }

        return sb.ToString();
    }

    private string? GetParticipantName(Guid? id, Competition comp)
    {
        if (!id.HasValue) return null;

        if (comp.Format is CompetitionFormat.DoublesKnockout or CompetitionFormat.DoublesGroupStage)
            return comp.DoublesTeams.FirstOrDefault(t => t.Id == id.Value)?.TeamName;

        if (comp.Format == CompetitionFormat.TeamKnockout)
            return _seasonTeams.FirstOrDefault(t => t.Id == id.Value)?.Name;

        return _seasonPlayers.FirstOrDefault(p => p.Id == id.Value)?.Name;
    }

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
            if (fixture.Frames.Any(fr => fr.Winner != FrameWinner.None))
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

    private sealed class CompetitionDisplayItem
    {
        public Competition Competition { get; }
        public string Display { get; }

        public CompetitionDisplayItem(Competition competition)
        {
            Competition = competition;
            var status = competition.Status switch
            {
                CompetitionStatus.Completed => " ✅",
                CompetitionStatus.InProgress => " 🔴",
                _ => ""
            };
            Display = $"{competition.Name}{status}";
        }
    }

    private sealed record CardStyle(
        string BgColor, string BgColor2,
        string TextColor, string SubTextColor,
        string Accent, string Accent2);
}
