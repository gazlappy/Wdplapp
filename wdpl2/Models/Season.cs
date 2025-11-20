using System;
using System.Collections.Generic;
using System.Linq;

namespace Wdpl2.Models
{
    /// <summary>
    /// A league season with weekly match rules and blackout dates.
    /// </summary>
    public sealed class Season
    {
        public bool IsActive { get; set; } = true;

        public Guid Id { get; set; } = Guid.NewGuid();

        /// <summary>Human-friendly label, e.g. "Winter 2025".</summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>Inclusive start date (date-only).</summary>
        public DateTime StartDate { get; set; } = DateTime.Today;

        /// <summary>Inclusive end date (date-only).</summary>
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(3);

        /// <summary>Weekly match day, e.g. Tuesday.</summary>
        public DayOfWeek MatchDayOfWeek { get; set; } = DayOfWeek.Tuesday;

        /// <summary>Typical start time for matches on the weekly day.</summary>
        public TimeSpan MatchStartTime { get; set; } = new TimeSpan(20, 0, 0); // 20:00

        /// <summary>Frames per match (used elsewhere in the app).</summary>
        public int FramesPerMatch { get; set; } = 10;

        /// <summary>Date-only list of days where no fixtures should be scheduled.</summary>
        public List<DateTime> BlackoutDates { get; set; } = new();

        /// <summary>Normalise Start/End and BlackoutDates to date-only (00:00) and dedupe.</summary>
        public void NormaliseDates()
        {
            StartDate = StartDate.Date;
            EndDate = EndDate.Date;

            if (BlackoutDates is null)
            {
                BlackoutDates = new List<DateTime>();
                return;
            }

            for (int i = 0; i < BlackoutDates.Count; i++)
                BlackoutDates[i] = BlackoutDates[i].Date;

            BlackoutDates = BlackoutDates
                .Select(d => d.Date)
                .Distinct()
                .OrderBy(d => d)
                .ToList();
        }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Name)
                ? $"{StartDate:dd MMM yyyy} – {EndDate:dd MMM yyyy}"
                : Name;
    }
}
