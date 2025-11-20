// File: Views/SeasonsPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views
{
    public partial class SeasonsPage : ContentPage
    {
        private static LeagueData League => DataStore.Data;

        private readonly ObservableCollection<Season> _items = new();
        private readonly ObservableCollection<string> _exclusionDates = new();
        private Season? _selected;
        private bool _isFlyoutOpen = false;

        public SeasonsPage()
        {
            InitializeComponent();

            StartPicker.Date = DateTime.Today;
            EndPicker.Date = DateTime.Today.AddMonths(6);
            ActiveSwitch.IsToggled = true;

            SeasonsList.ItemsSource = _items;
            ExclusionDatesList.ItemsSource = _exclusionDates;

            // Wire up burger menu and flyout
            BurgerMenuBtn.Clicked += OnBurgerMenuClicked;
            CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
            OverlayTap.Tapped += (_, __) => CloseFlyout();

            RefreshList(selectFirst: true);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = e.CurrentSelection?.FirstOrDefault() as Season;

            // UPDATE GLOBAL SEASON when a season is selected
            if (_selected != null)
            {
                SeasonService.CurrentSeasonId = _selected.Id;
                ShowSeasonInfo(_selected);
            }
            else
            {
                HideSeasonInfo();
            }

            PopulateEditor(_selected);
        }

        private void OnNewClicked(object sender, EventArgs e)
        {
            // Navigate to the new season setup wizard
            Navigation.PushAsync(new SeasonSetupPage());
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            var model = _selected ?? new Season { Id = Guid.NewGuid() };

            if (string.IsNullOrWhiteSpace(NameEntry.Text))
            {
                await DisplayAlert("Validation", "Please enter a season name.", "OK");
                return;
            }
            if (EndPicker.Date < StartPicker.Date)
            {
                await DisplayAlert("Validation", "End date must be after start date.", "OK");
                return;
            }

            model.Name = NameEntry.Text!.Trim();
            model.StartDate = StartPicker.Date;
            model.EndDate = EndPicker.Date;
            model.IsActive = ActiveSwitch.IsToggled;

            model.BlackoutDates = _exclusionDates
                .Select(s => DateTime.Parse(s))
                .ToList();

            model.NormaliseDates();

            var existing = League.Seasons.FirstOrDefault(s => s.Id == model.Id);
            if (existing == null)
                League.Seasons.Add(model);
            else
            {
                existing.Name = model.Name;
                existing.StartDate = model.StartDate;
                existing.EndDate = model.EndDate;
                existing.IsActive = model.IsActive;
                existing.BlackoutDates = model.BlackoutDates;
            }

            try { DataStore.Save(); } catch { }

            RefreshList(selectId: model.Id);
            StatusLabel.Text = $"Saved \"{model.Name}\" with {model.BlackoutDates.Count} exclusion date(s).";
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Delete", "Select a season to delete.", "OK");
                return;
            }

            // Get counts of associated data using cascade delete helper
            var (divisions, venues, teams, players, fixtures) = DataStore.Data.GetSeasonData(_selected.Id);

            var message = $"⚠️ WARNING: This will permanently delete:\n\n" +
                          $"• Season: {_selected.Name}\n" +
                          $"• {divisions.Count} Division(s)\n" +
                          $"• {venues.Count} Venue(s)\n" +
                          $"• {teams.Count} Team(s)\n" +
                          $"• {players.Count} Player(s)\n" +
                          $"• {fixtures.Count} Fixture(s)\n\n" +
                          $"This cannot be undone!\n\nAre you sure?";

            var confirm = await DisplayAlert(
                "Delete Season & All Data",
                message,
                "Yes, Delete Everything",
                "Cancel");

            if (!confirm) return;

            // Cascade delete
            DataStore.Data.DeleteSeasonCascade(_selected.Id);
            DataStore.Save();

            _selected = null;
            RefreshList(selectFirst: true);
            StatusLabel.Text = "Season and all associated data deleted.";
        }

        private async void OnSetActiveClicked(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Set Active", "Select a season first.", "OK");
                return;
            }

            // Deactivate all seasons
            foreach (var s in League.Seasons) 
                s.IsActive = false;
            
            // Activate the selected season
            _selected.IsActive = true;

            // Also update the active season ID in the data store
            League.ActiveSeasonId = _selected.Id;

            try 
            { 
                DataStore.Save();
                System.Diagnostics.Debug.WriteLine($"✅ Active season set: {_selected.Name} (ID: {_selected.Id})");
                System.Diagnostics.Debug.WriteLine($"✅ ActiveSeasonId saved: {League.ActiveSeasonId}");
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error saving: {ex.Message}");
            }

            RefreshList(selectId: _selected.Id);
            StatusLabel.Text = $"✅ \"{_selected.Name}\" set as active.";
            
            // Notify SeasonService
            SeasonService.CurrentSeasonId = _selected.Id;
        }

        private async void OnGenerateClicked(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Generate Fixtures", "Select a season first.", "OK");
                return;
            }

            var divCount = League.Divisions?.Count ?? 0;
            var teamCounts = League.Teams
                .GroupBy(t => t.DivisionId)
                .Select(g => new { DivisionId = g.Key, Count = g.Count() })
                .ToList();

            if (divCount == 0 || teamCounts.All(x => x.Count < 2))
            {
                await DisplayAlert(
                    "No Fixtures",
                    "You need at least one division with 2+ teams (with DivisionId set) before fixtures can be generated.",
                    "OK");
                return;
            }

            try
            {
                StatusLabel.Text = "Generating fixtures…";
                GenerateBtn.IsEnabled = false;

                var fixtures = FixtureGenerator.Generate(
                    league: League,
                    seasonId: _selected.Id,
                    startDate: _selected.StartDate,
                    matchNight: DayOfWeek.Tuesday,
                    roundsPerOpponent: 2,
                    kickoff: new TimeSpan(19, 30, 0));

                League.Fixtures.RemoveAll(f => f.SeasonId == _selected.Id);
                League.Fixtures.AddRange(fixtures);

                try { DataStore.Save(); } catch { }

                StatusLabel.Text = $"Generated {fixtures.Count} fixtures for \"{_selected.Name}\".";
                await DisplayAlert("Fixtures", StatusLabel.Text, "OK");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                await DisplayAlert("Error", ex.ToString(), "OK");
            }
            finally
            {
                GenerateBtn.IsEnabled = true;
            }
        }

        private async void OnImportHistoricalClicked(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Import Data", "Select a season first.", "OK");
                return;
            }

            var importPage = new ImportHistoricalDataPage(_selected.Id);
            await Navigation.PushModalAsync(new NavigationPage(importPage));
        }

        private async void OnAddExclusionClicked(object sender, EventArgs e)
        {
            var selectedDate = ExclusionDatePicker.Date;
            var dateString = selectedDate.ToString("ddd, dd MMM yyyy");

            if (_exclusionDates.Contains(dateString))
            {
                await DisplayAlert("Duplicate", "This date is already in the exclusion list.", "OK");
                return;
            }

            _exclusionDates.Add(dateString);

            var sorted = _exclusionDates
                .Select(s => DateTime.Parse(s))
                .OrderBy(d => d)
                .Select(d => d.ToString("ddd, dd MMM yyyy"))
                .ToList();

            _exclusionDates.Clear();
            foreach (var date in sorted)
                _exclusionDates.Add(date);

            StatusLabel.Text = $"Added exclusion date: {dateString}";
        }

        private void OnRemoveExclusionClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string dateString)
            {
                _exclusionDates.Remove(dateString);
                StatusLabel.Text = $"Removed exclusion date: {dateString}";
            }
        }

        private void RefreshList(bool selectFirst = false, Guid? selectId = null)
        {
            _items.Clear();
            foreach (var s in League.Seasons.OrderByDescending(s => s.IsActive).ThenBy(s => s.StartDate))
                _items.Add(s);

            Season? toSelect = null;
            if (selectId.HasValue)
                toSelect = _items.FirstOrDefault(s => s.Id == selectId.Value);
            else if (selectFirst)
                toSelect = _items.FirstOrDefault();

            if (toSelect != null)
            {
                SeasonsList.SelectedItem = toSelect;
                PopulateEditor(toSelect);
            }
            else
            {
                PopulateEditor(null);
            }
        }

        private void PopulateEditor(Season? s)
        {
            if (s == null)
            {
                NameEntry.Text = string.Empty;
                StartPicker.Date = DateTime.Today;
                EndPicker.Date = DateTime.Today.AddMonths(6);
                ActiveSwitch.IsToggled = false;
                _exclusionDates.Clear();
                HideSeasonInfo();
                return;
            }

            NameEntry.Text = s.Name;
            StartPicker.Date = s.StartDate == default ? DateTime.Today : s.StartDate;
            EndPicker.Date = s.EndDate == default ? DateTime.Today.AddMonths(6) : s.EndDate;
            ActiveSwitch.IsToggled = s.IsActive;

            _exclusionDates.Clear();
            if (s.BlackoutDates != null)
            {
                foreach (var date in s.BlackoutDates.OrderBy(d => d))
                {
                    _exclusionDates.Add(date.ToString("ddd, dd MMM yyyy"));
                }
            }

            ExclusionDatePicker.MinimumDate = s.StartDate;
            ExclusionDatePicker.MaximumDate = s.EndDate;

            if (ExclusionDatePicker.Date < s.StartDate || ExclusionDatePicker.Date > s.EndDate)
            {
                ExclusionDatePicker.Date = s.StartDate;
            }
        }

        private void OnBurgerMenuClicked(object? sender, EventArgs e)
        {
            if (_isFlyoutOpen)
                CloseFlyout();
            else
                OpenFlyout();
        }

        private void OnCloseFlyoutClicked(object? sender, EventArgs e)
        {
            CloseFlyout();
        }

        private async void OpenFlyout()
        {
            _isFlyoutOpen = true;
            FlyoutOverlay.IsVisible = true;
            FlyoutPanel.IsVisible = true;

            // Animate flyout sliding in
            FlyoutPanel.TranslationX = -400;
            await FlyoutPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
        }

        private async void CloseFlyout()
        {
            // Animate flyout sliding out
            await FlyoutPanel.TranslateTo(-400, 0, 250, Easing.CubicIn);
            
            FlyoutOverlay.IsVisible = false;
            FlyoutPanel.IsVisible = false;
            _isFlyoutOpen = false;
        }

        private void ShowSeasonInfo(Season season)
        {
            EmptyStatePanel.IsVisible = false;
            SeasonInfoPanel.IsVisible = true;
            
            SelectedSeasonName.Text = season.Name;
            SelectedSeasonDates.Text = $"{season.StartDate:MMM d, yyyy} - {season.EndDate:MMM d, yyyy}";
            SelectedSeasonStatus.Text = season.IsActive ? "Active Season" : "Inactive";
        }

        private void HideSeasonInfo()
        {
            EmptyStatePanel.IsVisible = true;
            SeasonInfoPanel.IsVisible = false;
        }
    }
}
