using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Season scenario planner: enter a date range, match night, number of teams,
/// rounds and competition nights to get a projected night-by-night schedule
/// for the coming season — before any fixtures are generated.
/// </summary>
public partial class SeasonScenarioPage : ContentPage
{
    private readonly IDataStore _dataStore;
    private LeagueData League => _dataStore.GetData();

    private readonly DatePicker _startPicker;
    private readonly DatePicker _endPicker;
    private readonly Picker _dayPicker;
    private readonly Picker _roundsPicker;
    private readonly Entry _bufferEntry;
    private readonly Switch _skipHolidaysSwitch;
    private readonly VerticalStackLayout _results;
    private readonly VerticalStackLayout _compList;
    private readonly VerticalStackLayout _skipList;
    private readonly VerticalStackLayout _divList;

    private sealed class DivisionEntry
    {
        public string Name = "";
        public int Teams = 8;
    }

    private readonly List<DivisionEntry> _divisions = [];

    private sealed class SkipDate
    {
        public DateTime Date;
        public string Reason = "";
    }

    private readonly List<SkipDate> _skipDates = [];

    private sealed class CompEntry
    {
        public string Name = "";
        public int Nights = 1;
        public bool Enabled = true;
    }

    private readonly List<CompEntry> _competitions =
    [
        new CompEntry { Name = "Singles KO",  Nights = 3, Enabled = false },
        new CompEntry { Name = "Doubles KO",  Nights = 2, Enabled = false },
        new CompEntry { Name = "Team KO",     Nights = 3, Enabled = false },
        new CompEntry { Name = "Captain's Cup", Nights = 1, Enabled = false },
    ];

    private static readonly string[] _dayNames =
        ["Monday", "Tuesday", "Wednesday", "Thursday", "Friday", "Saturday", "Sunday"];

    public SeasonScenarioPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        Title = "Season Scenario Planner";

        ToolbarItems.Add(new ToolbarItem
        {
            Text = "Close",
            Command = new Command(async () => await Navigation.PopModalAsync())
        });

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var labelColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");

        Label MakeLabel(string text) => new()
        {
            Text = text,
            FontSize = 13,
            TextColor = labelColor,
            VerticalOptions = LayoutOptions.Center
        };

        var today = DateTime.Today;
        // Default: next September to following April (typical league season)
        var defaultStart = new DateTime(today.Month >= 5 ? today.Year : today.Year - 1, 9, 1);
        if (defaultStart < today) defaultStart = today;

        _startPicker = new DatePicker { Date = defaultStart, Format = "dd MMM yyyy" };
        _endPicker = new DatePicker { Date = defaultStart.AddMonths(8), Format = "dd MMM yyyy" };

        _dayPicker = new Picker { Title = "Match night" };
        foreach (var d in _dayNames) _dayPicker.Items.Add(d);
        var activeSeason = League.Seasons.FirstOrDefault(s => s.IsActive);
        _dayPicker.SelectedIndex = activeSeason != null
            ? ((int)activeSeason.MatchDayOfWeek + 6) % 7
            : 1; // Tuesday

        _divList = new VerticalStackLayout { Spacing = 6 };

        // Seed divisions from the active season, falling back to one default division
        if (activeSeason != null)
        {
            foreach (var div in League.Divisions.Where(d => d.SeasonId == activeSeason.Id))
            {
                var teamCount = League.Teams.Count(t => t.SeasonId == activeSeason.Id && t.DivisionId == div.Id);
                _divisions.Add(new DivisionEntry
                {
                    Name = string.IsNullOrWhiteSpace(div.Name) ? $"Division {_divisions.Count + 1}" : div.Name,
                    Teams = Math.Max(2, teamCount)
                });
            }
        }
        if (_divisions.Count == 0)
        {
            var totalTeams = activeSeason != null
                ? Math.Max(2, League.Teams.Count(t => t.SeasonId == activeSeason.Id))
                : 10;
            _divisions.Add(new DivisionEntry { Name = "Division 1", Teams = totalTeams });
        }

