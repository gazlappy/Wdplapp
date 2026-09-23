using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Services;
using static Wdpl2.Helpers.PanelBuilder;

namespace Wdpl2.Views
{
    /// <summary>
    /// First-run setup: takes someone with an empty app to a league with a
    /// season, divisions, teams and venues, ready for fixtures.
    /// </summary>
    /// <remarks>
    /// Opens by itself when there are no seasons at all. Each step builds its
    /// controls from <see cref="NewLeagueSetup"/> and writes them back when
    /// left, so Back never loses what was typed.
    /// </remarks>
    public class SetupWizardPage : ContentPage
    {
        private static readonly Color Accent = Color.FromArgb("#16634B");

        private readonly IDataStore _dataStore;
        private readonly NewLeagueSetup _setup = new();

        private readonly Label _stepLabel = new() { FontSize = 13, FontAttributes = FontAttributes.Bold };
        private readonly ProgressBar _progress = new() { ProgressColor = Accent };
        private readonly ContentView _body = new();
        private readonly Button _back = new() { Text = "Back", AutomationId = "setup-back" };
        private readonly Button _next = new() { Text = "Next", AutomationId = "setup-next" };
        private readonly Grid _footer;
        private readonly Grid _header;

        private readonly List<(string Title, Func<View> Build)> _steps;
        private int _step;

        /// <summary>Writes the current step's controls into the setup; false keeps the step open.</summary>
        private Func<Task<bool>> _leave = () => Task.FromResult(true);

        public SetupWizardPage(IDataStore dataStore)
        {
            _dataStore = dataStore;
            Title = "Set up your league";

            var year = DateTime.Today.Year;
            _setup.SeasonName = $"{year}/{(year + 1) % 100:00}";

            var defaults = dataStore.GetData().Settings;
            _setup.MatchDay = defaults.DefaultMatchDay;
            _setup.MatchTime = defaults.DefaultMatchTime;
            if (defaults.DefaultFramesPerMatch > 0)
                _setup.FramesPerMatch = defaults.DefaultFramesPerMatch;

            _steps =
            [
                ("Welcome", WelcomeStep),
                ("League and season", LeagueStep),
                ("Divisions", DivisionsStep),
                ("Teams", TeamsStep),
                ("Venues", VenuesStep),
                ("Home venues", HomeVenuesStep),
                ("Check and create", ReviewStep),
            ];

            _back.Clicked += async (_, _) => await MoveAsync(-1);
            _next.SetDynamicResource(StyleProperty, "PrimaryButtonStyle");
            _next.Clicked += async (_, _) => await MoveAsync(+1);

            _header = new Grid
            {
                RowDefinitions = { new(GridLength.Auto), new(GridLength.Auto) },
                RowSpacing = 6,
                Padding = new Thickness(24, 20, 24, 8),
            };
            _header.Add(_stepLabel, 0, 0);
            _header.Add(_progress, 0, 1);

            _footer = new Grid
            {
                ColumnDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
                Padding = new Thickness(24, 12, 24, 20),
            };
            _footer.Add(_back, 0, 0);
            _footer.Add(_next, 2, 0);

            var column = new Grid
            {
                MaximumWidthRequest = 680,
                RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            };
            column.Add(_header, 0, 0);
            column.Add(new ScrollView { Content = _body, Padding = new Thickness(24, 8) }, 0, 1);
            column.Add(_footer, 0, 2);

            Content = column;
            Show(0);
        }

        // Closing the window's back arrow on a phone must not skip the choice.
        protected override bool OnBackButtonPressed() => true;

        private void Show(int step)
        {
            _step = step;
            _leave = () => Task.FromResult(true);
            _next.IsEnabled = true;
            _body.Content = _steps[step].Build();

            // The welcome has its own buttons; the footer is for the steps.
            var welcome = step == 0;
            _header.IsVisible = !welcome;
            _footer.IsVisible = !welcome;
            _stepLabel.Text = $"Step {step} of {_steps.Count - 1}: {_steps[step].Title}";
            _progress.Progress = (double)step / (_steps.Count - 1);
            _next.Text = step == _steps.Count - 1 ? "Create league" : "Next";
        }

