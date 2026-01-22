using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class GamesLibraryPage : ContentPage
{
    private readonly GamesLibraryService _gamesService;
    private string _currentCategory = "All";

    public GamesLibraryPage()
    {
        InitializeComponent();
        _gamesService = new GamesLibraryService();
        LoadCategoryFilters();
        LoadGames();
    }

    private void LoadCategoryFilters()
    {
        var categories = _gamesService.GetCategories();
        
        foreach (var category in categories)
        {
            var btn = new Button
            {
                Text = GetCategoryIcon(category) + " " + category,
                BackgroundColor = Color.FromArgb("#374151"),
                TextColor = Colors.White,
                CornerRadius = 20,
                Padding = new Thickness(15, 8),
                ClassId = category
            };
            btn.Clicked += OnCategoryFilterClicked;
            CategoryFilter.Children.Add(btn);
        }
    }

    private static string GetCategoryIcon(string category) => category switch
    {
        GameCategory.Sports => "?",
        GameCategory.Puzzle => "??",
        GameCategory.Arcade => "??",
        GameCategory.Card => "??",
        GameCategory.Strategy => "??",
        _ => "??"
    };

    private void OnCategoryFilterClicked(object? sender, EventArgs e)
    {
        if (sender is not Button btn) return;

        // Update selected state
        _currentCategory = btn.ClassId ?? "All";
        
        foreach (var child in CategoryFilter.Children)
        {
            if (child is Button filterBtn)
            {
                filterBtn.BackgroundColor = filterBtn == btn || (btn == AllCategoryBtn && filterBtn.ClassId == null)
                    ? Color.FromArgb("#3b82f6")
                    : Color.FromArgb("#374151");
            }
        }

        LoadGames();
    }

    private void LoadGames()
    {
        // Clear existing
        FeaturedGamesLayout.Children.Clear();
        AllGamesLayout.Children.Clear();

        var allGames = _currentCategory == "All"
            ? _gamesService.GetAllGames()
            : _gamesService.GetGamesByCategory(_currentCategory);

        // Featured games
        var featuredGames = allGames.Where(g => g.IsFeatured).ToList();
        FeaturedSection.IsVisible = featuredGames.Count > 0;
        
        foreach (var game in featuredGames)
        {
            FeaturedGamesLayout.Children.Add(CreateGameCard(game, true));
        }

        // All games
        foreach (var game in allGames)
        {
            AllGamesLayout.Children.Add(CreateGameCard(game, false));
        }
    }

    private View CreateGameCard(GameInfo game, bool isFeatured)
    {
        var cardWidth = isFeatured ? 280 : 200;
        var cardHeight = isFeatured ? 180 : 150;

        var card = new Frame
        {
            BackgroundColor = Color.FromArgb(game.ThumbnailColor),
            CornerRadius = 12,
            Padding = 15,
            HasShadow = true,
            WidthRequest = cardWidth,
            HeightRequest = cardHeight,
            Margin = new Thickness(5)
        };

        var content = new Grid
        {
            RowDefinitions =
            [
                new RowDefinition { Height = GridLength.Auto },
                new RowDefinition { Height = GridLength.Star },
                new RowDefinition { Height = GridLength.Auto }
            ]
        };

        // Top row: Icon + badges
        var topRow = new HorizontalStackLayout { Spacing = 8 };
        topRow.Children.Add(new Label 
        { 
            Text = game.Icon, 
            FontSize = isFeatured ? 40 : 32 
        });

        if (game.IsNew)
        {
            topRow.Children.Add(new Frame
            {
                BackgroundColor = Color.FromArgb("#22c55e"),
                CornerRadius = 8,
                Padding = new Thickness(6, 2),
                Content = new Label { Text = "NEW", FontSize = 10, TextColor = Colors.White, FontAttributes = FontAttributes.Bold }
            });
        }
        content.Children.Add(topRow);

        // Middle: Title + description
        var middle = new VerticalStackLayout
        {
            VerticalOptions = LayoutOptions.Center
        };
        middle.Children.Add(new Label
        {
            Text = game.Name,
            FontSize = isFeatured ? 18 : 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White
        });

        if (isFeatured)
        {
            middle.Children.Add(new Label
            {
                Text = game.Description,
                FontSize = 12,
                TextColor = Color.FromArgb("#e2e8f0"),
                MaxLines = 2,
                LineBreakMode = LineBreakMode.TailTruncation
            });
        }

        Grid.SetRow(middle, 1);
        content.Children.Add(middle);

        // Bottom: Player count + difficulty
        var bottom = new HorizontalStackLayout { Spacing = 10 };
        bottom.Children.Add(new Label
        {
            Text = $"?? {game.PlayerCount}P",
            FontSize = 11,
            TextColor = Color.FromArgb("#cbd5e1")
        });
        bottom.Children.Add(new Label
        {
            Text = $"?? {game.Difficulty}",
            FontSize = 11,
            TextColor = Color.FromArgb("#cbd5e1")
        });
        Grid.SetRow(bottom, 2);
        content.Children.Add(bottom);

        card.Content = content;

        // Add tap gesture
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += async (s, e) => await OnGameTapped(game);
        card.GestureRecognizers.Add(tapGesture);

        return card;
    }

    private async Task OnGameTapped(GameInfo game)
    {
        try
        {
            // Create instance of the game page
            var page = (Page?)Activator.CreateInstance(game.PageType);
            if (page != null)
            {
                await Navigation.PushAsync(page);
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Could not load {game.Name}: {ex.Message}", "OK");
        }
    }
}
