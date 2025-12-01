using Microsoft.EntityFrameworkCore;
using Wdpl2.Data;

namespace Wdpl2.Views;

public partial class TestPage : ContentPage
{
    private readonly LeagueContext _context;

    public TestPage(LeagueContext context)
    {
        InitializeComponent();
        _context = context;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadDataAsync();
    }

    private async Task LoadDataAsync()
    {
        try
        {
            // Device Information
            DeviceNameLabel.Text = DeviceInfo.Current.Name;
            PlatformLabel.Text = $"{DeviceInfo.Current.Platform} - {DeviceInfo.Current.Idiom}";
            VersionLabel.Text = $"{DeviceInfo.Current.VersionString}";
            AppVersionLabel.Text = AppInfo.Current.VersionString;

            // Database Information
            var dbPath = LeagueContext.GetDatabasePath();
            DbPathLabel.Text = dbPath;

            if (File.Exists(dbPath))
            {
                var fileInfo = new FileInfo(dbPath);
                DbSizeLabel.Text = FormatFileSize(fileInfo.Length);
                DbModifiedLabel.Text = fileInfo.LastWriteTime.ToString("yyyy-MM-dd HH:mm:ss");
            }
            else
            {
                DbSizeLabel.Text = "Database not found";
                DbModifiedLabel.Text = "N/A";
            }

            // Data Summary - Count records
            SeasonsCountLabel.Text = (await _context.Seasons.CountAsync()).ToString();
            DivisionsCountLabel.Text = (await _context.Divisions.CountAsync()).ToString();
            TeamsCountLabel.Text = (await _context.Teams.CountAsync()).ToString();
            PlayersCountLabel.Text = (await _context.Players.CountAsync()).ToString();
            FixturesCountLabel.Text = (await _context.Fixtures.CountAsync()).ToString();

            // Update timestamp
            TimestampLabel.Text = $"Last updated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}";
            StatusLabel.Text = "";
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
        }
    }

    private async void OnRefreshClicked(object sender, EventArgs e)
    {
        StatusLabel.Text = "Refreshing...";
        StatusLabel.TextColor = Color.FromArgb("#512BD4"); // Primary color
        await LoadDataAsync();
        StatusLabel.Text = "? Refreshed successfully!";
    }

    private async void OnCopyDbPathClicked(object sender, EventArgs e)
    {
        try
        {
            var dbPath = LeagueContext.GetDatabasePath();
            await Clipboard.SetTextAsync(dbPath);
            StatusLabel.Text = "? Database path copied to clipboard!";
            StatusLabel.TextColor = Color.FromArgb("#512BD4"); // Primary color
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error copying: {ex.Message}";
            StatusLabel.TextColor = Colors.Red;
        }
    }

    private static string FormatFileSize(long bytes)
    {
        string[] sizes = { "B", "KB", "MB", "GB" };
        double len = bytes;
        int order = 0;
        while (len >= 1024 && order < sizes.Length - 1)
        {
            order++;
            len = len / 1024;
        }
        return $"{len:0.##} {sizes[order]}";
    }
}
