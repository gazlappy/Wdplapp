using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class FixturesSheetPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<DivisionSelection> _divisions = new();
    private readonly ObservableCollection<SpecialEventItem> _events = new();
    private readonly ObservableCollection<VenuePhoneItem> _venuePhones = new();
    private string? _generatedHtml;

    public FixturesSheetPage()
    {
        InitializeComponent();
        
        SeasonPicker.ItemsSource = _seasons;
        SeasonPicker.ItemDisplayBinding = new Binding("Name");
        SeasonPicker.SelectedIndexChanged += OnSeasonChanged;
        
        DivisionsCollection.ItemsSource = _divisions;
        EventsCollection.ItemsSource = _events;
        VenuePhonesCollection.ItemsSource = _venuePhones;
        
        LoadData();
    }

    private void LoadData()
    {
        _seasons.Clear();
        foreach (var season in League.Seasons.OrderByDescending(s => s.StartDate))
            _seasons.Add(season);
        
        // Select active season
        var activeSeason = _seasons.FirstOrDefault(s => s.IsActive) ?? _seasons.FirstOrDefault();
        if (activeSeason != null)
        {
            SeasonPicker.SelectedItem = activeSeason;
        }
        
        // Load website settings defaults
        var settings = League.WebsiteSettings;
        LeagueNameEntry.Text = settings.LeagueName;
        WebsiteUrlEntry.Text = settings.WebsiteUrl;
        EmailEntry.Text = settings.ContactEmail;
    }

    private void OnSeasonChanged(object? sender, EventArgs e)
    {
        if (SeasonPicker.SelectedItem is not Season season) return;
        
        _divisions.Clear();
        var seasonDivisions = League.Divisions.Where(d => d.SeasonId == season.Id).OrderBy(d => d.Name);
        foreach (var div in seasonDivisions)
        {
            _divisions.Add(new DivisionSelection { Id = div.Id, Name = div.Name ?? "", IsSelected = true });
        }
        
        SeasonTitleEntry.Text = season.Name;
        
        // Auto-populate venue phones from venues
        _venuePhones.Clear();
        var venues = League.Venues.Where(v => v.SeasonId == season.Id);
        foreach (var venue in venues)
        {
            if (!string.IsNullOrWhiteSpace(venue.Notes) && venue.Notes.Any(char.IsDigit))
            {
                // Assume notes might contain phone number
                _venuePhones.Add(new VenuePhoneItem { VenueName = venue.Name, PhoneNumber = venue.Notes });
            }
        }
    }

    private void OnDivisionCheckedChanged(object sender, CheckedChangedEventArgs e)
    {
        // Handle in binding
    }

    private async void OnPreviewClicked(object sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml == null) return;
            
            PreviewWebView.Source = new HtmlWebViewSource { Html = _generatedHtml };
            PreviewFrame.IsVisible = true;
            
            SetStatus("Preview generated");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to generate preview: {ex.Message}", "OK");
        }
    }

    private async void OnExportClicked(object sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml == null) return;
            
            var season = SeasonPicker.SelectedItem as Season;
            var fileName = $"fixtures-sheet-{season?.Name?.Replace(" ", "-") ?? "export"}.html";
            
            // Save to a file
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, _generatedHtml);
            
            // Share the file
            await Share.Default.RequestAsync(new ShareFileRequest
            {
                Title = "Export Fixtures Sheet",
                File = new ShareFile(filePath)
            });
            
            SetStatus($"Exported: {fileName}");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to export: {ex.Message}", "OK");
        }
    }

    private async void OnPrintClicked(object sender, EventArgs e)
    {
        try
        {
            _generatedHtml = GenerateSheet();
            if (_generatedHtml == null) return;
            
            // For printing, we'll export and let the user print from their browser/app
            var season = SeasonPicker.SelectedItem as Season;
            var fileName = $"fixtures-sheet-{season?.Name?.Replace(" ", "-") ?? "print"}.html";
            var filePath = Path.Combine(FileSystem.CacheDirectory, fileName);
            await File.WriteAllTextAsync(filePath, _generatedHtml);
            
            // Open the file in the default browser for printing
            await Launcher.Default.OpenAsync(new OpenFileRequest
            {
                Title = "Print Fixtures Sheet",
                File = new ReadOnlyFile(filePath)
            });
            
            SetStatus("Opened for printing");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to prepare for printing: {ex.Message}", "OK");
        }
    }

    private string? GenerateSheet()
    {
        var season = SeasonPicker.SelectedItem as Season;
        if (season == null)
        {
            DisplayAlert("Error", "Please select a season", "OK");
            return null;
        }
        
        var selectedDivisions = _divisions.Where(d => d.IsSelected).Select(d => d.Id).ToList();
        if (selectedDivisions.Count == 0)
        {
            DisplayAlert("Error", "Please select at least one division", "OK");
            return null;
        }
        
        var settings = new FixturesSheetSettings
        {
            LeagueName = LeagueNameEntry.Text ?? "Pool League",
            SeasonName = SeasonTitleEntry.Text ?? season.Name,
            Subtitle = SubtitleEntry.Text ?? "",
            Orientation = LandscapeRadio.IsChecked ? PageOrientation.Landscape : PageOrientation.Portrait,
            ShowTeamNumbers = ShowTeamNumbersCheck.IsChecked,
            ShowVenueInfo = ShowVenueInfoCheck.IsChecked,
            ShowDivisionTeamLists = ShowDivisionListsCheck.IsChecked,
            WebsiteUrl = WebsiteUrlEntry.Text ?? "",
            EmailAddress = EmailEntry.Text ?? "",
            CancelledMatchContact = CancelledMatchContactEntry.Text ?? "",
            CancelledCompetitionContact = CancelledCompContactEntry.Text ?? ""
        };
        
        // Add special events
        foreach (var evt in _events)
        {
            settings.SpecialEvents.Add(new SpecialEvent
            {
                Date = evt.Date,
                DayOfWeek = evt.Date.ToString("dddd"),
                Description = evt.Description,
                Color = evt.Color
            });
        }
        
        // Add venue phone numbers
        foreach (var phone in _venuePhones)
        {
            settings.VenuePhoneNumbers[phone.VenueName] = phone.PhoneNumber;
        }
        
        // Add footer notes
        if (!string.IsNullOrWhiteSpace(FooterNotesEditor.Text))
        {
            var notes = FooterNotesEditor.Text.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            settings.FooterNotes.AddRange(notes.Select(n => n.Trim()));
        }
        
        var generator = new FixturesSheetGenerator(League, settings);
        return generator.GenerateFixturesSheet(season.Id, selectedDivisions);
    }

    private async void OnAddEventClicked(object sender, EventArgs e)
    {
        var datePicker = new DatePicker { Date = DateTime.Today };
        var descEntry = new Entry { Placeholder = "Event description (e.g. Team K.O. First Round)" };
        
        var content = new VerticalStackLayout
        {
            Spacing = 10,
            Children =
            {
                new Label { Text = "Date:" },
                datePicker,
                new Label { Text = "Description:" },
                descEntry
            }
        };
        
        var result = await DisplayAlert("Add Special Event", "Enter event details", "Add", "Cancel");
        
        // Simple approach - just add with default values for demo
        // In production, you'd use a proper dialog
        var description = await DisplayPromptAsync("Add Event", "Enter event description:", 
            placeholder: "Team K.O. First Round");
        
        if (!string.IsNullOrWhiteSpace(description))
        {
            _events.Add(new SpecialEventItem
            {
                Date = DateTime.Today,
                Description = description,
                Color = "#FFE4B5"
            });
        }
    }

    private void OnRemoveEventClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is SpecialEventItem item)
        {
            _events.Remove(item);
        }
    }

    private async void OnAddVenuePhoneClicked(object sender, EventArgs e)
    {
        var venueName = await DisplayPromptAsync("Add Venue Phone", "Venue name:");
        if (string.IsNullOrWhiteSpace(venueName)) return;
        
        var phoneNumber = await DisplayPromptAsync("Add Venue Phone", "Phone number:");
        if (string.IsNullOrWhiteSpace(phoneNumber)) return;
        
        _venuePhones.Add(new VenuePhoneItem
        {
            VenueName = venueName,
            PhoneNumber = phoneNumber
        });
    }

    private void OnRemoveVenuePhoneClicked(object sender, EventArgs e)
    {
        if (sender is Button btn && btn.CommandParameter is VenuePhoneItem item)
        {
            _venuePhones.Remove(item);
        }
    }

    private void OnClosePreviewClicked(object sender, EventArgs e)
    {
        PreviewFrame.IsVisible = false;
    }

    private void SetStatus(string message)
    {
        StatusLabel.Text = $"{DateTime.Now:HH:mm:ss} - {message}";
        StatusLabel.IsVisible = true;
    }
}

// Helper classes
public class DivisionSelection
{
    public Guid Id { get; set; }
    public string Name { get; set; } = "";
    public bool IsSelected { get; set; }
}

public class SpecialEventItem
{
    public DateTime Date { get; set; }
    public string Description { get; set; } = "";
    public string Color { get; set; } = "#FFE4B5";
    public string DateDisplay => Date.ToString("ddd d MMM");
}

public class VenuePhoneItem
{
    public string VenueName { get; set; } = "";
    public string PhoneNumber { get; set; } = "";
}
