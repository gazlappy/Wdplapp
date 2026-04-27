using Moq;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for INotificationService interface contract verification.
/// </summary>
public class INotificationServiceTests
{
    [Fact]
    public async Task RequestPermissionsAsync_WhenGranted_ReturnsTrue()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        mock.Setup(x => x.RequestPermissionsAsync()).ReturnsAsync(true);
        var service = mock.Object;

        // Act
        var result = await service.RequestPermissionsAsync();

        // Assert
        Assert.True(result);
        mock.Verify(x => x.RequestPermissionsAsync(), Times.Once);
    }

    [Fact]
    public async Task RequestPermissionsAsync_WhenDenied_ReturnsFalse()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        mock.Setup(x => x.RequestPermissionsAsync()).ReturnsAsync(false);
        var service = mock.Object;

        // Act
        var result = await service.RequestPermissionsAsync();

        // Assert
        Assert.False(result);
        mock.Verify(x => x.RequestPermissionsAsync(), Times.Once);
    }

    [Fact]
    public async Task AreNotificationsEnabledAsync_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        mock.Setup(x => x.AreNotificationsEnabledAsync()).ReturnsAsync(true);
        var service = mock.Object;

        // Act
        var result = await service.AreNotificationsEnabledAsync();

        // Assert
        Assert.True(result);
        mock.Verify(x => x.AreNotificationsEnabledAsync(), Times.Once);
    }

    [Fact]
    public async Task AreNotificationsEnabledAsync_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        mock.Setup(x => x.AreNotificationsEnabledAsync()).ReturnsAsync(false);
        var service = mock.Object;

        // Act
        var result = await service.AreNotificationsEnabledAsync();

        // Assert
        Assert.False(result);
        mock.Verify(x => x.AreNotificationsEnabledAsync(), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithAllParameters_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 1;
        var title = "Test Title";
        var message = "Test Message";
        var scheduledTime = DateTime.Now.AddHours(1);
        var soundFileName = "notification.mp3";

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime, soundFileName);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, soundFileName), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithoutSoundFileName_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 2;
        var title = "Reminder";
        var message = "Don't forget!";
        var scheduledTime = DateTime.Now.AddDays(1);

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, null), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithNullSoundFileName_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 3;
        var title = "Alert";
        var message = "Important notification";
        var scheduledTime = DateTime.Now.AddMinutes(30);

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime, null);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, null), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithEmptyStrings_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 4;
        var title = string.Empty;
        var message = string.Empty;
        var scheduledTime = DateTime.Now;

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, null), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithZeroId_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 0;
        var title = "Test";
        var message = "Test";
        var scheduledTime = DateTime.Now;

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, null), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithNegativeId_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = -1;
        var title = "Test";
        var message = "Test";
        var scheduledTime = DateTime.Now;

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, null), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithPastDate_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 5;
        var title = "Past Notification";
        var message = "This is in the past";
        var scheduledTime = DateTime.Now.AddDays(-1);

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, null), Times.Once);
    }

    [Fact]
    public async Task ScheduleNotificationAsync_WithFutureDate_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 6;
        var title = "Future Notification";
        var message = "This is in the future";
        var scheduledTime = DateTime.Now.AddYears(1);

        // Act
        await service.ScheduleNotificationAsync(id, title, message, scheduledTime);

        // Assert
        mock.Verify(x => x.ScheduleNotificationAsync(id, title, message, scheduledTime, null), Times.Once);
    }

    [Fact]
    public async Task CancelNotificationAsync_WithPositiveId_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 10;

        // Act
        await service.CancelNotificationAsync(id);

        // Assert
        mock.Verify(x => x.CancelNotificationAsync(id), Times.Once);
    }

    [Fact]
    public async Task CancelNotificationAsync_WithZeroId_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 0;

        // Act
        await service.CancelNotificationAsync(id);

        // Assert
        mock.Verify(x => x.CancelNotificationAsync(id), Times.Once);
    }

    [Fact]
    public async Task CancelNotificationAsync_WithNegativeId_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = -5;

        // Act
        await service.CancelNotificationAsync(id);

        // Assert
        mock.Verify(x => x.CancelNotificationAsync(id), Times.Once);
    }

    [Fact]
    public async Task CancelAllNotificationsAsync_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;

        // Act
        await service.CancelAllNotificationsAsync();

        // Assert
        mock.Verify(x => x.CancelAllNotificationsAsync(), Times.Once);
    }

    [Fact]
    public async Task CancelAllNotificationsAsync_CalledMultipleTimes_CallsMethodEachTime()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;

        // Act
        await service.CancelAllNotificationsAsync();
        await service.CancelAllNotificationsAsync();
        await service.CancelAllNotificationsAsync();

        // Assert
        mock.Verify(x => x.CancelAllNotificationsAsync(), Times.Exactly(3));
    }

    [Fact]
    public async Task ShowNotificationAsync_WithAllParameters_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 1;
        var title = "Test Title";
        var message = "Test Message";
        var soundFileName = "notification.mp3";

        // Act
        await service.ShowNotificationAsync(id, title, message, soundFileName);

        // Assert
        mock.Verify(x => x.ShowNotificationAsync(id, title, message, soundFileName), Times.Once);
    }

    [Fact]
    public async Task ShowNotificationAsync_WithoutSoundFileName_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 2;
        var title = "Alert";
        var message = "Important message";

        // Act
        await service.ShowNotificationAsync(id, title, message);

        // Assert
        mock.Verify(x => x.ShowNotificationAsync(id, title, message, null), Times.Once);
    }

    [Fact]
    public async Task ShowNotificationAsync_WithNullSoundFileName_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 3;
        var title = "Notification";
        var message = "Silent notification";

        // Act
        await service.ShowNotificationAsync(id, title, message, null);

        // Assert
        mock.Verify(x => x.ShowNotificationAsync(id, title, message, null), Times.Once);
    }

    [Fact]
    public async Task ShowNotificationAsync_WithEmptyStrings_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 4;
        var title = string.Empty;
        var message = string.Empty;

        // Act
        await service.ShowNotificationAsync(id, title, message);

        // Assert
        mock.Verify(x => x.ShowNotificationAsync(id, title, message, null), Times.Once);
    }

    [Fact]
    public async Task ShowNotificationAsync_WithZeroId_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = 0;
        var title = "Test";
        var message = "Test";

        // Act
        await service.ShowNotificationAsync(id, title, message);

        // Assert
        mock.Verify(x => x.ShowNotificationAsync(id, title, message, null), Times.Once);
    }

    [Fact]
    public async Task ShowNotificationAsync_WithNegativeId_CallsMethod()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        var service = mock.Object;
        var id = -1;
        var title = "Test";
        var message = "Test";

        // Act
        await service.ShowNotificationAsync(id, title, message);

        // Assert
        mock.Verify(x => x.ShowNotificationAsync(id, title, message, null), Times.Once);
    }

    [Fact]
    public async Task GetPendingNotificationCountAsync_WhenZero_ReturnsZero()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        mock.Setup(x => x.GetPendingNotificationCountAsync()).ReturnsAsync(0);
        var service = mock.Object;

        // Act
        var result = await service.GetPendingNotificationCountAsync();

        // Assert
        Assert.Equal(0, result);
        mock.Verify(x => x.GetPendingNotificationCountAsync(), Times.Once);
    }

    [Fact]
    public async Task GetPendingNotificationCountAsync_WhenPositive_ReturnsCount()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        mock.Setup(x => x.GetPendingNotificationCountAsync()).ReturnsAsync(5);
        var service = mock.Object;

        // Act
        var result = await service.GetPendingNotificationCountAsync();

        // Assert
        Assert.Equal(5, result);
        mock.Verify(x => x.GetPendingNotificationCountAsync(), Times.Once);
    }

    [Fact]
    public async Task GetPendingNotificationCountAsync_WhenNegative_ReturnsNegativeCount()
    {
        // Arrange
        var mock = new Mock<INotificationService>();
        mock.Setup(x => x.GetPendingNotificationCountAsync()).ReturnsAsync(-1);
        var service = mock.Object;

        // Act
        var result = await service.GetPendingNotificationCountAsync();

        // Assert
        Assert.Equal(-1, result);
        mock.Verify(x => x.GetPendingNotificationCountAsync(), Times.Once);
    }
}
