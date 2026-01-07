using System.Diagnostics;
using System.Text.RegularExpressions;
using Plugin.Maui.OCR;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class TestPage : ContentPage
{
    private readonly IOcrService _localOcrService;
    private readonly AzureVisionOcrService _azureOcrService;
    private byte[]? _currentImageData;
    private string _currentOcrText = "";
    private OcrResult? _lastLocalOcrResult;
    private AzureOcrResult? _lastAzureOcrResult;
    private readonly Stopwatch _processingStopwatch = new();
    private bool _useAzureOcr = false;

    // Data models for the UI
    public class LineInfo
    {
        public int LineNumber { get; set; }
        public string Text { get; set; } = "";
        public int CharCount => Text.Length;
        public double Confidence { get; set; } = 1.0;
        public string ConfidenceText => Confidence < 1.0 ? $"{Confidence:P0}" : "";
        public Color ConfidenceColor => Confidence >= 0.9 ? Colors.Green : Confidence >= 0.7 ? Colors.Orange : Colors.Red;
    }

    public class WordConfidenceInfo
    {
        public string Text { get; set; } = "";
        public double Confidence { get; set; }
        public string ConfidencePercent => $"{Confidence:P0}";
        public Color ConfidenceColor => Confidence >= 0.9 ? Colors.Green : Confidence >= 0.7 ? Colors.Orange : Colors.Red;
        public Color BackgroundColor => Confidence >= 0.9 
            ? Color.FromArgb("#F0FDF4") 
            : Confidence >= 0.7 
                ? Color.FromArgb("#FEF3C7") 
                : Color.FromArgb("#FEF2F2");
    }

    public TestPage()
    {
        InitializeComponent();
        _localOcrService = OcrPlugin.Default;
        _azureOcrService = new AzureVisionOcrService();
        
        // Check OCR status on load
        Loaded += async (s, e) => await InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await InitializeLocalOcrAsync();
        UpdateAzureConfigUI();
    }

    private async Task InitializeLocalOcrAsync()
    {
        try
        {
            // Show platform info
            var platform = DeviceInfo.Platform.ToString();
            var ocrEngine = platform switch
            {
                "Android" => "ML Kit",
                "iOS" => "Vision Framework",
                "MacCatalyst" => "Vision Framework",
                "WinUI" => "Windows.Media.Ocr",
                _ => "Unknown"
            };
            
            PlatformLabel.Text = $"Platform: {platform} | Engine: {ocrEngine}";
            OcrStatusLabel.Text = "Initializing...";
            
            await _localOcrService.InitAsync();
            
            OcrStatusLabel.Text = "Ready";
            OcrStatusLabel.TextColor = Colors.Green;
            LocalStatusCard.Stroke = Color.FromArgb("#10B981");
            LocalStatusCard.BackgroundColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#1E3A1E" : "#F0FDF4");
        }
        catch (Exception ex)
        {
            OcrStatusLabel.Text = $"Error: {ex.Message}";
            OcrStatusLabel.TextColor = Colors.Red;
            LocalStatusCard.Stroke = Color.FromArgb("#EF4444");
            LocalStatusCard.BackgroundColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#3D1E1E" : "#FEF2F2");
        }
    }

    private void UpdateAzureConfigUI()
    {
        if (_azureOcrService.IsConfigured)
        {
            AzureStatusLabel.Text = "Configured and ready";
            AzureStatusLabel.TextColor = Colors.Green;
            // Don't show keys in UI for security
            AzureEndpointEntry.Text = Preferences.Get("AzureVision_Endpoint", "");
            AzureApiKeyEntry.Text = "••••••••••••••••";
        }
        else
        {
            AzureStatusLabel.Text = "Not configured - enter credentials below";
            AzureStatusLabel.TextColor = Colors.Orange;
        }
    }

    private void OnOcrEngineChanged(object sender, CheckedChangedEventArgs e)
    {
        if (!e.Value) return;
        
        _useAzureOcr = AzureOcrRadio.IsChecked;
        
        // Show/hide Azure config
        AzureConfigCard.IsVisible = _useAzureOcr;
        LocalStatusCard.IsVisible = !_useAzureOcr;
        
        // Update UI state
        if (_useAzureOcr && !_azureOcrService.IsConfigured)
        {
            ProcessButton.IsEnabled = false;
        }
        else if (_currentImageData != null)
        {
            ProcessButton.IsEnabled = true;
        }
    }

    private void OnSaveAzureConfig(object sender, EventArgs e)
    {
        var endpoint = AzureEndpointEntry.Text?.Trim() ?? "";
        var apiKey = AzureApiKeyEntry.Text?.Trim() ?? "";
        
        // Don't save if showing masked key
        if (apiKey == "••••••••••••••••")
        {
            DisplayAlert("Info", "API key unchanged. Enter a new key to update.", "OK");
            return;
        }
        
        if (string.IsNullOrEmpty(endpoint) || string.IsNullOrEmpty(apiKey))
        {
            DisplayAlert("Missing Information", "Please enter both endpoint and API key.", "OK");
            return;
        }
        
        _azureOcrService.Configure(endpoint, apiKey);
        UpdateAzureConfigUI();
        
        if (_currentImageData != null)
        {
            ProcessButton.IsEnabled = true;
        }
        
        DisplayAlert("Saved", "Azure Vision configuration saved.", "OK");
    }

    private async void OnTestAzureConnection(object sender, EventArgs e)
    {
        // Save first if there's new input
        var endpoint = AzureEndpointEntry.Text?.Trim() ?? "";
        var apiKey = AzureApiKeyEntry.Text?.Trim() ?? "";
        
        if (!string.IsNullOrEmpty(endpoint) && !string.IsNullOrEmpty(apiKey) && apiKey != "••••••••••••••••")
        {
            _azureOcrService.Configure(endpoint, apiKey);
        }
        
        if (!_azureOcrService.IsConfigured)
        {
            await DisplayAlert("Not Configured", "Please enter endpoint and API key first.", "OK");
            return;
        }
        
        AzureStatusLabel.Text = "Testing connection...";
        AzureStatusLabel.TextColor = Colors.Gray;
        
        var (success, message) = await _azureOcrService.TestConnectionAsync();
        
        AzureStatusLabel.Text = message;
        AzureStatusLabel.TextColor = success ? Colors.Green : Colors.Red;
        
        await DisplayAlert(success ? "Success" : "Failed", message, "OK");
    }

    private void OnClearAzureConfig(object sender, EventArgs e)
    {
        _azureOcrService.ClearConfiguration();
        AzureEndpointEntry.Text = "";
        AzureApiKeyEntry.Text = "";
        UpdateAzureConfigUI();
    }

    private async void OnAzurePortalTapped(object sender, EventArgs e)
    {
        try
        {
            await Launcher.OpenAsync("https://portal.azure.com/#create/Microsoft.CognitiveServicesComputerVision");
        }
        catch
        {
            await DisplayAlert("Info", "Visit portal.azure.com to create a Computer Vision resource.", "OK");
        }
    }

    private async void OnSelectImageClicked(object sender, EventArgs e)
    {
        try
        {
            var result = await MediaPicker.PickPhotoAsync(new MediaPickerOptions
            {
                Title = "Select an image for OCR"
            });

            if (result != null)
            {
                await LoadImageAsync(result);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to select image: {ex.Message}");
        }
    }

    private async void OnTakePhotoClicked(object sender, EventArgs e)
    {
        try
        {
            if (!MediaPicker.IsCaptureSupported)
            {
                await DisplayAlert("Not Supported", "Camera capture is not supported on this device.", "OK");
                return;
            }

            var result = await MediaPicker.CapturePhotoAsync(new MediaPickerOptions
            {
                Title = "Take a photo for OCR"
            });

            if (result != null)
            {
                await LoadImageAsync(result);
            }
        }
        catch (Exception ex)
        {
            ShowError($"Failed to take photo: {ex.Message}");
        }
    }

    private async Task LoadImageAsync(FileResult imageFile)
    {
        try
        {
            using var stream = await imageFile.OpenReadAsync();
            using var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);
            _currentImageData = memoryStream.ToArray();

            // Show image preview
            SelectedImage.Source = ImageSource.FromStream(() => new MemoryStream(_currentImageData));
            ImagePreviewBorder.IsVisible = true;
            PlaceholderBorder.IsVisible = false;
            
            // Show image info
            var fileSizeKb = _currentImageData.Length / 1024.0;
            ImageInfoLabel.Text = $"{fileSizeKb:F1} KB | {imageFile.FileName}";
            ImageSizeLabel.Text = $"Image: {fileSizeKb:F1} KB";
            
            // Enable processing if engine is ready
            ProcessButton.IsEnabled = !_useAzureOcr || _azureOcrService.IsConfigured;
        }
        catch (Exception ex)
        {
            ShowError($"Failed to load image: {ex.Message}");
        }
    }

    private async void OnProcessClicked(object sender, EventArgs e)
    {
        await ProcessOcrAsync();
    }

    private async Task ProcessOcrAsync()
    {
        if (_currentImageData == null)
        {
            ShowError("No image loaded. Please select or capture an image first.");
            return;
        }

        try
        {
            SetProcessingState(true, _useAzureOcr ? "Connecting to Azure..." : "Initializing OCR engine...");
            
            _processingStopwatch.Restart();
            _lastLocalOcrResult = null;
            _lastAzureOcrResult = null;

            if (_useAzureOcr)
            {
                await ProcessWithAzureAsync();
            }
            else
            {
                await ProcessWithLocalOcrAsync();
            }
        }
        catch (Exception ex)
        {
            ShowError($"OCR processing failed:\n\n{ex.Message}");
            Debug.WriteLine($"OCR Error: {ex}");
        }
        finally
        {
            SetProcessingState(false);
        }
    }

    private async Task ProcessWithLocalOcrAsync()
    {
        SetProcessingState(true, "Recognizing text...");
        
        await _localOcrService.InitAsync();
        
        var tryHard = TryHardSwitch.IsToggled;
        _lastLocalOcrResult = await _localOcrService.RecognizeTextAsync(_currentImageData!, tryHard);
        
        _processingStopwatch.Stop();
        
        if (_lastLocalOcrResult == null || !_lastLocalOcrResult.Success)
        {
            ShowError("OCR failed to extract text. Try a clearer image with better lighting.");
            return;
        }

        _currentOcrText = _lastLocalOcrResult.AllText ?? "";
        
        UpdateResults();
        
        StatsCard.IsVisible = true;
        ProcessingTimeLabel.Text = $"Time: {_processingStopwatch.ElapsedMilliseconds}ms";
        EngineUsedLabel.Text = "Engine: Local (Device)";
        ConfidenceLabel.Text = ""; // Local OCR doesn't provide per-word confidence
        
        OcrStatusLabel.Text = "Last OCR: Success";
        OcrStatusLabel.TextColor = Colors.Green;
    }

    private async Task ProcessWithAzureAsync()
    {
        if (!_azureOcrService.IsConfigured)
        {
            ShowError("Azure Vision is not configured. Please add your endpoint and API key.");
            return;
        }
        
        SetProcessingState(true, "Uploading to Azure...");
        
        _lastAzureOcrResult = await _azureOcrService.RecognizeTextAsync(_currentImageData!);
        
        _processingStopwatch.Stop();
        
        if (_lastAzureOcrResult == null || !_lastAzureOcrResult.Success)
        {
            ShowError($"Azure OCR failed: {_lastAzureOcrResult?.Error ?? "Unknown error"}");
            return;
        }

        _currentOcrText = _lastAzureOcrResult.AllText;
        
        UpdateResults();
        UpdateConfidenceTab();
        
        StatsCard.IsVisible = true;
        ProcessingTimeLabel.Text = $"Time: {_processingStopwatch.ElapsedMilliseconds}ms";
        EngineUsedLabel.Text = "Engine: Azure Vision (Cloud)";
        ConfidenceLabel.Text = $"Avg Confidence: {_lastAzureOcrResult.AverageConfidence:P0}";
        
        AzureStatusLabel.Text = "Last OCR: Success";
        AzureStatusLabel.TextColor = Colors.Green;
    }

    private void UpdateResults()
    {
        // Raw Text Tab
        RawTextLabel.Text = string.IsNullOrWhiteSpace(_currentOcrText) 
            ? "(No text detected)" 
            : _currentOcrText;
        RawTextLabel.TextColor = string.IsNullOrWhiteSpace(_currentOcrText)
            ? Color.FromArgb("#64748B")
            : Color.FromArgb("#E2E8F0");

        // Lines Tab
        UpdateLinesTab();
        
        // Words Tab
        UpdateWordsTab();
        
        // Analysis Tab
        UpdateAnalysisTab();
    }

    private void UpdateLinesTab()
    {
        if (!ParseLinesSwitch.IsToggled || string.IsNullOrWhiteSpace(_currentOcrText))
        {
            LinesCollection.ItemsSource = null;
            LineCountLabel.Text = "0 lines detected";
            return;
        }

        List<LineInfo> lines;

        if (_useAzureOcr && _lastAzureOcrResult != null)
        {
            // Use Azure's line data with confidence
            lines = _lastAzureOcrResult.Lines.Select((line, index) => new LineInfo
            {
                LineNumber = index + 1,
                Text = line.Text,
                Confidence = line.Words.Count > 0 ? line.Words.Average(w => w.Confidence) : 1.0
            }).ToList();
        }
        else
        {
            // Parse from raw text
            lines = _currentOcrText
                .Split('\n', StringSplitOptions.RemoveEmptyEntries)
                .Select((text, index) => new LineInfo 
                { 
                    LineNumber = index + 1, 
                    Text = text.Trim(),
                    Confidence = 1.0
                })
                .Where(l => !string.IsNullOrWhiteSpace(l.Text))
                .ToList();
        }

        LinesCollection.ItemsSource = lines;
        LineCountLabel.Text = $"{lines.Count} lines detected";
    }

    private void UpdateWordsTab()
    {
        WordsFlexLayout.Children.Clear();
        
        if (string.IsNullOrWhiteSpace(_currentOcrText))
        {
            WordCountLabel.Text = "0 unique words";
            return;
        }

        // Extract words and count frequency
        var words = Regex.Matches(_currentOcrText.ToLower(), @"\b[a-zA-Z]{2,}\b")
            .Select(m => m.Value)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(50)
            .ToList();

        WordCountLabel.Text = $"{words.Count} unique words (top 50)";

        foreach (var wordGroup in words)
        {
            var count = wordGroup.Count();
            var fontSize = Math.Min(10 + count * 2, 18);
            var opacity = Math.Min(0.5 + count * 0.1, 1.0);
            
            var label = new Label
            {
                Text = wordGroup.Key,
                FontSize = fontSize,
                Opacity = opacity,
                Padding = new Thickness(6, 3),
                Margin = new Thickness(2),
                BackgroundColor = Color.FromArgb(Application.Current?.RequestedTheme == AppTheme.Dark ? "#374151" : "#E5E7EB")
            };
            
            // Add tap to copy
            var tapGesture = new TapGestureRecognizer();
            tapGesture.Tapped += async (s, e) =>
            {
                await Clipboard.SetTextAsync(wordGroup.Key);
                await DisplayAlert("Copied", $"'{wordGroup.Key}' copied to clipboard", "OK");
            };
            label.GestureRecognizers.Add(tapGesture);
            
            WordsFlexLayout.Children.Add(label);
        }
    }

    private void UpdateAnalysisTab()
    {
        if (string.IsNullOrWhiteSpace(_currentOcrText))
        {
            StatCharacters.Text = "0";
            StatWords.Text = "0";
            StatLines.Text = "0";
            StatNumbers.Text = "0";
            return;
        }

        // Basic stats
        StatCharacters.Text = _currentOcrText.Length.ToString();
        
        var wordCount = Regex.Matches(_currentOcrText, @"\b\w+\b").Count;
        StatWords.Text = wordCount.ToString();
        
        var lineCount = _currentOcrText.Split('\n', StringSplitOptions.RemoveEmptyEntries).Length;
        StatLines.Text = lineCount.ToString();
        
        // Find numbers
        var numbers = Regex.Matches(_currentOcrText, @"\b\d+\.?\d*\b");
        StatNumbers.Text = numbers.Count.ToString();

        // Pattern detection
        UpdatePatternDetection();
        
        // Entity detection
        UpdateEntityDetection();
    }

    private void UpdateConfidenceTab()
    {
        if (_lastAzureOcrResult == null || !_useAzureOcr)
        {
            ConfidenceInfoLabel.Text = "Confidence data only available with Azure Vision";
            HighConfCount.Text = "0";
            MedConfCount.Text = "0";
            LowConfCount.Text = "0";
            LowConfWordsPanel.Children.Clear();
            LowConfWordsPanel.Children.Add(new Label { Text = "Use Azure Vision for confidence data", FontSize = 12, TextColor = Colors.Gray });
            ConfidenceWordsCollection.ItemsSource = null;
            return;
        }

        var words = _lastAzureOcrResult.Words;
        
        var highConf = words.Count(w => w.Confidence >= 0.9);
        var medConf = words.Count(w => w.Confidence >= 0.7 && w.Confidence < 0.9);
        var lowConf = words.Count(w => w.Confidence < 0.7);
        
        HighConfCount.Text = highConf.ToString();
        MedConfCount.Text = medConf.ToString();
        LowConfCount.Text = lowConf.ToString();
        
        ConfidenceInfoLabel.Text = $"Analyzed {words.Count} words - Average: {_lastAzureOcrResult.AverageConfidence:P0}";
        
        // Low confidence words
        LowConfWordsPanel.Children.Clear();
        var lowConfWords = words.Where(w => w.Confidence < 0.7).OrderBy(w => w.Confidence).Take(10).ToList();
        
        if (lowConfWords.Count == 0)
        {
            LowConfWordsPanel.Children.Add(new Label { Text = "No low confidence words - excellent!", FontSize = 12, TextColor = Colors.Green });
        }
        else
        {
            foreach (var word in lowConfWords)
            {
                LowConfWordsPanel.Children.Add(new Label 
                { 
                    Text = $"'{word.Text}' - {word.Confidence:P0}", 
                    FontSize = 12, 
                    TextColor = Colors.Red 
                });
            }
        }
        
        // All words with confidence
        var wordInfos = words.Select(w => new WordConfidenceInfo
        {
            Text = w.Text,
            Confidence = w.Confidence
        }).OrderBy(w => w.Confidence).ToList();
        
        ConfidenceWordsCollection.ItemsSource = wordInfos;
    }

    private void UpdatePatternDetection()
    {
        PatternsPanel.Children.Clear();
        
        var patterns = new List<(string Name, string Pattern, string Color)>
        {
            ("Email addresses", @"\b[A-Za-z0-9._%+-]+@[A-Za-z0-9.-]+\.[A-Z|a-z]{2,}\b", "#3B82F6"),
            ("Phone numbers", @"\b(\+?\d{1,3}[-.\s]?)?\(?\d{3}\)?[-.\s]?\d{3}[-.\s]?\d{4}\b", "#10B981"),
            ("Dates", @"\b\d{1,2}[/\-\.]\d{1,2}[/\-\.]\d{2,4}\b", "#F59E0B"),
            ("Times", @"\b\d{1,2}:\d{2}(:\d{2})?\s*(AM|PM|am|pm)?\b", "#8B5CF6"),
            ("Currency", @"\$\d+\.?\d*|\d+\.?\d*\s*(USD|EUR|GBP|pounds?|dollars?)", "#EC4899"),
            ("URLs", @"https?://[^\s]+", "#06B6D4"),
            ("Scores (X-Y)", @"\b\d{1,2}\s*[-:]\s*\d{1,2}\b", "#EF4444"),
        };

        var foundAny = false;
        foreach (var (name, pattern, color) in patterns)
        {
            var matches = Regex.Matches(_currentOcrText, pattern, RegexOptions.IgnoreCase);
            if (matches.Count > 0)
            {
                foundAny = true;
                var examples = string.Join(", ", matches.Take(3).Select(m => $"'{m.Value}'"));
                if (matches.Count > 3) examples += $" (+{matches.Count - 3} more)";
                
                PatternsPanel.Children.Add(new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new BoxView { Color = Color.FromArgb(color), WidthRequest = 4, HeightRequest = 16, CornerRadius = 2 },
                        new Label { Text = $"{name}: {matches.Count} found", FontSize = 12, FontAttributes = FontAttributes.Bold },
                    }
                });
                PatternsPanel.Children.Add(new Label 
                { 
                    Text = examples, 
                    FontSize = 11, 
                    TextColor = Colors.Gray,
                    Margin = new Thickness(12, 0, 0, 4)
                });
            }
        }

        if (!foundAny)
        {
            PatternsPanel.Children.Add(new Label { Text = "No common patterns detected", FontSize = 12, TextColor = Colors.Gray });
        }
    }

    private void UpdateEntityDetection()
    {
        EntitiesPanel.Children.Clear();
        
        var entities = new List<(string Type, List<string> Values)>();
        
        // Detect potential names (capitalized words)
        var potentialNames = Regex.Matches(_currentOcrText, @"\b[A-Z][a-z]+(?:\s+[A-Z][a-z]+)+\b")
            .Select(m => m.Value)
            .Distinct()
            .Take(5)
            .ToList();
        if (potentialNames.Count > 0)
            entities.Add(("Potential Names", potentialNames));

        // Detect uppercase words (might be headers/labels)
        var upperWords = Regex.Matches(_currentOcrText, @"\b[A-Z]{2,}\b")
            .Select(m => m.Value)
            .Distinct()
            .Take(5)
            .ToList();
        if (upperWords.Count > 0)
            entities.Add(("Uppercase Labels", upperWords));

        // Detect numbers
        var numberList = Regex.Matches(_currentOcrText, @"\b\d+\.?\d*\b")
            .Select(m => m.Value)
            .Distinct()
            .Take(10)
            .ToList();
        if (numberList.Count > 0)
            entities.Add(("Numbers", numberList));

        if (entities.Count == 0)
        {
            EntitiesPanel.Children.Add(new Label { Text = "No entities detected", FontSize = 12, TextColor = Colors.Gray });
            return;
        }

        foreach (var (type, values) in entities)
        {
            EntitiesPanel.Children.Add(new Label 
            { 
                Text = type, 
                FontSize = 12, 
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 4, 0, 2)
            });
            
            var valuesText = string.Join(", ", values);
            EntitiesPanel.Children.Add(new Label 
            { 
                Text = valuesText, 
                FontSize = 11, 
                TextColor = Colors.Gray,
                Margin = new Thickness(8, 0, 0, 0)
            });
        }
    }

    private void OnTabClicked(object sender, EventArgs e)
    {
        if (sender is not Button clickedTab) return;
        
        // Reset all tab styles
        var inactiveColor = Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#9CA3AF") 
            : Color.FromArgb("#374151");
        
        TabRawText.BackgroundColor = Colors.Transparent;
        TabRawText.TextColor = inactiveColor;
        TabLines.BackgroundColor = Colors.Transparent;
        TabLines.TextColor = inactiveColor;
        TabWords.BackgroundColor = Colors.Transparent;
        TabWords.TextColor = inactiveColor;
        TabAnalysis.BackgroundColor = Colors.Transparent;
        TabAnalysis.TextColor = inactiveColor;
        TabConfidence.BackgroundColor = Colors.Transparent;
        TabConfidence.TextColor = inactiveColor;
        
        // Activate clicked tab
        clickedTab.BackgroundColor = Application.Current?.RequestedTheme == AppTheme.Dark 
            ? Color.FromArgb("#2563EB") 
            : Color.FromArgb("#3B82F6");
        clickedTab.TextColor = Colors.White;
        
        // Show corresponding panel
        RawTextPanel.IsVisible = clickedTab == TabRawText;
        LinesPanel.IsVisible = clickedTab == TabLines;
        WordsPanel.IsVisible = clickedTab == TabWords;
        AnalysisPanel.IsVisible = clickedTab == TabAnalysis;
        ConfidencePanel.IsVisible = clickedTab == TabConfidence;
    }

    private void OnClearClicked(object sender, EventArgs e)
    {
        _currentImageData = null;
        _currentOcrText = "";
        _lastLocalOcrResult = null;
        _lastAzureOcrResult = null;
        
        // Reset UI
        ImagePreviewBorder.IsVisible = false;
        PlaceholderBorder.IsVisible = true;
        ProcessButton.IsEnabled = false;
        StatsCard.IsVisible = false;
        
        RawTextLabel.Text = "(No OCR results yet)";
        RawTextLabel.TextColor = Color.FromArgb("#64748B");
        
        LinesCollection.ItemsSource = null;
        LineCountLabel.Text = "0 lines detected";
        
        WordsFlexLayout.Children.Clear();
        WordCountLabel.Text = "0 unique words";
        
        StatCharacters.Text = "0";
        StatWords.Text = "0";
        StatLines.Text = "0";
        StatNumbers.Text = "0";
        
        PatternsPanel.Children.Clear();
        PatternsPanel.Children.Add(new Label { Text = "No patterns detected yet", FontSize = 12, TextColor = Colors.Gray });
        
        EntitiesPanel.Children.Clear();
        EntitiesPanel.Children.Add(new Label { Text = "No entities detected yet", FontSize = 12, TextColor = Colors.Gray });
        
        // Reset confidence tab
        HighConfCount.Text = "0";
        MedConfCount.Text = "0";
        LowConfCount.Text = "0";
        LowConfWordsPanel.Children.Clear();
        LowConfWordsPanel.Children.Add(new Label { Text = "No low confidence words", FontSize = 12, TextColor = Colors.Gray });
        ConfidenceWordsCollection.ItemsSource = null;
    }

    private async void OnCopyTextClicked(object sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_currentOcrText))
        {
            await Clipboard.SetTextAsync(_currentOcrText);
            await DisplayAlert("Copied", "OCR text copied to clipboard!", "OK");
        }
        else
        {
            await DisplayAlert("Nothing to Copy", "No OCR text available.", "OK");
        }
    }

    private void SetProcessingState(bool isProcessing, string? message = null)
    {
        ProcessingPanel.IsVisible = isProcessing;
        ProcessingLabel.Text = message ?? "Processing...";
        
        SelectImageButton.IsEnabled = !isProcessing;
        TakePhotoButton.IsEnabled = !isProcessing;
        ProcessButton.IsEnabled = !isProcessing && _currentImageData != null && (!_useAzureOcr || _azureOcrService.IsConfigured);
        ClearButton.IsEnabled = !isProcessing;
    }

    private void ShowError(string message)
    {
        ErrorMessageLabel.Text = message;
        ErrorOverlay.IsVisible = true;
        
        if (_useAzureOcr)
        {
            AzureStatusLabel.Text = "Last OCR: Failed";
            AzureStatusLabel.TextColor = Colors.Red;
        }
        else
        {
            OcrStatusLabel.Text = "Last OCR: Failed";
            OcrStatusLabel.TextColor = Colors.Red;
        }
    }

    private void OnDismissErrorClicked(object sender, EventArgs e)
    {
        ErrorOverlay.IsVisible = false;
    }
}
