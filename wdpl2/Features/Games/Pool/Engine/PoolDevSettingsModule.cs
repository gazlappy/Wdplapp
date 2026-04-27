namespace Wdpl2.Services;

/// <summary>
/// Developer settings module - provides in-game controls for adjusting all game parameters.
/// Only active in DEBUG builds.
/// </summary>
public static class PoolDevSettingsModule
{
    private static bool IsDebug
    {
        get
        {
#if DEBUG
            return true;
#else
            return false;
#endif
        }
    }

    public static string GenerateJavaScript()
    {
        if (!IsDebug)
            return "// Dev settings disabled in release builds";

        // Build marker so we can verify in the WebView console which JS is loaded.
        // Bump this string whenever you make a change you want to be able to verify is live.
        var buildTag = $"build {System.DateTime.Now:yyyy-MM-dd HH:mm:ss} (export-replays + v2 settings)";

        return @"
// ============================================
// POOL DEVELOPER SETTINGS MODULE
// Real-time game parameter adjustment
// Build: " + buildTag + @"
// ============================================
console.log('%c[PoolGame]%c " + buildTag + @"', 'color:#10b981;font-weight:bold;', 'color:#888;');

const PoolDevSettings = {
    isVisible: false,
    game: null,
    buildTag: '" + buildTag + @"',

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
                <div class='dev-title'>
                    <span class='dev-title-icon'>&#9881;</span>
                    <span class='dev-title-text'>Dev Settings</span>
                </div>
                <button id='devSettingsClose' class='dev-close-btn' title='Close (F2)'>&#10005;</button>
            </div>
            <div id='devTabBar' class='dev-tabbar'></div>
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
                            <input type='range' id='cornerPocketOpeningMult' min='0.6' max='2.5' value='1.0' step='0.1'>
                            <span id='cornerPocketOpeningMultValue'>1.0x</span>
                        </div>
                        <div class='dev-control'>
                            <label>Side Opening:</label>
                            <input type='range' id='sidePocketOpeningMult' min='0.6' max='2.0' value='1.0' step='0.1'>
                            <span id='sidePocketOpeningMultValue'>1.0x</span>
                        </div>
                        <div class='dev-hint'>Controls how wide the gap is in the rails where balls enter</div>
                    </div>
                    <div class='dev-subsection'>
                        <div class='dev-subsection-title'>Capture Zones (Ball Potting)</div>
                        <div class='dev-control'>
                            <label>Corner Capture:</label>
                            <input type='range' id='cornerPocketRadius' min='15' max='45' value='27' step='1'>
                            <span id='cornerPocketRadiusValue'>27</span>
                        </div>
                        <div class='dev-control'>
                            <label>Side Capture:</label>
                            <input type='range' id='middlePocketRadius' min='15' max='45' value='26' step='1'>
                            <span id='middlePocketRadiusValue'>26</span>
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
                        <label>Viscous Drag:</label>
                        <input type='range' id='friction' min='0.95' max='1.0' value='0.992' step='0.001'>
                        <span id='frictionValue'>0.992</span>
                    </div>
                    <div class='dev-control'>
                        <label>Rolling Decel (px/fr):</label>
                        <input type='range' id='rollingResistance' min='0.020' max='0.150' value='0.055' step='0.005'>
                        <span id='rollingResistanceValue'>0.055</span>
                    </div>
                    <div class='dev-control'>
                        <label>Sliding Decel (px/fr):</label>
                        <input type='range' id='spinDecay' min='0.040' max='0.250' value='0.110' step='0.005'>
                        <span id='spinDecayValue'>0.110</span>
                    </div>
                    <div class='dev-hint'>Real px/frame deceleration values. Larger = balls stop sooner.</div>
                </div>
                
                <div class='dev-section'>
                    <h4>Physics - Collisions</h4>
                    <div class='dev-control'>
                        <label>Cushion Bounce:</label>
                        <input type='range' id='cushionRestitution' min='0.5' max='0.99' value='0.95' step='0.01'>
                        <span id='cushionRestitutionValue'>0.95</span>
                    </div>
                    <div class='dev-control'>
                        <label>Ball Restitution:</label>
                        <input type='range' id='ballRestitution' min='0.85' max='1.0' value='0.95' step='0.01'>
                        <span id='ballRestitutionValue'>0.95</span>
                    </div>
                    <div class='dev-control'>
                        <label>Collision Damping:</label>
                        <input type='range' id='collisionDamping' min='0.85' max='1.0' value='0.96' step='0.01'>
                        <span id='collisionDampingValue'>0.96</span>
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
                        <input type='range' id='maxPower' min='20' max='150' value='55' step='5'>
                        <span id='maxPowerValue'>55</span>
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
                        <div class='dev-hint'>Hold period (.) for fine aim, use &#8592; &#8594; for micro-adjustments</div>
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
                    <h4>AI Opponent</h4>
                    <div class='dev-control'>
                        <label>AI Mode:</label>
                        <select id='devAiMode' style='flex:1;'>
                            <option value='off'>Off</option>
                            <option value='p2'>AI is Player 2</option>
                            <option value='p1'>AI is Player 1</option>
                            <option value='both'>AI vs AI</option>
                        </select>
                    </div>
                    <div class='dev-control'>
                        <label>Difficulty:</label>
                        <select id='devAiDifficulty' style='flex:1;'>
                            <option value='easy'>Easy</option>
                            <option value='medium' selected>Medium</option>
                            <option value='hard'>Hard</option>
                        </select>
                    </div>
                    <div class='dev-control'>
                        <label>Think Time (ms):</label>
                        <input type='range' id='devAiThinkTime' min='200' max='4000' value='1600' step='100'>
                        <span id='devAiThinkTimeValue'>1600</span>
                    </div>
                    <div class='dev-control'>
                        <label>Think Jitter (ms):</label>
                        <input type='range' id='devAiThinkJitter' min='0' max='2000' value='700' step='50'>
                        <span id='devAiThinkJitterValue'>700</span>
                    </div>
                    <div class='dev-control'>
                        <label>Post-Shot Pause (ms):</label>
                        <input type='range' id='devAiPostShotPause' min='0' max='2000' value='600' step='50'>
                        <span id='devAiPostShotPauseValue'>600</span>
                    </div>
                    <div class='dev-control'>
                        <label>Aim Noise:</label>
                        <input type='range' id='devAiAimNoise' min='0' max='0.15' value='0.028' step='0.002'>
                        <span id='devAiAimNoiseValue'>0.028</span>
                    </div>
                    <div class='dev-control'>
                        <label>Power Noise:</label>
                        <input type='range' id='devAiPowerNoise' min='0' max='0.5' value='0.15' step='0.01'>
                        <span id='devAiPowerNoiseValue'>0.15</span>
                    </div>
                    <div class='dev-control'>
                        <label>Safety Chance:</label>
                        <input type='range' id='devAiSafetyChance' min='0' max='0.5' value='0.10' step='0.01'>
                        <span id='devAiSafetyChanceValue'>0.10</span>
                    </div>
                    <div class='dev-control'>
                        <label>Selection Bias:</label>
                        <input type='range' id='devAiSelectionBias' min='1' max='15' value='3.0' step='0.5'>
                        <span id='devAiSelectionBiasValue'>3.0</span>
                    </div>
                    <div class='dev-control'>
                        <label>Auto-Restart Frame:</label>
                        <input type='checkbox' id='devAiAutoRestart'>
                    </div>
                    <div class='dev-buttons'>
                        <button id='devAiTakeShotNow' class='dev-btn'>Take Shot Now</button>
                        <button id='devAiSkipTurn' class='dev-btn'>Skip Turn</button>
                        <button id='devAiThinkAloud' class='dev-btn'>Think Aloud</button>
                    </div>
                    <div class='dev-hint'>Live AI tuning. Changes apply on next shot.</div>
                </div>

                <div class='dev-section'>
                    <h4>Replay Recording</h4>
                    <div class='dev-control'>
                        <label>Recording:</label>
                        <input type='checkbox' id='devReplayRecording' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Sample Every N:</label>
                        <input type='range' id='devReplaySampleEvery' min='1' max='10' value='1' step='1'>
                        <span id='devReplaySampleEveryValue'>1</span>
                    </div>
                    <div class='dev-control'>
                        <label>Max Shots Stored:</label>
                        <input type='range' id='devReplayMaxShots' min='10' max='500' value='100' step='10'>
                        <span id='devReplayMaxShotsValue'>100</span>
                    </div>
                    <div class='dev-control'>
                        <label>Show Event Markers:</label>
                        <input type='checkbox' id='devReplayShowEvents' checked>
                    </div>
                    <div class='dev-control'>
                        <label>Trace Mode:</label>
                        <select id='devReplayTraceMode' style='flex:1;'>
                            <option value='hidden'>Hidden</option>
                            <option value='latest' selected>Latest Shot Only</option>
                            <option value='all'>All Recorded</option>
                        </select>
                    </div>
                    <div class='dev-buttons'>
                        <button id='devReplayClear' class='dev-btn'>Clear Buffer</button>
                        <button id='devReplaySummary' class='dev-btn'>Log Summary</button>
                        <button id='devReplayExport' class='dev-btn'>Export JSON</button>
                    </div>
                </div>

                <div class='dev-section'>
                    <h4>Cheats &amp; Test Tools</h4>
                    <div class='dev-control'>
                        <label>Pause Physics:</label>
                        <input type='checkbox' id='devPausePhysics'>
                    </div>
                    <div class='dev-control'>
                        <label>Slow Motion:</label>
                        <input type='checkbox' id='devSlowMotion'>
                    </div>
                    <div class='dev-control'>
                        <label>Time Scale:</label>
                        <input type='range' id='devTimeScale' min='0.05' max='2.0' value='1.0' step='0.05'>
                        <span id='devTimeScaleValue'>1.00x</span>
                    </div>
                    <div class='dev-control'>
                        <label>Auto-Pot Cheat:</label>
                        <input type='checkbox' id='devAutoPot'>
                    </div>
                    <div class='dev-control'>
                        <label>Disable Foul Detect:</label>
                        <input type='checkbox' id='devDisableFouls'>
                    </div>
                    <div class='dev-control'>
                        <label>Set Ball-in-Hand:</label>
                        <input type='checkbox' id='devBallInHand'>
                    </div>
                    <div class='dev-buttons'>
                        <button id='devStepFrame' class='dev-btn'>Step 1 Frame</button>
                        <button id='devForceFoul' class='dev-btn'>Force Foul</button>
                        <button id='devClearTable' class='dev-btn'>Clear Table</button>
                    </div>
                    <div class='dev-buttons' style='margin-top:6px;'>
                        <button id='devWinFrameP1' class='dev-btn'>Win Frame (P1)</button>
                        <button id='devWinFrameP2' class='dev-btn'>Win Frame (P2)</button>
                        <button id='devSkipToBlack' class='dev-btn'>Skip to Black</button>
                    </div>
                    <div class='dev-hint'>Cheats persist until toggled off. Use for testing edge cases.</div>
                </div>

                <div class='dev-section'>
                    <h4>Camera &amp; View</h4>
                    <div class='dev-control'>
                        <label>Canvas Zoom:</label>
                        <input type='range' id='devCanvasZoom' min='0.5' max='2.0' value='1.0' step='0.05'>
                        <span id='devCanvasZoomValue'>1.00x</span>
                    </div>
                    <div class='dev-control'>
                        <label>Canvas Rotation:</label>
                        <input type='range' id='devCanvasRotation' min='0' max='270' value='0' step='90'>
                        <span id='devCanvasRotationValue'>0&deg;</span>
                    </div>
                    <div class='dev-control'>
                        <label>Trail Length:</label>
                        <input type='range' id='devTrailLength' min='0' max='40' value='10' step='1'>
                        <span id='devTrailLengthValue'>10</span>
                    </div>
                    <div class='dev-control'>
                        <label>Show Spin Vector:</label>
                        <input type='checkbox' id='devShowSpinVector'>
                    </div>
                    <div class='dev-buttons'>
                        <button id='devResetCamera' class='dev-btn'>Reset Camera</button>
                        <button id='devFitToWindow' class='dev-btn'>Fit Window</button>
                        <button id='devScreenshot' class='dev-btn'>Screenshot</button>
                    </div>
                </div>

                <div class='dev-section'>
                    <h4>Performance &amp; Debug</h4>
                    <div class='dev-control'>
                        <label>Show FPS:</label>
                        <input type='checkbox' id='devShowFps'>
                    </div>
                    <div class='dev-control'>
                        <label>Show Frame Time:</label>
                        <input type='checkbox' id='devShowFrameTime'>
                    </div>
                    <div class='dev-control'>
                        <label>Show Ball Count:</label>
                        <input type='checkbox' id='devShowBallCount'>
                    </div>
                    <div class='dev-control'>
                        <label>Show Event Log:</label>
                        <input type='checkbox' id='devShowEventLog'>
                    </div>
                    <div class='dev-control'>
                        <label>Verbose Console:</label>
                        <input type='checkbox' id='devVerboseConsole'>
                    </div>
                    <div class='dev-buttons'>
                        <button id='devLogState' class='dev-btn'>Log Game State</button>
                        <button id='devLogBalls' class='dev-btn'>Log Ball Pos</button>
                        <button id='devClearConsole' class='dev-btn'>Clear Console</button>
                    </div>
                </div>

                <div class='dev-section'>
                    <h4>Scenarios &amp; State</h4>
                    <div class='dev-control'>
                        <label>Quick Rack:</label>
                        <select id='devQuickRack' style='flex:1;'>
                            <option value=''>(select)</option>
                            <option value='standard'>Standard 8-Ball Rack</option>
                            <option value='break-spread'>Post-Break Spread</option>
                            <option value='endgame'>Endgame (1 colour + 8)</option>
                            <option value='black-only'>Black Ball Only</option>
                            <option value='scratch-pos'>Scratch Risk Position</option>
                            <option value='cluster'>Tight Cluster</option>
                            <option value='snooker'>Snookered (cue blocked)</option>
                        </select>
                    </div>
                    <div class='dev-buttons'>
                        <button id='devSaveSlot1' class='dev-btn'>Save 1</button>
                        <button id='devSaveSlot2' class='dev-btn'>Save 2</button>
                        <button id='devSaveSlot3' class='dev-btn'>Save 3</button>
                    </div>
                    <div class='dev-buttons' style='margin-top:6px;'>
                        <button id='devLoadSlot1' class='dev-btn'>Load 1</button>
                        <button id='devLoadSlot2' class='dev-btn'>Load 2</button>
                        <button id='devLoadSlot3' class='dev-btn'>Load 3</button>
                    </div>
                    <div class='dev-hint'>Slots persist in localStorage between sessions.</div>
                </div>

                <div class='dev-section'>
                    <h4>Sound Channels</h4>
                    <div class='dev-control'>
                        <label>Master Volume:</label>
                        <input type='range' id='devVolMaster' min='0' max='100' value='50' step='5'>
                        <span id='devVolMasterValue'>50%</span>
                    </div>
                    <div class='dev-control'>
                        <label>Cue Strike:</label>
                        <input type='range' id='devVolCueStrike' min='0' max='100' value='100' step='5'>
                        <span id='devVolCueStrikeValue'>100%</span>
                    </div>
                    <div class='dev-control'>
                        <label>Ball-Ball:</label>
                        <input type='range' id='devVolBallBall' min='0' max='100' value='100' step='5'>
                        <span id='devVolBallBallValue'>100%</span>
                    </div>
                    <div class='dev-control'>
                        <label>Cushion:</label>
                        <input type='range' id='devVolCushion' min='0' max='100' value='100' step='5'>
                        <span id='devVolCushionValue'>100%</span>
                    </div>
                    <div class='dev-control'>
                        <label>Pocket:</label>
                        <input type='range' id='devVolPocket' min='0' max='100' value='100' step='5'>
                        <span id='devVolPocketValue'>100%</span>
                    </div>
                    <div class='dev-control'>
                        <label>Mute on AI Shot:</label>
                        <input type='checkbox' id='devMuteOnAi'>
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
                        <button id='exportSettings' class='dev-btn'>Export Settings</button>
                        <button id='resetDefaults' class='dev-btn'>Reset All</button>
                    </div>
                    <div class='dev-buttons' style='margin-top:8px;'>
                        <button id='exportReplays' class='dev-btn' style='background:linear-gradient(135deg,#0d9488,#0f766e);grid-column:span 3;'>Export Replays (recorded shots)</button>
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
            /* ====== Dev Settings Panel - compact tabbed redesign ====== */
            #devSettingsPanel {
                position: fixed;
                top: 50px;
                left: 50px;
                width: 620px;
                max-height: 82vh;
                background: linear-gradient(165deg, #0f172a 0%, #1e293b 100%);
                border: 1px solid #334155;
                border-radius: 14px;
                box-shadow: 0 25px 60px rgba(0,0,0,0.6), 0 0 0 1px rgba(255,255,255,0.05) inset;
                z-index: 10000;
                display: none;
                overflow: hidden;
                font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Arial, sans-serif;
                resize: both;
                min-width: 480px;
                min-height: 320px;
                color: #e2e8f0;
                --accent: #38bdf8;
                --accent-hot: #0ea5e9;
                --good: #4ade80;
                --warn: #fbbf24;
                --danger: #f87171;
                --text: #e2e8f0;
                --text-dim: #94a3b8;
                --surface: rgba(15,23,42,0.55);
                --surface-2: rgba(30,41,59,0.65);
                --border: rgba(148,163,184,0.18);
            }
            #devSettingsPanel.visible { display: flex; flex-direction: column; animation: devFadeIn 0.18s ease-out; }
            @keyframes devFadeIn {
                from { opacity: 0; transform: translateY(-8px) scale(0.98); }
                to   { opacity: 1; transform: translateY(0) scale(1); }
            }

            .dev-header {
                background: linear-gradient(180deg, rgba(56,189,248,0.10), rgba(56,189,248,0));
                padding: 10px 14px;
                display: flex;
                justify-content: space-between;
                align-items: center;
                border-bottom: 1px solid var(--border);
                cursor: move;
                user-select: none;
                flex: 0 0 auto;
            }
            .dev-title { display:flex; align-items:center; gap:10px; min-width:0; }
            .dev-title-icon { color: var(--accent); font-size: 18px; line-height: 1; }
            .dev-title-text { font-size: 14px; font-weight: 600; color: var(--text); letter-spacing: 0.2px; }
            .dev-close-btn {
                background: transparent;
                border: 1px solid var(--border);
                color: var(--text-dim);
                font-size: 13px;
                width: 26px; height: 26px;
                border-radius: 6px;
                cursor: pointer;
                display: inline-flex; align-items: center; justify-content: center;
                transition: all 0.15s;
            }
            .dev-close-btn:hover { background: var(--danger); color: #fff; border-color: transparent; }

            /* ===== Tab strip ===== */
            .dev-tabbar {
                display: flex;
                gap: 2px;
                padding: 6px 8px 0 8px;
                border-bottom: 1px solid var(--border);
                background: rgba(0,0,0,0.15);
                overflow-x: auto;
                scrollbar-width: none;
                flex: 0 0 auto;
            }
            .dev-tabbar::-webkit-scrollbar { display: none; }
            .dev-tab {
                padding: 7px 12px;
                font-size: 11px;
                font-weight: 600;
                color: var(--text-dim);
                background: transparent;
                border: none;
                border-bottom: 2px solid transparent;
                cursor: pointer;
                white-space: nowrap;
                transition: all 0.15s;
                letter-spacing: 0.3px;
                text-transform: uppercase;
            }
            .dev-tab:hover { color: var(--text); background: rgba(255,255,255,0.04); }
            .dev-tab.active {
                color: var(--accent);
                border-bottom-color: var(--accent);
                background: rgba(56,189,248,0.08);
            }
            .dev-tab .dev-tab-count {
                display: inline-block;
                margin-left: 6px;
                padding: 1px 6px;
                font-size: 9px;
                font-weight: 700;
                border-radius: 8px;
                background: rgba(148,163,184,0.18);
                color: var(--text-dim);
            }
            .dev-tab.active .dev-tab-count { background: rgba(56,189,248,0.20); color: var(--accent); }

            .dev-content {
                padding: 12px;
                overflow-y: auto;
                color: var(--text);
                flex: 1 1 auto;
                min-height: 0;
                /* Slide animation between tabs (forward = enter from right,
                   backward = enter from left). The transition is applied to
                   the inner sections wrapper via the .dev-tab-anim-* classes. */
                position: relative;
            }
            .dev-content.dev-tab-enter-right > .dev-section { animation: devTabSlideInRight 0.18s ease-out both; }
            .dev-content.dev-tab-enter-left  > .dev-section { animation: devTabSlideInLeft  0.18s ease-out both; }
            @keyframes devTabSlideInRight {
                from { opacity: 0; transform: translateX(20px); }
                to   { opacity: 1; transform: translateX(0); }
            }
            @keyframes devTabSlideInLeft {
                from { opacity: 0; transform: translateX(-20px); }
                to   { opacity: 1; transform: translateX(0); }
            }
            .dev-content::-webkit-scrollbar { width: 8px; }
            .dev-content::-webkit-scrollbar-track { background: transparent; }
            .dev-content::-webkit-scrollbar-thumb { background: rgba(148,163,184,0.25); border-radius: 4px; }
            .dev-content::-webkit-scrollbar-thumb:hover { background: rgba(148,163,184,0.45); }

            /* sections become cards inside their tab */
            .dev-section {
                background: var(--surface);
                border-radius: 10px;
                padding: 10px 12px;
                margin-bottom: 10px;
                border: 1px solid var(--border);
            }
            .dev-section[hidden] { display: none !important; }
            .dev-section h4 {
                margin: 0 0 8px 0;
                color: var(--accent);
                font-size: 11px;
                font-weight: 700;
                text-transform: uppercase;
                letter-spacing: 0.6px;
                display: flex;
                align-items: center;
                gap: 6px;
            }
            .dev-section h4::before {
                content: '';
                width: 3px;
                height: 11px;
                background: var(--accent);
                border-radius: 2px;
            }

            /* two-column control grid for compactness */
            .dev-section-grid {
                display: grid;
                grid-template-columns: 1fr 1fr;
                gap: 4px 14px;
            }
            .dev-section-grid > .dev-subsection,
            .dev-section-grid > .dev-hint,
            .dev-section-grid > .dev-buttons,
            .dev-section-grid > .dev-control.full { grid-column: 1 / -1; }

            .dev-control {
                display: grid;
                grid-template-columns: 110px 1fr 44px;
                align-items: center;
                gap: 8px;
                padding: 3px 0;
                min-height: 24px;
            }
            .dev-control label {
                font-size: 11px;
                font-weight: 500;
                color: var(--text-dim);
                white-space: nowrap;
                overflow: hidden;
                text-overflow: ellipsis;
            }
            .dev-control input[type='range'] {
                width: 100%;
                height: 4px;
                border-radius: 2px;
                background: rgba(148,163,184,0.18);
                outline: none;
                cursor: pointer;
                -webkit-appearance: none;
                appearance: none;
            }
            .dev-control input[type='range']::-webkit-slider-thumb {
                -webkit-appearance: none;
                width: 14px; height: 14px;
                border-radius: 50%;
                background: var(--accent);
                cursor: pointer;
                border: 2px solid #0f172a;
                box-shadow: 0 0 0 1px var(--accent);
            }
            .dev-control input[type='range']::-moz-range-thumb {
                width: 14px; height: 14px;
                border-radius: 50%;
                background: var(--accent);
                cursor: pointer;
                border: 2px solid #0f172a;
                box-shadow: 0 0 0 1px var(--accent);
            }
            .dev-control input[type='checkbox'] {
                width: 16px; height: 16px;
                cursor: pointer;
                accent-color: var(--accent);
                grid-column: 2 / -1;
                justify-self: start;
            }
            /* Selects: inline styles in the original markup leave option text
               white-on-white when the dropdown opens. Force a dark background
               and light text for both the closed select and its options. */
            #devSettingsPanel select {
                background: #1e293b !important;
                color: var(--text) !important;
                border: 1px solid var(--border) !important;
                border-radius: 6px !important;
                padding: 5px 8px !important;
                font-size: 11px;
                font-family: inherit;
                outline: none;
                cursor: pointer;
                appearance: none;
                -webkit-appearance: none;
                background-image: linear-gradient(45deg, transparent 50%, var(--text-dim) 50%),
                                  linear-gradient(135deg, var(--text-dim) 50%, transparent 50%);
                background-position: calc(100% - 14px) 50%, calc(100% - 9px) 50%;
                background-size: 5px 5px, 5px 5px;
                background-repeat: no-repeat;
                padding-right: 24px !important;
            }
            #devSettingsPanel select:focus {
                border-color: var(--accent) !important;
                box-shadow: 0 0 0 2px rgba(56,189,248,0.25);
            }
            #devSettingsPanel select option {
                background: #1e293b;
                color: var(--text);
            }
            .dev-control span {
                text-align: right;
                font-weight: 600;
                color: var(--warn);
                font-size: 11px;
                font-variant-numeric: tabular-nums;
            }

