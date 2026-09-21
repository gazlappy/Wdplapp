# Group Stage Methods - Add to CompetitionsPage.xaml.cs

**Instructions**: Add these methods to your CompetitionsPage.xaml.cs file. These integrate seamlessly with your existing code.

---

## 1. Modify `ShowCompetitionEditor()` - ADD THIS SECTION

Add this code block AFTER the "Bracket Generation" section and BEFORE the "Save Button":

```csharp
// ========== GROUP STAGE SETTINGS (Add after Bracket Generation section) ==========

// Group Stage Settings (show only for group stage formats)
if (competition.Format == CompetitionFormat.SinglesGroupStage || 
    competition.Format == CompetitionFormat.DoublesGroupStage)
{
    content.Children.Add(new Label 
    { 
        Text = "Group Stage Settings", 
        FontSize = 16, 
        FontAttributes = FontAttributes.Bold, 
        Margin = new Thickness(0, 12, 0, 4) 
    });

    // Initialize settings if null
    if (competition.GroupSettings == null)
    {
        competition.GroupSettings = new GroupStageSettings();
    }

    var groupsEntry = new Entry 
    { 
        Text = competition.GroupSettings.NumberOfGroups.ToString(),
        Keyboard = Keyboard.Numeric,
        Placeholder = "Number of groups"
    };

    var topAdvanceEntry = new Entry 
    { 
        Text = competition.GroupSettings.TopPlayersAdvance.ToString(),
        Keyboard = Keyboard.Numeric,
        Placeholder = "Top players advancing"
    };

    var lowerPlateEntry = new Entry 
    { 
        Text = competition.GroupSettings.LowerPlayersToPlate.ToString(),
        Keyboard = Keyboard.Numeric,
        Placeholder = "Lower players to plate"
    };

    var createPlateSwitch = new Switch 
    { 
        IsToggled = competition.GroupSettings.CreatePlateCompetition 
    };

    var plateSuffixEntry = new Entry 
    { 
        Text = competition.GroupSettings.PlateNameSuffix,
        Placeholder = "Plate name suffix"
    };

    content.Children.Add(CreateLabeledField("Number of Groups:", groupsEntry));
    content.Children.Add(CreateLabeledField("Top Players Advance:", topAdvanceEntry));
    content.Children.Add(CreateLabeledField("Lower to Plate:", lowerPlateEntry));
    content.Children.Add(CreateLabeledField("Create Plate Comp:", createPlateSwitch));
    content.Children.Add(CreateLabeledField("Plate Suffix:", plateSuffixEntry));

    // Update handlers
    groupsEntry.TextChanged += (s, e) =>
    {
        if (int.TryParse(groupsEntry.Text, out int val) && val > 0)
            competition.GroupSettings.NumberOfGroups = val;
    };

    topAdvanceEntry.TextChanged += (s, e) =>
    {
        if (int.TryParse(topAdvanceEntry.Text, out int val) && val >= 0)
            competition.GroupSettings.TopPlayersAdvance = val;
    };

    lowerPlateEntry.TextChanged += (s, e) =>
    {
        if (int.TryParse(lowerPlateEntry.Text, out int val) && val >= 0)
            competition.GroupSettings.LowerPlayersToPlate = val;
    };

    createPlateSwitch.Toggled += (s, e) =>
    {
        competition.GroupSettings.CreatePlateCompetition = createPlateSwitch.IsToggled;
    };

    plateSuffixEntry.TextChanged += (s, e) =>
    {
        competition.GroupSettings.PlateNameSuffix = plateSuffixEntry.Text ?? "Plate";
    };

    // Generate Groups Button
    var generateGroupsBtn = new Button
    {
        Text = "Generate Groups",
        BackgroundColor = Color.FromArgb("#8B5CF6"),
        TextColor = Colors.White,
        Padding = new Thickness(8, 4),
        Margin = new Thickness(0, 8, 0, 0)
    };
    generateGroupsBtn.Clicked += (s, e) => OnGenerateGroups();
    content.Children.Add(generateGroupsBtn);

    // View Groups Button (only if groups exist)
    if (competition.Groups.Count > 0)
    {
        var viewGroupsBtn = new Button
        {
            Text = $"View Groups ({competition.Groups.Count})",
            BackgroundColor = Color.FromArgb("#6366F1"),
            TextColor = Colors.White,
            Padding = new Thickness(8, 4),
            Margin = new Thickness(0, 4, 0, 0)
        };
        viewGroupsBtn.Clicked += (s, e) => ShowGroupsView();
        content.Children.Add(viewGroupsBtn);

        // Finalize & Create Knockouts Button
        var finalizeBtn = new Button
        {
            Text = "Finalize Groups & Create Knockouts",
            BackgroundColor = Color.FromArgb("#10B981"),
            TextColor = Colors.White,
            Padding = new Thickness(8, 4),
            Margin = new Thickness(0, 4, 0, 0)
        };
        finalizeBtn.Clicked += (s, e) => OnFinalizeGroups();
        content.Children.Add(finalizeBtn);
    }
}

// ========== END GROUP STAGE SETTINGS ==========
```

