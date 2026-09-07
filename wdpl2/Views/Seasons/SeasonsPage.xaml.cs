// File: Views/SeasonsPage.xaml.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.ViewModels;

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
        private readonly SeasonLibraryViewModel _library = new();
        private Season? _selected => _library.PreviewedSeason;
        private bool _isFlyoutOpen = false;
        private Guid? _pendingActivationId;
        private bool _activationRunning;

        public SeasonsPage(IDataStore dataStore)
        {
            _dataStore = dataStore;
            InitializeComponent();

            StartPicker.Date = DateTime.Today;
            EndPicker.Date = DateTime.Today.AddMonths(6);

            ExclusionDatesList.ItemsSource = _exclusionDates;

            CloseFlyoutBtn.Clicked += OnCloseFlyoutClicked;
            OverlayTap.Tapped += (_, __) => CloseFlyout();

            // Wire doubles toggle to show/hide frame count fields
            DoublesSwitch.Toggled += OnDoublesToggled;

            // Keep exclusion date picker range in sync with season dates
            StartPicker.DateSelected += OnSeasonDateChanged;
            EndPicker.DateSelected += OnSeasonDateChanged;

            SeasonFilter.SelectedIndex = 0;
            SizeChanged += (_, _) => UpdateLibraryLayout();
            RefreshList();
        }

        protected override void OnAppearing()
        {
            base.OnAppearing();
            RefreshList(selectId: _selected?.Id);
        }

        private void OnPreviewSeasonClicked(object sender, EventArgs e)
        {
            if (sender is Button { CommandParameter: SeasonCard card })
                PreviewSeason(card.Season);
        }

        private void OnSeasonCardTapped(object sender, TappedEventArgs e)
        {
            if (e.Parameter is SeasonCard card)
                PreviewSeason(card.Season);
        }

        private void OnPreviewCurrentClicked(object sender, EventArgs e)
        {
            if (_library.CurrentSeason != null)
                PreviewSeason(_library.CurrentSeason.Season);
        }

        private void PreviewSeason(Season season)
        {
            _library.Preview(season);
            PopulateEditor(season);
            ShowSeasonInfo(season);
            StatusLabel.Text = "Preview only - your working season has not changed.";
        }

        private void OnBackToLibraryClicked(object sender, EventArgs e)
        {
            if (_isFlyoutOpen) CloseFlyout();
            _library.ClosePreview();
            HideSeasonInfo();
            StatusLabel.Text = "";
        }

        protected override bool OnBackButtonPressed()
        {
            if (_isFlyoutOpen)
            {
                CloseFlyout();
                return true;
            }
            if (_selected != null)
            {
                OnBackToLibraryClicked(this, EventArgs.Empty);
                return true;
            }
            return base.OnBackButtonPressed();
        }

        private void OnLibraryFilterChanged(object sender, EventArgs e)
        {
            if (SeasonsList != null && SeasonFilter != null)
                RefreshLibraryCards();
        }

        private void UpdateLibraryLayout()
        {
            SeasonCardsLayout.Span = Width >= 1150 ? 3 : Width >= 740 ? 2 : 1;
            PageLayout.Padding = Width < 600 ? 12 : 20;
            FlyoutPanel.WidthRequest = Math.Max(280, Math.Min(440, Width - 24));
            PageSubtitle.IsVisible = Width >= 600;
            PageHeading.IsVisible = _selected == null || Width >= 600;
        }

        /// <summary>
        /// Make the given season the active one. Deactivates all others, persists, and notifies SeasonService.
        /// Called only by the explicit Use this season action, never by preview navigation.
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
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"SetActiveSeasonAsync save error: {ex.Message}");
                throw;
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
                RefreshList(selectId: _selected?.Id);
                
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

                if (isNew)
                    await _dataStore.AddSeasonAsync(model);
                else
                    await _dataStore.UpdateSeasonAsync(model);

                var jsonSeason = DataStore.Data.Seasons.FirstOrDefault(s => s.Id == model.Id);
                if (jsonSeason != null)
                    jsonSeason.BlackoutDateTitles = new Dictionary<string, string>(_exclusionTitles);
                DataStore.SaveJsonOnly();

                System.Diagnostics.Debug.WriteLine($"Season saved: {model.Name} IsActive={model.IsActive} ActiveSeasonId={DataStore.Data.ActiveSeasonId?.ToString() ?? "NULL"}");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Save error: {ex}");
                await DisplayAlert("Save Error", ex.Message, "OK");
                return;
            }

            if (SeasonService.Current.CurrentSeasonId == model.Id)
                SeasonService.Current.ForceRefresh();

            RefreshList(selectId: model.Id);
            CloseFlyout();
            StatusLabel.Text = $"Saved \"{model.Name}\". Working season unchanged.";
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

            _library.ClosePreview();
            CloseFlyout();
            RefreshList();
            StatusLabel.Text = "Season and all associated data deleted.";
        }

        private async void OnSetActiveClicked(object sender, EventArgs e)
        {
            if (_selected == null || _activationRunning) return;
            var season = _selected;
            SetActiveBtn.IsEnabled = false;
            try
            {
                await SetActiveSeasonAsync(season);
                RefreshList(selectId: _selected?.Id);
                StatusLabel.Text = $"Now using \"{season.Name}\" across the app.";
            }
            catch (Exception ex)
            {
                StatusLabel.Text = "Could not switch seasons.";
                await DisplayAlert("Switch season", ex.Message, "OK");
            }
            finally
            {
                if (_selected != null) ShowSeasonInfo(_selected);
            }
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

                    if (SeasonService.Current.CurrentSeasonId == _selected.Id)
                        SeasonService.Current.ForceRefresh();

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

                var selectedSeason = _selected;
                var seasonId = selectedSeason.Id;
                var seasonName = selectedSeason.Name;
                var savedFixtures = await _dataStore.GetFixturesAsync(seasonId);
                FixtureNumberEditor.EnsureUnplayed(savedFixtures);
                var settings = League.GetSettingsForSeason(seasonId);
                var fixtures = FixtureGenerator.Generate(
                    league: League,
                    seasonId: seasonId,
                    startDate: selectedSeason.StartDate,
                    matchNight: settings.DefaultMatchDay,
                    roundsPerOpponent: settings.DefaultRoundsPerOpponent,
                    kickoff: settings.DefaultMatchTime,
                    endDate: selectedSeason.EndDate,
                    blackoutDates: selectedSeason.BlackoutDates);

                var editor = new FixtureNumberEditor(League, seasonId, fixtures, savedFixtures);
                if (!await FixtureNumbersPage.ShowAsync(this, editor, _dataStore))
                {
                    StatusLabel.Text = "Generation canceled. Existing fixtures unchanged.";
                    return;
                }

                StatusLabel.Text = $"Generated and validated {fixtures.Count} fixtures for \"{seasonName}\". No team or home-table double bookings.";
                await DisplayAlert("Fixtures", StatusLabel.Text, "OK");
            }
            catch (Exception ex)
            {
                StatusLabel.Text = $"Error: {ex.Message}";
                await DisplayAlert("Error", ex.ToString(), "OK");
            }
            finally
            {
                RefreshList(selectId: _selected?.Id);
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

        private void RefreshList(Guid? selectId = null)
        {
            RefreshLibraryCards();
            var season = selectId.HasValue ? _items.FirstOrDefault(s => s.Id == selectId) : null;
            if (season != null)
            {
                _library.Preview(season);
                PopulateEditor(season);
                ShowSeasonInfo(season);
            }
            else
            {
                _library.ClosePreview();
                PopulateEditor(null);
            }
        }

        private void RefreshLibraryCards()
        {
            var data = League;
            _items.Clear();
            foreach (var season in data.Seasons) _items.Add(season);
            _library.Refresh(data, SeasonService.Current?.CurrentSeasonId, SeasonSearch.Text,
                (SeasonLibraryFilter)Math.Max(0, SeasonFilter.SelectedIndex));
            SeasonsList.ItemsSource = _library.Groups;
            LibraryCountLabel.Text = $"{_library.VisibleCount} season(s) · open a card to preview";
            CurrentSeasonCard.IsVisible = _library.CurrentSeason != null;
            CurrentSeasonName.Text = _library.CurrentSeason?.Name;
            CurrentSeasonSummary.Text = _library.CurrentSeason == null ? "" :
                $"{_library.CurrentSeason.Dates}\n{_library.CurrentSeason.Summary}";
        }

        private void PopulateEditor(Season? s)
        {
            if (s == null)
            {
                NameEntry.Text = string.Empty;
                StartPicker.Date = DateTime.Today;
                EndPicker.Date = DateTime.Today.AddMonths(6);
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
            if (_selected == null) return;
            _isFlyoutOpen = true;
            FlyoutOverlay.IsVisible = true;
            FlyoutPanel.IsVisible = true;

            // Animate flyout sliding in
            FlyoutPanel.TranslationX = -FlyoutPanel.WidthRequest;
            await FlyoutPanel.TranslateTo(0, 0, 250, Easing.CubicOut);
        }

        private async void CloseFlyout()
        {
            // Animate flyout sliding out
            await FlyoutPanel.TranslateTo(-FlyoutPanel.WidthRequest, 0, 250, Easing.CubicIn);
            
            FlyoutOverlay.IsVisible = false;
            FlyoutPanel.IsVisible = false;
            _isFlyoutOpen = false;
        }

        private void ShowSeasonInfo(Season season)
        {
            LibraryPanel.IsVisible = false;
            PreviewPanel.IsVisible = true;
            BackToLibraryBtn.IsVisible = true;
            PageHeading.Text = "Season overview";
            PageSubtitle.Text = "Browse first. Switch when you're ready.";
            UpdateLibraryLayout();
            EmptyStatePanel.IsVisible = false;
            SeasonInfoPanel.IsVisible = true;

            SelectedSeasonName.Text = season.Name;
            SelectedSeasonDates.Text = $"{season.StartDate:MMM d, yyyy} - {season.EndDate:MMM d, yyyy}";
            var isCurrent = SeasonService.Current?.CurrentSeasonId == season.Id;
            SelectedSeasonStatus.Text = isCurrent ? "Current working season" : "Preview only";
            SetActiveBtn.Text = isCurrent ? "Already using this season" : "Use this season";
            SetActiveBtn.IsEnabled = !isCurrent && !_activationRunning;
            PreviewHintLabel.Text = isCurrent
                ? "Teams, players, fixtures, and tables are using this season."
                : "Your working season is unchanged. Use this season to switch the rest of the app.";

            // Update badge color by setting background directly instead of style
            SelectedSeasonStatusBadge.BackgroundColor = isCurrent
                ? Color.FromArgb("#16634B")
                : Color.FromArgb("#52665D");

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
                GeneratePreviewBtn.IsEnabled = false;
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
                GeneratePreviewBtn.IsEnabled = true;
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

            RefreshList(selectId: _selected.Id);

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
            LibraryPanel.IsVisible = true;
            PreviewPanel.IsVisible = false;
            BackToLibraryBtn.IsVisible = false;
            PageHeading.Text = "Seasons";
            PageSubtitle.Text = "Your league, season by season.";
            UpdateLibraryLayout();
            EmptyStatePanel.IsVisible = true;
            SeasonInfoPanel.IsVisible = false;
        }
    }
}
