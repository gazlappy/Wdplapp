using System;
using Microsoft.Maui.Controls;
using Microsoft.Maui.Controls.Shapes;
using Wdpl2.Helpers;
using static Wdpl2.Helpers.PanelBuilder;

namespace Wdpl2.Views
{
    /// <summary>
    /// Help: the user manual, and the plain-English guide to how ratings work.
    /// </summary>
    /// <remarks>
    /// These lived inside Settings as two more categories, which is the last
    /// place someone new looks for help. Here they have their own entry at the
    /// foot of the sidebar.
    /// </remarks>
    public class HelpPage : ContentPage
    {
        private readonly ContentView _panel = new() { Padding = new Thickness(12) };
        private readonly Button _manualButton;
        private readonly Button _ratingsButton;

        public HelpPage()
        {
            Title = "Help";

            _manualButton = Choice("User manual", () => Show(manual: true));
            _ratingsButton = Choice("How ratings work", () => Show(manual: false));

            var choices = new HorizontalStackLayout
            {
                Spacing = 8,
                Padding = new Thickness(12, 12, 12, 0),
                Children = { _manualButton, _ratingsButton },
            };

            var grid = new Grid { RowDefinitions = { new(GridLength.Auto), new(GridLength.Star) } };
            grid.Add(choices, 0, 0);
            grid.Add(new ScrollView { Content = _panel }, 0, 1);
            Content = grid;

            Show(manual: true);
        }

        private static Button Choice(string text, Action onClick)
        {
            var button = new Button { Text = text, CornerRadius = 16, Padding = new Thickness(16, 6), FontSize = 13 };
            button.Clicked += (_, _) => onClick();
            return button;
        }

        private void Show(bool manual)
        {
            _panel.Content = manual ? CreateManualPanel() : CreateRatingsGuidePanel();

            var on = Color.FromArgb("#16634B");
            foreach (var (button, selected) in new[] { (_manualButton, manual), (_ratingsButton, !manual) })
            {
                button.BackgroundColor = selected ? on : Colors.Transparent;
                button.TextColor = selected ? Colors.White : on;
                button.BorderColor = on;
                button.BorderWidth = selected ? 0 : 1;
            }
        }

        // ═══════════════════════════════════════════════════════════
        //  RATINGS GUIDE  ("Ratings for Dummies")
        // ═══════════════════════════════════════════════════════════

        /// <summary>
        /// A friendly, plain-English walkthrough of how the app calculates player ratings.
        /// Reuses the collapsible <see cref="ManualSectionView"/> for a consistent look
        /// with the in-app Manual.
        /// </summary>
        private View CreateRatingsGuidePanel()
        {
            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Chart, "Ratings for Dummies",
                "Plain-English guide to how player ratings work — tap any chapter to expand it"));

            // Track sections so the expand/collapse-all buttons can drive them.
            var sections = new List<ManualSectionView>();

            View Add(string icon, string title, string body, bool startExpanded = false)
            {
                var s = ManualSection(icon, title, body, startExpanded);
                sections.Add(s);
                return s;
            }

