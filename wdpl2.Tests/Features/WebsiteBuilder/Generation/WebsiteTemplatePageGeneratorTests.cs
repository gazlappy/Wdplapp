using System.Text;
using Moq;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for WebsiteTemplatePageGenerator — generates HTML template pages for players and teams.
/// </summary>
public class WebsiteTemplatePageGeneratorTests
{
    [Fact]
    public void Constructor_SetsSettings()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };

        // Act
        var generator = new WebsiteTemplatePageGenerator(settings);

        // Assert
        Assert.NotNull(generator);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_CallsAllDelegates()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };
        var cacheBuster = "12345";
        var tableClasses = "table table-striped";

        var mockDocHead = new Mock<Action<StringBuilder, string, Season>>();
        var mockHeader = new Mock<Action<StringBuilder, Season>>();
        var mockNav = new Mock<Action<StringBuilder, string>>();
        var mockFooter = new Mock<Action<StringBuilder>>();

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            cacheBuster,
            mockDocHead.Object,
            mockHeader.Object,
            mockNav.Object,
            mockFooter.Object,
            tableClasses);

        // Assert
        mockDocHead.Verify(x => x(It.IsAny<StringBuilder>(), $"Player - {settings.LeagueName}", season), Times.Once);
        mockHeader.Verify(x => x(It.IsAny<StringBuilder>(), season), Times.Once);
        mockNav.Verify(x => x(It.IsAny<StringBuilder>(), "Players"), Times.Once);
        mockFooter.Verify(x => x(It.IsAny<StringBuilder>()), Times.Once);
        Assert.NotNull(result);
        Assert.Contains("<body>", result);
        Assert.Contains("</body>", result);
        Assert.Contains("</html>", result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_IncludesCacheBusterInJavaScript()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };
        var cacheBuster = "abc123";
        var tableClasses = "table";

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            cacheBuster,
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            tableClasses);

        // Assert
        Assert.Contains($"var cacheBuster = '{cacheBuster}';", result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_IncludesCustomBodyStartHtml()
    {
        // Arrange
        var customHtml = "<!-- Custom Start -->";
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyStartHtml = customHtml
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains(customHtml, result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_ExcludesCustomBodyStartHtml_WhenNull()
    {
        // Arrange
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyStartHtml = null!
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("<body>", result);
        Assert.DoesNotContain("<!-- Custom Start -->", result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_IncludesCustomBodyEndHtml()
    {
        // Arrange
        var customHtml = "<!-- Custom End -->";
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyEndHtml = customHtml
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains(customHtml, result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_ExcludesCustomBodyEndHtml_WhenWhitespace()
    {
        // Arrange
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyEndHtml = "   "
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("</body>", result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_EscapesSingleQuotesInLeagueName()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Bob's League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("Bob\\'s League", result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_IncludesTableClasses()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };
        var tableClasses = "custom-table striped-table";

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            tableClasses);

        // Assert
        Assert.Contains($"<table class=\"{tableClasses}\">", result);
    }

    [Fact]
    public void GeneratePlayerTemplatePage_ContainsExpectedHtmlStructure()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GeneratePlayerTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("Loading player data...", result);
        Assert.Contains("Player Not Found", result);
        Assert.Contains("players-data.json", result);
        Assert.Contains("id=\"player-name\"", result);
        Assert.Contains("id=\"player-team\"", result);
        Assert.Contains("id=\"stats-grid\"", result);
        Assert.Contains("id=\"match-history\"", result);
        Assert.Contains("Back to All Players", result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_CallsAllDelegates()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };
        var cacheBuster = "12345";
        var tableClasses = "table table-striped";

        var mockDocHead = new Mock<Action<StringBuilder, string, Season>>();
        var mockHeader = new Mock<Action<StringBuilder, Season>>();
        var mockNav = new Mock<Action<StringBuilder, string>>();
        var mockFooter = new Mock<Action<StringBuilder>>();

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            cacheBuster,
            mockDocHead.Object,
            mockHeader.Object,
            mockNav.Object,
            mockFooter.Object,
            tableClasses);

        // Assert
        mockDocHead.Verify(x => x(It.IsAny<StringBuilder>(), $"Team - {settings.LeagueName}", season), Times.Once);
        mockHeader.Verify(x => x(It.IsAny<StringBuilder>(), season), Times.Once);
        mockNav.Verify(x => x(It.IsAny<StringBuilder>(), "Divisions"), Times.Once);
        mockFooter.Verify(x => x(It.IsAny<StringBuilder>()), Times.Once);
        Assert.NotNull(result);
        Assert.Contains("<body>", result);
        Assert.Contains("</body>", result);
        Assert.Contains("</html>", result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_IncludesCacheBusterInJavaScript()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };
        var cacheBuster = "xyz789";
        var tableClasses = "table";

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            cacheBuster,
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            tableClasses);

        // Assert
        Assert.Contains($"var cacheBuster = '{cacheBuster}';", result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_IncludesCustomBodyStartHtml()
    {
        // Arrange
        var customHtml = "<!-- Team Custom Start -->";
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyStartHtml = customHtml
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains(customHtml, result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_ExcludesCustomBodyStartHtml_WhenEmpty()
    {
        // Arrange
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyStartHtml = ""
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("<body>", result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_IncludesCustomBodyEndHtml()
    {
        // Arrange
        var customHtml = "<!-- Team Custom End -->";
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyEndHtml = customHtml
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains(customHtml, result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_ExcludesCustomBodyEndHtml_WhenNull()
    {
        // Arrange
        var settings = new WebsiteSettings
        {
            LeagueName = "Test League",
            CustomBodyEndHtml = null!
        };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("</body>", result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_EscapesSingleQuotesInLeagueName()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Wellington's League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("Wellington\\'s League", result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_IncludesTableClasses()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };
        var tableClasses = "team-table data-table";

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            tableClasses);

        // Assert
        Assert.Contains($"<table class=\"{tableClasses}\">", result);
    }

    [Fact]
    public void GenerateTeamTemplatePage_ContainsExpectedHtmlStructure()
    {
        // Arrange
        var settings = new WebsiteSettings { LeagueName = "Test League" };
        var generator = new WebsiteTemplatePageGenerator(settings);
        var season = new Season { Name = "2024" };

        // Act
        var result = generator.GenerateTeamTemplatePage(
            season,
            "12345",
            (sb, title, s) => { },
            (sb, s) => { },
            (sb, nav) => { },
            sb => { },
            "table");

        // Assert
        Assert.Contains("Loading team data...", result);
        Assert.Contains("Team Not Found", result);
        Assert.Contains("teams-data.json", result);
        Assert.Contains("id=\"team-name\"", result);
        Assert.Contains("id=\"team-division\"", result);
        Assert.Contains("id=\"stats-grid\"", result);
        Assert.Contains("id=\"team-info\"", result);
        Assert.Contains("id=\"roster-list\"", result);
        Assert.Contains("id=\"match-history\"", result);
        Assert.Contains("Team Roster", result);
        Assert.Contains("Recent Matches", result);
        Assert.Contains("Back to Divisions", result);
    }
}
