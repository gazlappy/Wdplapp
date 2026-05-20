using System.Text.Json;

namespace Wdpl2.Services.Inbox;

/// <summary>
/// Groups <see cref="WebSubmission"/> rows of type <c>match_result</c>
/// by <see cref="WebSubmission.ReferenceId"/> (the fixture id) so the Inbox
/// page can show paired captain cards side-by-side and flag agreement/disputes.
/// </summary>
public sealed class FixtureSubmissionGroup
{
    public Guid FixtureId { get; init; }
    public string? HomeTeamName { get; init; }
    public string? AwayTeamName { get; init; }
    public string? FixtureDate { get; init; }

    public WebSubmission? HomeCard { get; init; }
    public WebSubmission? AwayCard { get; init; }
    public MatchResultPayload? HomePayload { get; init; }
    public MatchResultPayload? AwayPayload { get; init; }

    /// <summary>Any other unmatched submissions for this fixture (re-submissions, etc.).</summary>
    public List<WebSubmission> Extras { get; init; } = new();

    public bool HasBothSides => HomeCard != null && AwayCard != null;
    public bool IsAgreed     => HasBothSides && DisputeCount == 0;
    public bool IsDisputed   => HasBothSides && DisputeCount > 0;

    /// <summary>Number of frames where the two cards disagree on the winner.</summary>
    public int DisputeCount { get; init; }

    /// <summary>Per-frame side-by-side rows used to render a diff in the inbox UI.</summary>
    public List<FrameDiffRow> FrameRows { get; init; } = new();

    public string Summary
    {
        get
        {
            var teams = $"{HomeTeamName ?? "Home"} vs {AwayTeamName ?? "Away"}";
            var date  = string.IsNullOrWhiteSpace(FixtureDate) ? "" : $"  ({FixtureDate})";
            return teams + date;
        }
    }

    public string Status
    {
        get
        {
            if (!HasBothSides)
                return HomeCard != null
                    ? "Awaiting away captain"
                    : AwayCard != null
                        ? "Awaiting home captain"
                        : "No cards yet";
            return IsAgreed ? "Agreed - ready to import"
                            : $"{DisputeCount} frame(s) disputed";
        }
    }
}

public sealed class FrameDiffRow
{
    public int Number { get; init; }
    public string HomeText { get; init; } = "";
    public string AwayText { get; init; } = "";
    public bool IsAgreed { get; init; }
    public bool MissingHome { get; init; }
    public bool MissingAway { get; init; }

    public string Status => MissingHome ? "missing on home card"
                          : MissingAway ? "missing on away card"
                          : IsAgreed     ? "agreed"
                          : "disputed";
}

public static class SubmissionGrouper
{
    private static readonly JsonSerializerOptions _opts = new()
    {
        PropertyNameCaseInsensitive = true,
        NumberHandling = System.Text.Json.Serialization.JsonNumberHandling.AllowReadingFromString,
    };

