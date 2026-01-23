namespace Wdpl2.Services;

/// <summary>
/// Three.js-based photorealistic 3D pool table renderer
/// Uses WebGL for high-quality rendering with proper lighting, shadows, and materials
/// Loads external GLTF models for realistic table rendering
/// </summary>
public static class PoolThreeJSModule
{
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
    cameraDistance: 800,
    cameraHeight: 400,
    cameraAngle: 0,
    
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
        
        // Load Three.js from CDN if not already loaded
        if (typeof THREE === 'undefined') {
            await this.loadThreeJS();
        }
        
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
        
        // Try to load external model, fall back to procedural if it fails
        try {
            await this.loadTableModel();
        } catch (error) {
            console.warn('[ThreeJS] Failed to load external model, using procedural table:', error);
            await this.createProceduralTable();
        }
        
        this.setupControls();
        this.hideLoadingIndicator();
        
        // Handle resize
        window.addEventListener('resize', () => this.onResize());
        
        this.initialized = true;
        console.log('[ThreeJS] Initialization complete');
        return true;
    },
    
    loadThreeJS: async function() {
        return new Promise((resolve, reject) => {
            // Load Three.js core
            const threeScript = document.createElement('script');
            threeScript.src = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js';
            threeScript.onload = () => {
                console.log('[ThreeJS] Three.js loaded');
                
                // Load OrbitControls
                const controlsScript = document.createElement('script');
                controlsScript.src = 'https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js';
                controlsScript.onload = () => {
                    console.log('[ThreeJS] OrbitControls loaded');
                    
                    // Load GLTFLoader
                    const gltfScript = document.createElement('script');
                    gltfScript.src = 'https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/GLTFLoader.js';
                    gltfScript.onload = () => {
                        console.log('[ThreeJS] GLTFLoader loaded');
                        
                        // Load DRACOLoader for compressed models
                        const dracoScript = document.createElement('script');
                        dracoScript.src = 'https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/loaders/DRACOLoader.js';
                        dracoScript.onload = () => {
                            console.log('[ThreeJS] DRACOLoader loaded');
                            resolve();
                        };
                        dracoScript.onerror = () => {
                            console.warn('[ThreeJS] DRACOLoader failed to load, continuing without it');
                            resolve();
                        };
                        document.head.appendChild(dracoScript);
                    };
                    gltfScript.onerror = reject;
                    document.head.appendChild(gltfScript);
                };
                controlsScript.onerror = reject;
                document.head.appendChild(controlsScript);
            };
            threeScript.onerror = reject;
            document.head.appendChild(threeScript);
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
    
    setupRenderer: function() {
        this.renderer = new THREE.WebGLRenderer({ 
            antialias: this.antialias,
            alpha: true,
            powerPreference: 'high-performance'
        });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.renderer.toneMapping = THREE.ACESFilmicToneMapping;
        this.renderer.toneMappingExposure = 1.2;
        this.renderer.outputEncoding = THREE.sRGBEncoding;
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
    },
    
    setupCamera: function() {
        const aspect = window.innerWidth / window.innerHeight;
        this.camera = new THREE.PerspectiveCamera(45, aspect, 1, 5000);
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
        this.camera.lookAt(this.tableWidth / 2, 0, this.tableHeight / 2);
    },
    
    setupLighting: function() {
        // Clear existing lights
        this.lights.forEach(light => this.scene.remove(light));
        this.lights = [];
        
        // Ambient light for base illumination
        const ambient = new THREE.AmbientLight(0xffffff, 0.4);
        this.scene.add(ambient);
        this.lights.push(ambient);
        
        // Main overhead light (pool table lamp style)
        const mainLight = new THREE.SpotLight(0xfff8e8, 2.0);
        mainLight.position.set(this.tableWidth / 2, 600, this.tableHeight / 2);
        mainLight.target.position.set(this.tableWidth / 2, 0, this.tableHeight / 2);
        mainLight.angle = Math.PI / 3;
        mainLight.penumbra = 0.4;
        mainLight.decay = 1.5;
        mainLight.distance = 1500;
        mainLight.castShadow = true;
        mainLight.shadow.mapSize.width = this.shadowMapSize;
        mainLight.shadow.mapSize.height = this.shadowMapSize;
        mainLight.shadow.camera.near = 100;
        mainLight.shadow.camera.far = 1200;
        mainLight.shadow.bias = -0.0001;
        this.scene.add(mainLight);
        this.scene.add(mainLight.target);
        this.lights.push(mainLight);
        
        // Secondary fill lights for softer shadows
        const fillLight1 = new THREE.PointLight(0xffe4c4, 0.4);
        fillLight1.position.set(-200, 400, this.tableHeight / 2);
        this.scene.add(fillLight1);
        this.lights.push(fillLight1);
        
        const fillLight2 = new THREE.PointLight(0xffe4c4, 0.4);
        fillLight2.position.set(this.tableWidth + 200, 400, this.tableHeight / 2);
        this.scene.add(fillLight2);
        this.lights.push(fillLight2);
        
        // Rim light for definition
        const rimLight = new THREE.DirectionalLight(0xffffff, 0.3);
        rimLight.position.set(-500, 300, 0);
        this.scene.add(rimLight);
        this.lights.push(rimLight);
    },
    
    loadTableModel: async function() {
        this.updateLoadingProgress(10, 'Setting up GLTF loader...');
        
        // Initialize GLTF Loader
        this.gltfLoader = new THREE.GLTFLoader();
        
        // Setup DRACO decoder if available
        if (typeof THREE.DRACOLoader !== 'undefined') {
            const dracoLoader = new THREE.DRACOLoader();
            dracoLoader.setDecoderPath('https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/libs/draco/');
            this.gltfLoader.setDRACOLoader(dracoLoader);
        }
        
        this.updateLoadingProgress(20, 'Loading 3D model from GitHub...');
        
        // Try multiple URLs - master and main branches
        // Push your model files to GitHub first!
        const modelUrls = [
            'https://raw.githubusercontent.com/gazlappy/Wdplapp/master/wdpl2/Models/scene.gltf',
            'https://raw.githubusercontent.com/gazlappy/Wdplapp/main/wdpl2/Models/scene.gltf',
        ];
        
        // Try each URL until one works
        let lastError = null;
        for (const url of modelUrls) {
            try {
                console.log('[ThreeJS] Attempting to load model from:', url);
                this.updateLoadingProgress(30, 'Downloading from GitHub...');
                await this.loadGLTF(url);
                this.modelLoaded = true;
                console.log('[ThreeJS] Model loaded successfully from:', url);
                return;
            } catch (error) {
                console.warn('[ThreeJS] Failed to load from:', url);
                console.warn('[ThreeJS] Error:', error.message || error);
                lastError = error;
            }
        }
        
        // Fallback to procedural table if model fails to load
        console.log('[ThreeJS] All model URLs failed, using procedural table');
        console.log('[ThreeJS] Last error:', lastError);
        this.updateLoadingProgress(50, 'Model not found - using procedural table...');
        
        // Show a message to the user about pushing to GitHub
        setTimeout(() => {
            const status = document.getElementById('threejs-status');
            if (status) {
                status.innerHTML = '<span style="color:#f59e0b">?? Push model files to GitHub to see 3D model</span>';
            }
        }, 500);
        
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
                    
                    // The Sketchfab model has different dimensions
                    // Table is roughly 53x91 units in the model (Y is length in GLTF)
                    // We need to scale to fit our 1000x500 game dimensions
                    const modelTableWidth = size.y;  // GLTF Y = our X (length)
                    const modelTableHeight = size.x; // GLTF X = our Z (width)
                    
                    const scaleX = this.tableWidth / modelTableWidth;
                    const scaleZ = this.tableHeight / modelTableHeight;
                    const scale = Math.min(scaleX, scaleZ) * 0.85;
                    
                    console.log('[ThreeJS] Applying scale:', scale);
                    
                    model.scale.set(scale, scale, scale);
                    
                    // The model is oriented differently - rotate to match our coordinate system
                    model.rotation.x = -Math.PI / 2;  // Flip from Z-up to Y-up
                    
                    // Center the model on our table position
                    model.position.x = this.tableWidth / 2;
                    model.position.y = 0;
                    model.position.z = this.tableHeight / 2;
                    
                    // Store playing surface height for ball positioning
                    this.playingSurfaceY = 15 * scale;
                    
                    // Enable shadows on all meshes
                    model.traverse((child) => {
                        if (child.isMesh) {
                            child.castShadow = true;
                            child.receiveShadow = true;
                            
                            // Hide the model's built-in balls (we use our own)
                            if (child.name && (child.name.includes('Sphere') || child.name.includes('Duplicate'))) {
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
            
            mesh.position.x = ball.x;
            mesh.position.y = ball.r + this.playingSurfaceY;
            mesh.position.z = ball.y;
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
    
    createBallMesh: function(ball) {
        const geometry = new THREE.SphereGeometry(ball.r, 32, 32);
        
        let material;
        if (ball.color === 'white') {
            material = new THREE.MeshStandardMaterial({
                color: 0xf5f5f0,
                roughness: 0.15,
                metalness: 0.0,
                envMapIntensity: 0.5
            });
        } else if (ball.color === 'black') {
            material = new THREE.MeshStandardMaterial({
                color: 0x1a1a1a,
                roughness: 0.1,
                metalness: 0.05
            });
        } else if (ball.color === 'red') {
            material = new THREE.MeshStandardMaterial({
                color: 0xcc2222,
                roughness: 0.1,
                metalness: 0.05
            });
        } else if (ball.color === 'yellow') {
            material = new THREE.MeshStandardMaterial({
                color: 0xddaa00,
                roughness: 0.1,
                metalness: 0.05
            });
        } else {
            material = new THREE.MeshStandardMaterial({
                color: 0xcccccc,
                roughness: 0.15,
                metalness: 0.0
            });
        }
        
        const mesh = new THREE.Mesh(geometry, material);
        mesh.castShadow = true;
        mesh.receiveShadow = true;
        mesh.name = `ball_${ball.num}`;
        
        return mesh;
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
        
        if (!this.initialized) {
            await this.init(typeof game !== 'undefined' ? game : null);
        }
        
        this.enabled = !this.enabled;
        console.log('[ThreeJS] Enabled:', this.enabled);
        
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
