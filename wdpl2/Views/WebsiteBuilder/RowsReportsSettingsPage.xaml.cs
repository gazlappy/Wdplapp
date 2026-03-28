using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class RowsReportsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<ReportDisplayItem> _reports = new();
    private Guid? _selectedReportId;

    public RowsReportsSettingsPage()
    {
        InitializeComponent();
        ReportsCollection.ItemsSource = _reports;
        LoadReports();
    }

    private void LoadReports()
    {
        _reports.Clear();
        foreach (var report in League.WebsiteSettings.RowsReports
            .OrderByDescending(r => r.WeekNumber)
            .ThenByDescending(r => r.MatchDate))
        {
            _reports.Add(ReportDisplayItem.FromModel(report));
        }
    }

    private void OnAddReportClicked(object sender, EventArgs e)
    {
        var nextWeek = League.WebsiteSettings.RowsReports.Count > 0
            ? League.WebsiteSettings.RowsReports.Max(r => r.WeekNumber) + 1
            : 1;

        var newReport = new RowsReport
        {
            WeekNumber = nextWeek,
            MatchDate = DateTime.Now,
            Title = $"Week {nextWeek} Round-Up",
            IsPublished = true
        };

        League.WebsiteSettings.RowsReports.Add(newReport);
        DataStore.Save();

        var item = ReportDisplayItem.FromModel(newReport);
        _reports.Insert(0, item);
        ReportsCollection.SelectedItem = item;
        SelectReport(newReport.Id);
    }

    private void OnReportSelected(object sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is ReportDisplayItem item)
        {
            SelectReport(item.Id);
        }
    }

    private void SelectReport(Guid reportId)
    {
        _selectedReportId = reportId;
        var report = League.WebsiteSettings.RowsReports.FirstOrDefault(r => r.Id == reportId);
        if (report == null) return;

        WeekNumberEntry.Text = report.WeekNumber.ToString();
        MatchDatePicker.Date = report.MatchDate;
        TitleEntry.Text = report.Title;
        AuthorEntry.Text = report.Author;
        SummaryEditor.Text = report.Summary;
        ContentEditor.Text = report.Content;
        TagsEntry.Text = string.Join(", ", report.Tags);
        IsPublishedCheck.IsChecked = report.IsPublished;

        EditorPlaceholder.IsVisible = false;
        EditorForm.IsVisible = true;
        DeleteReportBtn.IsEnabled = true;
        DuplicateReportBtn.IsEnabled = true;
    }

    private async void OnSaveReportClicked(object sender, EventArgs e)
    {
        if (!_selectedReportId.HasValue) return;

        var report = League.WebsiteSettings.RowsReports.FirstOrDefault(r => r.Id == _selectedReportId.Value);
        if (report == null) return;

        if (int.TryParse(WeekNumberEntry.Text, out int week))
            report.WeekNumber = week;

        report.MatchDate = MatchDatePicker.Date;
        report.Title = TitleEntry.Text?.Trim() ?? "";
        report.Author = AuthorEntry.Text?.Trim() ?? "";
        report.Summary = SummaryEditor.Text?.Trim() ?? "";
        report.Content = ContentEditor.Text?.Trim() ?? "";
        report.IsPublished = IsPublishedCheck.IsChecked;
        report.DatePublished = DateTime.Now;

        report.Tags = (TagsEntry.Text ?? "")
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(t => !string.IsNullOrWhiteSpace(t))
            .ToList();

        DataStore.Save();
        LoadReports();

        // Re-select the saved report
        var item = _reports.FirstOrDefault(r => r.Id == report.Id);
        if (item != null)
            ReportsCollection.SelectedItem = item;

        await DisplayAlert("Saved", "Report saved successfully.", "OK");
    }

    private async void OnDeleteReportClicked(object sender, EventArgs e)
    {
        if (!_selectedReportId.HasValue) return;

        var confirmed = await DisplayAlert("Delete Report",
            "Are you sure you want to delete this report? This cannot be undone.",
            "Delete", "Cancel");

        if (!confirmed) return;

        League.WebsiteSettings.RowsReports.RemoveAll(r => r.Id == _selectedReportId.Value);
        DataStore.Save();

        _selectedReportId = null;
        EditorPlaceholder.IsVisible = true;
        EditorForm.IsVisible = false;
        DeleteReportBtn.IsEnabled = false;
        DuplicateReportBtn.IsEnabled = false;

        LoadReports();
    }

    private void OnDuplicateReportClicked(object sender, EventArgs e)
    {
        if (!_selectedReportId.HasValue) return;

        var source = League.WebsiteSettings.RowsReports.FirstOrDefault(r => r.Id == _selectedReportId.Value);
        if (source == null) return;

        var duplicate = new RowsReport
        {
            WeekNumber = source.WeekNumber,
            MatchDate = source.MatchDate,
            Title = source.Title + " (Copy)",
            Author = source.Author,
            Summary = source.Summary,
            Content = source.Content,
            IsPublished = false,
            Tags = new List<string>(source.Tags)
        };

        League.WebsiteSettings.RowsReports.Add(duplicate);
        DataStore.Save();

        LoadReports();

        var item = _reports.FirstOrDefault(r => r.Id == duplicate.Id);
        if (item != null)
        {
            ReportsCollection.SelectedItem = item;
            SelectReport(duplicate.Id);
        }
    }

    /// <summary>
    /// Display wrapper for the CollectionView
    /// </summary>
    public sealed class ReportDisplayItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string WeekLabel { get; set; } = "";
        public string DateLabel { get; set; } = "";
        public string StatusIcon { get; set; } = "";

        public static ReportDisplayItem FromModel(RowsReport report) => new()
        {
            Id = report.Id,
            Title = string.IsNullOrWhiteSpace(report.Title) ? $"Week {report.WeekNumber}" : report.Title,
            WeekLabel = $"Wk {report.WeekNumber}",
            DateLabel = report.MatchDate.ToString("ddd dd MMM yyyy"),
            StatusIcon = report.IsPublished ? "\u2705" : "\u270F\uFE0F"
        };
    }
}
