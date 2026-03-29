using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class RulesSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;

    // Map each WebView to the settings property it represents
    private readonly Dictionary<WebView, string> _editorMap = new();

    public RulesSettingsPage()
    {
        InitializeComponent();
        InitEditors();
    }

    // ── Rich text editor setup ──────────────────────────────────────────

    private void InitEditors()
    {
        var settings = League.WebsiteSettings;
        ShowRulesSwitch.IsToggled = settings.ShowRules;

        SetupEditor(ConstitutionWebView, settings.ConstitutionContent, "constitution");
        SetupEditor(MatchRulesWebView, settings.MatchRulesContent, "matchrules");
        SetupEditor(EpaRulesWebView, settings.EpaRulesContent, "eparules");
        SetupEditor(GeneralRulesWebView, settings.RulesContent, "general");
    }

    private void SetupEditor(WebView webView, string htmlContent, string key)
    {
        _editorMap[webView] = key;
        webView.Source = new HtmlWebViewSource { Html = BuildEditorHtml(htmlContent) };
    }

    /// <summary>
    /// Build a self-contained HTML page with a toolbar + contenteditable area.
    /// Pasted content from Word, browsers, etc. is preserved including images.
    /// Images pasted from the clipboard are converted to inline base64.
    /// </summary>
    private static string BuildEditorHtml(string content)
    {
        // Escape any backticks in content for the JS template literal
        var safeContent = (content ?? "")
            .Replace("\\", "\\\\")
            .Replace("`", "\\`");

        return $$"""
        <!DOCTYPE html>
        <html>
        <head>
        <meta charset="utf-8"/>
        <meta name="viewport" content="width=device-width, initial-scale=1"/>
        <style>
            * { margin: 0; padding: 0; box-sizing: border-box; }
            body { font-family: 'Segoe UI', sans-serif; font-size: 14px; background: #fff; }
            .toolbar {
                position: sticky; top: 0; z-index: 10;
                display: flex; flex-wrap: wrap; gap: 2px;
                padding: 6px; background: #f1f5f9; border-bottom: 1px solid #e2e8f0;
            }
            .toolbar button {
                border: 1px solid #cbd5e1; background: #fff; border-radius: 4px;
                padding: 4px 8px; cursor: pointer; font-size: 13px; min-width: 30px;
                color: #334155; font-family: inherit;
            }
            .toolbar button:hover { background: #e2e8f0; }
            .toolbar button.active { background: #3b82f6; color: #fff; border-color: #3b82f6; }
            .toolbar .sep { width: 1px; background: #cbd5e1; margin: 2px 4px; }
            #editor {
                min-height: 200px; padding: 16px; outline: none;
                line-height: 1.7; color: #1e293b;
            }
            #editor:empty::before {
                content: 'Type or paste your content here...';
                color: #94a3b8; font-style: italic;
            }
            #editor img { max-width: 100%; height: auto; border-radius: 4px; margin: 8px 0; }
            #editor h1, #editor h2, #editor h3, #editor h4 { margin: 16px 0 8px; color: #0f172a; }
            #editor h3 { font-size: 1.15em; }
            #editor ol, #editor ul { padding-left: 24px; margin: 8px 0; }
            #editor li { margin-bottom: 4px; }
            #editor table { border-collapse: collapse; width: 100%; margin: 8px 0; }
            #editor td, #editor th { border: 1px solid #e2e8f0; padding: 6px 10px; }
        </style>
        </head>
        <body>
        <div class="toolbar">
            <button onclick="fmt('bold')" title="Bold"><b>B</b></button>
            <button onclick="fmt('italic')" title="Italic"><i>I</i></button>
            <button onclick="fmt('underline')" title="Underline"><u>U</u></button>
            <div class="sep"></div>
            <button onclick="fmt('formatBlock','<h3>')" title="Heading">H3</button>
            <button onclick="fmt('formatBlock','<p>')" title="Paragraph">P</button>
            <div class="sep"></div>
            <button onclick="fmt('insertOrderedList')" title="Numbered list">1.</button>
            <button onclick="fmt('insertUnorderedList')" title="Bullet list">•</button>
            <div class="sep"></div>
            <button onclick="insertImg()" title="Insert image">&#128247;</button>
            <button onclick="fmt('removeFormat')" title="Clear formatting">&#10006;</button>
        </div>
        <div id="editor" contenteditable="true"></div>
        <script>
            var editor = document.getElementById('editor');

            // Load initial content
            editor.innerHTML = `{{safeContent}}`;

            function fmt(cmd, val) {
                document.execCommand(cmd, false, val || null);
                editor.focus();
            }

            // Handle pasted images — convert to inline base64
            editor.addEventListener('paste', function(e) {
                var items = (e.clipboardData || e.originalEvent.clipboardData).items;
                for (var i = 0; i < items.length; i++) {
                    if (items[i].type.indexOf('image') !== -1) {
                        e.preventDefault();
                        var blob = items[i].getAsFile();
                        var reader = new FileReader();
                        reader.onload = function(evt) {
                            document.execCommand('insertImage', false, evt.target.result);
                        };
                        reader.readAsDataURL(blob);
                        return;
                    }
                }
                // For HTML paste (Word, browser), the browser preserves formatting by default
            });

            // Insert image via file picker (base64 inline)
            function insertImg() {
                var input = document.createElement('input');
                input.type = 'file';
                input.accept = 'image/*';
                input.onchange = function() {
                    var file = input.files[0];
                    if (!file) return;
                    var reader = new FileReader();
                    reader.onload = function(evt) {
                        document.execCommand('insertImage', false, evt.target.result);
                    };
                    reader.readAsDataURL(file);
                };
                input.click();
            }

            // Called from C# to get content
            function getContent() { return editor.innerHTML; }

            // Called from C# to set content
            function setContent(html) { editor.innerHTML = html; }
        </script>
        </body>
        </html>
        """;
    }

    /// <summary>Read the HTML from a rich text editor WebView.</summary>
    private async Task<string> GetEditorContentAsync(WebView webView)
    {
        try
        {
            var result = await webView.EvaluateJavaScriptAsync("getContent()");
            // EvaluateJavaScriptAsync returns a JSON-encoded string (with enclosing quotes)
            if (result != null && result.StartsWith("\"") && result.EndsWith("\""))
                result = System.Text.Json.JsonSerializer.Deserialize<string>(result);
            return result?.Trim() ?? "";
        }
        catch
        {
            return "";
        }
    }

    /// <summary>Set the HTML in a rich text editor WebView.</summary>
    private async Task SetEditorContentAsync(WebView webView, string html)
    {
        try
        {
            var escaped = System.Text.Json.JsonSerializer.Serialize(html ?? "");
            await webView.EvaluateJavaScriptAsync($"setContent({escaped})");
        }
        catch { }
    }

    // ── Template insertion ───────────────────────────────────────────────

    private async void OnInsertConstitutionClicked(object sender, EventArgs e)
    {
        var current = await GetEditorContentAsync(ConstitutionWebView);
        if (!string.IsNullOrWhiteSpace(current) && current != "<br>")
        {
            if (!await ConfirmReplace("Constitution")) return;
        }
        await SetEditorContentAsync(ConstitutionWebView, GetConstitutionTemplate());
        ShowRulesSwitch.IsToggled = true;
    }

    private async void OnInsertMatchRulesClicked(object sender, EventArgs e)
    {
        var current = await GetEditorContentAsync(MatchRulesWebView);
        if (!string.IsNullOrWhiteSpace(current) && current != "<br>")
        {
            if (!await ConfirmReplace("League Match Rules")) return;
        }
        await SetEditorContentAsync(MatchRulesWebView, GetMatchRulesTemplate());
        ShowRulesSwitch.IsToggled = true;
    }

    private async void OnInsertEpaRulesClicked(object sender, EventArgs e)
    {
        var current = await GetEditorContentAsync(EpaRulesWebView);
        if (!string.IsNullOrWhiteSpace(current) && current != "<br>")
        {
            if (!await ConfirmReplace("EPA Rules")) return;
        }
        await SetEditorContentAsync(EpaRulesWebView, GetEpaRulesTemplate());
        ShowRulesSwitch.IsToggled = true;
    }

    private Task<bool> ConfirmReplace(string section) =>
        DisplayAlert("Replace Content",
            $"This will replace your current {section} content with the template. Continue?",
            "Replace", "Cancel");

    // ── Save ─────────────────────────────────────────────────────────────

    private async void OnSaveClicked(object sender, EventArgs e)
    {
        try
        {
            var settings = League.WebsiteSettings;

            settings.ShowRules = ShowRulesSwitch.IsToggled;
            settings.ConstitutionContent = await GetEditorContentAsync(ConstitutionWebView);
            settings.MatchRulesContent = await GetEditorContentAsync(MatchRulesWebView);
            settings.EpaRulesContent = await GetEditorContentAsync(EpaRulesWebView);
            settings.RulesContent = await GetEditorContentAsync(GeneralRulesWebView);

            DataStore.Save();

            await DisplayAlert("Saved", "Rules settings saved.", "OK");
            await Navigation.PopAsync();
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    // ── Templates ────────────────────────────────────────────────────────

    private static string GetConstitutionTemplate() =>
        """
        <h3>1. Name and Purpose</h3>
        <ol>
            <li>The league shall be known as <strong>[Your League Name]</strong>.</li>
            <li>The purpose of the league is to promote the game of 8-ball pool in a competitive and friendly environment.</li>
        </ol>

        <h3>2. Membership</h3>
        <ol>
            <li>Membership is open to all teams within the local area who agree to abide by this constitution and league rules.</li>
            <li>Each team must have a minimum of 5 registered players.</li>
            <li>Annual membership fees must be paid before the start of each season.</li>
        </ol>

        <h3>3. Committee</h3>
        <ol>
            <li>The league shall be managed by a committee consisting of a Chairperson, Secretary, Treasurer, and up to 3 ordinary members.</li>
            <li>Committee members are elected at the Annual General Meeting (AGM) and serve for one year.</li>
            <li>The committee has the authority to make decisions on matters not covered by this constitution.</li>
        </ol>

        <h3>4. Annual General Meeting</h3>
        <ol>
            <li>An AGM shall be held once per year, at least 14 days' notice given to all member teams.</li>
            <li>Each team is entitled to one vote at the AGM.</li>
            <li>Amendments to this constitution require a two-thirds majority of teams present.</li>
        </ol>

        <h3>5. Fees and Finances</h3>
        <ol>
            <li>Team registration fees and match fees shall be set by the committee before the start of each season.</li>
            <li>The Treasurer shall maintain proper accounts and present a financial report at the AGM.</li>
            <li>All funds shall be held in a league bank account requiring two signatories.</li>
        </ol>

        <h3>6. Disciplinary Procedures</h3>
        <ol>
            <li>Any complaints must be submitted in writing to the Secretary within 7 days of the incident.</li>
            <li>The committee will investigate and may impose sanctions including warnings, fines, suspensions, or expulsion.</li>
            <li>Any person subject to disciplinary action has the right to appeal in writing within 14 days.</li>
        </ol>

        <h3>7. Dissolution</h3>
        <ol>
            <li>The league may only be dissolved by a resolution passed at a Special General Meeting.</li>
            <li>Upon dissolution, any remaining funds shall be donated to a local charity chosen by the members.</li>
        </ol>
        """;

    private static string GetMatchRulesTemplate() =>
        """
        <h3>Match Format</h3>
        <ol>
            <li>Each match consists of <strong>15 singles frames</strong> (best of 15).</li>
            <li>Each player plays a maximum of 3 frames per match.</li>
            <li>Teams must field a minimum of 5 players per match.</li>
            <li>Matches are played on the home team's table on the designated match night.</li>
        </ol>

        <h3>Scoring and Points</h3>
        <ol>
            <li>Teams receive points for each frame won, plus a bonus for winning the match.</li>
            <li>The winning team receives 2 bonus points in addition to their frames won.</li>
            <li>In the event of a draw, each team receives 1 bonus point.</li>
        </ol>

        <h3>Player Registration</h3>
        <ol>
            <li>All players must be registered with the league before playing any frames.</li>
            <li>A player may only be registered with one team per season.</li>
            <li>The registration deadline is [insert date]. No new registrations after this date without committee approval.</li>
        </ol>

        <h3>Transfers</h3>
        <ol>
            <li>Transfers are only permitted during the designated transfer window.</li>
            <li>A player must not have played for another team in the current season.</li>
            <li>All transfers must be approved by the league committee.</li>
        </ol>

        <h3>Cancellations and Postponements</h3>
        <ol>
            <li>Cancellations must be notified at least <strong>48 hours</strong> in advance.</li>
            <li>Failure to give adequate notice will result in a penalty at the committee's discretion.</li>
            <li>Postponed matches must be rearranged and played within 14 days.</li>
        </ol>

        <h3>Late Cards and Penalties</h3>
        <ol>
            <li>Score cards must be submitted within 48 hours of the match.</li>
            <li>Late submissions may incur a points deduction.</li>
        </ol>

        <h3>Conduct</h3>
        <ol>
            <li>Unsportsmanlike behaviour will not be tolerated and may result in suspension.</li>
            <li>Any disputes during a match should be raised with the captains first.</li>
            <li>Unresolved disputes should be submitted in writing to the committee within 7 days.</li>
            <li>The home team captain is responsible for ensuring a fair and orderly match.</li>
        </ol>

        <h3>Doubles (if applicable)</h3>
        <ol>
            <li>Doubles frames follow the same rules as singles.</li>
            <li>Partners alternate shots — the same player cannot take consecutive shots.</li>
            <li>Doubles pairings must be declared before the start of the doubles frames.</li>
        </ol>
        """;

    private static string GetEpaRulesTemplate() =>
        """
        <h3>The Game</h3>
        <ol>
            <li>The game is played on a standard English 8-ball pool table with one white cue ball, seven yellow object balls, seven red object balls, and one black 8-ball.</li>
            <li>The game commences with a break shot. The player making the break must strike the racked balls with sufficient force to either pot a ball or cause at least two object balls to hit any cushion.</li>
            <li>Groups are determined by the first ball legally potted after the break.</li>
        </ol>

        <h3>Legal Shots</h3>
        <ol>
            <li>On all shots, the player must hit one of their own group of balls first (or the 8-ball when on the black).</li>
            <li>A shot is legal if the cue ball's first contact is with a ball "on" and then any ball is potted or any ball (including the cue ball) contacts a cushion.</li>
            <li>Combination shots are allowed provided the cue ball strikes a ball from the player's group first.</li>
        </ol>

        <h3>Fouls</h3>
        <ol>
            <li>Potting the cue ball (in-off).</li>
            <li>Failing to hit any ball with the cue ball.</li>
            <li>Hitting an opponent's ball first with the cue ball.</li>
            <li>Causing the cue ball or any object ball to leave the table.</li>
            <li>Playing a shot before all balls have come to rest.</li>
            <li>Touching any ball with anything other than the cue tip during a legal shot.</li>
            <li>Playing out of turn.</li>
            <li>Playing a push shot or double hit.</li>
        </ol>

        <h3>Foul Penalty</h3>
        <ol>
            <li>Following a foul, the incoming player has <strong>two visits</strong> (two consecutive shots).</li>
            <li>On the first visit after a foul, the player may play any ball on the table, including the opponent's balls and the 8-ball (this is a "free table").</li>
            <li>If a foul is committed while potting a ball, the potted ball remains potted but it is the opponent's turn.</li>
        </ol>

        <h3>Loss of Frame</h3>
        <ol>
            <li>Potting the 8-ball before completing your group (except on a free table after a foul).</li>
            <li>Potting the 8-ball and the cue ball in the same shot.</li>
            <li>Committing a foul while potting the 8-ball.</li>
            <li>The 8-ball leaving the table at any time.</li>
        </ol>

        <h3>Stalemate</h3>
        <ol>
            <li>If both players agree that the game has reached a stalemate (no progress being made), the frame shall be restarted with the same player breaking.</li>
        </ol>

        <p><em>These rules are based on the EPA (English Pool Association) ruleset. For the full official rules, visit <strong>www.epa.org.uk</strong>.</em></p>
        """;
}
