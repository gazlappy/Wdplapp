using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class CaptainsAccessSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    public sealed class TeamPinRow
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = "";
        public string DivisionName { get; set; } = "";
        public string? CaptainEmail { get; set; }
        public string? CaptainPhone { get; set; }
        public string? CaptainPin { get; set; }
    }

    private readonly ObservableCollection<TeamPinRow> _rows = new();

    public CaptainsAccessSettingsPage()
    {
        InitializeComponent();
        TeamsList.ItemsSource = _rows;
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = League.WebsiteSettings;

        EnableSwitch.IsToggled = s.EnableCaptainsArea;
        NavLabelEntry.Text = s.CaptainsAreaNavLabel;
        PageTitleEntry.Text = s.CaptainsAreaPageTitle;
        WelcomeEditor.Text = s.CaptainsWelcomeMessage;

        ShowFixturesSwitch.IsToggled = s.CaptainsShowFixtures;
        ShowResultsSwitch.IsToggled = s.CaptainsShowResults;
        ShowContactsSwitch.IsToggled = s.CaptainsShowContactList;
        ShowScoreSwitch.IsToggled = s.CaptainsShowScoreSubmission;
        ScoreUrlEntry.Text = s.CaptainsScoreSubmissionUrl;
        ShowAvailSwitch.IsToggled = s.CaptainsShowAvailability;
        AvailUrlEntry.Text = s.CaptainsAvailabilityUrl;
        ShowSheetSwitch.IsToggled = s.CaptainsShowFixturesSheetDownload;
        ShowRosterSwitch.IsToggled = s.CaptainsShowTeamRoster;
        AllowSelfUpdateSwitch.IsToggled = s.CaptainsAllowSelfUpdate;
        UpdateNotifyEmailEntry.Text = s.CaptainsUpdateNotifyEmail;

        // Filter teams to the website's selected season (or active season).
        var seasonId = s.SelectedSeasonId
            ?? League.Seasons.FirstOrDefault(x => x.IsActive)?.Id
            ?? League.Seasons.FirstOrDefault()?.Id;

        var divisionsById = League.Divisions.ToDictionary(d => d.Id, d => d);

        _rows.Clear();
        foreach (var t in League.Teams.Where(t => !seasonId.HasValue || t.SeasonId == seasonId).OrderBy(t => t.Name))
        {
            _rows.Add(new TeamPinRow
            {
                TeamId = t.Id,
                TeamName = t.Name ?? "(unnamed)",
                DivisionName = t.DivisionId.HasValue && divisionsById.TryGetValue(t.DivisionId.Value, out var d) ? d.Name ?? "" : "",
                CaptainEmail = t.CaptainEmail,
                CaptainPhone = t.CaptainPhone,
                CaptainPin = t.CaptainPin
            });
        }

        UpdatePinsFilledLabel();
    }

    private void UpdatePinsFilledLabel()
    {
        var withPin = _rows.Count(r => !string.IsNullOrWhiteSpace(r.CaptainPin));
        PinsFilledLabel.Text = $"{withPin}/{_rows.Count} team(s) with PIN";
        PinsFilledLabel.TextColor = withPin > 0 ? Color.FromArgb("#10B981") : Color.FromArgb("#9CA3AF");
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var s = League.WebsiteSettings;
            s.EnableCaptainsArea = EnableSwitch.IsToggled;
            s.CaptainsAreaNavLabel = string.IsNullOrWhiteSpace(NavLabelEntry.Text) ? "Captains" : NavLabelEntry.Text!.Trim();
            s.CaptainsAreaPageTitle = string.IsNullOrWhiteSpace(PageTitleEntry.Text) ? "Captains Area" : PageTitleEntry.Text!.Trim();
            s.CaptainsWelcomeMessage = WelcomeEditor.Text?.Trim() ?? "";

            s.CaptainsShowFixtures = ShowFixturesSwitch.IsToggled;
            s.CaptainsShowResults = ShowResultsSwitch.IsToggled;
            s.CaptainsShowContactList = ShowContactsSwitch.IsToggled;
            s.CaptainsShowScoreSubmission = ShowScoreSwitch.IsToggled;
            s.CaptainsScoreSubmissionUrl = ScoreUrlEntry.Text?.Trim() ?? "";
            s.CaptainsShowAvailability = ShowAvailSwitch.IsToggled;
            s.CaptainsAvailabilityUrl = AvailUrlEntry.Text?.Trim() ?? "";
            s.CaptainsShowFixturesSheetDownload = ShowSheetSwitch.IsToggled;
            s.CaptainsShowTeamRoster = ShowRosterSwitch.IsToggled;
            s.CaptainsAllowSelfUpdate = AllowSelfUpdateSwitch.IsToggled;
            s.CaptainsUpdateNotifyEmail = UpdateNotifyEmailEntry.Text?.Trim() ?? "";

            // Apply per-team PIN/contact edits
            foreach (var row in _rows)
            {
                var team = League.Teams.FirstOrDefault(t => t.Id == row.TeamId);
                if (team == null) continue;
                team.CaptainEmail = string.IsNullOrWhiteSpace(row.CaptainEmail) ? null : row.CaptainEmail.Trim();
                team.CaptainPhone = string.IsNullOrWhiteSpace(row.CaptainPhone) ? null : row.CaptainPhone.Trim();
                team.CaptainPin = string.IsNullOrWhiteSpace(row.CaptainPin) ? null : row.CaptainPin.Trim();
                team.ModifiedDate = DateTime.UtcNow;
            }

            DataStore.Save();

            await DisplayAlert("Saved", "Captains area settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }
}
