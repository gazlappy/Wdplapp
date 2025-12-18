using Microsoft.Maui.Controls;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Wdpl2.Views;

public partial class SourceCodeViewerPage : ContentPage
{
    private readonly Dictionary<string, string> _sourceFiles = new();
    
    public SourceCodeViewerPage()
    {
        InitializeComponent();
        LoadSourceFiles();
    }

    private void LoadSourceFiles()
    {
        // Try to find the original source files
        var basePaths = new[]
        {
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"Pool League 1.1\Pool League 1.1\plm modules"),
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\..\Pool League 1.1\Pool League 1.1\plm modules"),
            @"C:\Users\bobgc\source\repos\gazlappy\Wdplapp\wdpl2\Pool League 1.1\Pool League 1.1\plm modules"
        };

        string? foundPath = null;
        foreach (var path in basePaths)
        {
            try
            {
                var normalizedPath = Path.GetFullPath(path);
                if (Directory.Exists(normalizedPath))
                {
                    foundPath = normalizedPath;
                    break;
                }
            }
            catch { }
        }

        if (foundPath != null)
        {
            // Load .pas files
            try
            {
                var pasFiles = Directory.GetFiles(foundPath, "*.pas", SearchOption.TopDirectoryOnly)
                    .Where(f => !f.EndsWith(".~pas", StringComparison.OrdinalIgnoreCase))
                    .OrderBy(f => Path.GetFileName(f))
                    .ToList();

                foreach (var file in pasFiles)
                {
                    var fileName = Path.GetFileName(file);
                    _sourceFiles[fileName] = file;
                }

                // Also try to load the .dpr project file
                var dprPath = Path.Combine(Path.GetDirectoryName(foundPath) ?? "", "plm", "plm.dpr");
                if (File.Exists(dprPath))
                {
                    _sourceFiles["plm.dpr (Project)"] = dprPath;
                }
            }
            catch { }
        }

        // Add embedded sample if no files found
        if (_sourceFiles.Count == 0)
        {
            _sourceFiles["(Sample) main.pas"] = "EMBEDDED_SAMPLE";
        }

        // Populate picker
        FilePicker.ItemsSource = _sourceFiles.Keys.ToList();
        
        if (_sourceFiles.Count > 0)
        {
            // Select main.pas by default if available
            var mainIndex = _sourceFiles.Keys.ToList().FindIndex(k => k.Contains("main", StringComparison.OrdinalIgnoreCase));
            FilePicker.SelectedIndex = mainIndex >= 0 ? mainIndex : 0;
        }
    }

    private void OnFileSelected(object? sender, EventArgs e)
    {
        if (FilePicker.SelectedItem is not string selectedFile)
            return;

        if (!_sourceFiles.TryGetValue(selectedFile, out var filePath))
            return;

        string sourceCode;
        
        if (filePath == "EMBEDDED_SAMPLE")
        {
            sourceCode = GetEmbeddedSampleCode();
        }
        else
        {
            try
            {
                sourceCode = File.ReadAllText(filePath);
            }
            catch (Exception ex)
            {
                sourceCode = $"// Error reading file: {ex.Message}";
            }
        }

        SourceCodeLabel.Text = sourceCode;
    }

    private static string GetEmbeddedSampleCode()
    {
        return @"unit main;

{ ============================================== }
{  Admin4Pool - Pool League Manager              }
{  Main Form Unit                                }
{  Originally developed circa 2001              }
{  Written in Borland Delphi                    }
{ ============================================== }

interface

uses
  Windows, Messages, SysUtils, Classes, Graphics, Controls, Forms, Dialogs,
  StdCtrls, ExtCtrls, ComCtrls, Menus, DBCtrls, Grids, DBGrids, DB,
  DBTables, ImgList, Buttons;

type
  TForm1 = class(TForm)
    MainMenu1: TMainMenu;
    File1: TMenuItem;
    Exit1: TMenuItem;
    Help1: TMenuItem;
    About1: TMenuItem;
    StatusBar1: TStatusBar;
    PageControl1: TPageControl;
    TabSheet1: TTabSheet;  // Divisions
    TabSheet2: TTabSheet;  // Teams
    TabSheet3: TTabSheet;  // Players
    TabSheet4: TTabSheet;  // Fixtures
    TabSheet5: TTabSheet;  // Results
    TabSheet6: TTabSheet;  // Tables
    TabSheet7: TTabSheet;  // Ratings
    // ... many more components
    
    procedure FormCreate(Sender: TObject);
    procedure Exit1Click(Sender: TObject);
    procedure About1Click(Sender: TObject);
    procedure FormClose(Sender: TObject; var Action: TCloseAction);
    // ... many more event handlers
    
  private
    { Private declarations }
    FCurrentLeague: String;
    FCurrentSeason: Integer;
    procedure UpdateStatusBar;
    procedure RefreshAllGrids;
    procedure CalculateRatings;
    procedure GenerateWebPages;
    
  public
    { Public declarations }
    property CurrentLeague: String read FCurrentLeague write FCurrentLeague;
    property CurrentSeason: Integer read FCurrentSeason write FCurrentSeason;
  end;

var
  Form1: TForm1;

implementation

uses datamodule, about, player, team, division, venue, update;

{$R *.DFM}

procedure TForm1.FormCreate(Sender: TObject);
begin
  // Initialize the application
  FCurrentSeason := 1;
  FCurrentLeague := 'WDPL';
  
  // Connect to Paradox database
  DM1.Database1.DatabaseName := ExtractFilePath(Application.ExeName) + 'Data';
  DM1.Database1.Connected := True;
  
  // Refresh displays
  RefreshAllGrids;
  UpdateStatusBar;
end;

procedure TForm1.CalculateRatings;
var
  BaseRating, WinFactor, LossFactor: Double;
  BiasWeighting: Integer;
begin
  // VBA-style rating calculation
  // This is the algorithm that WDPL2 now replicates!
  
  BaseRating := 1000;      // Starting rating
  WinFactor := 1.25;       // RATINGWIN
  LossFactor := 0.75;      // RATINGLOSE
  BiasWeighting := 220;    // Base weighting
  
  // Calculate player ratings using progressive weighting
  // Newer frames weighted more heavily than older ones
  // Formula: Rating = Sum(RatingAttn * BiasX) / Sum(BiasX)
  
  // ... rating calculation code ...
end;

procedure TForm1.About1Click(Sender: TObject);
begin
  AboutBox.ShowModal;
end;

procedure TForm1.Exit1Click(Sender: TObject);
begin
  Close;
end;

end.

{ ============================================== }
{  This is a sample/reconstruction of the        }
{  original Delphi source code structure.        }
{  The actual source files may be available      }
{  in the Pool League 1.1 folder.               }
{ ============================================== }
";
    }
}
