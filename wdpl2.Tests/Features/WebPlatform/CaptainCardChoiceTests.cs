using System.Text.RegularExpressions;

namespace wdpl2.Tests;

/// <summary>
/// The captains' page is the one door: whatever is open for a team is found
/// there, league night or cup tie.
/// </summary>
/// <remarks>
/// Neither side of this can be run here - the page is JavaScript and the
/// module needs MySQL - so what is checked is the contract between them: the
/// action exists, the page asks for it, and every field the page reads is one
/// the PHP actually sends. That is the join that broke silently before.
/// </remarks>
public class CaptainCardChoiceTests
{
    private static string Read(params string[] parts) =>
        File.ReadAllText(Path.Combine(new[] { RepoRoot().FullName, "wdpl2", "web-backend" }.Concat(parts).ToArray()));

    private static string CaptainPage() => Read("captain", "index.html");

    private static string ScorecardsModule() => Read("api", "modules", "scorecards", "Module.php");

    [Fact]
    public void The_captains_page_asks_for_every_card_open_for_the_team()
    {
        var page = CaptainPage();

        Assert.Contains("call('scorecards','cards',{})", page);

        // The old one-kind-at-a-time call is what sent captains looking for
        // another page when their tie did not appear.
        Assert.DoesNotContain("'mine',{kind:", page);
    }

    [Fact]
    public void The_cards_action_is_registered_for_captains()
    {
        var php = ScorecardsModule();

        Assert.Matches(@"'cards'\s*=>\s*\['role'\s*=>\s*Role::Captain", php);
    }

    [Fact]
    public void Only_live_cards_of_the_captains_own_team_are_listed()
    {
        var body = CardsBody();

        Assert.Contains("Captain::requireTeamId()", body);
        Assert.Contains("c.state = ?", body);
        Assert.Contains("self::STATE_LIVE", body);
        Assert.Contains("f.home_team_id = ? OR f.away_team_id = ?", body);
    }

    [Fact]
    public void Every_field_the_page_reads_off_a_card_is_one_the_server_sends()
    {
        var sent = Regex.Matches(CardsBody(), @"'([a-z_][a-z0-9_]*)'\s*=>")
            .Select(m => m.Groups[1].Value)
            .ToHashSet(StringComparer.Ordinal);

        Assert.NotEmpty(sent);

        // What the chooser puts on screen, and what it picks a card by.
        foreach (var field in new[] { "fixture_id", "kind", "match_date", "opponent_name" })
            Assert.Contains(field, sent);

        // Only where the page handles a card from this action: elsewhere "c"
        // is any old variable.
        var chooser = Regex.Match(CaptainPage(), @"function renderPicks\(\).*?\n  \}", RegexOptions.Singleline);
        Assert.True(chooser.Success, "No renderPicks in the captains' page.");

        foreach (var read in Regex.Matches(chooser.Value, @"\bc\.([a-z_][a-z0-9_]*)\b")
                     .Select(m => m.Groups[1].Value).Distinct())
            Assert.True(sent.Contains(read), $"The captains' page reads c.{read}, which the cards action never sends.");
    }

    [Fact]
    public void A_live_tie_is_scored_from_its_own_row_on_the_competitions_tab()
    {
        var page = CaptainPage();

        Assert.Contains("Score this tie", page);
        Assert.Contains("data-tie=", page);

        // The tab used to be a signpost to another page; now it is the way in.
        Assert.DoesNotContain("Go to the competitions page", page);
    }

    [Fact]
    public void The_competitions_page_no_longer_signs_captains_in_for_a_tie()
    {
        var comp = Read("comp", "index.html");

        // Cup ties belong to the captains' own page: a second door here is a
        // second place to look when the card is not where it was expected.
        Assert.Contains("r.kind !== 'cup'", comp);
        Assert.DoesNotContain("location.href='tie.html'", comp);
        Assert.DoesNotContain("call('captains','login'", comp);
    }

    /// <summary>
    /// On a cup tie the coin decides who is home. Anything that answers
    /// "which side" has to ask the card, not the fixture's row, or the two
    /// disagree: the picker offered the other team's players under your own
    /// column, and a walk-in added from it joined the opposition.
    /// </summary>
    [Fact]
    public void The_squads_are_keyed_by_the_cards_sides_not_the_draws()
    {
        var body = Body("roster");

        Assert.Contains("self::sides($fixtureId)", body);
        Assert.DoesNotContain("FROM wdpl_fixtures", body);
    }

    [Fact]
    public void The_squads_are_read_again_once_the_toss_is_known()
    {
        var tossed = Regex.Match(CaptainPage(), @"function tossed\([^)]*\).*?\n  \}", RegexOptions.Singleline);

        Assert.True(tossed.Success, "No tossed() in the captains' page.");
        Assert.Contains("loadRosters()", tossed.Value);
    }

    /// <summary>
    /// The order a card is filled in holds for everyone. It used to be
    /// skipped in solo, which let a cup card be filled in any order at all.
    /// </summary>
    [Fact]
    public void Every_pick_goes_through_the_order_check_solo_included()
    {
        var php = ScorecardsModule();
        var setPlayer = Regex.Match(php, @"case 'set_player':.*?case 'set_doubles':", RegexOptions.Singleline);
        Assert.True(setPlayer.Success, "No set_player branch in the scorecards module.");

        Assert.Contains("NominationOrder::refusal($frames, $index, $slot, $cup)", setPlayer.Value);
        Assert.DoesNotContain("!$driving", setPlayer.Value);
    }

    [Fact]
    public void The_page_asks_the_order_for_the_columns_own_side_in_solo_too()
    {
        var page = CaptainPage();

        Assert.Contains("var locked = !mine || orderLocked(index, side);", page);
        Assert.DoesNotContain("!S.solo && (cup", page);
    }

    /// <summary>
    /// The toss winner chooses home or away. The page asks who won and what
    /// they chose, and the server works out which side is home from both.
    /// </summary>
    [Fact]
    public void The_toss_sends_who_won_and_what_they_chose()
    {
        var page = CaptainPage();
        Assert.Contains("call('scorecards','toss',{fixtureId:S.fixtureId, wonBy:wonBy, choice:choice})", page);
        Assert.Contains("id=\"tossHome\"", page);
        Assert.Contains("id=\"tossAway\"", page);

        var toss = Body("toss");
        Assert.Contains("Http::field('choice', 'home')", toss);
        Assert.Contains("CupRules::homeSide($wonBy, $choice)", toss);
    }

    private static string CardsBody() => Body("cards");

    /// <summary>One action's PHP, read out of the module.</summary>
    private static string Body(string action)
    {
        var match = Regex.Match(
            ScorecardsModule(),
            @"public static function " + action + @"\(\).*?\n    \}",
            RegexOptions.Singleline);

        Assert.True(match.Success, $"No {action}() action in the scorecards module.");
        return match.Value;
    }

    private static DirectoryInfo RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "wdpl2.sln")))
            dir = dir.Parent;

        Assert.NotNull(dir);
        return dir!;
    }
}
