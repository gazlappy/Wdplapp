namespace Wdpl2.Services;

/// <summary>
/// Developer settings module - provides in-game controls for adjusting all game parameters
/// </summary>
public static class PoolDevSettingsModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL DEVELOPER SETTINGS MODULE
// Real-time game parameter adjustment
// ============================================

const PoolDevSettings = {
    isVisible: false,
    game: null,
    
    init(game) {
        this.game = game;
        this.createSettingsPanel();
        this.attachEventListeners();
        
        // Toggle with F2 key
        document.addEventListener('keydown', (e) => {
            if (e.key === 'F2') {
                e.preventDefault();
                this.toggle();
            }
        });
        
        console.log('PoolDevSettings initialized - Press F2 to open');
    },
    
    createSettingsPanel() {
        const panel = document.createElement('div');
        panel.id = 'devSettingsPanel';
        panel.innerHTML = `
            <div class='dev-header'>
                <h3>Developer Settings</h3>
                <button id='devSettingsClose' class='dev-close-btn'>X</button>
            </div>
            <div class='dev-content'>
                <div class='dev-section'>
                    <h4>Table Dimensions</h4>
                    <div class='dev-control'>
                        <label>Table Width:</label>
                        <input type='range' id='tableWidth' min='800' max='1400' value='1000' step='50'>
                        <span id='tableWidthValue'>1000</span>
                    </div>
                    <div class='dev-control'>
                        <label>Table Height:</label>
                        <input type='range' id='tableHeight' min='400' max='800' value='500' step='50'>
                        <span id='tableHeightValue'>500</span>
                    </div>
                    <div class='dev-control'>
                        <label>Cushion Margin:</label>
                        <input type='range' id='cushionMargin' min='10' max='40' value='21' step='1'>
                        <span id='cushionMarginValue'>21</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Ball Sizes</h4>
                    <div class='dev-control'>
                        <label>Ball Radius:</label>
                        <input type='range' id='ballRadius' min='8' max='20' value='14' step='0.5'>
                        <span id='ballRadiusValue'>14</span>
                    </div>
                    <div class='dev-control'>
                        <label>Cue Ball Radius:</label>
                        <input type='range' id='cueBallRadius' min='8' max='20' value='13' step='0.5'>
                        <span id='cueBallRadiusValue'>13</span>
                    </div>
                    <div class='dev-control'>
                        <label>Ball Spacing:</label>
                        <input type='range' id='ballSpacing' min='0' max='5' value='0.5' step='0.1'>
                        <span id='ballSpacingValue'>0.5</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Pocket Sizes</h4>
                    <div class='dev-control'>
                        <label>Corner Opening:</label>
                        <input type='range' id='cornerPocketOpening' min='20' max='50' value='32' step='1'>
                        <span id='cornerPocketOpeningValue'>32</span>
                    </div>
                    <div class='dev-control'>
                        <label>Corner Capture Zone:</label>
                        <input type='range' id='cornerPocketRadius' min='15' max='45' value='28' step='1'>
                        <span id='cornerPocketRadiusValue'>28</span>
                    </div>
                    <div class='dev-control'>
                        <label>Middle Opening:</label>
                        <input type='range' id='middlePocketOpening' min='20' max='50' value='34' step='1'>
                        <span id='middlePocketOpeningValue'>34</span>
                    </div>
                    <div class='dev-control'>
                        <label>Middle Capture Zone:</label>
                        <input type='range' id='middlePocketRadius' min='15' max='45' value='30' step='1'>
                        <span id='middlePocketRadiusValue'>30</span>
                    </div>
                    <div class='dev-control'>
                        <label>Capture Threshold:</label>
                        <input type='range' id='captureThreshold' min='10' max='80' value='30' step='5'>
                        <span id='captureThresholdValue'>30%</span>
                    </div>
                    <div class='dev-control'>
                        <label>Pocket Depth Effect:</label>
                        <input type='range' id='pocketDepth' min='0.5' max='2.0' value='1.0' step='0.1'>
                        <span id='pocketDepthValue'>1.0</span>
                    </div>
                    <div class='dev-control'>
                        <label>Show Visual Zones:</label>
                        <input type='checkbox' id='showPocketZones' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Capture Zones:</label>
                        <input type='checkbox' id='showCaptureZones'>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Physics - Friction</h4>
                    <div class='dev-control'>
                        <label>Table Friction:</label>
                        <input type='range' id='friction' min='0.90' max='0.999' value='0.987' step='0.001'>
                        <span id='frictionValue'>0.987</span>
                    </div>
                    <div class='dev-control'>
                        <label>Rolling Resistance:</label>
                        <input type='range' id='rollingResistance' min='0.95' max='0.999' value='0.99' step='0.001'>
                        <span id='rollingResistanceValue'>0.99</span>
                    </div>
                    <div class='dev-control'>
                        <label>Spin Decay:</label>
                        <input type='range' id='spinDecay' min='0.90' max='0.999' value='0.98' step='0.001'>
                        <span id='spinDecayValue'>0.98</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Physics - Collisions</h4>
                    <div class='dev-control'>
                        <label>Cushion Bounce:</label>
                        <input type='range' id='cushionRestitution' min='0.5' max='0.95' value='0.78' step='0.01'>
                        <span id='cushionRestitutionValue'>0.78</span>
                    </div>
                    <div class='dev-control'>
                        <label>Ball Restitution:</label>
                        <input type='range' id='ballRestitution' min='0.85' max='1.0' value='0.95' step='0.01'>
                        <span id='ballRestitutionValue'>0.95</span>
                    </div>
                    <div class='dev-control'>
                        <label>Collision Damping:</label>
                        <input type='range' id='collisionDamping' min='0.85' max='1.0' value='0.98' step='0.01'>
                        <span id='collisionDampingValue'>0.98</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Shot Controls</h4>
                    <div class='dev-control'>
                        <label>Max Power:</label>
                        <input type='range' id='maxPower' min='20' max='150' value='40' step='5'>
                        <span id='maxPowerValue'>40</span>
                    </div>
                    <div class='dev-control'>
                        <label>Power Multiplier:</label>
                        <input type='range' id='powerMultiplier' min='0.5' max='2.0' value='1.0' step='0.1'>
                        <span id='powerMultiplierValue'>1.0</span>
                    </div>
                    <div class='dev-control'>
                        <label>Aim Sensitivity:</label>
                        <input type='range' id='aimSensitivity' min='0.5' max='2.0' value='1.0' step='0.1'>
                        <span id='aimSensitivityValue'>1.0</span>
                    </div>
                    <div class='dev-control'>
                        <label>Pull Distance:</label>
                        <input type='range' id='maxPullDistance' min='50' max='250' value='150' step='10'>
                        <span id='maxPullDistanceValue'>150</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Spin Controls</h4>
                    <div class='dev-control'>
                        <label>Max Spin:</label>
                        <input type='range' id='maxSpin' min='0.5' max='3.0' value='1.5' step='0.1'>
                        <span id='maxSpinValue'>1.5</span>
                    </div>
                    <div class='dev-control'>
                        <label>Spin Effect:</label>
                        <input type='range' id='spinEffect' min='0.1' max='1.0' value='0.5' step='0.05'>
                        <span id='spinEffectValue'>0.5</span>
                    </div>
                    <div class='dev-control'>
                        <label>English Transfer:</label>
                        <input type='range' id='englishTransfer' min='0.1' max='0.9' value='0.3' step='0.05'>
                        <span id='englishTransferValue'>0.3</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Visual Effects</h4>
                    <div class='dev-control'>
                        <label>Show Aim Line:</label>
                        <input type='checkbox' id='showAimLine' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Ghost Ball:</label>
                        <input type='checkbox' id='showGhostBall' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Trajectory:</label>
                        <input type='checkbox' id='showTrajectory'>
                    </div>
                    <div class='dev-control'>
                        <label>Show Velocities:</label>
                        <input type='checkbox' id='showVelocities'>
                    </div>
                    <div class='dev-control'>
                        <label>Show Ball Numbers:</label>
                        <input type='checkbox' id='showBallNumbers' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show FPS:</label>
                        <input type='checkbox' id='showFps'>
                    </div>
                    <div class='dev-control'>
                        <label>Ball Shadows:</label>
                        <input type='checkbox' id='ballShadows' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Table Texture:</label>
                        <input type='checkbox' id='tableTexture' checked>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Advanced Physics</h4>
                    <div class='dev-control'>
                        <label>Air Resistance:</label>
                        <input type='range' id='airResistance' min='0.990' max='1.0' value='0.999' step='0.001'>
                        <span id='airResistanceValue'>0.999</span>
                    </div>
                    <div class='dev-control'>
                        <label>Angular Damping:</label>
                        <input type='range' id='angularDamping' min='0.90' max='0.999' value='0.98' step='0.001'>
                        <span id='angularDampingValue'>0.98</span>
                    </div>
                    <div class='dev-control'>
                        <label>Minimum Speed:</label>
                        <input type='range' id='minSpeed' min='0.01' max='0.5' value='0.05' step='0.01'>
                        <span id='minSpeedValue'>0.05</span>
                    </div>
                    <div class='dev-control'>
                        <label>Gravity Effect:</label>
                        <input type='range' id='gravityEffect' min='0' max='2' value='1' step='0.1'>
                        <span id='gravityEffectValue'>1</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Audio Settings</h4>
                    <div class='dev-control'>
                        <label>Sound Effects:</label>
                        <input type='checkbox' id='soundEffects'>
                    </div>
                    <div class='dev-control'>
                        <label>Volume:</label>
                        <input type='range' id='volume' min='0' max='100' value='50' step='5'>
                        <span id='volumeValue'>50%</span>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Presets</h4>
                    <div class='dev-buttons'>
                        <button id='presetSupreme' class='dev-btn'>Supreme</button>
                        <button id='presetEasy' class='dev-btn'>Easy</button>
                        <button id='presetTight' class='dev-btn'>Tight</button>
                        <button id='presetRealistic' class='dev-btn'>Realistic</button>
                        <button id='presetArcade' class='dev-btn'>Arcade</button>
                        <button id='presetPro' class='dev-btn'>Pro</button>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Actions</h4>
                    <div class='dev-buttons'>
                        <button id='resetRack' class='dev-btn'>Reset Rack</button>
                        <button id='stopAllBalls' class='dev-btn'>Stop Balls</button>
                        <button id='testPockets' class='dev-btn'>Test Pockets</button>
                        <button id='randomShot' class='dev-btn'>Random Shot</button>
                        <button id='exportSettings' class='dev-btn'>Export</button>
                        <button id='resetDefaults' class='dev-btn'>Reset All</button>
                    </div>
                </div>
            </div>
        `;
        
        const style = document.createElement('style');
        style.textContent = `
            #devSettingsPanel {
                position: fixed;
                top: 50%;
                left: 50%;
                transform: translate(-50%, -50%);
                width: 500px;
                max-height: 85vh;
                background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%);
                border: 3px solid #3B82F6;
                border-radius: 12px;
                box-shadow: 0 20px 60px rgba(0,0,0,0.7);
                z-index: 10000;
                display: none;
                overflow: hidden;
                font-family: Arial, sans-serif;
            }
            #devSettingsPanel.visible { display: block; animation: slideIn 0.3s ease-out; }
            @keyframes slideIn {
                from { opacity: 0; transform: translate(-50%, -45%); }
                to { opacity: 1; transform: translate(-50%, -50%); }
            }
            .dev-header {
                background: rgba(0,0,0,0.3);
                padding: 15px 20px;
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-bottom: 2px solid #3B82F6;
            }
            .dev-header h3 { 
                margin: 0; 
                color: white; 
                font-size: 18px;
                font-weight: bold;
            }
            .dev-close-btn {
                background: #ef4444;
                border: none;
                color: white;
                font-size: 18px;
                font-weight: bold;
                width: 32px;
                height: 32px;
                border-radius: 50%;
                cursor: pointer;
                transition: all 0.2s;
                line-height: 32px;
            }
            .dev-close-btn:hover {
                background: #dc2626;
                transform: rotate(90deg);
            }
            .dev-content {
                padding: 15px;
                max-height: calc(85vh - 64px);
                overflow-y: auto;
                color: white;
            }
            .dev-content::-webkit-scrollbar { width: 8px; }
            .dev-content::-webkit-scrollbar-track { 
                background: rgba(0,0,0,0.2); 
                border-radius: 4px; 
            }
            .dev-content::-webkit-scrollbar-thumb { 
                background: #3B82F6; 
                border-radius: 4px; 
            }
            .dev-content::-webkit-scrollbar-thumb:hover { background: #2563EB; }
            .dev-section {
                background: rgba(255,255,255,0.1);
                border-radius: 8px;
                padding: 12px;
                margin-bottom: 10px;
                border: 1px solid rgba(255,255,255,0.1);
            }
            .dev-section h4 {
                margin: 0 0 10px 0;
                color: #4ade80;
                font-size: 13px;
                font-weight: bold;
                text-transform: uppercase;
                letter-spacing: 0.5px;
                border-bottom: 1px solid rgba(74,222,128,0.3);
                padding-bottom: 5px;
            }
            .dev-control {
                display: flex;
                align-items: center;
                margin-bottom: 6px;
                gap: 8px;
            }
            .dev-control label {
                flex: 0 0 140px;
                font-size: 11px;
                font-weight: 500;
            }
            .dev-control input[type='range'] {
                flex: 1;
                height: 5px;
                border-radius: 3px;
                background: rgba(255,255,255,0.2);
                outline: none;
                cursor: pointer;
            }
            .dev-control input[type='range']::-webkit-slider-thumb {
                -webkit-appearance: none;
                width: 14px;
                height: 14px;
                border-radius: 50%;
                background: #4ade80;
                cursor: pointer;
                border: 2px solid white;
                box-shadow: 0 2px 4px rgba(0,0,0,0.3);
            }
            .dev-control input[type='range']::-moz-range-thumb {
                width: 14px;
                height: 14px;
                border-radius: 50%;
                background: #4ade80;
                cursor: pointer;
                border: 2px solid white;
            }
            .dev-control input[type='checkbox'] {
                width: 18px;
                height: 18px;
                cursor: pointer;
                accent-color: #4ade80;
            }
            .dev-control span {
                flex: 0 0 50px;
                text-align: right;
                font-weight: bold;
                color: #fbbf24;
                font-size: 11px;
            }
            .dev-buttons {
                display: grid;
                grid-template-columns: repeat(3, 1fr);
                gap: 6px;
            }
            .dev-btn {
                padding: 8px 4px;
                border: none;
                border-radius: 6px;
                cursor: pointer;
                font-weight: bold;
                font-size: 11px;
                background: linear-gradient(135deg, #10b981 0%, #059669 100%);
                color: white;
                transition: all 0.2s;
                box-shadow: 0 2px 4px rgba(0,0,0,0.2);
            }
            .dev-btn:hover { 
                opacity: 0.9; 
                transform: translateY(-1px);
                box-shadow: 0 4px 8px rgba(0,0,0,0.3);
            }
            .dev-btn:active { 
                transform: translateY(0);
            }
        `;
        
        document.head.appendChild(style);
        document.body.appendChild(panel);
    },
    
    attachEventListeners() {
        const self = this;
        
        document.getElementById('devSettingsClose').addEventListener('click', () => self.toggle());
        
        // Table Dimensions
        this.addRangeListener('tableWidth', (val) => {
            self.game.width = parseFloat(val);
            self.game.canvas.width = self.game.width;
            self.game.repositionPockets();
        });
        
        this.addRangeListener('tableHeight', (val) => {
            self.game.height = parseFloat(val);
            self.game.canvas.height = self.game.height;
            self.game.repositionPockets();
        });
        
        this.addRangeListener('cushionMargin', (val) => {
            self.game.cushionMargin = parseFloat(val);
            self.game.repositionPockets();
        });
        
        // Ball Sizes
        this.addRangeListener('ballRadius', (val) => {
            const r = parseFloat(val);
            self.game.standardBallRadius = r;
            self.game.balls.forEach(b => { if (b.num !== 0) b.r = r; });
        });
        
        this.addRangeListener('cueBallRadius', (val) => {
            const r = parseFloat(val);
            self.game.cueBallRadius = r;
            if (self.game.cueBall) self.game.cueBall.r = r;
        });
        
        this.addRangeListener('ballSpacing', (val) => {
            self.game.ballSpacing = parseFloat(val);
        });
        
        // Pocket Sizes
        this.addRangeListener('cornerPocketOpening', (val) => {
            self.game.cornerPocketOpening = parseFloat(val);
            self.game.repositionPockets();
        });
        
        this.addRangeListener('cornerPocketRadius', (val) => {
            self.game.cornerPocketRadius = parseFloat(val);
            self.game.repositionPockets();
        });
        
        this.addRangeListener('middlePocketOpening', (val) => {
            self.game.middlePocketOpening = parseFloat(val);
            self.game.repositionPockets();
        });
        
        this.addRangeListener('middlePocketRadius', (val) => {
            self.game.middlePocketRadius = parseFloat(val);
            self.game.repositionPockets();
        });
        
        this.addRangeListener('captureThreshold', (val) => {
            self.game.captureThresholdPercent = parseFloat(val) / 100;
        }, (val) => val + '%');
        
        this.addRangeListener('pocketDepth', (val) => {
            self.game.pocketDepth = parseFloat(val);
        });
        
        this.addCheckboxListener('showPocketZones', (checked) => {
            self.game.showPocketZones = checked;
        });
        
        this.addCheckboxListener('showCaptureZones', (checked) => {
            self.game.showCaptureZones = checked;
        });
        
        // Physics - Friction
        this.addRangeListener('friction', (val) => {
            self.game.friction = parseFloat(val);
        });
        
        this.addRangeListener('rollingResistance', (val) => {
            self.game.rollingResistance = parseFloat(val);
        });
        
        this.addRangeListener('spinDecay', (val) => {
            self.game.spinDecay = parseFloat(val);
        });
        
        // Physics - Collisions
        this.addRangeListener('cushionRestitution', (val) => {
            self.game.cushionRestitution = parseFloat(val);
        });
        
        this.addRangeListener('ballRestitution', (val) => {
            self.game.ballRestitution = parseFloat(val);
        });
        
        this.addRangeListener('collisionDamping', (val) => {
            self.game.collisionDamping = parseFloat(val);
        });
        
        // Shot Controls
        this.addRangeListener('maxPower', (val) => {
            self.game.maxPower = parseFloat(val);
        });
        
        this.addRangeListener('powerMultiplier', (val) => {
            self.game.powerMultiplier = parseFloat(val);
        });
        
        this.addRangeListener('aimSensitivity', (val) => {
            self.game.aimSensitivity = parseFloat(val);
        });
        
        this.addRangeListener('maxPullDistance', (val) => {
            self.game.maxPullDistance = parseFloat(val);
        });
        
        // Spin Controls
        this.addRangeListener('maxSpin', (val) => {
            self.game.maxSpin = parseFloat(val);
        });
        
        this.addRangeListener('spinEffect', (val) => {
            self.game.spinEffect = parseFloat(val);
        });
        
        this.addRangeListener('englishTransfer', (val) => {
            self.game.englishTransfer = parseFloat(val);
        });
        
        // Visual Effects
        this.addCheckboxListener('showAimLine', (checked) => {
            self.game.showAimLine = checked;
        });
        
        this.addCheckboxListener('showGhostBall', (checked) => {
            self.game.showGhostBall = checked;
        });
        
        this.addCheckboxListener('showTrajectory', (checked) => {
            self.game.showTrajectory = checked;
        });
        
        this.addCheckboxListener('showVelocities', (checked) => {
            self.game.showVelocities = checked;
        });
        
        this.addCheckboxListener('showBallNumbers', (checked) => {
            self.game.showBallNumbers = checked;
        });
        
        this.addCheckboxListener('showFps', (checked) => {
            self.game.showFps = checked;
        });
        
        this.addCheckboxListener('ballShadows', (checked) => {
            self.game.ballShadows = checked;
        });
        
        this.addCheckboxListener('tableTexture', (checked) => {
            self.game.tableTexture = checked;
        });
        
        // Advanced Physics
        this.addRangeListener('airResistance', (val) => {
            self.game.airResistance = parseFloat(val);
        });
        
        this.addRangeListener('angularDamping', (val) => {
            self.game.angularDamping = parseFloat(val);
        });
        
        this.addRangeListener('minSpeed', (val) => {
            self.game.minSpeed = parseFloat(val);
        });
        
        this.addRangeListener('gravityEffect', (val) => {
            self.game.gravityEffect = parseFloat(val);
        });
        
        // Audio Settings
        this.addCheckboxListener('soundEffects', (checked) => {
            self.game.soundEffects = checked;
        });
        
        this.addRangeListener('volume', (val) => {
            self.game.volume = parseFloat(val) / 100;
        }, (val) => val + '%');
        
        // Preset buttons
        document.getElementById('presetSupreme').addEventListener('click', () => self.applyPreset('supreme'));
        document.getElementById('presetEasy').addEventListener('click', () => self.applyPreset('easy'));
        document.getElementById('presetTight').addEventListener('click', () => self.applyPreset('tight'));
        document.getElementById('presetRealistic').addEventListener('click', () => self.applyPreset('realistic'));
        document.getElementById('presetArcade').addEventListener('click', () => self.applyPreset('arcade'));
        document.getElementById('presetPro').addEventListener('click', () => self.applyPreset('pro'));
        
        // Action buttons
        document.getElementById('resetRack').addEventListener('click', () => self.game.resetRack());
        document.getElementById('stopAllBalls').addEventListener('click', () => self.game.stopBalls());
        document.getElementById('testPockets').addEventListener('click', () => self.testPockets());
        document.getElementById('randomShot').addEventListener('click', () => self.randomShot());
        document.getElementById('exportSettings').addEventListener('click', () => self.exportSettings());
        document.getElementById('resetDefaults').addEventListener('click', () => self.resetDefaults());
    },
    
    addCheckboxListener(id, callback) {
        const checkbox = document.getElementById(id);
        if (!checkbox) return;
        
        checkbox.addEventListener('change', (e) => {
            callback(e.target.checked);
        });
    },
    
    addRangeListener(id, callback, formatValue) {
        const input = document.getElementById(id);
        const valueSpan = document.getElementById(id + 'Value');
        if (!input || !valueSpan) return;
        
        input.addEventListener('input', (e) => {
            const val = e.target.value;
            valueSpan.textContent = formatValue ? formatValue(val) : val;
            callback(val);
        });
    },
    
    toggle() {
        this.isVisible = !this.isVisible;
        const panel = document.getElementById('devSettingsPanel');
        if (panel) {
            panel.classList.toggle('visible', this.isVisible);
        }
    },
    
    applyPreset(name) {
        const presets = {
            supreme: {
                cornerPocketOpening: 32, cornerPocketRadius: 28, middlePocketOpening: 34, middlePocketRadius: 30, 
                ballRadius: 14, captureThreshold: 30, pocketDepth: 1.0,
                friction: 0.987, cushionRestitution: 0.78, collisionDamping: 0.98,
                maxPower: 40, powerMultiplier: 1.0
            },
            easy: {
                cornerPocketOpening: 44, cornerPocketRadius: 40, middlePocketOpening: 46, middlePocketRadius: 42, 
                ballRadius: 12, captureThreshold: 20, pocketDepth: 1.2,
                friction: 0.985, cushionRestitution: 0.80, collisionDamping: 0.99,
                maxPower: 60, powerMultiplier: 1.2
            },
            tight: {
                cornerPocketOpening: 26, cornerPocketRadius: 22, middlePocketOpening: 28, middlePocketRadius: 24, 
                ballRadius: 14, captureThreshold: 50, pocketDepth: 0.8,
                friction: 0.990, cushionRestitution: 0.75, collisionDamping: 0.96,
                maxPower: 35, powerMultiplier: 0.9
            },
            realistic: {
                cornerPocketOpening: 31, cornerPocketRadius: 27, middlePocketOpening: 33, middlePocketRadius: 29, 
                ballRadius: 13.89, captureThreshold: 40, pocketDepth: 1.0,
                friction: 0.989, cushionRestitution: 0.77, collisionDamping: 0.97,
                maxPower: 45, powerMultiplier: 1.0, rollingResistance: 0.995, spinDecay: 0.985
            },
            arcade: {
                cornerPocketOpening: 48, cornerPocketRadius: 45, middlePocketOpening: 50, middlePocketRadius: 47, 
                ballRadius: 15, captureThreshold: 15, pocketDepth: 1.5,
                friction: 0.98, cushionRestitution: 0.85, collisionDamping: 0.99,
                maxPower: 100, powerMultiplier: 1.5, maxSpin: 2.5
            },
            pro: {
                cornerPocketOpening: 29, cornerPocketRadius: 25, middlePocketOpening: 31, middlePocketRadius: 27, 
                ballRadius: 13.5, captureThreshold: 45, pocketDepth: 0.9,
                friction: 0.992, cushionRestitution: 0.76, collisionDamping: 0.96,
                maxPower: 38, powerMultiplier: 0.95, rollingResistance: 0.997, spinDecay: 0.98
            }
        };
        
        const preset = presets[name];
        if (!preset) return;
        
        Object.keys(preset).forEach(key => {
            const input = document.getElementById(key);
            if (input) {
                input.value = preset[key];
                input.dispatchEvent(new Event('input'));
            }
        });
        
        console.log('Applied preset:', name);
    },
    
    testPockets() {
        this.game.stopBalls();
        this.game.balls = this.game.balls.filter(b => b.num === 0);
        
        this.game.pockets.forEach((pocket, index) => {
            const angle = (index * Math.PI * 2) / this.game.pockets.length;
            const dist = 40;
            this.game.balls.push({
                x: pocket.x + Math.cos(angle) * dist,
                y: pocket.y + Math.sin(angle) * dist,
                vx: 0, vy: 0,
                r: this.game.standardBallRadius,
                color: index % 2 === 0 ? 'red' : 'yellow',
                num: index + 1,
                rotation: 0,
                rotationAxisX: 0,
                rotationAxisY: 1
            });
        });
        
        console.log('Test balls placed near pockets');
    },
    
    randomShot() {
        if (!this.game.cueBall || this.game.cueBall.potted) return;
        
        const angle = Math.random() * Math.PI * 2;
        const power = Math.random() * this.game.maxPower;
        
        this.game.cueBall.vx = Math.cos(angle) * power;
        this.game.cueBall.vy = Math.sin(angle) * power;
        
        console.log('Random shot fired: angle=' + angle.toFixed(2) + ', power=' + power.toFixed(2));
    },
    
    exportSettings() {
        const settings = {
            table: {
                width: this.game.width,
                height: this.game.height,
                cushionMargin: this.game.cushionMargin
            },
            balls: {
                standardRadius: this.game.standardBallRadius,
                cueRadius: this.game.cueBallRadius,
                spacing: this.game.ballSpacing || 0.5
            },
            pockets: {
                cornerRadius: this.game.cornerPocketRadius,
                cornerOpening: this.game.cornerPocketOpening || 32,
                middleRadius: this.game.middlePocketRadius,
                middleOpening: this.game.middlePocketOpening || 34,
                captureThreshold: this.game.captureThresholdPercent,
                pocketDepth: this.game.pocketDepth || 1.0
            },
            physics: {
                friction: this.game.friction,
                rollingResistance: this.game.rollingResistance || 0.99,
                spinDecay: this.game.spinDecay || 0.98,
                cushionRestitution: this.game.cushionRestitution,
                ballRestitution: this.game.ballRestitution || 0.95,
                collisionDamping: this.game.collisionDamping,
                airResistance: this.game.airResistance || 0.999,
                angularDamping: this.game.angularDamping || 0.98,
                minSpeed: this.game.minSpeed || 0.05,
                gravityEffect: this.game.gravityEffect || 1
            },
            controls: {
                maxPower: this.game.maxPower,
                powerMultiplier: this.game.powerMultiplier || 1.0,
                aimSensitivity: this.game.aimSensitivity || 1.0,
                maxPullDistance: this.game.maxPullDistance || 150
            },
            spin: {
                maxSpin: this.game.maxSpin || 1.5,
                spinEffect: this.game.spinEffect || 0.5,
                englishTransfer: this.game.englishTransfer || 0.3
            },
            visual: {
                showPocketZones: this.game.showPocketZones,
                showAimLine: this.game.showAimLine !== false,
                showGhostBall: this.game.showGhostBall !== false,
                showTrajectory: this.game.showTrajectory || false,
                showVelocities: this.game.showVelocities || false,
                showBallNumbers: this.game.showBallNumbers !== false,
                showFps: this.game.showFps || false,
                ballShadows: this.game.ballShadows !== false,
                tableTexture: this.game.tableTexture !== false
            }
        };
        
        const json = JSON.stringify(settings, null, 2);
        
        if (navigator.clipboard) {
            navigator.clipboard.writeText(json).then(() => {
                alert('Settings exported to clipboard!');
                console.log('Exported settings:', json);
            }).catch(err => {
                console.error('Failed to copy:', err);
                this.fallbackCopy(json);
            });
        } else {
            this.fallbackCopy(json);
        }
    },
    
    fallbackCopy(text) {
        const textarea = document.createElement('textarea');
        textarea.value = text;
        textarea.style.position = 'fixed';
        textarea.style.opacity = '0';
        document.body.appendChild(textarea);
        textarea.select();
        try {
            document.execCommand('copy');
            alert('Settings exported!');
        } catch (err) {
            console.error('Fallback copy failed:', err);
            alert('Export failed. Check console for settings.');
            console.log(text);
        }
        document.body.removeChild(textarea);
    },
    
    resetDefaults() {
        if (!confirm('Reset all settings to defaults?')) return;
        
        const defaults = {
            tableWidth: 1000, tableHeight: 500, cushionMargin: 21,
            ballRadius: 14, cueBallRadius: 13, ballSpacing: 0.5,
            cornerPocketOpening: 32, cornerPocketRadius: 28, middlePocketOpening: 34, middlePocketRadius: 30, 
            captureThreshold: 30, pocketDepth: 1.0,
            friction: 0.987, rollingResistance: 0.99, spinDecay: 0.98,
            cushionRestitution: 0.78, ballRestitution: 0.95, collisionDamping: 0.98,
            maxPower: 40, powerMultiplier: 1.0, aimSensitivity: 1.0, maxPullDistance: 150,
            maxSpin: 1.5, spinEffect: 0.5, englishTransfer: 0.3,
            airResistance: 0.999, angularDamping: 0.98, minSpeed: 0.05, gravityEffect: 1,
            volume: 50
        };
        
        Object.keys(defaults).forEach(key => {
            const input = document.getElementById(key);
            if (input) {
                input.value = defaults[key];
                input.dispatchEvent(new Event('input'));
            }
        });
        
        // Reset checkboxes
        const checkboxDefaults = {
            showPocketZones: true, showCaptureZones: false, showAimLine: true, showGhostBall: true,
            showBallNumbers: true, ballShadows: true, tableTexture: true,
            showTrajectory: false, showVelocities: false, showFps: false, soundEffects: false
        };
        
        Object.keys(checkboxDefaults).forEach(key => {
            const checkbox = document.getElementById(key);
            if (checkbox) {
                checkbox.checked = checkboxDefaults[key];
                checkbox.dispatchEvent(new Event('change'));
            }
        });
        
        console.log('Reset to defaults');
    }
};

console.log('PoolDevSettings module loaded');
";
    }
}
