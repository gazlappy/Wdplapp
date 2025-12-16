using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Storage;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Dedicated page for importing WDPL SQL dump files from phpMyAdmin/VBA Access
/// Redesigned with step-by-step wizard and better user feedback
/// </summary>
public partial class SqlImportPage : ContentPage
{
    private string? _selectedFilePath;
    private SqlFileImporter.SqlImportResult? _lastImportResult;
    private SqlFileImporter.ParsedSqlData? _parsedData;
    
    // UI state
    private int _currentStep = 1;
    private bool _isProcessing = false;
    
    // Optional pre-selected file path (set before page appears)
    private string? _preSelectedFilePath;

    public SqlImportPage()
    {
        InitializeComponent();
        BuildUI();
    }
    
    /// <summary>
    /// Constructor with pre-selected file path
    /// </summary>
    public SqlImportPage(string filePath) : this()
    {
        _preSelectedFilePath = filePath;
    }
    
    /// <summary>
    /// Load a pre-selected file after the page appears
    /// </summary>
    public async Task LoadFileAsync(string filePath)
    {
        _selectedFilePath = filePath;
        
        var fileLabel = FindElement<Label>("FileLabel");
        var fileSubLabel = FindElement<Label>("FileSubLabel");
        
        if (fileLabel != null)
        {
            fileLabel.Text = Path.GetFileName(filePath);
            fileLabel.TextColor = Color.FromArgb("#4CAF50");
        }
        
        if (fileSubLabel != null)
        {
            try
            {
                var fileInfo = new FileInfo(filePath);
                fileSubLabel.Text = $"{fileInfo.Length / 1024} KB • Click to change";
            }
            catch
            {
                fileSubLabel.Text = "Click to change file";
            }
        }

        UpdateStepVisibility();
        
        // Automatically start preview
        await PreviewDataAsync();
    }
    
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        
        // If we have a pre-selected file, load it automatically
        if (!string.IsNullOrEmpty(_preSelectedFilePath))
        {
            await LoadFileAsync(_preSelectedFilePath);
            _preSelectedFilePath = null; // Clear so it doesn't reload
        }
    }

    private void BuildUI()
    {
        Title = "SQL Import Wizard";
        BackgroundColor = Color.FromArgb("#F5F5F5");

        var mainLayout = new VerticalStackLayout
        {
            Padding = new Thickness(16),
            Spacing = 0
        };

        // Header with progress steps
        mainLayout.Children.Add(BuildHeader());
        
        // Step indicator
        mainLayout.Children.Add(BuildStepIndicator());

        // Main content area
        var contentFrame = new Border
        {
            StrokeThickness = 0,
            BackgroundColor = Colors.White,
            Padding = new Thickness(20),
            Margin = new Thickness(0, 16, 0, 0),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 12 },
            Shadow = new Shadow { Brush = Colors.Black, Opacity = 0.1f, Offset = new Point(0, 2), Radius = 8 }
        };

        var contentStack = new VerticalStackLayout { Spacing = 16, StyleId = "ContentStack" };
        
        // Step 1: File Selection
        contentStack.Children.Add(BuildStep1FileSelection());
        
        // Step 2: Preview (hidden initially)
        contentStack.Children.Add(BuildStep2Preview());
        
        // Step 3: Import Progress (hidden initially)
        contentStack.Children.Add(BuildStep3Progress());
        
        // Step 4: Results (hidden initially)
        contentStack.Children.Add(BuildStep4Results());

        contentFrame.Content = contentStack;
        mainLayout.Children.Add(contentFrame);

        // Action buttons at bottom
        mainLayout.Children.Add(BuildActionButtons());

        Content = new ScrollView { Content = mainLayout };
        
        UpdateStepVisibility();
    }

    private View BuildHeader()
    {
        var headerStack = new VerticalStackLayout { Spacing = 4 };
        
        headerStack.Children.Add(new Label
        {
            Text = "?? SQL Import Wizard",
            FontSize = 28,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1976D2")
        });
        
        headerStack.Children.Add(new Label
        {
            Text = "Import historical data from VBA/Access WDPL database",
            FontSize = 14,
            TextColor = Color.FromArgb("#666666")
        });

        return headerStack;
    }

    private View BuildStepIndicator()
    {
        var stepGrid = new Grid
        {
            Margin = new Thickness(0, 20, 0, 0),
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(40) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(40) },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = new GridLength(40) },
                new ColumnDefinition { Width = GridLength.Star }
            }
        };

        // Step 1
        stepGrid.Add(CreateStepCircle(1, "Select", "Step1Circle", "Step1Label"), 0, 0);
        stepGrid.Add(CreateStepLine("Line1"), 1, 0);
        
        // Step 2
        stepGrid.Add(CreateStepCircle(2, "Preview", "Step2Circle", "Step2Label"), 2, 0);
        stepGrid.Add(CreateStepLine("Line2"), 3, 0);
        
        // Step 3
        stepGrid.Add(CreateStepCircle(3, "Import", "Step3Circle", "Step3Label"), 4, 0);
        stepGrid.Add(CreateStepLine("Line3"), 5, 0);
        
        // Step 4
        stepGrid.Add(CreateStepCircle(4, "Done", "Step4Circle", "Step4Label"), 6, 0);

        return stepGrid;
    }

    private View CreateStepCircle(int step, string label, string circleId, string labelId)
    {
        var stack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center, Spacing = 4 };
        
        var circle = new Border
        {
            WidthRequest = 36,
            HeightRequest = 36,
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 18 },
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            HorizontalOptions = LayoutOptions.Center,
            StyleId = circleId
        };
        
        circle.Content = new Label
        {
            Text = step.ToString(),
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center
        };
        
        stack.Children.Add(circle);
        stack.Children.Add(new Label
        {
            Text = label,
            FontSize = 11,
            TextColor = Color.FromArgb("#666666"),
            HorizontalTextAlignment = TextAlignment.Center,
            StyleId = labelId
        });

        return stack;
    }

    private View CreateStepLine(string lineId)
    {
        return new BoxView
        {
            HeightRequest = 3,
            BackgroundColor = Color.FromArgb("#E0E0E0"),
            VerticalOptions = LayoutOptions.Center,
            Margin = new Thickness(0, -10, 0, 0),
            StyleId = lineId
        };
    }

    private View BuildStep1FileSelection()
    {
        var step1 = new VerticalStackLayout { Spacing = 16, StyleId = "Step1Panel" };

        step1.Children.Add(new Label
        {
            Text = "Step 1: Select SQL File",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333")
        });

        step1.Children.Add(new Label
        {
            Text = "Choose a SQL dump file exported from phpMyAdmin or the VBA Access database.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666666")
        });

        // File drop zone
        var dropZone = new Border
        {
            StrokeThickness = 2,
            Stroke = Color.FromArgb("#90CAF9"),
            StrokeDashArray = new DoubleCollection { 5, 3 },
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            Padding = new Thickness(30, 40),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };

        var dropContent = new VerticalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.Center };
        dropContent.Children.Add(new Label
        {
            Text = "??",
            FontSize = 48,
            HorizontalOptions = LayoutOptions.Center
        });
        dropContent.Children.Add(new Label
        {
            Text = "No file selected",
            FontSize = 16,
            TextColor = Color.FromArgb("#1976D2"),
            HorizontalOptions = LayoutOptions.Center,
            StyleId = "FileLabel"
        });
        dropContent.Children.Add(new Label
        {
            Text = "Click to browse for .sql file",
            FontSize = 12,
            TextColor = Color.FromArgb("#666666"),
            HorizontalOptions = LayoutOptions.Center,
            StyleId = "FileSubLabel"
        });

        dropZone.Content = dropContent;
        
        var tapGesture = new TapGestureRecognizer();
        tapGesture.Tapped += OnSelectFileClicked;
        dropZone.GestureRecognizers.Add(tapGesture);
        
        step1.Children.Add(dropZone);

        // Supported tables info (collapsible)
        var infoExpander = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            Margin = new Thickness(0, 8, 0, 0)
        };

        var infoStack = new VerticalStackLayout { Spacing = 8 };
        infoStack.Children.Add(new Label
        {
            Text = "?? Supported Tables",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1976D2")
        });

        var tableList = new[]
        {
            ("tblleague", "Season settings"),
            ("tbldivisions", "Division names"),
            ("tblplayers", "Player names & IDs"),
            ("tblfixtures", "Match schedule"),
            ("tblmatchdetail", "Frame results")
        };

        foreach (var (table, desc) in tableList)
        {
            var row = new HorizontalStackLayout { Spacing = 8 };
            row.Children.Add(new Label { Text = "?", TextColor = Color.FromArgb("#4CAF50"), FontSize = 12 });
            row.Children.Add(new Label { Text = $"{table}", FontAttributes = FontAttributes.Bold, FontSize = 12, TextColor = Color.FromArgb("#333") });
            row.Children.Add(new Label { Text = $"- {desc}", FontSize = 12, TextColor = Color.FromArgb("#666") });
            infoStack.Children.Add(row);
        }

        infoExpander.Content = infoStack;
        step1.Children.Add(infoExpander);

        return step1;
    }

    private View BuildStep2Preview()
    {
        var step2 = new VerticalStackLayout { Spacing = 16, IsVisible = false, StyleId = "Step2Panel" };

        step2.Children.Add(new Label
        {
            Text = "Step 2: Review Data",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333")
        });

        step2.Children.Add(new Label
        {
            Text = "Review the data found in the SQL file before importing.",
            FontSize = 14,
            TextColor = Color.FromArgb("#666666")
        });

        // Data Summary Cards
        var summaryGrid = new Grid
        {
            ColumnDefinitions =
            {
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star },
                new ColumnDefinition { Width = GridLength.Star }
            },
            ColumnSpacing = 12,
            RowSpacing = 12,
            StyleId = "SummaryGrid"
        };

        summaryGrid.Add(CreateSummaryCard("??", "Season", "0", "SeasonCard"), 0, 0);
        summaryGrid.Add(CreateSummaryCard("??", "Players", "0", "PlayersCard"), 1, 0);
        summaryGrid.Add(CreateSummaryCard("??", "Fixtures", "0", "FixturesCard"), 2, 0);

        step2.Children.Add(summaryGrid);

        // Player Names Preview
        var playerPreviewFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            IsVisible = false,
            StyleId = "PlayerPreviewFrame"
        };

        var playerPreviewStack = new VerticalStackLayout { Spacing = 8 };
        playerPreviewStack.Children.Add(new Label
        {
            Text = "?? Player Names Found",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32")
        });

        var playerScroll = new ScrollView { HeightRequest = 150 };
        playerScroll.Content = new Label
        {
            Text = "",
            FontSize = 11,
            FontFamily = "Courier New",
            StyleId = "PlayerPreviewText"
        };
        playerPreviewStack.Children.Add(playerScroll);

        playerPreviewFrame.Content = playerPreviewStack;
        step2.Children.Add(playerPreviewFrame);

        // Tables Found
        var tablesFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };

        var tablesStack = new VerticalStackLayout { Spacing = 8 };
        tablesStack.Children.Add(new Label
        {
            Text = "?? Tables Found in SQL File",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333")
        });

        var tablesScroll = new ScrollView { HeightRequest = 120 };
        tablesScroll.Content = new Label
        {
            Text = "",
            FontSize = 11,
            FontFamily = "Courier New",
            StyleId = "TablesFoundText"
        };
        tablesStack.Children.Add(tablesScroll);

        tablesFrame.Content = tablesStack;
        step2.Children.Add(tablesFrame);

        // Duplicate handling info
        var dupeInfo = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#FF9800"),
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };

        var dupeStack = new VerticalStackLayout { Spacing = 4 };
        dupeStack.Children.Add(new Label
        {
            Text = "?? Duplicate Handling",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E65100")
        });
        dupeStack.Children.Add(new Label
        {
            Text = "• Players matched by name (case-insensitive)\n• Teams matched by name in same season\n• Fixtures matched by date + teams\n• Duplicates will be automatically skipped",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        dupeInfo.Content = dupeStack;
        step2.Children.Add(dupeInfo);

        return step2;
    }

    private View CreateSummaryCard(string icon, string title, string value, string cardId)
    {
        var card = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(12),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            StyleId = cardId
        };

        var stack = new VerticalStackLayout { HorizontalOptions = LayoutOptions.Center, Spacing = 4 };
        stack.Children.Add(new Label { Text = icon, FontSize = 24, HorizontalOptions = LayoutOptions.Center });
        stack.Children.Add(new Label { Text = value, FontSize = 20, FontAttributes = FontAttributes.Bold, HorizontalOptions = LayoutOptions.Center, StyleId = $"{cardId}Value" });
        stack.Children.Add(new Label { Text = title, FontSize = 12, TextColor = Color.FromArgb("#666"), HorizontalOptions = LayoutOptions.Center });

        card.Content = stack;
        return card;
    }

    private View BuildStep3Progress()
    {
        var step3 = new VerticalStackLayout { Spacing = 16, IsVisible = false, StyleId = "Step3Panel" };

        step3.Children.Add(new Label
        {
            Text = "Step 3: Importing Data",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#333333")
        });

        // Progress indicator
        var progressStack = new VerticalStackLayout { Spacing = 12, HorizontalOptions = LayoutOptions.Center };
        
        progressStack.Children.Add(new ActivityIndicator
        {
            IsRunning = true,
            Color = Color.FromArgb("#1976D2"),
            WidthRequest = 48,
            HeightRequest = 48,
            StyleId = "ImportSpinner"
        });

        progressStack.Children.Add(new Label
        {
            Text = "Processing...",
            FontSize = 16,
            TextColor = Color.FromArgb("#666"),
            HorizontalOptions = LayoutOptions.Center,
            StyleId = "ProgressLabel"
        });

        step3.Children.Add(progressStack);

        // Progress steps
        var stepsFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#E0E0E0"),
            BackgroundColor = Colors.White,
            Padding = new Thickness(16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };

        var stepsStack = new VerticalStackLayout { Spacing = 8, StyleId = "ImportStepsStack" };
        stepsStack.Children.Add(CreateProgressStep("Parsing SQL file...", "ParseStep"));
        stepsStack.Children.Add(CreateProgressStep("Creating season...", "SeasonStep"));
        stepsStack.Children.Add(CreateProgressStep("Importing divisions...", "DivisionStep"));
        stepsStack.Children.Add(CreateProgressStep("Importing teams...", "TeamStep"));
        stepsStack.Children.Add(CreateProgressStep("Importing players...", "PlayerStep"));
        stepsStack.Children.Add(CreateProgressStep("Importing fixtures...", "FixtureStep"));
        stepsStack.Children.Add(CreateProgressStep("Importing results...", "ResultStep"));
        stepsStack.Children.Add(CreateProgressStep("Saving data...", "SaveStep"));

        stepsFrame.Content = stepsStack;
        step3.Children.Add(stepsFrame);

        return step3;
    }

    private View CreateProgressStep(string text, string stepId)
    {
        var row = new HorizontalStackLayout { Spacing = 8, StyleId = stepId };
        row.Children.Add(new Label { Text = "?", TextColor = Color.FromArgb("#E0E0E0"), FontSize = 14, StyleId = $"{stepId}Icon" });
        row.Children.Add(new Label { Text = text, TextColor = Color.FromArgb("#999"), FontSize = 13, StyleId = $"{stepId}Text" });
        return row;
    }

    private View BuildStep4Results()
    {
        var step4 = new VerticalStackLayout { Spacing = 16, IsVisible = false, StyleId = "Step4Panel" };

        step4.Children.Add(new Label
        {
            Text = "? Import Complete!",
            FontSize = 20,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#4CAF50"),
            StyleId = "ResultsTitle"
        });

        // Results summary
        var resultsFrame = new Border
        {
            StrokeThickness = 2,
            Stroke = Color.FromArgb("#4CAF50"),
            BackgroundColor = Color.FromArgb("#E8F5E9"),
            Padding = new Thickness(16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };

        var resultsStack = new VerticalStackLayout { Spacing = 8 };
        resultsStack.Children.Add(new Label
        {
            Text = "Import Summary",
            FontSize = 16,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#2E7D32")
        });

        resultsStack.Children.Add(new Label
        {
            Text = "",
            FontSize = 13,
            StyleId = "ResultsSummaryText"
        });

        resultsFrame.Content = resultsStack;
        step4.Children.Add(resultsFrame);

        // Skipped items (if any)
        var skippedFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#FF9800"),
            BackgroundColor = Color.FromArgb("#FFF3E0"),
            Padding = new Thickness(16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            IsVisible = false,
            StyleId = "SkippedFrame"
        };

        var skippedStack = new VerticalStackLayout { Spacing = 4 };
        skippedStack.Children.Add(new Label
        {
            Text = "?? Skipped (Already Existed)",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#E65100")
        });
        skippedStack.Children.Add(new Label
        {
            Text = "",
            FontSize = 12,
            TextColor = Color.FromArgb("#666"),
            StyleId = "SkippedText"
        });

        skippedFrame.Content = skippedStack;
        step4.Children.Add(skippedFrame);

        // Warnings (if any)
        var warningsFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#F44336"),
            BackgroundColor = Color.FromArgb("#FFEBEE"),
            Padding = new Thickness(16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 },
            IsVisible = false,
            StyleId = "WarningsFrame"
        };

        var warningsStack = new VerticalStackLayout { Spacing = 4 };
        warningsStack.Children.Add(new Label
        {
            Text = "?? Warnings",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#C62828")
        });

        var warningsScroll = new ScrollView { HeightRequest = 120 };
        warningsScroll.Content = new Label
        {
            Text = "",
            FontSize = 11,
            TextColor = Color.FromArgb("#666"),
            StyleId = "WarningsText"
        };
        warningsStack.Children.Add(warningsScroll);

        warningsFrame.Content = warningsStack;
        step4.Children.Add(warningsFrame);

        // Next steps
        var nextStepsFrame = new Border
        {
            StrokeThickness = 1,
            Stroke = Color.FromArgb("#2196F3"),
            BackgroundColor = Color.FromArgb("#E3F2FD"),
            Padding = new Thickness(16),
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 8 }
        };

        var nextStepsStack = new VerticalStackLayout { Spacing = 4 };
        nextStepsStack.Children.Add(new Label
        {
            Text = "?? Next Steps",
            FontSize = 14,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#1565C0")
        });
        nextStepsStack.Children.Add(new Label
        {
            Text = "1. Go to Seasons page and update team names\n2. Verify player names are correct in Players page\n3. Activate the imported season when ready\n4. Review fixtures and results",
            FontSize = 12,
            TextColor = Color.FromArgb("#666")
        });

        nextStepsFrame.Content = nextStepsStack;
        step4.Children.Add(nextStepsFrame);

        return step4;
    }

    private View BuildActionButtons()
    {
        var buttonStack = new HorizontalStackLayout
        {
            Spacing = 12,
            HorizontalOptions = LayoutOptions.Fill,
            Margin = new Thickness(0, 20, 0, 0)
        };

        // Back button
        var backBtn = new Button
        {
            Text = "? Back",
            BackgroundColor = Color.FromArgb("#757575"),
            TextColor = Colors.White,
            FontSize = 15,
            HeightRequest = 48,
            CornerRadius = 8,
            IsVisible = false,
            WidthRequest = 100,
            StyleId = "BackButton"
        };
        backBtn.Clicked += OnBackClicked;
        buttonStack.Children.Add(backBtn);

        // Spacer
        buttonStack.Children.Add(new BoxView { HorizontalOptions = LayoutOptions.FillAndExpand, Color = Colors.Transparent });

        // Primary action button
        var primaryBtn = new Button
        {
            Text = "Select File",
            BackgroundColor = Color.FromArgb("#1976D2"),
            TextColor = Colors.White,
            FontSize = 15,
            HeightRequest = 48,
            CornerRadius = 8,
            Padding = new Thickness(24, 0),
            StyleId = "PrimaryButton"
        };
        primaryBtn.Clicked += OnPrimaryButtonClicked;
        buttonStack.Children.Add(primaryBtn);

        // Rollback button (hidden until import complete)
        var rollbackBtn = new Button
        {
            Text = "?? Undo Import",
            BackgroundColor = Color.FromArgb("#F44336"),
            TextColor = Colors.White,
            FontSize = 15,
            HeightRequest = 48,
            CornerRadius = 8,
            IsVisible = false,
            StyleId = "RollbackButton"
        };
        rollbackBtn.Clicked += OnRollbackClicked;
        buttonStack.Children.Add(rollbackBtn);

        return buttonStack;
    }

    private void UpdateStepVisibility()
    {
        var step1 = FindElement<VerticalStackLayout>("Step1Panel");
        var step2 = FindElement<VerticalStackLayout>("Step2Panel");
        var step3 = FindElement<VerticalStackLayout>("Step3Panel");
        var step4 = FindElement<VerticalStackLayout>("Step4Panel");
        var backBtn = FindElement<Button>("BackButton");
        var primaryBtn = FindElement<Button>("PrimaryButton");
        var rollbackBtn = FindElement<Button>("RollbackButton");

        if (step1 != null) step1.IsVisible = _currentStep == 1;
        if (step2 != null) step2.IsVisible = _currentStep == 2;
        if (step3 != null) step3.IsVisible = _currentStep == 3;
        if (step4 != null) step4.IsVisible = _currentStep == 4;

        if (backBtn != null) backBtn.IsVisible = _currentStep == 2;
        if (rollbackBtn != null) rollbackBtn.IsVisible = _currentStep == 4 && _lastImportResult != null;

        if (primaryBtn != null)
        {
            primaryBtn.IsVisible = _currentStep != 3;
            switch (_currentStep)
            {
                case 1:
                    primaryBtn.Text = _selectedFilePath != null ? "?? Preview Data" : "Select File";
                    primaryBtn.IsEnabled = true;
                    break;
                case 2:
                    primaryBtn.Text = "? Start Import";
                    primaryBtn.IsEnabled = true;
                    break;
                case 4:
                    primaryBtn.Text = "?? Import Another";
                    primaryBtn.IsEnabled = true;
                    break;
            }
        }

        UpdateStepIndicator();
    }

    private void UpdateStepIndicator()
    {
        for (int i = 1; i <= 4; i++)
        {
            var circle = FindElement<Border>($"Step{i}Circle");
            var label = FindElement<Label>($"Step{i}Label");
            
            if (circle != null)
            {
                if (i < _currentStep)
                {
                    circle.BackgroundColor = Color.FromArgb("#4CAF50"); // Completed
                }
                else if (i == _currentStep)
                {
                    circle.BackgroundColor = Color.FromArgb("#1976D2"); // Current
                }
                else
                {
                    circle.BackgroundColor = Color.FromArgb("#E0E0E0"); // Future
                }
            }

            if (i < 4)
            {
                var line = FindElement<BoxView>($"Line{i}");
                if (line != null)
                {
                    line.BackgroundColor = i < _currentStep ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0");
                }
            }
        }
    }

    private async void OnSelectFileClicked(object? sender, EventArgs e)
    {
        if (_isProcessing) return;

        try
        {
            var customFileType = new FilePickerFileType(
                new Dictionary<DevicePlatform, IEnumerable<string>>
                {
                    { DevicePlatform.WinUI, new[] { ".sql" } },
                    { DevicePlatform.MacCatalyst, new[] { "sql" } },
                    { DevicePlatform.Android, new[] { "*.sql" } },
                    { DevicePlatform.iOS, new[] { "sql" } }
                });

            var result = await FilePicker.PickAsync(new PickOptions
            {
                PickerTitle = "Select WDPL SQL dump file",
                FileTypes = customFileType
            });

            if (result != null)
            {
                _selectedFilePath = result.FullPath;
                _parsedData = null;

                var fileLabel = FindElement<Label>("FileLabel");
                var fileSubLabel = FindElement<Label>("FileSubLabel");
                
                if (fileLabel != null)
                {
                    var fileInfo = new FileInfo(result.FullPath);
                    fileLabel.Text = Path.GetFileName(result.FullPath);
                    fileLabel.TextColor = Color.FromArgb("#4CAF50");
                }
                
                if (fileSubLabel != null)
                {
                    var fileInfo = new FileInfo(result.FullPath);
                    fileSubLabel.Text = $"{fileInfo.Length / 1024} KB • Click to change";
                }

                UpdateStepVisibility();
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to select file: {ex.Message}", "OK");
        }
    }

    private async void OnPrimaryButtonClicked(object? sender, EventArgs e)
    {
        if (_isProcessing) return;

        switch (_currentStep)
        {
            case 1:
                if (_selectedFilePath == null)
                {
                    OnSelectFileClicked(sender, e);
                }
                else
                {
                    await PreviewDataAsync();
                }
                break;
            case 2:
                await RunImportAsync();
                break;
            case 4:
                ResetWizard();
                break;
        }
    }

    private void OnBackClicked(object? sender, EventArgs e)
    {
        if (_currentStep > 1 && !_isProcessing)
        {
            _currentStep = 1;
            UpdateStepVisibility();
        }
    }

    private async Task PreviewDataAsync()
    {
        if (string.IsNullOrEmpty(_selectedFilePath)) return;

        _isProcessing = true;
        
        try
        {
            _parsedData = await SqlFileImporter.ParseSqlFileAsync(_selectedFilePath);
            
            // Update summary cards
            UpdateSummaryCard("SeasonCard", _parsedData.Tables.ContainsKey("tblleague") ? "1" : "0");
            UpdateSummaryCard("PlayersCard", _parsedData.PlayerIdToName.Count.ToString());
            UpdateSummaryCard("FixturesCard", _parsedData.Tables.ContainsKey("tblfixtures") ? _parsedData.Tables["tblfixtures"].Count.ToString() : "0");

            // Update tables found
            var tablesText = FindElement<Label>("TablesFoundText");
            if (tablesText != null)
            {
                var sb = new System.Text.StringBuilder();
                foreach (var table in _parsedData.Tables.OrderBy(t => t.Key))
                {
                    sb.AppendLine($"? {table.Key}: {table.Value.Count} rows");
                }
                tablesText.Text = sb.ToString();
            }

            // Update player preview
            if (_parsedData.PlayerIdToName.Any())
            {
                var playerFrame = FindElement<Border>("PlayerPreviewFrame");
                var playerText = FindElement<Label>("PlayerPreviewText");
                
                if (playerFrame != null && playerText != null)
                {
                    playerFrame.IsVisible = true;
                    
                    var sb = new System.Text.StringBuilder();
                    var samples = _parsedData.PlayerIdToName.OrderBy(k => k.Key).Take(15).ToList();
                    
                    foreach (var kvp in samples)
                    {
                        var teamInfo = _parsedData.PlayerIdToTeamId.TryGetValue(kvp.Key, out var teamId) ? $" [Team {teamId}]" : "";
                        sb.AppendLine($"ID {kvp.Key,3} ? {kvp.Value}{teamInfo}");
                    }
                    
                    if (_parsedData.PlayerIdToName.Count > 15)
                    {
                        sb.AppendLine($"... and {_parsedData.PlayerIdToName.Count - 15} more");
                    }
                    
                    playerText.Text = sb.ToString();
                }
            }

            _currentStep = 2;
            UpdateStepVisibility();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to parse SQL file:\n\n{ex.Message}", "OK");
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void UpdateSummaryCard(string cardId, string value)
    {
        var valueLabel = FindElement<Label>($"${cardId}Value");
        if (valueLabel != null)
        {
            valueLabel.Text = value;
        }
    }

    private async Task RunImportAsync()
    {
        if (_selectedFilePath == null || _parsedData == null) return;

        _isProcessing = true;
        _currentStep = 3;
        UpdateStepVisibility();

        try
        {
            // Show progress
            UpdateProgressStep("ParseStep", true, "Parsing SQL file...");
            await Task.Delay(100);

            var (importedData, result) = await SqlFileImporter.ImportFromSqlFileAsync(
                _selectedFilePath,
                DataStore.Data,
                false);

            _lastImportResult = result;

            UpdateProgressStep("SeasonStep", true, $"Season: {result.DetectedSeason?.Name ?? "None"}");
            await Task.Delay(50);
            
            UpdateProgressStep("DivisionStep", true, $"Divisions: {result.ImportedDivisionIds.Count}");
            await Task.Delay(50);
            
            UpdateProgressStep("TeamStep", true, $"Teams: {result.TeamsImported} imported, {result.TeamsSkipped} skipped");
            await Task.Delay(50);
            
            UpdateProgressStep("PlayerStep", true, $"Players: {result.PlayersImported} imported, {result.PlayersSkipped} skipped");
            await Task.Delay(50);
            
            UpdateProgressStep("FixtureStep", true, $"Fixtures: {result.FixturesImported} imported, {result.FixturesSkipped} skipped");
            await Task.Delay(50);
            
            UpdateProgressStep("ResultStep", true, $"Results: {result.ResultsImported} matches, {result.FramesImported} frames");
            await Task.Delay(50);

            // Save data - ImportFromSqlFileAsync already adds to DataStore.Data
            DataStore.Save();
            
            UpdateProgressStep("SaveStep", true, "Data saved!");
            await Task.Delay(100);

            // Switch to the imported season so the user can see the data immediately
            if (result.DetectedSeason != null)
            {
                SeasonService.CurrentSeasonId = result.DetectedSeason.Id;
            }

            // Show results
            ShowResults(result);
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Import failed:\n\n{ex.Message}", "OK");
            _currentStep = 2;
            UpdateStepVisibility();
        }
        finally
        {
            _isProcessing = false;
        }
    }

    private void UpdateProgressStep(string stepId, bool completed, string text)
    {
        var iconLabel = FindElement<Label>($"{stepId}Icon");
        var textLabel = FindElement<Label>($"{stepId}Text");

        if (iconLabel != null)
        {
            iconLabel.Text = completed ? "?" : "?";
            iconLabel.TextColor = completed ? Color.FromArgb("#4CAF50") : Color.FromArgb("#E0E0E0");
        }

        if (textLabel != null)
        {
            textLabel.Text = text;
            textLabel.TextColor = completed ? Color.FromArgb("#333") : Color.FromArgb("#999");
        }
    }

    private void ShowResults(SqlFileImporter.SqlImportResult result)
    {
        _currentStep = 4;
        UpdateStepVisibility();

        // Update title based on success
        var title = FindElement<Label>("ResultsTitle");
        if (title != null)
        {
            title.Text = result.Success ? "? Import Complete!" : "?? Import Completed with Issues";
            title.TextColor = result.Success ? Color.FromArgb("#4CAF50") : Color.FromArgb("#FF9800");
        }

        // Summary
        var summaryText = FindElement<Label>("ResultsSummaryText");
        if (summaryText != null)
        {
            summaryText.Text = $"Season: {result.DetectedSeason?.Name ?? "None"}\n" +
                              $"Teams: {result.TeamsImported} imported\n" +
                              $"Players: {result.PlayersImported} imported\n" +
                              $"Fixtures: {result.FixturesImported} imported\n" +
                              $"Results: {result.ResultsImported} matches\n" +
                              $"Frames: {result.FramesImported} frame results";
        }

        // Skipped items
        var skippedFrame = FindElement<Border>("SkippedFrame");
        var skippedText = FindElement<Label>("SkippedText");
        if (skippedFrame != null && skippedText != null)
        {
            var hasSkipped = result.TeamsSkipped > 0 || result.PlayersSkipped > 0 || result.FixturesSkipped > 0;
            skippedFrame.IsVisible = hasSkipped;
            
            if (hasSkipped)
            {
                var sb = new System.Text.StringBuilder();
                if (result.TeamsSkipped > 0) sb.AppendLine($"• {result.TeamsSkipped} teams (already existed)");
                if (result.PlayersSkipped > 0) sb.AppendLine($"• {result.PlayersSkipped} players (already existed)");
                if (result.FixturesSkipped > 0) sb.AppendLine($"• {result.FixturesSkipped} fixtures (already existed)");
                skippedText.Text = sb.ToString().TrimEnd();
            }
        }

        // Warnings
        var warningsFrame = FindElement<Border>("WarningsFrame");
        var warningsText = FindElement<Label>("WarningsText");
        if (warningsFrame != null && warningsText != null)
        {
            warningsFrame.IsVisible = result.Warnings.Any() || result.Errors.Any();
            
            if (result.Warnings.Any() || result.Errors.Any())
            {
                var sb = new System.Text.StringBuilder();
                foreach (var warning in result.Warnings)
                {
                    sb.AppendLine($"?? {warning}");
                }
                foreach (var error in result.Errors)
                {
                    sb.AppendLine($"? {error}");
                }
                warningsText.Text = sb.ToString().TrimEnd();
            }
        }
    }

    private void ResetWizard()
    {
        _selectedFilePath = null;
        _parsedData = null;
        _lastImportResult = null;
        _currentStep = 1;

        var fileLabel = FindElement<Label>("FileLabel");
        var fileSubLabel = FindElement<Label>("FileSubLabel");
        
        if (fileLabel != null)
        {
            fileLabel.Text = "No file selected";
            fileLabel.TextColor = Color.FromArgb("#1976D2");
        }
        
        if (fileSubLabel != null)
        {
            fileSubLabel.Text = "Click to browse for .sql file";
        }

        var playerFrame = FindElement<Border>("PlayerPreviewFrame");
        if (playerFrame != null) playerFrame.IsVisible = false;

        UpdateStepVisibility();
    }

    private async void OnRollbackClicked(object? sender, EventArgs e)
    {
        if (_lastImportResult == null) return;

        var confirm = await DisplayAlert(
            "Confirm Rollback",
            $"This will remove ALL data from the last import:\n\n" +
            $"• {_lastImportResult.ImportedSeasonIds.Count} Season(s)\n" +
            $"• {_lastImportResult.ImportedDivisionIds.Count} Division(s)\n" +
            $"• {_lastImportResult.TeamsImported} Team(s)\n" +
            $"• {_lastImportResult.PlayersImported} Player(s)\n" +
            $"• {_lastImportResult.FixturesImported} Fixture(s)\n\n" +
            "This cannot be undone!",
            "Yes, Rollback",
            "Cancel");

        if (!confirm) return;

        try
        {
            SqlFileImporter.RollbackImport(DataStore.Data, _lastImportResult);
            DataStore.Save();

            await DisplayAlert("Success", "Import has been rolled back successfully.", "OK");
            ResetWizard();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Rollback failed:\n\n{ex.Message}", "OK");
        }
    }

    private T? FindElement<T>(string styleId) where T : Element
    {
        return FindInElement<T>(Content, styleId);
    }

    private T? FindInElement<T>(IView? element, string styleId) where T : Element
    {
        if (element == null) return null;

        if (element is T typedElement && typedElement.StyleId == styleId)
            return typedElement;

        if (element is Layout layout)
        {
            foreach (var child in layout.Children)
            {
                var found = FindInElement<T>(child, styleId);
                if (found != null) return found;
            }
        }

        if (element is ScrollView scrollView)
        {
            return FindInElement<T>(scrollView.Content, styleId);
        }

        if (element is Border border)
        {
            return FindInElement<T>(border.Content, styleId);
        }

        if (element is ContentView contentView)
        {
            return FindInElement<T>(contentView.Content, styleId);
        }

        return null;
    }
}
