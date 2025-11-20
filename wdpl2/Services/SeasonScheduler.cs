using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    public static class SeasonScheduler
    {
        public static List<DateTime> GenerateMatchNights(Season season)
        {
            if (season is null) throw new ArgumentNullException(nameof(season));
            season.NormaliseDates();

            var results = new List<DateTime>();
            if (season.EndDate < season.StartDate) return results;

            // first occurrence of the desired day on/after StartDate
            var first = season.StartDate;
            while (first.DayOfWeek != season.MatchDayOfWeek)
                first = first.AddDays(1);

            var blackout = new HashSet<DateTime>(season.BlackoutDates.Select(d => d.Date));

            for (var d = first; d <= season.EndDate; d = d.AddDays(7))
            {
                if (blackout.Contains(d.Date)) continue;
                results.Add(d.Date + season.MatchStartTime);
            }

            return results;
        }
    }
}
