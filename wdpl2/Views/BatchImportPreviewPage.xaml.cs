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
    private System.Collections.Generic.List<PlayerFrameRecord> _aggregatedFrameResults = new();
    
    // Track potential player duplicates for merge suggestions
    private System.Collections.Generic.List<PlayerMergeSuggestion> _playerMergeSuggestions = new();

    /// <summary>
    /// Represents an individual frame result (player vs player)
    /// </summary>
    private class PlayerFrameRecord
    {
        public DateTime Date { get; set; }
        public string PlayerName { get; set; } = "";
        public string PlayerTeam { get; set; } = "";
        public string OpponentName { get; set; } = "";
        public string OpponentTeam { get; set; } = "";
        public bool PlayerWon { get; set; }
        public int? RatingAttained { get; set; }
        public int? Weighting { get; set; }
        
        // Key for deduplication (same frame seen from both players' perspectives)
        public string GetFrameKey()
        {
            // Sort names alphabetically to get consistent key regardless of perspective
            var names = new[] { PlayerName.ToLower(), OpponentName.ToLower() };
            Array.Sort(names);
            return $"{Date:yyyyMMdd}|{names[0]}|{names[1]}";
        }
    }

    /// <summary>
    /// Represents a potential player merge suggestion (similar names)
    /// </summary>
    private class PlayerMergeSuggestion
    {
        public string PlayerKey1 { get; set; } = "";
        public string PlayerKey2 { get; set; } = "";
        public string Name1 { get; set; } = "";
        public string Name2 { get; set; } = "";
        public string Team1 { get; set; } = "";
        public string Team2 { get; set; } = "";
        public int SimilarityScore { get; set; }
        public string Reason { get; set; } = "";
        public bool ShouldMerge { get; set; } = false;
    }

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
            SummaryLabel.Text = $"Processing {filePaths.Count} file(s)";

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
            _aggregatedFrameResults.Clear();
            _playerMergeSuggestions.Clear();

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
                var firstName = nameParts.Length > 0 ? nameParts[0] : "";
                var lastName = nameParts.Length > 1 ? nameParts[1] : "";
                
                // Use fuzzy matching to add or merge player
                AddOrMergePlayer(firstName, lastName, player.TeamName);
            }

            // Player from profile
            if (htmlResult.PlayerProfile != null)
            {
                var profile = htmlResult.PlayerProfile;
                var profileNameParts = profile.PlayerName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                var profileFirstName = profileNameParts.Length > 0 ? profileNameParts[0] : "";
                var profileLastName = profileNameParts.Length > 1 ? profileNameParts[1] : "";
                
                // Use fuzzy matching for profile player
                var profilePlayerKey = AddOrMergePlayer(profileFirstName, profileLastName, profile.TeamName);
                var profileFullName = $"{NormalizePlayerName(profileFirstName)} {NormalizePlayerName(profileLastName)}".Trim();
                
                // Add opponents from match history AND create frame results
                foreach (var match in profile.MatchHistory)
                {
                    var oppParts = match.OpponentName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries);
                    var oppFirst = oppParts.Length > 0 ? oppParts[0] : "";
                    var oppLast = oppParts.Length > 1 ? oppParts[1] : "";
                    
                    // Use fuzzy matching for opponent
                    var oppKey = AddOrMergePlayer(oppFirst, oppLast, match.OpponentTeam);
                    var oppPlayer = _aggregatedPlayers[oppKey];
                    var oppFullName = $"{oppPlayer.FirstName} {oppPlayer.LastName}".Trim();
                    
                    // Create frame result for this match
                    var frameRecord = new PlayerFrameRecord
                    {
                        Date = match.Date,
                        PlayerName = profileFullName,
                        PlayerTeam = NormalizeTeamName(profile.TeamName),
                        OpponentName = oppFullName,
                        OpponentTeam = NormalizeTeamName(match.OpponentTeam),
                        PlayerWon = match.Result.Equals("Won", StringComparison.OrdinalIgnoreCase),
                        RatingAttained = match.RatingAttained
                    };
                    
                    // Check for duplicate (same frame seen from opponent's profile)
                    var frameKey = frameRecord.GetFrameKey();
                    if (!_aggregatedFrameResults.Any(fr => fr.GetFrameKey() == frameKey))
                    {
                        _aggregatedFrameResults.Add(frameRecord);
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

            // Note: Player vs player frame results are extracted from player profiles above
            // The results.htm file only has team-level scores, not individual frame data

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
        
        // Count auto-merged players
        var autoMerged = _playerMergeSuggestions.Count(s => s.ShouldMerge);
        var pendingMerges = _playerMergeSuggestions.Count(s => !s.ShouldMerge);
        
        var mergeInfo = "";
        if (autoMerged > 0 || pendingMerges > 0)
        {
            mergeInfo = $" (merged: {autoMerged}";
            if (pendingMerges > 0)
                mergeInfo += $", potential: {pendingMerges}";
            mergeInfo += ")";
        }
        
        // Use AGGREGATED counts (deduplicated)
        SummaryLabel.Text = $"Files: {totalFiles} ({selectedFiles} selected) | " +
            $"Found: {_aggregatedDivisions.Count} divisions, {_aggregatedTeams.Count} teams, " +
            $"{_aggregatedPlayers.Count} players{mergeInfo}, {_aggregatedResults.Count} fixtures, {_aggregatedFrameResults.Count} frames";
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

        // Show player merge info
        var autoMerged = _playerMergeSuggestions.Count(s => s.ShouldMerge);
        if (autoMerged > 0)
        {
            AlertsStack.Children.Add(new Label
            {
                Text = $"?? {autoMerged} player(s) auto-merged due to similar names",
                TextColor = Colors.LightGreen,
                FontSize = 12
            });
            hasIssues = true;
        }

        // Show potential duplicates that need review
        var pendingMerges = _playerMergeSuggestions.Where(s => !s.ShouldMerge).ToList();
        if (pendingMerges.Any())
        {
            hasIssues = true;
            AlertsStack.Children.Add(new Label
            {
                Text = $"?? {pendingMerges.Count} potential duplicate player(s) found:",
                TextColor = Colors.Yellow,
                FontSize = 12,
                FontAttributes = FontAttributes.Bold
            });
            
            // Show first few potential duplicates
            foreach (var merge in pendingMerges.Take(5))
            {
                AlertsStack.Children.Add(new Label
                {
                    Text = $"   • {merge.Name1} ({merge.Team1}) ~ {merge.Name2} ({merge.Team2}) - {merge.Reason}",
                    TextColor = Colors.Yellow,
                    FontSize = 11
                });
            }
            
            if (pendingMerges.Count > 5)
            {
                AlertsStack.Children.Add(new Label
                {
                    Text = $"   ... and {pendingMerges.Count - 5} more",
                    TextColor = Colors.Yellow,
                    FontSize = 11,
                    FontAttributes = FontAttributes.Italic
                });
            }
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
                $"• {_aggregatedResults.Count} fixtures/results\n" +
                $"• {_aggregatedFrameResults.Count} player frame results",
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
            var playerMap = new System.Collections.Generic.Dictionary<string, Guid>(StringComparer.OrdinalIgnoreCase);

            // 1. Import Divisions
            ProgressLabel.Text = "Importing divisions...";
            ProgressBar.Progress = 0.1;
            await Task.Delay(10);

            foreach (var divEntry in _aggregatedDivisions)
            {
                var divPreview = divEntry.Value;
                
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
            ProgressBar.Progress = 0.2;
            await Task.Delay(10);

            foreach (var teamEntry in _aggregatedTeams)
            {
                var teamPreview = teamEntry.Value;
                var teamKey = teamEntry.Key;
                
                var existingTeam = data.Teams.FirstOrDefault(t => 
                    t.SeasonId == seasonId && 
                    GetTeamKey(t.Name ?? "") == teamKey);

                if (existingTeam != null)
                {
                    teamMap[teamKey] = existingTeam.Id;
                    result.TeamsSkipped++;
                }
                else
                {
                    Guid? divId = null;
                    if (!string.IsNullOrWhiteSpace(teamPreview.DivisionName))
                    {
                        var divKey = teamPreview.DivisionName.ToLowerInvariant();
                        divisionMap.TryGetValue(divKey, out var mappedDivId);
                        divId = mappedDivId != Guid.Empty ? mappedDivId : null;
                    }

                    var team = new Team
                    {
                        Id = Guid.NewGuid(),
                        SeasonId = seasonId,
                        Name = teamPreview.Name,
                        DivisionId = divId
                    };
                    data.Teams.Add(team);
                    teamMap[teamKey] = team.Id;
                    result.TeamsCreated++;
                }
            }

            // 3. Import Players
            ProgressLabel.Text = "Importing players...";
            ProgressBar.Progress = 0.4;
            await Task.Delay(10);

            foreach (var playerEntry in _aggregatedPlayers)
            {
                var playerPreview = playerEntry.Value;
                var playerKey = playerEntry.Key;
                
                // Use fuzzy matching to find existing player
                var existingPlayer = FindExistingPlayerFuzzy(data.Players, seasonId, playerPreview.FirstName, playerPreview.LastName);

                if (existingPlayer != null)
                {
                    playerMap[playerKey] = existingPlayer.Id;
                    result.PlayersSkipped++;
                }
                else
                {
                    Guid? teamId = null;
                    if (!string.IsNullOrWhiteSpace(playerPreview.TeamName))
                    {
                        var teamKey = GetTeamKey(playerPreview.TeamName);
                        teamMap.TryGetValue(teamKey, out var mappedTeamId);
                        teamId = mappedTeamId != Guid.Empty ? mappedTeamId : null;
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
                    playerMap[playerKey] = player.Id;
                    result.PlayersCreated++;
                }
            }

            // 4. Import Fixtures/Results (team scores)
            ProgressLabel.Text = "Importing fixtures...";
            ProgressBar.Progress = 0.6;
            await Task.Delay(10);

            // Track fixtures by date+teams for linking frame results
            var fixtureMap = new System.Collections.Generic.Dictionary<string, Fixture>();

            foreach (var matchResult in _aggregatedResults)
            {
                var homeTeamKey = GetTeamKey(matchResult.HomeTeam);
                var awayTeamKey = GetTeamKey(matchResult.AwayTeam);
                
                teamMap.TryGetValue(homeTeamKey, out var homeTeamId);
                teamMap.TryGetValue(awayTeamKey, out var awayTeamId);

                if (homeTeamId == Guid.Empty || awayTeamId == Guid.Empty)
                {
                    result.FixturesSkipped++;
                    continue;
                }

                var existingFixture = data.Fixtures.FirstOrDefault(f =>
                    f.SeasonId == seasonId &&
                    f.Date.Date == matchResult.Date.Date &&
                    f.HomeTeamId == homeTeamId &&
                    f.AwayTeamId == awayTeamId);

                if (existingFixture != null)
                {
                    // Store for frame linking
                    var fixtureKey = $"{matchResult.Date:yyyyMMdd}|{homeTeamKey}|{awayTeamKey}";
                    fixtureMap[fixtureKey] = existingFixture;
                    result.FixturesSkipped++;
                }
                else
                {
                    Guid? divId = null;
                    if (!string.IsNullOrWhiteSpace(matchResult.Division))
                    {
                        var divKey = matchResult.Division.ToLowerInvariant();
                        divisionMap.TryGetValue(divKey, out var mappedDivId);
                        divId = mappedDivId != Guid.Empty ? mappedDivId : null;
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
                    
                    // Store for frame linking
                    var fixtureKey = $"{matchResult.Date:yyyyMMdd}|{homeTeamKey}|{awayTeamKey}";
                    fixtureMap[fixtureKey] = fixture;
                    
                    data.Fixtures.Add(fixture);
                    result.FixturesCreated++;
                }
            }

            // 5. Import Frame Results (player vs player) - link to fixtures
            ProgressLabel.Text = "Importing player frame results...";
            ProgressBar.Progress = 0.8;
            await Task.Delay(10);

            // Group frame results by fixture (date + teams)
            var framesByFixture = _aggregatedFrameResults
                .GroupBy(fr => fr.Date.Date)
                .ToList();

            foreach (var dateGroup in framesByFixture)
            {
                foreach (var frameResult in dateGroup)
                {
                    // Find player IDs
                    var playerKey = GetPlayerKey(
                        frameResult.PlayerName.Split(' ').FirstOrDefault() ?? "",
                        string.Join(" ", frameResult.PlayerName.Split(' ').Skip(1)));
                    var oppKey = GetPlayerKey(
                        frameResult.OpponentName.Split(' ').FirstOrDefault() ?? "",
                        string.Join(" ", frameResult.OpponentName.Split(' ').Skip(1)));
                    
                    playerMap.TryGetValue(playerKey, out var playerId);
                    playerMap.TryGetValue(oppKey, out var opponentId);

                    if (playerId == Guid.Empty || opponentId == Guid.Empty)
                        continue;

                    // Find team IDs
                    var playerTeamKey = GetTeamKey(frameResult.PlayerTeam);
                    var oppTeamKey = GetTeamKey(frameResult.OpponentTeam);
                    teamMap.TryGetValue(playerTeamKey, out var playerTeamId);
                    teamMap.TryGetValue(oppTeamKey, out var oppTeamId);

                    // Try to find the fixture
                    Fixture? fixture = null;
                    
                    // Try player's team as home
                    var fixtureKey1 = $"{frameResult.Date:yyyyMMdd}|{playerTeamKey}|{oppTeamKey}";
                    fixtureMap.TryGetValue(fixtureKey1, out fixture);
                    
                    // Try opponent's team as home
                    if (fixture == null)
                    {
                        var fixtureKey2 = $"{frameResult.Date:yyyyMMdd}|{oppTeamKey}|{playerTeamKey}";
                        fixtureMap.TryGetValue(fixtureKey2, out fixture);
                    }

                    // If no fixture found, try to find in existing fixtures
                    if (fixture == null)
                    {
                        fixture = data.Fixtures.FirstOrDefault(f =>
                            f.SeasonId == seasonId &&
                            f.Date.Date == frameResult.Date.Date &&
                            ((f.HomeTeamId == playerTeamId && f.AwayTeamId == oppTeamId) ||
                             (f.HomeTeamId == oppTeamId && f.AwayTeamId == playerTeamId)));
                    }

                    // If still no fixture, create one
                    if (fixture == null)
                    {
                        fixture = new Fixture
                        {
                            Id = Guid.NewGuid(),
                            SeasonId = seasonId,
                            Date = frameResult.Date,
                            HomeTeamId = playerTeamId != Guid.Empty ? playerTeamId : oppTeamId,
                            AwayTeamId = oppTeamId != Guid.Empty ? oppTeamId : playerTeamId
                        };
                        data.Fixtures.Add(fixture);
                        fixtureMap[$"{frameResult.Date:yyyyMMdd}|{playerTeamKey}|{oppTeamKey}"] = fixture;
                        result.FixturesCreated++;
                    }

                    // Check if this frame already exists
                    var isPlayerHome = fixture.HomeTeamId == playerTeamId;
                    var homePlayerId = isPlayerHome ? playerId : opponentId;
                    var awayPlayerId = isPlayerHome ? opponentId : playerId;
                    
                    var existingFrame = fixture.Frames.FirstOrDefault(fr =>
                        fr.HomePlayerId == homePlayerId && fr.AwayPlayerId == awayPlayerId);

                    if (existingFrame == null)
                    {
                        // Determine winner
                        var winner = FrameWinner.None;
                        if (frameResult.PlayerWon)
                            winner = isPlayerHome ? FrameWinner.Home : FrameWinner.Away;
                        else
                            winner = isPlayerHome ? FrameWinner.Away : FrameWinner.Home;

                        var frame = new FrameResult
                        {
                            Number = fixture.Frames.Count + 1,
                            HomePlayerId = homePlayerId,
                            AwayPlayerId = awayPlayerId,
                            Winner = winner
                        };
                        
                        // Store rating data if available
                        if (frameResult.RatingAttained.HasValue)
                        {
                            if (isPlayerHome)
                                frame.HomePlayerRating = frameResult.RatingAttained;
                            else
                                frame.AwayPlayerRating = frameResult.RatingAttained;
                        }
                        
                        fixture.Frames.Add(frame);
                        result.FramesCreated++;
                    }
                    else
                    {
                        result.FramesSkipped++;
                    }
                }
            }

            // 6. Update season dates from imported fixture dates
            ProgressLabel.Text = "Updating season dates...";
            ProgressBar.Progress = 0.95;
            await Task.Delay(10);
            
            UpdateSeasonDatesFromImportedResults(seasonId, data);

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

    /// <summary>
    /// Update the season start and end dates based on the imported fixture dates.
    /// This ensures the season dates reflect the actual match dates from HTML.
    /// </summary>
    private void UpdateSeasonDatesFromImportedResults(Guid seasonId, LeagueData data)
    {
        var season = data.Seasons.FirstOrDefault(s => s.Id == seasonId);
        if (season == null) return;

        // Get all fixture dates for this season
        var fixtureDates = data.Fixtures
            .Where(f => f.SeasonId == seasonId)
            .Select(f => f.Date.Date)
            .Distinct()
            .OrderBy(d => d)
            .ToList();

        if (!fixtureDates.Any()) return;

        var earliestDate = fixtureDates.First();
        var latestDate = fixtureDates.Last();

        // Update season dates if the imported data has different dates
        bool updated = false;
        
        if (season.StartDate.Date != earliestDate)
        {
            System.Diagnostics.Debug.WriteLine($"Updating season start date: {season.StartDate:dd/MM/yyyy} -> {earliestDate:dd/MM/yyyy}");
            season.StartDate = earliestDate;
            updated = true;
        }

        if (season.EndDate.Date != latestDate)
        {
            System.Diagnostics.Debug.WriteLine($"Updating season end date: {season.EndDate:dd/MM/yyyy} -> {latestDate:dd/MM/yyyy}");
            season.EndDate = latestDate;
            updated = true;
        }

        if (updated)
        {
            System.Diagnostics.Debug.WriteLine($"Season dates updated from HTML import: {season.StartDate:dd/MM/yyyy} - {season.EndDate:dd/MM/yyyy}");
        }
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
        public int FramesCreated { get; set; }
        public int FramesSkipped { get; set; }
        public System.Collections.Generic.List<string> Errors { get; set; } = new();

        public string Summary => 
            $"Created:\n" +
            $"• {DivisionsCreated} divisions\n" +
            $"• {TeamsCreated} teams\n" +
            $"• {PlayersCreated} players\n" +
            $"• {FixturesCreated} fixtures\n" +
            $"• {FramesCreated} player frame results\n\n" +
            $"Skipped (already exist):\n" +
            $"• {DivisionsSkipped} divisions\n" +
            $"• {TeamsSkipped} teams\n" +
            $"• {PlayersSkipped} players\n" +
            $"• {FixturesSkipped} fixtures\n" +
            $"• {FramesSkipped} frames";
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
    /// Normalize player name for consistent storage - UPPERCASE like teams
    /// </summary>
    private string NormalizePlayerName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
            
        // Convert to uppercase like teams
        name = name.ToUpperInvariant();
        
        // Standardize apostrophes and quotes
        name = name.Replace("'", "'").Replace("'", "'").Replace("`", "'");
        
        return name.Trim();
    }

    /// <summary>
    /// Create a key for player deduplication (removes punctuation for comparison)
    /// </summary>
    private string GetPlayerKey(string firstName, string lastName)
    {
        var first = NormalizePlayerNameForKey(firstName);
        var last = NormalizePlayerNameForKey(lastName);
        return $"{first} {last}".Trim();
    }

    /// <summary>
    /// Normalize player name component for key generation (removes punctuation)
    /// </summary>
    private string NormalizePlayerNameForKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "";
            
        // Convert to uppercase
        name = name.ToUpperInvariant();
        
        // Remove apostrophes and common punctuation for comparison
        name = name.Replace("'", "")
                   .Replace("'", "")
                   .Replace("'", "")
                   .Replace("`", "")
                   .Replace(".", "")
                   .Replace(",", "")
                   .Replace("-", " ");
        
        // Remove extra spaces
        name = System.Text.RegularExpressions.Regex.Replace(name, @"\s+", " ").Trim();
        
        return name;
    }

    /// <summary>
    /// Find an existing player key using fuzzy matching
    /// Returns the existing key if a close match is found, or null
    /// </summary>
    private string? FindSimilarPlayerKey(string firstName, string lastName)
    {
        var targetKey = GetPlayerKey(firstName, lastName);
        var targetFirst = NormalizePlayerNameForKey(firstName);
        var targetLast = NormalizePlayerNameForKey(lastName);
        
        foreach (var existingKey in _aggregatedPlayers.Keys)
        {
            // Skip exact matches (already handled)
            if (existingKey.Equals(targetKey, StringComparison.OrdinalIgnoreCase))
                continue;
            
            var existingPlayer = _aggregatedPlayers[existingKey];
            var existingFirst = NormalizePlayerNameForKey(existingPlayer.FirstName);
            var existingLast = NormalizePlayerNameForKey(existingPlayer.LastName);
            
            // Check for similar names using various strategies
            
            // 1. Same last name, similar first name (within 2 chars edit distance)
            if (existingLast == targetLast && LevenshteinDistance(existingFirst, targetFirst) <= 2)
            {
                return existingKey;
            }
            
            // 2. Same first name, similar last name (within 2 chars edit distance)
            if (existingFirst == targetFirst && LevenshteinDistance(existingLast, targetLast) <= 2)
            {
                return existingKey;
            }
            
            // 3. Both names within edit distance (for typos in both)
            if (LevenshteinDistance(existingFirst, targetFirst) <= 1 && 
                LevenshteinDistance(existingLast, targetLast) <= 1)
            {
                return existingKey;
            }
            
            // 4. First name is initial that matches
            if ((targetFirst.Length == 1 && existingFirst.StartsWith(targetFirst)) ||
                (existingFirst.Length == 1 && targetFirst.StartsWith(existingFirst)))
            {
                if (existingLast == targetLast)
                    return existingKey;
            }
            
            // 5. Names swapped (first/last reversed)
            if (existingFirst == targetLast && existingLast == targetFirst)
            {
                return existingKey;
            }
        }
        
        return null;
    }

    /// <summary>
    /// Add a player with fuzzy matching - returns the key used (may be existing key if merged)
    /// </summary>
    private string AddOrMergePlayer(string firstName, string lastName, string teamName)
    {
        var normalizedFirst = NormalizePlayerName(firstName);
        var normalizedLast = NormalizePlayerName(lastName);
        var playerKey = GetPlayerKey(firstName, lastName);
        
        // Check for exact match first
        if (_aggregatedPlayers.ContainsKey(playerKey))
        {
            return playerKey;
        }
        
        // Try fuzzy matching
        var similarKey = FindSimilarPlayerKey(firstName, lastName);
        if (similarKey != null)
        {
            // Record merge suggestion for review
            var existingPlayer = _aggregatedPlayers[similarKey];
            var suggestion = new PlayerMergeSuggestion
            {
                PlayerKey1 = similarKey,
                PlayerKey2 = playerKey,
                Name1 = $"{existingPlayer.FirstName} {existingPlayer.LastName}",
                Name2 = $"{normalizedFirst} {normalizedLast}",
                Team1 = existingPlayer.TeamName,
                Team2 = NormalizeTeamName(teamName),
                SimilarityScore = CalculateSimilarity(similarKey, playerKey),
                Reason = GetSimilarityReason(existingPlayer.FirstName, existingPlayer.LastName, normalizedFirst, normalizedLast)
            };
            
            // Auto-merge if very similar (same team or very close names)
            if (suggestion.Team1 == suggestion.Team2 || suggestion.SimilarityScore >= 90)
            {
                suggestion.ShouldMerge = true;
                System.Diagnostics.Debug.WriteLine($"Auto-merging player: {suggestion.Name2} -> {suggestion.Name1} ({suggestion.Reason})");
                return similarKey;
            }
            
            // Record for potential manual review
            if (!_playerMergeSuggestions.Any(s => 
                (s.PlayerKey1 == similarKey && s.PlayerKey2 == playerKey) ||
                (s.PlayerKey1 == playerKey && s.PlayerKey2 == similarKey)))
            {
                _playerMergeSuggestions.Add(suggestion);
                System.Diagnostics.Debug.WriteLine($"Potential duplicate: {suggestion.Name2} ~ {suggestion.Name1} ({suggestion.Reason})");
            }
        }
        
        // Add as new player
        _aggregatedPlayers[playerKey] = new PlayerPreview
        {
            FirstName = normalizedFirst,
            LastName = normalizedLast,
            TeamName = NormalizeTeamName(teamName)
        };
        
        return playerKey;
    }

    /// <summary>
    /// Calculate similarity score between two player keys (0-100)
    /// </summary>
    private int CalculateSimilarity(string key1, string key2)
    {
        var maxLen = Math.Max(key1.Length, key2.Length);
        if (maxLen == 0) return 100;
        
        var distance = LevenshteinDistance(key1, key2);
        return Math.Max(0, 100 - (distance * 100 / maxLen));
    }

    /// <summary>
    /// Get a human-readable reason for why names are similar
    /// </summary>
    private string GetSimilarityReason(string first1, string last1, string first2, string last2)
    {
        var f1 = NormalizePlayerNameForKey(first1);
        var f2 = NormalizePlayerNameForKey(first2);
        var l1 = NormalizePlayerNameForKey(last1);
        var l2 = NormalizePlayerNameForKey(last2);
        
        if (l1 == l2 && f1 != f2)
            return $"Same last name, first name differs by {LevenshteinDistance(f1, f2)} char(s)";
        if (f1 == f2 && l1 != l2)
            return $"Same first name, last name differs by {LevenshteinDistance(l1, l2)} char(s)";
        if (f1 == l2 && l1 == f2)
            return "Names appear swapped";
        if (f1.Length == 1 || f2.Length == 1)
            return "Initial vs full name";
            
        return "Similar spelling";
    }

    /// <summary>
    /// Calculate Levenshtein distance between two strings (for fuzzy matching)
    /// </summary>
    private static int LevenshteinDistance(string a, string b)
    {
        if (string.IsNullOrEmpty(a)) return b?.Length ?? 0;
        if (string.IsNullOrEmpty(b)) return a.Length;

        var d = new int[a.Length + 1, b.Length + 1];

        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;

        for (int i = 1; i <= a.Length; i++)
        {
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = (b[j - 1] == a[i - 1]) ? 0 : 1;
                d[i, j] = Math.Min(
                    Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1),
                    d[i - 1, j - 1] + cost);
            }
        }

        return d[a.Length, b.Length];
    }

    private Player FindExistingPlayerFuzzy(System.Collections.Generic.List<Player> players, Guid seasonId, string firstName, string lastName)
    {
        // Normalize input
        var targetFirst = NormalizePlayerNameForKey(firstName);
        var targetLast = NormalizePlayerNameForKey(lastName);
        
        foreach (var player in players.Where(p => p.SeasonId == seasonId))
        {
            var existingFirst = NormalizePlayerNameForKey(player.FirstName);
            var existingLast = NormalizePlayerNameForKey(player.LastName);
            
            // Exact match
            if (existingFirst == targetFirst && existingLast == targetLast)
                return player;
            
            // Same last name, similar first name (within 2 chars edit distance)
            if (existingLast == targetLast && LevenshteinDistance(existingFirst, targetFirst) <= 2)
                return player;
            
            // Same first name, similar last name (within 2 chars edit distance)
            if (existingFirst == targetFirst && LevenshteinDistance(existingLast, targetLast) <= 2)
                return player;
            
            // Both names within edit distance (for typos in both)
            if (LevenshteinDistance(existingFirst, targetFirst) <= 1 && LevenshteinDistance(existingLast, targetLast) <= 1)
                return player;
            
            // First name is initial that matches
            if ((targetFirst.Length == 1 && existingFirst.StartsWith(targetFirst)) ||
                (existingFirst.Length == 1 && targetFirst.StartsWith(existingFirst)))
            {
                if (existingLast == targetLast)
                    return player;
            }
        }
        
        return null;
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
