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

            if (_selected != null)
            {
                // AUTO-ACTIVATE: When user clicks a season, activate it immediately
                System.Diagnostics.Debug.WriteLine($"🔄 Season selected: {_selected.Name} - Auto-activating...");
                
                var selectedId = _selected.Id;
                
                // Deactivate all other seasons
                foreach (var s in League.Seasons)
                {
                    if (s.Id != selectedId)
                        s.IsActive = false;
                }
                
                // Activate the selected season
                _selected.IsActive = true;
                League.ActiveSeasonId = selectedId;
                
                // Save changes
                try
                {
                    DataStore.Save();
                    System.Diagnostics.Debug.WriteLine($"✅ Auto-activated: {_selected.Name} (ID: {selectedId})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"❌ Error saving: {ex.Message}");
                }
                
                // Update SeasonService to notify all pages
                SeasonService.CurrentSeasonId = selectedId;
                System.Diagnostics.Debug.WriteLine($"✅ SeasonService updated: {SeasonService.CurrentSeasonId}");
                
                // Update UI status
                StatusLabel.Text = $"✅ \"{_selected.Name}\" activated";
                
                // Show season info
                ShowSeasonInfo(_selected);
                
                // Force UI update - keep alphabetical order, don't move items around
                var tempList = _items.ToList();
                _items.Clear();
                foreach (var season in tempList) // Keep existing order (alphabetical)
                {
                    _items.Add(season);
                }
                
                // Restore selection
                SeasonsList.SelectedItem = _items.FirstOrDefault(s => s.Id == selectedId);
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

        private void OnRefreshListClicked(object sender, EventArgs e)
        {
            try
            {
                // Reload data from storage
                DataStore.Load();
                
                // Refresh the list
                RefreshList(selectFirst: false);
                
                // Update status
                StatusLabel.Text = "✅ Seasons list refreshed";
                
                System.Diagnostics.Debug.WriteLine("=== Seasons List Refreshed ===");
                System.Diagnostics.Debug.WriteLine($"Total seasons: {League.Seasons.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
                StatusLabel.Text = $"❌ Refresh failed: {ex.Message}";
            }
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
            
            // Handle IsActive properly
            bool wasActive = model.IsActive;
            bool willBeActive = ActiveSwitch.IsToggled;
            
            if (willBeActive && !wasActive)
            {
                // Turning this season ON - deactivate all others
                System.Diagnostics.Debug.WriteLine($"🟢 Activating season: {model.Name}");
                foreach (var s in League.Seasons)
                    s.IsActive = false;
                    
                model.IsActive = true;
                League.ActiveSeasonId = model.Id;
            }
            else if (!willBeActive && wasActive)
            {
                // Turning this season OFF
                System.Diagnostics.Debug.WriteLine($"🔴 Deactivating season: {model.Name}");
                model.IsActive = false;
                
                // If this was the active season, clear the ActiveSeasonId
                if (League.ActiveSeasonId == model.Id)
                {
                    League.ActiveSeasonId = null;
                }
            }
            else if (willBeActive && wasActive)
            {
                // Season was already active and staying active
                System.Diagnostics.Debug.WriteLine($"✅ Season remains active: {model.Name}");
                model.IsActive = true;
                League.ActiveSeasonId = model.Id;
            }
            else
            {
                // Season was inactive and staying inactive
                System.Diagnostics.Debug.WriteLine($"⚪ Season remains inactive: {model.Name}");
                model.IsActive = false;
            }

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

            try 
            { 
                DataStore.Save();
                System.Diagnostics.Debug.WriteLine($"💾 Season saved: {model.Name} (ID: {model.Id})");
                System.Diagnostics.Debug.WriteLine($"   IsActive: {model.IsActive}");
                System.Diagnostics.Debug.WriteLine($"   ActiveSeasonId: {League.ActiveSeasonId?.ToString() ?? "NULL"}");
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Save error: {ex.Message}");
            }

            // ALWAYS update SeasonService to trigger the event
            System.Diagnostics.Debug.WriteLine($"🔄 Updating SeasonService.CurrentSeasonId...");
            System.Diagnostics.Debug.WriteLine($"   Before: {SeasonService.CurrentSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   wasActive: {wasActive}, willBeActive: {willBeActive}");
            System.Diagnostics.Debug.WriteLine($"   League.ActiveSeasonId: {League.ActiveSeasonId?.ToString() ?? "NULL"}");
            
            if (willBeActive)
            {
                // Season is being activated or staying active
                SeasonService.CurrentSeasonId = model.Id;
            }
            else
            {
                // Season is NOT active (either deactivated or staying inactive)
                // Clear the current season if there's no active season
                if (League.ActiveSeasonId == null)
                {
                    System.Diagnostics.Debug.WriteLine($"   No active season in League - setting CurrentSeasonId to NULL");
                    SeasonService.CurrentSeasonId = null;
                }
                else
                {
                    // There's another active season - switch to it
                    var activeSeason = League.Seasons.FirstOrDefault(s => s.IsActive);
                    if (activeSeason != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Switching to active season: {activeSeason.Name}");
                        SeasonService.CurrentSeasonId = activeSeason.Id;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"   No active season found - setting CurrentSeasonId to NULL");
                        SeasonService.CurrentSeasonId = null;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"   After: {SeasonService.CurrentSeasonId?.ToString() ?? "NULL"}");

            RefreshList(selectId: model.Id);
            
            var activeStatus = model.IsActive ? "✅ Active" : "⚪ Inactive";
            StatusLabel.Text = $"Saved \"{model.Name}\" - {activeStatus}";
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
                System.Diagnostics.Debug.WriteLine($"✅ SeasonService.CurrentSeasonId BEFORE: {SeasonService.CurrentSeasonId}");
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"❌ Error saving: {ex.Message}");
            }

            // Notify SeasonService - THIS MUST HAPPEN TO UPDATE ALL PAGES
            SeasonService.CurrentSeasonId = _selected.Id;
            
            System.Diagnostics.Debug.WriteLine($"✅ SeasonService.CurrentSeasonId AFTER: {SeasonService.CurrentSeasonId}");
            
            RefreshList(selectId: _selected.Id);
            StatusLabel.Text = $"✅ \"{_selected.Name}\" set as active.";
        }

        private async void OnFixMissingSeasonIdsClicked(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Fix Data", "Please select the season to assign to items with missing Season IDs.", "OK");
                return;
            }

            try
            {
                // Count items without season IDs
                int teamsFixed = 0;
                int playersFixed = 0;
                int divisionsFixed = 0;
                int venuesFixed = 0;
                int fixturesFixed = 0;

                // Fix Teams
                foreach (var team in League.Teams.Where(t => !t.SeasonId.HasValue))
                {
                    team.SeasonId = _selected.Id;
                    teamsFixed++;
                }

                // Fix Players
                foreach (var player in League.Players.Where(p => !p.SeasonId.HasValue))
                {
                    player.SeasonId = _selected.Id;
                    playersFixed++;
                }

                // Fix Divisions
                foreach (var division in League.Divisions.Where(d => !d.SeasonId.HasValue))
                {
                    division.SeasonId = _selected.Id;
                    divisionsFixed++;
                }

                // Fix Venues (NEW!)
                foreach (var venue in League.Venues.Where(v => !v.SeasonId.HasValue))
                {
                    venue.SeasonId = _selected.Id;
                    venuesFixed++;
                }

                // Fix Fixtures
                foreach (var fixture in League.Fixtures.Where(f => !f.SeasonId.HasValue))
                {
                    fixture.SeasonId = _selected.Id;
                    fixturesFixed++;
                }

                int totalFixed = teamsFixed + playersFixed + divisionsFixed + venuesFixed + fixturesFixed;

                if (totalFixed == 0)
                {
                    await DisplayAlert("✅ All Good!", "No items found with missing Season IDs.", "OK");
                    return;
                }

                var message = $"Found and fixed {totalFixed} item(s) with missing Season IDs:\n\n" +
                              $"• Teams: {teamsFixed}\n" +
                              $"• Players: {playersFixed}\n" +
                              $"• Divisions: {divisionsFixed}\n" +
                              $"• Venues: {venuesFixed}\n" +
                              $"• Fixtures: {fixturesFixed}\n\n" +
                              $"All items have been assigned to season: {_selected.Name}\n\n" +
                              $"Save changes now?";

                var confirm = await DisplayAlert("Fix Data", message, "Yes, Save", "Cancel");
                
                if (confirm)
                {
                    DataStore.Save();
                    StatusLabel.Text = $"✅ Fixed {totalFixed} items and saved!";
                    
                    // Trigger a refresh on all pages by updating the season service
                    SeasonService.CurrentSeasonId = _selected.Id;
                    
                    await DisplayAlert("Success!", $"Successfully fixed and saved {totalFixed} items.", "OK");
                }
                else
                {
                    // Reload data to undo changes
                    DataStore.Load();
                    StatusLabel.Text = "❌ Changes cancelled";
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnFixMissingSeasonIdsClicked Error: {ex}");
                await DisplayAlert("Error", $"Failed to fix data: {ex.Message}", "OK");
                StatusLabel.Text = $"Error: {ex.Message}";
            }
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
            foreach (var s in League.Seasons.OrderBy(s => s.Name)) // Alphabetical order
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
            
            // Update badge color by setting background directly instead of style
            SelectedSeasonStatusBadge.BackgroundColor = season.IsActive 
                ? Color.FromArgb("#10B981") // SuccessColor
                : Color.FromArgb("#06B6D4"); // InfoColor
            
            // Calculate statistics
            var (divisions, venues, teams, players, fixtures) = DataStore.Data.GetSeasonData(season.Id);
            var competitions = DataStore.Data.Competitions?.Where(c => c.SeasonId == season.Id).ToList() ?? new List<Models.Competition>();
            
            DivisionsCount.Text = divisions.Count.ToString();
            TeamsCount.Text = teams.Count.ToString();
            PlayersCount.Text = players.Count.ToString();
            FixturesCount.Text = fixtures.Count.ToString();
            VenuesCount.Text = venues.Count.ToString();
            CompetitionsCount.Text = competitions.Count.ToString();
        }

        private void HideSeasonInfo()
        {
            EmptyStatePanel.IsVisible = true;
            SeasonInfoPanel.IsVisible = false;
        }
    }
}
