using Wdpl2.Services.Web;
using static Wdpl2.Services.Web.ScorecardService;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// The notes captains write on their scorecards, read as messages.
/// </summary>
/// <remarks>
/// The notes box is on every card, but nothing read it - a captain could tell
/// the league something and it would sit on the website unseen. Read and
/// unread live on the website, so marking one here clears it on the admin
/// page too.
/// </remarks>
public partial class MessagesPage : ContentPage
{
    private List<CardMessage> _messages = new();

    public MessagesPage()
    {
        InitializeComponent();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadAsync();
    }

    private async void OnRefreshClicked(object? sender, EventArgs e) => await LoadAsync();

    private async void OnReadAllClicked(object? sender, EventArgs e) =>
        await RunAsync(client => MarkReadAsync(client, null));

    private Task LoadAsync() => RunAsync(GetMessagesAsync);

    private async Task RunAsync(Func<WebApiClient, Task<List<CardMessage>>> fetch)
    {
        RefreshButton.IsEnabled = false;
        ReadAllButton.IsEnabled = false;

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            _messages = await fetch(client);
            Render();
            Report("", error: false);
        }
        catch (WebApiException ex)
        {
            CountLabel.Text = "Could not read the website.";
            Report(ex.Code is "unknown_action" or "unknown_module"
                ? "The website does not have messages yet. Deploy the backend, then install tables."
                : ex.Message, error: true);
        }
        catch (InvalidOperationException ex)
        {
            CountLabel.Text = "Could not read the website.";
            Report(ex.Message, error: true);
        }
        finally
        {
            RefreshButton.IsEnabled = true;
            ReadAllButton.IsEnabled = true;
        }
    }

    private void Render()
    {
        MessageList.Children.Clear();

        var unread = _messages.Count(m => m.Unread);
        EmptyLabel.IsVisible = _messages.Count == 0;
        ReadAllButton.IsVisible = unread > 0;

        CountLabel.Text = (_messages.Count, unread) switch
        {
            (0, _) => "No messages",
            (_, 0) => _messages.Count == 1 ? "1 message, read" : $"{_messages.Count} messages, all read",
            (_, 1) => "1 new message",
            _ => $"{unread} new messages",
        };

        foreach (var message in _messages)
            MessageList.Children.Add(Card(message));
    }

    private View Card(CardMessage message)
    {
        var title = new Label
        {
            Text = $"{message.HomeTeam} v {message.AwayTeam}" + (message.Unread ? "  •  NEW" : ""),
            FontAttributes = FontAttributes.Bold,
            FontSize = 13,
            TextColor = Color.FromArgb(message.Unread ? "#1D4ED8" : "#1E293B"),
        };

        var when = new List<string>();
        if (message.IsCup) when.Add("Cup tie");
        if (message.MatchDate is { } date) when.Add(date.ToString("ddd d MMM"));
        if (message.WrittenAt is { } at) when.Add("written " + at.ToString("d MMM, HH:mm"));

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

        if (message.Unread)
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
            BorderColor = Color.FromArgb(message.Unread ? "#93C5FD" : "#E2E8F0"),
            BackgroundColor = Color.FromArgb(message.Unread ? "#EFF6FF" : "#F8FAFC"),
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
