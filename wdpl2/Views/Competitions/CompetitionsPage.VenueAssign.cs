using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views;

/// <summary>
/// Manual venue/table assignment helpers shared by the bracket / round-robin
/// match cards and the group-stage group headers.
/// Lets the user tap a match (or group) and pick a venue then a table from
/// the venues defined for the competition's season.
/// </summary>
public partial class CompetitionsPage
{
    /// <summary>
    /// Returns the venues available for the currently-selected competition's season.
    /// Falls back to all venues if no season is set.
    /// </summary>
    private List<Venue> GetVenuesForCurrentCompetition()
    {
        var seasonId = _selectedCompetition?.SeasonId ?? _viewModel.CurrentSeasonId;
        var all = _dataStore.GetData()?.Venues ?? new List<Venue>();
        var list = seasonId.HasValue
            ? all.Where(v => v != null && v.SeasonId == seasonId.Value).ToList()
            : all.Where(v => v != null).ToList();
        return list.OrderBy(v => v.Name).ToList();
    }

    /// <summary>
    /// Prompt the user to pick a venue and table for a competition match.
    /// Persists via <see cref="CompetitionEditorViewModel.SaveCompetitionAsync"/>.
    /// Re-renders the current view on success.
    /// </summary>
    private async Task AssignMatchVenueAsync(CompetitionMatch match, Competition competition)
    {
        if (_editorViewModel == null) return;
        if (_dataStore.GetData().IsSeasonLocked(competition.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot change venue — this season is locked.", "OK");
            return;
        }

        var venues = GetVenuesForCurrentCompetition();
        if (venues.Count == 0)
        {
            await DisplayAlert("No Venues",
                "There are no venues defined for this competition's season. Add venues on the Venues page first.",
                "OK");
            return;
        }

        var hasAssignment = match.VenueId.HasValue;
        var venueOptions = venues.Select(v => v.Name ?? "(unnamed)").ToList();
        string? clearOption = hasAssignment ? "❌ Clear assignment" : null;

        var actions = clearOption != null
            ? new[] { clearOption }.Concat(venueOptions).ToArray()
            : venueOptions.ToArray();

        var chosen = await DisplayActionSheet("Select Venue", "Cancel", null, actions);
        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

        if (chosen == clearOption)
        {
            match.VenueId = null;
            match.VenueName = null;
            match.TableId = null;
            match.TableLabel = null;
            await _editorViewModel.SaveCompetitionAsync();
            SetStatus("Venue/table cleared");
            RerenderCompetitionView(competition);
            return;
        }

        var venue = venues.FirstOrDefault(v => (v.Name ?? "(unnamed)") == chosen);
        if (venue == null) return;

        // Pick a table at that venue
        var tables = venue.Tables?.OrderBy(t => t.Label).ToList() ?? new List<VenueTable>();
        Guid? tableId = null;
        string? tableLabel = null;

        if (tables.Count > 0)
        {
            var tableNames = tables.Select(t => t.Label ?? "(unnamed)").ToArray();
            var chosenTable = await DisplayActionSheet("Select Table", "Cancel", null, tableNames);
            if (string.IsNullOrEmpty(chosenTable) || chosenTable == "Cancel") return;

            var table = tables.FirstOrDefault(t => (t.Label ?? "(unnamed)") == chosenTable);
            if (table != null)
            {
                tableId = table.Id;
                tableLabel = table.Label;
            }
        }

        match.VenueId = venue.Id;
        match.VenueName = venue.Name;
        match.TableId = tableId;
        match.TableLabel = tableLabel;

        await _editorViewModel.SaveCompetitionAsync();
        SetStatus($"Assigned {venue.Name}{(tableLabel != null ? " — " + tableLabel : "")}");
        RerenderCompetitionView(competition);
    }

    /// <summary>
    /// Prompt the user to pick a venue and table for a competition group.
    /// </summary>
    private async Task AssignGroupVenueAsync(CompetitionGroup group, Competition competition)
    {
        if (_editorViewModel == null) return;
        if (_dataStore.GetData().IsSeasonLocked(competition.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot change venue — this season is locked.", "OK");
            return;
        }

        var venues = GetVenuesForCurrentCompetition();
        if (venues.Count == 0)
        {
            await DisplayAlert("No Venues",
                "There are no venues defined for this competition's season. Add venues on the Venues page first.",
                "OK");
            return;
        }

        var hasAssignment = group.VenueId.HasValue;
        var venueOptions = venues.Select(v => v.Name ?? "(unnamed)").ToList();
        string? clearOption = hasAssignment ? "❌ Clear assignment" : null;

        var actions = clearOption != null
            ? new[] { clearOption }.Concat(venueOptions).ToArray()
            : venueOptions.ToArray();

        var chosen = await DisplayActionSheet($"Select Venue for {group.Name}", "Cancel", null, actions);
        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

        if (chosen == clearOption)
        {
            group.VenueId = null;
            group.VenueName = null;
            group.TableId = null;
            group.TableLabel = null;
            await _editorViewModel.SaveCompetitionAsync();
            SetStatus("Group venue/table cleared");
            RerenderCompetitionView(competition);
            return;
        }

        var venue = venues.FirstOrDefault(v => (v.Name ?? "(unnamed)") == chosen);
        if (venue == null) return;

        var tables = venue.Tables?.OrderBy(t => t.Label).ToList() ?? new List<VenueTable>();
        Guid? tableId = null;
        string? tableLabel = null;

        if (tables.Count > 0)
        {
            var tableNames = tables.Select(t => t.Label ?? "(unnamed)").ToArray();
            var chosenTable = await DisplayActionSheet("Select Table", "Cancel", null, tableNames);
            if (string.IsNullOrEmpty(chosenTable) || chosenTable == "Cancel") return;

            var table = tables.FirstOrDefault(t => (t.Label ?? "(unnamed)") == chosenTable);
            if (table != null)
            {
                tableId = table.Id;
                tableLabel = table.Label;
            }
        }

        group.VenueId = venue.Id;
        group.VenueName = venue.Name;
        group.TableId = tableId;
        group.TableLabel = tableLabel;

        await _editorViewModel.SaveCompetitionAsync();
        SetStatus($"Assigned {group.Name} → {venue.Name}{(tableLabel != null ? " — " + tableLabel : "")}");
        RerenderCompetitionView(competition);
    }

    /// <summary>
    /// After a venue/table change, refresh whichever view is currently active so
    /// the new assignment shows up immediately.
    /// </summary>
    private void RerenderCompetitionView(Competition competition)
    {
        if (competition.Format == CompetitionFormat.SinglesGroupStage ||
            competition.Format == CompetitionFormat.DoublesGroupStage)
        {
            ShowGroupsView();
        }
        else if (competition.Format == CompetitionFormat.RoundRobin)
        {
            ShowRoundRobinView(competition);
        }
        else
        {
            ShowTournamentBracket(competition);
        }
    }

    /// <summary>
    /// Manually edit which participants are in a match: swap home/away,
    /// change either side, or clear a slot. Refreshes the view on save.
    /// </summary>
    private async Task EditMatchTeamsAsync(CompetitionMatch match, Competition competition)
    {
        if (_editorViewModel == null) return;
        if (_dataStore.GetData().IsSeasonLocked(competition.SeasonId))
        {
            await DisplayAlert($"{Helpers.Emojis.Lock} Season Locked",
                "Cannot change teams — this season is locked.", "OK");
            return;
        }

        var p1Name = GetParticipantName(match.Participant1Id, competition.Format) ?? "TBD";
        var p2Name = GetParticipantName(match.Participant2Id, competition.Format) ?? "TBD";

        var options = new List<string>();
        if (match.Participant1Id.HasValue && match.Participant2Id.HasValue)
            options.Add("🔁 Swap home / away");
        options.Add($"✏️ Change home ({p1Name})");
        options.Add($"✏️ Change away ({p2Name})");
        if (match.Participant1Id.HasValue) options.Add($"❌ Clear home ({p1Name})");
        if (match.Participant2Id.HasValue) options.Add($"❌ Clear away ({p2Name})");

        var chosen = await DisplayActionSheet("Edit Match Teams", "Cancel", null, options.ToArray());
        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return;

        if (chosen.StartsWith("🔁"))
        {
            await _editorViewModel.SwapMatchParticipantsAsync(match.Id);
        }
        else if (chosen.StartsWith("❌"))
        {
            bool isSlot1 = chosen.Contains("Clear home");
            await _editorViewModel.ClearMatchSlotAsync(match.Id, isSlot1);
        }
        else
        {
            bool isSlot1 = chosen.Contains("Change home");
            var picked = await PickParticipantAsync(competition, isSlot1 ? "Select Home" : "Select Away", match);
            if (!picked.HasValue) return;
            await _editorViewModel.AssignParticipantToMatchAsync(match.Id, isSlot1, picked.Value);
        }

        SetStatus(_editorViewModel.StatusMessage);
        RerenderCompetitionView(competition);
    }

    /// <summary>
    /// Show a picker for any participant in the competition.
    /// Returns null if the user cancels.
    /// </summary>
    private async Task<Guid?> PickParticipantAsync(Competition competition, string title, CompetitionMatch? excludeFromMatch = null)
    {
        if (_editorViewModel == null) return null;

        var all = _editorViewModel.GetAllParticipants();
        if (all.Count == 0)
        {
            await DisplayAlert("No Participants", "This competition has no participants to choose from.", "OK");
            return null;
        }

        var excludeIds = new HashSet<Guid>();
        if (excludeFromMatch != null)
        {
            if (excludeFromMatch.Participant1Id.HasValue) excludeIds.Add(excludeFromMatch.Participant1Id.Value);
            if (excludeFromMatch.Participant2Id.HasValue) excludeIds.Add(excludeFromMatch.Participant2Id.Value);
        }

        var pickable = all.Where(p => !excludeIds.Contains(p.Id))
                          .OrderBy(p => p.Name)
                          .ToList();

        if (pickable.Count == 0)
        {
            await DisplayAlert("No Participants", "No other participants available.", "OK");
            return null;
        }

        var names = pickable.Select(p => p.Name).ToArray();
        var chosen = await DisplayActionSheet(title, "Cancel", null, names);
        if (string.IsNullOrEmpty(chosen) || chosen == "Cancel") return null;

        return pickable.FirstOrDefault(p => p.Name == chosen)?.Id;
    }
}
