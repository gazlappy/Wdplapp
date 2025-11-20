using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.Maui.ApplicationModel.DataTransfer;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views
{
    public partial class SettingsPage : ContentPage
    {
        private static AppSettings Settings => DataStore.Data.Settings;

        private readonly ObservableCollection<string> _categories = new()
        {
            "Player Ratings",
            "Match Scoring",
            "Fixture Defaults",
            "Import Data",
            "About"
        };

        // UI Elements (created programmatically)
        private Entry? _startingRatingEntry;
        private Entry? _ratingWeightingEntry;
        private Entry? _ratingsBiasEntry;
        private Entry? _winFactorEntry;
        private Entry? _lossFactorEntry;
        private Entry? _eightBallFactorEntry;
        private Switch? _useEightBallSwitch;
        private Entry? _minFramesEntry;
        private Entry? _pointsForWinEntry;
        private Entry? _pointsForDrawEntry;
        private Entry? _framesPerMatchEntry;
        private Picker? _matchDayPicker;
        private TimePicker? _matchTimePicker;
        private Entry? _roundsPerOpponentEntry;
        private Label? _statusLabel;

        public SettingsPage()
        {
            InitializeComponent();

            CategoriesList.ItemsSource = _categories;

            // Select first category by default
            CategoriesList.SelectedItem = _categories.First();

            // Don't use the responsive layout handler since XAML defines fixed columns
            // SizeChanged += (_, __) => ApplyResponsiveLayout(Width);
        }

        private void OnCategorySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var selected = e.CurrentSelection?.FirstOrDefault() as string;
            ShowCategory(selected);
        }

        private void ShowCategory(string? category)
        {
            View? content = category switch
            {
                "Player Ratings" => CreatePlayerRatingsPanel(),
                "Match Scoring" => CreateMatchScoringPanel(),
                "Fixture Defaults" => CreateFixtureDefaultsPanel(),
                "Import Data" => CreateImportDataPanel(),
                "About" => CreateAboutPanel(),
                _ => null
            };

            ContentPanel.Content = content;
        }

        private View CreatePlayerRatingsPanel()
        {
            _startingRatingEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1500", Text = Settings.RatingStartValue.ToString() };
            _ratingWeightingEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "100", Text = Settings.RatingWeighting.ToString() };
            _ratingsBiasEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "5", Text = Settings.RatingsBias.ToString() };
            _winFactorEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1.0", Text = Settings.WinFactor.ToString("0.00") };
            _lossFactorEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1.0", Text = Settings.LossFactor.ToString("0.00") };
            _eightBallFactorEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1.5", Text = Settings.EightBallFactor.ToString("0.00") };
            _useEightBallSwitch = new Switch { IsToggled = Settings.UseEightBallFactor };
            _minFramesEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "60", Text = Settings.MinFramesPercentage.ToString() };
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = 140 } }, RowSpacing = 12 };

            grid.Add(new Label { Text = "Starting rating:", VerticalTextAlignment = TextAlignment.Center }, 0, 0);
            grid.Add(_startingRatingEntry, 1, 0);

            grid.Add(new Label { Text = "Rating weighting:", VerticalTextAlignment = TextAlignment.Center }, 0, 1);
            grid.Add(_ratingWeightingEntry, 1, 1);

            grid.Add(new Label { Text = "Ratings bias (decay):", VerticalTextAlignment = TextAlignment.Center }, 0, 2);
            grid.Add(_ratingsBiasEntry, 1, 2);

            grid.Add(new Label { Text = "Win factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 3);
            grid.Add(_winFactorEntry, 1, 3);

            grid.Add(new Label { Text = "Loss factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 4);
            grid.Add(_lossFactorEntry, 1, 4);

            grid.Add(new Label { Text = "Use 8-ball factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 5);
            grid.Add(_useEightBallSwitch, 1, 5);

            grid.Add(new Label { Text = "8-ball factor:", VerticalTextAlignment = TextAlignment.Center }, 0, 6);
            grid.Add(_eightBallFactorEntry, 1, 6);

            grid.Add(new Label { Text = "Min frames % for table:", VerticalTextAlignment = TextAlignment.Center }, 0, 7);
            grid.Add(_minFramesEntry, 1, 7);

            _useEightBallSwitch.Toggled += (s, e) => _eightBallFactorEntry.IsEnabled = e.Value;
            _eightBallFactorEntry.IsEnabled = Settings.UseEightBallFactor;

            var infoFrame = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0F9FF"),
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new Label
                {
                    FontSize = 12,
                    LineHeight = 1.4,
                    FormattedText = new FormattedString
                    {
                        Spans =
                        {
                            new Span { Text = "VBA-Style Cumulative Weighted Rating:\n\n", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "• Earlier frames have lower weight (Weighting - Bias × frames)\n" },
                            new Span { Text = "• Later frames have higher weight (progressive bias increase)\n" },
                            new Span { Text = "• Rating based on opponent strength at time of match\n" },
                            new Span { Text = "• Win against stronger opponent = higher rating gain\n\n" },
                            new Span { Text = "Min Frames %:\n", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "Percentage of maximum available frames needed to appear in ratings table.\n" },
                            new Span { Text = "Example: If max is 30 frames and you set 60%, players need 18 frames.\n" },
                            new Span { Text = "All players still have ratings calculated.\n\n" },
                            new Span { Text = "Formula:\n", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "Rating = ?(OpponentRating × Factor × Weight) / ?Weight" }
                        }
                    }
                }
            };

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new Button { Text = "Save Settings", Command = new Command(OnSaveClicked) },
                    new Button { Text = "Reset to Defaults", BackgroundColor = Color.FromArgb("#FF6B6B"), TextColor = Colors.White, Command = new Command(OnResetClicked) }
                }
            };

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Player Rating System", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "VBA-style opponent-based cumulative weighted rating system", FontSize = 14, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 0, 0, 8) },
                    grid,
                    infoFrame,
                    buttons,
                    _statusLabel
                }
            };
        }

        private View CreateMatchScoringPanel()
        {
            _pointsForWinEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "2", Text = Settings.MatchWinBonus.ToString() };
            _pointsForDrawEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "1", Text = Settings.MatchDrawBonus.ToString() };
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = 140 } }, RowSpacing = 12 };

            grid.Add(new Label { Text = "Match win bonus:", VerticalTextAlignment = TextAlignment.Center }, 0, 0);
            grid.Add(_pointsForWinEntry, 1, 0);

            grid.Add(new Label { Text = "Match draw bonus:", VerticalTextAlignment = TextAlignment.Center }, 0, 1);
            grid.Add(_pointsForDrawEntry, 1, 1);

            var infoFrame = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0F9FF"),
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new Label
                {
                    FontSize = 12,
                    LineHeight = 1.4,
                    FormattedText = new FormattedString
                    {
                        Spans =
                        {
                            new Span { Text = "New Points System:\n\n", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "Team points = Frames Won + Bonus\n\n" },
                            new Span { Text = "• Win: ", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "Frames Won + Match Win Bonus\n" },
                            new Span { Text = "• Draw: ", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "Frames Won + Match Draw Bonus\n" },
                            new Span { Text = "• Loss: ", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "Frames Won (no bonus)\n\n" },
                            new Span { Text = "Example: ", FontAttributes = FontAttributes.Bold },
                            new Span { Text = "Team wins 6-4 with Win Bonus=2:\n" },
                            new Span { Text = "  Winner gets 6+2=8 points\n" },
                            new Span { Text = "  Loser gets 4 points" }
                        }
                    }
                }
            };

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new Button { Text = "Save Settings", Command = new Command(OnSaveClicked) },
                    new Button { Text = "Reset to Defaults", BackgroundColor = Color.FromArgb("#FF6B6B"), TextColor = Colors.White, Command = new Command(OnResetClicked) }
                }
            };

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Match Scoring", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Configure how team match points are awarded", FontSize = 14, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 0, 0, 8) },
                    grid,
                    infoFrame,
                    buttons,
                    _statusLabel
                }
            };
        }

        private View CreateFixtureDefaultsPanel()
        {
            _framesPerMatchEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "10", Text = Settings.DefaultFramesPerMatch.ToString() };
            _matchDayPicker = new Picker
            {
                ItemsSource = Enum.GetValues(typeof(DayOfWeek)).Cast<DayOfWeek>().Select(d => d.ToString()).ToList(),
                SelectedIndex = (int)Settings.DefaultMatchDay
            };
            _matchTimePicker = new TimePicker { Format = "HH:mm", Time = Settings.DefaultMatchTime };
            _roundsPerOpponentEntry = new Entry { Keyboard = Keyboard.Numeric, Placeholder = "2", Text = Settings.DefaultRoundsPerOpponent.ToString() };
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var grid = new Grid { ColumnDefinitions = { new ColumnDefinition(), new ColumnDefinition { Width = 140 } }, RowSpacing = 12 };

            grid.Add(new Label { Text = "Frames per match:", VerticalTextAlignment = TextAlignment.Center }, 0, 0);
            grid.Add(_framesPerMatchEntry, 1, 0);

            grid.Add(new Label { Text = "Default match day:", VerticalTextAlignment = TextAlignment.Center }, 0, 1);
            grid.Add(_matchDayPicker, 1, 1);

            grid.Add(new Label { Text = "Default match time:", VerticalTextAlignment = TextAlignment.Center }, 0, 2);
            grid.Add(_matchTimePicker, 1, 2);

            grid.Add(new Label { Text = "Rounds per opponent:", VerticalTextAlignment = TextAlignment.Center }, 0, 3);
            grid.Add(_roundsPerOpponentEntry, 1, 3);

            var infoFrame = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0F9FF"),
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                Content = new Label
                {
                    FontSize = 12,
                    LineHeight = 1.4,
                    Text = "These defaults are used when generating fixtures for a season. \nYou can override them when creating a specific season."
                }
            };

            var buttons = new HorizontalStackLayout
            {
                Spacing = 12,
                Margin = new Thickness(0, 16, 0, 0),
                Children =
                {
                    new Button { Text = "Save Settings", Command = new Command(OnSaveClicked) },
                    new Button { Text = "Reset to Defaults", BackgroundColor = Color.FromArgb("#FF6B6B"), TextColor = Colors.White, Command = new Command(OnResetClicked) }
                }
            };

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "Fixture Generation Defaults", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Default values used when generating new fixtures", FontSize = 14, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 0, 0, 8) },
                    grid,
                    infoFrame,
                    buttons,
                    _statusLabel
                }
            };
        }

        private View CreateImportDataPanel()
        {
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0), TextColor = Color.FromArgb("#666") };

            // ========== FILE SELECTION ==========
            var selectedFileLabel = new Label
            {
                Text = "No database selected",
                FontSize = 12,
                TextColor = Colors.Gray,
                Margin = new Thickness(0, 8, 0, 4)
            };

            var selectDbBtn = new Button
            {
                Text = "Select Access Database",
                Margin = new Thickness(0, 4)
            };

            // ========== ACTION BUTTONS ==========
            var buttonsLayout = new HorizontalStackLayout
            {
                Spacing = 8,
                Margin = new Thickness(0, 8, 0, 0)
            };

            var inspectBtn = new Button
            {
                Text = "Inspect Schema",
                IsEnabled = false
            };

            var importBtn = new Button
            {
                Text = "Import Data",
                IsEnabled = false,
                BackgroundColor = Color.FromArgb("#10B981"),
                TextColor = Colors.White
            };

            buttonsLayout.Add(inspectBtn);
            buttonsLayout.Add(importBtn);

            // ========== ACTIVITY INDICATOR ==========
            var loadingIndicator = new ActivityIndicator
            {
                IsRunning = false,
                IsVisible = false,
                Margin = new Thickness(0, 8)
            };

            // ========== OUTPUT DISPLAY ==========
            var outputLabel = new Label
            {
                Text = "Output:",
                FontAttributes = FontAttributes.Bold,
                Margin = new Thickness(0, 16, 0, 4)
            };

            var outputEditor = new Editor
            {
                IsReadOnly = true,
                FontFamily = "Courier New",
                FontSize = 11,
                HeightRequest = 350,
                Placeholder = "Schema inspection or import results will appear here...",
                Margin = new Thickness(0, 4)
            };

            var copyBtn = new Button
            {
                Text = "Copy to Clipboard",
                IsEnabled = false,
                Margin = new Thickness(0, 4)
            };

            // ========== IMPORT RESULTS SECTION ==========
            var resultsBorder = new Border
            {
                IsVisible = false,
                Stroke = Color.FromArgb("#10B981"),
                StrokeThickness = 2,
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0FDF4"),
                Margin = new Thickness(0, 12, 0, 0),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "? Import Results", FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = Color.FromArgb("#10B981") },
                        new Label { Text = "", FontSize = 12, LineHeight = 1.4 }
                    }
                }
            };

            var errorsBorder = new Border
            {
                IsVisible = false,
                Stroke = Color.FromArgb("#EF4444"),
                StrokeThickness = 2,
                Padding = 12,
                BackgroundColor = Color.FromArgb("#FEF2F2"),
                Margin = new Thickness(0, 12, 0, 0),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = new ScrollView
                {
                    MaximumHeightRequest = 150,
                    Content = new VerticalStackLayout
                    {
                        Spacing = 8,
                        Children =
                        {
                            new Label { Text = "? Errors", FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = Color.FromArgb("#EF4444") },
                            new Label { Text = "", FontSize = 11, LineHeight = 1.4 }
                        }
                    }
                }
            };

            string? selectedDatabasePath = null;

            // ========== EVENT HANDLERS ==========

            // File Selection
            selectDbBtn.Clicked += async (s, e) =>
            {
                try
                {
                    var customFileType = new FilePickerFileType(
                        new Dictionary<DevicePlatform, IEnumerable<string>>
                        {
                            { DevicePlatform.WinUI, new[] { ".accdb", ".mdb" } },
                            { DevicePlatform.MacCatalyst, new[] { "accdb", "mdb" } },
                            { DevicePlatform.Android, new[] { "application/msaccess", "application/x-msaccess" } },
                            { DevicePlatform.iOS, new[] { "accdb", "mdb" } }
                        });

                    var result = await FilePicker.PickAsync(new PickOptions
                    {
                        PickerTitle = "Select Access Database",
                        FileTypes = customFileType
                    });

                    if (result != null)
                    {
                        selectedDatabasePath = result.FullPath;
                        selectedFileLabel.Text = $"?? Selected: {System.IO.Path.GetFileName(result.FullPath)}";
                        selectedFileLabel.TextColor = Color.FromArgb("#10B981");
                        inspectBtn.IsEnabled = true;
                        importBtn.IsEnabled = true;
                        outputEditor.Text = "";
                        resultsBorder.IsVisible = false;
                        errorsBorder.IsVisible = false;
                        copyBtn.IsEnabled = false;
                        
                        if (_statusLabel != null)
                            _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Database selected - ready to inspect or import";
                    }
                }
                catch (Exception ex)
                {
                    await DisplayAlert("Error", $"Failed to select file: {ex.Message}", "OK");
                }
            };

            // Inspect Schema
            inspectBtn.Clicked += async (s, e) =>
            {
                if (string.IsNullOrEmpty(selectedDatabasePath)) return;

                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    await DisplayAlert("Windows Only", "Database inspection is only supported on Windows.", "OK");
                    return;
                }

                try
                {
                    loadingIndicator.IsRunning = true;
                    loadingIndicator.IsVisible = true;
                    inspectBtn.IsEnabled = false;
                    importBtn.IsEnabled = false;
                    outputEditor.Text = "?? Inspecting database schema...\n\nPlease wait...";
                    resultsBorder.IsVisible = false;
                    errorsBorder.IsVisible = false;

#pragma warning disable CA1416 // Validate platform compatibility - runtime check performed above
                    var schemaInfo = await Task.Run(() => 
                        AccessDatabaseImporter.InspectDatabaseSchema(selectedDatabasePath));
#pragma warning restore CA1416

                    outputEditor.Text = schemaInfo;
                    copyBtn.IsEnabled = true;
                    
                    if (_statusLabel != null)
                        _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Schema inspection complete";

                    await DisplayAlert("Success", "Schema inspection complete! Review the output below.", "OK");
                }
                catch (Exception ex)
                {
                    outputEditor.Text = $"? INSPECTION FAILED\n\n{ex}";
                    
                    if (_statusLabel != null)
                        _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Inspection failed";

                    await DisplayAlert("Error", $"Inspection failed: {ex.Message}", "OK");
                }
                finally
                {
                    loadingIndicator.IsRunning = false;
                    loadingIndicator.IsVisible = false;
                    inspectBtn.IsEnabled = true;
                    importBtn.IsEnabled = true;
                }
            };

            // Import Data
            importBtn.Clicked += async (s, e) =>
            {
                if (string.IsNullOrEmpty(selectedDatabasePath)) return;

                if (DeviceInfo.Platform != DevicePlatform.WinUI)
                {
                    await DisplayAlert("Windows Only", "Database import is only supported on Windows.", "OK");
                    return;
                }

                var confirm = await DisplayAlert(
                    "Confirm Import",
                    "This will import all historical data from the Access database and merge it with your current data.\n\n?? This cannot be undone. Consider backing up your current data first.\n\nContinue?",
                    "Yes, Import",
                    "Cancel");

                if (!confirm) return;

                try
                {
                    loadingIndicator.IsRunning = true;
                    loadingIndicator.IsVisible = true;
                    inspectBtn.IsEnabled = false;
                    importBtn.IsEnabled = false;
                    selectDbBtn.IsEnabled = false;
                    outputEditor.Text = "?? Importing data...\n\nPlease wait, this may take a few moments...";
                    resultsBorder.IsVisible = false;
                    errorsBorder.IsVisible = false;
                    
                    if (_statusLabel != null)
                        _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Importing data...";

#pragma warning disable CA1416 // Validate platform compatibility - runtime check performed above
                    // Run import
                    var importer = new AccessDatabaseImporter(selectedDatabasePath);
                    var (importedData, summary) = await Task.Run(() => importer.ImportAllAsync());
#pragma warning restore CA1416

                    if (summary.Success)
                    {
                        // Merge with existing data
                        await MergeImportedDataAsync(importedData);

                        // Show success
                        resultsBorder.IsVisible = true;
                        var resultsContent = resultsBorder.Content as VerticalStackLayout;
                        if (resultsContent != null && resultsContent.Children.Count > 1)
                        {
                            ((Label)resultsContent.Children[1]).Text = summary.Summary;
                        }

                        outputEditor.Text = $"? IMPORT SUCCESSFUL\n\n{summary.Summary}\n\n{summary.DiagnosticLog}";
                        copyBtn.IsEnabled = true;

                        if (_statusLabel != null)
                            _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Import completed successfully!";

                        await DisplayAlert("Success", "Data imported successfully! The app will reload.", "OK");

                        // Refresh the app
                        SeasonService.Initialize(); // Reinitialize season service
                    }
                    else
                    {
                        errorsBorder.IsVisible = true;
                        var errorContent = (errorsBorder.Content as ScrollView)?.Content as VerticalStackLayout;
                        if (errorContent != null && errorContent.Children.Count > 1)
                        {
                            ((Label)errorContent.Children[1]).Text = string.Join("\n", summary.Errors);
                        }

                        outputEditor.Text = $"? IMPORT FAILED\n\n{summary.Message}\n\nErrors:\n{string.Join("\n", summary.Errors)}";
                        copyBtn.IsEnabled = true;

                        if (_statusLabel != null)
                            _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Import failed";

                        await DisplayAlert("Import Failed", summary.Message, "OK");
                    }
                }
                catch (Exception ex)
                {
                    errorsBorder.IsVisible = true;
                    var errorContent = (errorsBorder.Content as ScrollView)?.Content as VerticalStackLayout;
                    if (errorContent != null && errorContent.Children.Count > 1)
                    {
                        ((Label)errorContent.Children[1]).Text = ex.ToString();
                    }

                    outputEditor.Text = $"? IMPORT ERROR\n\n{ex}";
                    copyBtn.IsEnabled = true;

                    if (_statusLabel != null)
                        _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Import error";

                    await DisplayAlert("Error", $"Import failed: {ex.Message}", "OK");
                }
                finally
                {
                    loadingIndicator.IsRunning = false;
                    loadingIndicator.IsVisible = false;
                    inspectBtn.IsEnabled = true;
                    importBtn.IsEnabled = true;
                    selectDbBtn.IsEnabled = true;
                }
            };

            // Copy to Clipboard
            copyBtn.Clicked += async (s, e) =>
            {
                if (!string.IsNullOrEmpty(outputEditor.Text))
                {
                    await Clipboard.SetTextAsync(outputEditor.Text);
                    await DisplayAlert("Copied", "Output copied to clipboard!", "OK");
                    
                    if (_statusLabel != null)
                        _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  ? Copied to clipboard";
                }
            };

            // ========== INFO FRAME ==========
            var infoFrame = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0F9FF"),
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 12, 0, 0),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "?? Access Database Import & Inspection", FontAttributes = FontAttributes.Bold, FontSize = 14 },
                        new Label
                        {
                            FontSize = 12,
                            LineHeight = 1.4,
                            FormattedText = new FormattedString
                            {
                                Spans =
                                {
                                    new Span { Text = "Instructions:\n", FontAttributes = FontAttributes.Bold },
                                    new Span { Text = "1. Click 'Select Access Database' to choose your .accdb or .mdb file\n" },
                                    new Span { Text = "2. Use 'Inspect Schema' to view database structure before importing\n" },
                                    new Span { Text = "3. Use 'Import Data' to merge historical data into your league\n\n" },
                                    new Span { Text = "Features:\n", FontAttributes = FontAttributes.Bold },
                                    new Span { Text = "• Imports divisions, venues, teams, players, seasons, and fixtures\n" },
                                    new Span { Text = "• Merges with existing data (no duplicates)\n" },
                                    new Span { Text = "• Calculates accurate VBA-style player ratings\n" },
                                    new Span { Text = "• Copy results to clipboard for reference\n\n" },
                                    new Span { Text = "Requirements:\n", FontAttributes = FontAttributes.Bold },
                                    new Span { Text = "• Windows-only (requires OLE DB drivers)\n" },
                                    new Span { Text = "• Supports .accdb (2007+) and .mdb (2003)\n" },
                                    new Span { Text = "• Database must be closed (not open in Access)" }
                                }
                            }
                        }
                    }
                }
            };

            // ========== WARNING FRAME ==========
            var warningFrame = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#FFFBEB"),
                Stroke = Color.FromArgb("#F59E0B"),
                StrokeThickness = 1,
                Margin = new Thickness(0, 8, 0, 0),
                StrokeShape = new RoundRectangle { CornerRadius = 8 },
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "?? Important Notes", FontAttributes = FontAttributes.Bold, FontSize = 14, TextColor = Color.FromArgb("#F59E0B") },
                        new Label
                        {
                            FontSize = 12,
                            LineHeight = 1.4,
                            FormattedText = new FormattedString
                            {
                                Spans =
                                {
                                    new Span { Text = "• " },
                                    new Span { Text = "Back up your data", FontAttributes = FontAttributes.Bold },
                                    new Span { Text = " before importing\n" },
                                    new Span { Text = "• Imports cannot be undone\n" },
                                    new Span { Text = "• Large databases may take several minutes\n" },
                                    new Span { Text = "• The app will reload after successful import" }
                                }
                            }
                        }
                    }
                }
            };

            return new ScrollView
            {
                Content = new VerticalStackLayout
                {
                    Spacing = 12,
                    Children =
                    {
                        new Label { Text = "Import Historical Data", FontSize = 20, FontAttributes = FontAttributes.Bold },
                        new Label { Text = "Import and inspect Access database files", FontSize = 14, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 0, 0, 8) },
                        
                        selectDbBtn,
                        selectedFileLabel,
                        buttonsLayout,
                        loadingIndicator,
                        
                        outputLabel,
                        outputEditor,
                        copyBtn,
                        
                        resultsBorder,
                        errorsBorder,
                        
                        warningFrame,
                        infoFrame,
                        _statusLabel
                    }
                }
            };
        }

        [SupportedOSPlatform("windows")]
        private async Task MergeImportedDataAsync(LeagueData importedData)
        {
            await Task.Run(() =>
            {
                var beforeCounts = new
                {
                    Divisions = DataStore.Data.Divisions.Count,
                    Venues = DataStore.Data.Venues.Count,
                    Teams = DataStore.Data.Teams.Count,
                    Players = DataStore.Data.Players.Count,
                    Seasons = DataStore.Data.Seasons.Count,
                    Fixtures = DataStore.Data.Fixtures.Count
                };

                // Merge data (avoiding duplicates)
                foreach (var div in importedData.Divisions)
                {
                    if (!DataStore.Data.Divisions.Any(d => d.Name.Equals(div.Name, StringComparison.OrdinalIgnoreCase)))
                        DataStore.Data.Divisions.Add(div);
                }

                foreach (var venue in importedData.Venues)
                {
                    if (!DataStore.Data.Venues.Any(v => v.Name != null && v.Name.Equals(venue.Name, StringComparison.OrdinalIgnoreCase)))
                        DataStore.Data.Venues.Add(venue);
                }

                foreach (var team in importedData.Teams)
                {
                    if (!DataStore.Data.Teams.Any(t => t.Name != null && t.Name.Equals(team.Name, StringComparison.OrdinalIgnoreCase)))
                        DataStore.Data.Teams.Add(team);
                }

                foreach (var player in importedData.Players)
                {
                    var fullName = player.FullName;
                    if (!DataStore.Data.Players.Any(p => p.FullName.Equals(fullName, StringComparison.OrdinalIgnoreCase)))
                        DataStore.Data.Players.Add(player);
                }

                foreach (var season in importedData.Seasons)
                {
                    season.IsActive = false; // Don't auto-activate imported seasons
                    if (!DataStore.Data.Seasons.Any(s => s.Name.Equals(season.Name, StringComparison.OrdinalIgnoreCase)))
                        DataStore.Data.Seasons.Add(season);
                }

                DataStore.Data.Fixtures.AddRange(importedData.Fixtures);
                
                DataStore.Save();

                System.Diagnostics.Debug.WriteLine($"Import Complete:");
                System.Diagnostics.Debug.WriteLine($"  Divisions: {beforeCounts.Divisions} ? {DataStore.Data.Divisions.Count}");
                System.Diagnostics.Debug.WriteLine($"  Venues: {beforeCounts.Venues} ? {DataStore.Data.Venues.Count}");
                System.Diagnostics.Debug.WriteLine($"  Teams: {beforeCounts.Teams} ? {DataStore.Data.Teams.Count}");
                System.Diagnostics.Debug.WriteLine($"  Players: {beforeCounts.Players} ? {DataStore.Data.Players.Count}");
                System.Diagnostics.Debug.WriteLine($"  Seasons: {beforeCounts.Seasons} ? {DataStore.Data.Seasons.Count}");
                System.Diagnostics.Debug.WriteLine($"  Fixtures: {beforeCounts.Fixtures} ? {DataStore.Data.Fixtures.Count}");
            });
        }

        private View CreateAboutPanel()
        {
            _statusLabel = new Label { FontSize = 12, Margin = new Thickness(0, 8, 0, 0) };

            var infoFrame = new Border
            {
                Padding = 12,
                BackgroundColor = Color.FromArgb("#F0F9FF"),
                Stroke = Color.FromArgb("#3B82F6"),
                StrokeThickness = 1,
                Content = new VerticalStackLayout
                {
                    Spacing = 8,
                    Children =
                    {
                        new Label { Text = "How Settings Work", FontAttributes = FontAttributes.Bold, FontSize = 14 },
                        new Label
                        {
                            FontSize = 12,
                            LineHeight = 1.4,
                            FormattedText = new FormattedString
                            {
                                Spans =
                                {
                                    new Span { Text = "• Settings are saved with your league data\n" },
                                    new Span { Text = "• Player ratings use VBA-style cumulative weighted calculation\n" },
                                    new Span { Text = "• Rating changes based on opponent strength at time of match\n" },
                                    new Span { Text = "• Changes to rating settings require refreshing the Tables page\n" },
                                    new Span { Text = "• Fixture defaults only apply to newly generated fixtures\n" },
                                    new Span { Text = "• Use 'Reset to Defaults' to restore original values" }
                                }
                            }
                        }
                    }
                }
            };

            return new VerticalStackLayout
            {
                Spacing = 12,
                Children =
                {
                    new Label { Text = "About Settings", FontSize = 20, FontAttributes = FontAttributes.Bold },
                    new Label { Text = "Information about league settings and data management", FontSize = 14, TextColor = Color.FromArgb("#666"), Margin = new Thickness(0, 0, 0, 8) },
                    infoFrame,
                    _statusLabel
                }
            };
        }

        private async void OnSaveClicked()
        {
            try
            {
                // Validate and save Player Rating settings
                if (_startingRatingEntry != null)
                {
                    if (!int.TryParse(_startingRatingEntry.Text, out var startingRating) || startingRating < 0)
                    {
                        await DisplayAlert("Invalid Input", "Starting rating must be a positive number.", "OK");
                        return;
                    }
                    Settings.RatingStartValue = startingRating;
                }

                if (_ratingWeightingEntry != null)
                {
                    if (!int.TryParse(_ratingWeightingEntry.Text, out var weighting) || weighting < 1)
                    {
                        await DisplayAlert("Invalid Input", "Rating weighting must be at least 1.", "OK");
                        return;
                    }
                    Settings.RatingWeighting = weighting;
                }

                if (_ratingsBiasEntry != null)
                {
                    if (!int.TryParse(_ratingsBiasEntry.Text, out var bias) || bias < 0)
                    {
                        await DisplayAlert("Invalid Input", "Ratings bias must be 0 or higher.", "OK");
                        return;
                    }
                    Settings.RatingsBias = bias;
                }

                if (_winFactorEntry != null)
                {
                    if (!double.TryParse(_winFactorEntry.Text, out var winFactor) || winFactor < 0 || winFactor > 10)
                    {
                        await DisplayAlert("Invalid Input", "Win factor must be between 0 and 10.", "OK");
                        return;
                    }
                    Settings.WinFactor = winFactor;
                }

                if (_lossFactorEntry != null)
                {
                    if (!double.TryParse(_lossFactorEntry.Text, out var lossFactor) || lossFactor < 0 || lossFactor > 10)
                    {
                        await DisplayAlert("Invalid Input", "Loss factor must be between 0 and 10.", "OK");
                        return;
                    }
                    Settings.LossFactor = lossFactor;
                }

                if (_eightBallFactorEntry != null)
                {
                    if (!double.TryParse(_eightBallFactorEntry.Text, out var eightBallFactor) || eightBallFactor < 0 || eightBallFactor > 10)
                    {
                        await DisplayAlert("Invalid Input", "8-ball factor must be between 0 and 10.", "OK");
                        return;
                    }
                    Settings.EightBallFactor = eightBallFactor;
                }

                if (_useEightBallSwitch != null)
                    Settings.UseEightBallFactor = _useEightBallSwitch.IsToggled;

                if (_minFramesEntry != null)
                {
                    if (!int.TryParse(_minFramesEntry.Text, out var minFramesPct) || minFramesPct < 0 || minFramesPct > 100)
                    {
                        await DisplayAlert("Invalid Input", "Min frames percentage must be between 0 and 100.", "OK");
                        return;
                    }
                    Settings.MinFramesPercentage = minFramesPct;
                }

                // Save Match Scoring settings
                if (_pointsForWinEntry != null && int.TryParse(_pointsForWinEntry.Text, out var ptsWin) && ptsWin >= 0)
                    Settings.MatchWinBonus = ptsWin;

                if (_pointsForDrawEntry != null && int.TryParse(_pointsForDrawEntry.Text, out var ptsDraw) && ptsDraw >= 0)
                    Settings.MatchDrawBonus = ptsDraw;

                // Save Fixture Defaults
                if (_framesPerMatchEntry != null && int.TryParse(_framesPerMatchEntry.Text, out var frames) && frames >= 1)
                    Settings.DefaultFramesPerMatch = frames;

                if (_matchDayPicker != null)
                    Settings.DefaultMatchDay = (DayOfWeek)_matchDayPicker.SelectedIndex;

                if (_matchTimePicker != null)
                    Settings.DefaultMatchTime = _matchTimePicker.Time;

                if (_roundsPerOpponentEntry != null && int.TryParse(_roundsPerOpponentEntry.Text, out var rounds) && rounds >= 1)
                    Settings.DefaultRoundsPerOpponent = rounds;

                DataStore.Save();

                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  Settings saved successfully.";

                await DisplayAlert("Settings Saved", "Your settings have been saved.", "OK");
            }
            catch (Exception ex)
            {
                if (_statusLabel != null)
                    _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  Error: {ex.Message}";
                await DisplayAlert("Error", $"Failed to save settings: {ex.Message}", "OK");
            }
        }

        private async void OnResetClicked()
        {
            var confirm = await DisplayAlert(
                "Reset Settings",
                "Are you sure you want to reset all settings to their default values?",
                "Reset",
                "Cancel");

            if (!confirm) return;

            Settings.ResetToDefaults();
            DataStore.Save();

            var selected = CategoriesList.SelectedItem as string;
            ShowCategory(selected);

            if (_statusLabel != null)
                _statusLabel.Text = $"{DateTime.Now:HH:mm:ss}  Settings reset to defaults.";
        }

        private void ApplyResponsiveLayout(double width)
        {
            var left = RootGrid.Children[0];
            var right = RootGrid.Children[1];

            RootGrid.ColumnDefinitions.Clear();
            RootGrid.RowDefinitions.Clear();

            if (width >= 800)
            {
                RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition(new GridLength(2, GridUnitType.Star)));

                Grid.SetRow((BindableObject)left, 0);
                Grid.SetRow((BindableObject)right, 0);
                Grid.SetColumn((BindableObject)left, 0);
                Grid.SetColumn((BindableObject)right, 1);
            }
            else
            {
                RootGrid.ColumnDefinitions.Add(new ColumnDefinition(GridLength.Star));
                RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Auto));
                RootGrid.RowDefinitions.Add(new RowDefinition(GridLength.Star));

                Grid.SetColumn((BindableObject)left, 0);
                Grid.SetColumn((BindableObject)right, 0);
                Grid.SetRow((BindableObject)left, 0);
                Grid.SetRow((BindableObject)right, 1);
            }
        }
    }
}
