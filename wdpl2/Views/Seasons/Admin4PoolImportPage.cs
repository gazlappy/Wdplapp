using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Services.Admin4Pool;

namespace Wdpl2.Views
{
    /// <summary>
    /// Review of seasons read from Admin4Pool exports, before any of them is saved.
    /// </summary>
    /// <remarks>
    /// Each season can be renamed or left out. Nothing is written until Import,
    /// and then every season listed goes in together or none does.
    /// </remarks>
    public class Admin4PoolImportPage : ContentPage
    {
        private static readonly Color Green = Color.FromArgb("#16634B");
        private static readonly Color Warn = Color.FromArgb("#9A5B00");
        private static readonly Color Bad = Color.FromArgb("#B3261E");

        private readonly Admin4PoolImport _import;
        private readonly Action _onImported;
        private readonly VerticalStackLayout _cards = new() { Spacing = 14 };
        private readonly Button _importButton;
        private readonly Label _status = new() { FontSize = 13, VerticalOptions = LayoutOptions.Center };
        private readonly Dictionary<Admin4PoolSeasonPlan, Label> _nameProblems = new();

        public Admin4PoolImportPage(Admin4PoolImport import, IEnumerable<string> unreadable, Action onImported)
        {
            _import = import;
            _onImported = onImported;
            Title = "Import from Admin4Pool";
            NavigationPage.SetHasNavigationBar(this, false);
            this.SetAppThemeColor(BackgroundColorProperty, Color.FromArgb("#F5F7F6"), Color.FromArgb("#111A17"));

            var intro = new Label
            {
                FontSize = 13,
                Text = "Each file becomes a new season with its own teams, players, venues and results. " +
                       "Seasons already here aren't changed. Players aren't linked to earlier seasons; " +
                       "use the duplicate finder on the Players page for that once they're in.",
            };
            intro.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#52665D"), Color.FromArgb("#ABC1B6"));

            var header = new VerticalStackLayout
            {
                Spacing = 4,
                Children = { new Label { Text = "Import from Admin4Pool", FontSize = 28, FontAttributes = FontAttributes.Bold }, intro },
            };
            foreach (var name in unreadable)
                header.Children.Add(new Label { Text = "Couldn't read " + name, TextColor = Bad, FontSize = 13 });

            var cancel = new Button { Text = "Cancel", BackgroundColor = Colors.Transparent, TextColor = Green, BorderColor = Green, BorderWidth = 1 };
            cancel.Clicked += async (_, _) => await Navigation.PopModalAsync();
            _importButton = new Button { BackgroundColor = Green, TextColor = Colors.White };
            _importButton.Clicked += OnImportClicked;

            var footer = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto), new(GridLength.Auto) }, ColumnSpacing = 10 };
            footer.Add(_status, 0, 0);
            footer.Add(cancel, 1, 0);
            footer.Add(_importButton, 2, 0);

            var layout = new Grid
            {
                Padding = 20,
                RowSpacing = 16,
                RowDefinitions = { new(GridLength.Auto), new(GridLength.Star), new(GridLength.Auto) },
            };
            layout.Add(header, 0, 0);
            layout.Add(new ScrollView { Content = _cards }, 0, 1);
            layout.Add(footer, 0, 2);
            Content = layout;

            foreach (var plan in _import.Plans) _cards.Children.Add(Card(plan));
            Refresh();
        }

        private View Card(Admin4PoolSeasonPlan plan)
        {
            var name = new Entry { Text = plan.Season.Name, FontSize = 20, FontAttributes = FontAttributes.Bold, Placeholder = "Season name" };
            var problem = new Label { FontSize = 12, TextColor = Bad, IsVisible = false };
            _nameProblems[plan] = problem;
            name.TextChanged += (_, e) => { plan.Season.Name = e.NewTextValue ?? ""; Refresh(); };

            var remove = new Button { Text = "Leave out", BackgroundColor = Colors.Transparent, TextColor = Green, FontSize = 13, VerticalOptions = LayoutOptions.Start };
            var top = new Grid { ColumnDefinitions = { new(GridLength.Star), new(GridLength.Auto) }, ColumnSpacing = 8 };
            top.Add(name, 0, 0);
            top.Add(remove, 1, 0);

            var season = plan.Season;
            var body = new VerticalStackLayout
            {
                Spacing = 6,
                Children =
                {
                    top,
                    problem,
                    Muted($"From {plan.SourceName} - \"{plan.LeagueName}\", season \"{plan.OriginalSeason}\""),
                    Line($"{season.StartDate:d MMM yyyy} to {season.EndDate:d MMM yyyy}, {season.MatchDayOfWeek}s"),
                    Line($"{plan.Divisions.Count} divisions, {plan.Teams.Count} teams, {plan.Players.Count} players, {plan.Venues.Count} venues"),
                    Line($"{plan.PlayedFixtures} matches: {plan.SinglesFrames:N0} singles frames" +
                         (plan.DoublesFrames > 0 ? $", {plan.DoublesFrames:N0} doubles frames" : "") +
                         $", {plan.EightBalls} eight-balls"),
                    TableCheck(plan),
                },
            };
            foreach (var warning in plan.Warnings)
                body.Children.Add(new Label { Text = "• " + warning, FontSize = 12, TextColor = Warn });

            var card = new Border
            {
                Padding = 18,
                StrokeShape = new RoundRectangle { CornerRadius = 16 },
                Content = body,
            };
            card.SetAppThemeColor(Border.StrokeProperty, Color.FromArgb("#DCE5DF"), Color.FromArgb("#354B40"));
            card.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#1C2922"));
            remove.Clicked += (_, _) =>
            {
                _import.Remove(plan);
                _nameProblems.Remove(plan);
                _cards.Children.Remove(card);
                Refresh();
            };
            return card;
        }

        private static View TableCheck(Admin4PoolSeasonPlan plan)
        {
            if (plan.TableDifferences.Count == 0)
                return new Label
                {
                    Text = $"✓ All {plan.TeamsChecked} teams' points match the table Admin4Pool saved",
                    FontSize = 13, TextColor = Green, FontAttributes = FontAttributes.Bold,
                };
            var box = new VerticalStackLayout { Spacing = 2 };
            box.Children.Add(new Label
            {
                Text = $"⚠ {plan.TableDifferences.Count} of {plan.TeamsChecked} teams' points differ from the table Admin4Pool saved:",
                FontSize = 13, TextColor = Warn, FontAttributes = FontAttributes.Bold,
            });
            foreach (var d in plan.TableDifferences)
                box.Children.Add(new Label { Text = "   " + d, FontSize = 12, TextColor = Warn });
            return box;
        }

        private void Refresh()
        {
            var ok = _import.Plans.Count > 0;
            foreach (var (plan, label) in _nameProblems)
            {
                var problem = _import.NameProblem(plan, plan.Season.Name);
                label.Text = problem ?? "";
                label.IsVisible = problem != null;
                ok &= problem == null;
            }
            var count = _import.Plans.Count;
            _importButton.Text = count == 1 ? "Import 1 season" : $"Import {count} seasons";
            _importButton.IsEnabled = ok;
            _status.Text = count == 0 ? "Nothing left to import." : ok ? "" : "Fix the names marked in red to continue.";
        }

        private async void OnImportClicked(object? sender, EventArgs e)
        {
            _importButton.IsEnabled = false;
            _status.Text = "Importing…";
            try
            {
                await _import.SaveAsync();
                var names = string.Join(", ", _import.Plans.Select(p => p.Season.Name));
                await DisplayAlert("Imported", $"Added {names}. They're in the season library, not in use; pick one there to look at it.", "OK");
                _onImported();
                await Navigation.PopModalAsync();
            }
            catch (Exception ex)
            {
                _status.Text = "";
                await DisplayAlert("Nothing was imported", ex.Message, "OK");
                Refresh();
            }
        }

        private static Label Line(string text) => new() { Text = text, FontSize = 13 };

        private static Label Muted(string text)
        {
            var label = new Label { Text = text, FontSize = 12 };
            label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#52665D"), Color.FromArgb("#ABC1B6"));
            return label;
        }
    }
}