            .dev-subsection {
                background: rgba(0,0,0,0.20);
                border-radius: 8px;
                padding: 8px 10px;
                margin: 4px 0;
                border-left: 2px solid var(--accent);
            }
            .dev-subsection-title {
                font-size: 10px;
                color: #93c5fd;
                font-weight: 700;
                margin-bottom: 4px;
                text-transform: uppercase;
                letter-spacing: 0.6px;
            }
            .dev-hint {
                font-size: 10px;
                color: var(--text-dim);
                font-style: italic;
                padding: 2px 0;
                grid-column: 1 / -1;
            }

            .dev-buttons {
                display: grid;
                grid-template-columns: repeat(3, 1fr);
                gap: 6px;
            }
            .dev-btn {
                padding: 7px 6px;
                border: 1px solid var(--border);
                border-radius: 7px;
                cursor: pointer;
                font-weight: 600;
                font-size: 11px;
                background: linear-gradient(180deg, rgba(56,189,248,0.18), rgba(56,189,248,0.08));
                color: var(--text);
                transition: all 0.15s;
            }
            .dev-btn:hover {
                background: linear-gradient(180deg, rgba(56,189,248,0.30), rgba(56,189,248,0.15));
                border-color: var(--accent);
                color: #fff;
                transform: translateY(-1px);
            }
            .dev-btn:active { transform: translateY(0); }
        `;

        document.head.appendChild(style);
        document.body.appendChild(panel);

        // Tabify: group existing dev-section blocks into named tabs without
        // touching the original innerHTML structure (so all input IDs and
        // event-listener wiring continue to work unchanged).
        this.tabifyPanel(panel);

        // Make the panel draggable
        this.makeDraggable(panel);
    },

    /**
     * Walk every .dev-section already inside the panel, decide which tab it
     * belongs to (from its <h4> text), apply data-tab attributes and build the
     * tab strip + click handlers. Sections retain their DOM positions; we just
     * toggle [hidden] on the ones that aren't in the active tab.
     */
    tabifyPanel(panel) {
        // Tab definition: title -> [matchers...] (lowercased substrings of h4)
        const tabDefs = [
            { id: 'table',     label: 'Table',     match: ['table dimensions', 'ball sizes', 'pocket sizes'] },
            { id: 'physics',   label: 'Physics',   match: ['wpa 2026', 'physics - friction', 'physics - collisions', 'advanced physics'] },
            { id: 'play',      label: 'Play',      match: ['shot controls', 'spin controls', 'game rules'] },
            { id: 'ai',        label: 'AI',        match: ['ai opponent'] },
            { id: 'cheats',    label: 'Cheats',    match: ['cheats', 'test tools'] },
            { id: 'replay',    label: 'Replay',    match: ['replay recording'] },
            { id: 'visual',    label: 'Visuals',   match: ['visual effects', 'vfx module', 'camera', 'view'] },
            { id: 'perf',      label: 'Debug',     match: ['performance', 'debug'] },
            { id: 'audio',     label: 'Audio',     match: ['audio settings', 'sound channels'] },
            { id: 'scenarios', label: 'Scenarios', match: ['scenarios', 'state'] },
            { id: 'tools',     label: 'Tools',     match: ['presets', 'actions'] },
        ];

        // Apply data-tab and turn each section's body into a 2-column grid.
        const sections = Array.from(panel.querySelectorAll('.dev-section'));
        const counts = {};
        for (const sec of sections) {
            const h4 = sec.querySelector('h4');
            const name = (h4 ? h4.textContent : '').toLowerCase().trim();
            let tabId = 'tools';
            for (const def of tabDefs) {
                if (def.match.some(m => name.indexOf(m) !== -1)) { tabId = def.id; break; }
            }
            sec.setAttribute('data-tab', tabId);
            counts[tabId] = (counts[tabId] || 0) + 1;

            // Wrap everything except the <h4> in a grid container for compact 2-col layout.
            const grid = document.createElement('div');
            grid.className = 'dev-section-grid';
            const moveables = Array.from(sec.children).filter(c => c.tagName !== 'H4');
            for (const m of moveables) grid.appendChild(m);
            sec.appendChild(grid);
        }

        // Build tab buttons
        const bar = panel.querySelector('#devTabBar');
        if (!bar) return;
        bar.innerHTML = '';
        for (const def of tabDefs) {
            const c = counts[def.id] || 0;
            if (c === 0) continue;
            const b = document.createElement('button');
            b.className = 'dev-tab';
            b.setAttribute('data-tab', def.id);
            b.innerHTML = def.label + '<span class=\'dev-tab-count\'>' + c + '</span>';
            b.addEventListener('click', () => this.activateTab(panel, def.id));
            bar.appendChild(b);
        }
        this.activateTab(panel, tabDefs[0].id);
    },

    activateTab(panel, tabId) {
        const tabs = Array.from(panel.querySelectorAll('.dev-tab'));
        const ids = tabs.map(t => t.getAttribute('data-tab'));
        const newIdx = ids.indexOf(tabId);
        const oldIdx = ids.indexOf(this._activeTabId);
        const direction = (oldIdx === -1 || newIdx > oldIdx) ? 'right' : 'left';
        this._activeTabId = tabId;

        for (const tab of tabs) {
            tab.classList.toggle('active', tab.getAttribute('data-tab') === tabId);
        }
        for (const sec of panel.querySelectorAll('.dev-section')) {
            sec.hidden = sec.getAttribute('data-tab') !== tabId;
        }

        // Apply the slide animation to the now-visible sections. Restart by
        // toggling the class off then on in the next frame so the keyframe
        // animation re-runs even when re-clicking the same tab.
        const content = panel.querySelector('.dev-content');
        if (content) {
            content.scrollTop = 0;
            content.classList.remove('dev-tab-enter-right', 'dev-tab-enter-left');
            // Force reflow so the next class addition re-triggers the animation.
            void content.offsetWidth;
            content.classList.add(direction === 'right' ? 'dev-tab-enter-right' : 'dev-tab-enter-left');
        }
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
            self.game.repositionPockets();
            console.log('Corner pocket opening:', val + 'x');
        }, (val) => val + 'x');

        this.addRangeListener('sidePocketOpeningMult', (val) => {
            self.game.sidePocketOpeningMult = parseFloat(val);
            self.game.repositionPockets();
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
        
        // WPA 2026 Standards (live values on PoolPhysics)
        this.addRangeListener('ballToBallFriction', (val) => {
            self.game.ballToBallFriction = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.BALL_TO_BALL_FRICTION = parseFloat(val);
        });

        this.addRangeListener('ballToClothSliding', (val) => {
            self.game.ballToClothSliding = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.BALL_TO_CLOTH_SLIDING = parseFloat(val);
        });

        this.addRangeListener('rollingResistanceCoeff', (val) => {
            // Coefficient (0.005-0.015) is informational only -- our model uses
            // px/frame deceleration directly via the 'Rolling Decel' slider above.
            self.game.rollingResistanceCoeff = parseFloat(val);
        });

        this.addRangeListener('spinDecayRateCoeff', (val) => {
            self.game.spinDecayRateCoeff = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.SPIN_DECAY_RATE = parseFloat(val);
        });

        this.addRangeListener('miscueLimit', (val) => {
            self.game.miscueLimit = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.MISCUE_LIMIT = parseFloat(val);
        });

        this.addRangeListener('maxSpinRpm', (val) => {
            self.game.maxSpinRpm = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.MAX_SPIN_RPM = parseFloat(val);
        });

        this.addRangeListener('cueBallMassVariance', (val) => {
            self.game.cueBallMassVariance = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.CUE_BALL_MASS_VARIANCE = parseFloat(val);
        });
        
        this.addCheckboxListener('showWpaInfo', (checked) => {
            self.game.showWpaInfo = checked;
        });
        
        // Physics - Friction (live values on PoolPhysics)
        // Slider 'friction' drives VISCOUS_DRAG, 'rollingResistance' drives ROLLING_DECEL,
        // and 'spinDecay' was repurposed to drive SLIDING_DECEL (px/frame).
        this.addRangeListener('friction', (val) => {
            self.game.friction = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.VISCOUS_DRAG = parseFloat(val);
        });

        this.addRangeListener('rollingResistance', (val) => {
            self.game.rollingResistance = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.ROLLING_DECEL = parseFloat(val);
        });

        this.addRangeListener('spinDecay', (val) => {
            self.game.spinDecay = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.SLIDING_DECEL = parseFloat(val);
        });

        // Physics - Collisions (live values on PoolPhysics)
        this.addRangeListener('cushionRestitution', (val) => {
            self.game.cushionRestitution = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.CUSHION_RESTITUTION = parseFloat(val);
        });

        this.addRangeListener('ballRestitution', (val) => {
            // No live target in current physics model -- kept for export/legacy presets.
            self.game.ballRestitution = parseFloat(val);
        });

        this.addRangeListener('collisionDamping', (val) => {
            self.game.collisionDamping = parseFloat(val);
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.COLLISION_DAMPING = parseFloat(val);
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
            if (typeof PoolPhysics !== 'undefined') PoolPhysics.MIN_VELOCITY = parseFloat(val);
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

        // Export recorded ball-trace replay data (different from settings export above).
        const exportReplaysBtn = document.getElementById('exportReplays');
        if (exportReplaysBtn) {
            exportReplaysBtn.addEventListener('click', () => {
                if (typeof PoolReplay === 'undefined') {
                    alert('Replay module not loaded.');
                    return;
                }
                // Always call export() -- it now wraps in an envelope and writes
                // to clipboard even when empty, so paste always reflects the action.
                PoolReplay.export();
            });
            // Refresh button label periodically so it shows the live shot count
            setInterval(() => {
                if (typeof PoolReplay !== 'undefined' && PoolReplay.shots) {
                    exportReplaysBtn.textContent = 'Export Replays (' + PoolReplay.shots.length + ' shot' + (PoolReplay.shots.length === 1 ? '' : 's') + ')';
                }
            }, 1000);
        }
        
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

        // ============================================================
        // ===== NEW DEV TOOLS WIRING (AI / Replay / Cheats / etc) =====
        // ============================================================
        const $ = (id) => document.getElementById(id);
        const onChange = (id, fn) => { const e = $(id); if (e) e.addEventListener('change', (ev) => fn(ev.target.value, ev.target.checked)); };
        const onClick  = (id, fn) => { const e = $(id); if (e) e.addEventListener('click',  fn); };

        // ---------- AI tab ----------
        onChange('devAiMode', (v) => { if (typeof PoolAI !== 'undefined') PoolAI.setMode(v); });
        onChange('devAiDifficulty', (v) => { if (typeof PoolAI !== 'undefined') PoolAI.setDifficulty(v); });
        this.addRangeListener('devAiThinkTime', (val) => { if (typeof PoolAI !== 'undefined') PoolAI.config.thinkTimeMs = parseInt(val, 10); });
        this.addRangeListener('devAiThinkJitter', (val) => { if (typeof PoolAI !== 'undefined') PoolAI.config.thinkTimeJitterMs = parseInt(val, 10); });
        this.addRangeListener('devAiPostShotPause', (val) => { if (typeof PoolAI !== 'undefined') PoolAI.config.postShotPauseMs = parseInt(val, 10); });
        this.addRangeListener('devAiAimNoise', (val) => {
            if (typeof PoolAI === 'undefined') return;
            const d = PoolAI.config.difficulty;
            if (PoolAI.profiles[d]) PoolAI.profiles[d].aimNoise = parseFloat(val);
        });
        this.addRangeListener('devAiPowerNoise', (val) => {
            if (typeof PoolAI === 'undefined') return;
            const d = PoolAI.config.difficulty;
            if (PoolAI.profiles[d]) PoolAI.profiles[d].powerNoise = parseFloat(val);
        });
        this.addRangeListener('devAiSafetyChance', (val) => {
            if (typeof PoolAI === 'undefined') return;
            const d = PoolAI.config.difficulty;
            if (PoolAI.profiles[d]) PoolAI.profiles[d].safetyChance = parseFloat(val);
        });
        this.addRangeListener('devAiSelectionBias', (val) => {
            if (typeof PoolAI === 'undefined') return;
            const d = PoolAI.config.difficulty;
            if (PoolAI.profiles[d]) PoolAI.profiles[d].selectionBias = parseFloat(val);
        });
        onChange('devAiAutoRestart', (_v, checked) => { self._aiAutoRestart = checked; });
        onClick('devAiTakeShotNow', () => {
            if (typeof PoolAI !== 'undefined' && PoolAI.isAiTurn && PoolAI.isAiTurn()) {
                PoolAI._scheduled = false; PoolAI._busy = false;
                PoolAI.showThinking(false);
                PoolAI.takeShot();
            } else {
                console.log('[Dev] Not AI turn');
            }
        });
        onClick('devAiSkipTurn', () => {
            if (self.game && typeof self.game.switchTurn === 'function') {
                self.game.switchTurn();
                console.log('[Dev] Turn skipped');
            }
        });
        onClick('devAiThinkAloud', () => {
            if (typeof PoolAI === 'undefined') return;
            const profile = PoolAI.profiles[PoolAI.config.difficulty];
            const shot = PoolAI.chooseBestShot(profile);
            const safety = PoolAI.chooseSafetyShot();
            console.log('%c[AI Think Aloud]', 'color:#38bdf8;font-weight:bold;');
            console.log('  Best pot:', shot ? { obj: shot.obj.num, pocket: shot.pocket && (shot.pocket.x|0)+','+(shot.pocket.y|0), score: shot.score|0, cut: ((shot.cutAngle||0)*180/Math.PI).toFixed(1)+'deg', power: shot.power.toFixed(1) } : 'none');
            console.log('  Safety:', safety ? { obj: safety.obj.num, bank: safety.bank, power: safety.power.toFixed(1) } : 'none');
            if (shot) console.log('  Spin would be:', PoolAI.chooseSpin(shot, profile));
        });

        // ---------- Replay tab ----------
        onChange('devReplayRecording', (_v, checked) => { if (typeof PoolReplay !== 'undefined') PoolReplay.config.recording = checked; });
        this.addRangeListener('devReplaySampleEvery', (val) => { if (typeof PoolReplay !== 'undefined') PoolReplay.config.sampleEvery = parseInt(val, 10); });
        this.addRangeListener('devReplayMaxShots', (val) => { if (typeof PoolReplay !== 'undefined') PoolReplay.config.maxShots = parseInt(val, 10); });
        onChange('devReplayShowEvents', (_v, checked) => { if (typeof PoolReplay !== 'undefined') PoolReplay.config.showEvents = checked; });
        onChange('devReplayTraceMode', (v) => { if (typeof PoolReplay !== 'undefined') PoolReplay.config.traceMode = v; });
        onClick('devReplayClear', () => { if (typeof PoolReplay !== 'undefined') { PoolReplay.shots = []; PoolReplay._current = null; console.log('[Dev] Replay buffer cleared'); } });
        onClick('devReplaySummary', () => { if (typeof PoolReplay !== 'undefined' && typeof PoolReplay.summary === 'function') PoolReplay.summary(); });
        onClick('devReplayExport', () => { if (typeof PoolReplay !== 'undefined' && typeof PoolReplay.export === 'function') PoolReplay.export(); });

        // ---------- Cheats tab ----------
        onChange('devPausePhysics', (_v, checked) => { self.game._devPausePhysics = checked; console.log('[Dev] Physics', checked ? 'PAUSED' : 'resumed'); });
        onChange('devSlowMotion', (_v, checked) => {
            self.game._devTimeScale = checked ? 0.25 : 1.0;
            const slider = $('devTimeScale'); const span = $('devTimeScaleValue');
            if (slider && span) { slider.value = self.game._devTimeScale; span.textContent = self.game._devTimeScale.toFixed(2) + 'x'; }
        });
        this.addRangeListener('devTimeScale', (val) => { self.game._devTimeScale = parseFloat(val); }, (v) => parseFloat(v).toFixed(2) + 'x');
        onChange('devAutoPot', (_v, checked) => { self.game._devAutoPot = checked; });
        onChange('devDisableFouls', (_v, checked) => { self.game._devDisableFouls = checked; });
        onChange('devBallInHand', (_v, checked) => { self.game.ballInHand = checked; if (self.game.updateTurnDisplay) self.game.updateTurnDisplay(); });
        onClick('devStepFrame', () => {
            const wasPaused = !!self.game._devPausePhysics;
            self.game._devPausePhysics = false;
            self.game._devStepOnce = true;
            setTimeout(() => { self.game._devPausePhysics = wasPaused; }, 50);
        });
        onClick('devForceFoul', () => {
            if (self.game && typeof self.game.commitFoul === 'function') {
                self.game.commitFoul('Forced foul (dev)');
            }
        });
        onClick('devClearTable', () => {
            if (!self.game) return;
            self.game.balls.forEach(b => { if (b.num !== 0) { b.potted = true; b.vx = 0; b.vy = 0; } });
            console.log('[Dev] Table cleared (cue ball preserved)');
        });
        onClick('devWinFrameP1', () => { if (self.game && typeof self.game.handleBlackPotted === 'function') { self.game.players[0].onBlack = true; self.game.handleBlackPotted(self.game.players[0]); } });
        onClick('devWinFrameP2', () => { if (self.game && typeof self.game.handleBlackPotted === 'function') { self.game.players[1].onBlack = true; self.game.handleBlackPotted(self.game.players[1]); } });
        onClick('devSkipToBlack', () => {
            if (!self.game) return;
            // Pot all colour balls; leave 8 + cue + 1 of opponent's colour for testing.
            const player = self.game.getCurrentPlayer ? self.game.getCurrentPlayer() : self.game.players[0];
            self.game.balls.forEach(b => {
                if (b.num === 0 || b.num === 8) return;
                if (player.color && b.color === player.color) { b.potted = true; b.vx = 0; b.vy = 0; }
            });
            if (self.game.checkIfOnBlack) self.game.checkIfOnBlack();
            console.log('[Dev] Skipped to black-ball stage for', player.name);
        });

        // ---------- Camera tab ----------
        this.addRangeListener('devCanvasZoom', (val) => {
            const z = parseFloat(val);
            const cv = self.game && self.game.canvas;
            if (cv) cv.style.transform = (self.game._devRotation ? 'rotate(' + self.game._devRotation + 'deg) ' : '') + 'scale(' + z + ')';
            self.game._devZoom = z;
        }, (v) => parseFloat(v).toFixed(2) + 'x');
        this.addRangeListener('devCanvasRotation', (val) => {
            const r = parseInt(val, 10);
            self.game._devRotation = r;
            const cv = self.game && self.game.canvas;
            const z = self.game._devZoom || 1;
            if (cv) cv.style.transform = 'rotate(' + r + 'deg) scale(' + z + ')';
        }, (v) => v + '\u00B0');
        this.addRangeListener('devTrailLength', (val) => {
            self.game._devTrailLength = parseInt(val, 10);
            // The render loop reads ball.trail.length cap from this if set.
        });
        onChange('devShowSpinVector', (_v, checked) => { self.game._devShowSpinVector = checked; });
        onClick('devResetCamera', () => {
            const cv = self.game && self.game.canvas;
            if (cv) cv.style.transform = '';
            self.game._devZoom = 1; self.game._devRotation = 0;
            const z = $('devCanvasZoom'); if (z) { z.value = 1; $('devCanvasZoomValue').textContent = '1.00x'; }
            const r = $('devCanvasRotation'); if (r) { r.value = 0; $('devCanvasRotationValue').textContent = '0\u00B0'; }
        });
        onClick('devFitToWindow', () => {
            const cv = self.game && self.game.canvas;
            if (!cv) return;
            const sx = (window.innerWidth - 40) / cv.width;
            const sy = (window.innerHeight - 40) / cv.height;
            const z = Math.min(sx, sy, 2);
            cv.style.transform = 'scale(' + z + ')';
            self.game._devZoom = z;
            const slider = $('devCanvasZoom'); if (slider) { slider.value = z; $('devCanvasZoomValue').textContent = z.toFixed(2) + 'x'; }
        });
        onClick('devScreenshot', () => {
            const cv = self.game && self.game.canvas;
            if (!cv) return;
            const url = cv.toDataURL('image/png');
            const a = document.createElement('a');
            a.href = url;
            a.download = 'pool-screenshot-' + new Date().toISOString().replace(/[:.]/g, '-') + '.png';
            a.click();
            console.log('[Dev] Screenshot saved');
        });

        // ---------- Performance / Debug tab ----------
        onChange('devShowFps', (_v, checked) => { self.game._devShowFps = checked; });
        onChange('devShowFrameTime', (_v, checked) => { self.game._devShowFrameTime = checked; });
        onChange('devShowBallCount', (_v, checked) => { self.game._devShowBallCount = checked; });
        onChange('devShowEventLog', (_v, checked) => { self.game._devShowEventLog = checked; });
        onChange('devVerboseConsole', (_v, checked) => { self.game._devVerbose = checked; });
        onClick('devLogState', () => {
            console.log('%c[Dev] Game State', 'color:#38bdf8;font-weight:bold;', {
                phase: self.game.gamePhase,
                currentPlayer: self.game.currentPlayerIndex,
                tableOpen: self.game.tableOpen,
                ballInHand: self.game.ballInHand,
                shotInProgress: self.game.shotInProgress,
                ballsLive: self.game.balls.filter(b => !b.potted).length,
                cueBall: self.game.cueBall ? { x: self.game.cueBall.x|0, y: self.game.cueBall.y|0 } : null,
                players: self.game.players.map(p => ({ name: p.name, color: p.color, onBlack: p.onBlack })),
            });
        });
        onClick('devLogBalls', () => {
            console.log('%c[Dev] Ball Positions', 'color:#38bdf8;font-weight:bold;');
            self.game.balls.filter(b => !b.potted).forEach(b => console.log('  #' + b.num, b.color, '(' + (b.x|0) + ',' + (b.y|0) + ')'));
        });
        onClick('devClearConsole', () => { console.clear(); });

        // ---------- Scenarios tab ----------
        onChange('devQuickRack', (v) => { if (v) self.applyQuickRack(v); });
        onClick('devSaveSlot1', () => self.saveStateSlot(1));
        onClick('devSaveSlot2', () => self.saveStateSlot(2));
        onClick('devSaveSlot3', () => self.saveStateSlot(3));
        onClick('devLoadSlot1', () => self.loadStateSlot(1));
        onClick('devLoadSlot2', () => self.loadStateSlot(2));
        onClick('devLoadSlot3', () => self.loadStateSlot(3));

        // ---------- Sound channels ----------
        const setChannelVol = (channel, v) => {
            const pct = parseInt(v, 10) / 100;
            if (typeof PoolAudio !== 'undefined') {
                PoolAudio._channelVol = PoolAudio._channelVol || {};
                PoolAudio._channelVol[channel] = pct;
            }
        };
        this.addRangeListener('devVolMaster', (v) => {
            const pct = parseInt(v, 10) / 100;
            if (typeof PoolAudio !== 'undefined' && typeof PoolAudio.setVolume === 'function') PoolAudio.setVolume(pct);
        }, (v) => v + '%');
        this.addRangeListener('devVolCueStrike', (v) => setChannelVol('cueStrike', v), (v) => v + '%');
        this.addRangeListener('devVolBallBall', (v) => setChannelVol('ballHit', v), (v) => v + '%');
        this.addRangeListener('devVolCushion', (v) => setChannelVol('cushionBounce', v), (v) => v + '%');
        this.addRangeListener('devVolPocket', (v) => setChannelVol('pocket', v), (v) => v + '%');
        onChange('devMuteOnAi', (_v, checked) => { self._muteOnAi = checked; });

        // AI auto-restart: hook into game over to start next frame automatically when in AI-vs-AI mode.
        if (!self._aiAutoRestartHooked) {
            self._aiAutoRestartHooked = true;
            const origShowGameOver = self.game.showGameOver ? self.game.showGameOver.bind(self.game) : null;
            if (origShowGameOver) {
                self.game.showGameOver = function (title, subtitle) {
                    origShowGameOver(title, subtitle);
                    if (self._aiAutoRestart && typeof PoolAI !== 'undefined' && PoolAI.config.aiPlayers && PoolAI.config.aiPlayers[0] && PoolAI.config.aiPlayers[1]) {
                        setTimeout(() => {
                            const overlay = document.getElementById('gameOverOverlay');
                            if (overlay) overlay.remove();
                            if (typeof self.game.nextFrame === 'function') self.game.nextFrame();
                            else if (typeof self.game.resetGame === 'function') self.game.resetGame();
                        }, 2000);
                    }
                };
            }
        }

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

    // ===== Dev tools support methods =====

    /** Save the current ball positions / phase / players to a localStorage slot. */
    saveStateSlot(n) {
        if (!this.game) return;
        const snap = {
            timestamp: Date.now(),
            phase: this.game.gamePhase,
            currentPlayerIndex: this.game.currentPlayerIndex,
            tableOpen: this.game.tableOpen,
            ballInHand: this.game.ballInHand,
            players: this.game.players.map(p => ({ name: p.name, color: p.color, onBlack: p.onBlack, ballsPotted: p.ballsPotted })),
            balls: this.game.balls.map(b => ({ num: b.num, color: b.color, x: b.x, y: b.y, potted: !!b.potted })),
        };
        try {
            localStorage.setItem('poolDevSlot_' + n, JSON.stringify(snap));
            console.log('[Dev] Saved table state to slot', n);
            this._showToast('Saved to slot ' + n);
        } catch (e) { console.error('[Dev] Save failed', e); }
    },

    /** Load a previously saved slot back into the live game state. */
    loadStateSlot(n) {
        if (!this.game) return;
        let raw;
        try { raw = localStorage.getItem('poolDevSlot_' + n); } catch (e) { return; }
        if (!raw) { console.warn('[Dev] Slot', n, 'is empty'); this._showToast('Slot ' + n + ' empty'); return; }
        let snap;
        try { snap = JSON.parse(raw); } catch (e) { console.error('[Dev] Parse failed', e); return; }

        this.game.gamePhase = snap.phase;
        this.game.currentPlayerIndex = snap.currentPlayerIndex;
        this.game.tableOpen = snap.tableOpen;
        this.game.ballInHand = snap.ballInHand;
        if (snap.players) {
            snap.players.forEach((p, i) => {
                if (this.game.players[i]) Object.assign(this.game.players[i], p);
            });
        }
        // Restore ball positions by number
        const byNum = {};
        for (const b of snap.balls) byNum[b.num] = b;
        for (const b of this.game.balls) {
            const s = byNum[b.num];
            if (s) { b.x = s.x; b.y = s.y; b.vx = 0; b.vy = 0; b.potted = !!s.potted; }
        }
        if (this.game.updateTurnDisplay) this.game.updateTurnDisplay();
        console.log('[Dev] Loaded slot', n, '(saved', new Date(snap.timestamp).toLocaleString() + ')');
        this._showToast('Loaded slot ' + n);
    },

    /** Build well-known table arrangements for testing. */
    applyQuickRack(kind) {
        const g = this.game;
        if (!g) return;
        const w = g.width, h = g.height;
        // Helper: hide all object balls then place a chosen subset.
        const hideAll = () => g.balls.forEach(b => { if (b.num !== 0) { b.potted = true; b.vx = 0; b.vy = 0; } });
        const place = (num, x, y) => {
            const b = g.balls.find(bb => bb.num === num);
            if (!b) return;
            b.potted = false; b.x = x; b.y = y; b.vx = 0; b.vy = 0;
        };
        const cue = g.cueBall;

        switch (kind) {
            case 'standard':
                if (typeof g.setupBalls === 'function') g.setupBalls();
                else if (typeof g.resetGame === 'function') g.resetGame();
                break;
            case 'break-spread':
                hideAll();
                // Scatter 8 balls roughly across the right half
                const scatter = [1,2,3,4,5,9,10,11];
                scatter.forEach((n, i) => {
                    const angle = i * (Math.PI * 2 / scatter.length);
                    place(n, w * 0.55 + Math.cos(angle) * 130, h * 0.5 + Math.sin(angle) * 90);
                });
                place(8, w * 0.65, h * 0.5);
                if (cue) { cue.x = w * 0.25; cue.y = h * 0.5; cue.vx = 0; cue.vy = 0; }
                break;
            case 'endgame':
                hideAll();
                // One yellow + 8 + cue
                place(9, w * 0.35, h * 0.40);
                place(10, w * 0.55, h * 0.65);
                place(8, w * 0.75, h * 0.50);
                if (cue) { cue.x = w * 0.20; cue.y = h * 0.50; cue.vx = 0; cue.vy = 0; }
                g.players[0].color = 'yellow'; g.players[1].color = 'red';
                g.tableOpen = false; g.gamePhase = 'playing';
                break;
            case 'black-only':
                hideAll();
                place(8, w * 0.75, h * 0.50);
                if (cue) { cue.x = w * 0.25; cue.y = h * 0.50; cue.vx = 0; cue.vy = 0; }
                g.players.forEach(p => p.onBlack = true);
                g.tableOpen = false; g.gamePhase = 'playing';
                break;
            case 'scratch-pos':
                hideAll();
                // Cue ball one inch from corner pocket aimed at a hanger
                place(1, w - 50, h - 50);
                if (cue) { cue.x = w - 120; cue.y = h - 120; cue.vx = 0; cue.vy = 0; }
                g.tableOpen = true; g.gamePhase = 'playing';
                break;
            case 'cluster':
                hideAll();
                const cx = w * 0.6, cy = h * 0.5;
                [1,2,3,9,10,11].forEach((n, i) => {
                    const a = i * Math.PI / 3;
                    place(n, cx + Math.cos(a) * 18, cy + Math.sin(a) * 18);
                });
                place(8, cx + 50, cy);
                if (cue) { cue.x = w * 0.20; cue.y = cy; cue.vx = 0; cue.vy = 0; }
                break;
            case 'snooker':
                hideAll();
                // Object ball with two opponent balls between it and the cue
                place(9, w * 0.85, h * 0.50);
                place(2, w * 0.55, h * 0.50); // blocker
                place(3, w * 0.45, h * 0.50); // blocker
                place(8, w * 0.50, h * 0.20);
                if (cue) { cue.x = w * 0.20; cue.y = h * 0.50; cue.vx = 0; cue.vy = 0; }
                g.players[0].color = 'yellow'; g.players[1].color = 'red';
                g.tableOpen = false; g.gamePhase = 'playing';
                break;
        }
        if (g.updateTurnDisplay) g.updateTurnDisplay();
        console.log('[Dev] Applied quick rack:', kind);
        this._showToast('Loaded: ' + kind);
        const sel = document.getElementById('devQuickRack');
        if (sel) sel.value = '';
    },

    _showToast(msg) {
        const t = document.createElement('div');
        t.textContent = msg;
        t.style.cssText = 'position:fixed;bottom:30px;left:50%;transform:translateX(-50%);background:rgba(15,23,42,0.95);color:#e2e8f0;padding:10px 18px;border-radius:8px;border:1px solid #38bdf8;font-size:13px;font-weight:600;z-index:10001;box-shadow:0 8px 20px rgba(0,0,0,0.4);';
        document.body.appendChild(t);
        setTimeout(() => { t.style.transition = 'opacity 0.4s'; t.style.opacity = '0'; }, 1500);
        setTimeout(() => t.remove(), 2000);
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
            // When opening, snap every slider to the actual current runtime value
            // (otherwise stale HTML defaults would overwrite real values on first nudge).
            if (this.isVisible) this.syncFromGameState();
        }
    },

    /**
     * Read live values from the game + PoolPhysics + PoolVFX and set every
     * input/checkbox/span to match -- WITHOUT firing 'input'/'change' events.
     * Prevents the 'touch slider, everything snaps to wrong default' bug.
     */
    syncFromGameState() {
        if (!this.game) return;
        const set = (id, value, fmt) => {
            const inp = document.getElementById(id);
            if (!inp || value === undefined || value === null) return;
            if (inp.type === 'checkbox') {
                inp.checked = !!value;
            } else {
                inp.value = value;
                const span = document.getElementById(id + 'Value');
                if (span) span.textContent = fmt ? fmt(value) : value;
            }
        };
        const g = this.game;
        const PP = (typeof PoolPhysics !== 'undefined') ? PoolPhysics : null;

        // Table
        set('tableWidth', g.width);
        set('tableHeight', g.height);
        set('cushionMargin', g.cushionMargin);
        // Balls
        set('ballRadius', g.standardBallRadius);
        set('cueBallRadius', g.cueBallRadius);
        // Pockets
        set('cornerPocketRadius', g.cornerPocketRadius);
        set('middlePocketRadius', g.middlePocketRadius);
        set('cornerPocketOpeningMult', g.cornerPocketOpeningMult || 1.0, v => v + 'x');
        set('sidePocketOpeningMult', g.sidePocketOpeningMult || 1.0, v => v + 'x');
        set('captureThreshold', Math.round((g.captureThresholdPercent || 0.3) * 100), v => v + '%');
        set('showPocketZones', g.showPocketZones);
        // Physics (live)
        if (PP) {
            set('friction', PP.VISCOUS_DRAG);
            set('rollingResistance', PP.ROLLING_DECEL);
            set('spinDecay', PP.SLIDING_DECEL);
            set('cushionRestitution', PP.CUSHION_RESTITUTION);
            set('collisionDamping', PP.COLLISION_DAMPING);
            set('minSpeed', PP.MIN_VELOCITY);
            set('ballToBallFriction', PP.BALL_TO_BALL_FRICTION);
            set('ballToClothSliding', PP.BALL_TO_CLOTH_SLIDING);
            set('spinDecayRateCoeff', PP.SPIN_DECAY_RATE);
            set('miscueLimit', PP.MISCUE_LIMIT);
            set('maxSpinRpm', PP.MAX_SPIN_RPM);
        }
        // Shot
        set('maxPower', g.maxPower);
        set('powerMultiplier', g.powerMultiplier);
        set('aimSensitivity', g.aimSensitivity);
        set('maxPullDistance', g.maxPullDistance);
        set('autoAimAssist', g.autoAimAssist);
        set('showShotPreview', g.showShotPreview);
        // Spin
        set('maxSpin', g.maxSpin);
        set('spinEffect', g.spinEffect);
        set('englishTransfer', g.englishTransfer);
        set('spinDecayRate', g.spinDecayRate);
        set('showSpinArrows', g.showSpinArrows);
        // Visuals
        set('showTrajectory', g.showTrajectoryPrediction);
        set('trajectoryLength', g.trajectoryLength);
        set('showCollisionPoints', g.showCollisionPoints);
        // Game rules
        set('ballInHandTouchFoul', g.ballInHandTouchFoul);
        // Shot control mode
        const modeSel = document.getElementById('shotControlMode');
        if (modeSel && g.shotControlMode) modeSel.value = g.shotControlMode;
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
            const ball = {
                x: pocket.x + Math.cos(angle) * dist,
                y: pocket.y + Math.sin(angle) * dist,
                vx: 0, vy: 0,
                r: this.game.standardBallRadius,
                color: index % 2 === 0 ? 'red' : 'yellow',
                num: index + 1,
                rotation: 0,
                rotationAxisX: 0,
                rotationAxisY: 1
            };
            // Initialise rotation properties (rotQ, numPos*, polePos*, eqPos*) so it renders correctly.
            if (typeof PoolBallRotation !== 'undefined' && typeof PoolBallRotation.initBall === 'function') {
                PoolBallRotation.initBall(ball);
            }
            this.game.balls.push(ball);
        });

        console.log('Test balls placed near pockets');
    },
    
    randomShot() {
        if (!this.game.cueBall || this.game.cueBall.potted) return;

        const angle = Math.random() * Math.PI * 2;
        const power = Math.random() * this.game.maxPower;

        // Properly register the shot with the rules engine
        if (typeof this.game.startShot === 'function') this.game.startShot();
        this.game.aimAngle = angle;
        this.game.cueBall.vx = Math.cos(angle) * power;
        this.game.cueBall.vy = Math.sin(angle) * power;

        console.log('Random shot fired: angle=' + angle.toFixed(2) + ', power=' + power.toFixed(2));
    },
    
    exportSettings() {
        // Pull live physics values from PoolPhysics (the active model).
        // Falls back to this.game.* for legacy fields that have no live target.
        const PP = (typeof PoolPhysics !== 'undefined') ? PoolPhysics : null;
        const pick = (live, fallback) => (live !== undefined && live !== null) ? live : fallback;

        const settings = {
            _version: 2,
            _generated: new Date().toISOString(),
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
                // Capture radii (how close ball center must be to pocket center to drop)
                cornerRadius: this.game.cornerPocketRadius,
                middleRadius: this.game.middlePocketRadius,
                // Rail-gap multipliers (1.0 = use the table base opening; the base values
                // for a real UK 8-ball table are 3.75 inch corner / 4.5 inch side)
                cornerOpeningMult: this.game.cornerPocketOpeningMult || 1.0,
                sideOpeningMult: this.game.sidePocketOpeningMult || 1.0,
                cornerOpeningPx: this.game.cornerPocketOpening,
                sideOpeningPx: this.game.sidePocketOpening,
                captureThreshold: this.game.captureThresholdPercent
            },
            physics: {
                // px/frame deceleration model (replaced multiplicative friction)
                viscousDrag:   pick(PP && PP.VISCOUS_DRAG,        this.game.friction),
                rollingDecel:  pick(PP && PP.ROLLING_DECEL,       this.game.rollingResistance),
                slidingDecel:  pick(PP && PP.SLIDING_DECEL,       this.game.spinDecay),
                minVelocity:   pick(PP && PP.MIN_VELOCITY,        this.game.minSpeed),
                cushionRestitution: pick(PP && PP.CUSHION_RESTITUTION, this.game.cushionRestitution),
                collisionDamping:   pick(PP && PP.COLLISION_DAMPING,   this.game.collisionDamping),
                ballToBallFriction: pick(PP && PP.BALL_TO_BALL_FRICTION, this.game.ballToBallFriction),
                ballToClothSliding: pick(PP && PP.BALL_TO_CLOTH_SLIDING, this.game.ballToClothSliding),
                spinDecayRate:      pick(PP && PP.SPIN_DECAY_RATE,       this.game.spinDecayRateCoeff),
                miscueLimit:        pick(PP && PP.MISCUE_LIMIT,          this.game.miscueLimit),
                maxSpinRpm:         pick(PP && PP.MAX_SPIN_RPM,          this.game.maxSpinRpm),
                // Legacy fields with no active target -- kept for completeness
                ballRestitution: this.game.ballRestitution || 0.95,
                airResistance:   this.game.airResistance || 0.999,
                angularDamping:  this.game.angularDamping || 0.98,
                gravityEffect:   this.game.gravityEffect !== undefined ? this.game.gravityEffect : 1
            },
            controls: {
                maxPower: this.game.maxPower,
                powerMultiplier: this.game.powerMultiplier || 1.0,
                aimSensitivity: this.game.aimSensitivity || 1.0,
                maxPullDistance: this.game.maxPullDistance || 150,
                shotControlMode: this.game.shotControlMode || 'drag'
            },
            spin: {
                maxSpin: this.game.maxSpin || 1.5,
                spinEffect: this.game.spinEffect || 2.0,
                englishTransfer: this.game.englishTransfer || 0.5
            },
            visual: {
                showPocketZones: this.game.showPocketZones,
                showAimLine: this.game.showAimLine !== false,
                showGhostBall: this.game.showGhostBalls !== false,
                showTrajectory: !!this.game.showTrajectoryPrediction,
                trajectoryLength: this.game.trajectoryLength,
                showVelocities: !!this.game.showVelocities,
                showBallNumbers: this.game.showBallNumbers !== false,
                showFps: !!this.game.showFps,
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
            } : {},
            quality: typeof PoolQuality !== 'undefined' ? {
                preset: PoolQuality.config.preset
            } : {},
            ai: typeof PoolAI !== 'undefined' ? {
                aiPlayers: PoolAI.config.aiPlayers,
                difficulty: PoolAI.config.difficulty
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
            // Pocket geometry tuned to real UK 8-ball table
            cornerPocketOpeningMult: 1.0, sidePocketOpeningMult: 1.0,
            cornerPocketRadius: 27, middlePocketRadius: 26,
            captureThreshold: 30,
            // Physics for the new px/frame deceleration model
            friction: 0.992, rollingResistance: 0.055, spinDecay: 0.110,
            cushionRestitution: 0.95, ballRestitution: 0.95, collisionDamping: 0.96,
            maxPower: 55, powerMultiplier: 1.0, aimSensitivity: 1.0, maxPullDistance: 150,
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
            // Schema version. Bump this whenever a slider's range/semantic changes
            // so loadSavedDefaults() can skip stale fields.
            _version: 3,

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
            vfxLightTemperature: typeof PoolVFX !== 'undefined' ? PoolVFX.lightTemperature : 'warm',

            // ---- AI tab ----
            aiMode: (typeof PoolAI !== 'undefined' && PoolAI.config && PoolAI.config.aiPlayers) ?
                ((PoolAI.config.aiPlayers[0] && PoolAI.config.aiPlayers[1]) ? 'both'
                    : (PoolAI.config.aiPlayers[0] ? 'p1'
                    : (PoolAI.config.aiPlayers[1] ? 'p2' : 'off'))) : 'off',
            aiDifficulty: (typeof PoolAI !== 'undefined' && PoolAI.config) ? PoolAI.config.difficulty : 'medium',
            aiThinkTimeMs: (typeof PoolAI !== 'undefined' && PoolAI.config) ? PoolAI.config.thinkTimeMs : 1600,
            aiThinkJitterMs: (typeof PoolAI !== 'undefined' && PoolAI.config) ? PoolAI.config.thinkTimeJitterMs : 700,
            aiPostShotPauseMs: (typeof PoolAI !== 'undefined' && PoolAI.config) ? PoolAI.config.postShotPauseMs : 600,
            aiAutoRestart: !!this._aiAutoRestart,

            // ---- Replay tab ----
            replayRecording: (typeof PoolReplay !== 'undefined') ? !!PoolReplay.config.recording : true,
            replaySampleEvery: (typeof PoolReplay !== 'undefined') ? PoolReplay.config.sampleEvery : 1,
            replayMaxShots: (typeof PoolReplay !== 'undefined') ? PoolReplay.config.maxShots : 100,
            replayShowEvents: (typeof PoolReplay !== 'undefined') ? !!PoolReplay.config.showEvents : true,
            replayTraceMode: (typeof PoolReplay !== 'undefined') ? PoolReplay.config.traceMode : 'latest',

            // ---- Camera / Debug overlays ----
            devShowFps: !!this.game._devShowFps,
            devShowFrameTime: !!this.game._devShowFrameTime,
            devShowBallCount: !!this.game._devShowBallCount,
            devShowSpinVector: !!this.game._devShowSpinVector,
            devCanvasZoom: this.game._devZoom || 1,
            devCanvasRotation: this.game._devRotation || 0,
            devTrailLength: this.game._devTrailLength || 10,

            // ---- Sound channels ----
            volMaster: (typeof PoolAudio !== 'undefined' && typeof PoolAudio._volume === 'number')
                ? Math.round(PoolAudio._volume * 100) : 50,
            volCueStrike: (typeof PoolAudio !== 'undefined' && PoolAudio._channelVol && typeof PoolAudio._channelVol.cueStrike === 'number')
                ? Math.round(PoolAudio._channelVol.cueStrike * 100) : 100,
            volBallBall: (typeof PoolAudio !== 'undefined' && PoolAudio._channelVol && typeof PoolAudio._channelVol.ballHit === 'number')
                ? Math.round(PoolAudio._channelVol.ballHit * 100) : 100,
            volCushion: (typeof PoolAudio !== 'undefined' && PoolAudio._channelVol && typeof PoolAudio._channelVol.cushionBounce === 'number')
                ? Math.round(PoolAudio._channelVol.cushionBounce * 100) : 100,
            volPocket: (typeof PoolAudio !== 'undefined' && PoolAudio._channelVol && typeof PoolAudio._channelVol.pocket === 'number')
                ? Math.round(PoolAudio._channelVol.pocket * 100) : 100,
            muteOnAi: !!this._muteOnAi,
        };

        const jsonStr = JSON.stringify(settings);
        console.log('[Dev] Saving settings (' + Object.keys(settings).length + ' fields)...');

        // 1) Always write to localStorage as a guaranteed fallback. The MAUI
        //    custom-scheme bridge (poolsettings://) can be flaky on some
        //    WebView2 builds -- localStorage round-trips reliably and is read
        //    by loadSavedDefaults() before the MAUI-injected blob.
        try {
            localStorage.setItem('poolGameDefaults', jsonStr);
            console.log('[Dev] Saved to localStorage');
        } catch (e) {
            console.warn('[Dev] localStorage save failed:', e);
        }

        // 2) Also notify the native side so settings persist across full
        //    WebView reloads (where localStorage may be cleared).
        try {
            window.location.href = 'poolsettings://save?' + encodeURIComponent(jsonStr);
        } catch (e) {
            console.warn('[Dev] MAUI bridge call failed:', e);
        }

        // 3) Cache for current session use
        window.poolGameSavedDefaults = settings;

        // 4) Visible confirmation (independent of MAUI bridge round-trip)
        if (typeof this._showToast === 'function') this._showToast('Settings saved');
        else if (typeof this.showNotification === 'function') this.showNotification('Settings saved!', 'success');
    },
    
    loadSavedDefaults() {
        let settings = null;
        let source = '';

        // 1) Prefer the MAUI-injected blob (survives full WebView reloads via Preferences).
        if (window.MAUI_SAVED_SETTINGS) {
            settings = window.MAUI_SAVED_SETTINGS;
            source = 'MAUI Preferences';
        }

        // 2) Fall back to localStorage (set by saveAsDefaults; survives JS-level resets).
        if (!settings) {
            try {
                const raw = localStorage.getItem('poolGameDefaults');
                if (raw) {
                    settings = JSON.parse(raw);
                    source = 'localStorage';
                }
            } catch (e) { /* ignore */ }
        }

        // 3) In-window cache (current session)
        if (!settings && window.poolGameSavedDefaults) {
            settings = window.poolGameSavedDefaults;
            source = 'window cache';
        }

        if (!settings) {
            console.log('[Dev] No saved defaults found');
            return;
        }
        console.log('[Dev] Loading saved defaults from', source);

        // ---------- MIGRATION ----------
        // These fields had their range or semantic changed in v2 and would be
        // applied incorrectly (silently clamped) if loaded from a v1 save.
        const STALE_V1_FIELDS = new Set([
            // Physics: friction was multiplicative -> now PoolPhysics.VISCOUS_DRAG
            'friction',
            // rollingResistance: was 0.95-0.999 multiplier -> now px/frame 0.020-0.150
            'rollingResistance',
            // spinDecay: was 0.90-0.999 multiplier -> now px/frame 0.040-0.250 (sliding decel)
            'spinDecay',
            // Cushion / collision tuned for the new model
            'cushionRestitution',
            'collisionDamping',
            // Pocket geometry retuned to real UK 8-ball table dimensions
            'cornerPocketRadius',
            'middlePocketRadius',
            'cornerPocketOpeningMult',
            'sidePocketOpeningMult',
            // maxPower default changed from 40 -> 55 with the new physics
            'maxPower',
        ]);

        const savedVersion = settings._version || 1;
        const skipStale = savedVersion < 2;
        const skippedKeys = [];

        // Apply all settings by updating inputs and triggering events
        try {
            Object.keys(settings).forEach(inputId => {
                if (inputId.startsWith('_')) return; // metadata key, skip
                if (skipStale && STALE_V1_FIELDS.has(inputId)) {
                    skippedKeys.push(inputId + '=' + settings[inputId]);
                    return;
                }
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

            if (skippedKeys.length > 0) {
                console.warn('[Dev] Migrated v1 saved settings - skipped stale fields:\n  ' + skippedKeys.join('\n  '));
                console.warn('[Dev] Tip: open F2 -> Actions -> Save as Defaults to re-save with the new model.');
            } else {
                console.log('Applied saved defaults successfully (v' + savedVersion + ')');
            }

            // ---- Apply settings whose saved key doesn't match an input ID ----
            // (AI / Replay / Camera / Sound were added later; their save keys
            //  use friendly names that need explicit remapping to UI controls.)
            const remap = {
                aiMode: 'devAiMode',
                aiDifficulty: 'devAiDifficulty',
                aiThinkTimeMs: 'devAiThinkTime',
                aiThinkJitterMs: 'devAiThinkJitter',
                aiPostShotPauseMs: 'devAiPostShotPause',
                aiAutoRestart: 'devAiAutoRestart',
                replayRecording: 'devReplayRecording',
                replaySampleEvery: 'devReplaySampleEvery',
                replayMaxShots: 'devReplayMaxShots',
                replayShowEvents: 'devReplayShowEvents',
                replayTraceMode: 'devReplayTraceMode',
                devShowFps: 'devShowFps',
                devShowFrameTime: 'devShowFrameTime',
                devShowBallCount: 'devShowBallCount',
                devShowSpinVector: 'devShowSpinVector',
                devCanvasZoom: 'devCanvasZoom',
                devCanvasRotation: 'devCanvasRotation',
                devTrailLength: 'devTrailLength',
                volMaster: 'devVolMaster',
                volCueStrike: 'devVolCueStrike',
                volBallBall: 'devVolBallBall',
                volCushion: 'devVolCushion',
                volPocket: 'devVolPocket',
                muteOnAi: 'devMuteOnAi',
            };
            Object.keys(remap).forEach(key => {
                if (settings[key] === undefined) return;
                const el = document.getElementById(remap[key]);
                if (!el) return;
                if (el.type === 'checkbox') {
                    el.checked = !!settings[key];
                    el.dispatchEvent(new Event('change'));
                } else if (el.tagName === 'SELECT') {
                    el.value = settings[key];
                    el.dispatchEvent(new Event('change'));
                } else {
                    el.value = settings[key];
                    el.dispatchEvent(new Event('input'));
                }
            });
        } catch (err) {
            console.error('Failed to apply settings:', err);
        }
    },

    clearSavedDefaults() {
        if (!confirm('Clear saved default settings?')) return;

        // Use MAUI bridge to clear settings
        try { window.location.href = 'poolsettings://clear'; } catch (e) { /* ignore */ }

        // Clear localStorage too
        try { localStorage.removeItem('poolGameDefaults'); } catch (e) { /* ignore */ }

        // Also clear window objects
        if (window.poolGameSavedDefaults) {
            delete window.poolGameSavedDefaults;
        }
        if (window.MAUI_SAVED_SETTINGS) {
            window.MAUI_SAVED_SETTINGS = null;
        }
        if (typeof this._showToast === 'function') this._showToast('Saved defaults cleared');
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
