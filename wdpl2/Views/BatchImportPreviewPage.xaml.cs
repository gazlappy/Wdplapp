using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

public partial class BatchImportPreviewPage : ContentPage
{
    private BatchImportPreview? _batchPreview;
    private readonly ObservableCollection<Season> _seasons = new();
    private readonly ObservableCollection<ImportFilePreview> _files = new();
    
    // Aggregated data across all files (deduplicated)
    private System.Collections.Generic.Dictionary<string, DivisionPreview> _aggregatedDivisions = new(StringComparer.OrdinalIgnoreCase);
    private System.Collections.Generic.Dictionary<string, TeamPreview> _aggregatedTeams = new(StringComparer.OrdinalIgnoreCase);
    private System.Collections.Generic.Dictionary<string, PlayerPreview> _aggregatedPlayers = new(StringComparer.OrdinalIgnoreCase);
    private System.Collections.Generic.List<HtmlLeagueParser.ExtractedResult> _aggregatedResults = new();

    public BatchImportPreviewPage()
    {
        InitializeComponent();
        
        SeasonPicker.ItemsSource = _seasons;
        FilesList.ItemsSource = _files;
    }

    public async Task LoadBatchPreviewAsync(System.Collections.Generic.List<string> filePaths)
    {
        try
        {
            System.Diagnostics.Debug.WriteLine($"Starting batch preview with {filePaths.Count} files");
            
            // Show loading indicator
            ImportButton.IsEnabled = false;
            LoadingBorder.IsVisible = true;
            LoadingLabel.Text = $"Loading {filePaths.Count} file(s)...";
            SummaryLabel.Text = $"Processing {filePaths.Count} file(s)...";

            // Allow UI to update
            await Task.Delay(100);

            // Load seasons first
            try
            {
                LoadSeasons();
                System.Diagnostics.Debug.WriteLine($"Loaded {_seasons.Count} seasons");
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading seasons: {ex.Message}");
            }

            SeasonSelectionBorder.IsVisible = true;
            await Task.Delay(50);

            // Clear aggregated data
            _aggregatedDivisions.Clear();
            _aggregatedTeams.Clear();
            _aggregatedPlayers.Clear();
            _aggregatedResults.Clear();

            // Process files
            const int batchSize = 10;
            var totalFiles = filePaths.Count;
            var processedFiles = new System.Collections.Generic.List<ImportFilePreview>();
            
            for (int i = 0; i < totalFiles; i += batchSize)
            {
                var batch = filePaths.Skip(i).Take(batchSize).ToList();
                var currentBatchEnd = Math.Min(i + batchSize, totalFiles);
                LoadingLabel.Text = $"Processing files {i + 1} - {currentBatchEnd} of {totalFiles}...";
                await Task.Delay(10);

                foreach (var filePath in batch)
                {
                    try
                    {
                        var filePreview = await ProcessFileAndAggregateAsync(filePath);
                        processedFiles.Add(filePreview);
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error processing {filePath}: {ex.Message}");
                        processedFiles.Add(new ImportFilePreview
                        {
                            FileName = System.IO.Path.GetFileName(filePath),
                            FilePath = filePath,
                            Status = FileImportStatus.Failed,
                            Errors = { $"Failed to process: {ex.Message}" }
                        });
                    }
                }
            }

            System.Diagnostics.Debug.WriteLine($"Processed {processedFiles.Count} files");
            System.Diagnostics.Debug.WriteLine($"Aggregated: {_aggregatedDivisions.Count} divisions, {_aggregatedTeams.Count} teams, {_aggregatedPlayers.Count} players, {_aggregatedResults.Count} results");

            // Create batch preview
            _batchPreview = new BatchImportPreview();
            foreach (var file in processedFiles)
            {
                _batchPreview.Files.Add(file);
            }

            LoadingBorder.IsVisible = false;

            if (_batchPreview.Files.Count == 0)
            {
                await DisplayAlert("No Data", "No files could be processed", "OK");
                await Navigation.PopAsync();
                return;
            }

            DisplayFiles();
            BatchActionsBorder.IsVisible = true;
            FilesListLabel.IsVisible = true;
            UpdateSummary();
            ShowAlerts();
            ImportButton.IsEnabled = true;
            
            System.Diagnostics.Debug.WriteLine("Batch preview loaded successfully");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Fatal error: {ex.Message}\n{ex.StackTrace}");
            LoadingBorder.IsVisible = false;
            
            try { await DisplayAlert("Error", $"Failed to load batch preview: {ex.Message}", "OK"); } catch { }
            try { await Navigation.PopAsync(); } catch { }
        }
    }