    /// <summary>
    /// Turns a flat list of pending submissions into one group per fixture.
    /// Non-<c>match_result</c> submissions are returned in <paramref name="other"/>.
    /// </summary>
    public static List<FixtureSubmissionGroup> Group(
        IEnumerable<WebSubmission> submissions,
        out List<WebSubmission> other)
    {
        other = new List<WebSubmission>();
        var byFixture = new Dictionary<Guid, List<(WebSubmission sub, MatchResultPayload payload)>>();

        foreach (var s in submissions)
        {
            if (!string.Equals(s.Type, "match_result", StringComparison.OrdinalIgnoreCase))
            {
                other.Add(s);
                continue;
            }

            MatchResultPayload? payload;
            try
            {
                payload = JsonSerializer.Deserialize<MatchResultPayload>(s.Payload.GetRawText(), _opts);
            }
            catch
            {
                other.Add(s);
                continue;
            }

            if (payload is null || payload.FixtureId == Guid.Empty)
            {
                other.Add(s);
                continue;
            }

            if (!byFixture.TryGetValue(payload.FixtureId, out var list))
                byFixture[payload.FixtureId] = list = new();
            list.Add((s, payload));
        }

        var groups = new List<FixtureSubmissionGroup>();
        foreach (var kv in byFixture)
        {
            // Keep the newest card per side; older ones go to Extras.
            var sorted = kv.Value
                .OrderByDescending(x => x.sub.ReceivedUtc ?? "")
                .ThenByDescending(x => x.sub.Id)
                .ToList();

            (WebSubmission sub, MatchResultPayload payload)? home = null;
            (WebSubmission sub, MatchResultPayload payload)? away = null;
            var extras = new List<WebSubmission>();

            foreach (var entry in sorted)
            {
                var side = entry.payload.SubmittedBy?.Side?.ToLowerInvariant();
                if (side == "home" && home is null)      home = entry;
                else if (side == "away" && away is null) away = entry;
                else                                     extras.Add(entry.sub);
            }

            var meta = home?.payload.FixtureMeta ?? away?.payload.FixtureMeta;
            var rows = BuildRows(home?.payload, away?.payload);
            int disputes = rows.Count(r => !r.IsAgreed);

            groups.Add(new FixtureSubmissionGroup
            {
                FixtureId     = kv.Key,
                HomeTeamName  = meta?.HomeTeamName,
                AwayTeamName  = meta?.AwayTeamName,
                FixtureDate   = meta?.FixtureDate,
                HomeCard      = home?.sub,
                AwayCard      = away?.sub,
                HomePayload   = home?.payload,
                AwayPayload   = away?.payload,
                DisputeCount  = disputes,
                FrameRows     = rows,
                Extras        = extras,
            });
        }

        return groups
            .OrderByDescending(g => g.HasBothSides)
            .ThenBy(g => g.FixtureDate)
            .ToList();
    }

    private static int CountDisputes(MatchResultPayload a, MatchResultPayload b)
    {
        var byNum = b.Frames.ToDictionary(f => f.Number, f => f);
        int disputes = 0;
        foreach (var fa in a.Frames)
        {
            if (!byNum.TryGetValue(fa.Number, out var fb)) { disputes++; continue; }
            var wa = (fa.Winner ?? "").ToLowerInvariant();
            var wb = (fb.Winner ?? "").ToLowerInvariant();
            if (wa != wb) disputes++;
        }
        // Frames present in b but missing in a also count as disputed.
        var aNums = a.Frames.Select(f => f.Number).ToHashSet();
        disputes += b.Frames.Count(f => !aNums.Contains(f.Number));
        return disputes;
    }

    private static List<FrameDiffRow> BuildRows(MatchResultPayload? home, MatchResultPayload? away)
    {
        var rows = new List<FrameDiffRow>();
        var homeByNum = home?.Frames.ToDictionary(f => f.Number) ?? new();
        var awayByNum = away?.Frames.ToDictionary(f => f.Number) ?? new();
        var nums = homeByNum.Keys.Union(awayByNum.Keys).OrderBy(n => n).ToList();

        foreach (var n in nums)
        {
            homeByNum.TryGetValue(n, out var fh);
            awayByNum.TryGetValue(n, out var fa);

            bool missingHome = fh is null;
            bool missingAway = fa is null;
            bool agreed = !missingHome && !missingAway
                          && string.Equals((fh!.Winner ?? "").Trim(), (fa!.Winner ?? "").Trim(),
                                           StringComparison.OrdinalIgnoreCase);

            rows.Add(new FrameDiffRow
            {
                Number      = n,
                HomeText    = FormatFrame(fh),
                AwayText    = FormatFrame(fa),
                IsAgreed    = agreed,
                MissingHome = missingHome,
                MissingAway = missingAway,
            });
        }
        return rows;
    }

    private static string FormatFrame(MatchResultFrame? f)
    {
        if (f is null) return "-";
        var hp = f.HomePlayerName ?? "?";
        var ap = f.AwayPlayerName ?? "?";
        var w  = (f.Winner ?? "").ToLowerInvariant() switch
        {
            "home" => $"WIN {hp}",
            "away" => $"WIN {ap}",
            _      => "no winner",
        };
        var flags = (f.EightBall ? " 8B" : "") + (f.IsDoubles ? " D" : "");
        return $"{hp} vs {ap} - {w}{flags}";
    }
}
