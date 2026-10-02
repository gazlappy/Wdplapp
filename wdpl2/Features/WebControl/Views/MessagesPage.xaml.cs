using Wdpl2.Models;
using Wdpl2.Services.Web;
using static Wdpl2.Services.Web.ScorecardService;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// The message centre: every note captains have written on their scorecards.
/// </summary>
/// <remarks>
/// The list is the app's own archive (<see cref="MessageArchive"/>), topped up
/// from the website each time the page opens, so it reaches back past anything
/// the website still holds and still opens with no connection. Read and unread
/// live on the website, which is why only a message still there can be new or
/// be marked read; marking one here clears it on the admin page too.
/// </remarks>
public partial class MessagesPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    // The website's current notes, for which kept messages are still unread.
    private List<CardMessage> _live = new();
    private List<Season> _seasons = new();
    private bool _filling;

    public MessagesPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        FillSeasons();
        Render();
        await LoadAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadAsync();

    private async void OnReadAllClicked(object? sender, EventArgs e) =>
        await RunAsync(client => MarkReadAsync(client, null));

    private void OnFilterChanged(object? sender, EventArgs e)
    {
        if (!_filling) Render();
    }

    private Task LoadAsync() => RunAsync(GetMessagesAsync);

    private async Task RunAsync(Func<WebApiClient, Task<List<CardMessage>>> fetch)
    {
        RefreshButton.IsEnabled = false;
        ReadAllButton.IsEnabled = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            _live = await fetch(client);

            var added = MessageArchive.Keep(League, _live);
            if (added > 0) DataStore.SaveJsonOnly();

            Render();
            Report(added > 0 ? $"{added} new message(s) kept from the website." : "", error: false);
        }
        catch (WebApiException ex)
        {
            Report(ex.Code is "unknown_action" or "unknown_module"
                ? "The website does not have messages yet. Deploy the backend, then install tables. Showing the messages already kept."
                : "Could not read the website, so this is what was kept before. " + ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            Report("Could not read the website, so this is what was kept before. " + ex.Message, error: true);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            ReadAllButton.IsEnabled = true;
        }
    }

    private void FillSeasons()
    {
        _filling = true;
        try
        {
            _seasons = League.Seasons.OrderByDescending(s => s.StartDate).ToList();

            SeasonPicker.Items.Clear();
            SeasonPicker.Items.Add("All seasons");
            foreach (var season in _seasons) SeasonPicker.Items.Add(season.Name);
            SeasonPicker.SelectedIndex = 0;
        }
        finally
        {
            _filling = false;
        }
    }

    private bool IsNew(CardMessageRecord kept) =>
        _live.Any(m => m.Unread && m.FixtureId == kept.FixtureId && m.Text.Trim() == kept.Text);

    private void Render()
    {
        MessageList.Children.Clear();

        var all = MessageArchive.Newest(League).ToList();
        var unread = all.Count(IsNew);

        ReadAllButton.IsVisible = unread > 0;
        CountLabel.Text = (all.Count, unread) switch
        {
            (0, _) => "No messages yet",
            (_, 0) => all.Count == 1 ? "1 message" : $"{all.Count} messages",
            _ => $"{all.Count} messages, {unread} new",
        };

        var shown = all.Where(Matches).ToList();
        foreach (var message in shown)
            MessageList.Children.Add(Card(message));

        EmptyLabel.IsVisible = shown.Count == 0;
        EmptyLabel.Text = all.Count == 0
            ? "No captain has written a note on a card yet. Anything they write will be kept here."
            : "No message matches.";
    }

    private bool Matches(CardMessageRecord message)
    {
        if (NewOnlyBox.IsChecked && !IsNew(message)) return false;

        var index = SeasonPicker.SelectedIndex;
        if (index > 0 && index <= _seasons.Count && message.SeasonId != _seasons[index - 1].Id) return false;

        var search = SearchEntry.Text?.Trim();
        if (string.IsNullOrEmpty(search)) return true;

        return message.HomeTeam.Contains(search, StringComparison.OrdinalIgnoreCase)
            || message.AwayTeam.Contains(search, StringComparison.OrdinalIgnoreCase)
            || message.Text.Contains(search, StringComparison.OrdinalIgnoreCase);
    }

    private View Card(CardMessageRecord message)
    {
        var fresh = IsNew(message);

        var title = new Label
        {
            Text = $"{message.HomeTeam} v {message.AwayTeam}" + (fresh ? "  •  NEW" : ""),
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            TextColor = Color.FromArgb(fresh ? "#1D4ED8" : "#1E293B"),
        };

        var when = new List<string>();
        if (message.IsCup) when.Add("Cup tie");
        if (message.MatchDate is { } date) when.Add(date.ToString("ddd d MMM yyyy"));
        if (message.SeasonId is { } seasonId && _seasons.FirstOrDefault(s => s.Id == seasonId) is { } season)
            when.Add(season.Name);
        when.Add("written " + (message.WrittenAt ?? message.KeptAt).ToString("d MMM, HH:mm"));

        var detail = new Label
        {
            Text = string.Join(" · ", when),
            FontSize = 11,
            TextColor = Color.FromArgb("#64748B"),
        };

        var text = new Label
        {
            Text = message.Text,
            FontSize = 13,
            TextColor = Color.FromArgb("#334155"),
            LineBreakMode = LineBreakMode.WordWrap,
            Margin = new Thickness(0, 6, 0, 0),
        };

        var heading = new Grid
        {
            ColumnDefinitions = { new ColumnDefinition(GridLength.Star), new ColumnDefinition(GridLength.Auto) },
            ColumnSpacing = 8,
        };
        heading.Add(new VerticalStackLayout { Spacing = 2, Children = { title, detail } });

        if (fresh)
        {
            var read = new Button
            {
                Text = "Mark read",
                BackgroundColor = Color.FromArgb("#2563EB"),
                TextColor = Colors.White,
                CornerRadius = 8,
                FontSize = 12,
                Padding = new Thickness(12, 4),
                VerticalOptions = LayoutOptions.Start,
            };
            read.Clicked += async (_, _) =>
            {
                read.IsEnabled = false;
                await RunAsync(client => MarkReadAsync(client, message.FixtureId));
            };
            heading.Add(read, 1);
        }

        return new Frame
        {
            BorderColor = Color.FromArgb(fresh ? "#93C5FD" : "#E2E8F0"),
            BackgroundColor = Color.FromArgb(fresh ? "#EFF6FF" : "#F8FAFC"),
            CornerRadius = 8,
            Padding = new Thickness(12, 10),
            HasShadow = false,
            Content = new VerticalStackLayout { Children = { heading, text } },
        };
    }

    private void Report(string message, bool error)
    {
        StatusLabel.Text = message;
        StatusLabel.TextColor = Color.FromArgb(error ? "#EF4444" : "#10B981");
    }
}