---

## 2. Modify `RefreshParticipants()` - ADD GROUP STAGE FORMATS

Add these cases to the format checking:

```csharp
// In RefreshParticipants method, update the condition:
if (format == CompetitionFormat.SinglesKnockout || 
    format == CompetitionFormat.RoundRobin || 
    format == CompetitionFormat.Swiss ||
    format == CompetitionFormat.SinglesGroupStage)  // ADD THIS LINE
{
    // Singles - use players
    // ...existing code...
}
else if (format == CompetitionFormat.DoublesKnockout ||
         format == CompetitionFormat.DoublesGroupStage)  // ADD THIS LINE
{
    // Doubles - use doubles teams
    // ...existing code...
}
```

---

## 3. Modify `OnAddParticipant()` - ADD GROUP STAGE FORMATS

Update the conditions:

```csharp
// In OnAddParticipant method:
if (format == CompetitionFormat.SinglesKnockout || 
    format == CompetitionFormat.RoundRobin || 
    format == CompetitionFormat.Swiss ||
    format == CompetitionFormat.SinglesGroupStage)  // ADD THIS LINE
{
    await ShowMultiSelectPlayersDialog();
}
else if (format == CompetitionFormat.DoublesKnockout ||
         format == CompetitionFormat.DoublesGroupStage)  // ADD THIS LINE
{
    await ShowDoublesTeamSelectionDialog();
}
```

---

## 4. Modify `GetParticipantName()` - ADD GROUP STAGE FORMATS

Update the switch expression:

```csharp
return format switch
{
    CompetitionFormat.SinglesKnockout or 
    CompetitionFormat.RoundRobin or 
    CompetitionFormat.Swiss or
    CompetitionFormat.SinglesGroupStage  // ADD THIS LINE
        => DataStore.Data.Players.FirstOrDefault(p => p.Id == participantId)?.FullName,
    
    CompetitionFormat.TeamKnockout
        => DataStore.Data.Teams.FirstOrDefault(t => t.Id == participantId)?.Name,
    
    CompetitionFormat.DoublesKnockout or
    CompetitionFormat.DoublesGroupStage  // ADD THIS LINE
        => GetDoublesTeamName(participantId.Value),
    
    _ => null
};
```

---

## 5. ADD NEW METHODS (Place at end of class, before closing brace)

