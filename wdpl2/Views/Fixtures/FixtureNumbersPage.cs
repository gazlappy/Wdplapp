using Wdpl2.Models;
using Wdpl2.Services;
using Microsoft.Maui.Controls.Shapes;

namespace Wdpl2.Views;

public sealed class FixtureNumbersPage : ContentPage
{
    private readonly FixtureNumberEditor _editor;
    private readonly IDataStore _store;
    private readonly TaskCompletionSource<bool> _completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private readonly Picker _division = new() { Title = "Division", ItemDisplayBinding = new Binding(nameof(Division.Name)) };
    private readonly Picker _from = new() { Title = "From", WidthRequest = 90 };
    private readonly Picker _to = new() { Title = "To", WidthRequest = 90 };
    private readonly Picker _firstBye = new() { Title = "Team for first bye", ItemDisplayBinding = new Binding(nameof(Team.Name)) };
    private readonly Label _byeStatus = new();
    private readonly Button _previewBye = new() { Text = "Preview first bye", HorizontalOptions = LayoutOptions.Start };
    private readonly Label _key = new();
    private readonly Label _review = new();
    private readonly Label _previewSummary = new();
    private readonly Label _fixtureChanges = new();
    private readonly VerticalStackLayout _previewKeys = new() { Spacing = 12 };
    private readonly Button _apply = new() { Text = "Apply to draft", IsEnabled = false, HorizontalOptions = LayoutOptions.Start };
    private readonly Button _save = new() { Text = "Confirm and save fixtures" };
    private readonly Grid _layout;
    private FixtureNumberEditor.SwapProposal? _proposal;
    private bool _saving;
    private bool _previewing;
    private bool _saved;

