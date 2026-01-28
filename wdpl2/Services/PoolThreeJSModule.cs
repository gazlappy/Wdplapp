using System.Reflection;
using System.Text.Json;

namespace Wdpl2.Services;

/// <summary>
/// Three.js-based photorealistic 3D pool table renderer
/// Uses WebGL for high-quality rendering with proper lighting, shadows, and materials
/// Loads external GLTF models for realistic table rendering
/// </summary>
public static class PoolThreeJSModule
{
    // Cached embedded model data
    private static string? _embeddedGltfJson;
    private static string? _embeddedBinBase64;
    
    /// <summary>
    /// Load embedded model files from embedded resources
    /// </summary>
    public static async Task LoadEmbeddedModelAsync()
    {
        if (_embeddedGltfJson != null && _embeddedBinBase64 != null)
            return; // Already loaded
            
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            
            // Find the embedded resource names
            var resourceNames = assembly.GetManifestResourceNames();
            System.Diagnostics.Debug.WriteLine($"[PoolThreeJS] Available resources: {string.Join(", ", resourceNames)}");
            
            var gltfResourceName = resourceNames.FirstOrDefault(n => n.EndsWith("scene.gltf"));
            var binResourceName = resourceNames.FirstOrDefault(n => n.EndsWith("scene.bin"));
            
            if (gltfResourceName == null || binResourceName == null)
            {
                System.Diagnostics.Debug.WriteLine($"[PoolThreeJS] Model resources not found. GLTF={gltfResourceName}, BIN={binResourceName}");
                return;
            }
            
            // Load GLTF JSON
            using var gltfStream = assembly.GetManifestResourceStream(gltfResourceName);
            if (gltfStream != null)
            {
                using var gltfReader = new StreamReader(gltfStream);
                _embeddedGltfJson = await gltfReader.ReadToEndAsync();
            }
            
            // Load BIN as base64
            using var binStream = assembly.GetManifestResourceStream(binResourceName);
            if (binStream != null)
            {
                using var memStream = new MemoryStream();
                await binStream.CopyToAsync(memStream);
                _embeddedBinBase64 = Convert.ToBase64String(memStream.ToArray());
            }
            
            System.Diagnostics.Debug.WriteLine($"[PoolThreeJS] Loaded embedded model: GLTF={_embeddedGltfJson?.Length ?? 0} chars, BIN={_embeddedBinBase64?.Length ?? 0} base64 chars");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"[PoolThreeJS] Failed to load embedded model: {ex.Message}");
            _embeddedGltfJson = null;
            _embeddedBinBase64 = null;
        }
    }
    
    /// <summary>
    /// Get the embedded model data as JavaScript code to include directly in HTML
    /// NOTE: Embedding large model data directly causes crashes, so we just set flags
    /// </summary>
    public static string GetEmbeddedModelScript()
    {
        // Don't embed the full model data - it's too large and crashes the WebView
        // Instead, we'll load from GitHub or use procedural table
        return "window.EMBEDDED_GLTF_MODEL = null; window.EMBEDDED_GLTF_BIN = null; console.log('[MAUI] Model will load from GitHub or use procedural');";
    }
    
    /// <summary>
    /// Get the GLTF JSON for injection via EvaluateJavaScriptAsync
    /// </summary>
    public static string? GetGltfJson() => _embeddedGltfJson;
    
    /// <summary>
    /// Get the binary data as base64 for injection via EvaluateJavaScriptAsync
    /// </summary>
    public static string? GetBinBase64() => _embeddedBinBase64;
    
    public static string GenerateJavaScript()
    {
        return """
// ============================================
// THREE.JS PHOTOREALISTIC POOL TABLE RENDERER
// Version 2.0 - GLTF Model Loading Support
// ============================================

const PoolThreeJS = {
    enabled: false,
    initialized: false,
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    table: null,
    tableModel: null,
    balls: [],
    ballMeshes: {},
    lights: [],
    container: null,
    animationId: null,
    gltfLoader: null,
    
    // Model configuration
    modelUrl: 'https://sketchfab.com/models/f6fec9539faa4556b70a087736491f59/embed',
    // Direct GLB URL (we'll use a proxy or fallback)
    useExternalModel: true,
    modelLoaded: false,
    
    // Camera settings
    cameraDistance: 1000,
    cameraHeight: 500,
    cameraAngle: 0.3,
    
    // Table dimensions (matching game physics)
    tableWidth: 1000,
    tableHeight: 500,
    
    // Model scaling - adjust these to fit the model to game coordinates
    modelScale: 1,
    modelOffsetX: 0,
    modelOffsetY: 0,
    modelOffsetZ: 0,
    
    // Playing surface height (where balls sit)
    playingSurfaceY: 15,
    
    // Quality settings
    shadowMapSize: 2048,
    antialias: true,
    
    init: async function(gameInstance) {
        if (this.initialized) return true;
        
        console.log('[ThreeJS] Initializing photorealistic renderer...');
        
        try {
            // Load Three.js from CDN if not already loaded
            if (typeof THREE === 'undefined') {
                console.log('[ThreeJS] THREE not found, loading from CDN...');
                await this.loadThreeJS();
            } else {
                console.log('[ThreeJS] THREE already loaded, version:', THREE.REVISION);
            }
            
            
            // Verify THREE loaded successfully
            if (typeof THREE === 'undefined') {
                throw new Error('THREE.js failed to load');
            }
            console.log('[ThreeJS] THREE.js ready, version:', THREE.REVISION);
            
            // Create container
            this.container = document.createElement('div');
            this.container.id = 'threejs-container';
            this.container.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;display:none;z-index:100;';
            
            // Try different container IDs used by different page versions
            const gameContainer = document.getElementById('canvasWrapper') || 
                                  document.getElementById('poolGameContainer') || 
                                  document.querySelector('.canvas-wrapper') ||
                                  document.body;
            gameContainer.appendChild(this.container);
            
            // Add loading indicator
            this.showLoadingIndicator();
            
            // Initialize Three.js
            this.setupRenderer();
            this.setupScene();
            this.setupCamera();
            this.setupLighting();
            
            // Try to load external table model, fall back to procedural if it fails
            try {
                await this.loadTableModel();
            } catch (error) {
                console.warn('[ThreeJS] Failed to load table model, using procedural table:', error);
                await this.createProceduralTable();
            }
            
            // Try to load ball 3D model (optional - falls back to textured spheres)
            try {
                await this.loadBallModel();
            } catch (error) {
                console.log('[ThreeJS] Using textured sphere balls (no 3D model)');
            }
            
            this.setupControls();
            this.createControlPanel(); // Add adjustment controls
            this.hideLoadingIndicator();
            
            // Handle resize
            window.addEventListener('resize', () => this.onResize());
            
            this.initialized = true;
            console.log('[ThreeJS] Initialization complete - UK Pool Balls');
            return true;
            
        } catch (error) {
            console.error('[ThreeJS] Initialization failed:', error);
            this.hideLoadingIndicator();
            alert('Failed to load 3D renderer: ' + error.message);
            return false;
        }
    },
    
    loadThreeJS: async function() {
        return new Promise((resolve, reject) => {
            console.log('[ThreeJS] Starting to load Three.js libraries...');
            
            const loadScript = (url, name) => {
                return new Promise((res, rej) => {
                    const script = document.createElement('script');
                    script.src = url;
                    script.crossOrigin = 'anonymous';
                    script.onload = () => {
                        console.log('[ThreeJS] ' + name + ' loaded successfully');
                        res();
                    };
                    script.onerror = (e) => {
                        console.error('[ThreeJS] Failed to load ' + name + ' from ' + url, e);
                        rej(new Error('Failed to load ' + name));
                    };
                    document.head.appendChild(script);
                });
            };
            
            // Use Three.js r137 - last version with global script support
            const VERSION = '0.137.0';
            
            // Load scripts sequentially
            loadScript(`https://cdnjs.cloudflare.com/ajax/libs/three.js/r137/three.min.js`, 'Three.js Core')
                .then(() => loadScript(`https://cdn.jsdelivr.net/npm/three@${VERSION}/examples/js/controls/OrbitControls.js`, 'OrbitControls'))
                .then(() => loadScript(`https://cdn.jsdelivr.net/npm/three@${VERSION}/examples/js/loaders/GLTFLoader.js`, 'GLTFLoader'))
                .then(() => {
                    console.log('[ThreeJS] All libraries loaded successfully');
                    resolve();
                })
                .catch((error) => {
                    console.error('[ThreeJS] Primary CDN failed:', error);
                    console.log('[ThreeJS] Trying backup CDN...');
                    
                    // Backup: use unpkg
                    loadScript(`https://unpkg.com/three@${VERSION}/build/three.min.js`, 'Three.js Core (backup)')
                        .then(() => loadScript(`https://unpkg.com/three@${VERSION}/examples/js/controls/OrbitControls.js`, 'OrbitControls (backup)'))
                        .then(() => loadScript(`https://unpkg.com/three@${VERSION}/examples/js/loaders/GLTFLoader.js`, 'GLTFLoader (backup)'))
                        .then(() => {
                            console.log('[ThreeJS] Backup libraries loaded');
                            resolve();
                        })
                        .catch((backupError) => {
                            console.error('[ThreeJS] All CDN attempts failed:', backupError);
                            reject(backupError);
                        });
                });
        });
    },
    
    
    showLoadingIndicator: function() {
        const loader = document.createElement('div');
        loader.id = 'threejs-loader';
        loader.style.cssText = `
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: rgba(0,0,0,0.9);
            color: white;
            padding: 30px 50px;
            border-radius: 15px;
            font-family: Arial, sans-serif;
            text-align: center;
            z-index: 1000;
            box-shadow: 0 10px 40px rgba(0,0,0,0.5);
        `;
        
        loader.innerHTML = `
            <div style="font-size: 24px; margin-bottom: 15px;">?? Loading 3D Table...</div>
            <div style="width: 200px; height: 6px; background: #333; border-radius: 3px; overflow: hidden;">
                <div id="threejs-progress" style="width: 0%; height: 100%; background: linear-gradient(90deg, #4ade80, #22c55e); transition: width 0.3s;"></div>
            </div>
            <div id="threejs-status" style="margin-top: 10px; font-size: 12px; color: #888;">Initializing...</div>
        `;
        this.container.appendChild(loader);
    },
    
    updateLoadingProgress: function(percent, status) {
        const progress = document.getElementById('threejs-progress');
        const statusEl = document.getElementById('threejs-status');
        if (progress) progress.style.width = percent + '%';
        if (statusEl) statusEl.textContent = status;
    },
    
    hideLoadingIndicator: function() {
        const loader = document.getElementById('threejs-loader');
        if (loader) {
            loader.style.opacity = '0';
            loader.style.transition = 'opacity 0.5s';
            setTimeout(() => loader.remove(), 500);
        }
    },
    
    // Settings storage
    settings: {
        // Ball settings
        ballHeight: 15,
        ballSize: 100,
        ballRoughness: 15,
        ballMetalness: 5,
        
        // Table settings
        tableScale: 100,
        tableWidthScale: 100,
        tableLengthScale: 100,
        tableYOffset: 0,
        tableXOffset: 0,
        tableZOffset: 0,
        tableRotation: 0,
        
        // Camera settings
        cameraFOV: 45,
        cameraMinDistance: 300,
        cameraMaxDistance: 1500,
        cameraDamping: 5,
        
        // Lighting settings
        ambientIntensity: 20,
        mainLightIntensity: 200,
        fillLightIntensity: 40,
        lampLightIntensity: 150,
        shadowSoftness: 40,
        
        // Environment settings
        exposure: 120,
        backgroundDarkness: 50,
        floorReflectivity: 10,
        
        // Quality settings
        shadowQuality: 2048,
        antialiasing: true,
        
        // Felt color (RGB)
        feltColorR: 26,
        feltColorG: 127,
        feltColorB: 55
    },
    
    createControlPanel: function() {
        const self = this;
        
        // Initialize settings from current values
        this.settings.ballHeight = this.playingSurfaceY;
        
        const panel = document.createElement('div');
        panel.id = 'threejs-controls';
        panel.style.cssText = `
            position: absolute;
            top: 10px;
            right: 10px;
            bottom: 10px;
            background: rgba(0,0,0,0.92);
            color: white;
            padding: 0;
            border-radius: 12px;
            font-family: 'Segoe UI', Arial, sans-serif;
            font-size: 11px;
            z-index: 1001;
            width: 280px;
            display: flex;
            flex-direction: column;
            box-shadow: 0 8px 32px rgba(0,0,0,0.5);
            border: 1px solid rgba(255,255,255,0.1);
        `;
        
        panel.innerHTML = `
            <div id="settingsHeader" style="background: linear-gradient(135deg, #1a472a 0%, #2d5a3d 100%); padding: 10px 12px; cursor: pointer; user-select: none; border-radius: 12px 12px 0 0; flex-shrink: 0;">
                <div style="display: flex; justify-content: space-between; align-items: center;">
                    <span style="font-weight: bold; font-size: 13px;">?? Settings</span>
                    <span id="toggleIcon" style="font-size: 14px; transition: transform 0.3s;">?</span>
                </div>
            </div>
            
            <div id="settingsContent" style="flex: 1; overflow-y: auto; padding: 8px 12px; min-height: 0;">
                <!-- BALL SETTINGS -->
                <div class="settings-section">
                    <div class="section-header" data-section="balls">?? Ball Settings <span class="collapse-icon">?</span></div>
                    <div class="section-content" id="ballsSection">
                        <div class="setting-row">
                            <label>Height: <span id="ballHeightValue">${this.settings.ballHeight.toFixed(0)}</span></label>
                            <input type="range" id="ballHeightSlider" min="0" max="500" value="${this.settings.ballHeight}">
                        </div>
                        <div class="setting-row">
                            <label>Size: <span id="ballSizeValue">${this.settings.ballSize}</span>%</label>
                            <input type="range" id="ballSizeSlider" min="50" max="150" value="${this.settings.ballSize}">
                        </div>
                        <div class="setting-row">
                            <label>Roughness: <span id="ballRoughnessValue">${this.settings.ballRoughness}</span>%</label>
                            <input type="range" id="ballRoughnessSlider" min="0" max="100" value="${this.settings.ballRoughness}">
                        </div>
                        <div class="setting-row">
                            <label>Metalness: <span id="ballMetalnessValue">${this.settings.ballMetalness}</span>%</label>
                            <input type="range" id="ballMetalnessSlider" min="0" max="100" value="${this.settings.ballMetalness}">
                        </div>
                    </div>
                </div>
                
                <!-- TABLE SETTINGS -->
                <div class="settings-section">
                    <div class="section-header" data-section="table">?? Table Settings <span class="collapse-icon">?</span></div>
                    <div class="section-content" id="tableSection">
                        <div class="setting-row">
                            <label>Overall Scale: <span id="tableScaleValue">${this.settings.tableScale}</span>%</label>
                            <input type="range" id="tableScaleSlider" min="50" max="150" value="${this.settings.tableScale}">
                        </div>
                        <div class="setting-row">
                            <label>Width Scale: <span id="tableWidthValue">${this.settings.tableWidthScale}</span>%</label>
                            <input type="range" id="tableWidthSlider" min="50" max="150" value="${this.settings.tableWidthScale}">
                        </div>
                        <div class="setting-row">
                            <label>Length Scale: <span id="tableLengthValue">${this.settings.tableLengthScale}</span>%</label>
                            <input type="range" id="tableLengthSlider" min="50" max="150" value="${this.settings.tableLengthScale}">
                        </div>
                        <div class="setting-row">
                            <label>Y Offset: <span id="tableYValue">${this.settings.tableYOffset}</span></label>
                            <input type="range" id="tableYSlider" min="-200" max="200" value="${this.settings.tableYOffset}">
                        </div>
                        <div class="setting-row">
                            <label>X Offset: <span id="tableXValue">${this.settings.tableXOffset}</span></label>
                            <input type="range" id="tableXSlider" min="-200" max="200" value="${this.settings.tableXOffset}">
                        </div>
                        <div class="setting-row">
                            <label>Z Offset: <span id="tableZValue">${this.settings.tableZOffset}</span></label>
                            <input type="range" id="tableZSlider" min="-200" max="200" value="${this.settings.tableZOffset}">
                        </div>
                        <div class="setting-row">
                            <label>Rotation: <span id="tableRotationValue">${this.settings.tableRotation}</span>°</label>
                            <input type="range" id="tableRotationSlider" min="-180" max="180" value="${this.settings.tableRotation}">
                        </div>
                    </div>
                </div>
                
                <!-- FELT COLOR -->
                <div class="settings-section">
                    <div class="section-header" data-section="felt">?? Felt Color <span class="collapse-icon">?</span></div>
                    <div class="section-content" id="feltSection">
                        <div class="color-preview" id="feltColorPreview" style="width: 100%; height: 30px; border-radius: 6px; margin-bottom: 8px; background: rgb(${this.settings.feltColorR}, ${this.settings.feltColorG}, ${this.settings.feltColorB});"></div>
                        <div class="setting-row">
                            <label>Red: <span id="feltRValue">${this.settings.feltColorR}</span></label>
                            <input type="range" id="feltRSlider" min="0" max="255" value="${this.settings.feltColorR}">
                        </div>
                        <div class="setting-row">
                            <label>Green: <span id="feltGValue">${this.settings.feltColorG}</span></label>
                            <input type="range" id="feltGSlider" min="0" max="255" value="${this.settings.feltColorG}">
                        </div>
                        <div class="setting-row">
                            <label>Blue: <span id="feltBValue">${this.settings.feltColorB}</span></label>
                            <input type="range" id="feltBSlider" min="0" max="255" value="${this.settings.feltColorB}">
                        </div>
                        <div class="preset-buttons">
                            <button class="preset-btn" data-felt="26,127,55">Green</button>
                            <button class="preset-btn" data-felt="25,80,150">Blue</button>
                            <button class="preset-btn" data-felt="140,30,30">Red</button>
                            <button class="preset-btn" data-felt="80,60,100">Purple</button>
                        </div>
                    </div>
                </div>
                
                <!-- CAMERA SETTINGS -->
                <div class="settings-section">
                    <div class="section-header" data-section="camera">?? Camera Settings <span class="collapse-icon">?</span></div>
                    <div class="section-content" id="cameraSection">
                        <div class="setting-row">
                            <label>FOV: <span id="cameraFOVValue">${this.settings.cameraFOV}</span>°</label>
                            <input type="range" id="cameraFOVSlider" min="20" max="90" value="${this.settings.cameraFOV}">
                        </div>
                        <div class="setting-row">
                            <label>Min Distance: <span id="cameraMinDistValue">${this.settings.cameraMinDistance}</span></label>
                            <input type="range" id="cameraMinDistSlider" min="100" max="800" value="${this.settings.cameraMinDistance}">
                        </div>
                        <div class="setting-row">
                            <label>Max Distance: <span id="cameraMaxDistValue">${this.settings.cameraMaxDistance}</span></label>
                            <input type="range" id="cameraMaxDistSlider" min="800" max="3000" value="${this.settings.cameraMaxDistance}">
                        </div>
                        <div class="setting-row">
                            <label>Damping: <span id="cameraDampingValue">${this.settings.cameraDamping}</span>%</label>
                            <input type="range" id="cameraDampingSlider" min="0" max="20" value="${this.settings.cameraDamping}">
                        </div>
                        <div class="preset-buttons">
                            <button class="preset-btn camera-preset" data-view="top">Top View</button>
                            <button class="preset-btn camera-preset" data-view="side">Side View</button>
                            <button class="preset-btn camera-preset" data-view="corner">Corner</button>
                            <button class="preset-btn camera-preset" data-view="player">Player</button>
                        </div>
                    </div>
                </div>
                
                <!-- LIGHTING SETTINGS -->
                <div class="settings-section">
                    <div class="section-header" data-section="lighting">?? Lighting Settings <span class="collapse-icon">?</span></div>
                    <div class="section-content" id="lightingSection">
                        <div class="setting-row">
                            <label>Ambient: <span id="ambientIntensityValue">${this.settings.ambientIntensity}</span>%</label>
                            <input type="range" id="ambientIntensitySlider" min="0" max="100" value="${this.settings.ambientIntensity}">
                        </div>
                        <div class="setting-row">
                            <label>Main Light: <span id="mainLightIntensityValue">${this.settings.mainLightIntensity}</span>%</label>
                            <input type="range" id="mainLightIntensitySlider" min="0" max="400" value="${this.settings.mainLightIntensity}">
                        </div>
                        <div class="setting-row">
                            <label>Fill Lights: <span id="fillLightIntensityValue">${this.settings.fillLightIntensity}</span>%</label>
                            <input type="range" id="fillLightIntensitySlider" min="0" max="100" value="${this.settings.fillLightIntensity}">
                        </div>
                        <div class="setting-row">
                            <label>Lamp: <span id="lampLightIntensityValue">${this.settings.lampLightIntensity}</span>%</label>
                            <input type="range" id="lampLightIntensitySlider" min="0" max="300" value="${this.settings.lampLightIntensity}">
                        </div>
                        <div class="setting-row">
                            <label>Shadow Softness: <span id="shadowSoftnessValue">${this.settings.shadowSoftness}</span>%</label>
                            <input type="range" id="shadowSoftnessSlider" min="0" max="100" value="${this.settings.shadowSoftness}">
                        </div>
                        <div class="preset-buttons">
                            <button class="preset-btn lighting-preset" data-lighting="bar">Bar</button>
                            <button class="preset-btn lighting-preset" data-lighting="bright">Bright</button>
                            <button class="preset-btn lighting-preset" data-lighting="dramatic">Dramatic</button>
                            <button class="preset-btn lighting-preset" data-lighting="soft">Soft</button>
                        </div>
                    </div>
                </div>
                
                <!-- ENVIRONMENT SETTINGS -->
                <div class="settings-section">
                    <div class="section-header" data-section="environment">?? Environment <span class="collapse-icon">?</span></div>
                    <div class="section-content" id="environmentSection">
                        <div class="setting-row">
                            <label>Exposure: <span id="exposureValue">${this.settings.exposure}</span>%</label>
                            <input type="range" id="exposureSlider" min="50" max="200" value="${this.settings.exposure}">
                        </div>
                        <div class="setting-row">
                            <label>Background: <span id="backgroundDarknessValue">${this.settings.backgroundDarkness}</span>%</label>
                            <input type="range" id="backgroundDarknessSlider" min="0" max="100" value="${this.settings.backgroundDarkness}">
                        </div>
                        <div class="setting-row">
                            <label>Floor Reflect: <span id="floorReflectivityValue">${this.settings.floorReflectivity}</span>%</label>
                            <input type="range" id="floorReflectivitySlider" min="0" max="100" value="${this.settings.floorReflectivity}">
                        </div>
                    </div>
                </div>
                
                <!-- QUALITY SETTINGS -->
                <div class="settings-section">
                    <div class="section-header" data-section="quality">?? Quality <span class="collapse-icon">?</span></div>
                    <div class="section-content" id="qualitySection">
                        <div class="setting-row">
                            <label>Shadow Quality:</label>
                            <select id="shadowQualitySelect" style="width: 100%; margin-top: 3px; padding: 4px; border-radius: 4px; background: #333; color: white; border: 1px solid #555; font-size: 10px;">
                                <option value="512" ${this.settings.shadowQuality === 512 ? 'selected' : ''}>Low</option>
                                <option value="1024" ${this.settings.shadowQuality === 1024 ? 'selected' : ''}>Medium</option>
                                <option value="2048" ${this.settings.shadowQuality === 2048 ? 'selected' : ''}>High</option>
                                <option value="4096" ${this.settings.shadowQuality === 4096 ? 'selected' : ''}>Ultra</option>
                            </select>
                        </div>
                        <div class="setting-row" style="display: flex; align-items: center; gap: 8px;">
                            <label style="flex: 1;">Anti-aliasing:</label>
                            <input type="checkbox" id="antialiasCheckbox" ${this.settings.antialiasing ? 'checked' : ''} style="width: 16px; height: 16px;">
                        </div>
                    </div>
                </div>
                
                <!-- ACTION BUTTONS -->
                <div style="margin-top: 10px; padding-top: 10px; border-top: 1px solid rgba(255,255,255,0.1);">
                    <div style="display: grid; grid-template-columns: 1fr 1fr 1fr 1fr; gap: 5px;">
                        <button id="resetSettingsBtn" class="action-btn" style="background: #444;" title="Reset All">??</button>
                        <button id="saveSettingsBtn" class="action-btn" style="background: #2d5a3d;" title="Save">??</button>
                        <button id="exportSettingsBtn" class="action-btn" style="background: #444;" title="Export">??</button>
                        <button id="importSettingsBtn" class="action-btn" style="background: #444;" title="Import">??</button>
                    </div>
                </div>
                
                <div style="font-size: 9px; color: #555; margin-top: 8px; text-align: center;">
                    Mouse: Rotate | Scroll: Zoom | Shift+3: Toggle
                </div>
            </div>
            
            <style>
                #threejs-controls .settings-section {
                    margin-bottom: 6px;
                    border: 1px solid rgba(255,255,255,0.1);
                    border-radius: 6px;
                    overflow: hidden;
                }
                #threejs-controls .section-header {
                    padding: 8px 10px;
                    background: rgba(255,255,255,0.05);
                    cursor: pointer;
                    user-select: none;
                    display: flex;
                    justify-content: space-between;
                    align-items: center;
                    font-weight: 500;
                    font-size: 11px;
                    transition: background 0.2s;
                }
                #threejs-controls .section-header:hover {
                    background: rgba(255,255,255,0.1);
                }
                #threejs-controls .section-content {
                    padding: 8px 10px;
                    background: rgba(0,0,0,0.2);
                    display: none;
                }
                #threejs-controls .section-content.expanded {
                    display: block;
                }
                #threejs-controls .collapse-icon {
                    transition: transform 0.3s;
                    font-size: 10px;
                }
                #threejs-controls .setting-row {
                    margin-bottom: 8px;
                }
                #threejs-controls .setting-row:last-child {
                    margin-bottom: 0;
                }
                #threejs-controls .setting-row label {
                    display: flex;
                    justify-content: space-between;
                    margin-bottom: 3px;
                    color: #ccc;
                    font-size: 10px;
                }
                #threejs-controls input[type="range"] {
                    width: 100%;
                    height: 5px;
                    -webkit-appearance: none;
                    background: linear-gradient(to right, #2d5a3d 0%, #2d5a3d 50%, #333 50%, #333 100%);
                    border-radius: 3px;
                    cursor: pointer;
                }
                #threejs-controls input[type="range"]::-webkit-slider-thumb {
                    -webkit-appearance: none;
                    width: 12px;
                    height: 12px;
                    background: #4ade80;
                    border-radius: 50%;
                    cursor: pointer;
                    box-shadow: 0 2px 4px rgba(0,0,0,0.3);
                }
                #threejs-controls .preset-buttons {
                    display: grid;
                    grid-template-columns: repeat(4, 1fr);
                    gap: 4px;
                    margin-top: 8px;
                }
                #threejs-controls .preset-btn {
                    padding: 5px 3px;
                    font-size: 9px;
                    background: #333;
                    border: 1px solid #555;
                    color: white;
                    border-radius: 4px;
                    cursor: pointer;
                    transition: all 0.2s;
                }
                #threejs-controls .preset-btn:hover {
                    background: #444;
                    border-color: #4ade80;
                }
                #threejs-controls .action-btn {
                    padding: 8px;
                    font-size: 11px;
                    border: none;
                    color: white;
                    border-radius: 5px;
                    cursor: pointer;
                    transition: all 0.2s;
                }
                #threejs-controls .action-btn:hover {
                    filter: brightness(1.2);
                }
                #settingsContent::-webkit-scrollbar {
                    width: 6px;
                }
                #settingsContent::-webkit-scrollbar-track {
                    background: rgba(0,0,0,0.2);
                }
                #settingsContent::-webkit-scrollbar-thumb {
                    background: #444;
                    border-radius: 3px;
                }
                #settingsContent::-webkit-scrollbar-thumb:hover {
                    background: #555;
                }
            </style>
        `;
        
        this.container.appendChild(panel);
        
        // Panel collapse/expand
        const header = document.getElementById('settingsHeader');
        const content = document.getElementById('settingsContent');
        const toggleIcon = document.getElementById('toggleIcon');
        let panelCollapsed = false;
        
        header.addEventListener('click', (e) => {
            // Prevent collapsing if clicking inside content
            if (e.target.closest('#settingsContent')) return;
            panelCollapsed = !panelCollapsed;
            content.style.display = panelCollapsed ? 'none' : 'flex';
            content.style.flexDirection = 'column';
            toggleIcon.style.transform = panelCollapsed ? 'rotate(-90deg)' : 'rotate(0deg)';
        });
        
        // Section collapse/expand - all start collapsed, click to expand
        document.querySelectorAll('#threejs-controls .section-header').forEach(sectionHeader => {
            const icon = sectionHeader.querySelector('.collapse-icon');
            // Start with collapsed icon
            icon.style.transform = 'rotate(-90deg)';
            
            sectionHeader.addEventListener('click', (e) => {
                e.stopPropagation();
                const sectionId = sectionHeader.dataset.section + 'Section';
                const sectionContent = document.getElementById(sectionId);
                const icon = sectionHeader.querySelector('.collapse-icon');
                if (sectionContent) {
                    const isExpanded = sectionContent.classList.contains('expanded');
                    sectionContent.classList.toggle('expanded');
                    icon.style.transform = isExpanded ? 'rotate(-90deg)' : 'rotate(0deg)';
                }
            });
        });
        
        // Ball settings
        this.setupSlider('ballHeightSlider', 'ballHeightValue', (val) => {
            this.settings.ballHeight = val;
            this.playingSurfaceY = val;
        });
        
        this.setupSlider('ballSizeSlider', 'ballSizeValue', (val) => {
            this.settings.ballSize = val;
            this.updateBallSizes(val / 100);
        }, '%');
        
        this.setupSlider('ballRoughnessSlider', 'ballRoughnessValue', (val) => {
            this.settings.ballRoughness = val;
            this.updateBallMaterials();
        }, '%');
        
        this.setupSlider('ballMetalnessSlider', 'ballMetalnessValue', (val) => {
            this.settings.ballMetalness = val;
            this.updateBallMaterials();
        }, '%');
        
        // Table settings
        this.setupSlider('tableScaleSlider', 'tableScaleValue', (val) => {
            this.settings.tableScale = val;
            this.updateTableScale();
        }, '%');
        
        this.setupSlider('tableWidthSlider', 'tableWidthValue', (val) => {
            this.settings.tableWidthScale = val;
            this.updateTableScale();
        }, '%');
        
        this.setupSlider('tableLengthSlider', 'tableLengthValue', (val) => {
            this.settings.tableLengthScale = val;
            this.updateTableScale();
        }, '%');
        
        this.setupSlider('tableYSlider', 'tableYValue', (val) => {
            this.settings.tableYOffset = val;
            if (this.tableModel && this.originalTableY !== undefined) {
                this.tableModel.position.y = this.originalTableY + val;
            }
        });
        
        this.setupSlider('tableXSlider', 'tableXValue', (val) => {
            this.settings.tableXOffset = val;
            if (this.tableModel) {
                this.tableModel.position.x = this.tableWidth / 2 + val;
            }
        });
        
        this.setupSlider('tableZSlider', 'tableZValue', (val) => {
            this.settings.tableZOffset = val;
            if (this.tableModel) {
                this.tableModel.position.z = this.tableHeight / 2 + val;
            }
        });
        
        this.setupSlider('tableRotationSlider', 'tableRotationValue', (val) => {
            this.settings.tableRotation = val;
            if (this.tableModel) {
                this.tableModel.rotation.y = (Math.PI / 2) + (val * Math.PI / 180);
            }
        }, '°');
        
        // Felt color settings
        const updateFeltColor = () => {
            const r = this.settings.feltColorR;
            const g = this.settings.feltColorG;
            const b = this.settings.feltColorB;
            document.getElementById('feltColorPreview').style.background = `rgb(${r}, ${g}, ${b})`;
            this.updateFeltColor(r, g, b);
        };
        
        this.setupSlider('feltRSlider', 'feltRValue', (val) => {
            this.settings.feltColorR = val;
            updateFeltColor();
        });
        
        this.setupSlider('feltGSlider', 'feltGValue', (val) => {
            this.settings.feltColorG = val;
            updateFeltColor();
        });
        
        this.setupSlider('feltBSlider', 'feltBValue', (val) => {
            this.settings.feltColorB = val;
            updateFeltColor();
        });
        
        // Felt presets
        document.querySelectorAll('[data-felt]').forEach(btn => {
            btn.addEventListener('click', () => {
                const [r, g, b] = btn.dataset.felt.split(',').map(Number);
                this.settings.feltColorR = r;
                this.settings.feltColorG = g;
                this.settings.feltColorB = b;
                document.getElementById('feltRSlider').value = r;
                document.getElementById('feltGSlider').value = g;
                document.getElementById('feltBSlider').value = b;
                document.getElementById('feltRValue').textContent = r;
                document.getElementById('feltGValue').textContent = g;
                document.getElementById('feltBValue').textContent = b;
                updateFeltColor();
            });
        });
        
        // Camera settings
        this.setupSlider('cameraFOVSlider', 'cameraFOVValue', (val) => {
            this.settings.cameraFOV = val;
            if (this.camera) {
                this.camera.fov = val;
                this.camera.updateProjectionMatrix();
            }
        }, '°');
        
        this.setupSlider('cameraMinDistSlider', 'cameraMinDistValue', (val) => {
            this.settings.cameraMinDistance = val;
            if (this.controls) this.controls.minDistance = val;
        });
        
        this.setupSlider('cameraMaxDistSlider', 'cameraMaxDistValue', (val) => {
            this.settings.cameraMaxDistance = val;
            if (this.controls) this.controls.maxDistance = val;
        });
        
        this.setupSlider('cameraDampingSlider', 'cameraDampingValue', (val) => {
            this.settings.cameraDamping = val;
            if (this.controls) this.controls.dampingFactor = val / 100;
        }, '%');
        
        // Camera presets
        document.querySelectorAll('.camera-preset').forEach(btn => {
            btn.addEventListener('click', () => {
                this.setCameraPreset(btn.dataset.view);
            });
        });
        
        // Lighting settings
        this.setupSlider('ambientIntensitySlider', 'ambientIntensityValue', (val) => {
            this.settings.ambientIntensity = val;
            this.updateLighting();
        }, '%');
        
        this.setupSlider('mainLightIntensitySlider', 'mainLightIntensityValue', (val) => {
            this.settings.mainLightIntensity = val;
            this.updateLighting();
        }, '%');
        
        this.setupSlider('fillLightIntensitySlider', 'fillLightIntensityValue', (val) => {
            this.settings.fillLightIntensity = val;
            this.updateLighting();
        }, '%');
        
        this.setupSlider('lampLightIntensitySlider', 'lampLightIntensityValue', (val) => {
            this.settings.lampLightIntensity = val;
            this.updateLighting();
        }, '%');
        
        this.setupSlider('shadowSoftnessSlider', 'shadowSoftnessValue', (val) => {
            this.settings.shadowSoftness = val;
            this.updateLighting();
        }, '%');
        
        // Lighting presets
        document.querySelectorAll('.lighting-preset').forEach(btn => {
            btn.addEventListener('click', () => {
                this.setLightingPreset(btn.dataset.lighting);
            });
        });
        
        // Environment settings
        this.setupSlider('exposureSlider', 'exposureValue', (val) => {
            this.settings.exposure = val;
            if (this.renderer) this.renderer.toneMappingExposure = val / 100;
        }, '%');
        
        this.setupSlider('backgroundDarknessSlider', 'backgroundDarknessValue', (val) => {
            this.settings.backgroundDarkness = val;
            this.updateBackground(val);
        }, '%');
        
        this.setupSlider('floorReflectivitySlider', 'floorReflectivityValue', (val) => {
            this.settings.floorReflectivity = val;
            this.updateFloorReflectivity(val);
        }, '%');
        
        // Quality settings
        document.getElementById('shadowQualitySelect').addEventListener('change', (e) => {
            this.settings.shadowQuality = parseInt(e.target.value);
            this.updateShadowQuality(this.settings.shadowQuality);
        });
        
        document.getElementById('antialiasCheckbox').addEventListener('change', (e) => {
            this.settings.antialiasing = e.target.checked;
            // Note: Requires renderer recreation
            console.log('[Settings] Anti-aliasing toggled (requires restart)');
        });
        
        // Action buttons
        document.getElementById('resetSettingsBtn').addEventListener('click', () => {
            this.resetSettings();
        });
        
        document.getElementById('saveSettingsBtn').addEventListener('click', () => {
            this.saveSettings();
        });
        
        document.getElementById('exportSettingsBtn').addEventListener('click', () => {
            this.exportSettings();
        });
        
        document.getElementById('importSettingsBtn').addEventListener('click', () => {
            this.importSettings();
        });
        
        // Load saved settings
        this.loadSettings();
        
        this.controlPanel = panel;
    },
    
    updateTableScale: function() {
        if (this.tableModel) {
            const baseScale = this.tableWidth / 127;
            const overallScale = this.settings.tableScale / 100;
            const widthScale = this.settings.tableWidthScale / 100;
            const lengthScale = this.settings.tableLengthScale / 100;
            
            // X = width (after rotation), Y = height, Z = length
            const scaleX = baseScale * overallScale * widthScale;
            const scaleY = baseScale * overallScale;
            const scaleZ = baseScale * overallScale * lengthScale;
            
            this.tableModel.scale.set(scaleX, scaleY, scaleZ);
        }
    },
    
    setupSlider: function(sliderId, valueId, callback, suffix = '') {
        const slider = document.getElementById(sliderId);
        if (slider) {
            slider.addEventListener('input', (e) => {
                const val = parseFloat(e.target.value);
                document.getElementById(valueId).textContent = val + suffix;
                callback(val);
                this.updateSliderGradient(slider, val);
            });
            // Initialize gradient
            this.updateSliderGradient(slider, parseFloat(slider.value));
        }
    },
    
    updateSliderGradient: function(slider, value) {
        const min = parseFloat(slider.min);
        const max = parseFloat(slider.max);
        const percent = ((value - min) / (max - min)) * 100;
        slider.style.background = `linear-gradient(to right, #4ade80 0%, #4ade80 ${percent}%, #333 ${percent}%, #333 100%)`;
    },
    
    updateBallSizes: function(scale) {
        Object.values(this.ballMeshes).forEach(mesh => {
            if (mesh && mesh.geometry) {
                const originalRadius = 14; // Default ball radius
                mesh.scale.set(scale, scale, scale);
            }
        });
    },
    
    updateBallMaterials: function() {
        const roughness = this.settings.ballRoughness / 100;
        const metalness = this.settings.ballMetalness / 100;
        Object.values(this.ballMeshes).forEach(mesh => {
            if (mesh && mesh.material) {
                mesh.material.roughness = roughness;
                mesh.material.metalness = metalness;
                mesh.material.needsUpdate = true;
            }
        });
    },
    
    updateFeltColor: function(r, g, b) {
        if (this.table || this.tableModel) {
            const target = this.table || this.tableModel;
            target.traverse((child) => {
                if (child.isMesh && child.material) {
                    const mat = child.material;
                    // Check if this looks like felt (green-ish material)
                    if (mat.color && mat.roughness > 0.7) {
                        mat.color.setRGB(r / 255, g / 255, b / 255);
                        mat.needsUpdate = true;
                    }
                }
            });
        }
    },
    
    updateLighting: function() {
        if (!this.lights || this.lights.length === 0) return;
        
        this.lights.forEach((light, index) => {
            if (light.isAmbientLight) {
                light.intensity = this.settings.ambientIntensity / 100;
            } else if (light.isSpotLight && index === 1) {
                light.intensity = this.settings.mainLightIntensity / 100;
                light.penumbra = this.settings.shadowSoftness / 100;
            } else if (light.isPointLight) {
                light.intensity = this.settings.fillLightIntensity / 100;
            }
        });
    },
    
    setCameraPreset: function(preset) {
        if (!this.camera || !this.controls) return;
        
        const presets = {
            top: { pos: [500, 1200, 250], target: [500, 0, 250] },
            side: { pos: [500, 300, 900], target: [500, 100, 250] },
            corner: { pos: [1100, 600, 700], target: [500, 50, 250] },
            player: { pos: [500, 350, 800], target: [500, 100, 250] }
        };
        
        const p = presets[preset];
        if (p) {
            this.camera.position.set(...p.pos);
            this.controls.target.set(...p.target);
            this.controls.update();
        }
    },
    
    setLightingPreset: function(preset) {
        const presets = {
            bar: { ambient: 20, main: 200, fill: 40, lamp: 150, shadow: 40 },
            bright: { ambient: 50, main: 300, fill: 60, lamp: 200, shadow: 20 },
            dramatic: { ambient: 10, main: 350, fill: 20, lamp: 200, shadow: 60 },
            soft: { ambient: 40, main: 150, fill: 50, lamp: 100, shadow: 70 }
        };
        
        const p = presets[preset];
        if (p) {
            this.settings.ambientIntensity = p.ambient;
            this.settings.mainLightIntensity = p.main;
            this.settings.fillLightIntensity = p.fill;
            this.settings.lampLightIntensity = p.lamp;
            this.settings.shadowSoftness = p.shadow;
            
            // Update sliders
            document.getElementById('ambientIntensitySlider').value = p.ambient;
            document.getElementById('mainLightIntensitySlider').value = p.main;
            document.getElementById('fillLightIntensitySlider').value = p.fill;
            document.getElementById('lampLightIntensitySlider').value = p.lamp;
            document.getElementById('shadowSoftnessSlider').value = p.shadow;
            
            document.getElementById('ambientIntensityValue').textContent = p.ambient + '%';
            document.getElementById('mainLightIntensityValue').textContent = p.main + '%';
            document.getElementById('fillLightIntensityValue').textContent = p.fill + '%';
            document.getElementById('lampLightIntensityValue').textContent = p.lamp + '%';
            document.getElementById('shadowSoftnessValue').textContent = p.shadow + '%';
            
            this.updateLighting();
        }
    },
    
    updateBackground: function(darkness) {
        if (!this.scene) return;
        const val = Math.round(255 * (1 - darkness / 100));
        const color = new THREE.Color(`rgb(${val * 0.1}, ${val * 0.15}, ${val * 0.2})`);
        this.scene.background = color;
    },
    
    updateFloorReflectivity: function(reflectivity) {
        if (this.scene) {
            this.scene.traverse((child) => {
                if (child.isMesh && child.geometry && child.geometry.type === 'PlaneGeometry') {
                    if (child.material) {
                        child.material.metalness = reflectivity / 100;
                        child.material.needsUpdate = true;
                    }
                }
            });
        }
    },
    
    updateShadowQuality: function(quality) {
        this.lights.forEach(light => {
            if (light.shadow && light.shadow.mapSize) {
                light.shadow.mapSize.width = quality;
                light.shadow.mapSize.height = quality;
                if (light.shadow.map) {
                    light.shadow.map.dispose();
                    light.shadow.map = null;
                }
            }
        });
    },
    
    resetSettings: function() {
        this.settings = {
            ballHeight: 15, ballSize: 100, ballRoughness: 15, ballMetalness: 5,
            tableScale: 100, tableWidthScale: 100, tableLengthScale: 100, tableYOffset: 0, tableXOffset: 0, tableZOffset: 0, tableRotation: 0,
            cameraFOV: 45, cameraMinDistance: 300, cameraMaxDistance: 1500, cameraDamping: 5,
            ambientIntensity: 20, mainLightIntensity: 200, fillLightIntensity: 40, lampLightIntensity: 150, shadowSoftness: 40,
            exposure: 120, backgroundDarkness: 50, floorReflectivity: 10,
            shadowQuality: 2048, antialiasing: true,
            feltColorR: 26, feltColorG: 127, feltColorB: 55
        };
        
        // Refresh the control panel
        if (this.controlPanel) {
            this.controlPanel.remove();
        }
        this.createControlPanel();
        
        // Apply default settings
        this.applyAllSettings();
        
        console.log('[Settings] Reset to defaults');
    },
    
    applyAllSettings: function() {
        // Apply all current settings to the scene
        this.playingSurfaceY = this.settings.ballHeight;
        this.updateBallMaterials();
        this.updateTableScale();
        this.updateLighting();
        if (this.renderer) this.renderer.toneMappingExposure = this.settings.exposure / 100;
        if (this.camera) {
            this.camera.fov = this.settings.cameraFOV;
            this.camera.updateProjectionMatrix();
        }
        if (this.controls) {
            this.controls.minDistance = this.settings.cameraMinDistance;
            this.controls.maxDistance = this.settings.cameraMaxDistance;
            this.controls.dampingFactor = this.settings.cameraDamping / 100;
        }
    },
    
    saveSettings: function() {
        try {
            localStorage.setItem('poolThreeJSSettings', JSON.stringify(this.settings));
            console.log('[Settings] Saved to localStorage');
            alert('Settings saved!');
        } catch (e) {
            console.error('[Settings] Failed to save:', e);
        }
    },
    
    loadSettings: function() {
        try {
            const saved = localStorage.getItem('poolThreeJSSettings');
            if (saved) {
                const loaded = JSON.parse(saved);
                Object.assign(this.settings, loaded);
                this.applyAllSettings();
                console.log('[Settings] Loaded from localStorage');
            }
        } catch (e) {
            console.error('[Settings] Failed to load:', e);
        }
    },
    
    exportSettings: function() {
        const dataStr = JSON.stringify(this.settings, null, 2);
        const blob = new Blob([dataStr], { type: 'application/json' });
        const url = URL.createObjectURL(blob);
        const a = document.createElement('a');
        a.href = url;
        a.download = 'pool3d-settings.json';
        a.click();
        URL.revokeObjectURL(url);
    },
    
    importSettings: function() {
        const input = document.createElement('input');
        input.type = 'file';
        input.accept = '.json';
        input.onchange = (e) => {
            const file = e.target.files[0];
            if (file) {
                const reader = new FileReader();
                reader.onload = (ev) => {
                    try {
                        const imported = JSON.parse(ev.target.result);
                        Object.assign(this.settings, imported);
                        if (this.controlPanel) {
                            this.controlPanel.remove();
                        }
                        this.createControlPanel();
                        this.applyAllSettings();
                        console.log('[Settings] Imported successfully');
                    } catch (err) {
                        alert('Failed to import settings: ' + err.message);
                    }
                };
                reader.readAsText(file);
            }
        };
        input.click();
    },
    
    setupRenderer: function() {
        this.renderer = new THREE.WebGLRenderer({ 
            antialias: this.antialias,
            alpha: true,
            powerPreference: 'high-performance',
            logarithmicDepthBuffer: true  // Reduces z-fighting
        });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.renderer.shadowMap.autoUpdate = true;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = 1.2;
        this.renderer.outputEncoding = THREE.sRGBEncoding;
        this.renderer.physicallyCorrectLights = true;  // More realistic light falloff
        this.container.appendChild(this.renderer.domElement);
    },
    
    setupScene: function() {
        this.scene = new THREE.Scene();
        
        // Environment/background - dark pool hall ambiance
        const canvas = document.createElement('canvas');
        canvas.width = 2;
        canvas.height = 512;
        const ctx = canvas.getContext('2d');
        const gradient = ctx.createLinearGradient(0, 0, 0, 512);
        gradient.addColorStop(0, '#1a2a3a');
        gradient.addColorStop(0.3, '#0f1a25');
        gradient.addColorStop(0.7, '#0a1015');
        gradient.addColorStop(1, '#050a0f');
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, 2, 512);
        
        const bgTexture = new THREE.CanvasTexture(canvas);
        bgTexture.magFilter = THREE.LinearFilter;
        this.scene.background = bgTexture;
        
        // Create bar room environment
        this.createBarRoom();
    },
    
    createBarRoom: function() {
        // Floor - dark wood
        const floorGeometry = new THREE.PlaneGeometry(3000, 2000);
        const floorCanvas = document.createElement('canvas');
        floorCanvas.width = 512;
        floorCanvas.height = 512;
        const floorCtx = floorCanvas.getContext('2d');
        
        // Wood grain pattern
        floorCtx.fillStyle = '#2a1810';
        floorCtx.fillRect(0, 0, 512, 512);
        for (let i = 0; i < 512; i += 32) {
            floorCtx.fillStyle = `rgba(${30 + Math.random() * 20}, ${15 + Math.random() * 10}, ${10 + Math.random() * 5}, 0.3)`;
            floorCtx.fillRect(0, i, 512, 16);
            // Add wood grain lines
            floorCtx.strokeStyle = 'rgba(0,0,0,0.1)';
            floorCtx.beginPath();
            floorCtx.moveTo(0, i + Math.random() * 5);
            for (let x = 0; x < 512; x += 10) {
                floorCtx.lineTo(x, i + Math.random() * 8);
            }
            floorCtx.stroke();
        }
        
        const floorTexture = new THREE.CanvasTexture(floorCanvas);
        floorTexture.wrapS = THREE.RepeatWrapping;
        floorTexture.wrapT = THREE.RepeatWrapping;
        floorTexture.repeat.set(10, 6);
        
        const floorMaterial = new THREE.MeshStandardMaterial({
            map: floorTexture,
            roughness: 0.8,
            metalness: 0.1
        });
        const floor = new THREE.Mesh(floorGeometry, floorMaterial);
        floor.rotation.x = -Math.PI / 2;
        floor.position.set(this.tableWidth / 2, 0, this.tableHeight / 2);
        floor.receiveShadow = true;
        this.scene.add(floor);
        
        // Back wall - dark paneling
        const wallGeometry = new THREE.PlaneGeometry(3000, 800);
        const wallCanvas = document.createElement('canvas');
        wallCanvas.width = 512;
        wallCanvas.height = 256;
        const wallCtx = wallCanvas.getContext('2d');
        
        // Dark wood paneling
        wallCtx.fillStyle = '#1a1210';
        wallCtx.fillRect(0, 0, 512, 256);
        // Add vertical panels
        for (let x = 0; x < 512; x += 64) {
            wallCtx.fillStyle = 'rgba(40, 25, 15, 0.5)';
            wallCtx.fillRect(x + 2, 0, 60, 256);
            wallCtx.strokeStyle = 'rgba(0,0,0,0.3)';
            wallCtx.strokeRect(x + 2, 0, 60, 256);
        }
        
        const wallTexture = new THREE.CanvasTexture(wallCanvas);
        wallTexture.wrapS = THREE.RepeatWrapping;
        wallTexture.repeat.set(4, 1);
        
        const wallMaterial = new THREE.MeshStandardMaterial({
            map: wallTexture,
            roughness: 0.9,
            metalness: 0.0
        });
        
        // Back wall
        const backWall = new THREE.Mesh(wallGeometry, wallMaterial);
        backWall.position.set(this.tableWidth / 2, 400, -500);
        backWall.receiveShadow = true;
        this.scene.add(backWall);
        
        // Side walls
        const sideWallGeometry = new THREE.PlaneGeometry(2000, 800);
        const leftWall = new THREE.Mesh(sideWallGeometry, wallMaterial.clone());
        leftWall.rotation.y = Math.PI / 2;
        leftWall.position.set(-500, 400, this.tableHeight / 2);
        this.scene.add(leftWall);
        
        const rightWall = new THREE.Mesh(sideWallGeometry, wallMaterial.clone());
        rightWall.rotation.y = -Math.PI / 2;
        rightWall.position.set(this.tableWidth + 500, 400, this.tableHeight / 2);
        this.scene.add(rightWall);
        
        // Add some bar atmosphere - hanging lamp over table
        this.createPoolLamp();
        
        // Add baseboard trim
        const baseboardGeometry = new THREE.BoxGeometry(3000, 30, 10);
        const baseboardMaterial = new THREE.MeshStandardMaterial({ color: 0x1a0f08, roughness: 0.7 });
        const baseboard = new THREE.Mesh(baseboardGeometry, baseboardMaterial);
        baseboard.position.set(this.tableWidth / 2, 15, -495);
        this.scene.add(baseboard);
    },
    
    createPoolLamp: function() {
        // Classic pool table lamp shade
        const lampGroup = new THREE.Group();
        
        // Lamp shade (long rectangular)
        const shadeGeometry = new THREE.BoxGeometry(600, 30, 150);
        const shadeMaterial = new THREE.MeshStandardMaterial({
            color: 0x228B22, // Green shade
            roughness: 0.3,
            metalness: 0.2,
            side: THREE.DoubleSide
        });
        const shade = new THREE.Mesh(shadeGeometry, shadeMaterial);
        shade.position.y = 0;
        lampGroup.add(shade);
        
        // Gold trim
        const trimGeometry = new THREE.BoxGeometry(610, 5, 160);
        const trimMaterial = new THREE.MeshStandardMaterial({
            color: 0xB8860B,
            roughness: 0.3,
            metalness: 0.6
        });
        const topTrim = new THREE.Mesh(trimGeometry, trimMaterial);
        topTrim.position.y = 17;
        lampGroup.add(topTrim);
        
        const bottomTrim = new THREE.Mesh(trimGeometry, trimMaterial);
        bottomTrim.position.y = -17;
        lampGroup.add(bottomTrim);
        
        // Hanging chain/rod
        const rodGeometry = new THREE.CylinderGeometry(3, 3, 200, 8);
        const rodMaterial = new THREE.MeshStandardMaterial({ color: 0xB8860B, metalness: 0.7 });
        const rod = new THREE.Mesh(rodGeometry, rodMaterial);
        rod.position.y = 115;
        lampGroup.add(rod);
        
        // Position lamp above table
        lampGroup.position.set(this.tableWidth / 2, 550, this.tableHeight / 2);
        this.scene.add(lampGroup);
        
        // Add warm light from lamp (no shadows - main light handles shadows)
        const lampLight = new THREE.SpotLight(0xffe4b5, 1.2);
        lampLight.position.set(this.tableWidth / 2, 520, this.tableHeight / 2);
        lampLight.target.position.set(this.tableWidth / 2, 0, this.tableHeight / 2);
        lampLight.angle = Math.PI / 3.5;
        lampLight.penumbra = 0.6;
        lampLight.decay = 1.5;
        lampLight.distance = 1200;
        lampLight.castShadow = false;  // Disable to prevent flickering from multiple shadow casters
        this.scene.add(lampLight);
        this.scene.add(lampLight.target);
    },
    
    setupCamera: function() {
        const aspect = window.innerWidth / window.innerHeight;
        this.camera = new THREE.PerspectiveCamera(45, aspect, 10, 5000);  // Increased near plane
        this.updateCameraPosition();
    },
    
    updateCameraPosition: function() {
        const x = Math.sin(this.cameraAngle) * this.cameraDistance;
        const z = Math.cos(this.cameraAngle) * this.cameraDistance;
        this.camera.position.set(
            this.tableWidth / 2 + x, 
            this.cameraHeight, 
            this.tableHeight / 2 + z
        );
        this.camera.lookAt(this.tableWidth / 2, 200, this.tableHeight / 2);
    },
    
    setupLighting: function() {
        // Clear existing lights
        this.lights.forEach(light => this.scene.remove(light));
        this.lights = [];
        
        // Ambient light for base illumination (dimmer for bar atmosphere)
        const ambient = new THREE.AmbientLight(0xffffff, 0.3);
        this.scene.add(ambient);
        this.lights.push(ambient);
        
        // Main overhead light (pool table lamp style)
        const mainLight = new THREE.SpotLight(0xfff8e8, 2.5);
        mainLight.position.set(this.tableWidth / 2, 800, this.tableHeight / 2);
        mainLight.target.position.set(this.tableWidth / 2, 0, this.tableHeight / 2);
        mainLight.angle = Math.PI / 4;
        mainLight.penumbra = 0.5;
        mainLight.decay = 1.2;
        mainLight.distance = 2000;
        mainLight.castShadow = true;
        mainLight.shadow.mapSize.width = this.shadowMapSize;
        mainLight.shadow.mapSize.height = this.shadowMapSize;
        mainLight.shadow.camera.near = 200;
        mainLight.shadow.camera.far = 1500;
        mainLight.shadow.camera.fov = 50;
        mainLight.shadow.bias = -0.001;
        mainLight.shadow.normalBias = 0.02;
        mainLight.shadow.radius = 4;  // Soft shadow blur
        this.scene.add(mainLight);
        this.scene.add(mainLight.target);
        this.lights.push(mainLight);
        
        // Secondary fill lights for softer shadows (no shadow casting to reduce artifacts)
        const fillLight1 = new THREE.PointLight(0xffe4c4, 0.5);
        fillLight1.position.set(-200, 400, this.tableHeight / 2);
        fillLight1.castShadow = false;
        this.scene.add(fillLight1);
        this.lights.push(fillLight1);
        
        const fillLight2 = new THREE.PointLight(0xffe4c4, 0.5);
        fillLight2.position.set(this.tableWidth + 200, 400, this.tableHeight / 2);
        fillLight2.castShadow = false;
        this.scene.add(fillLight2);
        this.lights.push(fillLight2);
        
        // Rim light for definition (no shadow)
        const rimLight = new THREE.DirectionalLight(0xffffff, 0.3);
        rimLight.position.set(-500, 300, 0);
        rimLight.castShadow = false;
        this.scene.add(rimLight);
        this.lights.push(rimLight);
    },
    
    loadTableModel: async function() {
        this.updateLoadingProgress(10, 'Setting up GLTF loader...');
        
        // Initialize GLTF Loader
        if (typeof THREE.GLTFLoader === 'undefined') {
            console.error('[ThreeJS] GLTFLoader not available!');
            throw new Error('GLTFLoader not loaded');
        }
        this.gltfLoader = new THREE.GLTFLoader();
        console.log('[ThreeJS] GLTFLoader initialized');
        
        this.updateLoadingProgress(20, 'Loading 3D model...');
        
        // Try model URLs from GitHub
        // NOTE: If repo is private, these won't work - will fall back to procedural table
        const modelUrls = [
            'https://raw.githubusercontent.com/gazlappy/Wdplapp/refs/heads/master/wdpl2/Models/scene.gltf',
        ];
        
        for (const url of modelUrls) {
            try {
                console.log('[ThreeJS] Trying to load model from:', url);
                this.updateLoadingProgress(40, 'Downloading from GitHub...');
                await this.loadGLTF(url);
                this.modelLoaded = true;
                console.log('[ThreeJS] Model loaded successfully from:', url);
                return;
            } catch (error) {
                console.warn('[ThreeJS] Failed to load from:', url, error.message);
            }
        }
        
        // Fall back to procedural table
        console.log('[ThreeJS] Using procedural table (push model to GitHub for 3D model)');
        this.updateLoadingProgress(50, 'Building procedural table...');
        await this.createProceduralTable();
        this.modelLoaded = true;
    },
    
    loadGLTF: function(url) {
        return new Promise((resolve, reject) => {
            this.gltfLoader.load(
                url,
                (gltf) => {
                    console.log('[ThreeJS] GLTF model loaded successfully');
                    this.updateLoadingProgress(80, 'Processing model...');
                    
                    const model = gltf.scene;
                    
                    // Calculate bounding box to scale and position correctly
                    const box = new THREE.Box3().setFromObject(model);
                    const size = box.getSize(new THREE.Vector3());
                    const center = box.getCenter(new THREE.Vector3());
                    
                    console.log('[ThreeJS] Model size:', size);
                    console.log('[ThreeJS] Model center:', center);
                    
                    // The model's long axis (Z=127) needs to be our X axis (1000)
                    // Rotate 90 degrees around Y to swap X and Z
                    model.rotation.y = Math.PI / 2;
                    
                    // Scale to fit our 1000x500 game dimensions
                    // After rotation, the model's Z becomes X, so use size.z for scaling
                    const scale = this.tableWidth / size.z;
                    
                    console.log('[ThreeJS] Applying scale:', scale);
                    
                    model.scale.set(scale, scale, scale);
                    
                    // Recalculate bounding box after rotation and scaling
                    const scaledBox = new THREE.Box3().setFromObject(model);
                    const scaledMin = scaledBox.min;
                    const scaledMax = scaledBox.max;
                    const scaledSize = scaledBox.getSize(new THREE.Vector3());
                    
                    console.log('[ThreeJS] Scaled bounds:', scaledMin, 'to', scaledMax);
                    console.log('[ThreeJS] Scaled size:', scaledSize);
                    
                    // Position the model so its playing area aligns with game coordinates
                    // Game uses (0,0) to (1000, 500) for ball positions
                    model.position.x = this.tableWidth / 2 - (scaledMin.x + scaledMax.x) / 2;
                    model.position.y = -scaledMin.y; // Put bottom of table at y=0
                    model.position.z = this.tableHeight / 2 - (scaledMin.z + scaledMax.z) / 2;
                    
                    // Store original Y position for control panel adjustments
                    this.originalTableY = model.position.y;
                    
                    console.log('[ThreeJS] Model position:', model.position);
                    
                    // Playing surface height - where the balls should sit on the felt
                    // Based on scaled table height, felt is just below the top
                    this.playingSurfaceY = (scaledMax.y - scaledMin.y) - 40; // Slightly below the top
                    console.log('[ThreeJS] Playing surface Y:', this.playingSurfaceY);
                    
                    // Enable shadows on all meshes
                    model.traverse((child) => {
                        if (child.isMesh) {
                            child.castShadow = true;
                            child.receiveShadow = true;
                            
                            // Hide the model's built-in balls (we use our own)
                            if (child.name && (child.name.toLowerCase().includes('sphere') || 
                                              child.name.toLowerCase().includes('ball') ||
                                              child.name.includes('Duplicate'))) {
                                child.visible = false;
                            }
                        }
                    });
                    
                    this.tableModel = model;
                    this.scene.add(model);
                    
                    
                    
                    
                    
                    // Add floor
                    this.addFloor();
                    
                    this.updateLoadingProgress(100, 'Complete!');
                    resolve(gltf);
                },
                (progress) => {
                    if (progress.total > 0) {
                        const percent = 20 + (progress.loaded / progress.total) * 60;
                        this.updateLoadingProgress(percent, `Loading: ${Math.round(progress.loaded / 1024)}KB`);
                    }
                },
                (error) => {
                    reject(error);
                }
            );
        });
    },
    
    addFloor: function() {
        const floorGeometry = new THREE.PlaneGeometry(3000, 3000);
        const floorMaterial = new THREE.MeshStandardMaterial({
            color: 0x1a1a1a,
            roughness: 0.8,
            metalness: 0.1
        });
        const floor = new THREE.Mesh(floorGeometry, floorMaterial);
        floor.rotation.x = -Math.PI / 2;
        floor.position.set(this.tableWidth / 2, -230, this.tableHeight / 2);
        floor.receiveShadow = true;
        this.scene.add(floor);
    },
    
    createProceduralTable: async function() {
        this.updateLoadingProgress(30, 'Creating table frame...');
        
        const tableGroup = new THREE.Group();
        tableGroup.name = 'poolTable';
        
        // ===== TABLE FRAME (Mahogany wood) =====
        const frameHeight = 80;
        const frameWidth = 50;
        const legHeight = 150;
        
        const woodMaterial = this.createWoodMaterial('#5D3A1A', '#8B4513');
        
        // Main frame
        const frameGeometry = new THREE.BoxGeometry(
            this.tableWidth + frameWidth * 2, 
            frameHeight, 
            this.tableHeight + frameWidth * 2
        );
        const outerFrame = new THREE.Mesh(frameGeometry, woodMaterial);
        outerFrame.position.set(this.tableWidth / 2, -frameHeight / 2, this.tableHeight / 2);
        outerFrame.receiveShadow = true;
        outerFrame.castShadow = true;
        tableGroup.add(outerFrame);
        
        this.updateLoadingProgress(40, 'Creating rails...');
        
        // Top rails
        const railMaterial = this.createWoodMaterial('#6B4423', '#8B5A2B');
        const railHeight = 25;
        const railWidth = 35;
        
        const northRail = this.createRail(this.tableWidth - 100, railHeight, railWidth, railMaterial);
        northRail.position.set(this.tableWidth / 2, railHeight / 2, railWidth / 2);
        tableGroup.add(northRail);
        
        const southRail = this.createRail(this.tableWidth - 100, railHeight, railWidth, railMaterial);
        southRail.position.set(this.tableWidth / 2, railHeight / 2, this.tableHeight - railWidth / 2);
        tableGroup.add(southRail);
        
        const eastRail = this.createRail(railWidth, railHeight, this.tableHeight - 100, railMaterial);
        eastRail.position.set(this.tableWidth - railWidth / 2, railHeight / 2, this.tableHeight / 2);
        tableGroup.add(eastRail);
        
        const westRail = this.createRail(railWidth, railHeight, this.tableHeight - 100, railMaterial);
        westRail.position.set(railWidth / 2, railHeight / 2, this.tableHeight / 2);
        tableGroup.add(westRail);
        
        this.updateLoadingProgress(50, 'Creating legs...');
        
        // Table legs
        const legGeometry = new THREE.CylinderGeometry(25, 30, legHeight, 16);
        const legMaterial = this.createWoodMaterial('#4A3520', '#6B4423');
        
        const legPositions = [
            [frameWidth, 0, frameWidth],
            [this.tableWidth + frameWidth, 0, frameWidth],
            [frameWidth, 0, this.tableHeight + frameWidth],
            [this.tableWidth + frameWidth, 0, this.tableHeight + frameWidth]
        ];
        
        legPositions.forEach(pos => {
            const leg = new THREE.Mesh(legGeometry, legMaterial);
            leg.position.set(pos[0], -frameHeight - legHeight / 2, pos[2]);
            leg.castShadow = true;
            leg.receiveShadow = true;
            tableGroup.add(leg);
        });
        
        this.updateLoadingProgress(60, 'Creating felt surface...');
        
        // Felt playing surface
        const feltGeometry = new THREE.PlaneGeometry(this.tableWidth - 60, this.tableHeight - 60, 64, 32);
        const feltMaterial = this.createFeltMaterial();
        const felt = new THREE.Mesh(feltGeometry, feltMaterial);
        felt.rotation.x = -Math.PI / 2;
        felt.position.set(this.tableWidth / 2, 1, this.tableHeight / 2);
        felt.receiveShadow = true;
        tableGroup.add(felt);
        
        this.updateLoadingProgress(70, 'Creating cushions...');
        
        // Cushions
        const cushionMaterial = this.createCushionMaterial();
        const cushionHeight = 20;
        const cushionWidth = 25;
        
        const topCushion = this.createCushion(this.tableWidth - 140, cushionHeight, cushionWidth, cushionMaterial);
        topCushion.position.set(this.tableWidth / 2, cushionHeight / 2 + 1, cushionWidth / 2 + 30);
        tableGroup.add(topCushion);
        
        const bottomCushion = this.createCushion(this.tableWidth - 140, cushionHeight, cushionWidth, cushionMaterial);
        bottomCushion.position.set(this.tableWidth / 2, cushionHeight / 2 + 1, this.tableHeight - cushionWidth / 2 - 30);
        tableGroup.add(bottomCushion);
        
        const leftCushion = this.createCushion(cushionWidth, cushionHeight, this.tableHeight - 140, cushionMaterial);
        leftCushion.position.set(cushionWidth / 2 + 30, cushionHeight / 2 + 1, this.tableHeight / 2);
        tableGroup.add(leftCushion);
        
        const rightCushion = this.createCushion(cushionWidth, cushionHeight, this.tableHeight - 140, cushionMaterial);
        rightCushion.position.set(this.tableWidth - cushionWidth / 2 - 30, cushionHeight / 2 + 1, this.tableHeight / 2);
        tableGroup.add(rightCushion);
        
        this.updateLoadingProgress(80, 'Creating pockets...');
        
        // Pockets
        this.createPockets(tableGroup);
        
        this.updateLoadingProgress(90, 'Adding floor...');
        
        // Floor
        const floorGeometry = new THREE.PlaneGeometry(3000, 3000);
        const floorMaterial = new THREE.MeshStandardMaterial({
            color: 0x1a1a1a,
            roughness: 0.8,
            metalness: 0.1
        });
        const floor = new THREE.Mesh(floorGeometry, floorMaterial);
        floor.rotation.x = -Math.PI / 2;
        floor.position.set(this.tableWidth / 2, -frameHeight - legHeight - 1, this.tableHeight / 2);
        floor.receiveShadow = true;
        tableGroup.add(floor);
        
        this.table = tableGroup;
        this.scene.add(tableGroup);
        
        this.updateLoadingProgress(100, 'Complete!');
        this.modelLoaded = true;
    },
    
    createWoodMaterial: function(colorDark, colorLight) {
        const canvas = document.createElement('canvas');
        canvas.width = 512;
        canvas.height = 512;
        const ctx = canvas.getContext('2d');
        
        ctx.fillStyle = colorDark;
        ctx.fillRect(0, 0, 512, 512);
        
        ctx.strokeStyle = colorLight;
        ctx.lineWidth = 2;
        for (let i = 0; i < 100; i++) {
            ctx.beginPath();
            const y = Math.random() * 512;
            ctx.moveTo(0, y + Math.sin(0) * 10);
            for (let x = 0; x < 512; x += 10) {
                ctx.lineTo(x, y + Math.sin(x * 0.02) * 10 + (Math.random() - 0.5) * 5);
            }
            ctx.stroke();
        }
        
        const woodTexture = new THREE.CanvasTexture(canvas);
        woodTexture.wrapS = THREE.RepeatWrapping;
        woodTexture.wrapT = THREE.RepeatWrapping;
        woodTexture.repeat.set(2, 2);
        
        return new THREE.MeshStandardMaterial({
            map: woodTexture,
            roughness: 0.6,
            metalness: 0.05,
            bumpScale: 0.02
        });
    },
    
    createFeltMaterial: function() {
        const canvas = document.createElement('canvas');
        canvas.width = 256;
        canvas.height = 256;
        const ctx = canvas.getContext('2d');
        
        ctx.fillStyle = '#1a7f37';
        ctx.fillRect(0, 0, 256, 256);
        
        const imageData = ctx.getImageData(0, 0, 256, 256);
        for (let i = 0; i < imageData.data.length; i += 4) {
            const noise = (Math.random() - 0.5) * 15;
            imageData.data[i] = Math.min(255, Math.max(0, imageData.data[i] + noise));
            imageData.data[i + 1] = Math.min(255, Math.max(0, imageData.data[i + 1] + noise));
            imageData.data[i + 2] = Math.min(255, Math.max(0, imageData.data[i + 2] + noise));
        }
        ctx.putImageData(imageData, 0, 0);
        
        const feltTexture = new THREE.CanvasTexture(canvas);
        feltTexture.wrapS = THREE.RepeatWrapping;
        feltTexture.wrapT = THREE.RepeatWrapping;
        feltTexture.repeat.set(8, 4);
        
        const bumpCanvas = document.createElement('canvas');
        bumpCanvas.width = 64;
        bumpCanvas.height = 64;
        const bumpCtx = bumpCanvas.getContext('2d');
        bumpCtx.fillStyle = '#808080';
        bumpCtx.fillRect(0, 0, 64, 64);
        for (let i = 0; i < 500; i++) {
            const x = Math.random() * 64;
            const y = Math.random() * 64;
            const brightness = Math.random() * 50 + 100;
            bumpCtx.fillStyle = `rgb(${brightness},${brightness},${brightness})`;
            bumpCtx.fillRect(x, y, 1, 1);
        }
        const bumpTexture = new THREE.CanvasTexture(bumpCanvas);
        bumpTexture.wrapS = THREE.RepeatWrapping;
        bumpTexture.wrapT = THREE.RepeatWrapping;
        bumpTexture.repeat.set(16, 8);
        
        return new THREE.MeshStandardMaterial({
            map: feltTexture,
            bumpMap: bumpTexture,
            bumpScale: 0.5,
            roughness: 0.9,
            metalness: 0.0,
            color: 0x1a8040
        });
    },
    
    createCushionMaterial: function() {
        return new THREE.MeshStandardMaterial({
            color: 0x1a7030,
            roughness: 0.7,
            metalness: 0.0
        });
    },
    
    createRail: function(width, height, depth, material) {
        const geometry = new THREE.BoxGeometry(width, height, depth);
        const mesh = new THREE.Mesh(geometry, material);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        return mesh;
    },
    
    createCushion: function(width, height, depth, material) {
        const shape = new THREE.Shape();
        shape.moveTo(0, 0);
        shape.lineTo(width, 0);
        shape.lineTo(width, height);
        shape.lineTo(width * 0.8, height * 0.7);
        shape.lineTo(width * 0.2, height * 0.7);
        shape.lineTo(0, height);
        shape.closePath();
        
        const extrudeSettings = { depth: depth, bevelEnabled: false };
        const geometry = new THREE.ExtrudeGeometry(shape, extrudeSettings);
        geometry.center();
        
        const mesh = new THREE.Mesh(geometry, material);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        return mesh;
    },
    
    createPockets: function(tableGroup) {
        const pocketMaterial = new THREE.MeshStandardMaterial({
            color: 0x0a0a0a,
            roughness: 0.9,
            metalness: 0.1
        });
        
        const pocketRadius = 30;
        const pocketDepth = 20;
        
        const pocketPositions = [
            [35, 35],
            [this.tableWidth - 35, 35],
            [35, this.tableHeight - 35],
            [this.tableWidth - 35, this.tableHeight - 35],
            [this.tableWidth / 2, 25],
            [this.tableWidth / 2, this.tableHeight - 25]
        ];
        
        pocketPositions.forEach(pos => {
            const pocketGeometry = new THREE.CylinderGeometry(pocketRadius, pocketRadius * 0.8, pocketDepth, 32);
            const pocket = new THREE.Mesh(pocketGeometry, pocketMaterial);
            pocket.position.set(pos[0], -pocketDepth / 2 + 2, pos[1]);
            pocket.receiveShadow = true;
            tableGroup.add(pocket);
            
            const rimGeometry = new THREE.TorusGeometry(pocketRadius + 3, 3, 8, 32);
            const rimMaterial = new THREE.MeshStandardMaterial({
                color: 0xc4a000,
                roughness: 0.3,
                metalness: 0.8
            });
            const rim = new THREE.Mesh(rimGeometry, rimMaterial);
            rim.rotation.x = Math.PI / 2;
            rim.position.set(pos[0], 3, pos[1]);
            rim.castShadow = true;
            tableGroup.add(rim);
        });
    },
    
    setupControls: function() {
        if (typeof THREE.OrbitControls !== 'undefined') {
            this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
            this.controls.target.set(this.tableWidth / 2, 0, this.tableHeight / 2);
            this.controls.enableDamping = true;
            this.controls.dampingFactor = 0.05;
            this.controls.minDistance = 300;
            this.controls.maxDistance = 1500;
            this.controls.maxPolarAngle = Math.PI / 2.2;
            this.controls.update();
        }
    },
    
    onResize: function() {
        if (!this.camera || !this.renderer) return;
        
        const width = window.innerWidth;
        const height = window.innerHeight;
        
        this.camera.aspect = width / height;
        this.camera.updateProjectionMatrix();
        this.renderer.setSize(width, height);
    },
    
    // Convert game coordinates to 3D world coordinates
    // Game: (0,0) at top-left, X goes right, Y goes down
    // 3D: centered on table, X goes right, Z goes "into" screen
    gameToWorld: function(gameX, gameY) {
        // The table model is centered, so offset the ball positions
        // to match the table's position
        return {
            x: gameX,
            y: this.playingSurfaceY,
            z: gameY
        };
    },
    
    updateBalls: function(gameBalls) {
        if (!this.scene) return;
        
        gameBalls.forEach(ball => {
            if (ball.potted) {
                if (this.ballMeshes[ball.num]) {
                    this.ballMeshes[ball.num].visible = false;
                }
                return;
            }
            
            let mesh = this.ballMeshes[ball.num];
            
            if (!mesh) {
                mesh = this.createBallMesh(ball);
                this.ballMeshes[ball.num] = mesh;
                this.scene.add(mesh);
            }
            
            // Convert game coordinates to 3D world position
            const worldPos = this.gameToWorld(ball.x, ball.y);
            mesh.position.x = worldPos.x;
            mesh.position.y = worldPos.y + ball.r;
            mesh.position.z = worldPos.z;
            mesh.visible = true;
            
            if (ball.vx || ball.vy) {
                const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
                if (speed > 0.01) {
                    const rotationAxis = new THREE.Vector3(-ball.vy, 0, ball.vx).normalize();
                    mesh.rotateOnWorldAxis(rotationAxis, speed * 0.1);
                }
            }
        });
    },
    
    
    
    // Ball model loading
    ballModelLoaded: false,
    ballModelTemplate: null,
    ballModelUrl: 'https://raw.githubusercontent.com/gazlappy/Wdplapp/master/wdpl2/Models/ball.glb',
    
    loadBallModel: async function() {
        if (this.ballModelLoaded) return;
        
        try {
            console.log('[ThreeJS] Loading ball 3D model...');
            
            return new Promise((resolve, reject) => {
                this.gltfLoader.load(
                    this.ballModelUrl,
                    (gltf) => {
                        console.log('[ThreeJS] Ball model loaded successfully');
                        this.ballModelTemplate = gltf.scene;
                        this.ballModelLoaded = true;
                        resolve();
                    },
                    (progress) => {
                        console.log('[ThreeJS] Ball model loading:', Math.round((progress.loaded / progress.total) * 100) + '%');
                    },
                    (error) => {
                        console.warn('[ThreeJS] Ball model failed to load, using procedural balls:', error);
                        this.ballModelLoaded = false;
                        resolve(); // Don't reject, fall back to procedural
                    }
                );
            });
        } catch (error) {
            console.warn('[ThreeJS] Ball model loading error:', error);
            this.ballModelLoaded = false;
        }
    },
    
    createBallMesh: function(ball) {
        let mesh;
        
        // Try to use 3D model if loaded
        if (this.ballModelLoaded && this.ballModelTemplate) {
            mesh = this.ballModelTemplate.clone();
            
            // Scale to match ball radius
            const scale = ball.r / 14; // Assuming model is designed for radius 14
            mesh.scale.set(scale, scale, scale);
            
            // Apply UK ball material
            const material = this.createUKBallMaterial(ball);
            mesh.traverse((child) => {
                if (child.isMesh) {
                    child.material = material;
                    child.castShadow = true;
                    child.receiveShadow = true;
                }
            });
        } else {
            // Fallback to procedural sphere
            const geometry = new THREE.SphereGeometry(ball.r, 48, 48);
            const material = this.createUKBallMaterial(ball);
            mesh = new THREE.Mesh(geometry, material);
        }
        
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        mesh.name = `ball_${ball.num}`;
        
        return mesh;
    },
    
    // UK Pool ball colors - Red and Yellow teams
    // UK 8-ball: 7 red, 7 yellow, 1 black, 1 white cue
    ukBallColors: {
        'white': { color: '#FFFEF0', hex: 0xFFFEF0, type: 'cue' },
        'cue':   { color: '#FFFEF0', hex: 0xFFFEF0, type: 'cue' },
        'red':   { color: '#CC0000', hex: 0xCC0000, type: 'red' },
        'yellow':{ color: '#FFD700', hex: 0xFFD700, type: 'yellow' },
        'black': { color: '#111111', hex: 0x111111, type: 'black' }
    },
    
    createUKBallMaterial: function(ball) {
        // Get ball color info
        const colorKey = ball.color ? ball.color.toLowerCase() : 'white';
        const ballInfo = this.ukBallColors[colorKey] || this.ukBallColors['white'];
        
        // Create high quality material for UK pool balls (solid colors, no numbers)
        const canvas = document.createElement('canvas');
        canvas.width = 512;
        canvas.height = 512;
        const ctx = canvas.getContext('2d');
        
        // Create gradient for 3D depth effect
        const gradient = ctx.createRadialGradient(180, 180, 0, 256, 256, 360);
        
        if (ballInfo.type === 'cue') {
            // White cue ball with subtle cream tint
            gradient.addColorStop(0, '#FFFFFF');
            gradient.addColorStop(0.3, '#FFFEF8');
            gradient.addColorStop(0.7, '#FFFEF0');
            gradient.addColorStop(1, '#F5F2E8');
        } else if (ballInfo.type === 'black') {
            // Black 8-ball with subtle sheen
            gradient.addColorStop(0, '#333333');
            gradient.addColorStop(0.3, '#222222');
            gradient.addColorStop(0.7, '#111111');
            gradient.addColorStop(1, '#000000');
        } else if (ballInfo.type === 'red') {
            // Deep red ball
            gradient.addColorStop(0, '#FF2222');
            gradient.addColorStop(0.3, '#DD0000');
            gradient.addColorStop(0.7, '#BB0000');
            gradient.addColorStop(1, '#880000');
        } else if (ballInfo.type === 'yellow') {
            // Bright yellow ball
            gradient.addColorStop(0, '#FFEE44');
            gradient.addColorStop(0.3, '#FFD700');
            gradient.addColorStop(0.7, '#EECC00');
            gradient.addColorStop(1, '#CCAA00');
        }
        
        ctx.fillStyle = gradient;
        ctx.fillRect(0, 0, 512, 512);
        
        // Add subtle surface imperfections for realism
        this.addBallSurfaceDetail(ctx, ballInfo.type);
        
        const texture = new THREE.CanvasTexture(canvas);
        texture.wrapS = THREE.ClampToEdgeWrapping;
        texture.wrapT = THREE.ClampToEdgeWrapping;
        
        // Create physically-based material
        const material = new THREE.MeshStandardMaterial({
            map: texture,
            roughness: ballInfo.type === 'cue' ? 0.06 : 0.08,
            metalness: 0.0,
            envMapIntensity: 1.0
        });
        
        // Add clearcoat for that glossy pool ball look (if supported)
        if (material.clearcoat !== undefined) {
            material.clearcoat = 1.0;
            material.clearcoatRoughness = 0.05;
        }
        
        return material;
    },
    
    addBallSurfaceDetail: function(ctx, ballType) {
        // Add very subtle specular highlight
        const highlight = ctx.createRadialGradient(160, 140, 0, 160, 140, 100);
        highlight.addColorStop(0, 'rgba(255, 255, 255, 0.3)');
        highlight.addColorStop(0.5, 'rgba(255, 255, 255, 0.1)');
        highlight.addColorStop(1, 'rgba(255, 255, 255, 0)');
        ctx.fillStyle = highlight;
        ctx.fillRect(0, 0, 512, 512);
        
        // Add micro surface noise
        const imageData = ctx.getImageData(0, 0, 512, 512);
        for (let i = 0; i < imageData.data.length; i += 4) {
            const noise = (Math.random() - 0.5) * 2;
            imageData.data[i] = Math.min(255, Math.max(0, imageData.data[i] + noise));
            imageData.data[i + 1] = Math.min(255, Math.max(0, imageData.data[i + 1] + noise));
            imageData.data[i + 2] = Math.min(255, Math.max(0, imageData.data[i + 2] + noise));
        }
        ctx.putImageData(imageData, 0, 0);
    },
    
    render: function() {
        if (!this.enabled || !this.renderer || !this.scene || !this.camera) return;
        
        if (this.controls) {
            this.controls.update();
        }
        
        this.renderer.render(this.scene, this.camera);
    },
    
    startAnimation: function() {
        const self = this;
        
        function animate() {
            if (!self.enabled) return;
            
            if (typeof game !== 'undefined' && game.balls) {
                self.updateBalls(game.balls);
            }
            
            self.render();
            self.animationId = requestAnimationFrame(animate);
        }
        
        if (!this.animationId) {
            animate();
        }
    },
    
    stopAnimation: function() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
    },
    
    toggle: async function() {
        console.log('[ThreeJS] Toggle called');
        
        try {
            if (!this.initialized) {
                console.log('[ThreeJS] Initializing...');
                await this.init(typeof game !== 'undefined' ? game : null);
                console.log('[ThreeJS] Initialized');
            }
            
            
            this.enabled = !this.enabled;
            
            if (this.enabled) {
                this.container.style.display = 'block';
                
                const canvas2D = document.getElementById('canvas') || 
                                document.getElementById('poolCanvas') ||
                                document.getElementById('poolTable');
                if (canvas2D) canvas2D.style.visibility = 'hidden';
                
                const canvas3D = document.getElementById('pool3DCanvas');
                if (canvas3D) canvas3D.style.display = 'none';
                
                if (typeof Pool3DRenderer !== 'undefined' && Pool3DRenderer.enabled) {
                    Pool3DRenderer.enabled = false;
                    Pool3DRenderer.stopAnimationLoop();
                    if (Pool3DRenderer.controlPanel) {
                        Pool3DRenderer.controlPanel.style.display = 'none';
                    }
                }
                
                this.startAnimation();
            } else {
                this.container.style.display = 'none';
                this.stopAnimation();
                
                const canvas2D = document.getElementById('canvas') || 
                                document.getElementById('poolCanvas') ||
                                document.getElementById('poolTable');
                if (canvas2D) canvas2D.style.visibility = 'visible';
            }
        } catch (error) {
            console.error('[ThreeJS] Toggle error:', error);
        }
    },
    
    dispose: function() {
        this.stopAnimation();
        
        if (this.renderer) {
            this.renderer.dispose();
        }
        
        if (this.scene) {
            this.scene.traverse(obj => {
                if (obj.geometry) obj.geometry.dispose();
                if (obj.material) {
                    if (Array.isArray(obj.material)) {
                        obj.material.forEach(m => m.dispose());
                    } else {
                        obj.material.dispose();
                    }
                }
            });
        }
        
        if (this.container && this.container.parentNode) {
            this.container.parentNode.removeChild(this.container);
        }
        
        this.initialized = false;
        this.enabled = false;
    }
};

// Keyboard shortcut (Shift+3)
document.addEventListener('keydown', function(e) {
    if (e.shiftKey && e.key === '#') {
        PoolThreeJS.toggle();
    }
});

console.log('[ThreeJS] Photorealistic renderer module loaded (v2.0 with GLTF support)');
""";
    }
}