            // ---- Expand / Collapse all ----
            var expandAllBtn = new Button
            {
                Text = $"{Emojis.Down}  Expand all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            expandAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            expandAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(true); };

            var collapseAllBtn = new Button
            {
                Text = $"{Emojis.Up}  Collapse all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            collapseAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            collapseAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(false); };

            root.Children.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 8),
                Children = { expandAllBtn, collapseAllBtn }
            });

            // ---- Foreword (always visible) ----
            root.Children.Add(InfoPanel("Foreword: what is a rating?",
                "A rating is just a number that describes how strong a player is. Higher = better. " +
                "Everyone starts at the same value (1000 by default), and the number goes up or down based on:\n\n" +
                "  • whether you won or lost each frame, and\n" +
                "  • how strong your opponent was at the time.\n\n" +
                "Beating a strong player gains you more than beating a weak player. " +
                "Losing to a weak player costs you more than losing to a strong one. " +
                "That\u2019s the whole idea — the rest is just maths."));

            // ---- Chapter 1 ----
            root.Children.Add(Add(Emojis.Star, "Chapter 1 — The big picture (in 30 seconds)",
                "Every match night you play a few frames. For each frame the app asks two questions:\n\n" +
                $"  1. Did you win or lose?\n" +
                $"  2. How strong was your opponent at the start of this week?\n\n" +
                "It turns each frame into a number called the \u201Cframe value\u201D, then mixes all your " +
                "frame values together using a weighted average. Recent frames count more than ancient ones.\n\n" +
                "That weighted average is your rating. Done.\n\n" +
                "Everything in the rest of this guide is just explaining the dials you can turn.",
                startExpanded: true));

            // ---- Chapter 2 ----
            root.Children.Add(Add(Emojis.Sparkles, "Chapter 2 — Where everyone starts",
                "Every player starts at the Starting Rating (default 1000) the very first week of their first season.\n\n" +
                $"{Emojis.Bullet} 1000 is just a number — it has no real-world meaning. It could be 100 or 500.\n" +
                $"{Emojis.Bullet} The only thing that matters is the difference between players. A 1100 player " +
                "is stronger than a 950 player, that\u2019s all.\n" +
                $"{Emojis.Bullet} New players added mid-season also start at 1000 in their first week.\n\n" +
                "You can change the starting value under Settings → Player Ratings, but unless you have a " +
                "reason to, leave it at 1000."));

            // ---- Chapter 3 ----
            root.Children.Add(Add(Emojis.Target, "Chapter 3 — What one frame is worth (the factors)",
                "For every frame you play, the app first works out a \u201Cframe value\u201D. " +
                "It does this by taking your opponent\u2019s rating going into this week and multiplying it by one of three factors:\n\n" +
                $"  • You WON the frame                  →  opp rating  ×  Win Factor      (default 1.25)\n" +
                $"  • You won on the 8-BALL              →  opp rating  ×  8-Ball Factor   (default 1.35)\n" +
                $"  • You LOST the frame                 →  opp rating  ×  Loss Factor     (default 0.75)\n\n" +
                "Worked example (against an average 1000-rated opponent):\n\n" +
                $"  Win:        1000 × 1.25 = 1250  → a frame valued at 1250\n" +
                $"  8-ball win: 1000 × 1.35 = 1350  → a frame valued at 1350\n" +
                $"  Loss:       1000 × 0.75 =  750  → a frame valued at  750\n\n" +
                "Now play a stronger opponent (rated 1200):\n\n" +
                $"  Win:        1200 × 1.25 = 1500  (worth more than beating a 1000)\n" +
                $"  Loss:       1200 × 0.75 =  900  (still adds to your average!)\n\n" +
                "That\u2019s the key insight: “losing\u201D to a strong opponent can still be a frame value of 900, " +
                "which keeps your rating high. Losing to a 600-rated opponent only gives you 600 × 0.75 = 450, " +
                "which drags it down.\n\n" +
                $"{Emojis.Bullet} Turn the 8-ball bonus on or off with the \u201CUse 8-ball factor\u201D switch.\n" +
                $"{Emojis.Bullet} If you want results to swing harder, raise the Win Factor and lower the Loss Factor."));

            // ---- Chapter 4 ----
            root.Children.Add(Add(Emojis.Clock, "Chapter 4 — Why recent frames matter more (weighting & bias)",
                "Once every one of your frames has a value, the app doesn\u2019t just average them. " +
                "It does a WEIGHTED average, where newer frames count more than old ones. This is what makes the " +
                "rating feel like \u201Cform\u201D rather than just a season average.\n\n" +
                "Two settings control the weights:\n\n" +
                $"  • Rating Weighting  (default 220) — the weight given to your NEWEST frame.\n" +
                $"  • Ratings Bias      (default   4) — how much LESS each older frame is worth.\n\n" +
                "So if you have played 5 frames in your career, the weights look like:\n\n" +
                $"  Frame 1 (oldest):  220 − 4×4 = 204\n" +
                $"  Frame 2:           220 − 4×3 = 208\n" +
                $"  Frame 3:           220 − 4×2 = 212\n" +
                $"  Frame 4:           220 − 4×1 = 216\n" +
                $"  Frame 5 (newest):  220        = 220\n\n" +
                "Your rating = (sum of frame value × weight) ÷ (sum of weights).\n\n" +
                "What this means in practice:\n\n" +
                $"  • A great night this week shifts your rating more than a great night last March.\n" +
                $"  • Old bad results slowly fade as you play more.\n" +
                $"  • Players who play more frames have more stable ratings (one fluke matters less).\n\n" +
                $"{Emojis.Bullet} If you want NEW frames to dominate even more, increase the Bias.\n" +
                $"{Emojis.Bullet} If you want a smoother, season-long average, decrease the Bias.\n" +
                $"{Emojis.Bullet} (Floor: weight is never allowed to drop below 1, even if the formula goes negative.)"));

            // ---- Chapter 5 ----
            root.Children.Add(Add(Emojis.Wrench, "Chapter 5 — A full worked example",
                "Let\u2019s rate \u201CAlex\u201D after a single night of pool. Alex plays 4 frames " +
                "against opponents who are all rated 1000 going into this week.\n\n" +
                "Defaults: Start 1000, Win 1.25, Loss 0.75, 8-ball 1.35, Weighting 220, Bias 4.\n\n" +
                $"Frame 1: WIN          → value = 1000 × 1.25 = 1250\n" +
                $"Frame 2: LOSS         → value = 1000 × 0.75 =  750\n" +
                $"Frame 3: WIN          → value = 1000 × 1.25 = 1250\n" +
                $"Frame 4: WIN (8-ball) → value = 1000 × 1.35 = 1350\n\n" +
                "With 4 total frames the weights are:\n\n" +
                $"  Frame 1 weight: 220 − 4×3 = 208\n" +
                $"  Frame 2 weight: 220 − 4×2 = 212\n" +
                $"  Frame 3 weight: 220 − 4×1 = 216\n" +
                $"  Frame 4 weight: 220        = 220\n\n" +
                "Numerator   = 1250×208 + 750×212 + 1250×216 + 1350×220\n" +
                "            = 260\u202F000 + 159\u202F000 + 270\u202F000 + 297\u202F000\n" +
                "            = 986\u202F000\n\n" +
                "Denominator = 208 + 212 + 216 + 220 = 856\n\n" +
                "Rating      = 986\u202F000 ÷ 856 ≈ 1151\n\n" +
                "So after one productive night against average opposition, Alex\u2019s rating is about 1151. " +
                "Beating a few stronger players, or winning more on the 8-ball, would push it higher."));

            // ---- Chapter 6 ----
            root.Children.Add(Add(Emojis.Calendar, "Chapter 6 — How weeks work",
                "The app calculates ratings WEEK BY WEEK, in chronological order, using the season\u2019s " +
                "start date as week 1.\n\n" +
                $"{Emojis.Bullet} At the start of week 1, EVERY player has the starting rating (1000).\n" +
                $"{Emojis.Bullet} All of week 1\u2019s frames are processed using those week-1 ratings.\n" +
                $"{Emojis.Bullet} At the end of the week, each player\u2019s rating is recalculated. That new " +
                "number is the rating they CARRY INTO week 2.\n" +
                $"{Emojis.Bullet} Repeat for every week of the season.\n\n" +
                "Why does this matter?\n\n" +
                $"  • The opponent\u2019s rating used in your frame value is the rating they had GOING INTO " +
                "this week, not their final season rating. So if a player gets hot late in the season, " +
                "early opponents don\u2019t suddenly get retroactive credit for beating them.\n" +
                $"  • Your weekly rating history is stored, which is what powers the rating sparkline " +
                "(▁▂▃▅▆▇█) and the rating-progression chart on player profiles."));

            // ---- Chapter 7 ----
            root.Children.Add(Add(Emojis.Player, "Chapter 7 — The minimum-frames rule",
                "Open the league\u2019s player ratings table and you\u2019ll only see the players who\u2019ve " +
                "played enough frames. That\u2019s the Min Frames % setting (default 60).\n\n" +
                $"{Emojis.Bullet} 60% means a player needs to have played at least 60% of the maximum " +
                "number of frames anyone in their division has played, before they show up.\n" +
                $"{Emojis.Bullet} Example: if the most-played player has 30 frames, others need at least " +
                "30 × 60% = 18 frames to appear.\n" +
                $"{Emojis.Bullet} The threshold ONLY hides players from the table — their rating is still " +
                "calculated and used wherever they show up (their profile, opponent\u2019s frame values, etc.).\n\n" +
                "Why have this rule?\n\n" +
                $"  • With only a handful of frames, a single big win or loss can swing a rating wildly. " +
                "Filtering them out keeps the headline table fair.\n" +
                $"  • Lower the % to be more inclusive (e.g. 40%), raise it to be stricter (e.g. 75%)."));

            // ---- Chapter 8 ----
            root.Children.Add(Add(Emojis.Reload, "Chapter 8 — The Recalculate button",
                "Under Settings → Player Ratings there\u2019s a big yellow button: " +
                "\u201C\U0001F504 Recalculate All Ratings\u201D. What does it actually do?\n\n" +
                $"{Emojis.Bullet} Imported data (especially from the old VBA / Access database) often " +
                "includes pre-baked rating numbers stored on each frame. Normally the app will TRUST those " +
                "and use them as-is, so historical ratings match what the old system showed.\n" +
                $"{Emojis.Bullet} But that means changing your rating settings does nothing for old data — " +
                "the imported numbers override the formula.\n" +
                $"{Emojis.Bullet} Recalculate All Ratings WIPES those imported per-frame rating values, " +
                "forcing the algorithm to rebuild every player\u2019s rating from scratch using the current " +
                "settings.\n\n" +
                "When to use it:\n\n" +
                $"  • Right after importing legacy data and you want consistent results going forward.\n" +
                $"  • After tweaking Win/Loss/8-ball factors or the Weighting/Bias and you want the " +
                "changes applied to past frames too.\n\n" +
                $"{Emojis.Warning} Take a backup first (Settings → Data Tools → Back up now). It\u2019s a " +
                "one-way operation — the imported numbers can\u2019t be brought back without restoring."));

            // ---- Chapter 9 ----
            root.Children.Add(Add(Emojis.Settings, "Chapter 9 — Tuning the dials safely",
                "Most leagues never need to change these. But if you do, here\u2019s a cheat sheet.\n\n" +
                $"{Emojis.Bullet} Want WINS to count for more?  → raise Win Factor (e.g. 1.25 → 1.40).\n" +
                $"{Emojis.Bullet} Want LOSSES to hurt more?     → lower Loss Factor (e.g. 0.75 → 0.60).\n" +
                $"{Emojis.Bullet} Want 8-balls to feel special? → raise 8-Ball Factor or keep it at 1.35.\n" +
                $"{Emojis.Bullet} Want NEW form to dominate?    → raise Bias (e.g. 4 → 6 or 8).\n" +
                $"{Emojis.Bullet} Want a SMOOTHER season-long view? → lower Bias toward 0 (≈ simple average).\n" +
                $"{Emojis.Bullet} Want more players in the table? → lower Min Frames % (e.g. 60 → 40).\n\n" +
                "Recommended workflow when experimenting:\n\n" +
                "  1. Take a backup (Settings → Data Tools → Back up now).\n" +
                "  2. Tweak ONE setting at a time.\n" +
                "  3. Tap Recalculate All Ratings.\n" +
                "  4. Open League Tables and see what changed.\n" +
                "  5. If it\u2019s wrong, restore the backup and try a smaller change.\n\n" +
                $"{Emojis.Bullet} Use the Settings Scope selector at the top of the Player Ratings panel " +
                "to keep the global defaults but try different values for ONE season only."));

            // ---- Chapter 10 ----
            root.Children.Add(Add(Emojis.Info, "Chapter 10 — The formula in one line (for the curious)",
                "For each player, sort their frames in chronological order. For frame number i out of N total " +
                "frames, define:\n\n" +
                "  weight_i = max(1,  Weighting − Bias × (N − i))\n" +
                "  value_i  = OpponentRating(at start of that week) × Factor\n\n" +
                "where Factor is:\n\n" +
                "  Win Factor      if you won (and the win wasn\u2019t on the 8-ball, or 8-ball factor is off)\n" +
                "  8-Ball Factor   if you won on the 8-ball and the 8-ball switch is on\n" +
                "  Loss Factor     if you lost\n\n" +
                "Then:\n\n" +
                "  Rating = Σ(value_i × weight_i)  ÷  Σ(weight_i)\n\n" +
                "That\u2019s it. Everything else in this guide is just explaining each piece of that one line."));

            // ---- FAQ ----
            root.Children.Add(Add(Emojis.Note, "Frequently asked questions",
                "Q. Why didn\u2019t my rating go up after I won 3-0?\n" +
                "A. Your opponents may have been low-rated, so each frame value was small. Or the win was " +
                "only a few frames against a much larger career history — the weighted average barely moved.\n\n" +
                "Q. Why does my rating change every week even though I didn\u2019t play?\n" +
                "A. It shouldn\u2019t. If you didn\u2019t play this week your rating carries over unchanged. If " +
                "you\u2019re seeing changes, check that no walkover frames were attributed to you in the " +
                "fixture editor.\n\n" +
                "Q. Two players have the same record but different ratings — why?\n" +
                "A. They played different opponents. Beating stronger opponents earns a higher rating, " +
                "and the timing matters too (recent results count more).\n\n" +
                "Q. Can a rating go below the starting value?\n" +
                "A. Yes. If you lose most of your frames against weak opponents your weighted average " +
                "will sit below 1000.\n\n" +
                "Q. Are ratings comparable across seasons?\n" +
                "A. Roughly, yes — the formula is the same, but each season starts everyone fresh at 1000. " +
                "Use the Career Stats page for a longer-term view.\n\n" +
                "Q. Are ratings comparable across leagues?\n" +
                "A. No — only against players within your league, because everyone started at 1000 together."));

            return root;
        }

        // ═══════════════════════════════════════════════════════════
        //  MANUAL (in-app user guide)
        // ═══════════════════════════════════════════════════════════

        private View CreateManualPanel()
        {
            var root = new VerticalStackLayout { Spacing = 0 };

            root.Children.Add(SectionHeader(Emojis.Note, "User Manual",
                $"How to use {Product.Name} — tap any heading to expand it"));

            // Track every section we add so the expand/collapse-all buttons can drive them.
            var sections = new List<ManualSectionView>();

            View Add(string icon, string title, string body, bool startExpanded = false)
            {
                var s = ManualSection(icon, title, body, startExpanded);
                sections.Add(s);
                return s;
            }

            // ---- Expand / Collapse all controls ----
            var expandAllBtn = new Button
            {
                Text = $"{Emojis.Down}  Expand all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            expandAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            expandAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(true); };

            var collapseAllBtn = new Button
            {
                Text = $"{Emojis.Up}  Collapse all",
                FontSize = 12,
                Padding = new Thickness(10, 4),
                HeightRequest = 32
            };
            collapseAllBtn.SetDynamicResource(Button.StyleProperty, "SecondaryButtonStyle");
            collapseAllBtn.Clicked += (_, _) => { foreach (var s in sections) s.SetExpanded(false); };

            root.Children.Add(new HorizontalStackLayout
            {
                Spacing = 8,
                Margin = new Thickness(0, 0, 0, 8),
                Children = { expandAllBtn, collapseAllBtn }
            });

            // ---- Welcome (always expanded) ----
            root.Children.Add(InfoPanel("Welcome",
                $"{Product.Name} runs a pool league from one place. It tracks seasons, " +
                "divisions, teams, players, venues, fixtures, results, ratings, cup competitions and even " +
                "generates the league website.\n\n" +
                "Navigation: the sidebar on the left lists every page, grouped under League, Matches, Stats " +
                "and Online (on a phone, open it with the menu button). Most pages let you add, edit and " +
                "delete inline, and changes are saved automatically.\n\n" +
                "This is the in-app reference. Tap a heading below to expand its detail."));

            // ---- Getting Started (expanded by default) ----
            root.Children.Add(Add(Emojis.Rocket, "Getting Started — first-time setup",
                "Follow this order the first time you set up a league. Each step links to the page of the same name in the side menu.\n\n" +
                $"1. {Emojis.Season} Seasons → New Season. Enter a name (e.g. \u201C2025 Winter\u201D), the start " +
                "and end dates and the default match day/time. Mark it Active so it shows up everywhere.\n\n" +
                $"2. {Emojis.Building} Venues. Add every pub or club that hosts matches. For each one, " +
                "set the number of tables — the scheduler uses this to avoid double-booking a venue on the same night.\n\n" +
                $"3. {Emojis.Division} Divisions. Create the divisions for the season (Premier, Division 1, etc.). " +
                "You can drag teams between divisions later, or use the Division Draw animation to randomise.\n\n" +
                $"4. {Emojis.Team} Teams. Add each team, assign it to a division and choose its home venue and home night. " +
                "Set a captain (any player on that team) so reminder notifications go to the right person.\n\n" +
                $"5. {Emojis.Player} Players. Add the squad for each team. Players can be moved between teams during " +
                "the season — the Transfer History keeps an audit trail.\n\n" +
                $"6. {Emojis.Fixture} Fixtures → Generate. The scheduler creates a balanced round-robin (or " +
                "multi-round) fixture list using your Fixture Defaults and venue availability. Review it, " +
                "resolve any clashes the validator flags, and publish.\n\n" +
                $"7. {Emojis.Chart} Enter results week by week. League Tables, ratings and achievements update automatically.",
                startExpanded: true));

            // ---- Seasons ----
            root.Children.Add(Add(Emojis.Season, "Seasons",
                "Seasons are the top-level container for everything else. Each division, team, player, fixture and competition belongs to exactly one season.\n\n" +
                $"{Emojis.Bullet} New Season: name, start/end dates, default match day, default match time, " +
                "frames per match, rounds per opponent.\n" +
                $"{Emojis.Bullet} Active Season: only one season is \u201Ccurrent\u201D at a time. Most pages " +
                "filter to it automatically. Switch the active season from Seasons → Set Active.\n" +
                $"{Emojis.Bullet} Season Setup: a guided wizard for blackout dates (public holidays, school " +
                "holidays, etc.), preset holidays from the country pack, and frame counts.\n" +
                $"{Emojis.Bullet} Season Comparison: pick two seasons and compare standings, top scorers, " +
                "win percentages and rating distributions side-by-side.\n" +
                $"{Emojis.Bullet} Season Copy: clone divisions, teams and players from a previous season into a " +
                "new one so you don\u2019t have to re-enter everything every year.\n" +
                $"{Emojis.Bullet} Settings Scope: most settings panels show a Settings Scope selector at the top. " +
                "Choose \u201CGlobal Defaults\u201D to edit values used by every season, or pick a specific " +
                "season and toggle \u201CUse custom settings for this season\u201D to override."));

            // ---- Divisions ----
            root.Children.Add(Add(Emojis.Division, "Divisions",
                "Divisions group teams of similar standard within a season.\n\n" +
                $"{Emojis.Bullet} Add a division with a name (e.g. \u201CPremier\u201D), an optional colour and " +
                "sort order — the sort order controls the display order on the dashboard and league tables.\n" +
                $"{Emojis.Bullet} Group Stage Settings: configure how groups are drawn and seeded for divisions " +
                "that use a group stage.\n" +
                $"{Emojis.Bullet} Division Draw / Group Draw animations: a randomiser with on-screen animation " +
                "— useful for AGM nights when you want to do the draw in front of the captains.\n" +
                $"{Emojis.Bullet} Promotion / Relegation: rules for moving teams between divisions at season " +
                "end are configured under Settings → Data Tools."));

            // ---- Teams ----
            root.Children.Add(Add(Emojis.Team, "Teams",
                $"{Emojis.Bullet} Each team has a name, division, home venue, home night, captain and squad of players.\n" +
                $"{Emojis.Bullet} Captain: drives the captain-only website (entry forms, availability) and is the " +
                "recipient of weekly notifications.\n" +
                $"{Emojis.Bullet} Home venue: the scheduler will book home fixtures here on the team\u2019s home night.\n" +
                $"{Emojis.Bullet} Players list: drag players between teams; transfers are recorded in the player\u2019s " +
                "Transfer History.\n" +
                $"{Emojis.Bullet} Team Analytics: per-team page showing form, rating progression, top performers " +
                "and head-to-head records."));

            // ---- Players ----
            root.Children.Add(Add(Emojis.Player, "Players",
                "Players are the heart of the data — every frame they play feeds into ratings and stats.\n\n" +
                $"{Emojis.Bullet} Add Player: first name, last name, date of birth (optional), team, contact details.\n" +
                $"{Emojis.Bullet} Player Profile: tap a player to see profile, current rating, total frames, win " +
                "percentage and a timeline of every fixture they\u2019ve played.\n" +
                $"{Emojis.Bullet} Career Stats: combined stats across every season they\u2019ve appeared in, " +
                "including imported historical data.\n" +
                $"{Emojis.Bullet} Frame Stats: break-by-break detail — 8-ball wins, deciding-frame record, " +
                "longest winning streak, etc.\n" +
                $"{Emojis.Bullet} Player Results: chronological list of every result with opponent and venue.\n" +
                $"{Emojis.Bullet} Availability: mark dates a player is unavailable so the scheduler / captain " +
                "knows in advance.\n" +
                $"{Emojis.Bullet} Transfers: when you move a player to another team, the move and date are " +
                "recorded so historical fixtures still show the correct team."));

            // ---- Venues ----
            root.Children.Add(Add(Emojis.Building, "Venues",
                $"{Emojis.Bullet} Each venue has a name, address, optional contact details and one or more tables. " +
                "Tables are stored as JSON on the venue and have their own labels (e.g. \u201CFront\u201D, \u201CBack\u201D).\n" +
                $"{Emojis.Bullet} Number of tables limits how many home fixtures can be played at the venue on the " +
                "same night — useful when one club hosts several teams.\n" +
                $"{Emojis.Bullet} The Venues page also shows which teams call this venue home.\n" +
                $"{Emojis.Bullet} On the Competitions page, each match can be given a venue so entrants know " +
                "where to play."));

            // ---- Fixtures ----
            root.Children.Add(Add(Emojis.Fixture, "Fixtures, Results & Match Day",
                "Fixtures are league matches between two teams on a given date.\n\n" +
                $"{Emojis.Bullet} Generate Fixtures: from the Fixtures page, click Generate. The Season Scheduler " +
                "produces a balanced round-robin using your Fixture Defaults (match day, time, rounds per " +
                "opponent, frames per match) and respects blackout dates.\n" +
                $"{Emojis.Bullet} Fixture Validator: warns about clashes — same team booked twice, venue capacity " +
                "exceeded, fixture on a blackout date, etc. Use the clash resolver to swap dates automatically.\n" +
                $"{Emojis.Bullet} Enter Results: tap a fixture to open the result editor. Enter each frame, mark " +
                "the winner, optionally flag 8-ball wins. Team score and player ratings update instantly.\n" +
                $"{Emojis.Bullet} Match Day Dashboard: a single-screen overview of every fixture being played " +
                "tonight — ideal to leave running on a tablet at the venue. Auto-refreshes as results come in.\n" +
                $"{Emojis.Bullet} Calendar: month view of every fixture, blackout date and event. Tap a day to " +
                "see what\u2019s on. Use Calendar Options to change the first day of the week and add personal events.\n" +
                $"{Emojis.Bullet} Snapshots: the Schedule Snapshot service saves the schedule before any " +
                "regeneration so you can roll back if needed."));

            // ---- Competitions ----
            root.Children.Add(Add(Emojis.Competition, "Competitions — Cups, Knockouts, Singles & Doubles",
                "Competitions are tournaments that sit alongside the league — cups, plate competitions, " +
                "singles and doubles championships.\n\n" +
                $"{Emojis.Bullet} Competition Wizard: walks you through creating a competition. Choose format " +
                "(knockout, double-elimination, round-robin, group stage + knockout, singles, doubles), entry " +
                "type (teams or players) and seeding rules.\n" +
                $"{Emojis.Bullet} Participants: add or remove entries. The system can auto-seed by current rating.\n" +
                $"{Emojis.Bullet} Bracket / Groups: enter results and the next round is filled in for you. " +
                "The bracket renders a printable single-elimination chart.\n" +
                $"{Emojis.Bullet} Venues: assign each match to a venue and time so participants know where to play.\n" +
                $"{Emojis.Bullet} Editor: rename the competition, change format mid-stream (with a warning), " +
                "or add custom rounds.\n" +
                $"{Emojis.Bullet} Doubles: pairings are stored as DoublesPairings on a DoublesTeam, so the same " +
                "player can partner different people across seasons."));

            // ---- League Tables & ratings ----
            root.Children.Add(Add(Emojis.Chart, "League Tables & Player Ratings",
                "League standings and player ratings are computed live from results.\n\n" +
                $"{Emojis.Bullet} League Tables: choose a division to see the standings. Columns: Played, Won, " +
                "Drawn, Lost, Frames For, Frames Against, Frame Difference, Points.\n" +
                $"{Emojis.Bullet} Points: Frames Won + Match Win Bonus on a win, + Match Draw Bonus on a draw, " +
                "none on a loss. Configure the bonuses under Settings → Match Scoring.\n" +
                $"{Emojis.Bullet} Tiebreakers: when teams are level on points, the Tiebreaker Order from Match " +
                "Scoring decides the order — Frame Difference, Frames For, Head-to-Head, Matches Won. Drag the " +
                "order to reflect your league\u2019s rules.\n" +
                $"{Emojis.Bullet} Player Ratings: VBA-style cumulative weighted formula — " +
                "Rating = Σ(OpponentRating × Factor × Weight) / ΣWeight. Tune Starting Rating, Weighting, Bias, " +
                "Win/Loss/8-ball factors and Min Frames % under Settings → Player Ratings.\n" +
                $"{Emojis.Bullet} Recalculate All Ratings: imported VBA/SQL data may contain pre-baked ratings. " +
                "Use this button to wipe and recompute everything from frame data using current settings."));

            // ---- Achievements ----
            root.Children.Add(Add(Emojis.Trophy, "Achievements & Season Awards",
                $"{Emojis.Bullet} Achievements unlock automatically as players reach milestones — first 8-ball, " +
                "100 frames played, 10-game winning streak, etc.\n" +
                $"{Emojis.Bullet} Each achievement shows when it was earned and against whom.\n" +
                $"{Emojis.Bullet} Season Awards page collates end-of-season honours — Player of the Year, Most " +
                "Improved, Highest Win %, Most 8-balls, etc.\n" +
                $"{Emojis.Bullet} Award winners feed into the Website Builder\u2019s History page."));

            // ---- Analytics ----
            root.Children.Add(Add(Emojis.Chart, "Analytics",
                $"{Emojis.Bullet} Analytics Hub: entry point to all charts and reports.\n" +
                $"{Emojis.Bullet} Team Analytics: per-team form line, rating curve, top frame-winners, " +
                "home-vs-away record, head-to-head matrix.\n" +
                $"{Emojis.Bullet} What-If Simulator: pick remaining fixtures and assign hypothetical scores to see " +
                "how the league table would look. Useful for run-in planning.\n" +
                $"{Emojis.Bullet} Career Stats: long-term player view across every season they\u2019ve appeared in."));

            // ---- Adding data ----
            root.Children.Add(Add(Emojis.Import, "Adding Data in Bulk",
                "You don\u2019t have to type everything in one record at a time.\n\n" +
                $"{Emojis.Bullet} CSV files: the Teams, Venues and Divisions pages each have an " +
                "Import (.csv) box. Drop a spreadsheet export in and the rows are added to the current season.\n" +
                $"{Emojis.Bullet} New season: on the Seasons page, Import from Previous Seasons copies the " +
                "divisions, venues, teams and players you choose into the new season.\n" +
                $"{Emojis.Bullet} Score Card Recognition: snap a photo of a paper score card and the OCR service " +
                "(Plugin.Maui.OCR + Azure Vision fallback) extracts the frame results.\n" +
                $"{Emojis.Bullet} Take a backup (Settings → Data Tools) before a large change."));

            // ---- Website Builder ----
            root.Children.Add(Add(Emojis.Building, "Website Builder",
                "The Website Builder turns your league data into a public static website ready to host anywhere.\n\n" +
                $"{Emojis.Bullet} Hub: pick which pages to publish (Home, Fixtures, Results, Standings, Players, " +
                "Divisions, Competitions, Rules, History, Gallery, Contact, Entry Forms, Captains-only area).\n" +
                $"{Emojis.Bullet} Branding & Colours: set the league name, tagline, primary/accent colours and " +
                "upload a logo.\n" +
                $"{Emojis.Bullet} Layout: drag-and-drop the order of sections on each page, choose between " +
                "single-column / sidebar / hero layouts.\n" +
                $"{Emojis.Bullet} Fixtures Sheet: printable PDF-style fixture sheet for handing out at the AGM.\n" +
                $"{Emojis.Bullet} Generate: builds a static folder of HTML, CSS, JSON data and images.\n" +
                $"{Emojis.Bullet} Deploy: upload via FTP (FtpUploadService) or push to GitHub Pages " +
                "(GitHubPagesService) directly from the Deployment Settings page.\n" +
                $"{Emojis.Bullet} Result Cards: make an image of a result, table or fixture, save it and copy " +
                "its caption, ready to post wherever the league shares its news."));

            // ---- Notifications ----
            root.Children.Add(Add(Emojis.Bell, "Notifications",
                $"{Emojis.Bullet} On first use, open Settings → Notifications and tap Request Notification " +
                "Permissions. The app uses Plugin.LocalNotification under the hood.\n" +
                $"{Emojis.Bullet} Match Reminders: scheduled per fixture for the captain. Choose how many hours " +
                "before (1, 2, 4, 6, 12 or 24).\n" +
                $"{Emojis.Bullet} Result Notifications: instant alert when a result is posted to a fixture you\u2019re part of.\n" +
                $"{Emojis.Bullet} Weekly Fixture List: Monday morning summary of the week\u2019s fixtures.\n" +
                $"{Emojis.Bullet} Test Notification button sends a sample so you can confirm permissions are working.\n" +
                $"{Emojis.Bullet} Cancel All Notifications wipes scheduled reminders — useful after regenerating fixtures."));

            // ---- Search ----
            root.Children.Add(Add(Emojis.Target, "Search",
                "The Search page jumps straight to any record.\n\n" +
                $"{Emojis.Bullet} Searches across players, teams, venues, divisions, fixtures and competitions.\n" +
                $"{Emojis.Bullet} Type any part of a name; results group by type with the most relevant hit at the top.\n" +
                $"{Emojis.Bullet} Tap a result to navigate to its detail page."));

            // ---- Settings overview ----
            root.Children.Add(Add(Emojis.Settings, "Settings — every panel explained",
                "The first three set how the league runs, and each can be set for one season only.\n\n" +
                $"{Emojis.Bullet} Match Scoring — win bonus, draw bonus, and the drag-and-drop Tiebreaker Order " +
                "used by League Tables.\n" +
                $"{Emojis.Bullet} Fixture Defaults — default match day, default match time, frames per match, rounds " +
                "per opponent. Used by the Season Scheduler when generating new fixtures.\n" +
                $"{Emojis.Bullet} Player Ratings — Starting rating, weighting, bias, win/loss factor, 8-ball factor, " +
                "min frames %. Recalculate All Ratings re-runs the formula across every historical frame.\n" +
                $"{Emojis.Bullet} Appearance — light / dark / follow system. Theme is applied immediately to all " +
                "open pages.\n" +
                $"{Emojis.Bullet} Notifications — enable/disable each notification type, set reminder lead time, " +
                "send a test notification, cancel all pending.\n" +
                $"{Emojis.Bullet} Data Tools — backups, clearing out orphaned records, resetting seasons, and " +
                "tidying divisions across seasons.\n" +
                $"{Emojis.Bullet} About — version and credits."));

            // ---- Backups ----
            root.Children.Add(Add(Emojis.Database, "Backups & Data Safety",
                "Backups live under Settings → Data Tools → Backups. Each one is a single .zip holding the " +
                "whole league.\n\n" +
                $"{Emojis.Bullet} Automatic: a backup is made every few saves. The newest 20 are kept and older " +
                "ones are deleted as new ones arrive.\n" +
                $"{Emojis.Bullet} Back up now: make one yourself before any large change (a recalculation, a " +
                "season copy, a clear-out). These are kept until you delete them.\n" +
                $"{Emojis.Bullet} Restore: choose Restore… beside a backup, or Restore from a file… for one " +
                "kept elsewhere. The app closes, and puts the backup in place when you next open it. The league " +
                "it replaces is backed up first (listed as \u201CBefore a restore\u201D), so a restore can " +
                "itself be undone.\n" +
                $"{Emojis.Bullet} Keeping a copy safe: on Windows, Open backups folder shows the files, ready " +
                "to copy onto a memory stick or into cloud storage.\n" +
                $"{Emojis.Bullet} Delete All Seasons makes a backup before it deletes anything."));

            // ---- Tips ----
            root.Children.Add(Add(Emojis.Sparkles, "Tips & Tricks",
                $"{Emojis.Bullet} Most settings panels show a Settings Scope selector — keep global defaults but " +
                "override values for one season only.\n" +
                $"{Emojis.Bullet} Pull down on any list to refresh.\n" +
                $"{Emojis.Bullet} The dashboard cards are tap-targets — they jump to the matching detail page.\n" +
                $"{Emojis.Bullet} On Windows the app respects the system accent colour for selected list items.\n" +
                $"{Emojis.Bullet} If a page looks wrong after changing the theme, navigate away and back — " +
                "theme-aware colours rebuild on next render.\n" +
                $"{Emojis.Bullet} Keep the database tidy: delete test seasons before importing real history, " +
                "and run the Data Integrity Validator after every major import."));

            // ---- Troubleshooting ----
            root.Children.Add(Add(Emojis.Wrench, "Troubleshooting",
                $"{Emojis.Bullet} Something has gone wrong with the league\u2019s data: restore the most recent " +
                "backup from before it happened (Settings → Data Tools → Backups).\n" +
                $"{Emojis.Bullet} Notifications not firing: re-open Settings → Notifications and tap Request " +
                "Permissions. On Android 13+ check the system app-info screen too.\n" +
                $"{Emojis.Bullet} Ratings look wrong after import: tap Recalculate All Ratings under Settings → " +
                "Player Ratings. Imported VBA values are wiped and recomputed from frames.\n" +
                $"{Emojis.Bullet} Website Builder deploy fails: check FTP credentials / GitHub token under " +
                "Deployment Settings, and that the generated output folder isn\u2019t open elsewhere.\n" +
                $"{Emojis.Bullet} Fixture clashes after generation: open the Fixture Validator on the Fixtures page " +
                "and use the suggested swaps to resolve them."));

            return root;
        }

        /// <summary>
        /// Helper that builds one collapsible manual section: a tappable header that toggles
        /// a body card. Used so the manual stays tidy and users can drill in only where needed.
        /// </summary>
        private ManualSectionView ManualSection(string icon, string title, string body, bool startExpanded = false)
        {
            return new ManualSectionView(icon, title, body, startExpanded);
        }

        /// <summary>
        /// View used by the user manual: a tappable header (with chevron + icon + title) that
        /// expands or collapses an info card containing the section body text.
        /// </summary>
        private sealed class ManualSectionView : VerticalStackLayout
        {
            private readonly Label _chevron;
            private readonly Border _bodyContainer;
            private bool _expanded;

            public ManualSectionView(string icon, string title, string body, bool startExpanded)
            {
                Spacing = 6;
                Margin = new Thickness(0, 8, 0, 0);

                _chevron = new Label
                {
                    Text = startExpanded ? "\u25BC" : "\u25B6",
                    FontSize = 12,
                    TextColor = SubtleText,
                    VerticalTextAlignment = TextAlignment.Center,
                    WidthRequest = 16
                };

                var titleLabel = new Label
                {
                    Text = $"{icon}  {title}",
                    FontSize = 16,
                    FontAttributes = FontAttributes.Bold,
                    TextColor = TitleText,
                    VerticalTextAlignment = TextAlignment.Center
                };

                var headerStack = new HorizontalStackLayout
                {
                    Spacing = 8,
                    Children = { _chevron, titleLabel }
                };

                var headerBorder = new Border
                {
                    Padding = new Thickness(10, 8),
                    BackgroundColor = FieldBg,
                    Stroke = CardStroke,
                    StrokeThickness = 1,
                    StrokeShape = new RoundRectangle { CornerRadius = 8 },
                    Content = headerStack
                };

                var tap = new TapGestureRecognizer();
                tap.Tapped += (_, _) => SetExpanded(!_expanded);
                headerBorder.GestureRecognizers.Add(tap);

                var bodyLabel = new Label
                {
                    Text = body,
                    FontSize = 13,
                    LineHeight = 1.4,
                    TextColor = BodyText
                };

                _bodyContainer = Card(bodyLabel);
                _bodyContainer.IsVisible = startExpanded;
                _expanded = startExpanded;

                Children.Add(headerBorder);
                Children.Add(_bodyContainer);
            }

            public void SetExpanded(bool expanded)
            {
                if (_expanded == expanded) return;
                _expanded = expanded;
                _bodyContainer.IsVisible = expanded;
                _chevron.Text = expanded ? "\u25BC" : "\u25B6";
            }
        }

    }
}