        private async Task MoveAsync(int by)
        {
            if (by > 0 && !await _leave()) return;
            if (by < 0) await _leave(); // keep what was typed; checks wait for Next

            var target = _step + by;

            // No venues, or no teams, means there is nothing to match up.
            if (_steps[Math.Clamp(target, 0, _steps.Count - 1)].Title == "Home venues"
                && (_setup.Venues.Count == 0 || !_setup.AllTeams.Any()))
                target += by;

            if (target >= _steps.Count)
            {
                await CreateAsync();
                return;
            }
            Show(Math.Clamp(target, 0, _steps.Count - 1));
        }

        // ───────────────────────────── steps

        private View WelcomeStep()
        {
            var start = new Button { Text = "Start a new league", AutomationId = "setup-start", HorizontalOptions = LayoutOptions.Fill };
            start.SetDynamicResource(StyleProperty, "PrimaryButtonStyle");
            start.Clicked += (_, _) => Show(1);

            var restore = new Button { Text = "Restore from a backup", AutomationId = "setup-restore", HorizontalOptions = LayoutOptions.Fill };
            restore.Clicked += async (_, _) => await RestoreAsync();

            var skip = new Button
            {
                Text = "Skip for now",
                AutomationId = "setup-skip",
                BackgroundColor = Colors.Transparent,
                TextColor = Accent,
                BorderWidth = 0,
            };
            skip.Clicked += async (_, _) => await Navigation.PopModalAsync();

            return new VerticalStackLayout
            {
                Spacing = 16,
                Padding = new Thickness(0, 48, 0, 0),
                Children =
                {
                    new Label { Text = $"Welcome to {Product.Name}", FontSize = 28, FontAttributes = FontAttributes.Bold, TextColor = TitleText },
                    Text("A few questions and your league is ready: its first season, divisions, teams and venues. "
                         + "It takes a couple of minutes, and everything can be changed afterwards."),
                    new VerticalStackLayout { Spacing = 10, Margin = new Thickness(0, 12, 0, 0), Children = { start, restore } },
                    Text("Moving from another computer? Restore the backup you made there - in the app's Settings, under Data Tools."),
                    skip,
                },
            };
        }

        private View LeagueStep()
        {
            var league = Field("League name", _setup.LeagueName, "e.g. Riverside Pool League", "setup-league-name");
            var season = Field("Season name", _setup.SeasonName, "e.g. 2026/27", "setup-season-name");

            var start = new DatePicker { Date = _setup.StartDate, Format = "d MMM yyyy" };
            var end = new DatePicker { Date = _setup.EndDate, Format = "d MMM yyyy" };

            var day = new Picker();
            foreach (var name in Enum.GetNames<DayOfWeek>()) day.Items.Add(name);
            day.SelectedIndex = (int)_setup.MatchDay;

            var time = new TimePicker { Time = _setup.MatchTime, Format = "HH:mm" };
            var frames = new Entry { Text = _setup.FramesPerMatch.ToString(), Keyboard = Keyboard.Numeric, WidthRequest = 90, HorizontalOptions = LayoutOptions.Start };

            _leave = async () =>
            {
                _setup.LeagueName = league.Entry.Text?.Trim() ?? "";
                _setup.SeasonName = season.Entry.Text?.Trim() ?? "";
                _setup.StartDate = start.Date;
                _setup.EndDate = end.Date;
                _setup.MatchDay = (DayOfWeek)Math.Max(0, day.SelectedIndex);
                _setup.MatchTime = time.Time;
                _setup.FramesPerMatch = int.TryParse(frames.Text, out var n) ? n : 0;

                var problems = _setup.LeagueProblems();
                if (problems.Count == 0) return true;
                await DisplayAlert("Nearly there", string.Join("\n", problems), "OK");
                return false;
            };

            return Step("Name the league, and say when its first season runs.",
                league.Row,
                season.Row,
                Pair(Labelled("Starts", start), Labelled("Ends", end)),
                Pair(Labelled("Match night", day), Labelled("Start time", time)),
                Labelled("Frames in a match", frames));
        }

