using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Helpers;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class FixturesSheetPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<DivisionItem> _divisions = new();
    private readonly ObservableCollection<EventItem> _events = new();
    private string? _generatedHtml;
    private string? _logoBase64;

    public FixturesSheetPage()
    {
        InitializeComponent();

        SeasonPicker.ItemsSource = _seasons;
        SeasonPicker.ItemDisplayBinding = new Binding("Name");
        SeasonPicker.SelectedIndexChanged += OnSeasonChanged;

        DivisionsCollection.ItemsSource = _divisions;
        EventsCollection.ItemsSource = _events;

        LoadData();
    }

    private void LoadData()
    {
        _seasons.Clear();
        foreach (var s in League.Seasons.OrderByDescending(s => s.StartDate))
            _seasons.Add(s);

        var active = _seasons.FirstOrDefault(s => s.IsActive) ?? _seasons.FirstOrDefault();
        if (active != null)
            SeasonPicker.SelectedItem = active;

        var ws = League.WebsiteSettings;

        // Load saved fixtures sheet settings
        var fs = League.FixturesSheetSettings;
        TitleEntry.Text = fs.Title;
        ShowTeamNumbersCheck.IsChecked = fs.ShowTeamNumbers;
        ShowDivisionListsCheck.IsChecked = fs.ShowDivisionLists;
        ShowVenueInfoCheck.IsChecked = fs.ShowVenueInfo;
        ShowSpecialEventsCheck.IsChecked = fs.ShowSpecialEvents;
        ShowFooterCheck.IsChecked = fs.ShowFooterNotes;
        LandscapeRadio.IsChecked = fs.IsLandscape;
        PortraitRadio.IsChecked = !fs.IsLandscape;
        FooterNotesEntry.Text = fs.FooterNotes;
        ExtraFooterNotesContainer.Children.Clear();
        foreach (var note in fs.ExtraFooterNotes)
            AddExtraFooterNoteRow(note);
        WebsiteUrlEntry.Text = fs.FooterWebsite ?? ws.WebsiteUrl;
        EmailEntry.Text = fs.FooterEmail ?? ws.ContactEmail;
        ContactNameEntry.Text = fs.FooterContactName;
        ContactPhoneEntry.Text = fs.FooterContactPhone;
        ReportNameEntry.Text = fs.FooterReportName;
        ReportPhoneEntry.Text = fs.FooterReportPhone;
        _logoBase64 = fs.LogoBase64;
        LogoStatusLabel.Text = string.IsNullOrWhiteSpace(_logoBase64) ? "No logo" : "Logo set ✓";

        // Load special events
        _events.Clear();
        foreach (var e in fs.SpecialEvents.OrderBy(e => e.Date))
            _events.Add(new EventItem { Date = e.Date, Description = e.Description, Color = e.Color, DayOfWeek = e.DayOfWeek });
    }

    private void OnSeasonChanged(object? sender, EventArgs e)
    {
        _divisions.Clear();
        if (SeasonPicker.SelectedItem is not Season season) return;

        var (divisions, _, _, _, _) = League.GetSeasonData(season.Id);
        foreach (var d in divisions)
            _divisions.Add(new DivisionItem { Id = d.Id, Name = d.Name, IsSelected = true });

        SyncExclusionDates(season);
    }

    private FixturesSheetSettings BuildSettings()
    {
        var ws = League.WebsiteSettings;
        return new FixturesSheetSettings
        {
            LeagueName = ws.LeagueName,
            SeasonName = (SeasonPicker.SelectedItem as Season)?.Name ?? "",
            Title = TitleEntry.Text,
            ShowTeamNumbers = ShowTeamNumbersCheck.IsChecked,
            ShowDivisionLists = ShowDivisionListsCheck.IsChecked,
            ShowVenueInfo = ShowVenueInfoCheck.IsChecked,
            ShowSpecialEvents = ShowSpecialEventsCheck.IsChecked,
            ShowFooterNotes = ShowFooterCheck.IsChecked,
            IsLandscape = LandscapeRadio.IsChecked,
            FooterNotes = FooterNotesEntry.Text,
            ExtraFooterNotes = ExtraFooterNotesContainer.Children
                .OfType<Grid>()
                .Select(g => g.Children.OfType<Entry>().FirstOrDefault()?.Text?.Trim() ?? "")
                .Where(t => !string.IsNullOrEmpty(t))
                .ToList(),
            FooterWebsite = WebsiteUrlEntry.Text,
            FooterEmail = EmailEntry.Text,
            FooterContactName = ContactNameEntry.Text,
            FooterContactPhone = ContactPhoneEntry.Text,
            FooterReportName = ReportNameEntry.Text,
            FooterReportPhone = ReportPhoneEntry.Text,
            LogoBase64 = _logoBase64,
            SpecialEvents = _events.Select(e => new SpecialEvent
            {
                Date = e.Date,
                Description = e.Description,
                Color = e.Color,
                DayOfWeek = e.DayOfWeek,
            }).ToList(),
        };
    }

    private List<Guid> GetSelectedDivisionIds() =>
        _divisions.Where(d => d.IsSelected).Select(d => d.Id).ToList();

    private string? GenerateSheet()
    {
        if (SeasonPicker.SelectedItem is not Season season) return null;

        var settings = BuildSettings();

        // Persist settings
        League.FixturesSheetSettings = settings;
        DataStore.Save();

        var divIds = GetSelectedDivisionIds();
        var gen = new FixturesSheetGenerator(League, settings);
        return gen.GenerateFixturesSheet(season.Id, divIds.Count > 0 ? divIds : null);
    }

    private void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            var settings = BuildSettings();
            League.FixturesSheetSettings = settings;
            DataStore.Save();
            SetStatus("Settings saved \u2713", true);
        }
        catch (Exception ex)
        {
            SetStatus($"Error saving: {ex.Message}", false);
        }
    }

    private void OnPreviewClicked(object? sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml != null)
            {
                WebViewHelper.LoadHtml(PreviewWebView, _generatedHtml);
                SetStatus("Preview updated", true);
            }
        }
        catch (Exception ex)
        {
            SetStatus($"Error: {ex.Message}", false);
        }
    }

    private async void OnExportClicked(object? sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml == null) return;

            var fileName = $"fixtures-sheet-{DateTime.Now:yyyyMMdd}.html";
            var path = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(path, _generatedHtml);

            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Fixtures Sheet",
                File = new ShareFile(path)
            });
            SetStatus("Exported!", true);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void OnPrintClicked(object? sender, EventArgs e)
    {
        _generatedHtml = GenerateSheet();
        if (_generatedHtml == null) return;

        // Load the sheet in the preview — the user can use the browser's print
        WebViewHelper.LoadHtml(PreviewWebView, _generatedHtml);
        SetStatus("Ready to print — use Ctrl+P in the preview", true);
    }

    private void OnSyncExclusionDatesClicked(object? sender, EventArgs e)
    {
        if (SeasonPicker.SelectedItem is not Season season) return;
        SyncExclusionDates(season);
        SetStatus($"Synced {season.BlackoutDates.Count} exclusion date(s) from season", true);
    }

    private void SyncExclusionDates(Season season)
    {
        // Remove previously synced exclusion dates
        var manualEvents = _events.Where(e => !e.IsFromSeason).ToList();
        _events.Clear();

        // Add season blackout dates
        foreach (var date in season.BlackoutDates.OrderBy(d => d))
        {
            var key = date.ToString("yyyy-MM-dd");
            var title = season.BlackoutDateTitles.TryGetValue(key, out var t) && !string.IsNullOrWhiteSpace(t)
                ? t
                : "No Fixtures";
            _events.Add(new EventItem
            {
                Date = date,
                Description = title,
                DayOfWeek = date.ToString("dddd"),
                Color = "#FECACA",
                IsFromSeason = true,
            });
        }

        // Re-add manual events
        foreach (var e in manualEvents)
            _events.Add(e);

        // Sort all by date
        var sorted = _events.OrderBy(x => x.Date).ToList();
        _events.Clear();
        foreach (var item in sorted)
            _events.Add(item);
    }

    private void OnAddEventClicked(object? sender, EventArgs e)
    {
        var desc = EventDescEntry.Text?.Trim();
        if (string.IsNullOrEmpty(desc)) return;

        var date = EventDatePicker.Date;
        _events.Add(new EventItem
        {
            Date = date,
            Description = desc,
            DayOfWeek = date.ToString("dddd"),
            Color = "#FDE68A",
        });
        EventDescEntry.Text = "";

        // Sort by date
        var sorted = _events.OrderBy(x => x.Date).ToList();
        _events.Clear();
        foreach (var item in sorted)
            _events.Add(item);
    }

    private void OnRemoveEventClicked(object? sender, EventArgs e)
    {
        if (sender is Button btn && btn.BindingContext is EventItem item)
            _events.Remove(item);
    }

    private async void OnUploadLogoClicked(object? sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select a logo image",
                FileTypes = FilePickerFileType.Images,
            });
            if (result == null) return;

            using var stream = await result.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            _logoBase64 = Convert.ToBase64String(ms.ToArray());
            LogoStatusLabel.Text = "Logo set ✓";
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", ex.Message, "OK");
        }
    }

    private void OnClearLogoClicked(object? sender, EventArgs e)
    {
        _logoBase64 = null;
        LogoStatusLabel.Text = "No logo";
    }

    private void SetStatus(string text, bool success)
    {
        StatusLabel.Text = text;
        StatusLabel.TextColor = success ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
        StatusLabel.IsVisible = true;
    }

    // ── View models ──────────────────────────────────────────

    private sealed class DivisionItem
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = "";
        public bool IsSelected { get; set; } = true;
    }

    private sealed class EventItem
    {
        public DateTime Date { get; set; }
        public string Description { get; set; } = "";
        public string DayOfWeek { get; set; } = "";
        public string Color { get; set; } = "#FDE68A";
        public bool IsFromSeason { get; set; }
        public string DateDisplay => Date.ToString("dd MMM");
        public string SourceBadge => IsFromSeason ? "(season)" : "";
    }

    private void OnAddExtraFooterNoteClicked(object? sender, EventArgs e)
    {
        AddExtraFooterNoteRow("");
    }

    private void AddExtraFooterNoteRow(string text)
    {
        var grid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(34))],
            ColumnSpacing = 4
        };
        var entry = new Entry { Placeholder = "Extra footer note...", Text = text, FontSize = 12 };
        var removeBtn = new Button
        {
            Text = "✕",
            BackgroundColor = Colors.Transparent,
            TextColor = Color.FromArgb("#EF4444"),
            FontSize = 13,
            HeightRequest = 34,
            WidthRequest = 34,
            Padding = 0
        };
        removeBtn.Clicked += (_, _) => ExtraFooterNotesContainer.Children.Remove(grid);
        grid.Add(entry, 0);
        grid.Add(removeBtn, 1);
        ExtraFooterNotesContainer.Children.Add(grid);
    }
}
