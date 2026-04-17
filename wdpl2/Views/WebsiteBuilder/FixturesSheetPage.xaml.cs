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

        // Load saved website-embed settings
        var ws = League.WebsiteSettings;
        ShowPrintableSheetCheck.IsChecked = ws.FixturesShowPrintableSheet;
        SheetDefaultExpandedCheck.IsChecked = ws.FixturesSheetDefaultExpanded;
        SheetTitleEntry.Text = ws.FixturesSheetTitle;

        WebsiteUrlEntry.Text = ws.WebsiteUrl;
        EmailEntry.Text = ws.ContactEmail;

        // Load saved fixtures sheet settings
        var fs = League.FixturesSheetSettings;
        FooterNotesEntry.Text = fs.FooterNotes;
        ContactNameEntry.Text = fs.FooterContactName;
        ContactPhoneEntry.Text = fs.FooterContactPhone;
        ReportNameEntry.Text = fs.FooterReportName;
        ReportPhoneEntry.Text = fs.FooterReportPhone;

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
    }

    private FixturesSheetSettings BuildSettings()
    {
        var ws = League.WebsiteSettings;
        return new FixturesSheetSettings
        {
            LeagueName = ws.LeagueName,
            SeasonName = (SeasonPicker.SelectedItem as Season)?.Name ?? "",
            ShowTeamNumbers = ShowTeamNumbersCheck.IsChecked,
            ShowDivisionLists = ShowDivisionListsCheck.IsChecked,
            ShowVenueInfo = ShowVenueInfoCheck.IsChecked,
            ShowSpecialEvents = ShowSpecialEventsCheck.IsChecked,
            ShowFooterNotes = ShowFooterCheck.IsChecked,
            IsLandscape = LandscapeRadio.IsChecked,
            FooterNotes = FooterNotesEntry.Text,
            FooterWebsite = WebsiteUrlEntry.Text,
            FooterEmail = EmailEntry.Text,
            FooterContactName = ContactNameEntry.Text,
            FooterContactPhone = ContactPhoneEntry.Text,
            FooterReportName = ReportNameEntry.Text,
            FooterReportPhone = ReportPhoneEntry.Text,
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
        var ws = League.WebsiteSettings;
        ws.FixturesShowPrintableSheet = ShowPrintableSheetCheck.IsChecked;
        ws.FixturesSheetDefaultExpanded = SheetDefaultExpandedCheck.IsChecked;
        ws.FixturesSheetTitle = string.IsNullOrWhiteSpace(SheetTitleEntry.Text)
            ? "Printable Fixtures Sheet" : SheetTitleEntry.Text;
        DataStore.Save();

        var divIds = GetSelectedDivisionIds();
        var gen = new FixturesSheetGenerator(League, settings);
        return gen.GenerateFixturesSheet(season.Id, divIds.Count > 0 ? divIds : null);
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
        public string DateDisplay => Date.ToString("dd MMM");
    }
}
