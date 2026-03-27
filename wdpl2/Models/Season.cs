using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
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
        [Required, MaxLength(100)]
        public string Name { get; set; } = string.Empty;

        /// <summary>Inclusive start date (date-only).</summary>
        public DateTime StartDate { get; set; } = DateTime.Today;

        /// <summary>Inclusive end date (date-only).</summary>
        public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(3);

        /// <summary>Weekly match day, e.g. Tuesday.</summary>
        public DayOfWeek MatchDayOfWeek { get; set; } = DayOfWeek.Tuesday;

        /// <summary>Typical start time for matches on the weekly day.</summary>
        public TimeSpan MatchStartTime { get; set; } = new TimeSpan(20, 0, 0); // 20:00

        /// <summary>
        /// Frames per match for this season.
        /// Default is 0, which means "use app settings DefaultFramesPerMatch".
        /// Set to a specific value (e.g., 10, 15) to override for this season only.
        /// When <see cref="IncludeDoubles"/> is true, this value is ignored in favour of
        /// <see cref="SinglesFrameCount"/> + <see cref="DoublesFrameCount"/>.
        /// </summary>
        public int FramesPerMatch { get; set; } = 0;

        /// <summary>Whether this season includes doubles frames in matches.</summary>
        public bool IncludeDoubles { get; set; }

        /// <summary>Number of singles frames per match when doubles is enabled.</summary>
        public int SinglesFrameCount { get; set; }

        /// <summary>Number of doubles frames per match when doubles is enabled.</summary>
        public int DoublesFrameCount { get; set; }

        /// <summary>Optional transfer window start date. Null = no restriction.</summary>
        public DateTime? TransferWindowStart { get; set; }

        /// <summary>Optional transfer window end date. Null = no restriction.</summary>
        public DateTime? TransferWindowEnd { get; set; }

        /// <summary>When this record was created.</summary>
        public DateTime CreatedDate { get; set; } = DateTime.UtcNow;

        /// <summary>When this record was last modified.</summary>
        public DateTime? ModifiedDate { get; set; }

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
