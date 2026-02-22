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
                <h3>Developer Settings<span class='dev-drag-hint'>(drag to move)</span></h3>
                <button id='devSettingsClose' class='dev-close-btn'>×</button>
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
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Pocket Openings (Rail Gaps)</div>
                        <div class='dev-control'>
                            <label>Corner Opening:</label>
                            <input type='range' id='cornerPocketOpeningMult' min='1.0' max='2.5' value='1.6' step='0.1'>
                            <span id='cornerPocketOpeningMultValue'>1.6x</span>
                        </div>
                        <div class='dev-control'>
                            <label>Side Opening:</label>
                            <input type='range' id='sidePocketOpeningMult' min='1.0' max='2.0' value='1.3' step='0.1'>
                            <span id='sidePocketOpeningMultValue'>1.3x</span>
                        </div>
                        <div class='dev-hint'>Controls how wide the gap is in the rails where balls enter</div>
                    </div>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Capture Zones (Ball Potting)</div>
                        <div class='dev-control'>
                            <label>Corner Capture:</label>
                            <input type='range' id='cornerPocketRadius' min='15' max='45' value='28' step='1'>
                            <span id='cornerPocketRadiusValue'>28</span>
                        </div>
                        <div class='dev-control'>
                            <label>Side Capture:</label>
                            <input type='range' id='middlePocketRadius' min='15' max='45' value='30' step='1'>
                            <span id='middlePocketRadiusValue'>30</span>
                        </div>
                        <div class='dev-control'>
                            <label>Capture Threshold:</label>
                            <input type='range' id='captureThreshold' min='10' max='80' value='30' step='5'>
                            <span id='captureThresholdValue'>30%</span>
                        </div>
                        <div class='dev-hint'>Controls when balls are considered potted</div>
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
                    <h4>WPA 2026 Standards</h4>
                    <div class='dev-control'>
                        <label>Ball-Ball Friction:</label>
                        <input type='range' id='ballToBallFriction' min='0.03' max='0.08' value='0.055' step='0.005'>
                        <span id='ballToBallFrictionValue'>0.055</span>
                    </div>
                    <div class='dev-control'>
                        <label>Ball-Cloth Sliding:</label>
                        <input type='range' id='ballToClothSliding' min='0.15' max='0.40' value='0.25' step='0.01'>
                        <span id='ballToClothSlidingValue'>0.25</span>
                    </div>
                    <div class='dev-control'>
                        <label>Rolling Resistance:</label>
                        <input type='range' id='rollingResistanceCoeff' min='0.005' max='0.015' value='0.010' step='0.001'>
                        <span id='rollingResistanceCoeffValue'>0.010</span>
                    </div>
                    <div class='dev-control'>
                        <label>Spin Decay Rate:</label>
                        <input type='range' id='spinDecayRateCoeff' min='5' max='15' value='10' step='1'>
                        <span id='spinDecayRateCoeffValue'>10</span>
                    </div>
                    <div class='dev-control'>
                        <label>Miscue Limit:</label>
                        <input type='range' id='miscueLimit' min='0.3' max='0.7' value='0.5' step='0.05'>
                        <span id='miscueLimitValue'>0.5</span>
                    </div>
                    <div class='dev-control'>
                        <label>Max Spin RPM:</label>
                        <input type='range' id='maxSpinRpm' min='3000' max='5000' value='4000' step='100'>
                        <span id='maxSpinRpmValue'>4000</span>
                    </div>
                    <div class='dev-control'>
                        <label>Cue Ball Mass Var:</label>
                        <input type='range' id='cueBallMassVariance' min='1.0' max='1.10' value='1.05' step='0.01'>
                        <span id='cueBallMassVarianceValue'>1.05</span>
                    </div>
                    <div class='dev-control'>
                        <label>Show WPA Info:</label>
                        <input type='checkbox' id='showWpaInfo' checked>
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
                        <label>Shot Control Mode:</label>
                        <select id='shotControlMode' style='flex:1;padding:4px;background:rgba(255,255,255,0.2);color:white;border:1px solid rgba(255,255,255,0.3);border-radius:4px;'>
                            <option value='drag'>Drag & Release (Default)</option>
                            <option value='click'>Click Power</option>
                            <option value='slider'>Power Slider</option>
                            <option value='tap'>Tap & Hold</option>
                            <option value='swipe'>Swipe</option>
                        </select>
                    </div>
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
                    <div class='dev-control'>
                        <label>Auto-Aim Assist:</label>
                        <input type='checkbox' id='autoAimAssist'>
                    </div>
                    <div class='dev-control'>
                        <label>Show Shot Preview:</label>
                        <input type='checkbox' id='showShotPreview' checked>
                    </div>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Fine-Tune Aiming (Hold . key)</div>
                        <div class='dev-control'>
                            <label>Fine-Tune Sensitivity:</label>
                            <input type='range' id='fineTuneSensitivity' min='5' max='50' value='15' step='5'>
                            <span id='fineTuneSensitivityValue'>15%</span>
                        </div>
                        <div class='dev-control'>
                            <label>Micro-Adjust Step:</label>
                            <input type='range' id='microAdjustStep' min='0.001' max='0.01' value='0.002' step='0.001'>
                            <span id='microAdjustStepValue'>0.002</span>
                        </div>
                        <div class='dev-hint'>Hold period (.) for fine aim, use ? ? for micro-adjustments</div>
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
                        <input type='range' id='spinEffect' min='0.1' max='2.0' value='1.0' step='0.1'>
                        <span id='spinEffectValue'>1.0</span>
                    </div>
                    <div class='dev-control'>
                        <label>English Transfer:</label>
                        <input type='range' id='englishTransfer' min='0.1' max='0.9' value='0.5' step='0.05'>
                        <span id='englishTransferValue'>0.5</span>
                    </div>
                    <div class='dev-control'>
                        <label>Spin Decay Rate:</label>
                        <input type='range' id='spinDecayRate' min='0.95' max='0.999' value='0.98' step='0.001'>
                        <span id='spinDecayRateValue'>0.98</span>
                    </div>
                    <div class='dev-control'>
                        <label>Sweet Spot Tolerance:</label>
                        <input type='range' id='sweetSpotTolerance' min='0.05' max='0.25' value='0.14' step='0.01'>
                        <span id='sweetSpotToleranceValue'>0.14</span>
                    </div>
                    <div class='dev-control'>
                        <label>Cue Tip Mass:</label>
                        <input type='range' id='cueTipMass' min='0.05' max='0.30' value='0.15' step='0.01'>
                        <span id='cueTipMassValue'>0.15</span>
                    </div>
                    <div class='dev-control'>
                        <label>Squirt Factor:</label>
                        <input type='range' id='squirtFactor' min='0.0' max='2.0' value='1.0' step='0.1'>
                        <span id='squirtFactorValue'>1.0</span>
                    </div>
                    <div class='dev-control'>
                        <label>Throw FIT Factor:</label>
                        <input type='range' id='throwFitFactor' min='0.0' max='1.0' value='0.6' step='0.05'>
                        <span id='throwFitFactorValue'>0.6</span>
                    </div>
                    <div class='dev-control'>
                        <label>Throw SIT Factor:</label>
                        <input type='range' id='throwSitFactor' min='0.0' max='0.3' value='0.15' step='0.01'>
                        <span id='throwSitFactorValue'>0.15</span>
                    </div>
                    <div class='dev-control'>
                        <label>Rail Grab Intensity:</label>
                        <input type='range' id='railGrabIntensity' min='0.0' max='2.0' value='1.0' step='0.1'>
                        <span id='railGrabIntensityValue'>1.0</span>
                    </div>
                    <div class='dev-control'>
                        <label>Show Spin Arrows:</label>
                        <input type='checkbox' id='showSpinArrows' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Sweet Spot Info:</label>
                        <input type='checkbox' id='showSweetSpot' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Squirt Deflection:</label>
                        <input type='checkbox' id='showSquirt' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Throw Effects:</label>
                        <input type='checkbox' id='showThrow' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Rail Grab:</label>
                        <input type='checkbox' id='showRailGrab' checked>
                    </div>
                </div>
                
                <div class='dev-section'>
                    <h4>Game Rules</h4>
                    <div class='dev-control'>
                        <label>Ball in Hand Touch Foul:</label>
                        <input type='checkbox' id='ballInHandTouchFoul' checked>
                    </div>
                    <div class='dev-hint'>When enabled, touching a ball while placing the cue ball is a foul</div>
                    <div class='dev-control' style='margin-top:10px;padding:8px;background:rgba(239,68,68,0.15);border:1px solid rgba(239,68,68,0.4);border-radius:6px;'>
                        <label style='color:#fca5a5;'>Disable Game Settings:</label>
                        <input type='checkbox' id='devOverrideGameSettings'>
                    </div>
                    <div class='dev-hint' style='color:#fca5a5;'>When enabled, the player Settings panel is locked and dev settings take priority</div>
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
                        <input type='checkbox' id='showTrajectory' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Show Collision Points:</label>
                        <input type='checkbox' id='showCollisionPoints' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Trajectory Length:</label>
                        <input type='range' id='trajectoryLength' min='50' max='500' value='200' step='25'>
                        <span id='trajectoryLengthValue'>200</span>
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
                    <h4>VFX Module</h4>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Table Effects</div>
                        <div class='dev-control'>
                            <label>Felt Noise Texture:</label>
                            <input type='checkbox' id='vfxFeltNoise' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Felt Noise Intensity:</label>
                            <input type='range' id='vfxFeltNoiseAlpha' min='0.02' max='0.30' value='0.12' step='0.02'>
                            <span id='vfxFeltNoiseAlphaValue'>0.12</span>
                        </div>
                        <div class='dev-control'>
                            <label>Cushion Shadows:</label>
                            <input type='checkbox' id='vfxCushionShadows' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Shadow Depth:</label>
                            <input type='range' id='vfxCushionShadowDepth' min='5' max='35' value='18' step='1'>
                            <span id='vfxCushionShadowDepthValue'>18</span>
                        </div>
                        <div class='dev-control'>
                            <label>Shadow Alpha:</label>
                            <input type='range' id='vfxCushionShadowAlpha' min='0.05' max='0.50' value='0.22' step='0.02'>
                            <span id='vfxCushionShadowAlphaValue'>0.22</span>
                        </div>
                        <div class='dev-control'>
                            <label>Wood Grain Rails:</label>
                            <input type='checkbox' id='vfxWoodGrain' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Table Edge Bevel:</label>
                            <input type='checkbox' id='vfxTableBevel' checked>
                        </div>
                    </div>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Pocket Effects</div>
                        <div class='dev-control'>
                            <label>Pocket Nets:</label>
                            <input type='checkbox' id='vfxPocketNets' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Pocket Stitching:</label>
                            <input type='checkbox' id='vfxPocketStitching' checked>
                        </div>
                    </div>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Ball Effects</div>
                        <div class='dev-control'>
                            <label>Dynamic Shadows:</label>
                            <input type='checkbox' id='vfxDynamicShadows' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Overhead Light Refl:</label>
                            <input type='checkbox' id='vfxOverheadReflection' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Environment Refl:</label>
                            <input type='checkbox' id='vfxEnvironmentReflection' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Settle Micro-bounce:</label>
                            <input type='checkbox' id='vfxBallSettle' checked>
                        </div>
                    </div>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Particle Effects</div>
                        <div class='dev-control'>
                            <label>Chalk Dust:</label>
                            <input type='checkbox' id='vfxChalkDust' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Collision Flashes:</label>
                            <input type='checkbox' id='vfxCollisionFlash' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Cushion Compression:</label>
                            <input type='checkbox' id='vfxCushionCompression' checked>
                        </div>
                    </div>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Cue & Lighting</div>
                        <div class='dev-control'>
                            <label>Cue Stick Inlays:</label>
                            <input type='checkbox' id='vfxCueInlays' checked>
                        </div>
                        <div class='dev-control'>
                            <label>Light Temperature:</label>
                            <select id='vfxLightTemperature' style='flex:1;padding:4px;background:rgba(255,255,255,0.2);color:white;border:1px solid rgba(255,255,255,0.3);border-radius:4px;'>
                                <option value='warm'>Warm (Tungsten)</option>
                                <option value='neutral'>Neutral (White)</option>
                                <option value='cool'>Cool (Fluorescent)</option>
                            </select>
                        </div>
                    </div>
                    <div class='dev-buttons'>
                        <button id='vfxAllOn' class='dev-btn'>All VFX On</button>
                        <button id='vfxAllOff' class='dev-btn'>All VFX Off</button>
                        <button id='vfxDefaults' class='dev-btn'>VFX Defaults</button>
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
                    <div class='dev-buttons' style='margin-top:8px;'>
                        <button id='ballInspectorBtn' class='dev-btn' style='background:linear-gradient(135deg, #8b5cf6 0%, #6d28d9 100%);grid-column:span 3;'>&#127913; Ball Inspector (F4)</button>
                    </div>
                    <div class='dev-buttons' style='margin-top:8px;'>
                        <button id='saveDefaults' class='dev-btn' style='background:linear-gradient(135deg, #3b82f6 0%, #1d4ed8 100%);grid-column:span 2;'>Save as Defaults</button>
                        <button id='clearDefaults' class='dev-btn' style='background:linear-gradient(135deg, #ef4444 0%, #b91c1c 100%);'>Clear Saved</button>
                    </div>
                </div>
            </div>
        `;
        
        const style = document.createElement('style');
        style.textContent = `
            #devSettingsPanel {
                position: fixed;
                top: 50px;
                left: 50px;
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
                resize: both;
                min-width: 400px;
                min-height: 300px;
            }
            #devSettingsPanel.visible { display: block; animation: slideIn 0.3s ease-out; }
            @keyframes slideIn {
                from { opacity: 0; transform: translateY(-20px); }
                to { opacity: 1; transform: translateY(0); }
            }
            .dev-header {
                background: rgba(0,0,0,0.3);
                padding: 12px 20px;
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-bottom: 2px solid #3B82F6;
                cursor: move;
                user-select: none;
            }
            .dev-header h3 { 
                margin: 0; 
                color: white; 
                font-size: 16px;
                font-weight: bold;
                display: flex;
                align-items: center;
                gap: 8px;
            }
            .dev-header h3::before {
                content: '??';
            }
            .dev-drag-hint {
                font-size: 10px;
                color: rgba(255,255,255,0.5);
                margin-left: 10px;
                font-weight: normal;
            }
            .dev-close-btn {
                background: #ef4444;
                border: none;
                color: white;
                font-size: 16px;
                font-weight: bold;
                width: 28px;
                height: 28px;
                border-radius: 50%;
                cursor: pointer;
                transition: all 0.2s;
                line-height: 28px;
            }
            .dev-close-btn:hover {
                background: #dc2626;
                transform: rotate(90deg);
            }
            .dev-content {
                padding: 15px;
                max-height: calc(85vh - 50px);
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
            .dev-subsection {
                background: rgba(0,0,0,0.15);
                border-radius: 6px;
                padding: 8px 10px;
                margin-bottom: 8px;
                border-left: 3px solid #3B82F6;
            }
            .dev-subsection-title {
                font-size: 11px;
                color: #93c5fd;
                font-weight: bold;
                margin-bottom: 6px;
                text-transform: uppercase;
                letter-spacing: 0.5px;
            }
            .dev-subsection .dev-control {
                padding: 3px 0;
            }
            .dev-subsection .dev-control label {
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
        
        // Make the panel draggable
        this.makeDraggable(panel);
    },
    
    makeDraggable(panel) {
        const header = panel.querySelector('.dev-header') || panel.querySelector('.inspector-header');
        if (!header) return;
        let isDragging = false;
        let startX, startY, initialX, initialY;
        
        header.addEventListener('mousedown', (e) => {
            if (e.target.classList.contains('dev-close-btn')) return;
            isDragging = true;
            startX = e.clientX;
            startY = e.clientY;
            initialX = panel.offsetLeft;
            initialY = panel.offsetTop;
            panel.style.cursor = 'grabbing';
            e.preventDefault();
        });
        
        document.addEventListener('mousemove', (e) => {
            if (!isDragging) return;
            const dx = e.clientX - startX;
            const dy = e.clientY - startY;
            
            let newX = initialX + dx;
            let newY = initialY + dy;
            
            // Keep panel within viewport
            newX = Math.max(0, Math.min(newX, window.innerWidth - panel.offsetWidth));
            newY = Math.max(0, Math.min(newY, window.innerHeight - panel.offsetHeight));
            
            panel.style.left = newX + 'px';
            panel.style.top = newY + 'px';
        });
        
        document.addEventListener('mouseup', () => {
            isDragging = false;
            panel.style.cursor = '';
        });
        
        // Touch support for mobile
        header.addEventListener('touchstart', (e) => {
            if (e.target.classList.contains('dev-close-btn')) return;
            isDragging = true;
            const touch = e.touches[0];
            startX = touch.clientX;
            startY = touch.clientY;
            initialX = panel.offsetLeft;
            initialY = panel.offsetTop;
        }, { passive: true });
        
        document.addEventListener('touchmove', (e) => {
            if (!isDragging) return;
            const touch = e.touches[0];
            const dx = touch.clientX - startX;
            const dy = touch.clientY - startY;
            
            let newX = initialX + dx;
            let newY = initialY + dy;
            
            newX = Math.max(0, Math.min(newX, window.innerWidth - panel.offsetWidth));
            newY = Math.max(0, Math.min(newY, window.innerHeight - panel.offsetHeight));
            
            panel.style.left = newX + 'px';
            panel.style.top = newY + 'px';
        }, { passive: true });
        
        document.addEventListener('touchend', () => {
            isDragging = false;
        });
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
        
        // Pocket Sizes - Opening Multipliers (controls visual gap in rails)
        this.addRangeListener('cornerPocketOpeningMult', (val) => {
            self.game.cornerPocketOpeningMult = parseFloat(val);
            console.log('Corner pocket opening:', val + 'x');
        }, (val) => val + 'x');
        
        this.addRangeListener('sidePocketOpeningMult', (val) => {
            self.game.sidePocketOpeningMult = parseFloat(val);
            console.log('Side pocket opening:', val + 'x');
        }, (val) => val + 'x');
        
        // Pocket Sizes - Capture Zones (controls when balls are potted)
        this.addRangeListener('cornerPocketRadius', (val) => {
            self.game.cornerPocketRadius = parseFloat(val);
            self.game.repositionPockets();
        });
        
        this.addRangeListener('middlePocketRadius', (val) => {
            self.game.middlePocketRadius = parseFloat(val);
            self.game.repositionPockets();
        });
        
        this.addRangeListener('captureThreshold', (val) => {
            self.game.captureThresholdPercent = parseFloat(val) / 100;
        }, (val) => val + '%');
        
        this.addCheckboxListener('showPocketZones', (checked) => {
            self.game.showPocketZones = checked;
        });
        
        this.addCheckboxListener('showCaptureZones', (checked) => {
            self.game.showCaptureZones = checked;
        });
        
        // WPA 2026 Standards
        this.addRangeListener('ballToBallFriction', (val) => {
            self.game.ballToBallFriction = parseFloat(val);
        });
        
        this.addRangeListener('ballToClothSliding', (val) => {
            self.game.ballToClothSliding = parseFloat(val);
        });
        
        this.addRangeListener('rollingResistanceCoeff', (val) => {
            self.game.rollingResistanceCoeff = parseFloat(val);
        });
        
        this.addRangeListener('spinDecayRateCoeff', (val) => {
            self.game.spinDecayRateCoeff = parseFloat(val);
        });
        
        this.addRangeListener('miscueLimit', (val) => {
            self.game.miscueLimit = parseFloat(val);
        });
        
        this.addRangeListener('maxSpinRpm', (val) => {
            self.game.maxSpinRpm = parseFloat(val);
        });
        
        this.addRangeListener('cueBallMassVariance', (val) => {
            self.game.cueBallMassVariance = parseFloat(val);
        });
        
        this.addCheckboxListener('showWpaInfo', (checked) => {
            self.game.showWpaInfo = checked;
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
        const shotModeSelect = document.getElementById('shotControlMode');
        if (shotModeSelect) {
            shotModeSelect.addEventListener('change', (e) => {
                self.game.shotControlMode = e.target.value;
                console.log('Shot control mode changed to:', e.target.value);
            });
        }
        
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
        
        this.addCheckboxListener('autoAimAssist', (checked) => {
            self.game.autoAimAssist = checked;
        });
        
        this.addCheckboxListener('showShotPreview', (checked) => {
            self.game.showShotPreview = checked;
        });
        
        // Fine-Tune Aiming
        this.addRangeListener('fineTuneSensitivity', (val) => {
            const sensitivity = parseFloat(val) / 100;
            if (typeof PoolInput !== 'undefined') {
                PoolInput.fineTuneSensitivity = sensitivity;
            }
            console.log('Fine-tune sensitivity:', val + '%');
        }, (val) => val + '%');
        
        this.addRangeListener('microAdjustStep', (val) => {
            if (typeof PoolInput !== 'undefined') {
                PoolInput.microAdjustStep = parseFloat(val);
            }
            console.log('Micro-adjust step:', val);
        });
        
        // Spin Controls
        this.addRangeListener('maxSpin', (val) => {
            self.game.maxSpin = parseFloat(val);
        });
        
        this.addRangeListener('spinEffect', (val) => {
            self.game.spinEffect = parseFloat(val);
            if (typeof PoolSpinControl !== 'undefined') {
                PoolSpinControl.spinEffectMultiplier = parseFloat(val);
            }
        });
        
        this.addRangeListener('englishTransfer', (val) => {
            self.game.englishTransfer = parseFloat(val);
        });
        
        this.addRangeListener('spinDecayRate', (val) => {
            self.game.spinDecayRate = parseFloat(val);
        });
        
        this.addRangeListener('sweetSpotTolerance', (val) => {
            self.game.sweetSpotTolerance = parseFloat(val);
        });
        
        this.addRangeListener('cueTipMass', (val) => {
            self.game.cueTipMass = parseFloat(val);
        });
        
        this.addRangeListener('squirtFactor', (val) => {
            self.game.squirtFactor = parseFloat(val);
        });
        
        this.addRangeListener('throwFitFactor', (val) => {
            self.game.throwFitFactor = parseFloat(val);
        });
        
        this.addRangeListener('throwSitFactor', (val) => {
            self.game.throwSitFactor = parseFloat(val);
        });
        
        this.addRangeListener('railGrabIntensity', (val) => {
            self.game.railGrabIntensity = parseFloat(val);
        });
        
        this.addCheckboxListener('showSpinArrows', (checked) => {
            self.game.showSpinArrows = checked;
        });
        
        this.addCheckboxListener('showSweetSpot', (checked) => {
            self.game.showSweetSpot = checked;
        });
        
        this.addCheckboxListener('showSquirt', (checked) => {
            self.game.showSquirt = checked;
        });
        
        this.addCheckboxListener('showThrow', (checked) => {
            self.game.showThrow = checked;
        });
        
        
        this.addCheckboxListener('showRailGrab', (checked) => {
            self.game.showRailGrab = checked;
        });
        
        // Game Rules
        this.addCheckboxListener('ballInHandTouchFoul', (checked) => {
            self.game.ballInHandTouchFoul = checked;
            console.log('Ball in hand touch foul:', checked ? 'enabled' : 'disabled');
        });

        // Override Game Settings toggle
        this.addCheckboxListener('devOverrideGameSettings', (checked) => {
            if (typeof PoolGameSettings !== 'undefined') {
                PoolGameSettings._devOverride = checked;

                // Hide or show the player settings button
                const settingsBtn = document.getElementById('gameSettingsBtn');
                if (settingsBtn) {
                    settingsBtn.style.display = checked ? 'none' : '';
                }

                // If enabling override, close the player settings panel
                if (checked && PoolGameSettings.isVisible) {
                    PoolGameSettings.toggle();
                }

                console.log('[Dev] Game Settings override:', checked ? 'ON (dev controls only)' : 'OFF (player settings active)');
            }
        });
        
        // Visual Effects
        this.addCheckboxListener('showAimLine', (checked) => {
            self.game.showAimLine = checked;
        });
        
        this.addCheckboxListener('showGhostBall', (checked) => {
            self.game.showGhostBalls = checked;
        });
        
        this.addCheckboxListener('showTrajectory', (checked) => {
            self.game.showTrajectoryPrediction = checked;
            // Sync to in-game prediction HUD
            if (typeof PoolInput !== 'undefined' && PoolInput.updatePredictionHud) {
                PoolInput.updatePredictionHud(self.game);
            }
        });
        
        this.addCheckboxListener('showCollisionPoints', (checked) => {
            self.game.showCollisionPoints = checked;
        });
        
        this.addRangeListener('trajectoryLength', (val) => {
            self.game.trajectoryLength = parseFloat(val);
            // Sync to in-game prediction HUD
            if (typeof PoolInput !== 'undefined' && PoolInput.updatePredictionHud) {
                PoolInput.updatePredictionHud(self.game);
            }
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

        // --- VFX Module controls ---
        const vfxCheckboxMap = {
            vfxFeltNoise: 'enableFeltNoise',
            vfxCushionShadows: 'enableCushionShadows',
            vfxPocketNets: 'enablePocketNets',
            vfxPocketStitching: 'enablePocketStitching',
            vfxWoodGrain: 'enableWoodGrain',
            vfxTableBevel: 'enableTableBevel',
            vfxOverheadReflection: 'enableOverheadReflection',
            vfxEnvironmentReflection: 'enableEnvironmentReflection',
            vfxDynamicShadows: 'enableDynamicShadows',
            vfxBallSettle: 'enableBallSettle',
            vfxChalkDust: 'enableChalkDust',
            vfxCollisionFlash: 'enableCollisionFlash',
            vfxCushionCompression: 'enableCushionCompression',
            vfxCueInlays: 'enableCueInlays'
        };

        Object.keys(vfxCheckboxMap).forEach(inputId => {
            self.addCheckboxListener(inputId, (checked) => {
                if (typeof PoolVFX !== 'undefined') {
                    PoolVFX[vfxCheckboxMap[inputId]] = checked;
                    console.log('[VFX]', vfxCheckboxMap[inputId], '=', checked);
                }
            });
        });

        this.addRangeListener('vfxFeltNoiseAlpha', (val) => {
            if (typeof PoolVFX !== 'undefined') PoolVFX.feltNoiseAlpha = parseFloat(val);
        });

        this.addRangeListener('vfxCushionShadowDepth', (val) => {
            if (typeof PoolVFX !== 'undefined') PoolVFX.cushionShadowDepth = parseFloat(val);
        });

        this.addRangeListener('vfxCushionShadowAlpha', (val) => {
            if (typeof PoolVFX !== 'undefined') PoolVFX.cushionShadowAlpha = parseFloat(val);
        });

        const vfxLightTempSelect = document.getElementById('vfxLightTemperature');
        if (vfxLightTempSelect) {
            vfxLightTempSelect.addEventListener('change', (e) => {
                if (typeof PoolVFX !== 'undefined') {
                    PoolVFX.lightTemperature = e.target.value;
                    console.log('[VFX] lightTemperature =', e.target.value);
                }
            });
        }

        // VFX bulk buttons
        document.getElementById('vfxAllOn').addEventListener('click', () => {
            Object.keys(vfxCheckboxMap).forEach(inputId => {
                const cb = document.getElementById(inputId);
                if (cb) { cb.checked = true; cb.dispatchEvent(new Event('change')); }
            });
        });
        document.getElementById('vfxAllOff').addEventListener('click', () => {
            Object.keys(vfxCheckboxMap).forEach(inputId => {
                const cb = document.getElementById(inputId);
                if (cb) { cb.checked = false; cb.dispatchEvent(new Event('change')); }
            });
        });
        document.getElementById('vfxDefaults').addEventListener('click', () => {
            Object.keys(vfxCheckboxMap).forEach(inputId => {
                const cb = document.getElementById(inputId);
                if (cb) { cb.checked = true; cb.dispatchEvent(new Event('change')); }
            });
            const feltAlpha = document.getElementById('vfxFeltNoiseAlpha');
            if (feltAlpha) { feltAlpha.value = 0.12; feltAlpha.dispatchEvent(new Event('input')); }
            const shadowDepth = document.getElementById('vfxCushionShadowDepth');
            if (shadowDepth) { shadowDepth.value = 18; shadowDepth.dispatchEvent(new Event('input')); }
            const shadowAlpha = document.getElementById('vfxCushionShadowAlpha');
            if (shadowAlpha) { shadowAlpha.value = 0.22; shadowAlpha.dispatchEvent(new Event('input')); }
            const lightTemp = document.getElementById('vfxLightTemperature');
            if (lightTemp) { lightTemp.value = 'warm'; lightTemp.dispatchEvent(new Event('change')); }
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
        
        // Save/Clear defaults buttons
        document.getElementById('saveDefaults').addEventListener('click', () => self.saveAsDefaults());
        document.getElementById('clearDefaults').addEventListener('click', () => self.clearSavedDefaults());

        // Ball Inspector
        document.getElementById('ballInspectorBtn').addEventListener('click', () => self.toggleBallInspector());
        document.addEventListener('keydown', (e) => {
            if (e.key === 'F4') {
                e.preventDefault();
                self.toggleBallInspector();
            }
        });

        // Load saved defaults on init
        this.loadSavedDefaults();
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
                cornerPocketOpeningMult: 1.6, sidePocketOpeningMult: 1.3,
                cornerPocketRadius: 28, middlePocketRadius: 30, 
                ballRadius: 14, captureThreshold: 30,
                friction: 0.987, cushionRestitution: 0.78, collisionDamping: 0.98,
                maxPower: 40, powerMultiplier: 1.0
            },
            easy: {
                cornerPocketOpeningMult: 2.2, sidePocketOpeningMult: 1.8,
                cornerPocketRadius: 40, middlePocketRadius: 42, 
                ballRadius: 12, captureThreshold: 20,
                friction: 0.985, cushionRestitution: 0.80, collisionDamping: 0.99,
                maxPower: 60, powerMultiplier: 1.2
            },
            tight: {
                cornerPocketOpeningMult: 1.2, sidePocketOpeningMult: 1.0,
                cornerPocketRadius: 22, middlePocketRadius: 24, 
                ballRadius: 14, captureThreshold: 50,
                friction: 0.990, cushionRestitution: 0.75, collisionDamping: 0.96,
                maxPower: 35, powerMultiplier: 0.9
            },
            realistic: {
                cornerPocketOpeningMult: 1.5, sidePocketOpeningMult: 1.25,
                cornerPocketRadius: 27, middlePocketRadius: 29, 
                ballRadius: 13.89, captureThreshold: 40,
                friction: 0.989, cushionRestitution: 0.77, collisionDamping: 0.97,
                maxPower: 45, powerMultiplier: 1.0, rollingResistance: 0.995, spinDecay: 0.985
            },
            arcade: {
                cornerPocketOpeningMult: 2.5, sidePocketOpeningMult: 2.0,
                cornerPocketRadius: 45, middlePocketRadius: 47, 
                ballRadius: 15, captureThreshold: 15,
                friction: 0.98, cushionRestitution: 0.85, collisionDamping: 0.99,
                maxPower: 100, powerMultiplier: 1.5, maxSpin: 2.5
            },
            pro: {
                cornerPocketOpeningMult: 1.3, sidePocketOpeningMult: 1.1,
                cornerPocketRadius: 25, middlePocketRadius: 27, 
                ballRadius: 13.5, captureThreshold: 45,
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
                cornerOpeningMult: this.game.cornerPocketOpeningMult || 1.6,
                middleRadius: this.game.middlePocketRadius,
                sideOpeningMult: this.game.sidePocketOpeningMult || 1.3,
                captureThreshold: this.game.captureThresholdPercent
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
            },
            vfx: typeof PoolVFX !== 'undefined' ? {
                enableFeltNoise: PoolVFX.enableFeltNoise,
                enableCushionShadows: PoolVFX.enableCushionShadows,
                enablePocketNets: PoolVFX.enablePocketNets,
                enablePocketStitching: PoolVFX.enablePocketStitching,
                enableWoodGrain: PoolVFX.enableWoodGrain,
                enableTableBevel: PoolVFX.enableTableBevel,
                enableOverheadReflection: PoolVFX.enableOverheadReflection,
                enableEnvironmentReflection: PoolVFX.enableEnvironmentReflection,
                enableDynamicShadows: PoolVFX.enableDynamicShadows,
                enableBallSettle: PoolVFX.enableBallSettle,
                enableChalkDust: PoolVFX.enableChalkDust,
                enableCollisionFlash: PoolVFX.enableCollisionFlash,
                enableCushionCompression: PoolVFX.enableCushionCompression,
                enableCueInlays: PoolVFX.enableCueInlays,
                feltNoiseAlpha: PoolVFX.feltNoiseAlpha,
                cushionShadowAlpha: PoolVFX.cushionShadowAlpha,
                cushionShadowDepth: PoolVFX.cushionShadowDepth,
                lightTemperature: PoolVFX.lightTemperature
            } : {}
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
            cornerPocketOpeningMult: 1.6, sidePocketOpeningMult: 1.3,
            cornerPocketRadius: 28, middlePocketRadius: 30, 
            captureThreshold: 30,
            friction: 0.987, rollingResistance: 0.99, spinDecay: 0.98,
            cushionRestitution: 0.78, ballRestitution: 0.95, collisionDamping: 0.98,
            maxPower: 40, powerMultiplier: 1.0, aimSensitivity: 1.0, maxPullDistance: 150,
            maxSpin: 1.5, spinEffect: 2.0, englishTransfer: 0.5, spinDecayRate: 0.98,
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
        
        // Reset shot control mode
        const shotModeSelect = document.getElementById('shotControlMode');
        if (shotModeSelect) {
            shotModeSelect.value = 'drag';
            shotModeSelect.dispatchEvent(new Event('change'));
        }
        
        // Reset checkboxes
        const checkboxDefaults = {
            showPocketZones: true, showCaptureZones: false, showAimLine: true, showGhostBall: true,
            showBallNumbers: true, ballShadows: true, tableTexture: true, showSpinArrows: true,
            showTrajectory: false, showVelocities: false, showFps: false, soundEffects: false,
            autoAimAssist: false, showShotPreview: true, showJawCollisionZones: false,
            ballInHandTouchFoul: true,
            devOverrideGameSettings: false,
            // VFX toggles
            vfxFeltNoise: true, vfxCushionShadows: true, vfxPocketNets: true,
            vfxPocketStitching: true, vfxWoodGrain: true, vfxTableBevel: true,
            vfxOverheadReflection: true, vfxEnvironmentReflection: true,
            vfxDynamicShadows: true, vfxBallSettle: true, vfxChalkDust: true,
            vfxCollisionFlash: true, vfxCushionCompression: true, vfxCueInlays: true
        };
        
        Object.keys(checkboxDefaults).forEach(key => {
            const checkbox = document.getElementById(key);
            if (checkbox) {
                checkbox.checked = checkboxDefaults[key];
                checkbox.dispatchEvent(new Event('change'));
            }
        });

        // Reset VFX range controls
        const vfxRangeDefaults = {
            vfxFeltNoiseAlpha: 0.12,
            vfxCushionShadowDepth: 18,
            vfxCushionShadowAlpha: 0.22
        };
        Object.keys(vfxRangeDefaults).forEach(key => {
            const input = document.getElementById(key);
            if (input) {
                input.value = vfxRangeDefaults[key];
                input.dispatchEvent(new Event('input'));
            }
        });

        // Reset VFX light temperature
        const lightTemp = document.getElementById('vfxLightTemperature');
        if (lightTemp) {
            lightTemp.value = 'warm';
            lightTemp.dispatchEvent(new Event('change'));
        }

        console.log('Reset to defaults');
    },
    
    saveAsDefaults() {
        const settings = {
            // Table
            tableWidth: this.game.width,
            tableHeight: this.game.height,
            cushionMargin: this.game.cushionMargin,
            
            // Balls
            ballRadius: this.game.standardBallRadius,
            cueBallRadius: this.game.cueBallRadius,
            ballSpacing: this.game.ballSpacing || 0.5,
            
            // Pockets - Opening multipliers
            cornerPocketOpeningMult: this.game.cornerPocketOpeningMult || 1.6,
            sidePocketOpeningMult: this.game.sidePocketOpeningMult || 1.3,
            cornerPocketRadius: this.game.cornerPocketRadius,
            middlePocketRadius: this.game.middlePocketRadius,
            captureThreshold: (this.game.captureThresholdPercent || 0.30) * 100,
            
            // Physics
            friction: this.game.friction,
            rollingResistance: this.game.rollingResistance || 0.99,
            spinDecay: this.game.spinDecay || 0.98,
            cushionRestitution: this.game.cushionRestitution,
            ballRestitution: this.game.ballRestitution || 0.95,
            collisionDamping: this.game.collisionDamping,
            airResistance: this.game.airResistance || 0.999,
            angularDamping: this.game.angularDamping || 0.98,
            minSpeed: this.game.minSpeed || 0.05,
            gravityEffect: this.game.gravityEffect || 1,
            
            // Shot Controls
            shotControlMode: this.game.shotControlMode || 'drag',
            maxPower: this.game.maxPower,
            powerMultiplier: this.game.powerMultiplier || 1.0,
            aimSensitivity: this.game.aimSensitivity || 1.0,
            maxPullDistance: this.game.maxPullDistance || 150,
            
            // Spin
            maxSpin: this.game.maxSpin || 1.5,
            spinEffect: this.game.spinEffect || 1.0,
            englishTransfer: this.game.englishTransfer || 0.5,
            spinDecayRate: this.game.spinDecayRate || 0.98,
            
            // Audio
            volume: (this.game.volume || 0.5) * 100,

            // Dev override
            devOverrideGameSettings: typeof PoolGameSettings !== 'undefined' ? (PoolGameSettings._devOverride || false) : false,

            // VFX settings
            vfxFeltNoise: typeof PoolVFX !== 'undefined' ? PoolVFX.enableFeltNoise : true,
            vfxCushionShadows: typeof PoolVFX !== 'undefined' ? PoolVFX.enableCushionShadows : true,
            vfxPocketNets: typeof PoolVFX !== 'undefined' ? PoolVFX.enablePocketNets : true,
            vfxPocketStitching: typeof PoolVFX !== 'undefined' ? PoolVFX.enablePocketStitching : true,
            vfxWoodGrain: typeof PoolVFX !== 'undefined' ? PoolVFX.enableWoodGrain : true,
            vfxTableBevel: typeof PoolVFX !== 'undefined' ? PoolVFX.enableTableBevel : true,
            vfxOverheadReflection: typeof PoolVFX !== 'undefined' ? PoolVFX.enableOverheadReflection : true,
            vfxEnvironmentReflection: typeof PoolVFX !== 'undefined' ? PoolVFX.enableEnvironmentReflection : true,
            vfxDynamicShadows: typeof PoolVFX !== 'undefined' ? PoolVFX.enableDynamicShadows : true,
            vfxBallSettle: typeof PoolVFX !== 'undefined' ? PoolVFX.enableBallSettle : true,
            vfxChalkDust: typeof PoolVFX !== 'undefined' ? PoolVFX.enableChalkDust : true,
            vfxCollisionFlash: typeof PoolVFX !== 'undefined' ? PoolVFX.enableCollisionFlash : true,
            vfxCushionCompression: typeof PoolVFX !== 'undefined' ? PoolVFX.enableCushionCompression : true,
            vfxCueInlays: typeof PoolVFX !== 'undefined' ? PoolVFX.enableCueInlays : true,
            vfxFeltNoiseAlpha: typeof PoolVFX !== 'undefined' ? PoolVFX.feltNoiseAlpha : 0.12,
            vfxCushionShadowDepth: typeof PoolVFX !== 'undefined' ? PoolVFX.cushionShadowDepth : 18,
            vfxCushionShadowAlpha: typeof PoolVFX !== 'undefined' ? PoolVFX.cushionShadowAlpha : 0.22,
            vfxLightTemperature: typeof PoolVFX !== 'undefined' ? PoolVFX.lightTemperature : 'warm'
        };
        
        const jsonStr = JSON.stringify(settings);
        console.log('Saving settings via MAUI bridge...');
        
        // Use MAUI bridge (custom URL scheme)
        // This triggers OnWebViewNavigating in C# which saves to Preferences
        window.location.href = 'poolsettings://save?' + encodeURIComponent(jsonStr);
        
        // Also store in window for immediate session use
        window.poolGameSavedDefaults = settings;
    },
    
    loadSavedDefaults() {
        let settings = null;
        
        // Check for MAUI-injected settings first (from Preferences)
        if (window.MAUI_SAVED_SETTINGS) {
            settings = window.MAUI_SAVED_SETTINGS;
            console.log('Loaded settings from MAUI Preferences (injected)');
        }
        
        // Fallback: Check window object
        if (!settings && window.poolGameSavedDefaults) {
            settings = window.poolGameSavedDefaults;
            console.log('Loaded settings from window object');
        }
        
        if (!settings) {
            console.log('No saved defaults found');
            return;
        }
        
        // Apply all settings by updating inputs and triggering events
        try {
            Object.keys(settings).forEach(inputId => {
                const input = document.getElementById(inputId);
                if (input) {
                    if (input.type === 'checkbox') {
                        input.checked = settings[inputId];
                        input.dispatchEvent(new Event('change'));
                    } else if (input.tagName === 'SELECT') {
                        input.value = settings[inputId];
                        input.dispatchEvent(new Event('change'));
                    } else {
                        input.value = settings[inputId];
                        input.dispatchEvent(new Event('input'));
                    }
                }
            });
            console.log('Applied saved defaults successfully');
        } catch (err) {
            console.error('Failed to apply settings:', err);
        }
    },
    
    clearSavedDefaults() {
        if (!confirm('Clear saved default settings?')) return;

        // Use MAUI bridge to clear settings
        window.location.href = 'poolsettings://clear';

        // Also clear window object
        if (window.poolGameSavedDefaults) {
            delete window.poolGameSavedDefaults;
        }
        if (window.MAUI_SAVED_SETTINGS) {
            window.MAUI_SAVED_SETTINGS = null;
        }
    },

    // ===== BALL INSPECTOR =====
    _ballInspectorVisible: false,
    _inspectorBalls: null,
    _inspectorCanvas: null,
    _inspectorCtx: null,
    _inspectorZoom: 3.0,
    _inspectorDrag: null,
    _inspectorAnimId: null,
    _inspectorAutoRotate: true,

    toggleBallInspector() {
        this._ballInspectorVisible = !this._ballInspectorVisible;
        let panel = document.getElementById('ballInspectorPanel');

        if (!panel) {
            this.createBallInspector();
            panel = document.getElementById('ballInspectorPanel');
        }

        if (!panel) {
            return;
        }

        if (this._ballInspectorVisible) {
            panel.classList.add('visible');
            panel.style.display = 'block';
            this.startInspectorLoop();
        } else {
            panel.classList.remove('visible');
            panel.style.display = 'none';
            this.stopInspectorLoop();
        }
    },

    createBallInspector() {
        try {
        const panel = document.createElement('div');
        panel.id = 'ballInspectorPanel';
        panel.innerHTML = `
            <div class='inspector-header'>
                <h3>Ball Inspector</h3>
                <div class='inspector-controls'>
                    <label style='font-size:11px;color:rgba(255,255,255,0.8);display:flex;align-items:center;gap:4px;'>
                        <input type='checkbox' id='inspectorAutoRotate' checked style='width:14px;height:14px;accent-color:#8b5cf6;'>
                        Auto-rotate
                    </label>
                    <button id='inspectorResetBtn' class='inspector-btn'>Reset</button>
                    <button id='ballInspectorClose' class='dev-close-btn'>&times;</button>
                </div>
            </div>
            <div class='inspector-body'>
                <canvas id='ballInspectorCanvas' width='600' height='240'></canvas>
                <div class='inspector-labels'>
                    <span class='inspector-label' style='color:#dc2626;'>Red (1)</span>
                    <span class='inspector-label' style='color:#eab308;'>Yellow (9)</span>
                    <span class='inspector-label' style='color:#a0a0a0;'>Black (8)</span>
                </div>
                <div class='inspector-hint'>Drag to rotate - Scroll to zoom</div>
                <div class='inspector-info' id='inspectorInfo'>Zoom: 3.0x</div>
            </div>
        `;

        const style = document.createElement('style');
        style.id = 'ballInspectorStyle';
        style.textContent = `
            #ballInspectorPanel {
                position: fixed; bottom: 30px; right: 30px; width: 630px;
                background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%);
                border: 2px solid #8b5cf6; border-radius: 12px;
                box-shadow: 0 15px 50px rgba(0,0,0,0.8);
                z-index: 10001; display: none; font-family: Arial, sans-serif; overflow: hidden;
            }
            #ballInspectorPanel.visible { display: block; }
            .inspector-header {
                background: rgba(0,0,0,0.4); padding: 10px 16px;
                display: flex; justify-content: space-between; align-items: center;
                border-bottom: 2px solid #8b5cf6; cursor: move; user-select: none;
            }
            .inspector-header h3 { margin: 0; color: white; font-size: 14px; font-weight: bold; }
            .inspector-controls { display: flex; align-items: center; gap: 10px; }
            .inspector-btn {
                padding: 4px 10px; border: 1px solid rgba(255,255,255,0.3); border-radius: 4px;
                background: rgba(255,255,255,0.1); color: white; font-size: 11px; cursor: pointer;
            }
            .inspector-btn:hover { background: rgba(255,255,255,0.2); }
            .inspector-body { padding: 12px; text-align: center; }
            #ballInspectorCanvas {
                background: #0d4f2b; border-radius: 8px;
                border: 1px solid rgba(255,255,255,0.15); cursor: grab; display: block; margin: 0 auto;
            }
            #ballInspectorCanvas:active { cursor: grabbing; }
            .inspector-labels { display: flex; justify-content: space-around; margin-top: 8px; font-weight: bold; font-size: 12px; }
            .inspector-label { flex: 1; text-align: center; }
            .inspector-hint { color: rgba(255,255,255,0.4); font-size: 10px; margin-top: 6px; }
            .inspector-info { color: #8b5cf6; font-size: 11px; font-weight: bold; margin-top: 4px; }
        `;

        if (!document.getElementById('ballInspectorStyle')) {
            document.head.appendChild(style);
        }
        document.body.appendChild(panel);

        this.makeDraggable(panel);

        // Init canvas
        this._inspectorCanvas = document.getElementById('ballInspectorCanvas');
        if (!this._inspectorCanvas) { console.error('Ball inspector: canvas not found'); return; }
        this._inspectorCtx = this._inspectorCanvas.getContext('2d');

        // Create 3 mock balls
        const baseR = 14;
        this._inspectorBalls = [
            { x: 100, y: 120, r: baseR, color: 'red', num: 1, vx: 0, vy: 0, potted: false },
            { x: 300, y: 120, r: baseR, color: 'yellow', num: 9, vx: 0, vy: 0, potted: false },
            { x: 500, y: 120, r: baseR, color: 'black', num: 8, vx: 0, vy: 0, potted: false }
        ];

        // Init rotation for each ball
        if (typeof PoolBallRotation !== 'undefined') {
            this._inspectorBalls.forEach(b => PoolBallRotation.initBall(b));
        }

        console.log('Ball inspector created OK, canvas:', this._inspectorCanvas.width, 'x', this._inspectorCanvas.height);

        // Event listeners
        const self = this;
        const canvas = this._inspectorCanvas;

        document.getElementById('ballInspectorClose').addEventListener('click', () => self.toggleBallInspector());

        document.getElementById('inspectorResetBtn').addEventListener('click', () => {
            if (typeof PoolBallRotation !== 'undefined') {
                self._inspectorBalls.forEach(b => PoolBallRotation.initBall(b));
            }
            self._inspectorZoom = 3.0;
            self.updateInspectorInfo();
        });

        const autoRotCb = document.getElementById('inspectorAutoRotate');
        if (autoRotCb) {
            autoRotCb.addEventListener('change', (e) => {
                self._inspectorAutoRotate = e.target.checked;
            });
        }

        // Mouse drag to rotate
        canvas.addEventListener('mousedown', (e) => {
            const rect = canvas.getBoundingClientRect();
            const mx = e.clientX - rect.left;
            self._inspectorDrag = {
                startX: e.clientX,
                startY: e.clientY,
                ballIndex: mx < 200 ? 0 : mx < 400 ? 1 : 2
            };
            e.preventDefault();
        });

        document.addEventListener('mousemove', (e) => {
            if (!self._inspectorDrag) return;
            const dx = e.clientX - self._inspectorDrag.startX;
            const dy = e.clientY - self._inspectorDrag.startY;
            self._inspectorDrag.startX = e.clientX;
            self._inspectorDrag.startY = e.clientY;

            const ball = self._inspectorBalls[self._inspectorDrag.ballIndex];
            if (ball && ball.rotQ && typeof PoolBallRotation !== 'undefined') {
                if (Math.abs(dx) > 0) {
                    PoolBallRotation.updateQuaternion(ball, { x: 0, y: 1, z: 0 }, dx * 0.02);
                }
                if (Math.abs(dy) > 0) {
                    PoolBallRotation.updateQuaternion(ball, { x: 1, y: 0, z: 0 }, dy * 0.02);
                }
            }
        });

        document.addEventListener('mouseup', () => {
            self._inspectorDrag = null;
        });

        // Scroll to zoom
        canvas.addEventListener('wheel', (e) => {
            e.preventDefault();
            self._inspectorZoom = Math.max(1.0, Math.min(8.0, self._inspectorZoom + (e.deltaY > 0 ? -0.3 : 0.3)));
            self.updateInspectorInfo();
        }, { passive: false });

        } catch (ex) {
            console.error('Ball inspector creation failed:', ex);
        }
    },

    updateInspectorInfo() {
        const info = document.getElementById('inspectorInfo');
        if (info) info.textContent = 'Zoom: ' + this._inspectorZoom.toFixed(1) + 'x';
    },

    startInspectorLoop() {
        const self = this;

        function loop() {
            if (!self._ballInspectorVisible) return;
            self.renderInspector();
            self._inspectorAnimId = requestAnimationFrame(loop);
        }
        loop();
    },

    stopInspectorLoop() {
        if (this._inspectorAnimId) {
            cancelAnimationFrame(this._inspectorAnimId);
            this._inspectorAnimId = null;
        }
    },

    renderInspector() {
        var ctx = this._inspectorCtx;
        var canvas = this._inspectorCanvas;
        var balls = this._inspectorBalls;

        if (!ctx || !canvas || !balls) return;

        var W = canvas.width;
        var H = canvas.height;

        // Clear and draw felt
        ctx.clearRect(0, 0, W, H);
        ctx.fillStyle = '#0d4f2b';
        ctx.fillRect(0, 0, W, H);

        var zoom = this._inspectorZoom;
        var baseR = 14;

        // Auto-rotate
        if (this._inspectorAutoRotate && typeof PoolBallRotation !== 'undefined' && !this._inspectorDrag) {
            for (var i = 0; i < balls.length; i++) {
                try { PoolBallRotation.updateQuaternion(balls[i], { x: 0.2, y: 1, z: 0.1 }, 0.015); } catch(e) {}
            }
        }

        for (var bi = 0; bi < balls.length; bi++) {
            var ball = balls[bi];
            var r = baseR * zoom;
            var bx = ball.x;
            var by = ball.y;

            // Build a scaled ball with all rotation properties
            var sb = {
                x: bx, y: by, r: r,
                color: ball.color, num: ball.num,
                vx: 0, vy: 0, potted: false,
                rotQ: ball.rotQ,
                numPosX: ball.numPosX, numPosY: ball.numPosY, numPosZ: ball.numPosZ,
                polePosX: ball.polePosX, polePosY: ball.polePosY, polePosZ: ball.polePosZ,
                eqPosX: ball.eqPosX, eqPosY: ball.eqPosY, eqPosZ: ball.eqPosZ
            };

            var lx = -r * 0.4;
            var ly = -r * 0.4;

            try {
                // Shadow
                ctx.save();
                ctx.fillStyle = 'rgba(0,0,0,0.35)';
                ctx.beginPath();
                ctx.ellipse(bx + 2, by + 3, r * 0.6, r * 0.35, 0, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();

                // Felt reflection
                ctx.save();
                ctx.beginPath();
                ctx.arc(sb.x, sb.y, sb.r, 0, Math.PI * 2);
                ctx.clip();
                var feltGrad = ctx.createRadialGradient(sb.x, sb.y + sb.r * 0.5, 0, sb.x, sb.y + sb.r * 0.5, sb.r * 0.7);
                feltGrad.addColorStop(0, 'rgba(27, 139, 61, 0.15)');
                feltGrad.addColorStop(0.5, 'rgba(27, 139, 61, 0.08)');
                feltGrad.addColorStop(1, 'rgba(27, 139, 61, 0)');
                ctx.fillStyle = feltGrad;
                ctx.beginPath();
                ctx.arc(sb.x, sb.y + sb.r * 0.5, sb.r * 0.7, 0, Math.PI * 2);
                ctx.fill();
                ctx.restore();

                // Ball body - same methods the game uses
                if (sb.color === 'red') {
                    PoolRendering.drawUKRedBall(ctx, sb, lx, ly);
                } else if (sb.color === 'yellow') {
                    PoolRendering.drawUKYellowBall(ctx, sb, lx, ly);
                } else if (sb.color === 'black') {
                    PoolRendering.drawBlackBall(ctx, sb, lx, ly);
                }

                // Number circle
                PoolRendering.drawBallNumber(ctx, sb);

                // Specular highlights
                PoolRendering.drawSpecularHighlights(ctx, sb, lx, ly);

                // Overhead light reflection
                if (typeof PoolVFX !== 'undefined' && PoolVFX.enableOverheadReflection) {
                    PoolVFX.drawOverheadLightReflection(ctx, sb);
                }

                // Environment reflection
                if (typeof PoolVFX !== 'undefined' && PoolVFX.enableEnvironmentReflection) {
                    PoolVFX.drawEnvironmentReflection(ctx, sb);
                }
            } catch (renderErr) {
                console.error('Inspector ball render error for ' + ball.color + ':', renderErr);
            }
        }

        // Divider lines
        ctx.strokeStyle = 'rgba(255,255,255,0.15)';
        ctx.setLineDash([4, 4]);
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.moveTo(200, 10); ctx.lineTo(200, H - 10);
        ctx.moveTo(400, 10); ctx.lineTo(400, H - 10);
        ctx.stroke();
        ctx.setLineDash([]);
    },

    showNotification(message, type) {
        const notification = document.createElement('div');
        notification.textContent = message;
        notification.style.cssText = `
            position: fixed;
            top: 20px;
            left: 50%;
            transform: translateX(-50%);
            padding: 12px 24px;
            border-radius: 8px;
            font-weight: bold;
            z-index: 20000;
            animation: fadeInOut 2s ease-in-out forwards;
            ${type === 'success' 
                ? 'background: linear-gradient(135deg, #10b981 0%, #059669 100%); color: white;'
                : 'background: linear-gradient(135deg, #ef4444 0%, #b91c1c 100%); color: white;'}
        `;
        
        // Add animation style if not exists
        if (!document.getElementById('notificationStyle')) {
            const style = document.createElement('style');
            style.id = 'notificationStyle';
            style.textContent = `
                @keyframes fadeInOut {
                    0% { opacity: 0; transform: translateX(-50%) translateY(-10px); }
                    15% { opacity: 1; transform: translateX(-50%) translateY(0); }
                    85% { opacity: 1; transform: translateX(-50%) translateY(0); }
                    100% { opacity: 0; transform: translateX(-50%) translateY(-10px); }
                }
            `;
            document.head.appendChild(style);
        }
        
        document.body.appendChild(notification);
        
        setTimeout(() => {
            notification.remove();
        }, 2000);
    }
};

console.log('PoolDevSettings module loaded');
";
    }
}