```csharp
// ========== GROUP STAGE METHODS ==========

private void OnGenerateGroups()
{
    if (_selectedCompetition == null) return;

    var format = _selectedCompetition.Format;
    if (format != CompetitionFormat.SinglesGroupStage && 
        format != CompetitionFormat.DoublesGroupStage)
    {
        SetStatus("This competition is not a group stage format");
        return;
    }

    if (_selectedCompetition.GroupSettings == null)
    {
        _selectedCompetition.GroupSettings = new GroupStageSettings();
    }

    List<Guid> participants;
    
    if (format == CompetitionFormat.DoublesGroupStage)
    {
        if (_selectedCompetition.DoublesTeams.Count < _selectedCompetition.GroupSettings.NumberOfGroups * 2)
        {
            SetStatus($"Need at least {_selectedCompetition.GroupSettings.NumberOfGroups * 2} teams");
            return;
        }
        participants = _selectedCompetition.DoublesTeams.Select(t => t.Id).ToList();
    }
    else
    {
        if (_selectedCompetition.ParticipantIds.Count < _selectedCompetition.GroupSettings.NumberOfGroups * 2)
        {
            SetStatus($"Need at least {_selectedCompetition.GroupSettings.NumberOfGroups * 2} players");
            return;
        }
        participants = _selectedCompetition.ParticipantIds;
    }

    // Generate groups
    var (groups, plateComp) = CompetitionGenerator.GenerateGroupStage(
        participants,
        _selectedCompetition.GroupSettings,
        format,
        _currentSeasonId,
        _selectedCompetition.Name,
        randomize: true
    );

    _selectedCompetition.Groups = groups;
    
    // Add plate competition if created
    if (plateComp != null)
    {
        DataStore.Data.Competitions.Add(plateComp);
        _selectedCompetition.PlateCompetitionId = plateComp.Id;
        RefreshCompetitions();
    }

    _selectedCompetition.Status = CompetitionStatus.InProgress;
    DataStore.Save();

    SetStatus($"Generated {groups.Count} groups with {groups.Sum(g => g.Matches.Count)} matches");
    
    // Refresh editor to show new buttons
    ShowCompetitionEditor(_selectedCompetition);
}

private void ShowGroupsView()
{
    if (_selectedCompetition == null || _selectedCompetition.Groups.Count == 0) return;

    var mainLayout = new VerticalStackLayout
    {
        Spacing = 10
    };

    // Header
    var headerGrid = new Grid
    {
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Auto },
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = GridLength.Auto }
        },
        ColumnSpacing = 8,
        Margin = new Thickness(0, 0, 0, 10)
    };

    var backBtn = new Button
    {
        Text = "? Back",
        Command = new Command(() => ShowCompetitionEditor(_selectedCompetition)),
        Padding = new Thickness(8, 4),
        FontSize = 13
    };

    var titleLabel = new Label
    {
        Text = $"{_selectedCompetition.Name} - Groups",
        FontSize = 16,
        FontAttributes = FontAttributes.Bold,
        VerticalTextAlignment = TextAlignment.Center,
        HorizontalTextAlignment = TextAlignment.Center
    };

    var applyAllBtn = new Button
    {
        Text = "Apply All Scores",
        BackgroundColor = Color.FromArgb("#10B981"),
        TextColor = Colors.White,
        Padding = new Thickness(12, 6),
        FontSize = 13
    };

    headerGrid.Add(backBtn, 0, 0);
    headerGrid.Add(titleLabel, 1, 0);
    headerGrid.Add(applyAllBtn, 2, 0);

    mainLayout.Children.Add(headerGrid);

    // Create tabs for each group
    var tabLayout = new HorizontalStackLayout
    {
        Spacing = 4,
        Margin = new Thickness(0, 0, 0, 8)
    };

    var groupContents = new Dictionary<int, View>();
    ContentView groupContentView = new ContentView();

    // Create tabs
    for (int i = 0; i < _selectedCompetition.Groups.Count; i++)
    {
        var group = _selectedCompetition.Groups[i];
        var groupIndex = i;

        var tabButton = new Button
        {
            Text = group.Name,
            FontSize = 12,
            Padding = new Thickness(12, 6),
            BackgroundColor = i == 0 ? Color.FromArgb("#3B82F6") : Color.FromArgb("#6B7280"),
            TextColor = Colors.White
        };

        // Store group content
        var groupContent = CreateGroupContent(group);
        groupContents[groupIndex] = groupContent;

        // Tab click handler
        tabButton.Clicked += (s, e) =>
        {
            // Update tab colors
            foreach (var child in tabLayout.Children)
            {
                if (child is Button btn)
                {
                    btn.BackgroundColor = Color.FromArgb("#6B7280");
                }
            }
            
            tabButton.BackgroundColor = Color.FromArgb("#3B82F6");
            groupContentView.Content = groupContents[groupIndex];
        };

        tabLayout.Children.Add(tabButton);
    }

    // Show first group by default
    if (groupContents.Count > 0)
    {
        groupContentView.Content = groupContents[0];
    }

    mainLayout.Children.Add(tabLayout);
    mainLayout.Children.Add(groupContentView);

    // Apply All Scores handler
    applyAllBtn.Clicked += (s, e) =>
    {
        ApplyAllGroupScores();
        ShowGroupsView(); // Refresh
    };

    ContentPanel.Content = new ScrollView { Content = mainLayout };
}

private View CreateGroupContent(CompetitionGroup group)
{
    var layout = new VerticalStackLayout
    {
        Spacing = 10
    };

    // Group name header
    layout.Children.Add(new Label
    {
        Text = group.Name,
        FontSize = 18,
        FontAttributes = FontAttributes.Bold,
        Margin = new Thickness(0, 0, 0, 10)
    });

    // Standings Table
    layout.Children.Add(new Label
    {
        Text = "Standings",
        FontSize = 15,
        FontAttributes = FontAttributes.Bold,
        Margin = new Thickness(0, 8, 0, 4)
    });

    var standings = CompetitionGenerator.CalculateGroupStandings(group);
    group.Standings = standings;

    var standingsGrid = CreateStandingsTable(standings, group);
    layout.Children.Add(standingsGrid);

    // Matches
    layout.Children.Add(new Label
    {
        Text = "Matches",
        FontSize = 15,
        FontAttributes = FontAttributes.Bold,
        Margin = new Thickness(0, 16, 0, 4)
    });

    foreach (var match in group.Matches)
    {
        var matchCard = CreateGroupMatchCard(match);
        layout.Children.Add(matchCard);
    }

    return layout;
}

private View CreateStandingsTable(List<GroupStanding> standings, CompetitionGroup group)
{
    var grid = new Grid
    {
        RowSpacing = 2,
        ColumnSpacing = 4,
        Padding = new Thickness(4),
        BackgroundColor = Color.FromArgb("#F9FAFB")
    };

    // Define columns
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // Pos
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Star }); // Player
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // P
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // W
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // D
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(30) }); // L
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // F
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(35) }); // A
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Diff
    grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(40) }); // Pts

    // Headers
    string[] headers = { "#", "Player", "P", "W", "D", "L", "F", "A", "Diff", "Pts" };
    for (int i = 0; i < headers.Length; i++)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var label = new Label
        {
            Text = headers[i],
            FontAttributes = FontAttributes.Bold,
            FontSize = 11,
            HorizontalTextAlignment = TextAlignment.Center,
            Padding = new Thickness(2)
        };
        grid.Children.Add(label);
        Grid.SetRow(label, 0);
        Grid.SetColumn(label, i);
    }

    // Data rows
    int row = 1;
    foreach (var standing in standings)
    {
        grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

        var playerName = GetParticipantName(standing.ParticipantId, _selectedCompetition!.Format);

        var rowData = new[]
        {
            standing.Position.ToString(),
            playerName ?? "Unknown",
            standing.Played.ToString(),
            standing.Won.ToString(),
            standing.Drawn.ToString(),
            standing.Lost.ToString(),
            standing.FramesFor.ToString(),
            standing.FramesAgainst.ToString(),
            standing.FrameDifference.ToString("+0;-#"),
            standing.Points.ToString()
        };

        for (int col = 0; col < rowData.Length; col++)
        {
            var label = new Label
            {
                Text = rowData[col],
                FontSize = 11,
                HorizontalTextAlignment = col == 1 ? TextAlignment.Start : TextAlignment.Center,
                Padding = new Thickness(2),
                BackgroundColor = row % 2 == 0 ? Colors.White : Color.FromArgb("#F3F4F6")
            };

            grid.Children.Add(label);
            Grid.SetRow(label, row);
            Grid.SetColumn(label, col);
        }

        row++;
    }

    return new Frame
    {
        Content = grid,
        Padding = 4,
        BorderColor = Color.FromArgb("#E5E7EB"),
        BackgroundColor = Colors.White
    };
}

private View CreateGroupMatchCard(CompetitionMatch match)
{
    var p1Name = GetParticipantName(match.Participant1Id, _selectedCompetition!.Format);
    var p2Name = GetParticipantName(match.Participant2Id, _selectedCompetition!.Format);

    var grid = new Grid
    {
        ColumnDefinitions =
        {
            new ColumnDefinition { Width = GridLength.Star },
            new ColumnDefinition { Width = new GridLength(35) },
            new ColumnDefinition { Width = new GridLength(10) },
            new ColumnDefinition { Width = new GridLength(35) },
            new ColumnDefinition { Width = GridLength.Star }
        },
        ColumnSpacing = 4,
        Padding = new Thickness(8, 6)
    };

    var p1Label = new Label
    {
        Text = p1Name ?? "TBD",
        FontSize = 12,
        VerticalTextAlignment = TextAlignment.Center,
        FontAttributes = match.WinnerId == match.Participant1Id ? FontAttributes.Bold : FontAttributes.None,
        TextColor = match.WinnerId == match.Participant1Id ? Color.FromArgb("#10B981") : null
    };

    var p1Score = new Entry
    {
        Text = match.Participant1Score.ToString(),
        Keyboard = Keyboard.Numeric,
        HorizontalTextAlignment = TextAlignment.Center,
        FontSize = 12,
        BackgroundColor = Color.FromArgb("#F3F4F6")
    };

    var vsLabel = new Label
    {
        Text = "vs",
        FontSize = 11,
        HorizontalTextAlignment = TextAlignment.Center,
        VerticalTextAlignment = TextAlignment.Center
    };

    var p2Score = new Entry
    {
        Text = match.Participant2Score.ToString(),
        Keyboard = Keyboard.Numeric,
        HorizontalTextAlignment = TextAlignment.Center,
        FontSize = 12,
        BackgroundColor = Color.FromArgb("#F3F4F6")
    };

    var p2Label = new Label
    {
        Text = p2Name ?? "TBD",
        FontSize = 12,
        VerticalTextAlignment = TextAlignment.Center,
        FontAttributes = match.WinnerId == match.Participant2Id ? FontAttributes.Bold : FontAttributes.None,
        TextColor = match.WinnerId == match.Participant2Id ? Color.FromArgb("#10B981") : null
    };

    grid.Add(p1Label, 0, 0);
    grid.Add(p1Score, 1, 0);
    grid.Add(vsLabel, 2, 0);
    grid.Add(p2Score, 3, 0);
    grid.Add(p2Label, 4, 0);

    var statusLabel = new Label
    {
        Text = match.IsComplete ? "?" : "",
        FontSize = 14,
        TextColor = Color.FromArgb("#10B981"),
        VerticalTextAlignment = TextAlignment.Center,
        Margin = new Thickness(8, 0, 0, 0)
    };

    var matchLayout = new HorizontalStackLayout
    {
        Children = { grid, statusLabel }
    };

    return new Frame
    {
        Content = matchLayout,
        Padding = 4,
        Margin = new Thickness(0, 2),
        BorderColor = match.IsComplete ? Color.FromArgb("#10B981") : Color.FromArgb("#E5E7EB"),
        BackgroundColor = Colors.White
    };
}

private void ApplyAllGroupScores()
{
    if (_selectedCompetition == null) return;

    bool anyUpdates = false;
    
    foreach (var group in _selectedCompetition.Groups)
    {
        foreach (var match in group.Matches)
        {
            if (!match.IsComplete && match.Participant1Id.HasValue && match.Participant2Id.HasValue)
            {
                if (match.Participant1Score > match.Participant2Score)
                    match.WinnerId = match.Participant1Id;
                else if (match.Participant2Score > match.Participant1Score)
                    match.WinnerId = match.Participant2Id;
                else if (match.Participant1Score > 0 || match.Participant2Score > 0)
                {
                    // It's a draw
                    match.WinnerId = null;
                }
                else
                {
                    continue; // No scores entered
                }

                match.IsComplete = true;
                anyUpdates = true;
            }
        }
    }

    if (anyUpdates)
    {
        DataStore.Save();
        SetStatus("All group scores applied");
    }
    else
    {
        SetStatus("No new scores to apply");
    }
}

private async void OnFinalizeGroups()
{
    if (_selectedCompetition == null || _selectedCompetition.GroupSettings == null) return;

    var confirm = await DisplayAlert(
        "Finalize Groups",
        "This will create knockout brackets from group standings. Make sure all group matches are complete. Continue?",
        "Yes, Create Knockouts",
        "Cancel");

    if (!confirm) return;

    // Advance participants from groups
    var (knockoutParticipants, plateParticipants) = CompetitionGenerator.AdvanceFromGroups(
        _selectedCompetition.Groups,
        _selectedCompetition.GroupSettings.TopPlayersAdvance,
        _selectedCompetition.GroupSettings.LowerPlayersToPlate
    );

    // Create main knockout bracket
    if (knockoutParticipants.Count >= 2)
    {
        _selectedCompetition.Rounds = CompetitionGenerator.GenerateSingleKnockout(
            knockoutParticipants,
            randomize: false // Seeded by group position
        );
        SetStatus($"Created knockout bracket with {knockoutParticipants.Count} players");
    }

    // Create plate competition
    if (plateParticipants.Count >= 2 && _selectedCompetition.PlateCompetitionId.HasValue)
    {
        var plateComp = DataStore.Data.Competitions
            .FirstOrDefault(c => c.Id == _selectedCompetition.PlateCompetitionId.Value);

        if (plateComp != null)
        {
            // Assign participants to plate
            if (_selectedCompetition.Format == CompetitionFormat.DoublesGroupStage)
            {
                plateComp.DoublesTeams = _selectedCompetition.DoublesTeams
                    .Where(t => plateParticipants.Contains(t.Id))
                    .ToList();
            }
            else
            {
                plateComp.ParticipantIds = plateParticipants;
            }

            // Generate plate knockout
            plateComp.Rounds = CompetitionGenerator.GenerateSingleKnockout(
                plateParticipants,
                randomize: false
            );
            plateComp.Status = CompetitionStatus.InProgress;

            SetStatus($"Created plate knockout with {plateParticipants.Count} players");
        }
    }

    _selectedCompetition.Status = CompetitionStatus.InProgress;
    DataStore.Save();
    RefreshCompetitions();

    await DisplayAlert("Success", 
        $"Knockout brackets created!\nMain: {knockoutParticipants.Count} players\nPlate: {plateParticipants.Count} players", 
        "OK");

    // Refresh view
    ShowCompetitionEditor(_selectedCompetition);
}

// ========== END GROUP STAGE METHODS ==========
```

---

## Summary of Changes

**Files Modified**: 1
- `CompetitionsPage.xaml.cs`

**Methods Added**: 7
1. `OnGenerateGroups()` - Generate groups from participants
2. `ShowGroupsView()` - Display groups with tabs
3. `CreateGroupContent()` - Build group view
4. `CreateStandingsTable()` - Display group standings
5. `CreateGroupMatchCard()` - Display group matches
6. `ApplyAllGroupScores()` - Process group scores
7. `OnFinalizeGroups()` - Create knockouts from groups

**Methods Modified**: 4
1. `ShowCompetitionEditor()` - Added group stage UI
2. `RefreshParticipants()` - Added group stage format checks
3. `OnAddParticipant()` - Added group stage format checks
4. `GetParticipantName()` - Added group stage format checks

---

## ? Ready to Integrate!

Follow the numbered sections above to add all group stage functionality to your CompetitionsPage!
