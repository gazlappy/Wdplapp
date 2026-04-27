using Wdpl2.Services;

namespace wdpl2.Tests;

/// <summary>
/// Tests for ResponsiveHelper — device category detection and responsive utilities.
/// Note: ApplyResponsiveLayout cannot be tested in unit tests as it requires MAUI Grid 
/// instances which need a dispatcher/UI thread context. These would require MAUI UI tests.
/// </summary>
public class ResponsiveHelperTests
{
    [Fact]
    public void GetDeviceCategory_ReturnsValidCategory()
    {
        // Act
        var category = ResponsiveHelper.GetDeviceCategory();
        
        // Assert
        Assert.True(category is DeviceCategory.Phone or DeviceCategory.Tablet or DeviceCategory.Desktop);
    }

    [Fact]
    public void IsPhone_ReturnsBoolean()
    {
        // Act
        var isPhone = ResponsiveHelper.IsPhone;
        
        // Assert - verify it matches GetDeviceCategory
        Assert.Equal(ResponsiveHelper.GetDeviceCategory() == DeviceCategory.Phone, isPhone);
    }

    [Fact]
    public void IsTablet_ReturnsBoolean()
    {
        // Act
        var isTablet = ResponsiveHelper.IsTablet;
        
        // Assert - verify it matches GetDeviceCategory
        Assert.Equal(ResponsiveHelper.GetDeviceCategory() == DeviceCategory.Tablet, isTablet);
    }

    [Fact]
    public void IsDesktop_ReturnsBoolean()
    {
        // Act
        var isDesktop = ResponsiveHelper.IsDesktop;
        
        // Assert - verify it matches GetDeviceCategory
        Assert.Equal(ResponsiveHelper.GetDeviceCategory() == DeviceCategory.Desktop, isDesktop);
    }

    [Fact]
    public void GetScreenWidthInDp_ConsistentWithDeviceCategory()
    {
        // Act
        var width = ResponsiveHelper.GetScreenWidthInDp();
        var category = ResponsiveHelper.GetDeviceCategory();
        
        // Assert - verify category matches width boundaries
        if (width < 600)
        {
            Assert.Equal(DeviceCategory.Phone, category);
        }
        else if (width < 900)
        {
            Assert.Equal(DeviceCategory.Tablet, category);
        }
        else
        {
            Assert.Equal(DeviceCategory.Desktop, category);
        }
    }

    [Fact]
    public void GetScreenHeightInDp_ReturnsValue()
    {
        // Act
        var height = ResponsiveHelper.GetScreenHeightInDp();
        
        // Assert - method executes and returns a double value
        Assert.IsType<double>(height);
    }

    [Fact]
    public void GetButtonStyleKey_WithDefaultParameter_ReturnsAppropriateKey()
    {
        // Act
        var result = ResponsiveHelper.GetButtonStyleKey();
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result == "PrimaryButtonStyle" || result == "MobilePrimaryButtonStyle");
    }

    [Fact]
    public void GetButtonStyleKey_WithCustomBaseKey_ReturnsAppropriateKey()
    {
        // Arrange
        var customKey = "SecondaryButtonStyle";
        
        // Act
        var result = ResponsiveHelper.GetButtonStyleKey(customKey);
        
        // Assert
        Assert.NotNull(result);
        Assert.True(result == customKey || result == $"Mobile{customKey}");
    }

    [Theory]
    [InlineData(SpacingSize.Small)]
    [InlineData(SpacingSize.Standard)]
    [InlineData(SpacingSize.Medium)]
    [InlineData(SpacingSize.Large)]
    [InlineData(SpacingSize.ExtraLarge)]
    public void GetSpacing_WithValidSize_ReturnsPositiveValue(SpacingSize size)
    {
        // Act
        var spacing = ResponsiveHelper.GetSpacing(size);
        
        // Assert
        Assert.True(spacing > 0);
    }

    [Fact]
    public void GetSpacing_WithDefaultParameter_ReturnsStandardSpacing()
    {
        // Act
        var spacing = ResponsiveHelper.GetSpacing();
        
        // Assert
        Assert.True(spacing == 12 || spacing == 8);
    }

    [Theory]
    [InlineData((SpacingSize)999)]
    public void GetSpacing_WithInvalidSize_ReturnsDefaultValue(SpacingSize size)
    {
        // Act
        var spacing = ResponsiveHelper.GetSpacing(size);
        
        // Assert
        Assert.True(spacing == 12 || spacing == 8);
    }

    [Theory]
    [InlineData(PaddingSize.Small)]
    [InlineData(PaddingSize.Standard)]
    [InlineData(PaddingSize.Medium)]
    [InlineData(PaddingSize.Large)]
    [InlineData(PaddingSize.ExtraLarge)]
    public void GetPadding_WithValidSize_ReturnsThickness(PaddingSize size)
    {
        // Act
        var padding = ResponsiveHelper.GetPadding(size);
        
        // Assert
        Assert.IsType<Thickness>(padding);
        Assert.True(padding.Left > 0);
    }

    [Fact]
    public void GetPadding_WithDefaultParameter_ReturnsStandardPadding()
    {
        // Act
        var padding = ResponsiveHelper.GetPadding();
        
        // Assert
        Assert.IsType<Thickness>(padding);
        Assert.True(padding.Left == 12 || padding.Left == 8);
    }

    [Theory]
    [InlineData((PaddingSize)999)]
    public void GetPadding_WithInvalidSize_ReturnsDefaultThickness(PaddingSize size)
    {
        // Act
        var padding = ResponsiveHelper.GetPadding(size);
        
        // Assert
        Assert.IsType<Thickness>(padding);
        Assert.True(padding.Left == 12 || padding.Left == 8);
    }

    [Theory]
    [InlineData(FontSizeCategory.Caption)]
    [InlineData(FontSizeCategory.Body)]
    [InlineData(FontSizeCategory.Subheadline)]
    [InlineData(FontSizeCategory.Headline)]
    [InlineData(FontSizeCategory.Title)]
    [InlineData(FontSizeCategory.LargeTitle)]
    public void GetFontSize_WithValidCategory_ReturnsPositiveValue(FontSizeCategory category)
    {
        // Act
        var fontSize = ResponsiveHelper.GetFontSize(category);
        
        // Assert
        Assert.True(fontSize > 0);
    }

    [Theory]
    [InlineData((FontSizeCategory)999)]
    public void GetFontSize_WithInvalidCategory_ReturnsDefaultValue(FontSizeCategory category)
    {
        // Act
        var fontSize = ResponsiveHelper.GetFontSize(category);
        
        // Assert
        Assert.True(fontSize == 14 * 1.15 || fontSize == 14 * 1.0);
    }

    [Fact]
    public void GetMinimumTouchTarget_ReturnsValidAccessibleSize()
    {
        // Act
        var touchTarget = ResponsiveHelper.GetMinimumTouchTarget();
        
        // Assert - should return either 48 (phone) or 44 (tablet/desktop)
        Assert.True(touchTarget == 48 || touchTarget == 44);
    }

    [Fact]
    public void GetMinimumTouchTarget_ConsistentWithIsPhone()
    {
        // Act
        var touchTarget = ResponsiveHelper.GetMinimumTouchTarget();
        var isPhone = ResponsiveHelper.IsPhone;
        
        // Assert - value should match IsPhone property
        var expectedSize = isPhone ? 48 : 44;
        Assert.Equal(expectedSize, touchTarget);
    }
}