        private View DivisionsStep()
        {
            var box = LinesBox(_setup.Divisions.Count > 0 ? _setup.Divisions : ["Division 1"], "setup-divisions");

            _leave = async () =>
            {
                var names = NewLeagueSetup.Lines(box.Text);
                if (names.Count == 0)
                {
                    await DisplayAlert("Nearly there", "Add at least one division. A small league can have just one.", "OK");
                    return false;
                }
                _setup.Divisions.Clear();
                _setup.Divisions.AddRange(names);
                return true;
            };

            return Step("One division per line, top division first. A small league can have just one.", box);
        }

        private View TeamsStep()
        {
            var boxes = _setup.Divisions
                .Select(d => (Division: d, Box: LinesBox(_setup.TeamsByDivision.GetValueOrDefault(d) ?? [], "setup-teams-" + d)))
                .ToList();

            _leave = async () =>
            {
                _setup.TeamsByDivision.Clear();
                foreach (var (division, box) in boxes)
                    _setup.TeamsByDivision[division] = NewLeagueSetup.Lines(box.Text);

                var problems = _setup.TeamProblems();
                if (problems.Count == 0) return true;
                await DisplayAlert("Nearly there", string.Join("\n", problems), "OK");
                return false;
            };

            var views = new List<View>();
            foreach (var (division, box) in boxes)
                views.Add(Labelled(division, box));
            return Step("Type each team on its own line. Players can be added later, on the Players page.", views.ToArray());
        }

        private View VenuesStep()
        {
            var box = LinesBox(_setup.Venues, "setup-venues");

            _leave = () =>
            {
                _setup.Venues.Clear();
                _setup.Venues.AddRange(NewLeagueSetup.Lines(box.Text));
                return Task.FromResult(true);
            };

            return Step("Where matches are played - one pub or club per line. You can leave this empty and add them later on the Venues page.", box);
        }

