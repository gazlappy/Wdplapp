using System;
using System.IO;
using System.Threading.Tasks;
using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ImageOptimizationService — image loading, optimization, and MIME type detection.
/// </summary>
public class ImageOptimizationServiceTests
{
    [Fact]
    public void ToDataUrl_WithPngBytes_ReturnsCorrectDataUrl()
    {
        // Arrange
        var service = new ImageOptimizationService();
        var imageData = new byte[] { 0x89, 0x50, 0x4E, 0x47 }; // PNG header bytes

        // Act
        var result = service.ToDataUrl(imageData, "image/png");

        // Assert
        Assert.Equal("data:image/png;base64,iVBORw==", result);
    }

    [Fact]
    public void ToDataUrl_WithDefaultMimeType_ReturnsPngDataUrl()
    {
        // Arrange
        var service = new ImageOptimizationService();
        var imageData = new byte[] { 0x01, 0x02, 0x03 };

        // Act
        var result = service.ToDataUrl(imageData);

        // Assert
        Assert.StartsWith("data:image/png;base64,", result);
        Assert.Equal("data:image/png;base64,AQID", result);
    }

    [Fact]
    public void ToDataUrl_WithJpegMimeType_ReturnsJpegDataUrl()
    {
        // Arrange
        var service = new ImageOptimizationService();
        var imageData = new byte[] { 0xFF, 0xD8, 0xFF };

        // Act
        var result = service.ToDataUrl(imageData, "image/jpeg");

        // Assert
        Assert.Equal("data:image/jpeg;base64,/9j/", result);
    }

    [Fact]
    public void GetMimeType_WithJpgExtension_ReturnsImageJpeg()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("photo.jpg");

        // Assert
        Assert.Equal("image/jpeg", result);
    }

    [Fact]
    public void GetMimeType_WithJpegExtension_ReturnsImageJpeg()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("photo.jpeg");

        // Assert
        Assert.Equal("image/jpeg", result);
    }

    [Fact]
    public void GetMimeType_WithPngExtension_ReturnsImagePng()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("photo.png");

        // Assert
        Assert.Equal("image/png", result);
    }

    [Fact]
    public void GetMimeType_WithGifExtension_ReturnsImageGif()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("animation.gif");

        // Assert
        Assert.Equal("image/gif", result);
    }

    [Fact]
    public void GetMimeType_WithBmpExtension_ReturnsImageBmp()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("image.bmp");

        // Assert
        Assert.Equal("image/bmp", result);
    }

    [Fact]
    public void GetMimeType_WithWebpExtension_ReturnsImageWebp()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("modern.webp");

        // Assert
        Assert.Equal("image/webp", result);
    }

    [Fact]
    public void GetMimeType_WithSvgExtension_ReturnsImageSvgXml()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("vector.svg");

        // Assert
        Assert.Equal("image/svg+xml", result);
    }

    [Fact]
    public void GetMimeType_WithUnknownExtension_ReturnsDefaultPng()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("file.txt");

        // Assert
        Assert.Equal("image/png", result);
    }

    [Fact]
    public void GetMimeType_WithUppercaseExtension_ReturnsCorrectMimeType()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("PHOTO.JPG");

        // Assert
        Assert.Equal("image/jpeg", result);
    }

    [Fact]
    public void GetMimeType_WithNoExtension_ReturnsDefaultPng()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.GetMimeType("filenoext");

        // Assert
        Assert.Equal("image/png", result);
    }

    [Fact]
    public void IsValidImageFile_WithJpgExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("photo.jpg");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithJpegExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("photo.jpeg");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithPngExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("photo.png");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithGifExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("animation.gif");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithBmpExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("image.bmp");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithWebpExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("modern.webp");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithSvgExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("vector.svg");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithInvalidExtension_ReturnsFalse()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("document.txt");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void IsValidImageFile_WithUppercaseExtension_ReturnsTrue()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("PHOTO.PNG");

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void IsValidImageFile_WithNoExtension_ReturnsFalse()
    {
        // Arrange
        var service = new ImageOptimizationService();

        // Act
        var result = service.IsValidImageFile("filenoext");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public async Task GetImageDimensionsAsync_WithAnyBytes_ReturnsDefaultDimensions()
    {
        // Arrange
        var service = new ImageOptimizationService();
        var imageData = new byte[] { 0x01, 0x02, 0x03, 0x04 };

        // Act
        var result = await service.GetImageDimensionsAsync(imageData);

        // Assert
        Assert.Equal(800, result.width);
        Assert.Equal(600, result.height);
    }

    [Fact]
    public async Task LoadAndOptimizeImageAsync_WithValidFile_ReturnsDataAndBase64()
    {
        // Arrange
        var service = new ImageOptimizationService();
        var tempFile = Path.GetTempFileName();
        var testData = new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A };
        await File.WriteAllBytesAsync(tempFile, testData);

        try
        {
            // Act
            var result = await service.LoadAndOptimizeImageAsync(tempFile);

            // Assert
            Assert.Equal(testData, result.data);
            Assert.Equal(Convert.ToBase64String(testData), result.base64);
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAndOptimizeImageAsync_WithCustomMaxDimensions_ReturnsDataAndBase64()
    {
        // Arrange
        var service = new ImageOptimizationService();
        var tempFile = Path.GetTempFileName();
        var testData = new byte[] { 0xFF, 0xD8, 0xFF };
        await File.WriteAllBytesAsync(tempFile, testData);

        try
        {
            // Act
            var result = await service.LoadAndOptimizeImageAsync(tempFile, 800, 600);

            // Assert
            Assert.Equal(testData, result.data);
            Assert.Equal(Convert.ToBase64String(testData), result.base64);
        }
        finally
        {
            // Cleanup
            File.Delete(tempFile);
        }
    }

    [Fact]
    public async Task LoadAndOptimizeImageAsync_WithNonExistentFile_ThrowsException()
    {
        // Arrange
        var service = new ImageOptimizationService();
        var nonExistentFile = Path.Combine(Path.GetTempPath(), $"nonexistent_{Guid.NewGuid()}.png");

        // Act & Assert
        await Assert.ThrowsAsync<FileNotFoundException>(async () =>
            await service.LoadAndOptimizeImageAsync(nonExistentFile));
    }
}
