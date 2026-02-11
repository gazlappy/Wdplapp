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

        // Inject freeform editor JS on home page
        if (_currentPage == "index.html")
            html = html.Replace("</body>", GetEditorScript() + "\n</body>");

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

    private void OnPageChanged(object? sender, EventArgs e)
    {
        if (_syncingPicker) return;
        if (PagePicker.SelectedIndex < 0) return;
        var label = PagePicker.SelectedItem?.ToString() ?? "Home";
        var page = Pages.FirstOrDefault(p => p.Label == label);
        _currentPage = page.FileName ?? "index.html";
        _currentQueryString = null;
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

    private async void OnBringForwardClicked(object? sender, EventArgs e)
    {
        await EditorWebView.EvaluateJavaScriptAsync("window.bringForward && window.bringForward()");
    }

    private async void OnSendBackClicked(object? sender, EventArgs e)
    {
        await EditorWebView.EvaluateJavaScriptAsync("window.sendBack && window.sendBack()");
    }

    private void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
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
        else if (url.StartsWith("app://status/"))
        {
            // Status updates from JS: app://status/{name}/{x}/{y}/{w}/{h}
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

    private async Task CollectPositionsFromEditor()
    {
        if (_currentPage != "index.html") return;

        try
        {
            var json = await EditorWebView.EvaluateJavaScriptAsync("window.getBlockPositions ? window.getBlockPositions() : ''");
            if (string.IsNullOrWhiteSpace(json) || json == "''") return;

            // MAUI WebView may return escaped JSON string
            if (json.StartsWith("\""))
                json = JsonSerializer.Deserialize<string>(json) ?? "";

            if (string.IsNullOrWhiteSpace(json)) return;

            var positions = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(json);
            if (positions == null) return;

            var inv = CultureInfo.InvariantCulture;
            foreach (var kvp in positions)
            {
                // Header sub-element positions ? stored as "left%;top%" strings
                if (kvp.Key.StartsWith("header-"))
                {
                    var left = kvp.Value.GetProperty("left").GetDouble();
                    var top = kvp.Value.GetProperty("top").GetDouble();
                    var posStr = $"{left.ToString("F1", inv)}%{';'}{top.ToString("F0", inv)}px";
                    
                    var settings = League.WebsiteSettings;
                    switch (kvp.Key)
                    {
                        case "header-logo":     settings.HeaderLogoPos = posStr; break;
                        case "header-title":    settings.HeaderTitlePos = posStr; break;
                        case "header-subtitle": settings.HeaderSubtitlePos = posStr; break;
                        case "header-badge":    settings.HeaderBadgePos = posStr; break;
                    }
                    continue;
                }

                var block = _blocks.Find(b => b.BlockType == kvp.Key);
                if (block != null)
                {
                    block.LeftPercent = kvp.Value.GetProperty("left").GetDouble();
                    block.TopPx = kvp.Value.GetProperty("top").GetDouble();
                    block.WidthPercent = kvp.Value.GetProperty("width").GetDouble();
                    block.HeightPx = kvp.Value.GetProperty("height").GetDouble();
                    block.ZIndex = kvp.Value.GetProperty("zIndex").GetInt32();
                }
            }
        }
        catch
        {
            // Ignore JS evaluation errors
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
        // Clear header sub-element positions
        var settings = League.WebsiteSettings;
        settings.HeaderLogoPos = "";
        settings.HeaderTitlePos = "";
        settings.HeaderSubtitlePos = "";
        settings.HeaderBadgePos = "";
        StatusLabel.Text = "Layout reset to defaults";
        GenerateAndLoad();
    }

    private async void OnSaveClicked(object? sender, EventArgs e)
    {
        try
        {
            await CollectPositionsFromEditor();
            SaveBlocksToSettings();
            DataStore.Save();
            StatusLabel.Text = "Saved!";
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
        [data-block-id][data-structural="true"].sel { outline-color: #8B5CF6; }
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
        .egrid { position:absolute; inset:0; pointer-events:none; z-index:0;
                 background-image: linear-gradient(rgba(59,130,246,0.04) 1px, transparent 1px),
                 linear-gradient(90deg, rgba(59,130,246,0.04) 1px, transparent 1px);
                 background-size: 50px 50px; }
        body:not(.editor-on) .rh, body:not(.editor-on) .blbl,
        body:not(.editor-on) .egrid { display:none !important; }
        body:not(.editor-on) [data-block-id] { cursor:default; outline:none !important; }
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
    canvas.insertBefore(grid, canvas.firstChild);

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

        el.addEventListener('mousedown', e => {
            if (!editorOn) return;
            e.preventDefault(); e.stopPropagation();
            sel(el);
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
        if (e.target === canvas || e.target === grid) desel();
    });

    document.addEventListener('mousemove', e => {
        if (mode === 'moving') doMove(e);
        else if (mode === 'resizing') doResize(e);
    });

    document.addEventListener('mouseup', () => {
        if (selected && (mode === 'moving' || mode === 'resizing')) {
            selected.classList.remove('moving');
            sendStatus();
        }
        mode = 'idle'; handle = null;
    });

    document.addEventListener('keydown', e => {
        if (!editorOn || !selected) return;
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

    function sel(el) {
        if (selected) selected.classList.remove('sel');
        selected = el; el.classList.add('sel');
        sendStatus();
    }
    function desel() {
        if (selected) selected.classList.remove('sel');
        selected = null;
        window.location.href = 'app://deselect';
    }

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
    }
    function doMove(e) {
        if (!selected) return;
        const dx = (e.clientX - startM.x) / canvasR.width * 100;
        const dy = e.clientY - startM.y;
        let nl = startR.l + dx;
        let nt = startR.t + dy;
        if (SNAP > 0 && !e.altKey) { nt = Math.round(nt/SNAP)*SNAP; nl = Math.round(nl*2)/2; }
        nl = Math.max(0, nl); nt = Math.max(0, nt);
        selected.style.left = nl + '%';
        selected.style.top = nt + 'px';
        /* When a sub-element moves, make it absolutely positioned */
        if (isSubElement(selected)) selected.style.position = 'absolute';
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
        let l = (parseFloat(selected.style.left)||0) + dxP;
        let t = (parseFloat(selected.style.top)||0) + dy;
        l = Math.max(0, l); t = Math.max(0, t);
        selected.style.left = l + '%';
        selected.style.top = t + 'px';
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
        /* Use hash to avoid navigation reload */
        window.location.hash = 'status/' + n + '/' + l + '/' + t + '/' + w + '/' + h;
    }

    /* === Public API for C# to call === */
    function isSubElement(el) {
        return el.dataset.blockId && el.dataset.blockId.startsWith('header-');
    }

    window.toggleEditor = function(on) {
        editorOn = on;
        document.body.classList.toggle('editor-on', on);
        if (!on && selected) { selected.classList.remove('sel'); selected = null; }
    };
    window.setSnap = function(on) { SNAP = on ? 10 : 0; };
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