        private View HomeVenuesStep()
        {
            const string none = "(not yet)";
            var pickers = new List<(string Team, Picker Picker)>();
            var rows = new List<View>();

            foreach (var team in _setup.AllTeams)
            {
                var picker = new Picker { WidthRequest = 260 };
                picker.Items.Add(none);
                foreach (var venue in _setup.Venues) picker.Items.Add(venue);

                // A team is usually named after its pub: "Red Lion A" plays at the Red Lion.
                var chosen = _setup.HomeVenues.GetValueOrDefault(team)
                             ?? _setup.Venues.FirstOrDefault(v => team.StartsWith(v, StringComparison.OrdinalIgnoreCase)
                                                                 || team.StartsWith("The " + v, StringComparison.OrdinalIgnoreCase)
                                                                 || ("The " + team).StartsWith(v, StringComparison.OrdinalIgnoreCase));
                picker.SelectedIndex = chosen is null ? 0 : Math.Max(0, picker.Items.IndexOf(chosen));

                pickers.Add((team, picker));
                var row = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) } };
                row.Add(new Label { Text = team, VerticalOptions = LayoutOptions.Center, TextColor = BodyText }, 0, 0);
                row.Add(picker, 1, 0);
                rows.Add(row);
            }

            _leave = () =>
            {
                _setup.HomeVenues.Clear();
                foreach (var (team, picker) in pickers)
                    if (picker.SelectedIndex > 0)
                        _setup.HomeVenues[team] = picker.Items[picker.SelectedIndex];
                return Task.FromResult(true);
            };

            return Step("Where each team plays at home. Teams named after their venue are filled in already.", rows.ToArray());
        }

        private View ReviewStep()
        {
            var teams = _setup.AllTeams.ToList();
            var summary =
                $"{_setup.LeagueName}\n" +
                $"Season {_setup.SeasonName}: {_setup.StartDate:d MMM yyyy} to {_setup.EndDate:d MMM yyyy}, " +
                $"{_setup.MatchDay}s at {_setup.MatchTime:hh\\:mm}, {_setup.FramesPerMatch} frames a match.\n\n" +
                string.Join("\n", _setup.Divisions.Select(d =>
                    $"{d}: {string.Join(", ", _setup.TeamsByDivision.GetValueOrDefault(d) ?? [])}")) +
                "\n\n" +
                (_setup.Venues.Count == 0
                    ? "No venues yet."
                    : $"Venues: {string.Join(", ", _setup.Venues)}. " +
                      $"{_setup.HomeVenues.Count} of {teams.Count} teams have a home venue.");

            var problems = _setup.Problems();
            _next.IsEnabled = problems.Count == 0;

            return Step(problems.Count == 0
                    ? "Here is your league. Create it, and the next thing is to make the fixtures."
                    : "A few things need putting right first - use Back to reach them.",
                Card(new Label { Text = summary, FontSize = 14, LineHeight = 1.4, TextColor = BodyText }),
                problems.Count == 0 ? new ContentView() : WarningBanner(string.Join("\n", problems)));
        }

        // ───────────────────────────── finishing

        private async Task CreateAsync()
        {
            _next.IsEnabled = false;
            try
            {
                var season = await _setup.SaveAsync(_dataStore);

                DataStore.Data.ActiveSeasonId = season.Id;
                DataStore.Data.WebsiteSettings.LeagueName = _setup.LeagueName;
                DataStore.SaveJsonOnly();
                SeasonService.Current.CurrentSeasonId = season.Id;
                SeasonService.Current.ForceRefresh();

                await Navigation.PopModalAsync();
                await Shell.Current.GoToAsync("//Fixtures");
                await Shell.Current.DisplayAlert("Your league is ready",
                    $"{_setup.LeagueName} is set up. Next, make the season's fixtures: open the menu button at the top "
                    + "of this page and choose Generate Fixtures.",
                    "OK");
            }
            catch (Exception ex)
            {
                _next.IsEnabled = true;
                await DisplayAlert("Could not create the league", ex.Message, "OK");
            }
        }

        private async Task RestoreAsync()
        {
            var picked = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Choose a backup",
                FileTypes = new FilePickerFileType(new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    [DevicePlatform.WinUI] = [".zip"],
                    [DevicePlatform.MacCatalyst] = ["zip"],
                    [DevicePlatform.Android] = ["application/zip"],
                    [DevicePlatform.iOS] = ["public.zip-archive"],
                }),
            });
            if (picked is null) return;

            try
            {
                BackupService.StageRestore(picked.FullPath);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Cannot restore", ex.Message, "OK");
                return;
            }

            await DisplayAlert("Restore", "The app will close now. Open it again and your league will be there.", "OK");
            Application.Current?.Quit();
        }

        // ───────────────────────────── building blocks

        private static View Step(string intro, params View[] parts)
        {
            var stack = new VerticalStackLayout { Spacing = 16, Padding = new Thickness(0, 8, 0, 16) };
            stack.Children.Add(Text(intro));
            foreach (var part in parts) stack.Children.Add(part);
            return stack;
        }

        private static Label Text(string text) =>
            new() { Text = text, FontSize = 15, LineHeight = 1.35, TextColor = BodyText };

        private static (View Row, Entry Entry) Field(string label, string value, string placeholder, string automationId)
        {
            var entry = new Entry { Text = value, Placeholder = placeholder, AutomationId = automationId };
            return (Labelled(label, entry), entry);
        }

        private static View Labelled(string label, View control) => new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = label, FontSize = 13, FontAttributes = FontAttributes.Bold, TextColor = BodyText },
                control,
            },
        };

        private static View Pair(View left, View right)
        {
            var grid = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 16 };
            grid.Add(left, 0, 0);
            grid.Add(right, 1, 0);
            return grid;
        }

        private static Editor LinesBox(IEnumerable<string> lines, string automationId) => new()
        {
            Text = string.Join("\n", lines),
            AutomationId = automationId,
            HeightRequest = 180,
            AutoSize = EditorAutoSizeOption.Disabled,
        };
    }
}
