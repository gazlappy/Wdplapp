// File: Services/LeagueDataService.cs
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Wdpl2.Models;

namespace Wdpl2.Services
{
    public partial class LeagueDataService
    {
        // ✅ Single shared instance for the whole app
        public static LeagueDataService Instance { get; } = new LeagueDataService();

        // Prevent accidental "new" use outside this class (use Instance)
        private LeagueDataService() { }

        public LeagueData League { get; private set; } = new();

        public void ReplaceFixturesForSeason(Guid seasonId, IEnumerable<Fixture> fixtures)
        {
            if (fixtures == null) throw new ArgumentNullException(nameof(fixtures));
            League.Fixtures.RemoveAll(f => f.SeasonId == seasonId);
            League.Fixtures.AddRange(fixtures);
        }

        public Task SaveAsync() => Task.CompletedTask;
    }
}
