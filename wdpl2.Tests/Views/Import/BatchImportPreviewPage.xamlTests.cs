using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using Wdpl2.Views;
using Xunit;

namespace wdpl2.Tests;

/// <summary>
/// Tests for BatchImportPreviewPage — batch import preview functionality.
/// 
/// Testing Notes:
/// - PlayerFrameRecord.GetFrameKey: Fully tested via reflection (private nested class)
/// - ImportResult.Summary: Fully tested via reflection (private nested class)
/// - BatchImportPreviewPage Constructor: Cannot be fully unit tested due to MAUI dependencies
///   (InitializeComponent requires XAML runtime). Method existence and signature verified.
/// - LoadBatchPreviewAsync: Cannot be fully unit tested due to MAUI dependencies (requires
///   UI controls, Navigation, DisplayAlert). Method existence and signature verified.
/// 
/// Full testing of the constructor and LoadBatchPreviewAsync would require MAUI integration
/// tests with a test host and mocked UI components.
/// </summary>
public class BatchImportPreviewPageXamlTests
{
    #region PlayerFrameRecord.GetFrameKey Tests
    
    [Fact]
    public void PlayerFrameRecord_GetFrameKey_SortsNamesAlphabetically()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record = Activator.CreateInstance(recordType);
        Assert.NotNull(record);
        
        var date = new DateTime(2024, 1, 15);
        recordType.GetProperty("Date")!.SetValue(record, date);
        recordType.GetProperty("PlayerName")!.SetValue(record, "Smith");
        recordType.GetProperty("OpponentName")!.SetValue(record, "Jones");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result = method.Invoke(record, null) as string;
        
        // Assert
        Assert.Equal("20240115|jones|smith", result);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_ReversedNames_ReturnsSameKey()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record1 = Activator.CreateInstance(recordType);
        var record2 = Activator.CreateInstance(recordType);
        Assert.NotNull(record1);
        Assert.NotNull(record2);
        
        var date = new DateTime(2024, 1, 15);
        recordType.GetProperty("Date")!.SetValue(record1, date);
        recordType.GetProperty("PlayerName")!.SetValue(record1, "Smith");
        recordType.GetProperty("OpponentName")!.SetValue(record1, "Jones");
        
        recordType.GetProperty("Date")!.SetValue(record2, date);
        recordType.GetProperty("PlayerName")!.SetValue(record2, "Jones");
        recordType.GetProperty("OpponentName")!.SetValue(record2, "Smith");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result1 = method.Invoke(record1, null) as string;
        var result2 = method.Invoke(record2, null) as string;
        
        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal("20240115|jones|smith", result1);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_CaseInsensitive()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record1 = Activator.CreateInstance(recordType);
        var record2 = Activator.CreateInstance(recordType);
        Assert.NotNull(record1);
        Assert.NotNull(record2);
        
        var date = new DateTime(2024, 3, 20);
        recordType.GetProperty("Date")!.SetValue(record1, date);
        recordType.GetProperty("PlayerName")!.SetValue(record1, "SMITH");
        recordType.GetProperty("OpponentName")!.SetValue(record1, "JONES");
        
        recordType.GetProperty("Date")!.SetValue(record2, date);
        recordType.GetProperty("PlayerName")!.SetValue(record2, "smith");
        recordType.GetProperty("OpponentName")!.SetValue(record2, "jones");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result1 = method.Invoke(record1, null) as string;
        var result2 = method.Invoke(record2, null) as string;
        
        // Assert
        Assert.Equal(result1, result2);
        Assert.Equal("20240320|jones|smith", result1);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_DifferentDates_DifferentKeys()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record1 = Activator.CreateInstance(recordType);
        var record2 = Activator.CreateInstance(recordType);
        Assert.NotNull(record1);
        Assert.NotNull(record2);
        
        recordType.GetProperty("Date")!.SetValue(record1, new DateTime(2024, 1, 15));
        recordType.GetProperty("PlayerName")!.SetValue(record1, "Smith");
        recordType.GetProperty("OpponentName")!.SetValue(record1, "Jones");
        
        recordType.GetProperty("Date")!.SetValue(record2, new DateTime(2024, 1, 16));
        recordType.GetProperty("PlayerName")!.SetValue(record2, "Smith");
        recordType.GetProperty("OpponentName")!.SetValue(record2, "Jones");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result1 = method.Invoke(record1, null) as string;
        var result2 = method.Invoke(record2, null) as string;
        