        var addDivButton = new Button
        {
            Text = "\uFF0B Add division",
            BackgroundColor = Color.FromArgb("#2563EB"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            Padding = new Thickness(10, 4),
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start
        };
        addDivButton.Clicked += async (_, _) =>
        {
            var name = await DisplayPromptAsync("Add Division",
                "Division name:", initialValue: $"Division {_divisions.Count + 1}", maxLength: 40);
            if (string.IsNullOrWhiteSpace(name)) return;
            var teamsStr = await DisplayPromptAsync("Add Division",
                $"How many teams in '{name.Trim()}'?", initialValue: "8",
                maxLength: 2, keyboard: Keyboard.Numeric);
            if (!int.TryParse(teamsStr, out var t) || t < 2) t = 2;
            _divisions.Add(new DivisionEntry { Name = name.Trim(), Teams = t });
            RebuildDivList();
        };

        _roundsPicker = new Picker { Title = "Rounds" };
        _roundsPicker.Items.Add("Play each team once");
        _roundsPicker.Items.Add("Play each team twice (home & away)");
        _roundsPicker.SelectedIndex = 1;

        _compList = new VerticalStackLayout { Spacing = 6 };

        // Seed from this league's existing competitions (any season), keeping defaults too
        foreach (var compName in League.Competitions
                     .Select(c => c.Name)
                     .Where(n => !string.IsNullOrWhiteSpace(n))
                     .Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (!_competitions.Any(c => string.Equals(c.Name, compName, StringComparison.OrdinalIgnoreCase)))
                _competitions.Add(new CompEntry { Name = compName, Nights = 1, Enabled = false });
        }

        var addCompButton = new Button
        {
            Text = "\uFF0B Add competition",
            BackgroundColor = Color.FromArgb("#14B8A6"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            Padding = new Thickness(10, 4),
            HeightRequest = 32,
            HorizontalOptions = LayoutOptions.Start
        };
        addCompButton.Clicked += async (_, _) =>
        {
            var name = await DisplayPromptAsync("Add Competition",
                "Competition name (e.g. Over 50s Singles):", maxLength: 50);
            if (string.IsNullOrWhiteSpace(name)) return;
            var nightsStr = await DisplayPromptAsync("Add Competition",
                $"How many nights does '{name.Trim()}' need?", initialValue: "1",
                maxLength: 2, keyboard: Keyboard.Numeric);
            if (!int.TryParse(nightsStr, out var n) || n < 1) n = 1;
            _competitions.Add(new CompEntry { Name = name.Trim(), Nights = n, Enabled = true });
            RebuildCompList();
        };

        _bufferEntry = new Entry
        {
            Keyboard = Keyboard.Numeric,
            Placeholder = "e.g. 2",
            Text = "0",
            WidthRequest = 80
        };

        _skipHolidaysSwitch = new Switch { IsToggled = true };

        _skipList = new VerticalStackLayout { Spacing = 6 };

        var addSkipDatePicker = new DatePicker { Date = defaultStart, Format = "dd MMM yyyy" };
        var addSkipButton = new Button
        {
            Text = "\uFF0B Skip this date",
            BackgroundColor = Color.FromArgb("#8B5CF6"),
            TextColor = Colors.White,
            FontSize = 12,
            CornerRadius = 6,
            Padding = new Thickness(10, 4),
            HeightRequest = 32,
            VerticalOptions = LayoutOptions.Center
        };
        addSkipButton.Clicked += async (_, _) =>
        {
            var date = addSkipDatePicker.Date.Date;
            if (_skipDates.Any(s => s.Date == date)) return;
            var reason = await DisplayPromptAsync("Skip Date",
                $"Why is {date:ddd dd MMM yyyy} unavailable? (optional)", maxLength: 40) ?? "";
            _skipDates.Add(new SkipDate { Date = date, Reason = reason.Trim() });
            _skipDates.Sort((a, b) => a.Date.CompareTo(b.Date));
            RebuildSkipList();
        };

        var skipAddRow = new HorizontalStackLayout
        {
            Spacing = 8,
            Children = { addSkipDatePicker, addSkipButton }
        };

        var calcButton = new Button
        {
            Text = "📊 Calculate Projection",
            BackgroundColor = Color.FromArgb("#2563EB"),
            TextColor = Colors.White,
            FontAttributes = FontAttributes.Bold,
            CornerRadius = 8,
            Margin = new Thickness(0, 8)
        };
        calcButton.Clicked += (_, _) => CalculateProjection();

        _results = new VerticalStackLayout { Spacing = 6 };

        var inputGrid = new Grid
        {
            ColumnDefinitions = [new ColumnDefinition(GridLength.Auto), new ColumnDefinition(GridLength.Star)],
            RowDefinitions =
            [
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto),
                new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Auto)
            ],
            ColumnSpacing = 12,
            RowSpacing = 10
        };

