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
        private readonly IDataStore _dataStore;
        private LeagueData League => _dataStore.GetData();

        /// <summary>Em-dash separator used between date and title in exclusion date display strings.</summary>
        private const string Separator = " \u2014 ";

        private readonly ObservableCollection<Season> _items = new();
        private readonly ObservableCollection<string> _exclusionDates = new();
        private readonly Dictionary<string, string> _exclusionTitles = new();
        private Season? _selected;
        private bool _isFlyoutOpen = false;
        private bool _isRefreshingList;
        private Guid? _pendingActivationId;
        private bool _activationRunning;

        public SeasonsPage(IDataStore dataStore)
        {
            _dataStore = dataStore;
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

            // Open on the app's current season (falls back to first if none)
            RefreshList(selectId: SeasonService.Current?.CurrentSeasonId, selectFirst: true);
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            // The Season Setup wizard may have added a new season; reflect it here.
            // The service ID is authoritative (another page may have switched
            // season); fall back to this page's previous selection.
            var selectId = SeasonService.Current?.CurrentSeasonId ?? _selected?.Id;
            RefreshList(selectId: selectId, selectFirst: true);
        }

        private async void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            _selected = e.CurrentSelection?.FirstOrDefault() as Season;

            if (_selected != null)
                ShowSeasonInfo(_selected);
            else
                HideSeasonInfo();

            PopulateEditor(_selected);

            // Ignore programmatic selection (page load / list rebuilds) so only
            // a real user click changes app state.
            if (_isRefreshingList || _selected == null)
                return;

            // Switch the app's working season so every other tab (fixtures,
            // teams, players…) follows the clicked season immediately.
            var seasonService = SeasonService.Current;
            if (seasonService != null && seasonService.CurrentSeasonId != _selected.Id)
            {
                seasonService.CurrentSeasonId = _selected.Id;
                StatusLabel.Text = $"Switched to \"{_selected.Name}\"";
            }

            // Keep the green "active" dot in sync: the clicked season becomes
            // the active one (deactivates the rest in place and persists).
            await SetActiveSeasonAsync(_selected);
        }

        /// <summary>
        /// Make the given season the active one. Deactivates all others, persists, and notifies SeasonService.
        /// Rapid successive calls are coalesced: only the latest target wins and writes never
        /// interleave. The bound items are mutated in place (IsActive raises PropertyChanged),
        /// so the green dot updates instantly without rebuilding the list. Rebuilding mid-click
        /// recycled row containers and made the highlight/dot land on 0 or 2 rows.
        /// </summary>
        private async Task SetActiveSeasonAsync(Season season)
        {
            _pendingActivationId = season.Id;
            if (_activationRunning)
                return; // the in-flight loop below picks up the new target

            _activationRunning = true;
            try
            {
                while (_pendingActivationId is Guid targetId)
                {
                    _pendingActivationId = null;

                    // Work directly on the bound list items so the dots reflect
                    // exactly what we persist - no stale snapshot mismatch.
                    var target = _items.FirstOrDefault(s => s.Id == targetId);
                    if (target == null)
                        continue;

                    // Deactivate any other seasons currently flagged active
                    foreach (var s in _items.Where(s => s.IsActive && s.Id != targetId).ToList())
                    {
                        s.IsActive = false;
                        await _dataStore.UpdateSeasonAsync(s);
                    }

                    if (!target.IsActive)
                    {
                        target.IsActive = true;
                        await _dataStore.UpdateSeasonAsync(target);
                    }

                    // ActiveSeasonId is a JSON-only field; persist via legacy JSON store
                    if (DataStore.Data.ActiveSeasonId != targetId)
                    {
                        DataStore.Data.ActiveSeasonId = targetId;
                        DataStore.SaveJsonOnly();
                    }

                    SeasonService.Current.CurrentSeasonId = targetId;

                    // Keep the info panel/editor in sync if this row is still selected
                    if (_selected?.Id == targetId)
                    {
                        ShowSeasonInfo(target);
                        ActiveSwitch.IsToggled = true;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetActiveSeasonAsync save error: {ex.Message}");
            }
            finally
            {
                _activationRunning = false;
            }
        }

        private void OnNewClicked(object sender, EventArgs e)
        {
            // Navigate to the new season setup wizard
            var page = Application.Current?.Handler?.MauiContext?.Services.GetService<SeasonSetupPage>()
                ?? throw new InvalidOperationException("SeasonSetupPage not registered");
            Navigation.PushAsync(page);
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
            
            bool willBeActive = ActiveSwitch.IsToggled;
            model.IsActive = willBeActive;

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

            try
            {
                var isNew = League.Seasons.All(s => s.Id != model.Id);

                // If turning this season on, deactivate any other currently-active seasons first
                if (willBeActive)
                {
                    foreach (var s in League.Seasons.Where(s => s.IsActive && s.Id != model.Id).ToList())
                    {
                        s.IsActive = false;
                        await _dataStore.UpdateSeasonAsync(s);
                    }
                }

                if (isNew)
                    await _dataStore.AddSeasonAsync(model);
                else
                    await _dataStore.UpdateSeasonAsync(model);

                // ActiveSeasonId is JSON-only; persist via legacy JSON store
                if (willBeActive)
                    DataStore.Data.ActiveSeasonId = model.Id;
                else if (DataStore.Data.ActiveSeasonId == model.Id)
                    DataStore.Data.ActiveSeasonId = League.Seasons.FirstOrDefault(s => s.IsActive && s.Id != model.Id)?.Id;
                DataStore.SaveJsonOnly();

                System.Diagnostics.Debug.WriteLine($"Season saved: {model.Name} IsActive={model.IsActive} ActiveSeasonId={DataStore.Data.ActiveSeasonId?.ToString() ?? "NULL"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex}");
                await DisplayAlert("Save Error", ex.Message, "OK");
                return;
            }

            // Notify the rest of the app
            SeasonService.Current.CurrentSeasonId = DataStore.Data.ActiveSeasonId;

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
            var (divisions, venues, teams, players, fixtures) = _dataStore.GetData().GetSeasonData(_selected.Id);

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

            // Cascade delete all season data in SQLite (single transaction)
            var deletedId = _selected.Id;
            await _dataStore.DeleteSeasonAsync(_selected);

            // Clean up JSON-only data (doubles pairings, active season pointer)
            DataStore.Data.DoublesPairings.RemoveAll(dp => dp.SeasonId == deletedId);
            if (DataStore.Data.ActiveSeasonId == deletedId)
                DataStore.Data.ActiveSeasonId = League.Seasons.FirstOrDefault(s => s.IsActive)?.Id;
            DataStore.SaveJsonOnly();

            // Don't leave the rest of the app pointing at a deleted season
            if (SeasonService.Current.CurrentSeasonId == deletedId)
                SeasonService.Current.CurrentSeasonId = DataStore.Data.ActiveSeasonId;

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

            await SetActiveSeasonAsync(_selected);
            StatusLabel.Text = $"\u2705 \"{_selected.Name}\" set as active.";
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
                // Track exactly which items we mutated so a Cancel can revert just those.
                var fixedTeams = League.Teams.Where(t => !t.SeasonId.HasValue).ToList();
                var fixedPlayers = League.Players.Where(p => !p.SeasonId.HasValue).ToList();
                var fixedDivisions = League.Divisions.Where(d => !d.SeasonId.HasValue).ToList();
                var fixedVenues = League.Venues.Where(v => !v.SeasonId.HasValue).ToList();
                var fixedFixtures = League.Fixtures.Where(f => !f.SeasonId.HasValue).ToList();

                foreach (var team in fixedTeams) team.SeasonId = _selected.Id;
                foreach (var player in fixedPlayers) player.SeasonId = _selected.Id;
                foreach (var division in fixedDivisions) division.SeasonId = _selected.Id;
                foreach (var venue in fixedVenues) venue.SeasonId = _selected.Id;
                foreach (var fixture in fixedFixtures) fixture.SeasonId = _selected.Id;

                int teamsFixed = fixedTeams.Count;
                int playersFixed = fixedPlayers.Count;
                int divisionsFixed = fixedDivisions.Count;
                int venuesFixed = fixedVenues.Count;
                int fixturesFixed = fixedFixtures.Count;
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
                    // Persist each reassigned entity through the typed store
                    // (mutating the GetData() snapshot alone is never saved).
                    // Parent entities before children to respect FK order.
                    foreach (var division in fixedDivisions) await _dataStore.UpdateDivisionAsync(division);
                    foreach (var venue in fixedVenues) await _dataStore.UpdateVenueAsync(venue);
                    foreach (var team in fixedTeams) await _dataStore.UpdateTeamAsync(team);
                    foreach (var player in fixedPlayers) await _dataStore.UpdatePlayerAsync(player);
                    foreach (var fixture in fixedFixtures) await _dataStore.UpdateFixtureAsync(fixture);

                    StatusLabel.Text = $"\u2705 Fixed {totalFixed} items and saved!";

                    // Trigger a refresh on all pages by updating the season service
                    SeasonService.Current.CurrentSeasonId = _selected.Id;

                    await DisplayAlert("Success!", $"Successfully fixed and saved {totalFixed} items.", "OK");
                }
                else
                {
                    // Revert exactly the items we mutated (don't reload from disk — that
                    // would discard any other unsaved changes elsewhere in the app).
                    foreach (var team in fixedTeams) team.SeasonId = null;
                    foreach (var player in fixedPlayers) player.SeasonId = null;
                    foreach (var division in fixedDivisions) division.SeasonId = null;
                    foreach (var venue in fixedVenues) venue.SeasonId = null;
                    foreach (var fixture in fixedFixtures) fixture.SeasonId = null;
                    StatusLabel.Text = "Changes cancelled";
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

            // Scope pre-checks to the selected season so other seasons' data
            // doesn't mask a missing-division/team problem here.
            var divCount = League.Divisions?.Count(d => d.SeasonId == _selected.Id) ?? 0;
            var teamCounts = League.Teams
                .Where(t => t.SeasonId == _selected.Id)
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

            var existingCount = League.Fixtures.Count(f => f.SeasonId == _selected.Id);
            if (existingCount > 0)
            {
                var replace = await DisplayAlert(
                    "Generate Fixtures",
                    $"{existingCount} existing fixture(s) for \"{_selected.Name}\" will be replaced. Continue?",
                    "Generate", "Cancel");
                if (!replace) return;
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

                // Atomic replace via the typed store (mutating the GetData()
                // snapshot then calling SaveAsync() never persisted anything).
                await _dataStore.ReplaceFixturesForSeasonAsync(_selected.Id, fixtures);

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

            var importPage = Application.Current?.Handler?.MauiContext?.Services.GetService<ImportHistoricalDataPage>()
                ?? throw new InvalidOperationException("ImportHistoricalDataPage not registered");
            importPage.SetTargetSeason(_selected.Id);
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
            var calEvent = _dataStore.GetData().CalendarEvents
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
            _isRefreshingList = true;
            try
            {
                _items.Clear();
                foreach (var s in League.Seasons.OrderBy(s => s.Name)) // Alphabetical order
                    _items.Add(s);

                Season? toSelect = null;
                if (selectId.HasValue)
                    toSelect = _items.FirstOrDefault(s => s.Id == selectId.Value);
                if (toSelect == null && selectFirst)
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
            finally
            {
                _isRefreshingList = false;
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
                var calendarEvents = _dataStore.GetData().CalendarEvents;
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
            var (divisions, venues, teams, players, fixtures) = _dataStore.GetData().GetSeasonData(season.Id);
            var competitions = _dataStore.GetData().Competitions?.Where(c => c.SeasonId == season.Id).ToList() ?? new List<Models.Competition>();

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

            await _dataStore.UpdateSeasonAsync(_selected);
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

                var sql = SqlExportService.GenerateSeasonSql(_dataStore.GetData(), _selected.Id);
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
