using System.Collections;
using Moq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for WebsiteGenerator — static HTML website generation.
/// </summary>
public class WebsiteGeneratorTests
{
    private static LeagueData CreateTestLeagueData(bool hasActiveSeason = true, Guid? seasonId = null)
    {
        var league = new LeagueData();
        var season = new Season
        {
            Id = seasonId ?? Guid.NewGuid(),
            Name = "2024",
            IsActive = hasActiveSeason
        };
        league.Seasons.Add(season);
        return league;
    }

    private static WebsiteSettings CreateTestSettings()
    {
        return new WebsiteSettings
        {
            LeagueName = "Test League",
            SelectedTemplate = "modern"
        };
    }

    #region Constructor Tests

    [Fact]
    public void Constructor_ValidParameters_StoresLeagueAndSettings()
    {
        // Arrange
        var league = new LeagueData();
        var settings = new WebsiteSettings();

        // Act
        var generator = new WebsiteGenerator(league, settings);

        // Assert
        Assert.NotNull(generator);
    }

    #endregion

    #region GenerateWebsite Tests

    [Fact]
    public void GenerateWebsite_NoSeasons_ThrowsInvalidOperationException()
    {
        // Arrange
        var league = new LeagueData();
        var settings = CreateTestSettings();
        var generator = new WebsiteGenerator(league, settings);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.GenerateWebsite());
        Assert.Equal("No season selected for website generation", ex.Message);
    }

    [Fact]
    public void GenerateWebsite_SelectedSeasonIdNotFound_ThrowsInvalidOperationException()
    {
        // Arrange
        var league = CreateTestLeagueData(hasActiveSeason: false);
        var settings = CreateTestSettings();
        settings.SelectedSeasonId = Guid.NewGuid(); // Non-existent ID
        var generator = new WebsiteGenerator(league, settings);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.GenerateWebsite());
        Assert.Equal("No season selected for website generation", ex.Message);
    }

    [Fact]
    public void GenerateWebsite_NoActiveSeasonAndNoSelectedId_ThrowsInvalidOperationException()
    {
        // Arrange
        var league = CreateTestLeagueData(hasActiveSeason: false);
        var settings = CreateTestSettings();
        settings.SelectedSeasonId = null;
        var generator = new WebsiteGenerator(league, settings);

        // Act & Assert
        var ex = Assert.Throws<InvalidOperationException>(() => generator.GenerateWebsite());
        Assert.Equal("No season selected for website generation", ex.Message);
    }

    [Fact]
    public void GenerateWebsite_WithActiveSeason_GeneratesCorePagesOnly()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        // Disable all optional pages
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.NotNull(files);
        Assert.Contains("home.html", files.Keys);
        Assert.Contains("style.css", files.Keys);
        Assert.Equal(2, files.Count);
    }

    [Fact]
    public void GenerateWebsite_WithSelectedSeasonId_UsesSpecifiedSeason()
    {
        // Arrange
        var seasonId = Guid.NewGuid();
        var league = CreateTestLeagueData(hasActiveSeason: false, seasonId: seasonId);
        var settings = CreateTestSettings();
        settings.SelectedSeasonId = seasonId;
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.NotNull(files);
        Assert.Contains("home.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowStandings_GeneratesStandingsPages()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = true;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("standings.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowFixtures_GeneratesFixturesPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = true;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("fixtures.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowResults_GeneratesResultsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = true;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("results.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowPlayerStats_GeneratesPlayerPages()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = true;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("players.html", files.Keys);
        Assert.Contains("players-data.json", files.Keys);
        Assert.Contains("player.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowDivisions_GeneratesDivisionsAndTeamPages()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = true;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("divisions.html", files.Keys);
        Assert.Contains("teams-data.json", files.Keys);
        Assert.Contains("team.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowCompetitions_GeneratesCompetitionsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = true;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("competitions.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowGalleryWithImages_GeneratesGalleryPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = true;
        settings.GalleryImages.Add(new GalleryImage { FileName = "test.jpg", Caption = "Test" });
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("gallery.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowGalleryWithoutImages_DoesNotGenerateGalleryPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = true;
        settings.GalleryImages.Clear();
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("gallery.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowRulesWithContent_GeneratesRulesPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = true;
        settings.ConstitutionContent = "Some rules";
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("rules.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowContactWithInfo_GeneratesContactPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = true;
        settings.ContactEmail = "test@example.com";
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("contact.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowSponsorsWithSponsors_GeneratesSponsorsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = true;
        settings.Sponsors.Add(new Sponsor { Name = "Test Sponsor" });
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("sponsors.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowNewsWithItems_GeneratesNewsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = true;
        settings.NewsItems.Add(new NewsItem { Title = "Test News" });
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("news.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowRowsReportsWithReports_GeneratesRowsReportsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = true;
        settings.RowsReports.Add(new RowsReport { Title = "Test Report" });
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("rows-reports.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowEntryFormsWithPublishedForms_GeneratesEntryFormsPages()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = true;
        settings.EntryForms.Add(new EntryForm { Title = "Test Form", IsPublished = true });
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("entry-forms.html", files.Keys);
        Assert.Contains("_submissions.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowEntryFormsWithoutPublishedForms_DoesNotGenerateEntryFormsPages()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = true;
        settings.EntryForms.Add(new EntryForm { Title = "Test Form", IsPublished = false });
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("entry-forms.html", files.Keys);
        Assert.DoesNotContain("_submissions.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowPoolGame_GeneratesPoolGamePage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = true;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("pool-game.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowHistoryWithHonours_GeneratesHistoryPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = true;
        settings.HistoricHonours.Add(new HistoricHonour { Season = "2020", Title = "Champion" });
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("history.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_CustomPageWithSlug_GeneratesPageWithSlug()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        settings.CustomPages.Add(new CustomPage { Title = "About", Slug = "about-us", IsPublished = true });
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("about-us.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_CustomPageWithoutSlug_GeneratesPageWithTitleSlug()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        settings.CustomPages.Add(new CustomPage { Title = "About Us", Slug = "", IsPublished = true });
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("about-us.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_CustomPageNotPublished_DoesNotGeneratePage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        settings.CustomPages.Add(new CustomPage { Title = "Draft", Slug = "draft", IsPublished = false });
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("draft.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_GenerateSitemap_GeneratesSitemapXml()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = true;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("sitemap.xml", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_CustomPageWithWhitespaceSlug_GeneratesPageWithTitleSlug()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        settings.CustomPages.Add(new CustomPage { Title = "About Us", Slug = "   ", IsPublished = true });
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("about-us.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_GenerateSitemap_DoesNotGenerateWithoutFlag()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("sitemap.xml", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_MultipleCustomPages_GeneratesAllPublishedPages()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        settings.CustomPages.Add(new CustomPage { Title = "Page 1", Slug = "page-1", IsPublished = true });
        settings.CustomPages.Add(new CustomPage { Title = "Page 2", Slug = "page-2", IsPublished = true });
        settings.CustomPages.Add(new CustomPage { Title = "Page 3", Slug = "page-3", IsPublished = false });
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("page-1.html", files.Keys);
        Assert.Contains("page-2.html", files.Keys);
        Assert.DoesNotContain("page-3.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowContactWithoutInfo_DoesNotGenerateContactPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = true;
        settings.ContactEmail = "";
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("contact.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowSponsorsWithoutSponsors_DoesNotGenerateSponsorsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = true;
        settings.Sponsors.Clear();
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("sponsors.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowNewsWithoutItems_DoesNotGenerateNewsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = true;
        settings.NewsItems.Clear();
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("news.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowRowsReportsWithoutReports_DoesNotGenerateRowsReportsPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = true;
        settings.RowsReports.Clear();
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("rows-reports.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowHistoryWithoutHonours_DoesNotGenerateHistoryPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = true;
        settings.HistoricHonours.Clear();
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("history.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_ShowRulesWithoutContent_DoesNotGenerateRulesPage()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = true;
        settings.ConstitutionContent = "";
        settings.MatchRulesContent = "";
        settings.EpaRulesContent = "";
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.DoesNotContain("rules.html", files.Keys);
    }

    [Fact]
    public void GenerateWebsite_CustomPageWithNullSlug_GeneratesPageWithTitleSlug()
    {
        // Arrange
        var league = CreateTestLeagueData();
        var settings = CreateTestSettings();
        settings.ShowStandings = false;
        settings.ShowFixtures = false;
        settings.ShowResults = false;
        settings.ShowPlayerStats = false;
        settings.ShowDivisions = false;
        settings.ShowCompetitions = false;
        settings.ShowGallery = false;
        settings.ShowRules = false;
        settings.ShowContactPage = false;
        settings.ShowSponsors = false;
        settings.ShowNews = false;
        settings.ShowRowsReports = false;
        settings.ShowEntryForms = false;
        settings.ShowPoolGame = false;
        settings.ShowHistory = false;
        settings.GenerateSitemap = false;
        settings.CustomPages.Add(new CustomPage { Title = "Test Page", Slug = null!, IsPublished = true });
        var generator = new WebsiteGenerator(league, settings);

        // Act
        var files = generator.GenerateWebsite();

        // Assert
        Assert.Contains("test-page.html", files.Keys);
    }

    #endregion

    #region PlayerStat Tests

    [Fact]
    public void PlayerStat_WinPercentage_WithPlayed_ReturnsCorrectPercentage()
    {
        // Arrange
        var playerStat = CreatePlayerStat(played: 10, won: 7);

        // Act
        var percentage = GetWinPercentage(playerStat);

        // Assert
        Assert.Equal(70.0, percentage);
    }

    [Fact]
    public void PlayerStat_WinPercentage_WithZeroPlayed_ReturnsZero()
    {
        // Arrange
        var playerStat = CreatePlayerStat(played: 0, won: 0);

        // Act
        var percentage = GetWinPercentage(playerStat);

        // Assert
        Assert.Equal(0, percentage);
    }

    [Fact]
    public void PlayerStat_WinPercentage_WithAllWins_Returns100()
    {
        // Arrange
        var playerStat = CreatePlayerStat(played: 5, won: 5);

        // Act
        var percentage = GetWinPercentage(playerStat);

        // Assert
        Assert.Equal(100.0, percentage);
    }

    [Fact]
    public void PlayerStat_WinPercentage_WithNoWins_ReturnsZero()
    {
        // Arrange
        var playerStat = CreatePlayerStat(played: 5, won: 0);

        // Act
        var percentage = GetWinPercentage(playerStat);

        // Assert
        Assert.Equal(0, percentage);
    }

    [Fact]
    public void PlayerStat_WinPercentage_WithFractionalResult_ReturnsCorrectDecimal()
    {
        // Arrange
        var playerStat = CreatePlayerStat(played: 3, won: 1);

        // Act
        var percentage = GetWinPercentage(playerStat);

        // Assert
        Assert.Equal(33.333333333333336, percentage, precision: 10);
    }

    private static object CreatePlayerStat(int played, int won)
    {
        // Use reflection to create PlayerStat since it's a private nested class
        var type = typeof(WebsiteGenerator).GetNestedType("PlayerStat", System.Reflection.BindingFlags.NonPublic);
        var instance = Activator.CreateInstance(type!);
        type!.GetProperty("Played")!.SetValue(instance, played);
        type!.GetProperty("Won")!.SetValue(instance, won);
        return instance!;
    }

    private static double GetWinPercentage(object playerStat)
    {
        var type = playerStat.GetType();
        var property = type.GetProperty("WinPercentage");
        return (double)property!.GetValue(playerStat)!;
    }

    #endregion

    #region SingleGrouping Tests

    [Fact]
    public void SingleGrouping_Constructor_StoresKeyAndElements()
    {
        // Arrange
        var key = "test-key";
        var elements = new List<string> { "element1", "element2" };

        // Act
        var grouping = new SingleGrouping<string, string>(key, elements);

        // Assert
        Assert.NotNull(grouping);
        Assert.Equal(key, grouping.Key);
    }

    [Fact]
    public void SingleGrouping_Key_ReturnsStoredKey()
    {
        // Arrange
        var key = 42;
        var elements = new List<int> { 1, 2, 3 };
        var grouping = new SingleGrouping<int, int>(key, elements);

        // Act
        var retrievedKey = grouping.Key;

        // Assert
        Assert.Equal(42, retrievedKey);
    }

    [Fact]
    public void SingleGrouping_GetEnumerator_ReturnsElementsEnumerator()
    {
        // Arrange
        var key = "test";
        var elements = new List<string> { "a", "b", "c" };
        var grouping = new SingleGrouping<string, string>(key, elements);

        // Act
        var result = grouping.ToList();

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Contains("a", result);
        Assert.Contains("b", result);
        Assert.Contains("c", result);
    }

    [Fact]
    public void SingleGrouping_NonGenericGetEnumerator_ReturnsElementsEnumerator()
    {
        // Arrange
        var key = "test";
        var elements = new List<string> { "x", "y" };
        var grouping = new SingleGrouping<string, string>(key, elements);

        // Act
        var enumerator = ((IEnumerable)grouping).GetEnumerator();
        var result = new List<string>();
        while (enumerator.MoveNext())
        {
            result.Add((string)enumerator.Current);
        }

        // Assert
        Assert.Equal(2, result.Count);
        Assert.Contains("x", result);
        Assert.Contains("y", result);
    }

    [Fact]
    public void SingleGrouping_GenericGetEnumerator_EnumeratesAllElements()
    {
        // Arrange
        var key = 1;
        var elements = new List<int> { 10, 20, 30 };
        var grouping = new SingleGrouping<int, int>(key, elements);

        // Act
        using var enumerator = grouping.GetEnumerator();
        var result = new List<int>();
        while (enumerator.MoveNext())
        {
            result.Add(enumerator.Current);
        }

        // Assert
        Assert.Equal(3, result.Count);
        Assert.Equal(10, result[0]);
        Assert.Equal(20, result[1]);
        Assert.Equal(30, result[2]);
    }

    [Fact]
    public void SingleGrouping_GenericGetEnumerator_EmptyList_ReturnsEmptyEnumerator()
    {
        // Arrange
        var key = "empty";
        var elements = new List<string>();
        var grouping = new SingleGrouping<string, string>(key, elements);

        // Act
        using var enumerator = grouping.GetEnumerator();
        var hasElements = enumerator.MoveNext();

        // Assert
        Assert.False(hasElements);
    }

    [Fact]
    public void SingleGrouping_NonGenericGetEnumerator_EmptyList_ReturnsEmptyEnumerator()
    {
        // Arrange
        var key = 42;
        var elements = new List<int>();
        var grouping = new SingleGrouping<int, int>(key, elements);

        // Act
        var enumerator = ((IEnumerable)grouping).GetEnumerator();
        var hasElements = enumerator.MoveNext();

        // Assert
        Assert.False(hasElements);
    }

    [Fact]
    public void SingleGrouping_GenericGetEnumerator_SingleElement_EnumeratesCorrectly()
    {
        // Arrange
        var key = "single";
        var elements = new List<string> { "only" };
        var grouping = new SingleGrouping<string, string>(key, elements);

        // Act
        using var enumerator = grouping.GetEnumerator();
        var results = new List<string>();
        while (enumerator.MoveNext())
        {
            results.Add(enumerator.Current);
        }

        // Assert
        Assert.Single(results);
        Assert.Equal("only", results[0]);
    }

    [Fact]
    public void SingleGrouping_NonGenericGetEnumerator_MultipleIterations_WorksCorrectly()
    {
        // Arrange
        var key = "test";
        var elements = new List<string> { "a", "b" };
        var grouping = new SingleGrouping<string, string>(key, elements);

        // Act
        var enumerator1 = ((IEnumerable)grouping).GetEnumerator();
        var count1 = 0;
        while (enumerator1.MoveNext()) count1++;

        var enumerator2 = ((IEnumerable)grouping).GetEnumerator();
        var count2 = 0;
        while (enumerator2.MoveNext()) count2++;

        // Assert
        Assert.Equal(2, count1);
        Assert.Equal(2, count2);
    }

    #endregion
}
