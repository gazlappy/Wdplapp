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

        /// <summary>Em-dash separator used between date and title in exclusion date display strings.</summary>
        private const string Separator = " \u2014 ";

        private readonly ObservableCollection<Season> _items = new();
        private readonly ObservableCollection<string> _exclusionDates = new();
        private readonly Dictionary<string, string> _exclusionTitles = new();
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

            // Wire doubles toggle to show/hide frame count fields
            DoublesSwitch.Toggled += OnDoublesToggled;

            // Keep exclusion date picker range in sync with season dates
            StartPicker.DateSelected += OnSeasonDateChanged;
            EndPicker.DateSelected += OnSeasonDateChanged;

            RefreshList(selectFirst: true);
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = e.CurrentSelection?.FirstOrDefault() as Season;

            if (_selected != null)
            {
                // AUTO-ACTIVATE: When user clicks a season, activate it immediately
                System.Diagnostics.Debug.WriteLine($"?? Season selected: {_selected.Name} - Auto-activating...");
                
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
                    System.Diagnostics.Debug.WriteLine($"? Auto-activated: {_selected.Name} (ID: {selectedId})");
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"? Error saving: {ex.Message}");
                }
                
                // Update SeasonService to notify all pages
                SeasonService.Current.CurrentSeasonId = selectedId;
                System.Diagnostics.Debug.WriteLine($"? SeasonService updated: {SeasonService.Current.CurrentSeasonId}");
                
                // Update UI status
                StatusLabel.Text = $"? \"{_selected.Name}\" activated";
                
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
                StatusLabel.Text = "? Seasons list refreshed";
                
                System.Diagnostics.Debug.WriteLine("=== Seasons List Refreshed ===");
                System.Diagnostics.Debug.WriteLine($"Total seasons: {League.Seasons.Count}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Refresh error: {ex.Message}");
                StatusLabel.Text = $"? Refresh failed: {ex.Message}";
            }
        }

        private async void OnSaveClicked(object sender, EventArgs e)
        {
            if (_selected?.IsLocked == true)
            {
                await DisplayAlert($"{Helpers.Emojis.Lock} Locked", "This season is locked and cannot be edited. Unlock it first.", "OK");
                return;
            }

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
                System.Diagnostics.Debug.WriteLine($"?? Activating season: {model.Name}");
                foreach (var s in League.Seasons)
                    s.IsActive = false;
                    
                model.IsActive = true;
                League.ActiveSeasonId = model.Id;
            }
            else if (!willBeActive && wasActive)
            {
                // Turning this season OFF
                System.Diagnostics.Debug.WriteLine($"?? Deactivating season: {model.Name}");
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
                System.Diagnostics.Debug.WriteLine($"? Season remains active: {model.Name}");
                model.IsActive = true;
                League.ActiveSeasonId = model.Id;
            }
            else
            {
                // Season was inactive and staying inactive
                System.Diagnostics.Debug.WriteLine($"? Season remains inactive: {model.Name}");
                model.IsActive = false;
            }

            model.BlackoutDates = _exclusionDates
                .Select(s =>
                {
                    var datePart = s.Contains(Separator) ? s[..s.IndexOf(Separator)] : s;
                    return DateTime.Parse(datePart);
                })
                .ToList();
            model.BlackoutDateTitles = new Dictionary<string, string>(_exclusionTitles);

            model.IncludeDoubles = DoublesSwitch.IsToggled;
            model.SinglesFrameCount = int.TryParse(SinglesFramesEntry.Text, out var sc) ? sc : 0;
            model.DoublesFrameCount = int.TryParse(DoublesFramesEntry.Text, out var dc) ? dc : 0;

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
                existing.BlackoutDateTitles = model.BlackoutDateTitles;
                existing.IncludeDoubles = model.IncludeDoubles;
                existing.SinglesFrameCount = model.SinglesFrameCount;
                existing.DoublesFrameCount = model.DoublesFrameCount;
            }

            try 
            { 
                DataStore.Save();
                System.Diagnostics.Debug.WriteLine($"?? Season saved: {model.Name} (ID: {model.Id})");
                System.Diagnostics.Debug.WriteLine($"   IsActive: {model.IsActive}");
                System.Diagnostics.Debug.WriteLine($"   ActiveSeasonId: {League.ActiveSeasonId?.ToString() ?? "NULL"}");
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Save error: {ex.Message}");
            }

            // ALWAYS update SeasonService to trigger the event
            System.Diagnostics.Debug.WriteLine($"?? Updating SeasonService.Current.CurrentSeasonId...");
            System.Diagnostics.Debug.WriteLine($"   Before: {SeasonService.Current.CurrentSeasonId?.ToString() ?? "NULL"}");
            System.Diagnostics.Debug.WriteLine($"   wasActive: {wasActive}, willBeActive: {willBeActive}");
            System.Diagnostics.Debug.WriteLine($"   League.ActiveSeasonId: {League.ActiveSeasonId?.ToString() ?? "NULL"}");
            
            if (willBeActive)
            {
                // Season is being activated or staying active
                SeasonService.Current.CurrentSeasonId = model.Id;
            }
            else
            {
                // Season is NOT active (either deactivated or staying inactive)
                // Clear the current season if there's no active season
                if (League.ActiveSeasonId == null)
                {
                    System.Diagnostics.Debug.WriteLine($"   No active season in League - setting CurrentSeasonId to NULL");
                    SeasonService.Current.CurrentSeasonId = null;
                }
                else
                {
                    // There's another active season - switch to it
                    var activeSeason = League.Seasons.FirstOrDefault(s => s.IsActive);
                    if (activeSeason != null)
                    {
                        System.Diagnostics.Debug.WriteLine($"   Switching to active season: {activeSeason.Name}");
                        SeasonService.Current.CurrentSeasonId = activeSeason.Id;
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine($"   No active season found - setting CurrentSeasonId to NULL");
                        SeasonService.Current.CurrentSeasonId = null;
                    }
                }
            }
            
            System.Diagnostics.Debug.WriteLine($"   After: {SeasonService.Current.CurrentSeasonId?.ToString() ?? "NULL"}");

            RefreshList(selectId: model.Id);
            
            var activeStatus = model.IsActive ? "? Active" : "? Inactive";
            StatusLabel.Text = $"Saved \"{model.Name}\" - {activeStatus}";
        }

        private async void OnDeleteClicked(object sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Delete", "Select a season to delete.", "OK");
                return;
            }

            if (_selected.IsLocked)
            {
                await DisplayAlert($"{Helpers.Emojis.Lock} Locked", "This season is locked and cannot be deleted. Unlock it first.", "OK");
                return;
            }

            // Get counts of associated data using cascade delete helper
            var (divisions, venues, teams, players, fixtures) = DataStore.Data.GetSeasonData(_selected.Id);

            var message = $"?? WARNING: This will permanently delete:\n\n" +
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

            // Cascade delete all data for this season
            DataStore.Data.DeleteSeasonCascade(_selected.Id);

            // Also clean up any orphaned entities (null or invalid SeasonId)
            DataStore.Data.CleanupOrphans();
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
                System.Diagnostics.Debug.WriteLine($"? Active season set: {_selected.Name} (ID: {_selected.Id})");
                System.Diagnostics.Debug.WriteLine($"? ActiveSeasonId saved: {League.ActiveSeasonId}");
                System.Diagnostics.Debug.WriteLine($"? SeasonService.Current.CurrentSeasonId BEFORE: {SeasonService.Current.CurrentSeasonId}");
            } 
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"? Error saving: {ex.Message}");
            }

            // Notify SeasonService - THIS MUST HAPPEN TO UPDATE ALL PAGES
            SeasonService.Current.CurrentSeasonId = _selected.Id;
            
            System.Diagnostics.Debug.WriteLine($"? SeasonService.Current.CurrentSeasonId AFTER: {SeasonService.Current.CurrentSeasonId}");
            
            RefreshList(selectId: _selected.Id);
            StatusLabel.Text = $"? \"{_selected.Name}\" set as active.";
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
                    await DisplayAlert("? All Good!", "No items found with missing Season IDs.", "OK");
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
                    StatusLabel.Text = $"? Fixed {totalFixed} items and saved!";
                    
                    // Trigger a refresh on all pages by updating the season service
                    SeasonService.Current.CurrentSeasonId = _selected.Id;
                    
                    await DisplayAlert("Success!", $"Successfully fixed and saved {totalFixed} items.", "OK");
                }
                else
                {
                    // Reload data to undo changes
                    DataStore.Load();
                    StatusLabel.Text = "? Changes cancelled";
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

            if (_selected.IsLocked)
            {
                await DisplayAlert($"{Helpers.Emojis.Lock} Locked", "This season is locked. Unlock it to generate fixtures.", "OK");
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

                var settings = League.GetSettingsForSeason(_selected.Id);
                var fixtures = FixtureGenerator.Generate(
                    league: League,
                    seasonId: _selected.Id,
                    startDate: _selected.StartDate,
                    matchNight: settings.DefaultMatchDay,
                    roundsPerOpponent: settings.DefaultRoundsPerOpponent,
                    kickoff: settings.DefaultMatchTime,
                    endDate: _selected.EndDate,
                    blackoutDates: _selected.BlackoutDates);

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
            var dateKey = selectedDate.ToString("yyyy-MM-dd");

            if (_exclusionTitles.ContainsKey(dateKey))
            {
                await DisplayAlert("Duplicate", "This date is already in the exclusion list.", "OK");
                return;
            }

            var title = (ExclusionTitleEntry.Text ?? "").Trim();
            _exclusionTitles[dateKey] = title;

            var displayText = FormatExclusionDisplay(selectedDate, title);

            _exclusionDates.Add(displayText);
            SortExclusionDates();

            ExclusionTitleEntry.Text = "";
            StatusLabel.Text = $"Added exclusion date: {displayText}";
        }

        private async void OnEditExclusionClicked(object sender, EventArgs e)
        {
            if (sender is not Button btn || btn.CommandParameter is not string dateString) return;

            var datePart = dateString.Contains(Separator) ? dateString[..dateString.IndexOf(Separator)] : dateString;
            if (!DateTime.TryParse(datePart, out var date)) return;

            var dateKey = date.ToString("yyyy-MM-dd");
            var currentTitle = _exclusionTitles.GetValueOrDefault(dateKey, "");

            var newTitle = await DisplayPromptAsync(
                "Edit Exclusion Date",
                $"Title for {date:ddd, dd MMM yyyy}:",
                initialValue: currentTitle,
                placeholder: "e.g. Christmas Day");

            if (newTitle == null) return; // cancelled

            newTitle = newTitle.Trim();
            _exclusionTitles[dateKey] = newTitle;

            // Update the display string
            var newDisplay = FormatExclusionDisplay(date, newTitle);
            var index = _exclusionDates.IndexOf(dateString);
            if (index >= 0)
                _exclusionDates[index] = newDisplay;

            // Sync title to any matching calendar event on the same date
            var calEvent = DataStore.Data.CalendarEvents
                .FirstOrDefault(ce => ce.Date.Date == date.Date);
            if (calEvent != null && !string.IsNullOrWhiteSpace(newTitle))
            {
                calEvent.Title = newTitle;
                DataStore.SaveJsonOnly();
            }

            StatusLabel.Text = $"Updated: {newDisplay}";
        }

        private void OnRemoveExclusionClicked(object sender, EventArgs e)
        {
            if (sender is Button btn && btn.CommandParameter is string dateString)
            {
                _exclusionDates.Remove(dateString);
                var datePart = dateString.Contains(Separator) ? dateString[..dateString.IndexOf(Separator)] : dateString;
                if (DateTime.TryParse(datePart, out var dt))
                    _exclusionTitles.Remove(dt.ToString("yyyy-MM-dd"));
                StatusLabel.Text = $"Removed exclusion date: {dateString}";
            }
        }

        /// <summary>Format an exclusion date for display in the list.</summary>
        private static string FormatExclusionDisplay(DateTime date, string title) =>
            string.IsNullOrWhiteSpace(title)
                ? date.ToString("ddd, dd MMM yyyy")
                : $"{date:ddd, dd MMM yyyy}{Separator}{title}";

        /// <summary>Sort the exclusion dates collection by date.</summary>
        private void SortExclusionDates()
        {
            var sorted = _exclusionDates
                .Select(s =>
                {
                    var datePart = s.Contains(Separator) ? s[..s.IndexOf(Separator)] : s;
                    return (Display: s, Date: DateTime.Parse(datePart));
                })
                .OrderBy(x => x.Date)
                .Select(x => x.Display)
                .ToList();

            _exclusionDates.Clear();
            foreach (var item in sorted)
                _exclusionDates.Add(item);
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
                DoublesSwitch.IsToggled = false;
                SinglesFramesEntry.Text = string.Empty;
                DoublesFramesEntry.Text = string.Empty;
                SetDoublesFieldsVisible(false);
                _exclusionDates.Clear();
                _exclusionTitles.Clear();
                ExclusionDatePicker.MinimumDate = DateTime.Today;
                ExclusionDatePicker.MaximumDate = DateTime.Today.AddMonths(6);
                ExclusionDatePicker.Date = DateTime.Today;
                HideSeasonInfo();
                return;
            }

            NameEntry.Text = s.Name;
            StartPicker.Date = s.StartDate == default ? DateTime.Today : s.StartDate;
            EndPicker.Date = s.EndDate == default ? DateTime.Today.AddMonths(6) : s.EndDate;
            ActiveSwitch.IsToggled = s.IsActive;

            DoublesSwitch.IsToggled = s.IncludeDoubles;
            SinglesFramesEntry.Text = s.SinglesFrameCount > 0 ? s.SinglesFrameCount.ToString() : "";
            DoublesFramesEntry.Text = s.DoublesFrameCount > 0 ? s.DoublesFrameCount.ToString() : "";
            SetDoublesFieldsVisible(s.IncludeDoubles);

            _exclusionDates.Clear();
            _exclusionTitles.Clear();
            if (s.BlackoutDates != null)
            {
                var calendarEvents = DataStore.Data.CalendarEvents;
                foreach (var date in s.BlackoutDates.OrderBy(d => d))
                {
                    var dateKey = date.ToString("yyyy-MM-dd");
                    var title = s.BlackoutDateTitles?.GetValueOrDefault(dateKey, "") ?? "";

                    // If no title set, try to match from a calendar event on the same date
                    if (string.IsNullOrWhiteSpace(title))
                    {
                        var matchingEvent = calendarEvents.FirstOrDefault(ce => ce.Date.Date == date.Date);
                        if (matchingEvent != null && !string.IsNullOrWhiteSpace(matchingEvent.Title))
                        {
                            title = matchingEvent.Title;
                            s.BlackoutDateTitles ??= new();
                            s.BlackoutDateTitles[dateKey] = title;
                        }
                    }

                    _exclusionTitles[dateKey] = title;
                    _exclusionDates.Add(FormatExclusionDisplay(date, title));
                }
            }

            ExclusionDatePicker.MinimumDate = s.StartDate;
            ExclusionDatePicker.MaximumDate = s.EndDate;

            if (ExclusionDatePicker.Date < s.StartDate || ExclusionDatePicker.Date > s.EndDate)
            {
                ExclusionDatePicker.Date = s.StartDate;
            }
        }

        private void OnSeasonDateChanged(object? sender, DateChangedEventArgs e)
        {
            ExclusionDatePicker.MinimumDate = StartPicker.Date;
            ExclusionDatePicker.MaximumDate = EndPicker.Date;

            if (ExclusionDatePicker.Date < StartPicker.Date)
                ExclusionDatePicker.Date = StartPicker.Date;
            else if (ExclusionDatePicker.Date > EndPicker.Date)
                ExclusionDatePicker.Date = EndPicker.Date;
        }

        private void OnDoublesToggled(object? sender, ToggledEventArgs e)
        {
            SetDoublesFieldsVisible(e.Value);
        }

        private void SetDoublesFieldsVisible(bool visible)
        {
            SinglesFramesLabel.IsVisible = visible;
            SinglesFramesEntry.IsVisible = visible;
            DoublesFramesLabel.IsVisible = visible;
            DoublesFramesEntry.IsVisible = visible;
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

            // Lock status
            UpdateLockUI(season);

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

        private void UpdateLockUI(Season season)
        {
            if (season.IsLocked)
            {
                LockInfoBtn.Text = $"{Helpers.Emojis.Unlock} Unlock Season";
                LockInfoBtn.BackgroundColor = Color.FromArgb("#D97706");
                LockInfoBtn.TextColor = Colors.White;
                LockBtn.Text = $"{Helpers.Emojis.Unlock} Unlock";
                LockStatusLabel.Text = $"{Helpers.Emojis.Lock} This season is locked — no changes can be made to its data.";
                LockStatusLabel.IsVisible = true;
                LockStatusLabel.TextColor = Color.FromArgb("#DC2626");

                // Disable destructive/editing actions in flyout
                SaveBtn.IsEnabled = false;
                DeleteBtn.IsEnabled = false;
                GenerateBtn.IsEnabled = false;
                ImportHistoricalBtn.IsEnabled = false;
                FixDataBtn.IsEnabled = false;
            }
            else
            {
                LockInfoBtn.Text = $"{Helpers.Emojis.Lock} Lock Season";
                LockInfoBtn.BackgroundColor = Color.FromArgb("#6B7280");
                LockInfoBtn.TextColor = Colors.White;
                LockBtn.Text = $"{Helpers.Emojis.Lock} Lock";
                LockStatusLabel.IsVisible = false;

                SaveBtn.IsEnabled = true;
                DeleteBtn.IsEnabled = true;
                GenerateBtn.IsEnabled = true;
                ImportHistoricalBtn.IsEnabled = true;
                FixDataBtn.IsEnabled = true;
            }
        }

        private async void OnLockToggleClicked(object? sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Lock", "Select a season first.", "OK");
                return;
            }

            if (_selected.IsLocked)
            {
                var confirm = await DisplayAlert(
                    $"{Helpers.Emojis.Unlock} Unlock Season",
                    $"Unlock \"{_selected.Name}\"?\n\nThis will allow editing and deleting season data again.",
                    "Unlock", "Cancel");
                if (!confirm) return;

                _selected.IsLocked = false;
            }
            else
            {
                var confirm = await DisplayAlert(
                    $"{Helpers.Emojis.Lock} Lock Season",
                    $"Lock \"{_selected.Name}\"?\n\nWhen locked:\n" +
                    "• Fixtures, teams, players and venues cannot be added, edited or deleted\n" +
                    "• The season itself cannot be deleted\n" +
                    "• You can unlock it again later",
                    "Lock", "Cancel");
                if (!confirm) return;

                _selected.IsLocked = true;
            }

            DataStore.Save();
            UpdateLockUI(_selected);

            // Refresh list to update lock icon
            var selectedId = _selected.Id;
            var tempList = _items.ToList();
            _items.Clear();
            foreach (var season in tempList)
                _items.Add(season);
            SeasonsList.SelectedItem = _items.FirstOrDefault(s => s.Id == selectedId);

            StatusLabel.Text = _selected.IsLocked
                ? $"{Helpers.Emojis.Lock} \"{_selected.Name}\" locked"
                : $"{Helpers.Emojis.Unlock} \"{_selected.Name}\" unlocked";
        }

        private async void OnExportSqlClicked(object? sender, EventArgs e)
        {
            if (_selected == null)
            {
                await DisplayAlert("Export SQL", "Select a season first.", "OK");
                return;
            }

            try
            {
                StatusLabel.Text = "Generating SQL export…";
                ExportSqlBtn.IsEnabled = false;

                var sql = SqlExportService.GenerateSeasonSql(DataStore.Data, _selected.Id);
                var fileName = $"{_selected.Name.Replace(" ", "_")}_Export_{DateTime.Now:yyyyMMdd_HHmmss}.sql";

                await ExportService.ShareFileAsync(sql, fileName, $"SQL Export — {_selected.Name}");
                StatusLabel.Text = $"{Helpers.Emojis.Success} SQL exported for \"{_selected.Name}\"";
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"OnExportSqlClicked Error: {ex}");
                await DisplayAlert($"{Helpers.Emojis.Error} Export Failed", ex.Message, "OK");
                StatusLabel.Text = $"Export failed: {ex.Message}";
            }
            finally
            {
                ExportSqlBtn.IsEnabled = true;
            }
        }

        private void HideSeasonInfo()
        {
            EmptyStatePanel.IsVisible = true;
            SeasonInfoPanel.IsVisible = false;
        }
    }
}
