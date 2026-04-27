using Moq;
using Wdpl2.Services;
using Wdpl2.ViewModels;

namespace Wdpl2.Tests;

/// <summary>
/// Tests for SeasonsViewModel — season list and CRUD operations.
/// </summary>
public class SeasonsViewModelTests
{
    [Fact]
    public void Constructor_ValidDataStore_InitializesViewModel()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();

        // Act
        var viewModel = new SeasonsViewModel(mockDataStore.Object);

        // Assert
        Assert.NotNull(viewModel);
    }

    [Fact]
    public void Cleanup_WhenCalled_DoesNotThrow()
    {
        // Arrange
        var mockDataStore = new Mock<IDataStore>();
        var viewModel = new SeasonsViewModel(mockDataStore.Object);

        // Act & Assert
        viewModel.Cleanup();
    }
}
