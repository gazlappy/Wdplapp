using System.Xml.Linq;

namespace wdpl2.Tests;

public class FixtureDateCardControlsTests
{
    [Fact]
    public void FixturesTab_OpensSheetAndSheetExplainsHiddenDatesOnLoad()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "wdpl2.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var fixtures = Path.Combine(directory.FullName, "wdpl2", "Views", "Fixtures", "FixturesPage.xaml");
        var document = XDocument.Load(fixtures);
        Assert.Contains(document.Descendants(), e =>
            (string?)e.Attribute("Text") == "Fixtures Sheet" &&
            (string?)e.Attribute("Clicked") == "OnFixturesSheetClicked");
        Assert.Contains("Navigation.PushAsync(new WebsiteBuilder.FixturesSheetPage())", File.ReadAllText(fixtures + ".cs"));
        Assert.Contains("LoadData();\n        ShowSpecialEventsCheck.CheckedChanged", Read("FixturesSheetPage.xaml.cs").Replace("\r\n", "\n"));
        Assert.Contains("UpdateEventVisibilityStatus();", Read("FixturesSheetPage.xaml.cs"));
    }

    [Fact]
    public void Calendar_UsesSharedSnapshotAndCorrectPersistencePaths()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "wdpl2.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        var code = File.ReadAllText(Path.Combine(directory.FullName, "wdpl2", "Views", "Calendar", "CalendarPage.xaml.cs"));
        Assert.Contains("League => DataStore.Data", code);
        Assert.DoesNotContain("_dataStore.SaveAsync()", code);
        Assert.Contains("await _dataStore.UpdateSeasonAsync(season);", code);
        Assert.Contains("DataStore.SaveJsonOnly();", code);
    }

    private static string Read(string file)
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory != null && !File.Exists(Path.Combine(directory.FullName, "wdpl2.sln"))) directory = directory.Parent;
        Assert.NotNull(directory);
        return File.ReadAllText(Path.Combine(directory.FullName, "wdpl2", "Features", "WebsiteBuilder", "Views", file));
    }

    [Theory]
    [InlineData("FixturesSheetPage")]
    [InlineData("FixturesSettingsPage")]
    public void DateCards_HaveDiscoverableLabelAndSharedSavedSetting(string page)
    {
        var document = XDocument.Parse(Read(page + ".xaml"));
        XNamespace x = "http://schemas.microsoft.com/winfx/2009/xaml";
        var checkbox = Assert.Single(document.Descendants().Where(e => (string?)e.Attribute(x + "Name") == "ShowSpecialEventsCheck"));
        Assert.Contains(checkbox.Parent!.Elements(), e => (string?)e.Attribute("Text") == "Show competition / event date cards");
        var code = Read(page + ".xaml.cs");
        Assert.Contains("ShowSpecialEventsCheck.IsChecked =", code);
        Assert.Contains("ShowSpecialEvents = ShowSpecialEventsCheck.IsChecked", code);
    }
}
