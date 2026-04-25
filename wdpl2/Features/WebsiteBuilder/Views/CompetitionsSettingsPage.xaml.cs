using Microsoft.Maui.Controls;
using Microsoft.Maui.Graphics;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class CompetitionsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    private readonly List<Competition> _ordered = new();

    public CompetitionsSettingsPage()
    {
        InitializeComponent();
        LoadOrdering();
        Render();
    }

    private void LoadOrdering()
    {
        var settings = League.WebsiteSettings;
        var available = League.Competitions
            .Where(c => c.Status != CompetitionStatus.Draft)
            .ToList();

        // Honour any saved order first
        var indexMap = new Dictionary<Guid, int>();
        for (int i = 0; i < settings.CompetitionDisplayOrder.Count; i++)
            indexMap[settings.CompetitionDisplayOrder[i]] = i;

        _ordered.Clear();
        _ordered.AddRange(available
            .OrderBy(c => indexMap.TryGetValue(c.Id, out var idx) ? idx : int.MaxValue)
            .ThenByDescending(c => c.Status == CompetitionStatus.InProgress)
            .ThenByDescending(c => c.CreatedDate));
    }

    private void Render()
    {
        OrderList.Children.Clear();

        if (_ordered.Count == 0)
        {
            EmptyMessage.IsVisible = true;
            return;
        }
        EmptyMessage.IsVisible = false;

        for (int i = 0; i < _ordered.Count; i++)
        {
            var comp = _ordered[i];
            var index = i;

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition { Width = new GridLength(28) },
                    new ColumnDefinition { Width = GridLength.Star },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto },
                    new ColumnDefinition { Width = GridLength.Auto }
                },
                ColumnSpacing = 8,
                Padding = new Thickness(8, 6),
                BackgroundColor = Color.FromArgb("#F9FAFB")
            };

            grid.Add(new Label
            {
                Text = $"{i + 1}.",
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#6B7280"),
                VerticalOptions = LayoutOptions.Center
            }, 0);

            grid.Add(new Label
            {
                Text = comp.Name,
                VerticalOptions = LayoutOptions.Center,
                LineBreakMode = LineBreakMode.TailTruncation
            }, 1);

            var statusLabel = new Label
            {
                Text = comp.Status.ToString(),
                FontSize = 11,
                Padding = new Thickness(8, 2),
                TextColor = Colors.White,
                BackgroundColor = comp.Status == CompetitionStatus.InProgress
                    ? Color.FromArgb("#10B981")
                    : Color.FromArgb("#6B7280"),
                VerticalOptions = LayoutOptions.Center
            };
            grid.Add(statusLabel, 2);

            var upButton = new Button
            {
                Text = "↑",
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#EFF6FF"),
                TextColor = Color.FromArgb("#3B82F6"),
                IsEnabled = i > 0
            };
            upButton.Clicked += (_, _) => Move(index, -1);
            grid.Add(upButton, 3);

            var downButton = new Button
            {
                Text = "↓",
                WidthRequest = 36,
                HeightRequest = 36,
                Padding = 0,
                CornerRadius = 6,
                BackgroundColor = Color.FromArgb("#EFF6FF"),
                TextColor = Color.FromArgb("#3B82F6"),
                IsEnabled = i < _ordered.Count - 1
            };
            downButton.Clicked += (_, _) => Move(index, +1);
            grid.Add(downButton, 4);

            OrderList.Children.Add(grid);
        }
    }

    private void Move(int index, int delta)
    {
        var newIndex = index + delta;
        if (newIndex < 0 || newIndex >= _ordered.Count) return;
        (_ordered[index], _ordered[newIndex]) = (_ordered[newIndex], _ordered[index]);
        Render();
    }

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            League.WebsiteSettings.CompetitionDisplayOrder = _ordered.Select(c => c.Id).ToList();
            DataStore.Save();
            await DisplayAlert("Saved", "Competition display order saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private async void OnCancelClicked(object sender, EventArgs e)
        => await Navigation.PopAsync();
}