        void AddRow(int row, string label, View input)
        {
            var l = MakeLabel(label);
            Grid.SetRow(l, row); Grid.SetColumn(l, 0);
            Grid.SetRow(input, row); Grid.SetColumn(input, 1);
            inputGrid.Children.Add(l);
            inputGrid.Children.Add(input);
        }

        AddRow(0, "Season start", _startPicker);
        AddRow(1, "Season end", _endPicker);
        AddRow(2, "Match night", _dayPicker);
        AddRow(3, "League format", _roundsPicker);
        AddRow(4, "Spare/buffer nights", _bufferEntry);
        AddRow(5, "Skip bank holidays", _skipHolidaysSwitch);

        RebuildDivList();
        RebuildCompList();

        var divSection = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "Divisions",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937")
                },
                new Label
                {
                    Text = "Divisions play in parallel on the same nights — the largest division sets how many league nights are needed.",
                    FontSize = 12,
                    TextColor = labelColor
                },
                _divList,
                addDivButton
            }
        };

        var compSection = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "Competitions",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937")
                },
                new Label
                {
                    Text = "Tick the competitions to include and set how many nights each needs.",
                    FontSize = 12,
                    TextColor = labelColor
                },
                _compList,
                addCompButton
            }
        };

        var skipSection = new VerticalStackLayout
        {
            Spacing = 6,
            Children =
            {
                new Label
                {
                    Text = "Skip dates",
                    FontSize = 14,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937")
                },
                new Label
                {
                    Text = "Add dates that can't be used (venue unavailable, local events, etc.).",
                    FontSize = 12,
                    TextColor = labelColor
                },
                _skipList,
                skipAddRow
            }
        };

        Content = new ScrollView
        {
            Content = new VerticalStackLayout
            {
                Padding = 16,
                Spacing = 8,
                Children =
                {
                    new Label
                    {
                        Text = "Plan the coming season: enter the dates and structure to see how the match nights map out.",
                        FontSize = 13,
                        TextColor = labelColor
                    },
                    inputGrid,
                    divSection,
                    compSection,
                    skipSection,
                    calcButton,
                    _results
                }
            }
        };
    }

    private void RebuildDivList()
    {
        _divList.Children.Clear();
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var textColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937");
        var mutedColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");

        foreach (var div in _divisions)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Star),   // name
                    new ColumnDefinition(GridLength.Auto),   // teams stepper
                    new ColumnDefinition(GridLength.Auto),   // teams label
                    new ColumnDefinition(GridLength.Auto)    // remove
                ],
                ColumnSpacing = 8
            };

            var nameLabel = new Label
            {
                Text = div.Name,
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                TextColor = textColor,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            Grid.SetColumn(nameLabel, 0);
            row.Children.Add(nameLabel);

            var teamsLabel = new Label
            {
                Text = $"{div.Teams} teams",
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center,
                TextColor = mutedColor,
                WidthRequest = 62
            };

            var stepper = new Stepper
            {
                Minimum = 2,
                Maximum = 30,
                Increment = 1,
                Value = div.Teams,
                Scale = 0.8,
                VerticalOptions = LayoutOptions.Center
            };
            stepper.ValueChanged += (_, e) =>
            {
                div.Teams = (int)e.NewValue;
                teamsLabel.Text = $"{div.Teams} teams";
            };
            Grid.SetColumn(stepper, 1);
            row.Children.Add(stepper);
            Grid.SetColumn(teamsLabel, 2);
            row.Children.Add(teamsLabel);

            var removeBtn = new Button
            {
                Text = "\u2715",
                FontSize = 11,
                Padding = new Thickness(6, 2),
                HeightRequest = 26,
                WidthRequest = 30,
                CornerRadius = 4,
                BackgroundColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#F3F4F6"),
                TextColor = Color.FromArgb("#DC2626"),
                VerticalOptions = LayoutOptions.Center
            };
            removeBtn.Clicked += (_, _) =>
            {
                if (_divisions.Count <= 1) return; // always keep at least one division
                _divisions.Remove(div);
                RebuildDivList();
            };
            Grid.SetColumn(removeBtn, 3);
            row.Children.Add(removeBtn);

            _divList.Children.Add(row);
        }
    }

    private void RebuildCompList()
    {
        _compList.Children.Clear();
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var textColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937");
        var mutedColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");

        foreach (var comp in _competitions)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),   // checkbox
                    new ColumnDefinition(GridLength.Star),   // name
                    new ColumnDefinition(GridLength.Auto),   // nights stepper
                    new ColumnDefinition(GridLength.Auto),   // nights label
                    new ColumnDefinition(GridLength.Auto)    // remove
                ],
                ColumnSpacing = 8
            };

            var check = new CheckBox { IsChecked = comp.Enabled, Color = Color.FromArgb("#F97316"), Scale = 0.8 };
            check.CheckedChanged += (_, e) => comp.Enabled = e.Value;
            Grid.SetColumn(check, 0);
            row.Children.Add(check);

            var nameLabel = new Label
            {
                Text = comp.Name,
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                TextColor = textColor,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            Grid.SetColumn(nameLabel, 1);
            row.Children.Add(nameLabel);

            var nightsLabel = new Label
            {
                Text = $"{comp.Nights} night{(comp.Nights != 1 ? "s" : "")}",
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center,
                TextColor = mutedColor,
                WidthRequest = 60
            };

            var stepper = new Stepper
            {
                Minimum = 1,
                Maximum = 20,
                Increment = 1,
                Value = comp.Nights,
                Scale = 0.8,
                VerticalOptions = LayoutOptions.Center
            };
            stepper.ValueChanged += (_, e) =>
            {
                comp.Nights = (int)e.NewValue;
                nightsLabel.Text = $"{comp.Nights} night{(comp.Nights != 1 ? "s" : "")}";
            };
            Grid.SetColumn(stepper, 2);
            row.Children.Add(stepper);
            Grid.SetColumn(nightsLabel, 3);
            row.Children.Add(nightsLabel);

            var removeBtn = new Button
            {
                Text = "\u2715",
                FontSize = 11,
                Padding = new Thickness(6, 2),
                HeightRequest = 26,
                WidthRequest = 30,
                CornerRadius = 4,
                BackgroundColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#F3F4F6"),
                TextColor = Color.FromArgb("#DC2626"),
                VerticalOptions = LayoutOptions.Center
            };
            removeBtn.Clicked += (_, _) =>
            {
                _competitions.Remove(comp);
                RebuildCompList();
            };
            Grid.SetColumn(removeBtn, 4);
            row.Children.Add(removeBtn);

            _compList.Children.Add(row);
        }
    }

    private void RebuildSkipList()
    {
        _skipList.Children.Clear();
        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var textColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937");
        var mutedColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");

        foreach (var skip in _skipDates)
        {
            var row = new Grid
            {
                ColumnDefinitions =
                [
                    new ColumnDefinition(GridLength.Auto),   // date
                    new ColumnDefinition(GridLength.Star),   // reason
                    new ColumnDefinition(GridLength.Auto)    // remove
                ],
                ColumnSpacing = 8
            };

            var dateLabel = new Label
            {
                Text = skip.Date.ToString("ddd dd MMM yyyy"),
                FontSize = 13,
                VerticalOptions = LayoutOptions.Center,
                TextColor = textColor
            };
            Grid.SetColumn(dateLabel, 0);
            row.Children.Add(dateLabel);

            var reasonLabel = new Label
            {
                Text = string.IsNullOrEmpty(skip.Reason) ? "unavailable" : skip.Reason,
                FontSize = 12,
                VerticalOptions = LayoutOptions.Center,
                TextColor = mutedColor,
                LineBreakMode = LineBreakMode.TailTruncation
            };
            Grid.SetColumn(reasonLabel, 1);
            row.Children.Add(reasonLabel);

            var removeBtn = new Button
            {
                Text = "\u2715",
                FontSize = 11,
                Padding = new Thickness(6, 2),
                HeightRequest = 26,
                WidthRequest = 30,
                CornerRadius = 4,
                BackgroundColor = isDark ? Color.FromArgb("#374151") : Color.FromArgb("#F3F4F6"),
                TextColor = Color.FromArgb("#DC2626"),
                VerticalOptions = LayoutOptions.Center
            };
            removeBtn.Clicked += (_, _) =>
            {
                _skipDates.Remove(skip);
                RebuildSkipList();
            };
            Grid.SetColumn(removeBtn, 2);
            row.Children.Add(removeBtn);

            _skipList.Children.Add(row);
        }
    }

    private void CalculateProjection()
    {
        _results.Children.Clear();

        var isDark = Application.Current?.RequestedTheme == AppTheme.Dark;
        var textColor = isDark ? Color.FromArgb("#E5E7EB") : Color.FromArgb("#1F2937");
        var mutedColor = isDark ? Color.FromArgb("#9CA3AF") : Color.FromArgb("#6B7280");

        void AddResult(string text, Color? color = null, bool bold = false, double size = 13)
        {
            _results.Children.Add(new Label
            {
                Text = text,
                FontSize = size,
                FontAttributes = bold ? FontAttributes.Bold : FontAttributes.None,
                TextColor = color ?? textColor
            });
        }

        var start = _startPicker.Date.Date;
        var end = _endPicker.Date.Date;
        if (end <= start)
        {
            AddResult("⚠️ End date must be after the start date.", Color.FromArgb("#DC2626"), true);
            return;
        }

        if (_divisions.Count == 0)
        {
            AddResult("⚠️ Add at least one division with 2 or more teams.", Color.FromArgb("#DC2626"), true);
            return;
        }

        int.TryParse(_bufferEntry.Text?.Trim(), out int bufferNights);
        bufferNights = Math.Max(0, bufferNights);

        // Expand selected competitions into an ordered list of named nights
        var compNights = new List<string>();
        foreach (var comp in _competitions.Where(c => c.Enabled))
            for (int i = 1; i <= comp.Nights; i++)
                compNights.Add(comp.Nights > 1 ? $"{comp.Name} — night {i} of {comp.Nights}" : comp.Name);
        int competitionNights = compNights.Count;

        var matchDay = (DayOfWeek)(((_dayPicker.SelectedIndex < 0 ? 1 : _dayPicker.SelectedIndex) + 1) % 7);
        bool doubleRound = _roundsPicker.SelectedIndex != 0;
        bool skipHolidays = _skipHolidaysSwitch.IsToggled;

        // Round-robin structure per division (divisions play in parallel, so the
        // largest division determines the number of league nights needed).
        // Even team count: n-1 rounds per half; odd: n rounds (one team sits out).
        int RoundsPerHalf(int t) => t % 2 == 0 ? t - 1 : t;
        int roundsPerHalf = _divisions.Max(d => RoundsPerHalf(d.Teams));
        int leagueNights = doubleRound ? roundsPerHalf * 2 : roundsPerHalf;
        int totalTeams = _divisions.Sum(d => d.Teams);
        int matchesPerNight = _divisions.Sum(d => d.Teams / 2);

        var holidays = skipHolidays ? GetBankHolidays(start.Year, end.Year) : [];
        var skipMap = _skipDates.ToDictionary(s => s.Date,
            s => string.IsNullOrEmpty(s.Reason) ? "Unavailable" : s.Reason);

        // Collect available match nights in range
        var nights = new List<(DateTime Date, string? Holiday)>();
        for (var d = start; d <= end; d = d.AddDays(1))
        {
            if (d.DayOfWeek != matchDay) continue;
            if (skipMap.TryGetValue(d, out var skipReason)) { nights.Add((d, skipReason)); continue; }
            holidays.TryGetValue(d, out var holName);
            if (skipHolidays && holName != null) { nights.Add((d, holName)); continue; }
            nights.Add((d, null));
        }

        var usableNights = nights.Where(n => n.Holiday == null).ToList();
        int neededNights = leagueNights + competitionNights + bufferNights;

        // ── Summary ──
        AddResult("── Projection Summary ──", mutedColor, true, 15);
        AddResult($"{totalTeams} teams in {_divisions.Count} division{(_divisions.Count != 1 ? "s" : "")} · up to {matchesPerNight} match{(matchesPerNight != 1 ? "es" : "")} per night · {(doubleRound ? "home & away" : "single round")}");
        foreach (var div in _divisions)
        {
            int divNights = doubleRound ? RoundsPerHalf(div.Teams) * 2 : RoundsPerHalf(div.Teams);
            AddResult($"   \u2022 {div.Name}: {div.Teams} teams → {divNights} league night{(divNights != 1 ? "s" : "")}" +
                      (div.Teams % 2 != 0 ? " (odd — one team sits out each night)" : ""), mutedColor);
        }
        AddResult($"League nights needed (longest division): {leagueNights}");
        if (competitionNights > 0)
        {
            AddResult($"Competition nights: {competitionNights}");
            foreach (var comp in _competitions.Where(c => c.Enabled))
                AddResult($"   \u2022 {comp.Name}: {comp.Nights} night{(comp.Nights != 1 ? "s" : "")}", mutedColor);
        }
        if (bufferNights > 0) AddResult($"Spare/buffer nights: {bufferNights}");
        AddResult($"Total nights needed: {neededNights}", bold: true);
        AddResult($"{matchDay}s available {start:dd MMM yyyy} – {end:dd MMM yyyy}: {usableNights.Count}" +
                  (nights.Count > usableNights.Count ? $" ({nights.Count - usableNights.Count} lost to bank holidays/skipped dates)" : ""),
                  bold: true);

        if (usableNights.Count >= neededNights)
        {
            int spare = usableNights.Count - neededNights;
            var finishNight = usableNights[neededNights - 1].Date;
            AddResult($"✅ Fits! Season would finish by {finishNight:ddd dd MMM yyyy} with {spare} spare night{(spare != 1 ? "s" : "")}.",
                Color.FromArgb("#10B981"), true, 14);
        }
        else
        {
            int shortBy = neededNights - usableNights.Count;
            var projectedEnd = ProjectEndDate(end, matchDay, shortBy, skipHolidays, holidays, skipMap);
            AddResult($"❌ Doesn't fit — short by {shortBy} night{(shortBy != 1 ? "s" : "")}. " +
                      $"You'd need to run until {projectedEnd:ddd dd MMM yyyy}, reduce competitions, or change the format.",
                Color.FromArgb("#DC2626"), true, 14);
        }

        // ── Night-by-night projection ──
        AddResult("");
        AddResult("── Night-by-Night Projection ──", mutedColor, true, 15);

        int leagueRound = 0, compNumber = 0, bufferNumber = 0, halfway = doubleRound ? roundsPerHalf : leagueNights;
        int assigned = 0;
        foreach (var (date, holiday) in nights)
        {
            string label;
            Color color = textColor;
            if (holiday != null)
            {
                label = $"🏦 {holiday} — no matches";
                color = Color.FromArgb("#EC4899");
            }
            else if (assigned < leagueNights)
            {
                leagueRound++;
                assigned++;
                var half = doubleRound ? (leagueRound <= halfway ? " (1st half)" : " (2nd half)") : "";
                label = $"🎱 League round {leagueRound}{half}";
            }
            else if (assigned < leagueNights + competitionNights)
            {
                assigned++;
                label = $"🏆 {compNights[compNumber]}";
                compNumber++;
                color = Color.FromArgb("#F97316");
            }
            else if (assigned < neededNights)
            {
                bufferNumber++;
                assigned++;
                label = $"📌 Spare night {bufferNumber} (catch-ups)";
                color = mutedColor;
            }
            else
            {
                label = "— free";
                color = Color.FromArgb("#10B981");
            }

            AddResult($"{date:ddd dd MMM yyyy}   {label}", color);
        }

        // Overflow nights beyond the end date, if the plan doesn't fit
        if (usableNights.Count < neededNights)
        {
            AddResult("");
            AddResult("── Overflow (beyond your end date) ──", Color.FromArgb("#DC2626"), true);
            int remaining = neededNights - usableNights.Count;
            var d = end;
            while (remaining > 0)
            {
                d = d.AddDays(1);
                if (d.DayOfWeek != matchDay) continue;
                if (skipMap.ContainsKey(d)) continue;
                if (skipHolidays && GetBankHolidays(d.Year, d.Year).ContainsKey(d)) continue;
                assigned++;
                remaining--;
                string label = assigned <= leagueNights
                    ? $"🎱 League round {++leagueRound}"
                    : assigned <= leagueNights + competitionNights
                        ? $"🏆 {compNights[compNumber++]}"
                        : $"📌 Spare night {++bufferNumber}";
                AddResult($"{d:ddd dd MMM yyyy}   {label}", Color.FromArgb("#DC2626"));
            }
        }
    }

    private static DateTime ProjectEndDate(DateTime from, DayOfWeek matchDay, int nightsNeeded,
        bool skipHolidays, Dictionary<DateTime, string> holidays, Dictionary<DateTime, string>? skipDates = null)
    {
        var d = from;
        int found = 0;
        while (found < nightsNeeded)
        {
            d = d.AddDays(1);
            if (d.DayOfWeek != matchDay) continue;
            if (skipDates?.ContainsKey(d) == true) continue;
            if (skipHolidays && (holidays.ContainsKey(d) || GetBankHolidays(d.Year, d.Year).ContainsKey(d))) continue;
            found++;
        }
        return d;
    }

    // ── UK bank holidays (England & Wales) ──

    private static Dictionary<DateTime, string> GetBankHolidays(int startYear, int endYear)
    {
        var holidays = new Dictionary<DateTime, string>();
        for (int year = startYear; year <= endYear; year++)
        {
            AddHoliday(holidays, NextWorkingDay(new DateTime(year, 1, 1)), "New Year's Day");
            var easter = EasterSunday(year);
            AddHoliday(holidays, easter.AddDays(-2), "Good Friday");
            AddHoliday(holidays, easter.AddDays(1), "Easter Monday");
            AddHoliday(holidays, FirstMonday(year, 5), "Early May Bank Holiday");
            AddHoliday(holidays, LastMonday(year, 5), "Spring Bank Holiday");
            AddHoliday(holidays, LastMonday(year, 8), "Summer Bank Holiday");
            AddHoliday(holidays, NextWorkingDay(new DateTime(year, 12, 25)), "Christmas Day");
            AddHoliday(holidays, NextWorkingDay(new DateTime(year, 12, 26), skip: holidays.Keys), "Boxing Day");
        }
        return holidays;
    }

    private static void AddHoliday(Dictionary<DateTime, string> d, DateTime date, string name)
    {
        if (!d.ContainsKey(date)) d[date] = name;
    }

    private static DateTime NextWorkingDay(DateTime date, IEnumerable<DateTime>? skip = null)
    {
        var skipSet = skip != null ? new HashSet<DateTime>(skip) : null;
        while (date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday
               || (skipSet?.Contains(date) ?? false))
            date = date.AddDays(1);
        return date;
    }

    private static DateTime FirstMonday(int year, int month)
    {
        var d = new DateTime(year, month, 1);
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(1);
        return d;
    }

    private static DateTime LastMonday(int year, int month)
    {
        var d = new DateTime(year, month, DateTime.DaysInMonth(year, month));
        while (d.DayOfWeek != DayOfWeek.Monday) d = d.AddDays(-1);
        return d;
    }

    /// <summary>Anonymous Gregorian algorithm for Easter Sunday.</summary>
    private static DateTime EasterSunday(int year)
    {
        int a = year % 19;
        int b = year / 100;
        int c = year % 100;
        int d = b / 4;
        int e = b % 4;
        int f = (b + 8) / 25;
        int g = (b - f + 1) / 3;
        int h = (19 * a + b - d - g + 15) % 30;
        int i = c / 4;
        int k = c % 4;
        int l = (32 + 2 * e + 2 * i - h - k) % 7;
        int m = (a + 11 * h + 22 * l) / 451;
        int month = (h + l - 7 * m + 114) / 31;
        int day = ((h + l - 7 * m + 114) % 31) + 1;
        return new DateTime(year, month, day);
    }
}
