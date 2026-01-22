namespace Wdpl2.Services;

/// <summary>
/// Game settings module - provides user-friendly options for players
/// Separate from dev settings, focused on gameplay and cosmetics
/// </summary>
public static class PoolGameSettingsModule
{
    public static string GenerateJavaScript()
    {
        return """
// ============================================
// POOL GAME SETTINGS MODULE
// User-friendly game options and cosmetics
// ============================================

const PoolGameSettings = {
    isVisible: false,
    game: null,
    
    
    // Default settings
    settings: {
        // Player settings
        player1Name: 'Player 1',
        player2Name: 'Player 2',
        
        // Shot control
        shotControlMode: 'drag',
        showAimLine: true,
        showTrajectory: true,
        showGhostBall: true,
        fineTuneSensitivity: 15,
        
        // Table colors
        clothColor: '#1a7f37',      // Green baize
        railColor: '#8B4513',        // Wood brown
        pocketColor: '#1a1a1a',      // Dark pocket
        
        // Ball style
        ballStyle: 'uk',             // 'uk' (red/yellow), 'us' (solids/stripes), 'classic'
        cueBallColor: '#f5f5f5',
        
        // Audio
        soundEnabled: true,
        soundVolume: 70,
        
        // Game assists
        showSpinIndicator: true,
        showPowerMeter: true,
        
        // Break rules
        goldenBall: false,           // If true, potting black on break wins the game
        goldenDuck: false,           // If true, potting black AND white on break loses the game
        
        // Match settings
        matchType: 'single',         // 'single', 'best3', 'best5', 'best7'
        player1Frames: 0,
        player2Frames: 0
    },
    
    init(game) {
        this.game = game;
        this.loadSettings();
        this.createSettingsButton();
        this.createSettingsPanel();
        console.log('PoolGameSettings initialized');
    },
    
    createSettingsButton() {
        const btn = document.createElement('button');
        btn.id = 'gameSettingsBtn';
        btn.innerHTML = '?? Settings';
        btn.style.cssText = `
            position: fixed;
            top: 10px;
            left: 10px;
            padding: 10px 18px;
            background: linear-gradient(135deg, #3B82F6, #2563EB);
            color: white;
            border: none;
            border-radius: 8px;
            font-weight: bold;
            cursor: pointer;
            z-index: 9998;
            font-size: 14px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.3);
            transition: all 0.2s;
        `;
        btn.onmouseover = () => btn.style.transform = 'scale(1.05)';
        btn.onmouseout = () => btn.style.transform = 'scale(1)';
        btn.onclick = () => this.toggle();
        document.body.appendChild(btn);
    },
    
    createSettingsPanel() {
        const panel = document.createElement('div');
        panel.id = 'gameSettingsPanel';
        panel.innerHTML = `
            <div class='settings-overlay' onclick='PoolGameSettings.toggle()'></div>
            <div class='settings-modal'>
                <div class='settings-header'>
                    <h2>?? Game Settings</h2>
                    <button class='settings-close' onclick='PoolGameSettings.toggle()'>?</button>
                </div>
                
                <div class='settings-content'>
                    <div class='settings-tabs'>
                        <button class='tab-btn active' data-tab='players'>?? Players</button>
                        <button class='tab-btn' data-tab='rules'>?? Rules</button>
                        <button class='tab-btn' data-tab='controls'>?? Controls</button>
                        <button class='tab-btn' data-tab='table'>?? Table</button>
                        <button class='tab-btn' data-tab='audio'>?? Audio</button>
                    </div>
                    
                    <!-- Players Tab -->
                    <div class='tab-content active' id='tab-players'>
                        <div class='settings-section'>
                            <h3>Player Names</h3>
                            <div class='setting-row'>
                                <label>Player 1:</label>
                                <input type='text' id='setting-player1Name' maxlength='15' placeholder='Player 1'>
                            </div>
                            <div class='setting-row'>
                                <label>Player 2:</label>
                                <input type='text' id='setting-player2Name' maxlength='15' placeholder='Player 2'>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Rules Tab -->
                    <div class='tab-content' id='tab-rules'>
                        <div class='settings-section'>
                            <h3>Match Format</h3>
                            <div class='setting-row'>
                                <label>Match Type:</label>
                                <select id='setting-matchType'>
                                    <option value='single'>Single Frame</option>
                                    <option value='best3'>Best of 3 Frames</option>
                                    <option value='best5'>Best of 5 Frames</option>
                                    <option value='best7'>Best of 7 Frames</option>
                                </select>
                            </div>
                            <div class='frame-score-display' id='frameScoreDisplay'>
                                <span id='p1FrameScore'>0</span> - <span id='p2FrameScore'>0</span>
                            </div>
                        </div>
                        <div class='settings-section'>
                            <h3>Break Shot Rules</h3>
                            <div class='setting-row'>
                                <label>Golden Ball:</label>
                                <input type='checkbox' id='setting-goldenBall'>
                                <span class='setting-desc'>Win by potting black on break</span>
                            </div>
                            <div class='setting-row'>
                                <label>Golden Duck:</label>
                                <input type='checkbox' id='setting-goldenDuck'>
                                <span class='setting-desc'>Lose by potting black + white on break</span>
                            </div>
                            <div class='rules-info'>
                                <p>?? <strong>Golden Ball OFF:</strong> Black potted on break = re-spot black, continue</p>
                                <p>?? <strong>Golden Ball ON:</strong> Black potted on break = instant win!</p>
                                <p>?? <strong>Golden Duck ON:</strong> Black + White potted on break = instant loss!</p>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Controls Tab -->
                    <div class='tab-content' id='tab-controls'>
                        <div class='settings-section'>
                            <h3>Shot Control</h3>
                            <div class='setting-row'>
                                <label>Control Mode:</label>
                                <select id='setting-shotControlMode'>
                                    <option value='drag'>Drag & Release</option>
                                    <option value='click'>Click Power</option>
                                    <option value='slider'>Power Slider</option>
                                </select>
                            </div>
                            <div class='setting-row'>
                                <label>Fine-Tune Aim (hold .):</label>
                                <input type='range' id='setting-fineTuneSensitivity' min='5' max='50' value='15'>
                                <span class='range-value'>15%</span>
                            </div>
                        </div>
                        <div class='settings-section'>
                            <h3>Visual Aids</h3>
                            <div class='setting-row'>
                                <label>Show Aim Line:</label>
                                <input type='checkbox' id='setting-showAimLine' checked>
                            </div>
                            <div class='setting-row'>
                                <label>Show Ball Trajectory:</label>
                                <input type='checkbox' id='setting-showTrajectory' checked>
                            </div>
                            <div class='setting-row'>
                                <label>Show Ghost Ball:</label>
                                <input type='checkbox' id='setting-showGhostBall' checked>
                            </div>
                            <div class='setting-row'>
                                <label>Show Spin Indicator:</label>
                                <input type='checkbox' id='setting-showSpinIndicator' checked>
                            </div>
                            <div class='setting-row'>
                                <label>Show Power Meter:</label>
                                <input type='checkbox' id='setting-showPowerMeter' checked>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Table Tab -->
                    <div class='tab-content' id='tab-table'>
                        <div class='settings-section'>
                            <h3>Table Colors</h3>
                            <div class='setting-row'>
                                <label>Cloth Color:</label>
                                <div class='color-options' data-setting='clothColor'>
                                    <button class='color-btn' data-color='#1a7f37' style='background:#1a7f37' title='Classic Green'></button>
                                    <button class='color-btn' data-color='#1e5631' style='background:#1e5631' title='Dark Green'></button>
                                    <button class='color-btn' data-color='#234f8b' style='background:#234f8b' title='Tournament Blue'></button>
                                    <button class='color-btn' data-color='#1a3a5c' style='background:#1a3a5c' title='Navy Blue'></button>
                                    <button class='color-btn' data-color='#8b1a1a' style='background:#8b1a1a' title='Red Baize'></button>
                                    <button class='color-btn' data-color='#4a1a4a' style='background:#4a1a4a' title='Purple'></button>
                                    <button class='color-btn' data-color='#2d2d2d' style='background:#2d2d2d' title='Black'></button>
                                    <input type='color' id='setting-clothColor' class='color-picker' title='Custom Color'>
                                </div>
                            </div>
                            <div class='setting-row'>
                                <label>Rail Color:</label>
                                <div class='color-options' data-setting='railColor'>
                                    <button class='color-btn' data-color='#8B4513' style='background:#8B4513' title='Classic Oak'></button>
                                    <button class='color-btn' data-color='#5c3317' style='background:#5c3317' title='Dark Walnut'></button>
                                    <button class='color-btn' data-color='#d4a574' style='background:#d4a574' title='Light Maple'></button>
                                    <button class='color-btn' data-color='#2d1b0e' style='background:#2d1b0e' title='Espresso'></button>
                                    <button class='color-btn' data-color='#8b0000' style='background:#8b0000' title='Mahogany'></button>
                                    <button class='color-btn' data-color='#1a1a1a' style='background:#1a1a1a' title='Black'></button>
                                    <input type='color' id='setting-railColor' class='color-picker' title='Custom Color'>
                                </div>
                            </div>
                        </div>
                        <div class='settings-section'>
                            <h3>Ball Style</h3>
                            <div class='ball-style-options'>
                                <div class='ball-style-option' data-style='uk'>
                                    <div class='ball-preview'>
                                        <span class='ball red'></span>
                                        <span class='ball yellow'></span>
                                        <span class='ball black'></span>
                                    </div>
                                    <span>UK 8-Ball<br><small>Red & Yellow</small></span>
                                </div>
                                <div class='ball-style-option' data-style='us'>
                                    <div class='ball-preview'>
                                        <span class='ball solid'></span>
                                        <span class='ball stripe'></span>
                                        <span class='ball black'></span>
                                    </div>
                                    <span>US 8-Ball<br><small>Solids & Stripes</small></span>
                                </div>
                                <div class='ball-style-option' data-style='classic'>
                                    <div class='ball-preview'>
                                        <span class='ball blue-ball'></span>
                                        <span class='ball orange-ball'></span>
                                        <span class='ball black'></span>
                                    </div>
                                    <span>Modern<br><small>Blue & Orange</small></span>
                                </div>
                            </div>
                        </div>
                    </div>
                    
                    <!-- Audio Tab -->
                    <div class='tab-content' id='tab-audio'>
                        <div class='settings-section'>
                            <h3>Sound Settings</h3>
                            <div class='setting-row'>
                                <label>Sound Effects:</label>
                                <input type='checkbox' id='setting-soundEnabled' checked>
                            </div>
                            <div class='setting-row'>
                                <label>Volume:</label>
                                <input type='range' id='setting-soundVolume' min='0' max='100' value='70'>
                                <span class='range-value'>70%</span>
                            </div>
                        </div>
                        <div class='settings-section'>
                            <h3>Test Sounds</h3>
                            <div class='sound-test-buttons'>
                                <button onclick='PoolGameSettings.testSound("cueHit")'>?? Cue Hit</button>
                                <button onclick='PoolGameSettings.testSound("ballCollision")'>?? Ball Collision</button>
                                <button onclick='PoolGameSettings.testSound("pocket")'>??? Pocket</button>
                                <button onclick='PoolGameSettings.testSound("cushion")'>?? Cushion</button>
                            </div>
                        </div>
                    </div>
                </div>
                
                <div class='settings-footer'>
                    <button class='btn-reset' onclick='PoolGameSettings.resetToDefaults()'>Reset Defaults</button>
                    <button class='btn-apply' onclick='PoolGameSettings.applyAndClose()'>Apply & Close</button>
                </div>
            </div>
        `;
        
        // Add styles
        const style = document.createElement('style');
        style.textContent = `
            #gameSettingsPanel {
                display: none;
                position: fixed;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                z-index: 10001;
            }
            #gameSettingsPanel.visible {
                display: block;
            }
            .settings-overlay {
                position: absolute;
                top: 0;
                left: 0;
                width: 100%;
                height: 100%;
                background: rgba(0,0,0,0.7);
                backdrop-filter: blur(4px);
            }
            .settings-modal {
                position: absolute;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
                width: 90%;
                max-width: 550px;
                max-height: 85vh;
                background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
                border-radius: 16px;
                box-shadow: 0 20px 60px rgba(0,0,0,0.5);
                overflow: hidden;
                display: flex;
                flex-direction: column;
            }
            .settings-header {
                display: flex;
                justify-content: space-between;
                align-items: center;
                padding: 20px 25px;
                background: linear-gradient(135deg, #2a3f5f 0%, #1a2a4a 100%);
                border-bottom: 1px solid rgba(255,255,255,0.1);
            }
            .settings-header h2 {
                margin: 0;
                color: white;
                font-size: 22px;
            }
            .settings-close {
                background: rgba(255,255,255,0.1);
                border: none;
                color: white;
                width: 36px;
                height: 36px;
                border-radius: 50%;
                font-size: 18px;
                cursor: pointer;
                transition: all 0.2s;
            }
            .settings-close:hover {
                background: rgba(239,68,68,0.8);
            }
            .settings-content {
                flex: 1;
                overflow-y: auto;
                padding: 0;
            }
            .settings-tabs {
                display: flex;
                background: rgba(0,0,0,0.3);
                padding: 10px;
                gap: 8px;
                flex-wrap: wrap;
            }
            .tab-btn {
                flex: 1;
                min-width: 80px;
                padding: 10px 15px;
                background: rgba(255,255,255,0.1);
                border: none;
                border-radius: 8px;
                color: #94a3b8;
                font-size: 13px;
                font-weight: 600;
                cursor: pointer;
                transition: all 0.2s;
            }
            .tab-btn:hover {
                background: rgba(255,255,255,0.15);
            }
            .tab-btn.active {
                background: linear-gradient(135deg, #3B82F6, #2563EB);
                color: white;
            }
            .tab-content {
                display: none;
                padding: 20px;
            }
            .tab-content.active {
                display: block;
            }
            .settings-section {
                background: rgba(255,255,255,0.05);
                border-radius: 12px;
                padding: 18px;
                margin-bottom: 15px;
            }
            .settings-section h3 {
                margin: 0 0 15px 0;
                color: #4ade80;
                font-size: 14px;
                text-transform: uppercase;
                letter-spacing: 1px;
            }
            .setting-row {
                display: flex;
                align-items: center;
                margin-bottom: 12px;
                gap: 12px;
            }
            .setting-row:last-child {
                margin-bottom: 0;
            }
            .setting-row label {
                flex: 0 0 140px;
                color: #e2e8f0;
                font-size: 14px;
            }
            .setting-row input[type='text'],
            .setting-row select {
                flex: 1;
                padding: 10px 14px;
                background: rgba(0,0,0,0.3);
                border: 1px solid rgba(255,255,255,0.2);
                border-radius: 8px;
                color: white;
                font-size: 14px;
            }
            .setting-row input[type='text']:focus,
            .setting-row select:focus {
                outline: none;
                border-color: #3B82F6;
            }
            .setting-row input[type='range'] {
                flex: 1;
                height: 6px;
                border-radius: 3px;
                background: rgba(255,255,255,0.2);
                outline: none;
                cursor: pointer;
            }
            .setting-row input[type='range']::-webkit-slider-thumb {
                -webkit-appearance: none;
                width: 18px;
                height: 18px;
                border-radius: 50%;
                background: #3B82F6;
                cursor: pointer;
                border: 2px solid white;
            }
            .setting-row input[type='checkbox'] {
                width: 22px;
                height: 22px;
                cursor: pointer;
                accent-color: #3B82F6;
                flex-shrink: 0;
            }
            .setting-desc {
                flex: 1;
                color: #94a3b8;
                font-size: 12px;
                font-style: italic;
            }
            .rules-info {
                margin-top: 15px;
                padding: 12px;
                background: rgba(0,0,0,0.2);
                border-radius: 8px;
                border-left: 3px solid #fbbf24;
            }
            .rules-info p {
                margin: 8px 0;
                color: #cbd5e1;
                font-size: 13px;
                line-height: 1.4;
            }
            .rules-info p:first-child {
                margin-top: 0;
            }
            .rules-info p:last-child {
                margin-bottom: 0;
            }
            .frame-score-display {
                text-align: center;
                padding: 15px;
                background: rgba(0,0,0,0.3);
                border-radius: 10px;
                margin-top: 10px;
                font-size: 32px;
                font-weight: bold;
                color: #4ade80;
                letter-spacing: 8px;
            }
            .range-value {
                flex: 0 0 45px;
                text-align: right;
                color: #fbbf24;
                font-weight: bold;
                font-size: 13px;
            }
            .color-options {
                display: flex;
                gap: 8px;
                flex-wrap: wrap;
                align-items: center;
            }
            .color-btn {
                width: 36px;
                height: 36px;
                border-radius: 8px;
                border: 3px solid transparent;
                cursor: pointer;
                transition: all 0.2s;
            }
            .color-btn:hover {
                transform: scale(1.1);
            }
            .color-btn.selected {
                border-color: white;
                box-shadow: 0 0 10px rgba(255,255,255,0.5);
            }
            .color-picker {
                width: 36px;
                height: 36px;
                border: none;
                border-radius: 8px;
                cursor: pointer;
                background: none;
            }
            .ball-style-options {
                display: flex;
                gap: 12px;
                flex-wrap: wrap;
            }
            .ball-style-option {
                flex: 1;
                min-width: 120px;
                padding: 15px;
                background: rgba(0,0,0,0.3);
                border: 2px solid rgba(255,255,255,0.1);
                border-radius: 12px;
                cursor: pointer;
                text-align: center;
                color: #94a3b8;
                transition: all 0.2s;
            }
            .ball-style-option:hover {
                border-color: rgba(255,255,255,0.3);
            }
            .ball-style-option.selected {
                border-color: #3B82F6;
                background: rgba(59,130,246,0.2);
                color: white;
            }
            .ball-preview {
                display: flex;
                justify-content: center;
                gap: 6px;
                margin-bottom: 10px;
            }
            .ball-preview .ball {
                width: 24px;
                height: 24px;
                border-radius: 50%;
                box-shadow: inset -2px -2px 4px rgba(0,0,0,0.3), inset 2px 2px 4px rgba(255,255,255,0.2);
            }
            .ball.red { background: radial-gradient(circle at 30% 30%, #ff6b6b, #dc2626); }
            .ball.yellow { background: radial-gradient(circle at 30% 30%, #fde047, #eab308); }
            .ball.black { background: radial-gradient(circle at 30% 30%, #525252, #1a1a1a); }
            .ball.solid { background: radial-gradient(circle at 30% 30%, #3b82f6, #1d4ed8); }
            .ball.stripe { background: repeating-linear-gradient(90deg, #f97316 0px, #f97316 8px, white 8px, white 16px); border-radius: 50%; }
            .ball.blue-ball { background: radial-gradient(circle at 30% 30%, #60a5fa, #2563eb); }
            .ball.orange-ball { background: radial-gradient(circle at 30% 30%, #fb923c, #ea580c); }
            .sound-test-buttons {
                display: flex;
                gap: 10px;
                flex-wrap: wrap;
            }
            .sound-test-buttons button {
                padding: 10px 16px;
                background: rgba(255,255,255,0.1);
                border: 1px solid rgba(255,255,255,0.2);
                border-radius: 8px;
                color: white;
                cursor: pointer;
                font-size: 13px;
                transition: all 0.2s;
            }
            .sound-test-buttons button:hover {
                background: rgba(255,255,255,0.2);
            }
            .settings-footer {
                display: flex;
                justify-content: space-between;
                padding: 18px 25px;
                background: rgba(0,0,0,0.3);
                border-top: 1px solid rgba(255,255,255,0.1);
            }
            .btn-reset {
                padding: 12px 24px;
                background: rgba(239,68,68,0.2);
                border: 1px solid rgba(239,68,68,0.5);
                border-radius: 8px;
                color: #fca5a5;
                font-weight: bold;
                cursor: pointer;
                transition: all 0.2s;
            }
            .btn-reset:hover {
                background: rgba(239,68,68,0.3);
            }
            .btn-apply {
                padding: 12px 30px;
                background: linear-gradient(135deg, #10b981, #059669);
                border: none;
                border-radius: 8px;
                color: white;
                font-weight: bold;
                cursor: pointer;
                transition: all 0.2s;
            }
            .btn-apply:hover {
                transform: scale(1.02);
                box-shadow: 0 4px 15px rgba(16,185,129,0.4);
            }
        `;
        
        document.head.appendChild(style);
        document.body.appendChild(panel);
        
        this.attachEventListeners();
    },
    
    attachEventListeners() {
        // Tab switching
        document.querySelectorAll('.tab-btn').forEach(btn => {
            btn.addEventListener('click', () => {
                document.querySelectorAll('.tab-btn').forEach(b => b.classList.remove('active'));
                document.querySelectorAll('.tab-content').forEach(c => c.classList.remove('active'));
                btn.classList.add('active');
                document.getElementById('tab-' + btn.dataset.tab).classList.add('active');
            });
        });
        
        // Color buttons
        document.querySelectorAll('.color-options').forEach(container => {
            const setting = container.dataset.setting;
            container.querySelectorAll('.color-btn').forEach(btn => {
                btn.addEventListener('click', () => {
                    container.querySelectorAll('.color-btn').forEach(b => b.classList.remove('selected'));
                    btn.classList.add('selected');
                    this.settings[setting] = btn.dataset.color;
                    document.getElementById('setting-' + setting).value = btn.dataset.color;
                    this.applySettings();
                });
            });
        });
        
        // Color pickers
        ['clothColor', 'railColor'].forEach(setting => {
            const picker = document.getElementById('setting-' + setting);
            if (picker) {
                picker.addEventListener('input', () => {
                    this.settings[setting] = picker.value;
                    // Deselect preset buttons
                    picker.closest('.color-options').querySelectorAll('.color-btn').forEach(b => b.classList.remove('selected'));
                    this.applySettings();
                });
            }
        });
        
        // Ball style options
        document.querySelectorAll('.ball-style-option').forEach(option => {
            option.addEventListener('click', () => {
                document.querySelectorAll('.ball-style-option').forEach(o => o.classList.remove('selected'));
                option.classList.add('selected');
                this.settings.ballStyle = option.dataset.style;
                this.applySettings();
            });
        });
        
        // Range inputs
        document.querySelectorAll('.setting-row input[type="range"]').forEach(input => {
            const valueSpan = input.nextElementSibling;
            input.addEventListener('input', () => {
                if (valueSpan && valueSpan.classList.contains('range-value')) {
                    valueSpan.textContent = input.value + '%';
                }
                const settingName = input.id.replace('setting-', '');
                this.settings[settingName] = parseInt(input.value);
                this.applySettings();
            });
        });
        
        // Text inputs
        ['player1Name', 'player2Name'].forEach(setting => {
            const input = document.getElementById('setting-' + setting);
            if (input) {
                input.addEventListener('change', () => {
                    this.settings[setting] = input.value || setting.replace('player', 'Player ').replace('Name', '');
                    this.applySettings();
                });
            }
        });
        
        // Select inputs
        const shotModeSelect = document.getElementById('setting-shotControlMode');
        if (shotModeSelect) {
            shotModeSelect.addEventListener('change', () => {
                this.settings.shotControlMode = shotModeSelect.value;
                this.applySettings();
            });
        }
        
        // Match type select
        const matchTypeSelect = document.getElementById('setting-matchType');
        if (matchTypeSelect) {
            matchTypeSelect.addEventListener('change', () => {
                this.settings.matchType = matchTypeSelect.value;
                // Reset frame scores when match type changes
                this.settings.player1Frames = 0;
                this.settings.player2Frames = 0;
                this.updateFrameScoreDisplay();
                this.applySettings();
            });
        }
        
        // Checkbox inputs
        ['showAimLine', 'showTrajectory', 'showGhostBall', 'showSpinIndicator', 'showPowerMeter', 'soundEnabled', 'goldenBall', 'goldenDuck'].forEach(setting => {
            const checkbox = document.getElementById('setting-' + setting);
            if (checkbox) {
                checkbox.addEventListener('change', () => {
                    this.settings[setting] = checkbox.checked;
                    this.applySettings();
                });
            }
        });
    },
    
    toggle() {
        this.isVisible = !this.isVisible;
        const panel = document.getElementById('gameSettingsPanel');
        if (this.isVisible) {
            this.loadSettingsToUI();
            panel.classList.add('visible');
        } else {
            panel.classList.remove('visible');
        }
    },
    
    loadSettingsToUI() {
        // Player names
        document.getElementById('setting-player1Name').value = this.settings.player1Name;
        document.getElementById('setting-player2Name').value = this.settings.player2Name;
        
        // Match type
        const matchTypeSelect = document.getElementById('setting-matchType');
        if (matchTypeSelect) matchTypeSelect.value = this.settings.matchType;
        this.updateFrameScoreDisplay();
        
        // Shot control
        document.getElementById('setting-shotControlMode').value = this.settings.shotControlMode;
        document.getElementById('setting-fineTuneSensitivity').value = this.settings.fineTuneSensitivity;
        document.querySelector('#setting-fineTuneSensitivity + .range-value').textContent = this.settings.fineTuneSensitivity + '%';
        
        // Checkboxes
        ['showAimLine', 'showTrajectory', 'showGhostBall', 'showSpinIndicator', 'showPowerMeter', 'soundEnabled', 'goldenBall', 'goldenDuck'].forEach(setting => {
            const checkbox = document.getElementById('setting-' + setting);
            if (checkbox) checkbox.checked = this.settings[setting];
        });
        
        // Colors
        document.getElementById('setting-clothColor').value = this.settings.clothColor;
        document.getElementById('setting-railColor').value = this.settings.railColor;
        
        // Highlight selected color buttons
        ['clothColor', 'railColor'].forEach(setting => {
            const container = document.querySelector(`.color-options[data-setting="${setting}"]`);
            if (container) {
                container.querySelectorAll('.color-btn').forEach(btn => {
                    btn.classList.toggle('selected', btn.dataset.color === this.settings[setting]);
                });
            }
        });
        
        // Ball style
        document.querySelectorAll('.ball-style-option').forEach(option => {
            option.classList.toggle('selected', option.dataset.style === this.settings.ballStyle);
        });
        
        // Volume
        document.getElementById('setting-soundVolume').value = this.settings.soundVolume;
        document.querySelector('#setting-soundVolume + .range-value').textContent = this.settings.soundVolume + '%';
    },
    
    updateFrameScoreDisplay() {
        const p1Score = document.getElementById('p1FrameScore');
        const p2Score = document.getElementById('p2FrameScore');
        if (p1Score) p1Score.textContent = this.settings.player1Frames;
        if (p2Score) p2Score.textContent = this.settings.player2Frames;
    },
    
    applySettings() {
        if (!this.game) return;
        
        // Apply player names
        if (this.game.players) {
            this.game.players[0].name = this.settings.player1Name;
            this.game.players[1].name = this.settings.player2Name;
            if (typeof this.game.updateTurnDisplay === 'function') {
                this.game.updateTurnDisplay();
            }
        }
        
        // Apply shot control mode
        this.game.shotControlMode = this.settings.shotControlMode;
        
        // Apply fine-tune sensitivity
        if (typeof PoolInput !== 'undefined') {
            PoolInput.fineTuneSensitivity = this.settings.fineTuneSensitivity / 100;
        }
        
        // Apply visual aids
        this.game.showAimLine = this.settings.showAimLine;
        this.game.showTrajectoryPrediction = this.settings.showTrajectory;
        this.game.showGhostBalls = this.settings.showGhostBall;
        this.game.showSpinArrows = this.settings.showSpinIndicator;
        this.game.showPowerMeter = this.settings.showPowerMeter;
        
        
        // Apply table colors
        this.game.clothColor = this.settings.clothColor;
        this.game.railColor = this.settings.railColor;
        
        // Apply ball style
        this.game.ballStyle = this.settings.ballStyle;
        this.updateBallColors();
        
        // Apply audio settings
        if (typeof PoolAudio !== 'undefined') {
            PoolAudio.setEnabled(this.settings.soundEnabled);
            PoolAudio.setVolume(this.settings.soundVolume / 100);
        }
        
        // Apply break rules (Golden Ball / Golden Duck)
        this.game.goldenBall = this.settings.goldenBall;
        this.game.goldenDuck = this.settings.goldenDuck;
        
        // Apply match settings
        this.game.matchType = this.settings.matchType;
        this.game.player1Frames = this.settings.player1Frames;
        this.game.player2Frames = this.settings.player2Frames;
        
        this.saveSettings();
    },
    
    updateBallColors() {
        if (!this.game || !this.game.balls) return;
        
        // Ball color schemes
        const schemes = {
            uk: { group1: 'red', group2: 'yellow', group1Color: '#dc2626', group2Color: '#eab308' },
            us: { group1: 'solid', group2: 'stripe', group1Color: '#2563eb', group2Color: '#f97316' },
            classic: { group1: 'blue', group2: 'orange', group1Color: '#3b82f6', group2Color: '#f97316' }
        };
        
        const scheme = schemes[this.settings.ballStyle] || schemes.uk;
        this.game.ballColorScheme = scheme;
        
        console.log('Applying ball style:', this.settings.ballStyle, scheme);
        
        // Update existing balls' display colors (visual only)
        this.game.balls.forEach(ball => {
            // Group 1 balls (reds in UK, solids in US, blues in classic)
            if (ball.color === 'red') {
                ball.displayColor = scheme.group1Color;
                ball.groupName = scheme.group1;
                console.log('Ball', ball.num, 'set to group1 color:', scheme.group1Color);
            } 
            // Group 2 balls (yellows in UK, stripes in US, oranges in classic)
            else if (ball.color === 'yellow') {
                ball.displayColor = scheme.group2Color;
                ball.groupName = scheme.group2;
                console.log('Ball', ball.num, 'set to group2 color:', scheme.group2Color);
            }
        });
    },
    
    testSound(soundName) {
        if (typeof PoolAudio !== 'undefined') {
            PoolAudio.play(soundName, 0.8);
        }
    },
    
    resetToDefaults() {
        this.settings = {
            player1Name: 'Player 1',
            player2Name: 'Player 2',
            shotControlMode: 'drag',
            showAimLine: true,
            showTrajectory: true,
            showGhostBall: true,
            fineTuneSensitivity: 15,
            clothColor: '#1a7f37',
            railColor: '#8B4513',
            pocketColor: '#1a1a1a',
            ballStyle: 'uk',
            cueBallColor: '#f5f5f5',
            soundEnabled: true,
            soundVolume: 70,
            showSpinIndicator: true,
            showPowerMeter: true,
            goldenBall: false,
            goldenDuck: false,
            matchType: 'single',
            player1Frames: 0,
            player2Frames: 0
        };
        this.loadSettingsToUI();
        this.applySettings();
    },
    
    // Record a frame win and check for match win
    recordFrameWin(playerIndex) {
        if (playerIndex === 0) {
            this.settings.player1Frames++;
        } else {
            this.settings.player2Frames++;
        }
        this.updateFrameScoreDisplay();
        this.saveSettings();
        
        // Check for match win
        const framesToWin = this.getFramesToWin();
        if (this.settings.player1Frames >= framesToWin) {
            return { matchWon: true, winner: 0 };
        } else if (this.settings.player2Frames >= framesToWin) {
            return { matchWon: true, winner: 1 };
        }
        return { matchWon: false };
    },
    
    getFramesToWin() {
        switch (this.settings.matchType) {
            case 'best3': return 2;
            case 'best5': return 3;
            case 'best7': return 4;
            default: return 1;
        }
    },
    
    getMatchName() {
        switch (this.settings.matchType) {
            case 'best3': return 'Best of 3';
            case 'best5': return 'Best of 5';
            case 'best7': return 'Best of 7';
            default: return 'Single Frame';
        }
    },
    
    resetMatch() {
        this.settings.player1Frames = 0;
        this.settings.player2Frames = 0;
        this.updateFrameScoreDisplay();
        this.saveSettings();
    },
    
    applyAndClose() {
        this.applySettings();
        this.toggle();
    },
    
    saveSettings() {
        try {
            localStorage.setItem('poolGameSettings', JSON.stringify(this.settings));
        } catch (e) {
            console.warn('Could not save settings:', e);
        }
    },
    
    loadSettings() {
        try {
            const saved = localStorage.getItem('poolGameSettings');
            if (saved) {
                this.settings = { ...this.settings, ...JSON.parse(saved) };
            }
        } catch (e) {
            console.warn('Could not load settings:', e);
        }
    }
};
""";
    }
}
