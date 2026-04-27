using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests.Features.Games.Pool.Engine;

public class PoolGameSettingsModuleTests
{
    [Fact]
    public void GenerateJavaScript_ReturnsNonNullString()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GenerateJavaScript_ReturnsNonEmptyString()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolGameSettingsObject()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("const PoolGameSettings = {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsModuleHeaderComment()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// ============================================", result);
        Assert.Contains("// POOL GAME SETTINGS MODULE", result);
        Assert.Contains("// User-friendly game options and cosmetics", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRequiredProperties()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("isVisible: false,", result);
        Assert.Contains("game: null,", result);
        Assert.Contains("_devOverride: false,", result);
        Assert.Contains("settings: {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsDefaultPlayerSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("player1Name: 'Player 1',", result);
        Assert.Contains("player2Name: 'Player 2',", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShotControlSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("shotControlMode: 'drag',", result);
        Assert.Contains("showAimLine: true,", result);
        Assert.Contains("showTrajectory: true,", result);
        Assert.Contains("showGhostBall: true,", result);
        Assert.Contains("fineTuneSensitivity: 15,", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsTableColorSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("clothColor: '#1a7f37',", result);
        Assert.Contains("railColor: '#5C3317',", result);
        Assert.Contains("pocketColor: '#1a1a1a',", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBallStyleSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("ballStyle: 'uk',", result);
        Assert.Contains("cueBallColor: '#f5f5f5',", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsAudioSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("soundEnabled: true,", result);
        Assert.Contains("soundVolume: 70,", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGameAssistSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showSpinIndicator: true,", result);
        Assert.Contains("showPowerMeter: true,", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBreakRulesSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("goldenBall: false,", result);
        Assert.Contains("goldenDuck: false,", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsMatchSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("matchType: 'single',", result);
        Assert.Contains("player1Frames: 0,", result);
        Assert.Contains("player2Frames: 0,", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsLightingSettings()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("lightTemperature: 'warm'", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsInitMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("init(game) {", result);
        Assert.Contains("this.game = game;", result);
        Assert.Contains("this.loadSettings();", result);
        Assert.Contains("this.createSettingsButton();", result);
        Assert.Contains("this.createSettingsPanel();", result);
        Assert.Contains("console.log('PoolGameSettings initialized');", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCreateSettingsButtonMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createSettingsButton() {", result);
        Assert.Contains("const btn = document.createElement('button');", result);
        Assert.Contains("btn.id = 'gameSettingsBtn';", result);
        Assert.Contains("btn.innerHTML = '\\u2699\\uFE0F Settings';", result);
        Assert.Contains("document.body.appendChild(btn);", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCreateSettingsPanelMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createSettingsPanel() {", result);
        Assert.Contains("const panel = document.createElement('div');", result);
        Assert.Contains("panel.id = 'gameSettingsPanel';", result);
        Assert.Contains("document.body.appendChild(panel);", result);
        Assert.Contains("this.attachEventListeners();", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsSettingsPanelHTML()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='settings-overlay'", result);
        Assert.Contains("<div class='settings-modal'>", result);
        Assert.Contains("<div class='settings-header'>", result);
        Assert.Contains("Game Settings</h2>", result);
        Assert.Contains("<div class='settings-content'>", result);
        Assert.Contains("<div class='settings-tabs'>", result);
        Assert.Contains("<div class='settings-footer'>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsAllTabs()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<button class='tab-btn active' data-tab='players'>", result);
        Assert.Contains("Players</button>", result);
        Assert.Contains("<button class='tab-btn' data-tab='rules'>", result);
        Assert.Contains("Rules</button>", result);
        Assert.Contains("<button class='tab-btn' data-tab='controls'>", result);
        Assert.Contains("Controls</button>", result);
        Assert.Contains("<button class='tab-btn' data-tab='table'>", result);
        Assert.Contains("Table</button>", result);
        Assert.Contains("<button class='tab-btn' data-tab='audio'>", result);
        Assert.Contains("Audio</button>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPlayersTabContent()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='tab-content active' id='tab-players'>", result);
        Assert.Contains("<h3>Player Names</h3>", result);
        Assert.Contains("<input type='text' id='setting-player1Name'", result);
        Assert.Contains("<input type='text' id='setting-player2Name'", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRulesTabContent()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='tab-content' id='tab-rules'>", result);
        Assert.Contains("<h3>Match Format</h3>", result);
        Assert.Contains("<select id='setting-matchType'>", result);
        Assert.Contains("<option value='single'>Single Frame</option>", result);
        Assert.Contains("<option value='best3'>Best of 3 Frames</option>", result);
        Assert.Contains("<option value='best5'>Best of 5 Frames</option>", result);
        Assert.Contains("<option value='best7'>Best of 7 Frames</option>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBreakShotRules()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<h3>Break Shot Rules</h3>", result);
        Assert.Contains("<label>Golden Ball:</label>", result);
        Assert.Contains("<input type='checkbox' id='setting-goldenBall'>", result);
        Assert.Contains("<span class='setting-desc'>Win by potting black on break</span>", result);
        Assert.Contains("<label>Golden Duck:</label>", result);
        Assert.Contains("<input type='checkbox' id='setting-goldenDuck'>", result);
        Assert.Contains("<span class='setting-desc'>Lose by potting black + white on break</span>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsControlsTabContent()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='tab-content' id='tab-controls'>", result);
        Assert.Contains("<h3>Shot Control</h3>", result);
        Assert.Contains("<select id='setting-shotControlMode'>", result);
        Assert.Contains("<option value='drag'>Drag & Release</option>", result);
        Assert.Contains("<option value='click'>Click Power</option>", result);
        Assert.Contains("<option value='slider'>Power Slider</option>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsVisualAids()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<h3>Visual Aids</h3>", result);
        Assert.Contains("<input type='checkbox' id='setting-showAimLine' checked>", result);
        Assert.Contains("<input type='checkbox' id='setting-showTrajectory' checked>", result);
        Assert.Contains("<input type='checkbox' id='setting-showGhostBall' checked>", result);
        Assert.Contains("<input type='checkbox' id='setting-showSpinIndicator' checked>", result);
        Assert.Contains("<input type='checkbox' id='setting-showPowerMeter' checked>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsTableTabContent()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='tab-content' id='tab-table'>", result);
        Assert.Contains("<h3>Table Colors</h3>", result);
        Assert.Contains("<label>Cloth Color:</label>", result);
        Assert.Contains("<label>Rail Color:</label>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsColorOptions()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='color-options' data-setting='clothColor'>", result);
        Assert.Contains("<button class='color-btn' data-color='#1a7f37' style='background:#1a7f37' title='Classic Green'></button>", result);
        Assert.Contains("<input type='color' id='setting-clothColor' class='color-picker' title='Custom Color'>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBallStyleOptions()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<h3>Ball Style</h3>", result);
        Assert.Contains("<div class='ball-style-option' data-style='uk'>", result);
        Assert.Contains("<span>UK 8-Ball<br><small>Red & Yellow</small></span>", result);
        Assert.Contains("<div class='ball-style-option' data-style='us'>", result);
        Assert.Contains("<span>US 8-Ball<br><small>Solids & Stripes</small></span>", result);
        Assert.Contains("<div class='ball-style-option' data-style='classic'>", result);
        Assert.Contains("<span>Modern<br><small>Blue & Orange</small></span>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsAudioTabContent()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='tab-content' id='tab-audio'>", result);
        Assert.Contains("<h3>Sound Settings</h3>", result);
        Assert.Contains("<input type='checkbox' id='setting-soundEnabled' checked>", result);
        Assert.Contains("<input type='range' id='setting-soundVolume' min='0' max='100' value='70'>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsSoundTestButtons()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<h3>Test Sounds</h3>", result);
        Assert.Contains("<button onclick='PoolGameSettings.testSound(\"cueHit\")'", result);
        Assert.Contains("Cue Hit</button>", result);
        Assert.Contains("<button onclick='PoolGameSettings.testSound(\"ballCollision\")'", result);
        Assert.Contains("Ball Collision</button>", result);
        Assert.Contains("<button onclick='PoolGameSettings.testSound(\"pocket\")'", result);
        Assert.Contains("Pocket</button>", result);
        Assert.Contains("<button onclick='PoolGameSettings.testSound(\"cushion\")'", result);
        Assert.Contains("Cushion</button>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsFooterButtons()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<button class='btn-reset' onclick='PoolGameSettings.resetToDefaults()'>Reset Defaults</button>", result);
        Assert.Contains("<button class='btn-apply' onclick='PoolGameSettings.applyAndClose()'>Apply & Close</button>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCSSStyles()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("const style = document.createElement('style');", result);
        Assert.Contains("style.textContent = `", result);
        Assert.Contains("#gameSettingsPanel {", result);
        Assert.Contains("document.head.appendChild(style);", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsSettingsOverlayStyle()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains(".settings-overlay {", result);
        Assert.Contains("backdrop-filter: blur(4px);", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsSettingsModalStyle()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains(".settings-modal {", result);
        Assert.Contains("max-width: 550px;", result);
        Assert.Contains("border-radius: 16px;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsAttachEventListenersMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("attachEventListeners() {", result);
        Assert.Contains("// Tab switching", result);
        Assert.Contains("document.querySelectorAll('.tab-btn').forEach(btn => {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsToggleMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("toggle() {", result);
        Assert.Contains("// Block opening if dev override is active", result);
        Assert.Contains("if (this._devOverride && !this.isVisible) return;", result);
        Assert.Contains("this.isVisible = !this.isVisible;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsLoadSettingsToUIMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("loadSettingsToUI() {", result);
        Assert.Contains("// Player names", result);
        Assert.Contains("document.getElementById('setting-player1Name').value = this.settings.player1Name;", result);
        Assert.Contains("document.getElementById('setting-player2Name').value = this.settings.player2Name;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsUpdateFrameScoreDisplayMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("updateFrameScoreDisplay() {", result);
        Assert.Contains("const p1Score = document.getElementById('p1FrameScore');", result);
        Assert.Contains("const p2Score = document.getElementById('p2FrameScore');", result);
        Assert.Contains("if (p1Score) p1Score.textContent = this.settings.player1Frames;", result);
        Assert.Contains("if (p2Score) p2Score.textContent = this.settings.player2Frames;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplySettingsMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("applySettings() {", result);
        Assert.Contains("if (!this.game) return;", result);
        Assert.Contains("// If dev settings override is active, skip applying player settings", result);
        Assert.Contains("if (this._devOverride) {", result);
        Assert.Contains("this.saveSettings();", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsUpdateBallColorsMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("updateBallColors() {", result);
        Assert.Contains("if (!this.game || !this.game.balls) return;", result);
        Assert.Contains("// Ball color schemes", result);
        Assert.Contains("const schemes = {", result);
        Assert.Contains("uk: { group1: 'red', group2: 'yellow', group1Color: '#dc2626', group2Color: '#eab308' },", result);
        Assert.Contains("us: { group1: 'solid', group2: 'stripe', group1Color: '#2563eb', group2Color: '#f97316' },", result);
        Assert.Contains("classic: { group1: 'blue', group2: 'orange', group1Color: '#3b82f6', group2Color: '#f97316' }", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsTestSoundMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("testSound(soundName) {", result);
        Assert.Contains("if (typeof PoolAudio !== 'undefined') {", result);
        Assert.Contains("PoolAudio.play(soundName, 0.8);", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsResetToDefaultsMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("resetToDefaults() {", result);
        Assert.Contains("this.settings = {", result);
        Assert.Contains("this.loadSettingsToUI();", result);
        Assert.Contains("this.applySettings();", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRecordFrameWinMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("recordFrameWin(playerIndex) {", result);
        Assert.Contains("if (playerIndex === 0) {", result);
        Assert.Contains("this.settings.player1Frames++;", result);
        Assert.Contains("} else {", result);
        Assert.Contains("this.settings.player2Frames++;", result);
        Assert.Contains("// Check for match win", result);
        Assert.Contains("const framesToWin = this.getFramesToWin();", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGetFramesToWinMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("getFramesToWin() {", result);
        Assert.Contains("switch (this.settings.matchType) {", result);
        Assert.Contains("case 'best3': return 2;", result);
        Assert.Contains("case 'best5': return 3;", result);
        Assert.Contains("case 'best7': return 4;", result);
        Assert.Contains("default: return 1;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGetMatchNameMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("getMatchName() {", result);
        Assert.Contains("case 'best3': return 'Best of 3';", result);
        Assert.Contains("case 'best5': return 'Best of 5';", result);
        Assert.Contains("case 'best7': return 'Best of 7';", result);
        Assert.Contains("default: return 'Single Frame';", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsResetMatchMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("resetMatch() {", result);
        Assert.Contains("this.settings.player1Frames = 0;", result);
        Assert.Contains("this.settings.player2Frames = 0;", result);
        Assert.Contains("this.updateFrameScoreDisplay();", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyAndCloseMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("applyAndClose() {", result);
        Assert.Contains("this.applySettings();", result);
        Assert.Contains("this.toggle();", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsSaveSettingsMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("saveSettings() {", result);
        Assert.Contains("try {", result);
        Assert.Contains("localStorage.setItem('poolGameSettings', JSON.stringify(this.settings));", result);
        Assert.Contains("} catch (e) {", result);
        Assert.Contains("console.warn('Could not save settings:', e);", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsLoadSettingsMethod()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("loadSettings() {", result);
        Assert.Contains("const saved = localStorage.getItem('poolGameSettings');", result);
        Assert.Contains("if (saved) {", result);
        Assert.Contains("this.settings = { ...this.settings, ...JSON.parse(saved) };", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEventListenerForColorButtons()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Color buttons", result);
        Assert.Contains("document.querySelectorAll('.color-options').forEach(container => {", result);
        Assert.Contains("const setting = container.dataset.setting;", result);
        Assert.Contains("container.querySelectorAll('.color-btn').forEach(btn => {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEventListenerForColorPickers()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Color pickers", result);
        Assert.Contains("['clothColor', 'railColor'].forEach(setting => {", result);
        Assert.Contains("const picker = document.getElementById('setting-' + setting);", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEventListenerForBallStyleOptions()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Ball style options", result);
        Assert.Contains("document.querySelectorAll('.ball-style-option').forEach(option => {", result);
        Assert.Contains("this.settings.ballStyle = option.dataset.style;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEventListenerForRangeInputs()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Range inputs", result);
        Assert.Contains("document.querySelectorAll('.setting-row input[type=\"range\"]').forEach(input => {", result);
        Assert.Contains("const valueSpan = input.nextElementSibling;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEventListenerForTextInputs()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Text inputs", result);
        Assert.Contains("['player1Name', 'player2Name'].forEach(setting => {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEventListenerForCheckboxes()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Checkbox inputs", result);
        Assert.Contains("['showAimLine', 'showTrajectory', 'showGhostBall', 'showSpinIndicator', 'showPowerMeter', 'soundEnabled', 'goldenBall', 'goldenDuck'].forEach(setting => {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsMatchTypeEventListener()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Match type select", result);
        Assert.Contains("const matchTypeSelect = document.getElementById('setting-matchType');", result);
        Assert.Contains("// Reset frame scores when match type changes", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsLightTemperatureEventListener()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Light temperature select", result);
        Assert.Contains("const lightTempSelect = document.getElementById('setting-lightTemperature');", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsFineTuneSensitivitySetting()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<label>Fine-Tune Aim (hold .):</label>", result);
        Assert.Contains("<input type='range' id='setting-fineTuneSensitivity' min='5' max='50' value='15'>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsLightingSection()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<h3>Lighting</h3>", result);
        Assert.Contains("<select id='setting-lightTemperature'>", result);
        Assert.Contains("<option value='warm'>", result);
        Assert.Contains("Warm (Tungsten)</option>", result);
        Assert.Contains("<option value='neutral'>", result);
        Assert.Contains("Neutral (White)</option>", result);
        Assert.Contains("<option value='cool'>", result);
        Assert.Contains("Cool (Fluorescent)</option>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsFrameScoreDisplay()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='frame-score-display' id='frameScoreDisplay'>", result);
        Assert.Contains("<span id='p1FrameScore'>0</span> - <span id='p2FrameScore'>0</span>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRulesInfoSection()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("<div class='rules-info'>", result);
        Assert.Contains("<strong>Golden Ball OFF:</strong> Black potted on break = re-spot black, continue</p>", result);
        Assert.Contains("<strong>Golden Ball ON:</strong> Black potted on break = instant win!</p>", result);
        Assert.Contains("<strong>Golden Duck ON:</strong> Black + White potted on break = instant loss!</p>", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShotModeSelectEventListener()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Select inputs", result);
        Assert.Contains("const shotModeSelect = document.getElementById('setting-shotControlMode');", result);
        Assert.Contains("if (shotModeSelect) {", result);
        Assert.Contains("shotModeSelect.addEventListener('change', () => {", result);
        Assert.Contains("this.settings.shotControlMode = shotModeSelect.value;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplySettingsLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply player names", result);
        Assert.Contains("if (this.game.players) {", result);
        Assert.Contains("this.game.players[0].name = this.settings.player1Name;", result);
        Assert.Contains("this.game.players[1].name = this.settings.player2Name;", result);
        Assert.Contains("if (typeof this.game.updateTurnDisplay === 'function') {", result);
        Assert.Contains("this.game.updateTurnDisplay();", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyVisualAidsLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply visual aids", result);
        Assert.Contains("this.game.showAimLine = this.settings.showAimLine;", result);
        Assert.Contains("this.game.showTrajectoryPrediction = this.settings.showTrajectory;", result);
        Assert.Contains("this.game.showGhostBalls = this.settings.showGhostBall;", result);
        Assert.Contains("this.game.showSpinArrows = this.settings.showSpinIndicator;", result);
        Assert.Contains("this.game.showPowerMeter = this.settings.showPowerMeter;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyTableColorsLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply table colors", result);
        Assert.Contains("this.game.clothColor = this.settings.clothColor;", result);
        Assert.Contains("this.game.railColor = this.settings.railColor;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyAudioLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply audio settings", result);
        Assert.Contains("if (typeof PoolAudio !== 'undefined') {", result);
        Assert.Contains("PoolAudio.setEnabled(this.settings.soundEnabled);", result);
        Assert.Contains("PoolAudio.setVolume(this.settings.soundVolume / 100);", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyLightTemperatureLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply light temperature (#15)", result);
        Assert.Contains("if (typeof PoolVFX !== 'undefined') {", result);
        Assert.Contains("PoolVFX.lightTemperature = this.settings.lightTemperature || 'warm';", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyBreakRulesLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply break rules (Golden Ball / Golden Duck)", result);
        Assert.Contains("this.game.goldenBall = this.settings.goldenBall;", result);
        Assert.Contains("this.game.goldenDuck = this.settings.goldenDuck;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyMatchSettingsLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply match settings", result);
        Assert.Contains("this.game.matchType = this.settings.matchType;", result);
        Assert.Contains("this.game.player1Frames = this.settings.player1Frames;", result);
        Assert.Contains("this.game.player2Frames = this.settings.player2Frames;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsApplyFineTuneSensitivityLogic()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// Apply fine-tune sensitivity", result);
        Assert.Contains("if (typeof PoolInput !== 'undefined') {", result);
        Assert.Contains("PoolInput.fineTuneSensitivity = this.settings.fineTuneSensitivity / 100;", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsButtonStyleDefinitions()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains(".btn-reset {", result);
        Assert.Contains(".btn-apply {", result);
        Assert.Contains(".btn-reset:hover {", result);
        Assert.Contains(".btn-apply:hover {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBallPreviewStyles()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains(".ball-preview {", result);
        Assert.Contains(".ball-preview .ball {", result);
        Assert.Contains(".ball.red { background: radial-gradient(circle at 30% 30%, #ff6b6b, #dc2626); }", result);
        Assert.Contains(".ball.yellow { background: radial-gradient(circle at 30% 30%, #fde047, #eab308); }", result);
        Assert.Contains(".ball.black { background: radial-gradient(circle at 30% 30%, #525252, #1a1a1a); }", result);
        Assert.Contains(".ball.solid { background: radial-gradient(circle at 30% 30%, #3b82f6, #1d4ed8); }", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsColorPickerStyles()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains(".color-picker {", result);
        Assert.Contains(".color-btn {", result);
        Assert.Contains(".color-btn:hover {", result);
        Assert.Contains(".color-btn.selected {", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsTabStyles()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.Contains(".settings-tabs {", result);
        Assert.Contains(".tab-btn {", result);
        Assert.Contains(".tab-btn:hover {", result);
        Assert.Contains(".tab-btn.active {", result);
        Assert.Contains(".tab-content {", result);
        Assert.Contains(".tab-content.active {", result);
    }

    [Fact]
    public void GenerateJavaScript_HasConsistentStringLength()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        // The string should be quite large (several thousand characters)
        Assert.True(result.Length > 10000, $"Expected JavaScript length > 10000, but was {result.Length}");
    }

    [Fact]
    public void GenerateJavaScript_StartsWithExpectedComment()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.StartsWith("// ============================================", result.TrimStart());
    }

    [Fact]
    public void GenerateJavaScript_EndsWithClosingBraceAndSemicolon()
    {
        // Act
        var result = PoolGameSettingsModule.GenerateJavaScript();

        // Assert
        Assert.EndsWith("};", result.TrimEnd());
    }
}
