using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests.Features.Games.Pool.Engine;

public class PoolGameGeneratorTests
{
    [Fact]
    public void GeneratePoolGameHtml_WithValidLeagueName_ReturnsNonNull()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePoolGameHtml_WithValidLeagueName_ReturnsNonEmpty()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GeneratePoolGameHtml_WithValidLeagueName_ContainsLeagueNameInTitle()
    {
        // Arrange
        var leagueName = "Wellington District Pool League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains($"UK 8-Ball Pool - {leagueName}", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_WithEmptyLeagueName_ReturnsValidHtml()
    {
        // Arrange
        var leagueName = "";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.NotNull(result);
        Assert.Contains("UK 8-Ball Pool - ", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_WithSpecialCharacters_EncodesCorrectly()
    {
        // Arrange
        var leagueName = "Test & League <>";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains($"UK 8-Ball Pool - {leagueName}", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_WithLongLeagueName_HandlesCorrectly()
    {
        // Arrange
        var leagueName = new string('A', 100);

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.NotNull(result);
        Assert.Contains(leagueName, result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsDocType()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.StartsWith("<!DOCTYPE html>", result.TrimStart());
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsHtmlTag()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<html lang=\"en\">", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsHeadSection()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<head>", result);
        Assert.Contains("</head>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsBodySection()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<body>", result);
        Assert.Contains("</body>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsMetaCharset()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<meta charset=\"UTF-8\">", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsViewportMeta()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<meta name=\"viewport\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsStyleSection()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<style>", result);
        Assert.Contains("</style>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCssForGameContainer()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains(".game-container", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCssForPlayerPanel()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains(".player-panel", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCssForTableContainer()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains(".table-container", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCssForMobileControls()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains(".mobile-controls", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCssForBallReturnWindow()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains(".ball-return-window", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCssMediaQueries()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("@media (max-width: 768px)", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCanvasElement()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<canvas id=\"poolTable\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsGameHeaderDiv()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<div class=\"game-header\">", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsGameTitle()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("UK 8-Ball Pool</h1>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsNewGameButton()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"newGameBtn\"", result);
        Assert.Contains("New Game", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsRulesButton()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"rulesBtn\"", result);
        Assert.Contains("EPA Rules", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsBallInHandButton()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"ballInHandBtn\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsDevSettingsButton()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"devSettingsBtn\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsPlayer1Panel()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"player1Panel\"", result);
        Assert.Contains("<h3>Player 1</h3>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsPlayer2Panel()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"player2Panel\"", result);
        Assert.Contains("<h3>Player 2</h3>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsTurnIndicator()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"turnIndicator\"", result);
        Assert.Contains("Player 1's Turn", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsGameMessage()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"gameMessage\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsBallReturnTray()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"ballReturnTray\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsMobileControls()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"mobileControls\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsRulesModal()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"rulesModal\"", result);
        Assert.Contains("EPA International 8-Ball Rules", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsScriptTags()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<script>", result);
        Assert.Contains("</script>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsMobileDetectionScript()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("const isMobile =", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsInstructions()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("<div class=\"instructions\">", result);
        Assert.Contains("EPA International Rules", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsLegalBreakRules()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("Legal Break", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsPowerBarContainer()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"powerBarContainer\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsFoulIndicator()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"foulIndicator\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsBallStats()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"redsPotted\"", result);
        Assert.Contains("id=\"yellowsPotted\"", result);
        Assert.Contains("id=\"blackPotted\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCssAnimations()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("@keyframes", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsMobilePowerSlider()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"mobilePowerSlider\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsMobileShootButton()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"mobileShootBtn\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsDebugInfo()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("id=\"debugInfo\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsCanvasWrapper()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("class=\"canvas-wrapper\"", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_WithNullLeagueName_HandlesCorrectly()
    {
        // Arrange
        string? leagueName = null;

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName!);

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsClosingHtmlTag()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("</html>", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsEpaRulesLink()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("www.epa.org.uk", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsTouchActionNone()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("touch-action: none", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsResponsiveStyles()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("@media (max-width: 480px)", result);
    }

    [Fact]
    public void GeneratePoolGameHtml_ContainsLandscapeMediaQuery()
    {
        // Arrange
        var leagueName = "Test League";

        // Act
        var result = PoolGameGenerator.GeneratePoolGameHtml(leagueName);

        // Assert
        Assert.Contains("@media (max-height: 500px) and (orientation: landscape)", result);
    }
}
