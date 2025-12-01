using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Service to schedule match reminder notifications
/// </summary>
public class MatchReminderService
{
    private readonly INotificationService _notificationService;
    private readonly IDataStore _dataStore;
    
    // Base notification ID for match reminders (10000-19999)
    private const int BaseNotificationId = 10000;
    
    // NEW: Track scheduled reminders in memory
    private readonly List<ScheduledReminder> _scheduledReminders = new();

    public MatchReminderService(INotificationService notificationService, IDataStore dataStore)
    {
        _notificationService = notificationService;
        _dataStore = dataStore;
    }
    
    // NEW: Model for scheduled reminders
    public class ScheduledReminder
    {
        public Guid FixtureId { get; set; }
        public DateTime MatchDate { get; set; }
        public DateTime ReminderTime { get; set; }
        public string HomeTeam { get; set; } = "";
        public string AwayTeam { get; set; } = "";
        public int NotificationId { get; set; }
    }

    /// <summary>
    /// Schedule reminder for a specific match
    /// </summary>
    public async Task ScheduleMatchReminderAsync(Guid fixtureId, DateTime matchDate, string homeTeam, string awayTeam, int hoursBeforeMatch = 2)
    {
        try
        {
            var reminderTime = matchDate.AddHours(-hoursBeforeMatch);

            // Only schedule if in the future
            if (reminderTime <= DateTime.Now)
                return;

            var notificationId = BaseNotificationId + Math.Abs(fixtureId.GetHashCode() % 10000);
            var title = $"?? Match Reminder";
            var message = $"{homeTeam} vs {awayTeam} starts in {hoursBeforeMatch} hours";

            await _notificationService.ScheduleNotificationAsync(
                notificationId,
                title,
                message,
                reminderTime);
                
            // Track this reminder
            _scheduledReminders.RemoveAll(r => r.FixtureId == fixtureId); // Remove old if exists
            _scheduledReminders.Add(new ScheduledReminder
            {
                FixtureId = fixtureId,
                MatchDate = matchDate,
                ReminderTime = reminderTime,
                HomeTeam = homeTeam,
                AwayTeam = awayTeam,
                NotificationId = notificationId
            });
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schedule match reminder error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Cancel a specific match reminder
    /// </summary>
    public async Task CancelMatchReminderAsync(Guid fixtureId)
    {
        try
        {
            var reminder = _scheduledReminders.FirstOrDefault(r => r.FixtureId == fixtureId);
            if (reminder != null)
            {
                await _notificationService.CancelNotificationAsync(reminder.NotificationId);
                _scheduledReminders.Remove(reminder);
            }
            else
            {
                // Try to cancel by calculating the notification ID
                var notificationId = BaseNotificationId + Math.Abs(fixtureId.GetHashCode() % 10000);
                await _notificationService.CancelNotificationAsync(notificationId);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cancel match reminder error: {ex.Message}");
        }
    }
    
    /// <summary>
    /// Get all scheduled reminders
    /// </summary>
    public async Task<List<ScheduledReminder>> GetAllScheduledRemindersAsync()
    {
        try
        {
            // Clean up past reminders
            var now = DateTime.Now;
            _scheduledReminders.RemoveAll(r => r.ReminderTime < now);
            
            return await Task.FromResult(_scheduledReminders.OrderBy(r => r.MatchDate).ToList());
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Get scheduled reminders error: {ex.Message}");
            return new List<ScheduledReminder>();
        }
    }

    /// <summary>
    /// Schedule reminders for a specific player's upcoming matches
    /// </summary>
    public async Task SchedulePlayerMatchRemindersAsync(Guid playerId, int hoursBeforeMatch = 2)
    {
        try
        {
            var player = _dataStore.GetData().Players?.FirstOrDefault(p => p.Id == playerId);
            if (player == null)
                return;

            var team = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == player.TeamId);
            if (team == null)
                return;

            // Get upcoming fixtures for this player's team
            var upcomingFixtures = _dataStore.GetData().Fixtures?
                .Where(f => (f.HomeTeamId == team.Id || f.AwayTeamId == team.Id) && 
                           f.Date > DateTime.Now &&
                           f.Date < DateTime.Now.AddDays(30)) // Next 30 days
                .OrderBy(f => f.Date)
                .ToList();

            if (upcomingFixtures == null || !upcomingFixtures.Any())
                return;

            foreach (var fixture in upcomingFixtures)
            {
                var opponent = fixture.HomeTeamId == team.Id
                    ? _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.AwayTeamId)
                    : _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.HomeTeamId);

                var venue = _dataStore.GetData().Venues?.FirstOrDefault(v => v.Id == fixture.VenueId);
                var isHome = fixture.HomeTeamId == team.Id;

                var homeTeamName = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.HomeTeamId)?.Name ?? "Home";
                var awayTeamName = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.AwayTeamId)?.Name ?? "Away";

                await ScheduleMatchReminderAsync(fixture.Id, fixture.Date, homeTeamName, awayTeamName, hoursBeforeMatch);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schedule player reminders error: {ex.Message}");
        }
    }

    /// <summary>
    /// Schedule reminders for all players in a team
    /// </summary>
    public async Task ScheduleTeamMatchRemindersAsync(Guid teamId, int hoursBeforeMatch = 2)
    {
        try
        {
            var team = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == teamId);
            if (team == null)
                return;

            var players = _dataStore.GetData().Players?
                .Where(p => p.TeamId == teamId)
                .ToList();

            if (players == null || !players.Any())
                return;

            foreach (var player in players)
            {
                await SchedulePlayerMatchRemindersAsync(player.Id, hoursBeforeMatch);
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schedule team reminders error: {ex.Message}");
        }
    }

    /// <summary>
    /// Schedule reminder for a specific fixture
    /// </summary>
    public async Task ScheduleFixtureReminderAsync(Fixture fixture, int hoursBeforeMatch = 2)
    {
        try
        {
            var homeTeam = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
            var awayTeam = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.AwayTeamId);

            await ScheduleMatchReminderAsync(
                fixture.Id,
                fixture.Date,
                homeTeam?.Name ?? "Home",
                awayTeam?.Name ?? "Away",
                hoursBeforeMatch);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schedule fixture reminder error: {ex.Message}");
        }
    }

    /// <summary>
    /// Cancel all match reminder notifications
    /// </summary>
    public async Task CancelAllMatchRemindersAsync()
    {
        try
        {
            // Cancel all tracked reminders
            foreach (var reminder in _scheduledReminders.ToList())
            {
                await _notificationService.CancelNotificationAsync(reminder.NotificationId);
            }
            
            _scheduledReminders.Clear();
            
            // Also cancel any untracked notifications in our ID range
            for (int i = BaseNotificationId; i < BaseNotificationId + 10000; i++)
            {
                try
                {
                    await _notificationService.CancelNotificationAsync(i);
                }
                catch
                {
                    // Ignore errors for non-existent notifications
                }
            }
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Cancel all reminders error: {ex.Message}");
        }
    }

    /// <summary>
    /// Send a notification for a match result
    /// </summary>
    public async Task NotifyMatchResultAsync(Fixture fixture)
    {
        try
        {
            var homeTeam = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.HomeTeamId);
            var awayTeam = _dataStore.GetData().Teams?.FirstOrDefault(t => t.Id == fixture.AwayTeamId);

            var homeScore = fixture.HomeScore;
            var awayScore = fixture.AwayScore;

            var winner = homeScore > awayScore ? homeTeam?.Name : awayTeam?.Name;
            var title = "?? Match Result";
            var message = $"{homeTeam?.Name ?? "Home"} {homeScore} - {awayScore} {awayTeam?.Name ?? "Away"}";

            if (homeScore != awayScore)
            {
                message += $" • {winner} wins!";
            }
            else
            {
                message += " • Draw!";
            }

            var notificationId = BaseNotificationId + 5000 + Math.Abs(fixture.Id.GetHashCode() % 5000);

            await _notificationService.ShowNotificationAsync(
                notificationId,
                title,
                message);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Notify match result error: {ex.Message}");
        }
    }

    /// <summary>
    /// Schedule a weekly fixture list notification
    /// </summary>
    public async Task ScheduleWeeklyFixtureListAsync(DayOfWeek dayOfWeek, TimeSpan time)
    {
        try
        {
            var now = DateTime.Now;
            var daysUntilTarget = ((int)dayOfWeek - (int)now.DayOfWeek + 7) % 7;
            if (daysUntilTarget == 0)
                daysUntilTarget = 7; // Next week if today

            var nextOccurrence = now.Date.AddDays(daysUntilTarget).Add(time);

            var upcomingFixtures = _dataStore.GetData().Fixtures?
                .Where(f => f.Date > now && f.Date < now.AddDays(7))
                .OrderBy(f => f.Date)
                .Take(5)
                .ToList();

            if (upcomingFixtures == null || !upcomingFixtures.Any())
                return;

            var title = "?? This Week's Fixtures";
            var message = $"{upcomingFixtures.Count} matches this week";

            var notificationId = 20000; // Fixed ID for weekly fixture list

            await _notificationService.ScheduleNotificationAsync(
                notificationId,
                title,
                message,
                nextOccurrence);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Schedule weekly fixture list error: {ex.Message}");
        }
    }
}
