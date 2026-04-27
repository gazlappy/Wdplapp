using System;
using System.Reflection;
using Moq;
using Wdpl2.ViewModels;
using Wdpl2.Views;

namespace wdpl2.Tests;

/// <summary>
/// Tests for CompetitionsPage and related types.
/// </summary>
/// <remarks>
/// Note: The CompetitionsPage constructors, OnAppearing(), OnDisappearing(), and SetContentPanel() 
/// methods cannot be unit tested because they depend on:
/// 1. XAML infrastructure (InitializeComponent) which requires a running MAUI application
/// 2. UI controls (ContentPanel, SeasonLabel, etc.) that are initialized through XAML
/// 3. Application.Current?.Handler?.MauiContext for dependency injection resolution
/// 4. Static DataStore which has a static constructor that depends on FileSystem.AppDataDirectory
/// 5. Static SeasonService for current season management
/// 6. ContentPage lifecycle methods (base.OnAppearing, base.OnDisappearing) that require UI context
/// 
/// These dependencies require a full MAUI application context and should be tested
/// through integration/UI tests rather than unit tests. Refactoring to use dependency
/// injection would make these methods unit-testable.
/// 
/// Reference: Repository insight on MAUI ContentPage testing limitations.
/// </remarks>
public class CompetitionsPageXamlTests
{
    [Fact]
    public void CompetitionsPage_CannotBeUnitTested_DocumentationTest()
    {
        // This test documents that CompetitionsPage constructors and lifecycle methods
        // cannot be unit tested due to MAUI infrastructure dependencies.
        // See class remarks for details.
        Assert.True(true);
    }

    [Fact]
    public void SetStatus_WithText_SetsFormattedStatusMessage()
    {
        // Arrange
        var mockDataStore = new Mock<Wdpl2.Services.IDataStore>();
        var mockSeasonService = new Mock<Wdpl2.Services.ISeasonService>();
        mockSeasonService.Setup(s => s.CurrentSeasonId).Returns(Guid.NewGuid());

        var viewModel = new CompetitionsViewModel(mockDataStore.Object, mockSeasonService.Object);

        var page = CreateCompetitionsPageWithoutConstructor();
        SetPrivateField(page, "_viewModel", viewModel);

        var testMessage = "Test status message";

        // Act
        page.SetStatus(testMessage);

        // Assert
        var statusMessage = viewModel.StatusMessage;

        Assert.NotNull(statusMessage);
        Assert.Contains(testMessage, statusMessage);
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}\s{2}Test status message$", statusMessage);
    }

    [Fact]
    public void SetStatus_WithEmptyString_SetsFormattedStatusMessageWithEmptyText()
    {
        // Arrange
        var mockDataStore = new Mock<Wdpl2.Services.IDataStore>();
        var mockSeasonService = new Mock<Wdpl2.Services.ISeasonService>();
        mockSeasonService.Setup(s => s.CurrentSeasonId).Returns(Guid.NewGuid());

        var viewModel = new CompetitionsViewModel(mockDataStore.Object, mockSeasonService.Object);

        var page = CreateCompetitionsPageWithoutConstructor();
        SetPrivateField(page, "_viewModel", viewModel);

        // Act
        page.SetStatus(string.Empty);

        // Assert
        var statusMessage = viewModel.StatusMessage;

        Assert.NotNull(statusMessage);
        Assert.Matches(@"^\d{2}:\d{2}:\d{2}\s{2}$", statusMessage);
    }

    [Fact]
    public void SetStatus_WithSpecialCharacters_SetsFormattedStatusMessageWithSpecialCharacters()
    {
        // Arrange
        var mockDataStore = new Mock<Wdpl2.Services.IDataStore>();
        var mockSeasonService = new Mock<Wdpl2.Services.ISeasonService>();
        mockSeasonService.Setup(s => s.CurrentSeasonId).Returns(Guid.NewGuid());

        var viewModel = new CompetitionsViewModel(mockDataStore.Object, mockSeasonService.Object);

        var page = CreateCompetitionsPageWithoutConstructor();
        SetPrivateField(page, "_viewModel", viewModel);

        var testMessage = "Test: <html> & \"quotes\" 'apostrophe'";

        // Act
        page.SetStatus(testMessage);

        // Assert
        var statusMessage = viewModel.StatusMessage;

        Assert.NotNull(statusMessage);
        Assert.Contains(testMessage, statusMessage);
    }

    [Fact]
    public void SetStatus_WithLongText_SetsFormattedStatusMessageWithFullText()
    {
        // Arrange
        var mockDataStore = new Mock<Wdpl2.Services.IDataStore>();
        var mockSeasonService = new Mock<Wdpl2.Services.ISeasonService>();
        mockSeasonService.Setup(s => s.CurrentSeasonId).Returns(Guid.NewGuid());

        var viewModel = new CompetitionsViewModel(mockDataStore.Object, mockSeasonService.Object);

        var page = CreateCompetitionsPageWithoutConstructor();
        SetPrivateField(page, "_viewModel", viewModel);

        var testMessage = new string('A', 500);

        // Act
        page.SetStatus(testMessage);

        // Assert
        var statusMessage = viewModel.StatusMessage;

        Assert.NotNull(statusMessage);
        Assert.Contains(testMessage, statusMessage);
    }

    [Fact]
    public void SetStatus_CalledMultipleTimes_UpdatesStatusMessageEachTime()
    {
        // Arrange
        var mockDataStore = new Mock<Wdpl2.Services.IDataStore>();
        var mockSeasonService = new Mock<Wdpl2.Services.ISeasonService>();
        mockSeasonService.Setup(s => s.CurrentSeasonId).Returns(Guid.NewGuid());

        var viewModel = new CompetitionsViewModel(mockDataStore.Object, mockSeasonService.Object);

        var page = CreateCompetitionsPageWithoutConstructor();
        SetPrivateField(page, "_viewModel", viewModel);

        // Act & Assert - First call
        page.SetStatus("First message");
        var firstMessage = viewModel.StatusMessage;
        Assert.Contains("First message", firstMessage);

        // Small delay to ensure different timestamps
        System.Threading.Thread.Sleep(1100);

        // Act & Assert - Second call
        page.SetStatus("Second message");
        var secondMessage = viewModel.StatusMessage;
        Assert.Contains("Second message", secondMessage);
        Assert.NotEqual(firstMessage, secondMessage);
    }

    private static CompetitionsPage CreateCompetitionsPageWithoutConstructor()
    {
        return (CompetitionsPage)System.Runtime.CompilerServices.RuntimeHelpers
            .GetUninitializedObject(typeof(CompetitionsPage));
    }

    private static void SetPrivateField(object obj, string fieldName, object value)
    {
        var field = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance);
        if (field == null)
        {
            throw new ArgumentException($"Field '{fieldName}' not found on type '{obj.GetType().Name}'");
        }
        field.SetValue(obj, value);
    }
}