    /// <summary>
    /// Process a single HTML file and aggregate data (deduplicating across all files)
    /// </summary>
    private async Task<ImportFilePreview> ProcessFileAndAggregateAsync(string filePath)
    {
        var filePreview = new ImportFilePreview
        {
            FileName = System.IO.Path.GetFileName(filePath),
            FilePath = filePath
        };

        try
        {
            var fileInfo = new System.IO.FileInfo(filePath);
            filePreview.FileSizeBytes = fileInfo.Length;

            var htmlResult = await Task.Run(async () => await HtmlLeagueParser.ParseHtmlFileAsync(filePath));
            
            filePreview.PageTitle = htmlResult.PageHeading ?? htmlResult.PageTitle ?? filePreview.FileName;
            filePreview.TablesFound = htmlResult.Tables.Count;
            filePreview.HasLeagueTable = htmlResult.HasLeagueTable;
            filePreview.HasResults = htmlResult.HasResults;
            filePreview.HasPlayerStats = htmlResult.HasPlayerStats;
            filePreview.HasFixtures = htmlResult.HasFixtures;

            if (!htmlResult.Success)
            {
                filePreview.Errors.AddRange(htmlResult.Errors);
                filePreview.Status = FileImportStatus.Failed;
                return filePreview;
            }

            // Create minimal preview for this file (just for display)
            filePreview.Preview = new ImportPreview
            {
                FileName = filePreview.FileName,
                FileType = ".html"
            };

            // === AGGREGATE DIVISIONS ===
            if (!string.IsNullOrWhiteSpace(htmlResult.DetectedDivision))
            {
                var divName = NormalizeDivisionName(htmlResult.DetectedDivision);
                var divKey = divName.ToLowerInvariant();
                if (!_aggregatedDivisions.ContainsKey(divKey))
                {
                    _aggregatedDivisions[divKey] = new DivisionPreview { Name = divName };
                }
            }

            // Divisions from results
            foreach (var result in htmlResult.Results)
            {
                if (!string.IsNullOrWhiteSpace(result.Division))
                {
                    var divName = NormalizeDivisionName(result.Division);
                    var divKey = divName.ToLowerInvariant();
                    if (!_aggregatedDivisions.ContainsKey(divKey))
                    {
                        _aggregatedDivisions[divKey] = new DivisionPreview { Name = divName };
                    }
                }
            }

            // === AGGREGATE TEAMS ===
            foreach (var team in htmlResult.Teams)
            {
                var teamName = NormalizeTeamName(team.Name);
                var teamKey = GetTeamKey(team.Name);
                
                if (!_aggregatedTeams.ContainsKey(teamKey))
                {
                    var divName = NormalizeDivisionName(team.Division);
                    _aggregatedTeams[teamKey] = new TeamPreview
                    {
                        Name = teamName,
                        DivisionName = divName,
                        IsWinner = team.Position == 1,
                        IsRunnerUp = team.Position == 2
                    };
                    
                    // Update division winner/runner-up
                    var divKey = divName.ToLowerInvariant();
                    if (_aggregatedDivisions.TryGetValue(divKey, out var div))
                    {
                        if (team.Position == 1) div.WinnerTeam = teamName;
                        if (team.Position == 2) div.RunnerUpTeam = teamName;
                    }
                }
            }

            // Teams from results
            foreach (var result in htmlResult.Results)
            {
                if (!string.IsNullOrWhiteSpace(result.HomeTeam))
                {
                    var teamName = NormalizeTeamName(result.HomeTeam);
                    var teamKey = GetTeamKey(result.HomeTeam);
                    if (!_aggregatedTeams.ContainsKey(teamKey))
                    {
                        _aggregatedTeams[teamKey] = new TeamPreview
                        {
                            Name = teamName,
                            DivisionName = NormalizeDivisionName(result.Division)
                        };
                    }
                }
                if (!string.IsNullOrWhiteSpace(result.AwayTeam))
                {
                    var teamName = NormalizeTeamName(result.AwayTeam);
                    var teamKey = GetTeamKey(result.AwayTeam);
                    if (!_aggregatedTeams.ContainsKey(teamKey))
                    {
                        _aggregatedTeams[teamKey] = new TeamPreview
                        {
                            Name = teamName,
                            DivisionName = NormalizeDivisionName(result.Division)
                        };
                    }
                }
            }

            // === AGGREGATE PLAYERS ===
            foreach (var player in htmlResult.Players)
            {
                var nameParts = player.Name.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var firstName = NormalizePlayerName(nameParts.Length > 0 ? nameParts[0] : "");
                var lastName = NormalizePlayerName(nameParts.Length > 1 ? nameParts[1] : "");
                var playerKey = GetPlayerKey(firstName, lastName);
                
                if (!_aggregatedPlayers.ContainsKey(playerKey))
                {
                    _aggregatedPlayers[playerKey] = new PlayerPreview
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        TeamName = NormalizeTeamName(player.TeamName)
                    };
                }
            }

            // Player from profile
            if (htmlResult.PlayerProfile != null)
            {
                var profile = htmlResult.PlayerProfile;
                var nameParts = profile.PlayerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var firstName = NormalizePlayerName(nameParts.Length > 0 ? nameParts[0] : "");
                var lastName = NormalizePlayerName(nameParts.Length > 1 ? nameParts[1] : "");
                var playerKey = GetPlayerKey(firstName, lastName);
                
                if (!_aggregatedPlayers.ContainsKey(playerKey))
                {
                    _aggregatedPlayers[playerKey] = new PlayerPreview
                    {
                        FirstName = firstName,
                        LastName = lastName,
                        TeamName = NormalizeTeamName(profile.TeamName)
                    };
                }
                
                // Add opponents from match history
                foreach (var match in profile.MatchHistory)
                {
                    var oppParts = match.OpponentName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    var oppFirst = NormalizePlayerName(oppParts.Length > 0 ? oppParts[0] : "");
                    var oppLast = NormalizePlayerName(oppParts.Length > 1 ? oppParts[1] : "");
                    var oppKey = GetPlayerKey(oppFirst, oppLast);
                    
                    if (!_aggregatedPlayers.ContainsKey(oppKey))
                    {
                        _aggregatedPlayers[oppKey] = new PlayerPreview
                        {
                            FirstName = oppFirst,
                            LastName = oppLast,
                            TeamName = NormalizeTeamName(match.OpponentTeam)
                        };
                    }
                }
            }

            // === AGGREGATE RESULTS (fixtures) ===
            foreach (var result in htmlResult.Results)
            {
                // Normalize team names for comparison
                var homeTeam = NormalizeTeamName(result.HomeTeam);
                var awayTeam = NormalizeTeamName(result.AwayTeam);
                
                // Check for duplicate result (same date, same teams)
                var isDuplicate = _aggregatedResults.Any(r =>
                    r.Date == result.Date &&
                    GetTeamKey(r.HomeTeam) == GetTeamKey(result.HomeTeam) &&
                    GetTeamKey(r.AwayTeam) == GetTeamKey(result.AwayTeam));
                    
                if (!isDuplicate)
                {
                    // Store with normalized names
                    _aggregatedResults.Add(new HtmlLeagueParser.ExtractedResult
                    {
                        Date = result.Date,
                        Division = NormalizeDivisionName(result.Division),
                        HomeTeam = homeTeam,
                        HomeScore = result.HomeScore,
                        AwayTeam = awayTeam,
                        AwayScore = result.AwayScore
                    });
                }
            }

            filePreview.Warnings.AddRange(htmlResult.Warnings);
            filePreview.Status = FileImportStatus.Pending;
            
            System.Diagnostics.Debug.WriteLine($"  {filePreview.FileName}: Type={htmlResult.DetectedPageType}, Div={htmlResult.DetectedDivision}");
        }
        catch (Exception ex)
        {
            filePreview.Errors.Add($"Error reading file: {ex.Message}");
            filePreview.Status = FileImportStatus.Failed;
        }