    public FixtureNumbersPage(FixtureNumberEditor editor, IDataStore store)
    {
        _editor = editor;
        _store = store;
        Title = "Review fixture numbers";
        var swap = new Button { Text = "Preview change", HorizontalOptions = LayoutOptions.Start };
        var reset = new Button { Text = "Reset draft" };
        var cancel = new Button { Text = "Cancel without saving" };
        var discard = new Button { Text = "Discard preview", HorizontalOptions = LayoutOptions.Start };
        var controls = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Heading("1. Choose a change"),
                new Label { Text = "Swap 1 ↔ 2 to reverse a pair, or move between paired blocks (1 → 3 or 1 → 4). Table partners follow. Any required changes in other divisions need your confirmation." },
                _division,
                new HorizontalStackLayout
                {
                    Spacing = 12,
                    Children = { _from, new Label { Text = "→", VerticalOptions = LayoutOptions.Center, FontSize = 22 }, _to }
                },
                swap,
                Heading("Choose the first bye"),
                _byeStatus, _firstBye, _previewBye,
                Heading("Current draft numbers"),
                _key
            }
        };
        var preview = new VerticalStackLayout
        {
            Spacing = 12,
            Children =
            {
                Heading("2. Preview before applying"), _previewSummary, _previewKeys,
                Heading("Fixture changes for this proposal"), _fixtureChanges,
                new FlexLayout { Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap, Children = { _apply, discard } },
                Heading("Applied draft — review before saving"), _review
            }
        };
        var left = Card(new ScrollView { Content = controls });
        var right = Card(new ScrollView { Content = preview });
        var split = new Grid
        {
            ColumnSpacing = 16, RowSpacing = 16,
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(new GridLength(0)) },
            RowDefinitions = { new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Star) }
        };
        split.Add(left);
        split.Add(right, 0, 1);
        _layout = new Grid
        {
            Padding = 16, RowSpacing = 12,
            RowDefinitions = { new RowDefinition(GridLength.Auto), new RowDefinition(GridLength.Star), new RowDefinition(GridLength.Auto) }
        };
        _layout.Add(new VerticalStackLayout
        {
            Spacing = 4,
            Children =
            {
                new Label { Text = editor.SeasonName, FontSize = 24, FontAttributes = FontAttributes.Bold },
                new Label { Text = "Preview → Apply to draft → Save. Dates and opponents can change. Nothing is saved until confirmed." }
            }
        });
        _layout.Add(split, 0, 1);
        _layout.Add(new FlexLayout
        {
            Wrap = Microsoft.Maui.Layouts.FlexWrap.Wrap,
            Children = { _save, reset, cancel }
        }, 0, 2);
        Content = _layout;
        SizeChanged += (_, _) =>
        {
            bool wide = Width >= 900;
            Grid.SetColumn(right, wide ? 1 : 0);
            Grid.SetRow(right, wide ? 0 : 1);
            split.ColumnDefinitions[0].Width = wide ? new GridLength(340) : GridLength.Star;
            split.ColumnDefinitions[1].Width = wide ? GridLength.Star : new GridLength(0);
            split.RowDefinitions[1].Height = wide ? new GridLength(0) : GridLength.Star;
        };
        _division.ItemsSource = editor.Divisions;
        _division.SelectedIndexChanged += (_, _) => { ClearPreview(); RefreshKey(); };
        _from.ItemsSource = Enumerable.Range(1, editor.SlotCount).ToList();
        _from.SelectedIndexChanged += (_, _) =>
        {
            ClearPreview();
            if (_from.SelectedItem is int number)
            {
                _to.ItemsSource = Enumerable.Range(1, editor.SlotCount).Where(n => n != number).ToList();
                _to.SelectedIndex = -1;
            }
        };
        _to.SelectedIndexChanged += (_, _) => ClearPreview();
        _firstBye.SelectedIndexChanged += (_, _) => ClearPreview();
        _previewBye.Clicked += async (_, _) =>
        {
            if (_saving || _previewing) return;
            if (_division.SelectedItem is not Division division || _firstBye.SelectedItem is not Team team)
            {
                await DisplayAlert("Choose a team", "Select a team to receive a bye on the opening fixture night.", "OK");
                return;
            }
            try
            {
                _previewing = true;
                _layout.IsEnabled = false;
                ClearPreview();
                _proposal = editor.PrepareFirstBye(division.Id, team.Id);
                ShowPreview(_proposal);
            }
            catch (Exception ex) { await DisplayAlert("First bye not available", ex.Message, "OK"); }
            finally { _previewing = false; _layout.IsEnabled = true; }
        };
        swap.Clicked += async (_, _) =>
        {
            if (_saving || _previewing) return;
            if (_division.SelectedItem is not Division division || _from.SelectedItem is not int from || _to.SelectedItem is not int to)
            {
                await DisplayAlert("Choose numbers", "Select a division, source number and destination number.", "OK");
                return;
            }
            try
            {
                _previewing = true;
                _layout.IsEnabled = false;
                ClearPreview();
                _proposal = editor.PrepareSwap(division.Id, from, to);
                ShowPreview(_proposal);
            }
            catch (Exception ex) { await DisplayAlert("Move not allowed", ex.Message, "OK"); }
            finally { _previewing = false; _layout.IsEnabled = true; }
        };
        _apply.Clicked += async (_, _) => await ApplyPreviewAsync();
        discard.Clicked += (_, _) => ClearPreview();
        reset.Clicked += (_, _) => { editor.Reset(); ClearPreview(); RefreshKey(); };
        cancel.Clicked += async (_, _) => await CancelAsync();
        _save.Clicked += async (_, _) => await SaveAsync();
        if (_division.ItemsSource.Count > 0) _division.SelectedIndex = 0;
        _from.SelectedIndex = 0;
        RefreshKey();
    }

    private static Label Heading(string text) => new() { Text = text, FontSize = 18, FontAttributes = FontAttributes.Bold };

    private static Border Card(View content)
    {
        var card = new Border
        {
            Padding = 16, StrokeThickness = 1, Stroke = new SolidColorBrush(Colors.Gray),
            StrokeShape = new RoundRectangle { CornerRadius = 12 }, Content = content
        };
        card.SetAppThemeColor(BackgroundColorProperty, Colors.White, Color.FromArgb("#202020"));
        return card;
    }

    private void ClearPreview()
    {
        _proposal = null;
        _apply.IsEnabled = false;
        _save.IsEnabled = true;
        _previewKeys.Children.Clear();
        _previewSummary.Text = "Preview a number swap or choose a team for the first bye. Your draft stays unchanged until you apply the proposal.";
        _fixtureChanges.Text = "No pending proposal.";
    }

    private void ShowPreview(FixtureNumberEditor.SwapProposal proposal)
    {
        _previewSummary.Text = proposal.Summary;
        _fixtureChanges.Text = string.IsNullOrEmpty(proposal.FixtureChanges) ? "No fixture changes." : proposal.FixtureChanges;
        var current = _editor.Numbers;
        var proposed = proposal.PreviewNumbers;
        var teams = _editor.Teams;
        foreach (var division in _editor.Divisions.Where(d => teams.Any(t => t.DivisionId == d.Id && current[t.Id] != proposed[t.Id])))
        {
            var rows = new Grid
            {
                ColumnSpacing = 8, RowSpacing = 8,
                ColumnDefinitions = { new ColumnDefinition(new GridLength(36)), new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Star) }
            };
            rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
            rows.Add(new Label { Text = "No.", FontAttributes = FontAttributes.Bold });
            rows.Add(new Label { Text = "Current", FontAttributes = FontAttributes.Bold }, 1);
            rows.Add(new Label { Text = "Proposed", FontAttributes = FontAttributes.Bold }, 2);
            for (int number = 1; number <= _editor.SlotCount; number++)
            {
                rows.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                var before = teams.FirstOrDefault(t => t.DivisionId == division.Id && current[t.Id] == number);
                var after = teams.FirstOrDefault(t => t.DivisionId == division.Id && proposed[t.Id] == number);
                bool changed = before?.Id != after?.Id;
                rows.Add(new Label { Text = number.ToString(), FontAttributes = FontAttributes.Bold }, 0, number);
                rows.Add(new Label { Text = before?.Name ?? "BYE" }, 1, number);
                var label = new Label { Text = after?.Name ?? "BYE", FontAttributes = changed ? FontAttributes.Bold : FontAttributes.None };
                if (changed) label.SetAppThemeColor(Label.TextColorProperty, Color.FromArgb("#175CD3"), Color.FromArgb("#93C5FD"));
                rows.Add(label, 2, number);
            }
            _previewKeys.Children.Add(Heading(division.Name));
            _previewKeys.Children.Add(rows);
        }
        _apply.Text = proposal.IncludesOtherDivisions ? "Apply linked division changes…" : "Apply to draft";
        _apply.IsEnabled = true;
        _save.IsEnabled = false;
    }

    private async Task ApplyPreviewAsync()
    {
        if (_saving || _previewing || _proposal is not { } proposal) return;
        _previewing = true;
        _layout.IsEnabled = false;
        try
        {
            if (proposal.IncludesOtherDivisions && !await DisplayAlert("Include linked division swaps?",
                "Apply all these changes together to keep table partners compatible?\n\n" + proposal.Summary,
                "Include changes", "Cancel")) return;
            _editor.ApplySwap(proposal);
            ClearPreview();
            RefreshKey();
            _previewSummary.Text = "Change applied to the draft only. Review the applied changes below, then save when ready.";
        }
        catch (Exception ex) { ClearPreview(); await DisplayAlert("Move not allowed", ex.Message, "OK"); }
        finally { _previewing = false; _layout.IsEnabled = true; }
    }

    public static async Task<bool> ShowAsync(Page owner, FixtureNumberEditor editor, IDataStore store)
    {
        var page = new FixtureNumbersPage(editor, store);
        await owner.Navigation.PushModalAsync(new NavigationPage(page));
        return await page._completion.Task;
    }

    private void RefreshKey()
    {
        var numbers = _editor.Numbers;
        var teams = _editor.Teams;
        var venues = _editor.Venues;
        if (_division.SelectedItem is Division division)
        {
            var lines = new List<string>();
            for (int number = 1; number <= _editor.SlotCount; number++)
            {
                var team = teams.FirstOrDefault(t => t.DivisionId == division.Id && numbers[t.Id] == number);
                if (team == null) { lines.Add($"{number}. BYE"); continue; }
                var venue = venues.FirstOrDefault(v => v.Id == team.VenueId);
                var table = venue?.Tables.FirstOrDefault(t => t.Id == team.TableId);
                lines.Add($"{number}. {team.Name} — {venue?.Name}, {table?.Label}");
            }
            _key.Text = string.Join("\n", lines);
            var members = teams.Where(t => t.DivisionId == division.Id).OrderBy(t => t.Name).ToList();
            var selected = (_firstBye.SelectedItem as Team)?.Id;
            _firstBye.ItemsSource = members;
            _firstBye.SelectedItem = members.FirstOrDefault(t => t.Id == selected);
            bool hasByeSlots = members.Count < _editor.SlotCount;
            _firstBye.IsEnabled = hasByeSlots;
            _previewBye.IsEnabled = hasByeSlots;
            var byes = _editor.OpeningByeTeams(division.Id);
            _byeStatus.Text = hasByeSlots
                ? $"Opening night: {_editor.OpeningDate:dd MMM yyyy}. Current bye: {(byes.Count == 0 ? "None" : string.Join(", ", byes.Select(t => t.Name)))}. Preview checks all required team and table-partner changes."
                : "This division has no BYE slots, so a first bye cannot be assigned.";
        }
        _review.Text = _editor.Review();
    }

    private async Task SaveAsync()
    {
        if (_saving || _previewing || _proposal != null) return;
        _saving = true;
        _layout.IsEnabled = false;
        try
        {
            if (!await DisplayAlert("Save reviewed fixtures?", $"Save the reviewed draw for {_editor.SeasonName}? This replaces the season's current unplayed fixture schedule. Review the changes above before confirming.", "Save", "Cancel")) return;
            await _store.SaveFixtureNumbersAsync(_editor);
            _saved = true;
            // Number changes can move dates. Remove reminders tied to the previous dates.
            try
            {
                var reminders = Handler?.MauiContext?.Services.GetService<MatchReminderService>();
                if (reminders != null)
                    foreach (var id in _editor.PreviousFixtureIds) await reminders.CancelMatchReminderAsync(id);
            }
            catch (Exception ex)
            {
                await DisplayAlert("Fixtures saved", $"Reminder cleanup failed: {ex.Message}. Review match reminders before relying on them.", "OK");
            }
            await Navigation.PopModalAsync();
            _completion.TrySetResult(true);
        }
        catch (Exception ex) { await DisplayAlert("Cannot save fixtures", ex.Message, "OK"); }
        finally { _saving = false; _layout.IsEnabled = true; }
    }

    private async Task CancelAsync()
    {
        if (_saving || _previewing) return;
        if (_editor.HasChanges && !await DisplayAlert("Discard draft?", "Discard the number changes without saving?", "Discard", "Keep editing")) return;
        await Navigation.PopModalAsync();
        _completion.TrySetResult(false);
    }

    protected override bool OnBackButtonPressed()
    {
        if (!_saving && !_previewing) _ = CancelAsync();
        return true;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        if (!Navigation.ModalStack.Contains(Parent as Page))
            _completion.TrySetResult(_saved);
    }
}
