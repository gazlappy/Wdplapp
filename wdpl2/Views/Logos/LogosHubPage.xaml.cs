using System.Collections.ObjectModel;
using Wdpl2.Features.WebsiteBuilder.Logo;
using Wdpl2.Features.WebsiteBuilder.Views;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.Logos;

public partial class LogosHubPage : ContentPage
{
    private readonly IDataStore _dataStore;
    private LeagueData League => _dataStore.GetData();

    private readonly ObservableCollection<LogoCatalogDisplayItem> _items = new();

    public LogosHubPage(IDataStore dataStore)
    {
        _dataStore = dataStore;
        InitializeComponent();
        LogosCollection.ItemsSource = _items;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        Reload();
    }

    private void Reload()
    {
        _items.Clear();
        foreach (var l in League.WebsiteSettings.LogoCatalog)
            _items.Add(LogoCatalogDisplayItem.FromModel(l));

        EmptyState.IsVisible = _items.Count == 0;
        SubtitleLabel.Text = _items.Count == 0
            ? "Design and manage logos for the league website and individual teams."
            : $"{_items.Count} logo(s) in your catalog. Use them on the website or assign to teams.";
    }

    private async void OnDesignNewClicked(object sender, EventArgs e)
    {
        await Navigation.PushAsync(new LogoDesignerPage(null, null));
    }

    private async void OnUploadClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await FilePicker.Default.PickAsync(new PickOptions
            {
                PickerTitle = "Select Logo Image",
                FileTypes = FilePickerFileType.Images
            });
            if (result == null) return;

            using var stream = await result.OpenReadAsync();
            using var ms = new MemoryStream();
            await stream.CopyToAsync(ms);
            var bytes = ms.ToArray();

            var name = await DisplayPromptAsync("Save Logo", "Enter a name:",
                placeholder: Path.GetFileNameWithoutExtension(result.FileName));
            if (string.IsNullOrWhiteSpace(name)) return;

            var category = await DisplayPromptAsync("Save Logo", "Category (optional):",
                placeholder: "Team");
            if (string.IsNullOrWhiteSpace(category)) category = "General";

            League.WebsiteSettings.AddLogoCatalogItem(name, bytes, "", category);
            await _dataStore.SaveAsync();
            Reload();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Upload Failed", ex.Message, "OK");
        }
    }

    private async void OnEditClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not LogoCatalogDisplayItem item) return;
        if (string.IsNullOrEmpty(item.DesignJson))
        {
            await DisplayAlert("Not Editable", "Only logos created with the designer can be edited.", "OK");
            return;
        }

        var recipe = LogoDesignRecipe.FromJson(item.DesignJson);
        await Navigation.PushAsync(new LogoDesignerPage(item.Id, recipe));
    }

    private async void OnDeleteClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not LogoCatalogDisplayItem item) return;

        var confirm = await DisplayAlert("Delete Logo", $"Remove '{item.Name}' from the catalog?", "Delete", "Cancel");
        if (!confirm) return;

        // Clear from any teams using it
        foreach (var t in League.Teams.Where(t => t.LogoCatalogId == item.Id))
            t.LogoCatalogId = null;

        League.WebsiteSettings.RemoveLogoCatalogItem(item.Id);
        await _dataStore.SaveAsync();
        Reload();
    }

    private async void OnAssignToTeamClicked(object sender, EventArgs e)
    {
        if (sender is not Button b || b.CommandParameter is not LogoCatalogDisplayItem item) return;

        var teams = League.Teams
            .Where(t => !string.IsNullOrWhiteSpace(t.Name))
            .OrderBy(t => t.Name)
            .ToList();

        if (teams.Count == 0)
        {
            await DisplayAlert("No Teams", "There are no teams to assign this logo to.", "OK");
            return;
        }

        var labels = teams.Select(t =>
        {
            var marker = t.LogoCatalogId == item.Id ? " ✓" : "";
            return $"{t.Name}{marker}";
        }).ToArray();

        var picked = await DisplayActionSheet($"Assign '{item.Name}' to team", "Cancel", "Clear from all teams", labels);
        if (string.IsNullOrEmpty(picked) || picked == "Cancel") return;

        if (picked == "Clear from all teams")
        {
            foreach (var t in League.Teams.Where(t => t.LogoCatalogId == item.Id))
                t.LogoCatalogId = null;
            await _dataStore.SaveAsync();
            await DisplayAlert("Cleared", $"'{item.Name}' is no longer assigned to any team.", "OK");
            return;
        }

        var idx = Array.IndexOf(labels, picked);
        if (idx < 0 || idx >= teams.Count) return;

        var team = teams[idx];
        // Toggle: if already assigned, clear; otherwise assign
        if (team.LogoCatalogId == item.Id)
        {
            team.LogoCatalogId = null;
            await DisplayAlert("Removed", $"'{item.Name}' removed from {team.Name}.", "OK");
        }
        else
        {
            team.LogoCatalogId = item.Id;
            await DisplayAlert("Assigned", $"'{item.Name}' assigned to {team.Name}.", "OK");
        }
        await _dataStore.SaveAsync();
    }
}