        return filePreview;
    }

    private void LoadSeasons()
    {
        _seasons.Clear();
        var seasons = DataStore.Data?.Seasons;
        if (seasons == null || !seasons.Any()) return;

        foreach (var season in seasons.OrderByDescending(s => s.StartDate))
            _seasons.Add(season);

        if (_seasons.Any())
        {
            var currentSeason = _seasons.FirstOrDefault(s => s.Id == SeasonService.CurrentSeasonId);
            SeasonPicker.SelectedItem = currentSeason ?? _seasons.First();
        }
    }

    private void DisplayFiles()
    {
        if (_batchPreview == null) return;

        _files.Clear();
        var filesToShow = _batchPreview.Files.Take(50).ToList();
        foreach (var file in filesToShow)
            _files.Add(file);

        if (_batchPreview.Files.Count > 50)
        {
            AlertsStack.Children.Add(new Label
            {
                Text = $"?? Showing first 50 of {_batchPreview.Files.Count} files.",
                TextColor = Colors.White,
                FontSize = 12
            });
            AlertsBorder.IsVisible = true;
        }
    }

    private void UpdateSummary()
    {
        if (_batchPreview == null) return;

        var totalFiles = _batchPreview.TotalFiles;
        var selectedFiles = _batchPreview.SelectedFiles;
        
        // Use AGGREGATED counts (deduplicated)
        SummaryLabel.Text = $"Files: {totalFiles} ({selectedFiles} selected) | " +
            $"Found: {_aggregatedDivisions.Count} divisions, {_aggregatedTeams.Count} teams, " +
            $"{_aggregatedPlayers.Count} players, {_aggregatedResults.Count} fixtures";
    }

    private void ShowAlerts()
    {
        if (_batchPreview == null) return;

        var hasIssues = AlertsStack.Children.Count > 0;

        if (_aggregatedTeams.Count == 0 && _aggregatedPlayers.Count == 0)
        {
            hasIssues = true;
            AlertsStack.Children.Add(new Label
            {
                Text = "?? No league data detected in these files",
                TextColor = Colors.White,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            });
        }

        var filesWithErrors = _batchPreview.Files.Count(f => f.HasErrors);
        if (filesWithErrors > 0)
        {
            hasIssues = true;
            AlertsStack.Children.Add(new Label
            {
                Text = $"? {filesWithErrors} file(s) have errors",
                TextColor = Colors.White,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            });
        }

        AlertsBorder.IsVisible = hasIssues;
    }

    private void OnSeasonChanged(object? sender, EventArgs e)
    {
        var season = SeasonPicker.SelectedItem as Season;
        if (season != null)
        {
            if (_batchPreview != null)
            {
                _batchPreview.TargetSeasonId = season.Id;
                _batchPreview.TargetSeasonName = season.Name;
            }
            SeasonLabel.Text = $"Target Season: {season.Name}";
        }
    }

    private async void OnCreateSeasonClicked(object? sender, EventArgs e)
    {
        var seasonName = await DisplayPromptAsync("New Season", "Enter season name:", initialValue: $"Season {DateTime.Now.Year}");
        if (string.IsNullOrWhiteSpace(seasonName)) return;

        var season = new Season { Id = Guid.NewGuid(), Name = seasonName, StartDate = DateTime.Now, IsActive = false };
        DataStore.Data.Seasons.Add(season);
        DataStore.Save();
        _seasons.Add(season);
        SeasonPicker.SelectedItem = season;
        await DisplayAlert("Success", $"Created season: {seasonName}", "OK");
    }

    private void OnSelectAllFilesClicked(object? sender, EventArgs e)
    {
        if (_batchPreview == null) return;
        foreach (var file in _batchPreview.Files) file.Include = true;
        foreach (var file in _files) file.Include = true;
        RefreshFilesList();
        UpdateSummary();
    }

    private void OnDeselectAllFilesClicked(object? sender, EventArgs e)
    {
        if (_batchPreview == null) return;
        foreach (var file in _batchPreview.Files) file.Include = false;
        foreach (var file in _files) file.Include = false;
        RefreshFilesList();
        UpdateSummary();
    }

    private void RefreshFilesList()
    {
        var temp = FilesList.ItemsSource;
        FilesList.ItemsSource = null;
        FilesList.ItemsSource = temp;
    }

    private async void OnImportClicked(object? sender, EventArgs e)
    {
        try
        {
            if (_batchPreview == null)
            {
                await DisplayAlert("Error", "No preview data available", "OK");
                return;
            }

            var selectedSeason = SeasonPicker.SelectedItem as Season;
            if (selectedSeason == null)
            {
                await DisplayAlert("No Season", "Please select a target season", "OK");
                return;
            }

            var selectedFiles = _batchPreview.Files.Count(f => f.Include);
            if (selectedFiles == 0)
            {
                await DisplayAlert("Nothing Selected", "Please select at least one file to import", "OK");
                return;
            }

            var confirm = await DisplayAlert(
                "Confirm Batch Import",
                $"Import data into {selectedSeason.Name}?\n\n" +
                $"This will add:\n" +
                $"• {_aggregatedDivisions.Count} divisions\n" +
                $"• {_aggregatedTeams.Count} teams\n" +
                $"• {_aggregatedPlayers.Count} players\n" +
                $"• {_aggregatedResults.Count} fixtures/results",
                "Yes, Import All",
                "Cancel");

            if (!confirm) return;

            ImportButton.IsEnabled = false;
            ImportButton.Text = "Importing...";
            ProgressBorder.IsVisible = true;
            ProgressBar.Progress = 0;
            ProgressLabel.Text = "Importing data...";

            // Import all aggregated data at once
            var result = await ImportAggregatedDataAsync(selectedSeason.Id);

            ProgressBorder.IsVisible = false;

            if (result.Success)
            {
                DataStore.Save();
                await DisplayAlert("Batch Import Complete!", result.Summary, "OK");
                await Navigation.PopAsync();
            }
            else
            {
                await DisplayAlert("Import Failed", result.Summary + "\n\n" + string.Join("\n", result.Errors.Take(5)), "OK");
                ImportButton.IsEnabled = true;
                ImportButton.Text = "Import All Selected";
            }
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Batch import failed: {ex.Message}", "OK");
            ImportButton.IsEnabled = true;
            ImportButton.Text = "Import All Selected";
            ProgressBorder.IsVisible = false;
        }
    }

    /// <summary>
    /// Import all aggregated data at once (deduplicated)
    /// </summary>
    private async Task<ImportResult> ImportAggregatedDataAsync(Guid seasonId)
    {
        var result = new ImportResult();
        var data = DataStore.Data;

        try
        {
            // Track created entities for linking
            var divisionMap = new System.Collections.Generic.Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);
            var teamMap = new System.Collections.Generic.Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            // 1. Import Divisions
            ProgressLabel.Text = "Importing divisions...";
            ProgressBar.Progress = 0.1;
            await Task.Delay(10);

            foreach (var divEntry in _aggregatedDivisions)
            {
                var divPreview = divEntry.Value;
                
                // Check if division already exists for this season
                var existingDiv = data.Divisions.FirstOrDefault(d => 
                    d.SeasonId == seasonId && 
                    d.Name.Equals(divPreview.Name, StringComparison.OrdinalIgnoreCase));

                if (existingDiv != null)
                {
                    divisionMap[divPreview.Name.ToLowerInvariant()] = existingDiv.Id;
                    result.DivisionsSkipped++;
                }
                else
                {
                    var division = new Division
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId,
                        Name = divPreview.Name
                    };
                    data.Divisions.Add(division);
                    divisionMap[divPreview.Name.ToLowerInvariant()] = division.Id;
                    result.DivisionsCreated++;
                }
            }

            // 2. Import Teams
            ProgressLabel.Text = "Importing teams...";
            ProgressBar.Progress = 0.3;
            await Task.Delay(10);

            foreach (var teamEntry in _aggregatedTeams)
            {
                var teamPreview = teamEntry.Value;
                var teamKey = teamEntry.Key; // This is already the normalized key
                
                // Check if team already exists for this season (using normalized comparison)
                var existingTeam = data.Teams.FirstOrDefault(t => 
                    t.SeasonId == seasonId && 
                    GetTeamKey(t.Name) == teamKey);

                if (existingTeam != null)
                {
                    teamMap[teamKey] = existingTeam.Id;
                    result.TeamsSkipped++;
                }
                else
                {
                    // Get division ID
                    Guid? divId = null;
                    if (!string.IsNullOrWhiteSpace(teamPreview.DivisionName))
                    {
                        var divKey = teamPreview.DivisionName.ToLowerInvariant();
                        if (divisionMap.TryGetValue(divKey, out var mappedDivId))
                        {
                            divId = mappedDivId;
                        }
                    }

                    var team = new Team
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId,
                        Name = teamPreview.Name, // Already normalized to UPPERCASE
                        DivisionId = divId
                    };
                    data.Teams.Add(team);
                    teamMap[teamKey] = team.Id;
                    result.TeamsCreated++;
                }
            }

            // 3. Import Players
            ProgressLabel.Text = "Importing players...";
            ProgressBar.Progress = 0.5;
            await Task.Delay(10);

            foreach (var playerEntry in _aggregatedPlayers)
            {
                var playerPreview = playerEntry.Value;
                
                // Check if player already exists for this season
                var existingPlayer = data.Players.FirstOrDefault(p => 
                    p.SeasonId == seasonId && 
                    p.FirstName.Equals(playerPreview.FirstName, StringComparison.OrdinalIgnoreCase) &&
                    p.LastName.Equals(playerPreview.LastName, StringComparison.OrdinalIgnoreCase));

                if (existingPlayer != null)
                {
                    result.PlayersSkipped++;
                }
                else
                {
                    // Get team ID using normalized key
                    Guid? teamId = null;
                    if (!string.IsNullOrWhiteSpace(playerPreview.TeamName))
                    {
                        var teamKey = GetTeamKey(playerPreview.TeamName);
                        if (teamMap.TryGetValue(teamKey, out var mappedTeamId))
                        {
                            teamId = mappedTeamId;
                        }
                    }

                    var player = new Player
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId,
                        FirstName = playerPreview.FirstName,
                        LastName = playerPreview.LastName,
                        TeamId = teamId
                    };
                    data.Players.Add(player);
                    result.PlayersCreated++;
                }
            }

            // 4. Import Fixtures/Results
            ProgressLabel.Text = "Importing fixtures...";
            ProgressBar.Progress = 0.7;
            await Task.Delay(10);

            foreach (var matchResult in _aggregatedResults)
            {
                // Get team IDs using normalized keys
                var homeTeamKey = GetTeamKey(matchResult.HomeTeam);
                var awayTeamKey = GetTeamKey(matchResult.AwayTeam);
                
                teamMap.TryGetValue(homeTeamKey, out var homeTeamId);
                teamMap.TryGetValue(awayTeamKey, out var awayTeamId);

                // Skip if we don't have valid team IDs
                if (homeTeamId == Guid.Empty || awayTeamId == Guid.Empty)
                {
                    result.FixturesSkipped++;
                    continue;
                }

                // Check if fixture already exists
                var existingFixture = data.Fixtures.FirstOrDefault(f =>
                    f.SeasonId == seasonId &&
                    f.Date.Date == matchResult.Date.Date &&
                    f.HomeTeamId == homeTeamId &&
                    f.AwayTeamId == awayTeamId);

                if (existingFixture != null)
                {
                    result.FixturesSkipped++;
                }
                else
                {
                    // Get division ID
                    Guid? divId = null;
                    if (!string.IsNullOrWhiteSpace(matchResult.Division))
                    {
                        var divKey = matchResult.Division.ToLowerInvariant();
                        if (divisionMap.TryGetValue(divKey, out var mappedDivId))
                        {
                            divId = mappedDivId;
                        }
                    }

                    var fixture = new Fixture
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId,
                        DivisionId = divId,
                        Date = matchResult.Date,
                        HomeTeamId = homeTeamId,
                        AwayTeamId = awayTeamId
                    };
                    
                    // Create frame results to represent the aggregate scores
                    var totalFrames = matchResult.HomeScore + matchResult.AwayScore;
                    for (int frameNum = 1; frameNum <= totalFrames; frameNum++)
                    {
                        var isHomeWin = frameNum <= matchResult.HomeScore;
                        fixture.Frames.Add(new FrameResult
                        {
                            Number = frameNum,
                            Winner = isHomeWin ? FrameWinner.Home : FrameWinner.Away
                        });
                    }
                    
                    data.Fixtures.Add(fixture);
                    result.FixturesCreated++;
                }
            }

            ProgressLabel.Text = "Complete!";
            ProgressBar.Progress = 1.0;
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add(ex.Message);
            result.Success = false;
        }

        return result;
    }

    private async void OnCancelClicked(object? sender, EventArgs e)
    {
        var confirm = await DisplayAlert("Cancel Batch Import", "Are you sure you want to cancel?", "Yes, Cancel", "No");
        if (confirm) await Navigation.PopAsync();
    }

    // Simple result class
    private class ImportResult
    {
        public bool Success { get; set; }
        public int DivisionsCreated { get; set; }
        public int DivisionsSkipped { get; set; }
        public int TeamsCreated { get; set; }
        public int TeamsSkipped { get; set; }
        public int PlayersCreated { get; set; }
        public int PlayersSkipped { get; set; }
        public int FixturesCreated { get; set; }
        public int FixturesUpdated { get; set; }
        public int FixturesSkipped { get; set; }
        public System.Collections.Generic.List<string> Errors { get; set; } = new();

        public string Summary => 
            $"Created:\n" +
            $"• {DivisionsCreated} divisions\n" +
            $"• {TeamsCreated} teams\n" +
            $"• {PlayersCreated} players\n" +
            $"• {FixturesCreated} fixtures\n\n" +
            $"Skipped (already exist):\n" +
            $"• {DivisionsSkipped} divisions\n" +
            $"• {TeamsSkipped} teams\n" +
            $"• {PlayersSkipped} players\n" +
            $"• {FixturesSkipped} fixtures";
    }

    /// <summary>
    /// Normalize team name for consistent storage and comparison
    /// Converts to uppercase and standardizes punctuation
    /// </summary>
    private string NormalizeTeamName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
            
        // Convert to uppercase
        name = name.ToUpperInvariant();
        
        // Standardize apostrophes and quotes
        name = name.Replace("'", "'").Replace("'", "'").Replace("`", "'");
        
        return name.Trim();
    }

    /// <summary>
    /// Create a key for team deduplication (removes punctuation for comparison)
    /// </summary>
    private string GetTeamKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
            
        // Normalize first
        name = NormalizeTeamName(name);
        
        // Remove apostrophes and common punctuation for comparison
        name = name.Replace("'", "")
                   .Replace(".", "")
                   .Replace(",", "")
                   .Replace("-", " ")
                   .Replace("&", "AND");
        
        // Remove extra spaces
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
        
        return name;
    }

    /// <summary>
    /// Normalize player name for consistent storage
    /// </summary>
    private string NormalizePlayerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
            
        // Title case for player names
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 1)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            else
                parts[i] = parts[i].ToUpperInvariant();
        }
        return string.Join(" ", parts);
    }

    /// <summary>
    /// Create a key for player deduplication (removes case and punctuation differences)
    /// </summary>
    private string GetPlayerKey(string firstName, string lastName)
    {
        return $"{NormalizePlayerName(firstName)} {NormalizePlayerName(lastName)}".Trim();
    }

    /// <summary>
    /// Normalize division name for consistent storage
    /// </summary>
    private string NormalizeDivisionName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
            
        // Title case for division names
        var parts = name.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        for (int i = 0; i < parts.Length; i++)
        {
            if (parts[i].Length > 1)
                parts[i] = char.ToUpperInvariant(parts[i][0]) + parts[i].Substring(1).ToLowerInvariant();
            else
                parts[i] = parts[i].ToUpperInvariant();
        }
        return string.Join(" ", parts);
    }
}