        // Assert
        Assert.NotEqual(result1, result2);
        Assert.Equal("20240115|jones|smith", result1);
        Assert.Equal("20240116|jones|smith", result2);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_EmptyNames_ReturnsValidKey()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record = Activator.CreateInstance(recordType);
        Assert.NotNull(record);
        
        var date = new DateTime(2024, 12, 25);
        recordType.GetProperty("Date")!.SetValue(record, date);
        recordType.GetProperty("PlayerName")!.SetValue(record, "");
        recordType.GetProperty("OpponentName")!.SetValue(record, "");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result = method.Invoke(record, null) as string;
        
        // Assert
        Assert.Equal("20241225||", result);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_SpecialCharacters_PreservesInKey()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record = Activator.CreateInstance(recordType);
        Assert.NotNull(record);
        
        var date = new DateTime(2024, 6, 10);
        recordType.GetProperty("Date")!.SetValue(record, date);
        recordType.GetProperty("PlayerName")!.SetValue(record, "O'Brien");
        recordType.GetProperty("OpponentName")!.SetValue(record, "Smith-Jones");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result = method.Invoke(record, null) as string;
        
        // Assert
        Assert.Equal("20240610|o'brien|smith-jones", result);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_SameNames_ReturnsValidKey()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record = Activator.CreateInstance(recordType);
        Assert.NotNull(record);
        
        var date = new DateTime(2024, 7, 4);
        recordType.GetProperty("Date")!.SetValue(record, date);
        recordType.GetProperty("PlayerName")!.SetValue(record, "Smith");
        recordType.GetProperty("OpponentName")!.SetValue(record, "Smith");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result = method.Invoke(record, null) as string;
        
        // Assert
        Assert.Equal("20240704|smith|smith", result);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_WhitespaceInNames_PreservesWhitespace()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record = Activator.CreateInstance(recordType);
        Assert.NotNull(record);
        
        var date = new DateTime(2024, 8, 20);
        recordType.GetProperty("Date")!.SetValue(record, date);
        recordType.GetProperty("PlayerName")!.SetValue(record, "John Smith");
        recordType.GetProperty("OpponentName")!.SetValue(record, "Jane Doe");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result = method.Invoke(record, null) as string;
        
