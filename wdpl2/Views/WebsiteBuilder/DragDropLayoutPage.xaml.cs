using System.Globalization;
using System.Text.Json;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class DragDropLayoutPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private Dictionary<string, string>? _generatedFiles;
    private List<LayoutBlock> _blocks = new();
    private bool _editMode = true;
    private bool _snapEnabled = true;
    private bool _syncingPicker;
    private string _currentPage = "index.html";
    private string? _currentQueryString;

    private static readonly (string FileName, string Label)[] Pages =
    [
        ("index.html", "Home"),
        ("standings.html", "Standings"),
        ("fixtures.html", "Fixtures"),
        ("results.html", "Results"),
        ("players.html", "Players"),
        ("divisions.html", "Divisions"),
    ];

    public DragDropLayoutPage()
    {
        InitializeComponent();
        _blocks = League.WebsiteSettings.GetEffectiveLayoutBlocks();
        SetupPagePicker();
        GenerateAndLoad();
    }

    private void SetupPagePicker()
    {
        var settings = League.WebsiteSettings;
        var items = new List<string> { "Home" };
        if (settings.ShowStandings) items.Add("Standings");
        if (settings.ShowFixtures) items.Add("Fixtures");
        if (settings.ShowResults) items.Add("Results");
        if (settings.ShowPlayerStats) items.Add("Players");
        if (settings.ShowDivisions) items.Add("Divisions");
        PagePicker.ItemsSource = items;
        PagePicker.SelectedIndex = 0;
    }

    private void GenerateAndLoad()
    {
        try
        {
            SaveBlocksToSettings();
            var generator = new WebsiteGenerator(League, League.WebsiteSettings);
            _generatedFiles = generator.GenerateWebsite();
            StatusLabel.Text = $"{_generatedFiles.Count} files generated";
            LoadCurrentPage();
        }
        catch (Exception ex)
        {
            StatusLabel.Text = $"Error: {ex.Message}";
        }
    }

    private void LoadCurrentPage()
    {
        if (_generatedFiles == null) return;
        if (!_generatedFiles.TryGetValue(_currentPage, out var html)) return;

        if (_generatedFiles.TryGetValue("style.css", out var css))
            html = html.Replace("<link rel=\"stylesheet\" href=\"style.css\">", $"<style>{css}</style>");

        // For template pages (player.html, team.html), inline the JSON data
        // so fetch() isn't needed, and inject the query string so URLSearchParams works
        html = InlineJsonData(html);

        if (!string.IsNullOrEmpty(_currentQueryString))
        {
            // Inject a script that sets the query string before the page JS runs
            var fakeQs = _currentQueryString.Replace("\\", "\\\\").Replace("'", "\\'");
            var qsScript = $"<script>if(!window.location.search){{" +
                           $"Object.defineProperty(window,'_editorQS',{{value:'{fakeQs}'}});" +
                           $"var _origUSP=URLSearchParams;" +
                           $"URLSearchParams=function(s){{return new _origUSP(window._editorQS||s);}};" +
                           $"}}</script>";
            html = html.Replace("<head>", "<head>" + qsScript);
        }

        // Inject freeform editor JS on all pages
        if (_currentPage == "index.html")
        {
            html = html.Replace("</body>", GetEditorScript() + "\n</body>");
        }
        else
        {
            // Wrap other pages' body content in a page-canvas so the editor can work with them
            html = html.Replace("<div class=\"content-area\">",
                "<div class=\"content-area\" data-block-id=\"content\" data-block-name=\"Content\" data-structural=\"false\">");
            
            // Wrap everything between <body> and </body> in a page-canvas
            html = html.Replace("<body>",
                "<body>\n    <div class=\"page-canvas\" style=\"position:relative;\">");
            html = html.Replace("</body>",
                "    </div>\n" + GetEditorScript() + "\n</body>");
        }

        EditorWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private string InlineJsonData(string html)
    {
        if (_generatedFiles == null) return html;

        // Replace fetch('players-data.json').then(parse) with inline data
        if (_generatedFiles.TryGetValue("players-data.json", out var playersJson))
        {
            var escaped = playersJson.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "").Replace("\n", "");
            // Match the exact pattern from WebsiteTemplatePageGenerator
            const string fetchPlayers = "fetch('players-data.json')";
            const string thenParse = ".then(function(r) { return r.json(); })";
            html = ReplaceFetchPattern(html, fetchPlayers, thenParse, escaped);
        }

        // Replace fetch('teams-data.json').then(parse) with inline data
        if (_generatedFiles.TryGetValue("teams-data.json", out var teamsJson))
        {
            var escaped = teamsJson.Replace("\\", "\\\\").Replace("'", "\\'")
                .Replace("\r", "").Replace("\n", "");
            const string fetchTeams = "fetch('teams-data.json')";
            const string thenParse = ".then(function(r) { return r.json(); })";
            html = ReplaceFetchPattern(html, fetchTeams, thenParse, escaped);
        }

        return html;
    }

    /// <summary>
    /// Replaces a fetch('...').then(parse) pattern with Promise.resolve(inline data),
    /// tolerant of any whitespace/newlines between the two parts.
    /// </summary>
    private static string ReplaceFetchPattern(string html, string fetchPart, string thenPart, string escapedJson)
    {
        var idx = html.IndexOf(fetchPart, StringComparison.Ordinal);
        if (idx < 0) return html;

        var afterFetch = idx + fetchPart.Length;
        // Skip any whitespace/newlines between fetch(...) and .then(...)
        var thenIdx = html.IndexOf(thenPart, afterFetch, StringComparison.Ordinal);
        if (thenIdx < 0) return html;

        var endIdx = thenIdx + thenPart.Length;
        return string.Concat(
            html.AsSpan(0, idx),
            $"Promise.resolve(JSON.parse('{escapedJson}'))",
            html.AsSpan(endIdx));
    }

    private async void OnPageChanged(object? sender, EventArgs e)
    {
        if (_syncingPicker) return;
        if (PagePicker.SelectedIndex < 0) return;
        
        // Collect any unsaved position changes before switching away
        await CollectPositionsFromEditor();
        SaveBlocksToSettings();
        
        var label = PagePicker.SelectedItem?.ToString() ?? "Home";
        var page = Pages.FirstOrDefault(p => p.Label == label);
        _currentPage = page.FileName ?? "index.html";
        _currentQueryString = null;
        
        // Regenerate so the page we're switching to (and back) uses current positions
        var generator = new WebsiteGenerator(League, League.WebsiteSettings);
        _generatedFiles = generator.GenerateWebsite();
        
        LoadCurrentPage();
    }

    private async void OnEditToggleClicked(object? sender, EventArgs e)
    {
        _editMode = !_editMode;
        EditToggleBtn.Text = _editMode ? "\u270E Edit" : "\u270E Preview";
        EditToggleBtn.BackgroundColor = _editMode ? Color.FromArgb("#F59E0B") : Color.FromArgb("#6B7280");
        if (_currentPage == "index.html")
            await EditorWebView.EvaluateJavaScriptAsync($"window.toggleEditor && window.toggleEditor({(_editMode ? "true" : "false")})");
    }

    private async void OnSnapToggleClicked(object? sender, EventArgs e)
    {
        _snapEnabled = !_snapEnabled;
        SnapBtn.Text = _snapEnabled ? "Snap: ON" : "Snap: OFF";
        SnapBtn.BackgroundColor = _snapEnabled ? Color.FromArgb("#6366F1") : Color.FromArgb("#475569");
        await EditorWebView.EvaluateJavaScriptAsync($"window.setSnap && window.setSnap({(_snapEnabled ? "true" : "false")})");
    }

    private async void OnGridClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet("Grid Options", "Cancel", null,
            "Hide Grid",
            "Small (25px)",
            "Medium (50px)",
            "Large (100px)",
            "Columns (2-col guides)",
            "Columns (3-col guides)");
        if (string.IsNullOrEmpty(choice) || choice == "Cancel") return;

        string jsCall = choice switch
        {
            "Hide Grid"           => "window.setGrid && window.setGrid(0, '')",
            "Small (25px)"        => "window.setGrid && window.setGrid(25, 'dots')",
            "Medium (50px)"       => "window.setGrid && window.setGrid(50, 'lines')",
            "Large (100px)"       => "window.setGrid && window.setGrid(100, 'lines')",
            "Columns (2-col guides)" => "window.setGrid && window.setGrid(0, 'cols2')",
            "Columns (3-col guides)" => "window.setGrid && window.setGrid(0, 'cols3')",
            _ => ""
        };
        if (!string.IsNullOrEmpty(jsCall))
        {
            await EditorWebView.EvaluateJavaScriptAsync(jsCall);
            GridBtn.Text = choice == "Hide Grid" ? "Grid" : "Grid \u2713";
            GridBtn.BackgroundColor = choice == "Hide Grid" ? Color.FromArgb("#475569") : Color.FromArgb("#0EA5E9");
        }
    }

    private async void OnAlignClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet("Align & Arrange", "Cancel", null,
            "--- Position ---",
            "\u2B05 Align Left",
            "\u27A1 Align Right",
            "\u2194 Center Horizontally",
            "\u2B06 Align Top",
            "\u2B07 Align Bottom",
            "\u2195 Center Vertically",
            "--- Size ---",
            "\U0001F4CF Match Width (to widest)",
            "\U0001F4D0 Match Height (to tallest)",
            "\U0001F4D0 Match Size (both)",
            "--- Distribute ---",
            "\u2195 Distribute Vertically",
            "\u2194 Distribute Horizontally",
            "\u2195 Equal Vertical Gaps",
            "--- Arrange ---",
            "\u2B07 Stack Vertically",
            "\u27A1 Stack Horizontally",
            "\u2B05 Align All Left Edges",
            "\u2194 Center All Horizontally",
            "--- Structural ---",
            "\u2194 Stretch Full Width",
            "\u2B06 Auto-Position Selected",
            "\u2728 Auto-Position All Header/Nav/Footer");
        if (string.IsNullOrEmpty(choice) || choice == "Cancel" || choice.StartsWith("---")) return;

        string jsCall = choice switch
        {
            _ when choice.Contains("Align Left")           => "window.alignBlock && window.alignBlock('left')",
            _ when choice.Contains("Align Right")          => "window.alignBlock && window.alignBlock('right')",
            _ when choice.Contains("Center Horizontally")   => "window.alignBlock && window.alignBlock('centerH')",
            _ when choice.Contains("Align Top")            => "window.alignBlock && window.alignBlock('top')",
            _ when choice.Contains("Align Bottom")         => "window.alignBlock && window.alignBlock('bottom')",
            _ when choice.Contains("Center Vertically")    => "window.alignBlock && window.alignBlock('centerV')",
            _ when choice.Contains("Match Width")          => "window.alignBlock && window.alignBlock('matchWidth')",
            _ when choice.Contains("Match Height")         => "window.alignBlock && window.alignBlock('matchHeight')",
            _ when choice.Contains("Match Size")           => "window.alignBlock && window.alignBlock('matchSize')",
            _ when choice.Contains("Distribute Vertically") => "window.alignBlock && window.alignBlock('distributeV')",
            _ when choice.Contains("Distribute Horizontally") => "window.alignBlock && window.alignBlock('distributeH')",
            _ when choice.Contains("Equal Vertical Gaps")  => "window.alignBlock && window.alignBlock('equalGapsV')",
            _ when choice.Contains("Stack Vertically")     => "window.alignBlock && window.alignBlock('stackV')",
            _ when choice.Contains("Stack Horizontally")   => "window.alignBlock && window.alignBlock('stackH')",
            _ when choice.Contains("All Left Edges")       => "window.alignBlock && window.alignBlock('alignAllLeft')",
            _ when choice.Contains("Center All")           => "window.alignBlock && window.alignBlock('centerAllH')",
            _ when choice.Contains("Stretch Full Width")   => "window.alignBlock && window.alignBlock('stretchFull')",
            _ when choice.Contains("Auto-Position Selected") => "window.alignBlock && window.alignBlock('autoStructural')",
            _ when choice.Contains("Auto-Position All")    => "window.alignBlock && window.alignBlock('autoAllStructural')",
            _ => ""
        };
        if (!string.IsNullOrEmpty(jsCall))
            await EditorWebView.EvaluateJavaScriptAsync(jsCall);
    }

    private async void OnBringForwardClicked(object? sender, EventArgs e)
    {
        await EditorWebView.EvaluateJavaScriptAsync("window.bringForward && window.bringForward()");
    }

    private async void OnSendBackClicked(object? sender, EventArgs e)
    {
        await EditorWebView.EvaluateJavaScriptAsync("window.sendBack && window.sendBack()");
    }

    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        if (e.Url.StartsWith("app://"))
        {
            e.Cancel = true;
            HandleEditorCommand(e.Url);
            return;
        }

        // Intercept .html links and load from generated files
        var url = e.Url;
        string? targetFile = null;
        string? queryString = null;

        // Extract filename and query string from URLs like
        // "player.html?id=xxx" or "http://...../standings.html"
        var lastSlash = url.LastIndexOf('/');
        var pathPart = lastSlash >= 0 ? url[(lastSlash + 1)..] : url;
        var qIdx = pathPart.IndexOf('?');
        if (qIdx >= 0)
        {
            queryString = pathPart[qIdx..]; // includes the '?'
            pathPart = pathPart[..qIdx];
        }

        if (pathPart.EndsWith(".html", StringComparison.OrdinalIgnoreCase))
            targetFile = pathPart;

        if (targetFile != null && _generatedFiles != null && _generatedFiles.ContainsKey(targetFile))
        {
            e.Cancel = true;
            
            // Collect positions before navigating away, then regenerate and load
            await CollectPositionsFromEditor();
            SaveBlocksToSettings();
            var generator = new WebsiteGenerator(League, League.WebsiteSettings);
            _generatedFiles = generator.GenerateWebsite();
            
            _currentPage = targetFile;
            _currentQueryString = queryString;
            SyncPagePicker(targetFile);
            LoadCurrentPage();
        }
    }

    private void SyncPagePicker(string fileName)
    {
        var match = Pages.FirstOrDefault(p => p.FileName == fileName);
        if (match.Label != null && PagePicker.ItemsSource is IList<string> items)
        {
            var idx = items.IndexOf(match.Label);
            if (idx >= 0)
            {
                _syncingPicker = true;
                PagePicker.SelectedIndex = idx;
                _syncingPicker = false;
            }
        }
    }

    private void HandleEditorCommand(string url)
    {
        if (url.StartsWith("app://toggle-visible/"))
        {
            var id = url.Replace("app://toggle-visible/", "");
            var block = _blocks.Find(b => b.BlockType == id);
            if (block != null)
            {
                block.IsEnabled = false;
                StatusLabel.Text = $"{block.DisplayName} hidden";
                GenerateAndLoad();
            }
        }
        else if (url.StartsWith("app://pos/"))
        {
            // Block position update: app://pos/{id}/{left}/{top}/{width}/{height}/{zIndex}
            var parts = url.Replace("app://pos/", "").Split('/');
            if (parts.Length >= 6)
            {
                var inv = CultureInfo.InvariantCulture;
                var block = _blocks.Find(b => b.BlockType == parts[0]);
                if (block != null)
                {
                    if (double.TryParse(parts[1], NumberStyles.Float, inv, out var left)) block.LeftPercent = left;
                    if (double.TryParse(parts[2], NumberStyles.Float, inv, out var top)) block.TopPx = top;
                    if (double.TryParse(parts[3], NumberStyles.Float, inv, out var width) && width > 0) block.WidthPercent = width;
                    if (double.TryParse(parts[4], NumberStyles.Float, inv, out var height)) block.HeightPx = height;
                    if (int.TryParse(parts[5], out var zIndex)) block.ZIndex = zIndex;
                }
            }
        }
        else if (url == "app://pos-done")
        {
            _positionCollectionDone = true;
        }
        else if (url.StartsWith("app://status/"))
        {
            var parts = url.Replace("app://status/", "").Split('/');
            if (parts.Length >= 5)
            {
                var name = Uri.UnescapeDataString(parts[0]);
                InfoLabel.Text = name;
                PosLabel.Text = $"X: {parts[1]}%  Y: {parts[2]}px";
                SizeLabel.Text = $"W: {parts[3]}%  H: {parts[4]}px";
            }
        }
        else if (url.StartsWith("app://deselect"))
        {
            InfoLabel.Text = "Click any element to select";
            PosLabel.Text = "";
            SizeLabel.Text = "";
        }
    }

    private bool _positionCollectionDone;

    /// <summary>
    /// Triggers JS to push all block positions to C# via app:// navigation (reliable, no EvaluateJavaScriptAsync parsing).
    /// </summary>
    private async Task CollectPositionsFromEditor()
    {
        if (_currentPage != "index.html") return;

        try
        {
            _positionCollectionDone = false;
            await EditorWebView.EvaluateJavaScriptAsync("window.pushPositionsToHost && window.pushPositionsToHost()");
            
            // Wait for the pos-done signal (positions arrive via HandleEditorCommand)
            var timeout = 50;
            while (!_positionCollectionDone && timeout > 0)
            {
                await Task.Delay(50);
                timeout--;
            }
        }
        catch
        {
            // Editor not available (e.g. page not loaded yet)
        }
    }

    private void SaveBlocksToSettings()
    {
        for (int i = 0; i < _blocks.Count; i++)
            _blocks[i].Order = i;
        League.WebsiteSettings.HomeLayoutBlocks = new List<LayoutBlock>(_blocks);
        League.WebsiteSettings.HomeSectionOrder = _blocks
            .Where(b => b.IsEnabled && !b.IsStructural)
            .OrderBy(b => b.Order)
            .Select(b => b.Id)
            .ToList();
    }

    private void OnResetClicked(object? sender, EventArgs e)
    {
        _blocks = LayoutBlock.GetDefaultBlocks();
        // Re-enable all blocks and recalculate positions from scratch
        foreach (var b in _blocks)
            b.TopPx = 0;
        LayoutBlock.AutoPositionBlocks(_blocks);
        ClearHeaderSubPositions();
        StatusLabel.Text = "Layout reset to defaults";
        GenerateAndLoad();
    }

    private async void OnAutoLayoutClicked(object? sender, EventArgs e)
    {
        var choice = await DisplayActionSheet(
            "Choose Layout", "Cancel", null,
            "--- Page Layouts ---",
            "\U0001F4F0 Single Column (stacked)",
            "\U0001F4C4 Two Column (sidebar)",
            "\U0001F4CA Dashboard (grid)",
            "\U0001F3AF Hero Focus (big welcome)",
            "\U0001F3C6 Compact Page (minimal spacing)",
            "--- Header: Layout ---",
            "\U0001F3AF Centered (classic)",
            "\u2194 Split (logo left, text right)",
            "\u2630 Two-Row (logo+badge / title)",
            "\U0001F3C6 Scoreboard (grid layout)",
            "--- Header: Visual ---",
            "\U0001F3AC Banner (large cinematic)",
            "\U0001F3A8 Glass (frosted blur)",
            "\U0001F30A Mesh Gradient (organic)",
            "\U0001F3DF Stadium (dark spotlights)",
            "--- Header: Animated ---",
            "\U0001F308 Animated Gradient (colour shift)",
            "\U0001F30A Wave Gradient (undulating)",
            "\U0001F4A1 Pulse Glow (breathing light)",
            "\u2728 Shimmer (light sweep)",
            "\U0001F30C Aurora (northern lights)",
            "\U0001F4A0 Neon (glowing text)",
            "\U0001F526 Spotlight Sweep (moving beam)",
            "\U0001F32C Breathing (subtle pulse)",
            "--- Header: Compact ---",
            "\u26A1 Compact (slim bar)",
            "\U0001F4CB Minimal Bar (ultra-slim)",
            "--- Header: Overlap ---",
            "\U0001F4E6 Card (floating card)",
            "\u2B07 Overlay Hero (overlaps content)",
            "\U0001F3C5 Championship (ribbon shape)",
            "--- Header: Minimalist ---",
            "\U0001F524 Text Only (no background)",
            "\u2501 Underline (accent border)",
            "\U0001F4AD Transparent (invisible)");

        if (string.IsNullOrEmpty(choice) || choice == "Cancel" || choice.StartsWith("---")) return;

        // Collect only enabled non-structural content blocks
        var content = _blocks.Where(b => b.IsEnabled && !b.IsStructural).OrderBy(b => b.Order).ToList();
        var structural = _blocks.Where(b => b.IsStructural).ToList();

        // Page layouts
        if (choice.Contains("Single Column"))
            ApplyLayoutSingleColumn(structural, content);
        else if (choice.Contains("Two Column"))
            ApplyLayoutTwoColumn(structural, content);
        else if (choice.Contains("Dashboard"))
            ApplyLayoutDashboard(structural, content);
        else if (choice.Contains("Hero Focus"))
            ApplyLayoutHeroFocus(structural, content);
        else if (choice.Contains("Compact Page"))
            ApplyLayoutCompact(structural, content);
        // Header styles
        else if (choice.Contains("Centered"))
            League.WebsiteSettings.HeaderLayout = "centered";
        else if (choice.Contains("Split"))
            League.WebsiteSettings.HeaderLayout = "split";
        else if (choice.Contains("Two-Row"))
            League.WebsiteSettings.HeaderLayout = "two-row";
        else if (choice.Contains("Scoreboard"))
            League.WebsiteSettings.HeaderLayout = "scoreboard";
        else if (choice.Contains("Banner"))
            League.WebsiteSettings.HeaderLayout = "banner";
        else if (choice.Contains("Glass"))
            League.WebsiteSettings.HeaderLayout = "glass";
        else if (choice.Contains("Mesh Gradient"))
            League.WebsiteSettings.HeaderLayout = "mesh-gradient";
        else if (choice.Contains("Stadium"))
            League.WebsiteSettings.HeaderLayout = "stadium";
        else if (choice.Contains("Animated Gradient"))
            League.WebsiteSettings.HeaderLayout = "animated-gradient";
        else if (choice.Contains("Wave Gradient"))
            League.WebsiteSettings.HeaderLayout = "wave-gradient";
        else if (choice.Contains("Pulse Glow"))
            League.WebsiteSettings.HeaderLayout = "pulse-glow";
        else if (choice.Contains("Shimmer"))
            League.WebsiteSettings.HeaderLayout = "shimmer";
        else if (choice.Contains("Aurora"))
            League.WebsiteSettings.HeaderLayout = "aurora";
        else if (choice.Contains("Neon"))
            League.WebsiteSettings.HeaderLayout = "neon";
        else if (choice.Contains("Spotlight"))
            League.WebsiteSettings.HeaderLayout = "spotlight-sweep";
        else if (choice.Contains("Breathing"))
            League.WebsiteSettings.HeaderLayout = "breathing";
        else if (choice.Contains("Compact"))
            League.WebsiteSettings.HeaderLayout = "compact";
        else if (choice.Contains("Minimal Bar"))
            League.WebsiteSettings.HeaderLayout = "minimal-bar";
        else if (choice.Contains("Card"))
            League.WebsiteSettings.HeaderLayout = "card";
        else if (choice.Contains("Overlay"))
            League.WebsiteSettings.HeaderLayout = "overlay-hero";
        else if (choice.Contains("Championship"))
            League.WebsiteSettings.HeaderLayout = "championship";
        else if (choice.Contains("Text Only"))
            League.WebsiteSettings.HeaderLayout = "text-only";
        else if (choice.Contains("Underline"))
            League.WebsiteSettings.HeaderLayout = "underline";
        else if (choice.Contains("Transparent"))
            League.WebsiteSettings.HeaderLayout = "transparent";

        ClearHeaderSubPositions();
        StatusLabel.Text = $"Applied: {choice.Split(' ', 2).Last()}";
        GenerateAndLoad();
    }

    private void ClearHeaderSubPositions()
    {
        var settings = League.WebsiteSettings;
        settings.HeaderLogoPos = "";
        settings.HeaderTitlePos = "";
        settings.HeaderSubtitlePos = "";
        settings.HeaderBadgePos = "";
    }

    /// <summary>
    /// Single column — everything stacked full-width, simple top-to-bottom flow.
    /// </summary>
    private static void ApplyLayoutSingleColumn(List<LayoutBlock> structural, List<LayoutBlock> content)
    {
        double y = 0;
        const double gap = 20;

        foreach (var b in structural.Where(b => b.BlockType == "header"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 130; }

        foreach (var b in structural.Where(b => b.BlockType == "nav"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 60; }

        y += gap;
        foreach (var b in content)
        {
            b.LeftPercent = 5;
            b.TopPx = y;
            b.WidthPercent = 90;
            b.HeightPx = 0;
            b.ZIndex = 1;
            y += 280 + gap;
        }

        foreach (var b in structural.Where(b => b.BlockType == "footer"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
    }

    /// <summary>
    /// Two column — first content block full-width as hero, rest in 2-column grid, 
    /// like a classic website with a sidebar.
    /// </summary>
    private static void ApplyLayoutTwoColumn(List<LayoutBlock> structural, List<LayoutBlock> content)
    {
        double y = 0;
        const double gap = 20;
        const double margin = 2;

        foreach (var b in structural.Where(b => b.BlockType == "header"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 130; }

        foreach (var b in structural.Where(b => b.BlockType == "nav"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 60; }

        y += gap;

        // First block full-width as hero
        if (content.Count > 0)
        {
            content[0].LeftPercent = margin;
            content[0].TopPx = y;
            content[0].WidthPercent = 96;
            content[0].HeightPx = 0;
            content[0].ZIndex = 1;
            y += 250 + gap;
        }

        // Remaining blocks in 2-column pairs
        for (int i = 1; i < content.Count; i += 2)
        {
            content[i].LeftPercent = margin;
            content[i].TopPx = y;
            content[i].WidthPercent = 47;
            content[i].HeightPx = 0;
            content[i].ZIndex = 1;

            if (i + 1 < content.Count)
            {
                content[i + 1].LeftPercent = 51;
                content[i + 1].TopPx = y;
                content[i + 1].WidthPercent = 47;
                content[i + 1].HeightPx = 0;
                content[i + 1].ZIndex = 1;
            }
            y += 300 + gap;
        }

        foreach (var b in structural.Where(b => b.BlockType == "footer"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
    }

    /// <summary>
    /// Dashboard grid — all content blocks in a tight 2- or 3-column grid, 
    /// like an analytics dashboard.
    /// </summary>
    private static void ApplyLayoutDashboard(List<LayoutBlock> structural, List<LayoutBlock> content)
    {
        double y = 0;
        const double gap = 15;
        const double margin = 1.5;
        const int cols = 3;
        double colWidth = (100 - margin * 2 - gap / 5 * (cols - 1)) / cols;

        foreach (var b in structural.Where(b => b.BlockType == "header"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 100; }

        foreach (var b in structural.Where(b => b.BlockType == "nav"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 50; }

        y += gap;

        for (int i = 0; i < content.Count; i++)
        {
            int col = i % cols;
            if (col == 0 && i > 0) y += 280 + gap;

            content[i].LeftPercent = margin + col * (colWidth + gap / 5);
            content[i].TopPx = y;
            content[i].WidthPercent = colWidth;
            content[i].HeightPx = 0;
            content[i].ZIndex = 1;
        }

        y += 280 + gap;

        foreach (var b in structural.Where(b => b.BlockType == "footer"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
    }

    /// <summary>
    /// Hero Focus — large welcome/hero section at the top, stats overlapping the hero,
    /// then content below. Magazine-style.
    /// </summary>
    private static void ApplyLayoutHeroFocus(List<LayoutBlock> structural, List<LayoutBlock> content)
    {
        double y = 0;
        const double gap = 20;

        foreach (var b in structural.Where(b => b.BlockType == "header"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 130; }

        foreach (var b in structural.Where(b => b.BlockType == "nav"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 60; }

        y += gap;

        // First block (welcome/hero) — extra tall, full width
        var hero = content.FirstOrDefault(b => b.BlockType == "welcome");
        if (hero != null)
        {
            hero.LeftPercent = 0;
            hero.TopPx = y;
            hero.WidthPercent = 100;
            hero.HeightPx = 0;
            hero.ZIndex = 1;
            y += 300;
        }

        // Stats block overlaps hero bottom
        var stats = content.FirstOrDefault(b => b.BlockType == "quick-stats");
        if (stats != null)
        {
            stats.LeftPercent = 8;
            stats.TopPx = y - 60; // overlap into hero
            stats.WidthPercent = 84;
            stats.HeightPx = 0;
            stats.ZIndex = 5; // above hero
            y += 140;
        }

        y += gap;

        // Remaining content in 2 columns
        var remaining = content.Where(b => b != hero && b != stats).ToList();
        for (int i = 0; i < remaining.Count; i += 2)
        {
            remaining[i].LeftPercent = 2;
            remaining[i].TopPx = y;
            remaining[i].WidthPercent = 47;
            remaining[i].HeightPx = 0;
            remaining[i].ZIndex = 1;

            if (i + 1 < remaining.Count)
            {
                remaining[i + 1].LeftPercent = 51;
                remaining[i + 1].TopPx = y;
                remaining[i + 1].WidthPercent = 47;
                remaining[i + 1].HeightPx = 0;
                remaining[i + 1].ZIndex = 1;
            }
            y += 300 + gap;
        }

        foreach (var b in structural.Where(b => b.BlockType == "footer"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
    }

    /// <summary>
    /// Compact — minimal spacing, tight layout. Good for data-heavy pages.
    /// </summary>
    private static void ApplyLayoutCompact(List<LayoutBlock> structural, List<LayoutBlock> content)
    {
        double y = 0;
        const double gap = 8;
        const double margin = 1;

        foreach (var b in structural.Where(b => b.BlockType == "header"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 90; }

        foreach (var b in structural.Where(b => b.BlockType == "nav"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; y += 45; }

        y += gap;

        // Pack into 2 columns with tight spacing
        for (int i = 0; i < content.Count; i += 2)
        {
            content[i].LeftPercent = margin;
            content[i].TopPx = y;
            content[i].WidthPercent = 49;
            content[i].HeightPx = 0;
            content[i].ZIndex = 1;

            if (i + 1 < content.Count)
            {
                content[i + 1].LeftPercent = 50 + margin;
                content[i + 1].TopPx = y;
                content[i + 1].WidthPercent = 49;
                content[i + 1].HeightPx = 0;
                content[i + 1].ZIndex = 1;
            }
            y += 240 + gap;
        }

        foreach (var b in structural.Where(b => b.BlockType == "footer"))
        { b.LeftPercent = 0; b.TopPx = y; b.WidthPercent = 100; b.ZIndex = 10; }
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            // Read current positions from the JS editor into _blocks
            await CollectPositionsFromEditor();
            // Write _blocks into settings and persist to disk
            SaveBlocksToSettings();
            DataStore.Save();
            
            // Regenerate cached files so page switching uses saved positions
            var generator = new WebsiteGenerator(League, League.WebsiteSettings);
            _generatedFiles = generator.GenerateWebsite();
            
            StatusLabel.Text = "Layout saved!";
            await DisplayAlert("Saved", "Layout saved successfully.", "OK");
        }
        catch (Exception ex)
        {
            await DisplayAlert("Error", $"Failed to save: {ex.Message}", "OK");
        }
    }

    private async void OnCloseClicked(object? sender, EventArgs e)
    {
        await Navigation.PopAsync();
    }

    private static string GetEditorScript() => """
<script>
(function() {
    const HANDLE = 8;
    let SNAP = 10;
    let selected = null;
    let mode = 'idle';
    let handle = null;
    let startM = {x:0,y:0};
    let startR = {l:0,t:0,w:0,h:0};
    let editorOn = true;
    let canvasR = null;

    const canvas = document.querySelector('.page-canvas');
    if (!canvas) return;

    const css = document.createElement('style');
    css.textContent = `
        .editor-on [data-block-id] { cursor: pointer; box-sizing: border-box; }
        [data-block-id].sel { outline: 2px solid #3B82F6; outline-offset: -1px; }
        [data-block-id].msel { outline: 2px dashed #3B82F6; outline-offset: -1px; }
        [data-block-id][data-structural="true"].sel { outline-color: #8B5CF6; }
        [data-block-id][data-structural="true"].msel { outline-color: #8B5CF6; }
        [data-block-id].moving { cursor: grabbing !important; opacity: 0.85; }
        /* Shrink sub-elements to content size so selection fits tightly */
        .header-content > [data-block-id] { width: fit-content; margin-left: auto; margin-right: auto; }
        .rh { position:absolute; width:${HANDLE}px; height:${HANDLE}px;
               background:#fff; border:2px solid #3B82F6; border-radius:1px;
               z-index:99999; display:none; pointer-events:auto; }
        .sel > .rh { display:block; }
        .rh.nw{top:-4px;left:-4px;cursor:nw-resize} .rh.n{top:-4px;left:calc(50% - 4px);cursor:n-resize}
        .rh.ne{top:-4px;right:-4px;cursor:ne-resize} .rh.w{top:calc(50% - 4px);left:-4px;cursor:w-resize}
        .rh.e{top:calc(50% - 4px);right:-4px;cursor:e-resize} .rh.sw{bottom:-4px;left:-4px;cursor:sw-resize}
        .rh.s{bottom:-4px;left:calc(50% - 4px);cursor:s-resize} .rh.se{bottom:-4px;right:-4px;cursor:se-resize}
        .blbl { position:absolute; top:-20px; left:0; background:#1E293B; color:#F1F5F9;
                 font:700 10px system-ui; padding:2px 6px; border-radius:3px 3px 0 0;
                 display:none; white-space:nowrap; z-index:99998; pointer-events:none; }
        [data-structural="true"] > .blbl { background:#6D28D9; }
        .editor-on [data-block-id]:hover > .blbl, .sel > .blbl { display:block; }
        .egrid { position:absolute; inset:0; pointer-events:none; z-index:0; }
        .eguide { position:absolute; pointer-events:none; z-index:99990; }
        .eguide-h { left:0; right:0; height:1px; background:#06B6D4; }
        .eguide-v { top:0; bottom:0; width:1px; background:#06B6D4; }
        body:not(.editor-on) .rh, body:not(.editor-on) .blbl,
        body:not(.editor-on) .egrid, body:not(.editor-on) .eguide { display:none !important; }
        body:not(.editor-on) [data-block-id] { cursor:default; outline:none !important; }
        /* Context menu */
        .ectx { position:fixed; z-index:100000; background:#1E293B; border:1px solid #334155;
                 border-radius:8px; padding:4px 0; min-width:200px; box-shadow:0 8px 24px rgba(0,0,0,0.4);
                 font:12px system-ui; color:#E2E8F0; display:none; }
        .ectx-item { padding:6px 14px; cursor:pointer; display:flex; align-items:center; gap:8px; }
        .ectx-item:hover { background:#334155; }
        .ectx-item.disabled { opacity:0.35; pointer-events:none; }
        .ectx-sep { height:1px; background:#334155; margin:4px 0; }
        .ectx-hdr { padding:4px 14px; font-size:10px; color:#64748B; text-transform:uppercase; letter-spacing:0.5px; }
        .ectx-sub { position:relative; }
        .ectx-submenu { position:absolute; left:100%; top:-4px; display:none;
                         background:#1E293B; border:1px solid #334155; border-radius:8px;
                         padding:4px 0; min-width:200px; box-shadow:0 8px 24px rgba(0,0,0,0.4);
                         font:12px system-ui; color:#E2E8F0; z-index:100001; }
        .ectx-sub:hover > .ectx-submenu { display:block; }
        .ectx-sub > .ectx-item::after { content:'?'; margin-left:auto; font-size:10px; opacity:0.5; }
        body:not(.editor-on) .ectx { display:none !important; }
        /* Header sub-element editor styles */
        [data-block-id^="header-"] { position: relative; }
        [data-block-id^="header-"].sel { outline-color: #F59E0B; }
        [data-block-id^="header-"] > .rh { border-color: #F59E0B; }
        [data-block-id^="header-"] > .blbl { background: #B45309; }
    `;
    document.head.appendChild(css);
    document.body.classList.add('editor-on');

    const grid = document.createElement('div');
    grid.className = 'egrid';
    setGridStyle(grid, 50, 'lines');
    canvas.insertBefore(grid, canvas.firstChild);

    /* Snap guide lines container */
    const guidesContainer = document.createElement('div');
    guidesContainer.style.cssText = 'position:absolute;inset:0;pointer-events:none;z-index:99990;';
    canvas.appendChild(guidesContainer);

    function setGridStyle(el, size, type) {
        if (!size && !type.startsWith('cols')) { el.style.backgroundImage = 'none'; return; }
        if (type === 'dots') {
            el.style.backgroundImage = `radial-gradient(circle, rgba(59,130,246,0.15) 1px, transparent 1px)`;
            el.style.backgroundSize = `${size}px ${size}px`;
        } else if (type === 'lines') {
            el.style.backgroundImage = `linear-gradient(rgba(59,130,246,0.04) 1px, transparent 1px), linear-gradient(90deg, rgba(59,130,246,0.04) 1px, transparent 1px)`;
            el.style.backgroundSize = `${size}px ${size}px`;
        } else if (type === 'cols2') {
            el.style.backgroundImage = `linear-gradient(90deg, transparent 49.9%, rgba(59,130,246,0.12) 50%, transparent 50.1%)`;
            el.style.backgroundSize = `100% 100%`;
        } else if (type === 'cols3') {
            el.style.backgroundImage = `linear-gradient(90deg, transparent 33.2%, rgba(59,130,246,0.12) 33.3%, transparent 33.4%, transparent 66.5%, rgba(59,130,246,0.12) 66.6%, transparent 66.7%)`;
            el.style.backgroundSize = `100% 100%`;
        }
    }

    function showGuides(guides) {
        guidesContainer.innerHTML = '';
        guides.forEach(g => {
            const line = document.createElement('div');
            line.className = 'eguide ' + (g.dir === 'h' ? 'eguide-h' : 'eguide-v');
            if (g.dir === 'h') line.style.top = g.pos + 'px';
            else line.style.left = g.pos + '%';
            guidesContainer.appendChild(line);
        });
    }
    function clearGuides() { guidesContainer.innerHTML = ''; }

    /** Find snap guides: compare selected block edges/center against all others */
    function findSnapGuides(el) {
        const guides = [];
        const THRESHOLD = 6; // px tolerance
        const r = el.getBoundingClientRect();
        const cR = canvas.getBoundingClientRect();
        const elTop = r.top - cR.top, elBot = r.bottom - cR.top, elMidY = (elTop+elBot)/2;
        const elLeft = (r.left - cR.left)/cR.width*100, elRight = (r.right - cR.left)/cR.width*100, elMidX = (elLeft+elRight)/2;

        canvas.querySelectorAll(':scope > [data-block-id]').forEach(other => {
            if (other === el || other.style.display === 'none') return;
            const oR = other.getBoundingClientRect();
            const oTop = oR.top - cR.top, oBot = oR.bottom - cR.top, oMidY = (oTop+oBot)/2;
            const oLeft = (oR.left - cR.left)/cR.width*100, oRight = (oR.right - cR.left)/cR.width*100, oMidX = (oLeft+oRight)/2;

            // Horizontal guides (match Y positions)
            if (Math.abs(elTop - oTop) < THRESHOLD) guides.push({dir:'h', pos:oTop});
            if (Math.abs(elBot - oBot) < THRESHOLD) guides.push({dir:'h', pos:oBot});
            if (Math.abs(elMidY - oMidY) < THRESHOLD) guides.push({dir:'h', pos:oMidY});
            if (Math.abs(elTop - oBot) < THRESHOLD) guides.push({dir:'h', pos:oBot});
            if (Math.abs(elBot - oTop) < THRESHOLD) guides.push({dir:'h', pos:oTop});

            // Vertical guides (match X positions)
            if (Math.abs(elLeft - oLeft) < 0.5) guides.push({dir:'v', pos:oLeft});
            if (Math.abs(elRight - oRight) < 0.5) guides.push({dir:'v', pos:oRight});
            if (Math.abs(elMidX - oMidX) < 0.5) guides.push({dir:'v', pos:oMidX});
            if (Math.abs(elLeft - oRight) < 0.5) guides.push({dir:'v', pos:oRight});
            if (Math.abs(elRight - oLeft) < 0.5) guides.push({dir:'v', pos:oLeft});
        });
        // Canvas center guides
        if (Math.abs(elMidX - 50) < 0.5) guides.push({dir:'v', pos:50});
        return guides;
    }

    /* ====== Right-click context menu ====== */
    const ctxMenu = document.createElement('div');
    ctxMenu.className = 'ectx';
    document.body.appendChild(ctxMenu);

    function hideCtx() { ctxMenu.style.display = 'none'; }
    document.addEventListener('click', hideCtx);
    document.addEventListener('scroll', hideCtx, true);

    function ctxItem(label, icon, action, disabled) {
        const d = document.createElement('div');
        d.className = 'ectx-item' + (disabled ? ' disabled' : '');
        d.innerHTML = icon + ' ' + label;
        if (!disabled) d.addEventListener('mousedown', e => { e.stopPropagation(); hideCtx(); action(); });
        return d;
    }
    function ctxSep() { const d = document.createElement('div'); d.className = 'ectx-sep'; return d; }
    function ctxHdr(label) { const d = document.createElement('div'); d.className = 'ectx-hdr'; d.textContent = label; return d; }
    function ctxSub(label, icon, children) {
        const wrap = document.createElement('div');
        wrap.className = 'ectx-sub';
        const trigger = document.createElement('div');
        trigger.className = 'ectx-item';
        trigger.innerHTML = icon + ' ' + label;
        wrap.appendChild(trigger);
        const sub = document.createElement('div');
        sub.className = 'ectx ectx-submenu';
        children.forEach(c => sub.appendChild(c));
        wrap.appendChild(sub);
        return wrap;
    }

    function showCtx(e, el) {
        e.preventDefault();
        e.stopPropagation();
        if (!editorOn) return;

        ctxMenu.innerHTML = '';
        const hasSel = !!el;
        const isSub = hasSel && isSubElement(el);

        if (hasSel) {
            /* Preserve multi-selection if right-clicking a block already in the set */
            if (multiSel.has(el)) {
                if (selected && selected !== el) selected.classList.remove('sel');
                selected = el;
                el.classList.remove('msel');
                el.classList.add('sel');
            } else {
                sel(el);
            }
            canvasR = canvas.getBoundingClientRect();
            const name = el.dataset.blockName || el.dataset.blockId;
            const mCount = multiSel.size > 1 ? ' (+' + (multiSel.size-1) + ' more)' : '';
            ctxMenu.appendChild(ctxHdr(name + mCount));

            /* Structural block quick actions (header, nav, footer) */
            if (!isSub && el.dataset.structural === 'true') {
                ctxMenu.appendChild(ctxItem('Stretch Full Width', '\u2194', () => window.alignBlock('stretchFull')));
                ctxMenu.appendChild(ctxItem('Auto-Position', '\u2B06', () => window.alignBlock('autoPos')));
                ctxMenu.appendChild(ctxItem('Stretch + Auto-Position', '\u2728', () => window.alignBlock('autoStructural')));
                ctxMenu.appendChild(ctxSep());
            }

            /* Align sub-menu */
            ctxMenu.appendChild(ctxSub('Align', '\u2194', [
                ctxItem('Align Left', '\u2B05', () => window.alignBlock('left')),
                ctxItem('Center Horizontally', '\u2194', () => window.alignBlock('centerH')),
                ctxItem('Align Right', '\u27A1', () => window.alignBlock('right')),
                ctxSep(),
                ctxItem('Align Top', '\u2B06', () => window.alignBlock('top')),
                ctxItem('Center Vertically', '\u2195', () => window.alignBlock('centerV')),
                ctxItem('Align Bottom', '\u2B07', () => window.alignBlock('bottom')),
            ]));

            ctxMenu.appendChild(ctxSub('Arrange', '\u2630', [
                ctxItem('Stack Vertically', '\u2B07', () => window.alignBlock('stackV')),
                ctxItem('Stack Horizontally', '\u27A1', () => window.alignBlock('stackH')),
                ctxSep(),
                ctxItem('Align All Left Edges', '\u2B05', () => window.alignBlock('alignAllLeft')),
                ctxItem('Center All Horizontally', '\u2194', () => window.alignBlock('centerAllH')),
            ]));

            ctxMenu.appendChild(ctxSub('Size', '\u2B1C', [
                ctxItem('Match Height (tallest)', '\u2195', () => window.alignBlock('matchHeight')),
                ctxItem('Match Size (both)', '\u2B1C', () => window.alignBlock('matchSize')),
            ]));

            /* Z-index */
            ctxMenu.appendChild(ctxSep());
            ctxMenu.appendChild(ctxItem('Bring Forward', '\u2B06', () => window.bringForward()));
            ctxMenu.appendChild(ctxItem('Send Back', '\u2B07', () => window.sendBack()));

            /* Size */
            if (!isSub) {
                ctxMenu.appendChild(ctxSep());
                ctxMenu.appendChild(ctxSub('Set Width', '\u2194', [
                    ctxItem('Full Width (96%)', '', () => { el.style.width='96%'; el.style.maxWidth='96%'; sendStatus(); }),
                    ctxItem('Half Width (47%)', '', () => { el.style.width='47%'; el.style.maxWidth='47%'; sendStatus(); }),
                    ctxItem('Third Width (31%)', '', () => { el.style.width='31%'; el.style.maxWidth='31%'; sendStatus(); }),
                    ctxItem('Quarter Width (23%)', '', () => { el.style.width='23%'; el.style.maxWidth='23%'; sendStatus(); }),
                    ctxItem('Auto (fit content)', '', () => { el.style.width=''; el.style.maxWidth='96%'; sendStatus(); }),
                ]));
            }

            /* Hide */
            ctxMenu.appendChild(ctxSep());
            ctxMenu.appendChild(ctxItem('Hide Block', '\u274C', () => {
                window.location.href = 'app://toggle-visible/' + el.dataset.blockId;
            }));
        }

        /* Grid sub-menu — always shown */
        ctxMenu.appendChild(ctxSep());
        ctxMenu.appendChild(ctxSub('Grid', '\u2591', [
            ctxItem('No Grid', '', () => window.setGrid(0, '')),
            ctxItem('Dots 25px', '\u00B7', () => window.setGrid(25, 'dots')),
            ctxItem('Lines 50px', '\u2500', () => window.setGrid(50, 'lines')),
            ctxItem('Lines 100px', '\u2501', () => window.setGrid(100, 'lines')),
            ctxSep(),
            ctxItem('2-Column Guides', '\u2502', () => window.setGrid(0, 'cols2')),
            ctxItem('3-Column Guides', '\u2502', () => window.setGrid(0, 'cols3')),
        ]));

        /* Global layout actions */
        ctxMenu.appendChild(ctxSep());
        ctxMenu.appendChild(ctxItem('Auto-position Header/Nav/Footer', '\u2728', () => window.alignBlock('autoAllStructural')));
        ctxMenu.appendChild(ctxItem('Match All Widths', '\uD83D\uDCCF', () => window.alignBlock('matchWidth')));
        ctxMenu.appendChild(ctxSub('Distribute', '\uD83D\uDCD0', [
            ctxItem('Distribute Vertically', '\u2195', () => window.alignBlock('distributeV')),
            ctxItem('Distribute Horizontally', '\u2194', () => window.alignBlock('distributeH')),
            ctxItem('Equal Vertical Gaps', '\u2195', () => window.alignBlock('equalGapsV')),
        ]));

        /* Position & show */
        ctxMenu.style.display = 'block';
        const mx = e.clientX, my = e.clientY;
        const mw = ctxMenu.offsetWidth, mh = ctxMenu.offsetHeight;
        const vw = window.innerWidth, vh = window.innerHeight;
        ctxMenu.style.left = (mx + mw > vw ? vw - mw - 4 : mx) + 'px';
        ctxMenu.style.top = (my + mh > vh ? vh - mh - 4 : my) + 'px';
    }

    /* Wire up contextmenu on canvas and all blocks */
    canvas.addEventListener('contextmenu', e => {
        if (!editorOn) return;
        if (e.target === canvas || e.target.classList.contains('egrid')) {
            /* Right-click on empty canvas area — deselect, show global options only */
            desel();
            showCtx(e, null);
        }
    });

    /* Setup: decorate all [data-block-id] elements with handles and labels */
    function setupBlock(el) {
        const lbl = document.createElement('div');
        lbl.className = 'blbl';
        lbl.textContent = el.dataset.blockName || el.dataset.blockId;
        el.appendChild(lbl);

        ['nw','n','ne','w','e','sw','s','se'].forEach(p => {
            const h = document.createElement('div');
            h.className = 'rh ' + p;
            h.dataset.handle = p;
            el.appendChild(h);
        });

        el.addEventListener('contextmenu', e => {
            if (!editorOn) return;
            showCtx(e, el);
        });

        el.addEventListener('mousedown', e => {
            if (!editorOn) return;
            e.preventDefault(); e.stopPropagation();
            /* Right-click is handled by contextmenu — don't change selection */
            if (e.button === 2) return;
            sel(el, e.shiftKey || e.ctrlKey || e.metaKey);
            canvasR = canvas.getBoundingClientRect();
            if (e.target.classList.contains('rh')) {
                startResize(e, el, e.target.dataset.handle);
            } else {
                startMove(e, el);
            }
        });
    }

    /* Top-level canvas blocks */
    const blocks = [...canvas.querySelectorAll(':scope > [data-block-id]')];
    blocks.forEach(setupBlock);

    /* Header sub-elements: make header-content relative and setup children */
    document.querySelectorAll('.header-content').forEach(hc => {
        hc.style.position = 'relative';
        hc.style.minHeight = '120px';
        [...hc.querySelectorAll('[data-block-id]')].forEach(setupBlock);
    });

    canvas.addEventListener('mousedown', e => {
        if (!editorOn) return;
        if (e.button === 2) return; /* right-click handled by contextmenu */
        if (e.target === canvas || e.target === grid) desel();
    });

    document.addEventListener('mousemove', e => {
        if (mode === 'moving') doMove(e);
        else if (mode === 'resizing') doResize(e);
    });

    document.addEventListener('mouseup', () => {
        if (selected && (mode === 'moving' || mode === 'resizing')) {
            selected.classList.remove('moving');
            multiSel.forEach(s => s.classList.remove('moving'));
            clearGuides();
            sendStatus();
        }
        mode = 'idle'; handle = null; groupStarts = [];
    });

    document.addEventListener('keydown', e => {
        if (!editorOn) return;
        /* Ctrl+A / Cmd+A: select all content blocks */
        if ((e.ctrlKey || e.metaKey) && e.key === 'a') {
            e.preventDefault();
            multiSel.forEach(s => s.classList.remove('msel','sel'));
            multiSel.clear();
            canvas.querySelectorAll(':scope > [data-block-id]').forEach(el => {
                if (el.dataset.structural !== 'true' && el.style.display !== 'none') {
                    multiSel.add(el);
                    el.classList.add('msel');
                }
            });
            if (multiSel.size > 0) {
                selected = [...multiSel][multiSel.size-1];
                selected.classList.remove('msel');
                selected.classList.add('sel');
            }
            sendStatus();
            return;
        }
        if (!selected) return;
        const step = e.shiftKey ? 10 : 1;
        switch(e.key) {
            case 'ArrowUp':    nudge(0,-step); e.preventDefault(); break;
            case 'ArrowDown':  nudge(0,step);  e.preventDefault(); break;
            case 'ArrowLeft':  nudge(-step,0); e.preventDefault(); break;
            case 'ArrowRight': nudge(step,0);  e.preventDefault(); break;
            case 'Delete': case 'Backspace':
                window.location.href = 'app://toggle-visible/' + selected.dataset.blockId;
                break;
            case 'Escape': desel(); break;
        }
    });

    const multiSel = new Set();

    function sel(el, additive) {
        if (additive) {
            /* Shift+click: toggle in/out of multi-selection */
            if (multiSel.has(el)) {
                multiSel.delete(el);
                el.classList.remove('msel','sel');
                if (selected === el) {
                    selected = multiSel.size > 0 ? [...multiSel][multiSel.size-1] : null;
                    if (selected) selected.classList.add('sel');
                }
            } else {
                multiSel.add(el);
                if (selected && selected !== el) {
                    multiSel.add(selected);
                    selected.classList.remove('sel');
                    selected.classList.add('msel');
                }
                el.classList.add('sel');
                selected = el;
            }
            /* Ensure all multi-selected show the dashed outline */
            multiSel.forEach(s => { if (s !== selected) s.classList.add('msel'); });
        } else {
            /* Normal click: clear multi and select one */
            multiSel.forEach(s => s.classList.remove('msel','sel'));
            multiSel.clear();
            if (selected && selected !== el) selected.classList.remove('sel');
            selected = el;
            el.classList.add('sel');
        }
        sendStatus();
    }
    function desel() {
        multiSel.forEach(s => s.classList.remove('msel','sel'));
        multiSel.clear();
        if (selected) selected.classList.remove('sel');
        selected = null;
        window.location.href = 'app://deselect';
    }
    function getSelSet() {
        /* Returns the working set: multi-selection if >1, otherwise just the primary */
        if (multiSel.size > 1) return [...multiSel];
        if (selected) return [selected];
        return [];
    }

    let groupStarts = [];
    function startMove(e, el) {
        mode = 'moving';
        el.classList.add('moving');
        startM = {x: e.clientX, y: e.clientY};
        startR = { l: parseFloat(el.style.left)||0, t: parseFloat(el.style.top)||0 };
        /* For header sub-elements, resolve the reference container */
        if (isSubElement(el)) {
            const parent = el.closest('.header-content') || el.parentElement;
            canvasR = parent.getBoundingClientRect();
        }
        /* Capture start positions for group move */
        groupStarts = [];
        if (multiSel.size > 1) {
            multiSel.forEach(s => {
                groupStarts.push({ el: s, l: parseFloat(s.style.left)||0, t: parseFloat(s.style.top)||0 });
            });
        }
    }
    function doMove(e) {
        if (!selected) return;
        const dx = (e.clientX - startM.x) / canvasR.width * 100;
        const dy = e.clientY - startM.y;
        /* Move all multi-selected blocks together */
        if (groupStarts.length > 1) {
            groupStarts.forEach(gs => {
                let nl = gs.l + dx;
                let nt = gs.t + dy;
                if (SNAP > 0 && !e.altKey) { nt = Math.round(nt/SNAP)*SNAP; nl = Math.round(nl*2)/2; }
                nl = Math.max(0, nl); nt = Math.max(0, nt);
                gs.el.style.left = nl + '%';
                gs.el.style.top = nt + 'px';
            });
        } else {
            let nl = startR.l + dx;
            let nt = startR.t + dy;
            if (SNAP > 0 && !e.altKey) { nt = Math.round(nt/SNAP)*SNAP; nl = Math.round(nl*2)/2; }
            nl = Math.max(0, nl); nt = Math.max(0, nt);
            selected.style.left = nl + '%';
            selected.style.top = nt + 'px';
            /* When a sub-element moves, make it absolutely positioned */
            if (isSubElement(selected)) selected.style.position = 'absolute';
        }
        /* Show smart snap guides */
        if (!isSubElement(selected)) {
            const guides = findSnapGuides(selected);
            if (guides.length > 0) showGuides(guides); else clearGuides();
        }
        sendStatus();
    }

    function startResize(e, el, h) {
        mode = 'resizing'; handle = h;
        el.classList.add('moving');
        startM = {x: e.clientX, y: e.clientY};
        if (isSubElement(el)) {
            const parent = el.closest('.header-content') || el.parentElement;
            canvasR = parent.getBoundingClientRect();
        }
        const r = el.getBoundingClientRect();
        startR = {
            l: parseFloat(el.style.left)||0,
            t: parseFloat(el.style.top)||0,
            w: parseFloat(el.style.width) || parseFloat(el.style.maxWidth) || (r.width/canvasR.width*100),
            h: r.height
        };
    }
    function doResize(e) {
        if (!selected || !handle) return;
        const dx = (e.clientX - startM.x) / canvasR.width * 100;
        const dy = e.clientY - startM.y;
        let l=startR.l, t=startR.t, w=startR.w, h=startR.h;
        if (handle.includes('e')) w = Math.max(5, startR.w + dx);
        if (handle.includes('w')) { w = Math.max(5, startR.w - dx); l = startR.l + dx; }
        if (handle.includes('s')) h = Math.max(30, startR.h + dy);
        if (handle.includes('n')) { h = Math.max(30, startR.h - dy); t = startR.t + dy; }
        selected.style.left = l + '%';
        selected.style.top = t + 'px';
        selected.style.width = w + '%';
        selected.style.maxWidth = w + '%';
        if (handle.includes('n') || handle.includes('s')) {
            selected.style.height = h + 'px';
            selected.style.overflow = 'auto';
        }
        sendStatus();
    }

    function nudge(dx, dy) {
        if (!selected) return;
        canvasR = canvasR || canvas.getBoundingClientRect();
        const dxP = (dx / canvasR.width) * 100;
        const targets = multiSel.size > 1 ? [...multiSel] : [selected];
        targets.forEach(el => {
            let l = (parseFloat(el.style.left)||0) + dxP;
            let t = (parseFloat(el.style.top)||0) + dy;
            l = Math.max(0, l); t = Math.max(0, t);
            el.style.left = l + '%';
            el.style.top = t + 'px';
        });
        sendStatus();
    }

    function sendStatus() {
        if (!selected) return;
        const n = encodeURIComponent(selected.dataset.blockName || selected.dataset.blockId);
        const l = (parseFloat(selected.style.left)||0).toFixed(1);
        const t = (parseFloat(selected.style.top)||0).toFixed(0);
        const w = (parseFloat(selected.style.width) || parseFloat(selected.style.maxWidth) || 100).toFixed(1);
        const r = selected.getBoundingClientRect();
        const h = selected.style.height ? parseFloat(selected.style.height).toFixed(0) : Math.round(r.height).toString();
        const count = multiSel.size > 1 ? '/' + multiSel.size + 'sel' : '';
        /* Use hash to avoid navigation reload */
        window.location.hash = 'status/' + n + count + '/' + l + '/' + t + '/' + w + '/' + h;
    }

    /* === Public API for C# to call === */
    function isSubElement(el) {
        return el.dataset.blockId && el.dataset.blockId.startsWith('header-');
    }

    window.toggleEditor = function(on) {
        editorOn = on;
        document.body.classList.toggle('editor-on', on);
        if (!on) { desel(); }
    };
    window.setSnap = function(on) { SNAP = on ? 10 : 0; };
    window.setGrid = function(size, type) { setGridStyle(grid, size, type); };
    window.bringForward = function() {
        if (selected) { const z = parseInt(selected.style.zIndex)||1; selected.style.zIndex = z+1; sendStatus(); }
    };
    window.sendBack = function() {
        if (selected) { const z = parseInt(selected.style.zIndex)||1; selected.style.zIndex = Math.max(1, z-1); sendStatus(); }
    };
    window.getBlockPositions = function() {
        const pos = {};
        /* Top-level canvas blocks */
        canvas.querySelectorAll(':scope > [data-block-id]').forEach(el => {
            pos[el.dataset.blockId] = {
                left: parseFloat(el.style.left)||0,
                top: parseFloat(el.style.top)||0,
                width: parseFloat(el.style.width) || parseFloat(el.style.maxWidth) || 100,
                height: el.style.height ? parseFloat(el.style.height) : 0,
                zIndex: parseInt(el.style.zIndex)||1
            };
        });
        /* Header sub-elements (positions relative to header) */
        document.querySelectorAll('[data-block-id^="header-"]').forEach(el => {
            if (el.style.position === 'absolute') {
                pos[el.dataset.blockId] = {
                    left: parseFloat(el.style.left)||0,
                    top: parseFloat(el.style.top)||0,
                    width: 0, height: 0, zIndex: 0
                };
            }
        });
        return JSON.stringify(pos);
    };

    /* Push all block positions to C# via app:// protocol (reliable, no EvaluateJavaScriptAsync needed) */
    window.pushPositionsToHost = function() {
        const msgs = [];
        canvas.querySelectorAll(':scope > [data-block-id]').forEach(el => {
            const id = el.dataset.blockId;
            const l = (parseFloat(el.style.left)||0).toFixed(2);
            const t = (parseFloat(el.style.top)||0).toFixed(1);
            const w = (parseFloat(el.style.width) || parseFloat(el.style.maxWidth) || 100).toFixed(2);
            const h = el.style.height ? parseFloat(el.style.height).toFixed(0) : '0';
            const z = parseInt(el.style.zIndex)||1;
            msgs.push('app://pos/' + id + '/' + l + '/' + t + '/' + w + '/' + h + '/' + z);
        });
        msgs.push('app://pos-done');
        let i = 0;
        function sendNext() {
            if (i < msgs.length) {
                window.location.href = msgs[i++];
                setTimeout(sendNext, 20);
            }
        }
        sendNext();
    };

    window.alignBlock = function(cmd) {
        const globalCmds = ['distributeV','distributeH','equalGapsV','matchWidth','matchHeight','matchSize','stackV','stackH','alignAllLeft','centerAllH','autoAllStructural'];
        if (!selected && !globalCmds.includes(cmd)) return;
        const cR = canvas.getBoundingClientRect();
        const all = [...canvas.querySelectorAll(':scope > [data-block-id]')].filter(el => el.style.display !== 'none');

        /* Helper: get the actual rendered width of an element as a % of canvas */
        function pctW(el) { return el.getBoundingClientRect().width / cR.width * 100; }
        /* Helper: get configured/explicit width or fall back to rendered */
        function cfgW(el) { return parseFloat(el.style.width) || parseFloat(el.style.maxWidth) || pctW(el); }

        /* Working set: multi-selection if available, otherwise all non-structural blocks */
        const hasMulti = multiSel.size > 1;
        const targets = hasMulti
            ? [...multiSel].filter(el => el.style.display !== 'none')
            : all.filter(el => el.dataset.structural !== 'true');

        if (cmd === 'left') {
            const els = hasMulti ? targets : (selected ? [selected] : []);
            els.forEach(el => { el.style.left = '0%'; });
        } else if (cmd === 'right') {
            const els = hasMulti ? targets : (selected ? [selected] : []);
            els.forEach(el => { el.style.left = (100 - pctW(el)) + '%'; });
        } else if (cmd === 'centerH') {
            const els = hasMulti ? targets : (selected ? [selected] : []);
            els.forEach(el => { el.style.left = ((100 - pctW(el)) / 2) + '%'; });
        } else if (cmd === 'top') {
            /* Find first structural block bottom as reference */
            let topY = 0;
            all.forEach(el => {
                if (el.dataset.structural === 'true') {
                    const b = el.getBoundingClientRect();
                    topY = Math.max(topY, b.bottom - cR.top);
                }
            });
            const els = hasMulti ? targets : (selected ? [selected] : []);
            els.forEach(el => { el.style.top = topY + 'px'; });
        } else if (cmd === 'bottom') {
            const footer = all.find(el => el.dataset.blockId === 'footer');
            if (footer) {
                const ft = parseFloat(footer.style.top) || 0;
                const els = hasMulti ? targets : (selected ? [selected] : []);
                els.forEach(el => {
                    const h = el.getBoundingClientRect().height;
                    el.style.top = (ft - h - 10) + 'px';
                });
            }
        } else if (cmd === 'centerV') {
            /* Center between nav bottom and footer top */
            let navBot = 180, footerTop = 1400;
            all.forEach(el => {
                if (el.dataset.blockId === 'nav') navBot = el.getBoundingClientRect().bottom - cR.top;
                if (el.dataset.blockId === 'footer') footerTop = parseFloat(el.style.top) || 1400;
            });
            const els = hasMulti ? targets : (selected ? [selected] : []);
            els.forEach(el => {
                const h = el.getBoundingClientRect().height;
                el.style.top = (navBot + (footerTop - navBot - h) / 2) + 'px';
            });
        } else if (cmd === 'matchWidth') {
            /* Set target blocks to the same width as the widest one */
            let maxW = 0;
            targets.forEach(el => { maxW = Math.max(maxW, cfgW(el)); });
            if (maxW > 0) {
                targets.forEach(el => { el.style.maxWidth = maxW + '%'; el.style.width = maxW + '%'; });
            }
        } else if (cmd === 'matchHeight') {
            /* Set target blocks to the same height as the tallest */
            let maxH = 0;
            targets.forEach(el => { maxH = Math.max(maxH, el.getBoundingClientRect().height); });
            if (maxH > 30) {
                targets.forEach(el => { el.style.height = maxH + 'px'; el.style.overflow = 'auto'; });
            }
        } else if (cmd === 'matchSize') {
            /* Match both width and height to largest */
            let maxW = 0, maxH = 0;
            targets.forEach(el => {
                maxW = Math.max(maxW, cfgW(el));
                maxH = Math.max(maxH, el.getBoundingClientRect().height);
            });
            targets.forEach(el => {
                if (maxW > 0) { el.style.maxWidth = maxW + '%'; el.style.width = maxW + '%'; }
                if (maxH > 30) { el.style.height = maxH + 'px'; el.style.overflow = 'auto'; }
            });
        } else if (cmd === 'distributeV') {
            /* Distribute target blocks evenly between nav and footer */
            const content = [...targets];
            if (content.length < 2) return;
            content.sort((a,b) => (parseFloat(a.style.top)||0) - (parseFloat(b.style.top)||0));
            let startY = 180, endY = 1400;
            all.forEach(el => {
                if (el.dataset.blockId === 'nav') startY = (parseFloat(el.style.top)||0) + el.getBoundingClientRect().height + 20;
                if (el.dataset.blockId === 'footer') endY = (parseFloat(el.style.top)||0) - 20;
            });
            const totalH = content.reduce((s,el) => s + el.getBoundingClientRect().height, 0);
            const gap = (endY - startY - totalH) / Math.max(1, content.length - 1);
            let y = startY;
            content.forEach(el => {
                el.style.top = Math.round(y) + 'px';
                y += el.getBoundingClientRect().height + gap;
            });
        } else if (cmd === 'distributeH') {
            /* Distribute target blocks evenly across the horizontal space */
            const content = [...targets];
            if (content.length < 2) return;
            content.sort((a,b) => (parseFloat(a.style.left)||0) - (parseFloat(b.style.left)||0));
            const totalW = content.reduce((s,el) => s + pctW(el), 0);
            const hGap = (96 - totalW) / Math.max(1, content.length - 1);
            let x = 2;
            content.forEach(el => {
                el.style.left = x.toFixed(1) + '%';
                x += pctW(el) + hGap;
            });
        } else if (cmd === 'equalGapsV') {
            /* Make gaps between target blocks equal, keeping first and last in place */
            const content = [...targets];
            if (content.length < 3) return;
            content.sort((a,b) => (parseFloat(a.style.top)||0) - (parseFloat(b.style.top)||0));
            const firstTop = parseFloat(content[0].style.top) || 0;
            const lastEl = content[content.length - 1];
            const lastTop = parseFloat(lastEl.style.top) || 0;
            const totalH = content.slice(0, -1).reduce((s,el) => s + el.getBoundingClientRect().height, 0);
            const gap = (lastTop - firstTop - totalH) / Math.max(1, content.length - 1);
            let y = firstTop;
            content.forEach((el, i) => {
                if (i === content.length - 1) return;
                el.style.top = Math.round(y) + 'px';
                y += el.getBoundingClientRect().height + gap;
            });
        } else if (cmd === 'stackV') {
            /* Stack target blocks vertically at the same left position */
            const content = [...targets];
            if (content.length < 1) return;
            content.sort((a,b) => (parseFloat(a.style.top)||0) - (parseFloat(b.style.top)||0));
            const refLeft = selected ? (parseFloat(selected.style.left)||2) : 2;
            let startY = 180;
            all.forEach(el => {
                if (el.dataset.blockId === 'nav') startY = (parseFloat(el.style.top)||0) + el.getBoundingClientRect().height + 20;
            });
            let y = startY;
            content.forEach(el => {
                el.style.left = refLeft + '%';
                el.style.top = Math.round(y) + 'px';
                y += el.getBoundingClientRect().height + 20;
            });
        } else if (cmd === 'stackH') {
            /* Stack target blocks side by side on the same row */
            const content = [...targets];
            if (content.length < 1) return;
            content.sort((a,b) => (parseFloat(a.style.left)||0) - (parseFloat(b.style.left)||0));
            const refTop = selected ? (parseFloat(selected.style.top)||200) : 200;
            const availW = 96;
            const eachW = Math.max(10, availW / content.length - 1);
            let x = 2;
            content.forEach(el => {
                el.style.left = x.toFixed(1) + '%';
                el.style.top = refTop + 'px';
                el.style.width = eachW.toFixed(1) + '%';
                el.style.maxWidth = eachW.toFixed(1) + '%';
                x += eachW + 1;
            });
        } else if (cmd === 'alignAllLeft') {
            /* Align target blocks to the same left edge as the selected block */
            const refLeft = selected ? (parseFloat(selected.style.left)||0) : 2;
            targets.forEach(el => { el.style.left = refLeft + '%'; });
        } else if (cmd === 'centerAllH') {
            /* Center target blocks horizontally */
            targets.forEach(el => {
                el.style.left = ((100 - pctW(el)) / 2) + '%';
            });
        } else if (cmd === 'stretchFull') {
            /* Stretch selected block to full width */
            const els = hasMulti ? targets : (selected ? [selected] : []);
            els.forEach(el => {
                el.style.left = '0%';
                el.style.width = '100%';
                el.style.maxWidth = '100%';
            });
        } else if (cmd === 'autoPos') {
            /* Auto-position the selected structural block */
            if (!selected) return;
            autoPositionOne(selected, all, cR);
        } else if (cmd === 'autoStructural') {
            /* Stretch + auto-position the selected structural block */
            const els = hasMulti ? targets : (selected ? [selected] : []);
            els.forEach(el => {
                el.style.left = '0%';
                el.style.width = '100%';
                el.style.maxWidth = '100%';
                autoPositionOne(el, all, cR);
            });
        } else if (cmd === 'autoAllStructural') {
            /* Auto-stretch and position ALL structural blocks */
            const structs = all.filter(el => el.dataset.structural === 'true');
            structs.forEach(el => {
                el.style.left = '0%';
                el.style.width = '100%';
                el.style.maxWidth = '100%';
            });
            /* Position in order: header(0) -> nav(below header) -> footer(below everything) */
            const header = structs.find(el => el.dataset.blockId === 'header');
            const nav = structs.find(el => el.dataset.blockId === 'nav');
            const footer = structs.find(el => el.dataset.blockId === 'footer');
            let y = 0;
            if (header) { header.style.top = '0px'; header.style.zIndex = '10'; y = header.getBoundingClientRect().height; }
            if (nav) { nav.style.top = Math.round(y) + 'px'; nav.style.zIndex = '10'; y += nav.getBoundingClientRect().height; }
            /* Footer goes below the lowest content block */
            if (footer) {
                let maxBot = y;
                all.forEach(el => {
                    if (el.dataset.structural !== 'true') {
                        const bot = (parseFloat(el.style.top)||0) + el.getBoundingClientRect().height;
                        if (bot > maxBot) maxBot = bot;
                    }
                });
                footer.style.top = Math.round(maxBot + 30) + 'px';
                footer.style.zIndex = '10';
            }
        }

        function autoPositionOne(el, allBlocks, canvasRect) {
            const id = el.dataset.blockId;
            if (id === 'header') {
                el.style.top = '0px';
                el.style.zIndex = '10';
            } else if (id === 'nav') {
                const header = allBlocks.find(b => b.dataset.blockId === 'header');
                const headerBot = header ? header.getBoundingClientRect().height : 0;
                el.style.top = Math.round(headerBot) + 'px';
                el.style.zIndex = '10';
            } else if (id === 'footer') {
                let maxBot = 0;
                allBlocks.forEach(b => {
                    if (b !== el) {
                        const bot = (parseFloat(b.style.top)||0) + b.getBoundingClientRect().height;
                        if (bot > maxBot) maxBot = bot;
                    }
                });
                el.style.top = Math.round(maxBot + 30) + 'px';
                el.style.zIndex = '10';
            }
        }

        sendStatus();
    };
})();
</script>
""";

    // Use hash changes for status updates (no navigation/reload)
    protected override void OnAppearing()
    {
        base.OnAppearing();
        EditorWebView.Navigated += OnWebViewNavigated;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        EditorWebView.Navigated -= OnWebViewNavigated;
    }

    private void OnWebViewNavigated(object? sender, WebNavigatedEventArgs e)
    {
        // Poll for hash-based status updates via a timer
        StartStatusPolling();
    }

    private CancellationTokenSource? _pollCts;

    private void StartStatusPolling()
    {
        _pollCts?.Cancel();
        _pollCts = new CancellationTokenSource();
        var token = _pollCts.Token;

        Dispatcher.StartTimer(TimeSpan.FromMilliseconds(150), () =>
        {
            if (token.IsCancellationRequested) return false;
            _ = PollStatusAsync();
            return !token.IsCancellationRequested;
        });
    }

    private string _lastHash = "";

    private async Task PollStatusAsync()
    {
        try
        {
            var hash = await EditorWebView.EvaluateJavaScriptAsync("window.location.hash");
            if (hash == null || hash == _lastHash) return;
            _lastHash = hash;

            // Strip quotes if returned as string literal
            hash = hash.Trim('"').TrimStart('#');

            if (hash.StartsWith("status/"))
            {
                var parts = hash.Replace("status/", "").Split('/');
                if (parts.Length >= 5)
                {
                    InfoLabel.Text = Uri.UnescapeDataString(parts[0]);
                    PosLabel.Text = $"X: {parts[1]}%  Y: {parts[2]}px";
                    SizeLabel.Text = $"W: {parts[3]}%  H: {parts[4]}px";
                }
            }
        }
        catch { /* ignore */ }
    }
}
