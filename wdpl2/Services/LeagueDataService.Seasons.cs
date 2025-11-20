// File: Services/LeagueDataService.Seasons.cs
using System;
using System.Collections.Generic;
using System.Linq;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    public partial class LeagueDataService
    {
        public List<Season> Seasons => League.Seasons;

        public Season? GetSeason(Guid id) => League.Seasons.FirstOrDefault(s => s.Id == id);

        public void AddOrUpdateSeason(Season season)
        {
            if (season == null) throw new ArgumentNullException(nameof(season));
            var existing = GetSeason(season.Id);
            if (existing == null)
                League.Seasons.Add(season);
            else
            {
                existing.Name = season.Name;
                existing.StartDate = season.StartDate;
                existing.EndDate = season.EndDate;
                existing.IsActive = season.IsActive;
            }
        }

        public void RemoveSeason(Guid seasonId)
        {
            League.Seasons.RemoveAll(s => s.Id == seasonId);
            League.Fixtures.RemoveAll(f => f.SeasonId == seasonId);
        }

        public void SetActiveSeason(Guid activeSeasonId)
        {
            foreach (var s in League.Seasons)
                s.IsActive = s.Id == activeSeasonId;
        }

        public Season? GetActiveSeason() => League.Seasons.FirstOrDefault(s => s.IsActive);
    }
}