        // Assert
        Assert.Equal("20240820|jane doe|john smith", result);
    }

    [Fact]
    public void PlayerFrameRecord_GetFrameKey_MixedCaseNames_NormalizesToLowercase()
    {
        // Arrange
        var recordType = typeof(BatchImportPreviewPage).GetNestedType("PlayerFrameRecord", BindingFlags.NonPublic);
        Assert.NotNull(recordType);
        
        var record = Activator.CreateInstance(recordType);
        Assert.NotNull(record);
        
        var date = new DateTime(2024, 9, 15);
        recordType.GetProperty("Date")!.SetValue(record, date);
        recordType.GetProperty("PlayerName")!.SetValue(record, "McDonald");
        recordType.GetProperty("OpponentName")!.SetValue(record, "O'Neill");
        
        // Act
        var method = recordType.GetMethod("GetFrameKey");
        Assert.NotNull(method);
        var result = method.Invoke(record, null) as string;
        
        // Assert
        Assert.Equal("20240915|mcdonald|o'neill", result);
    }

    #endregion

    #region ImportResult.Summary Tests

    [Fact]
    public void ImportResult_Summary_AllZeros_ReturnsFormattedString()
    {
        // Arrange
        var resultType = typeof(BatchImportPreviewPage).GetNestedType("ImportResult", BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        
        var result = Activator.CreateInstance(resultType);
        Assert.NotNull(result);
        
        // Act
        var summaryProperty = resultType.GetProperty("Summary");
        Assert.NotNull(summaryProperty);
        var summary = summaryProperty.GetValue(result) as string;
        
        // Assert
        Assert.NotNull(summary);
        Assert.Contains("Created:", summary);
        Assert.Contains("• 0 divisions", summary);
        Assert.Contains("• 0 teams", summary);
        Assert.Contains("• 0 players", summary);
        Assert.Contains("• 0 fixtures", summary);
        Assert.Contains("• 0 player frame results", summary);
        Assert.Contains("• 0 doubles pairings", summary);
        Assert.Contains("Skipped (already exist):", summary);
    }

    [Fact]
    public void ImportResult_Summary_WithCreatedValues_FormatsCorrectly()
    {
        // Arrange
        var resultType = typeof(BatchImportPreviewPage).GetNestedType("ImportResult", BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        
        var result = Activator.CreateInstance(resultType);
        Assert.NotNull(result);
        
        resultType.GetProperty("DivisionsCreated")!.SetValue(result, 2);
        resultType.GetProperty("TeamsCreated")!.SetValue(result, 10);
        resultType.GetProperty("PlayersCreated")!.SetValue(result, 50);
        resultType.GetProperty("FixturesCreated")!.SetValue(result, 15);
        resultType.GetProperty("FramesCreated")!.SetValue(result, 100);
        resultType.GetProperty("DoublesPairingsCreated")!.SetValue(result, 25);
        
        // Act
        var summaryProperty = resultType.GetProperty("Summary");
        Assert.NotNull(summaryProperty);
        var summary = summaryProperty.GetValue(result) as string;
        
        // Assert
        Assert.NotNull(summary);
        Assert.Contains("• 2 divisions", summary);
        Assert.Contains("• 10 teams", summary);
        Assert.Contains("• 50 players", summary);
        Assert.Contains("• 15 fixtures", summary);
        Assert.Contains("• 100 player frame results", summary);
        Assert.Contains("• 25 doubles pairings", summary);
    }

    [Fact]
    public void ImportResult_Summary_WithSkippedValues_FormatsCorrectly()
    {
        // Arrange
        var resultType = typeof(BatchImportPreviewPage).GetNestedType("ImportResult", BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        
        var result = Activator.CreateInstance(resultType);
        Assert.NotNull(result);
        
        resultType.GetProperty("DivisionsSkipped")!.SetValue(result, 1);
        resultType.GetProperty("TeamsSkipped")!.SetValue(result, 5);
        resultType.GetProperty("PlayersSkipped")!.SetValue(result, 20);
        resultType.GetProperty("FixturesSkipped")!.SetValue(result, 8);
        resultType.GetProperty("FramesSkipped")!.SetValue(result, 30);
        resultType.GetProperty("DoublesPairingsSkipped")!.SetValue(result, 12);
        
        // Act
        var summaryProperty = resultType.GetProperty("Summary");
        Assert.NotNull(summaryProperty);
        var summary = summaryProperty.GetValue(result) as string;
        
        // Assert
        Assert.NotNull(summary);
        Assert.Contains("• 1 divisions", summary);
        Assert.Contains("• 5 teams", summary);
        Assert.Contains("• 20 players", summary);
        Assert.Contains("• 8 fixtures", summary);
        Assert.Contains("• 30 frames", summary);
        Assert.Contains("• 12 doubles pairings", summary);
    }

    [Fact]
    public void ImportResult_Summary_MixedValues_ContainsAllSections()
    {
        // Arrange
        var resultType = typeof(BatchImportPreviewPage).GetNestedType("ImportResult", BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        
        var result = Activator.CreateInstance(resultType);
        Assert.NotNull(result);
        
        resultType.GetProperty("DivisionsCreated")!.SetValue(result, 3);
        resultType.GetProperty("DivisionsSkipped")!.SetValue(result, 1);
        resultType.GetProperty("TeamsCreated")!.SetValue(result, 12);
        resultType.GetProperty("TeamsSkipped")!.SetValue(result, 4);
        resultType.GetProperty("PlayersCreated")!.SetValue(result, 60);
        resultType.GetProperty("PlayersSkipped")!.SetValue(result, 15);
        
        // Act
        var summaryProperty = resultType.GetProperty("Summary");
        Assert.NotNull(summaryProperty);
        var summary = summaryProperty.GetValue(result) as string;
        
        // Assert
        Assert.NotNull(summary);
        Assert.Contains("Created:", summary);
        Assert.Contains("Skipped (already exist):", summary);
        Assert.Contains("• 3 divisions", summary);
        Assert.Contains("• 1 divisions", summary);
        Assert.Contains("• 12 teams", summary);
        Assert.Contains("• 4 teams", summary);
    }

    [Fact]
    public void ImportResult_Summary_LargeNumbers_FormatsCorrectly()
    {
        // Arrange
        var resultType = typeof(BatchImportPreviewPage).GetNestedType("ImportResult", BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        
        var result = Activator.CreateInstance(resultType);
        Assert.NotNull(result);
        
        resultType.GetProperty("DivisionsCreated")!.SetValue(result, 999);
        resultType.GetProperty("TeamsCreated")!.SetValue(result, 1000);
        resultType.GetProperty("PlayersCreated")!.SetValue(result, 5000);
        resultType.GetProperty("FixturesCreated")!.SetValue(result, 2500);
        resultType.GetProperty("FramesCreated")!.SetValue(result, 10000);
        resultType.GetProperty("DoublesPairingsCreated")!.SetValue(result, 3000);
        
        // Act
        var summaryProperty = resultType.GetProperty("Summary");
        Assert.NotNull(summaryProperty);
        var summary = summaryProperty.GetValue(result) as string;
        
        // Assert
        Assert.NotNull(summary);
        Assert.Contains("• 999 divisions", summary);
        Assert.Contains("• 1000 teams", summary);
        Assert.Contains("• 5000 players", summary);
        Assert.Contains("• 2500 fixtures", summary);
        Assert.Contains("• 10000 player frame results", summary);
        Assert.Contains("• 3000 doubles pairings", summary);
    }

    [Fact]
    public void ImportResult_Summary_ContainsNewlineCharacters()
    {
        // Arrange
        var resultType = typeof(BatchImportPreviewPage).GetNestedType("ImportResult", BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        
        var result = Activator.CreateInstance(resultType);
        Assert.NotNull(result);
        
        // Act
        var summaryProperty = resultType.GetProperty("Summary");
        Assert.NotNull(summaryProperty);
        var summary = summaryProperty.GetValue(result) as string;
        
        // Assert
        Assert.NotNull(summary);
        Assert.Contains("\n", summary);
        var lines = summary.Split('\n');
        Assert.True(lines.Length > 10);
    }

    [Fact]
    public void ImportResult_Summary_ContainsBulletPoints()
    {
        // Arrange
        var resultType = typeof(BatchImportPreviewPage).GetNestedType("ImportResult", BindingFlags.NonPublic);
        Assert.NotNull(resultType);
        
        var result = Activator.CreateInstance(resultType);
        Assert.NotNull(result);
        
        // Act
        var summaryProperty = resultType.GetProperty("Summary");
        Assert.NotNull(summaryProperty);
        var summary = summaryProperty.GetValue(result) as string;
        
        // Assert
        Assert.NotNull(summary);
        var bulletCount = summary.Split('•').Length - 1;
        Assert.Equal(12, bulletCount); // 6 created + 6 skipped
    }

    #endregion

    #region BatchImportPreviewPage Constructor Tests

    // Note: The BatchImportPreviewPage constructor cannot be fully tested in a unit test
    // because it calls InitializeComponent() which requires XAML compilation and MAUI runtime.
    // These tests verify the existence and signature of the constructor but cannot execute it.
    // Full testing would require MAUI integration tests.

    [Fact]
    public void Constructor_Exists()
    {
        // Arrange & Act
        var constructor = typeof(BatchImportPreviewPage).GetConstructor(Type.EmptyTypes);
        
        // Assert
        Assert.NotNull(constructor);
        Assert.True(constructor.IsPublic);
    }

    #endregion

    #region LoadBatchPreviewAsync Tests

    // Note: LoadBatchPreviewAsync cannot be fully tested in a unit test because it:
    // - Requires UI controls (ImportButton, LoadingBorder, etc.) to be initialized
    // - Calls DisplayAlert and Navigation.PopAsync which require MAUI runtime
    // - Calls private methods (LoadSeasons, ProcessFileAndAggregateAsync, etc.)
    // These tests verify method existence and signature but cannot execute the full method.
    // Full testing would require MAUI integration tests with mocked UI components.

    [Fact]
    public void LoadBatchPreviewAsync_MethodExists()
    {
        // Arrange & Act
        var method = typeof(BatchImportPreviewPage).GetMethod("LoadBatchPreviewAsync");
        
        // Assert
        Assert.NotNull(method);
        Assert.True(method.IsPublic);
        Assert.Equal(typeof(Task), method.ReturnType);
        
        var parameters = method.GetParameters();
        Assert.Single(parameters);
        Assert.Equal("filePaths", parameters[0].Name);
        Assert.Equal(typeof(List<string>), parameters[0].ParameterType);
    }

    #endregion
}
