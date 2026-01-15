namespace Wdpl2.Services;

/// <summary>
/// 3D View module for pool game - Starting with empty room, will add table in modules
/// </summary>
public static class Pool3DViewModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL 3D VIEW MODULE - CLEAN START
// Just an empty room with lighting
// ============================================

const Pool3DView = {
    is3DMode: false,
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    game: null,
    balls3D: [],
    animationId: null,
    container: null,
    scale: 0.5,
    
    // Materials
    materials: {},
    
    async init(game) {
        this.game = game;
        if (typeof THREE === 'undefined') await this.loadThreeJS();
        this.createToggleButton();
        console.log('Pool3DView initialized');
    },
    
    async loadThreeJS() {
        return new Promise((resolve, reject) => {
            const s1 = document.createElement('script');
            s1.src = 'https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js';
            s1.onload = () => {
                const s2 = document.createElement('script');
                s2.src = 'https://cdn.jsdelivr.net/npm/three@0.128.0/examples/js/controls/OrbitControls.js';
                s2.onload = resolve;
                s2.onerror = reject;
                document.head.appendChild(s2);
            };
            s1.onerror = reject;
            document.head.appendChild(s1);
        });
    },
    
    createToggleButton() {
        const btn = document.createElement('button');
        btn.id = 'toggle3DBtn';
        btn.innerHTML = '?? 3D View';
        btn.style.cssText = 'position:fixed;top:10px;left:10px;padding:12px 20px;background:linear-gradient(135deg,#8B5CF6,#6D28D9);color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:9999;font-size:14px;';
        btn.onclick = () => this.toggle();
        document.body.appendChild(btn);
    },
    
    toggle() {
        this.is3DMode = !this.is3DMode;
        const btn = document.getElementById('toggle3DBtn');
        if (this.is3DMode) {
            this.enable3D();
            btn.innerHTML = '?? 2D View';
            btn.style.background = 'linear-gradient(135deg,#10B981,#059669)';
        } else {
            this.disable3D();
            btn.innerHTML = '?? 3D View';
            btn.style.background = 'linear-gradient(135deg,#8B5CF6,#6D28D9)';
        }
    },
    
    enable3D() {
        // Hide 2D canvas
        if (this.game.canvas) this.game.canvas.style.display = 'none';
        document.querySelectorAll('#status,#controls,.ball-return-window').forEach(e => { if(e) e.style.display = 'none'; });
        
        // Create container
        this.container = document.createElement('div');
        this.container.id = 'pool3DContainer';
        this.container.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:100;background:#1a1a2e;';
        document.body.appendChild(this.container);
        
        // Setup everything
        this.setupScene();
        this.createMaterials();
        this.createRoom();
        this.createTable();
        this.createBalls();
        this.createLighting();
        this.setupControls();
        this.createUI();
        this.animate();
    },
    
    disable3D() {
        // Show 2D canvas
        if (this.game.canvas) this.game.canvas.style.display = 'block';
        document.querySelectorAll('#status,#controls,.ball-return-window').forEach(e => { if(e) e.style.display = ''; });
        
        // Cleanup
        if (this.container) { this.container.remove(); this.container = null; }
        if (this.animationId) { cancelAnimationFrame(this.animationId); this.animationId = null; }
        if (this.renderer) { this.renderer.dispose(); this.renderer = null; }
        this.scene = null;
        this.camera = null;
        this.controls = null;
        this.balls3D = [];
    },
    
    setupScene() {
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x1a1a2e);
        
        this.camera = new THREE.PerspectiveCamera(50, window.innerWidth/window.innerHeight, 0.1, 5000);
        this.camera.position.set(0, 250, 350);
        this.camera.lookAt(0, 0, 0);
        
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.renderer.shadowMap.type = THREE.PCFSoftShadowMap;
        this.container.appendChild(this.renderer.domElement);
        
        // Handle resize
        window.addEventListener('resize', () => {
            if (!this.is3DMode) return;
            this.camera.aspect = window.innerWidth / window.innerHeight;
            this.camera.updateProjectionMatrix();
            this.renderer.setSize(window.innerWidth, window.innerHeight);
        });
    },
    
    createMaterials() {
        this.materials = {
            // Room
            floor: new THREE.MeshStandardMaterial({ color: 0x2a2a3e, roughness: 0.8 }),
            wall: new THREE.MeshStandardMaterial({ color: 0x3a3a4e, roughness: 0.9 }),
            
            // Table
            felt: new THREE.MeshStandardMaterial({ color: 0x0d6b32, roughness: 0.9 }),
            wood: new THREE.MeshStandardMaterial({ color: 0x5D3A1A, roughness: 0.5 }),
            cushion: new THREE.MeshStandardMaterial({ color: 0x1B8A4A, roughness: 0.6 }),
            slate: new THREE.MeshStandardMaterial({ color: 0x3A4A4A, roughness: 0.7 }),
            pocket: new THREE.MeshStandardMaterial({ color: 0x111111, roughness: 1.0 }),
            
            // Balls
            white: new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.2 }),
            red: new THREE.MeshStandardMaterial({ color: 0xDC2626, roughness: 0.2 }),
            yellow: new THREE.MeshStandardMaterial({ color: 0xEAB308, roughness: 0.2 }),
            black: new THREE.MeshStandardMaterial({ color: 0x1a1a1a, roughness: 0.2 })
        };
    },
    
    createRoom() {
        // Floor
        const floor = new THREE.Mesh(
            new THREE.PlaneGeometry(1500, 1500),
            this.materials.floor
        );
        floor.rotation.x = -Math.PI / 2;
        floor.position.y = -50;
        floor.receiveShadow = true;
        this.scene.add(floor);
        
        // Back wall
        const backWall = new THREE.Mesh(
            new THREE.PlaneGeometry(1500, 400),
            this.materials.wall
        );
        backWall.position.set(0, 150, -500);
        this.scene.add(backWall);
        
        // Side walls
        const leftWall = new THREE.Mesh(
            new THREE.PlaneGeometry(1000, 400),
            this.materials.wall
        );
        leftWall.rotation.y = Math.PI / 2;
        leftWall.position.set(-750, 150, 0);
        this.scene.add(leftWall);
        
        const rightWall = new THREE.Mesh(
            new THREE.PlaneGeometry(1000, 400),
            this.materials.wall
        );
        rightWall.rotation.y = -Math.PI / 2;
        rightWall.position.set(750, 150, 0);
        this.scene.add(rightWall);
        
        
        
        
        console.log('Room created');
    },
    
    createTable() {
        // Table dimensions based on game scale
        const W = this.game.width * this.scale;  // ~250
        const H = this.game.height * this.scale; // ~125
        const tableY = 0; // Table surface at Y=0
        
        // === DEBUG: PLAYING SURFACE OVERLAY ===
        // Shows the exact area where balls can roll (matches 2D game coordinates)
        const debugOverlay = new THREE.Group();
        debugOverlay.name = 'playingSurfaceOverlay';
        
        // Playing surface outline (wireframe rectangle)
        const outlineGeom = new THREE.EdgesGeometry(new THREE.PlaneGeometry(W, H));
        const outlineMat = new THREE.LineBasicMaterial({ color: 0x00ff00, linewidth: 2 });
        const outline = new THREE.LineSegments(outlineGeom, outlineMat);
        outline.rotation.x = -Math.PI / 2;
        outline.position.y = tableY + 2;
        debugOverlay.add(outline);
        
        // Corner markers (where corner pockets are)
        const cornerMarkerGeom = new THREE.CircleGeometry(8, 16);
        const cornerMarkerMat = new THREE.MeshBasicMaterial({ color: 0xff0000, transparent: true, opacity: 0.5 });
        [[-W/2, -H/2], [W/2, -H/2], [-W/2, H/2], [W/2, H/2]].forEach(([x, z]) => {
            const marker = new THREE.Mesh(cornerMarkerGeom, cornerMarkerMat);
            marker.rotation.x = -Math.PI / 2;
            marker.position.set(x, tableY + 2.1, z);
            debugOverlay.add(marker);
        });
        
        // Side pocket markers
        const sideMarkerMat = new THREE.MeshBasicMaterial({ color: 0xffff00, transparent: true, opacity: 0.5 });
        [[0, -H/2], [0, H/2]].forEach(([x, z]) => {
            const marker = new THREE.Mesh(cornerMarkerGeom, sideMarkerMat);
            marker.rotation.x = -Math.PI / 2;
            marker.position.set(x, tableY + 2.1, z);
            debugOverlay.add(marker);
        });
        
        // Cushion boundary lines (inner play area)
        const cushionMargin = this.game.cushionMargin * this.scale;
        const innerW = W - cushionMargin * 2;
        const innerH = H - cushionMargin * 2;
        const innerOutlineGeom = new THREE.EdgesGeometry(new THREE.PlaneGeometry(innerW, innerH));
        const innerOutlineMat = new THREE.LineBasicMaterial({ color: 0x00ffff, linewidth: 1 });
        const innerOutline = new THREE.LineSegments(innerOutlineGeom, innerOutlineMat);
        innerOutline.rotation.x = -Math.PI / 2;
        innerOutline.position.y = tableY + 2.2;
        debugOverlay.add(innerOutline);
        
        
        
        
        // Add label
        console.log('DEBUG OVERLAY: Green = full table bounds, Cyan = cushion boundary, Red = corner pockets, Yellow = side pockets');
        
        this.scene.add(debugOverlay);
        this.debugOverlay = debugOverlay;
        
        // === SLATE BED WITH POCKET CUTOUTS ===
        const slateThickness = 8;
        const slateY = tableY - slateThickness/2;
        
        // Pocket cutout sizes
        const cornerPocketR = 16; // Corner pocket radius
        const sidePocketR = 14;   // Side pocket radius
        
        // Create slate shape with edge cutouts
        // Drawing clockwise, with cutouts curving INWARD from corners/edges
        const slateShape = new THREE.Shape();
        
        const halfW = W/2;
        const halfH = H/2;
        
        // Start at top edge, after top-left corner pocket
        slateShape.moveTo(-halfW + cornerPocketR, -halfH);
        
        // Top edge to top side pocket
        slateShape.lineTo(-sidePocketR, -halfH);
        
        // Top side pocket - semicircle cutout going INTO the slate (toward +Y in shape coords)
        slateShape.absarc(0, -halfH, sidePocketR, Math.PI, 0, false);
        
        // Continue top edge to top-right corner
        slateShape.lineTo(halfW - cornerPocketR, -halfH);
        
        // Top-right corner pocket - quarter circle cutout
        slateShape.absarc(halfW, -halfH, cornerPocketR, Math.PI, Math.PI * 1.5, false);
        
        // Right edge to bottom-right corner
        slateShape.lineTo(halfW, halfH - cornerPocketR);
        
        // Bottom-right corner pocket
        slateShape.absarc(halfW, halfH, cornerPocketR, Math.PI * 1.5, 0, false);
        
        // Bottom edge to bottom side pocket
        slateShape.lineTo(sidePocketR, halfH);
        
        // Bottom side pocket - semicircle cutout going INTO the slate (toward -Y in shape coords)
        slateShape.absarc(0, halfH, sidePocketR, 0, Math.PI, false);
        
        // Continue bottom edge to bottom-left corner
        slateShape.lineTo(-halfW + cornerPocketR, halfH);
        
        // Bottom-left corner pocket
        slateShape.absarc(-halfW, halfH, cornerPocketR, 0, Math.PI * 0.5, false);
        
        // Left edge to top-left corner
        slateShape.lineTo(-halfW, -halfH + cornerPocketR);
        
        // Top-left corner pocket
        slateShape.absarc(-halfW, -halfH, cornerPocketR, Math.PI * 0.5, Math.PI, false);
        
        // Close the shape
        slateShape.lineTo(-halfW + cornerPocketR, -halfH);
        
        // Extrude the shape to create 3D slate
        const extrudeSettings = {
            depth: slateThickness,
            bevelEnabled: false
        };
        const slateGeom = new THREE.ExtrudeGeometry(slateShape, extrudeSettings);
        
        // Rotate and position (ExtrudeGeometry extrudes along Z, we need Y)
        const slate = new THREE.Mesh(slateGeom, this.materials.slate);
        slate.rotation.x = -Math.PI / 2;
        slate.position.y = slateY + slateThickness/2;
        slate.receiveShadow = true;
        this.scene.add(slate);
        
        // Add pocket hole interiors (black cylinders going down into pockets)
        const pocketDepth = 20;
        const pocketMat = this.materials.pocket;
        
        // Corner pockets - positioned at the actual corners
        [[-halfW, -halfH], [halfW, -halfH], [-halfW, halfH], [halfW, halfH]].forEach(([x, z]) => {
            const pocketHole = new THREE.Mesh(
                new THREE.CylinderGeometry(cornerPocketR, cornerPocketR * 1.2, pocketDepth, 24),
                pocketMat
            );
            pocketHole.position.set(x, slateY - pocketDepth/2 + 2, z);
            this.scene.add(pocketHole);
        });
        
        // Side pockets - on the long edges (top and bottom in 3D space)
        [[0, -halfH], [0, halfH]].forEach(([x, z]) => {
            const pocketHole = new THREE.Mesh(
                new THREE.CylinderGeometry(sidePocketR, sidePocketR * 1.2, pocketDepth, 24),
                pocketMat
            );
            pocketHole.position.set(x, slateY - pocketDepth/2 + 2, z);
            this.scene.add(pocketHole);
        });
        
        console.log('Table created (slate with edge pocket cutouts): W=' + W + ', H=' + H);
    },
    
    addCushion(x, y, z, w, h, d) {
        const cushion = new THREE.Mesh(
            new THREE.BoxGeometry(w, h, d),
            this.materials.cushion
        );
        cushion.position.set(x, y, z);
        cushion.castShadow = true;
        this.scene.add(cushion);
    },
    
    addRail(x, y, z, w, h, d) {
        const rail = new THREE.Mesh(
            new THREE.BoxGeometry(w, h, d),
            this.materials.wood
        );
        rail.position.set(x, y, z);
        rail.castShadow = true;
        rail.receiveShadow = true;
        this.scene.add(rail);
    },
    
    addCornerPocket(x, y, z, r, jawY, corner) {
        // Pocket hole (black cylinder going down)
        const pocket = new THREE.Mesh(
            new THREE.CylinderGeometry(r, r * 1.3, 15, 24),
            this.materials.pocket
        );
        pocket.position.set(x, y, z);
        this.scene.add(pocket);
        
        // Create angled jaw pieces
        // Corner pockets have two jaws at 45 degrees
        const jawLen = 18;
        const jawH = 5;
        const jawW = 3;
        const jawOffset = r + jawW/2 + 1;
        
        // Jaw material (same green as cushions)
        const jawMat = this.materials.cushion;
        
        // Create angled jaw geometry (wedge shape)
        const jawShape = new THREE.Shape();
        jawShape.moveTo(0, 0);
        jawShape.lineTo(jawLen, 0);
        jawShape.lineTo(jawLen, jawH * 0.3);
        jawShape.lineTo(0, jawH);
        jawShape.closePath();
        
        const jawGeom = new THREE.ExtrudeGeometry(jawShape, { depth: jawW, bevelEnabled: false });
        
        // Position jaws based on corner
        if (corner === 'topLeft') {
            // Jaw along top edge (pointing right)
            const jaw1 = new THREE.Mesh(jawGeom, jawMat);
            jaw1.rotation.x = -Math.PI/2;
            jaw1.rotation.z = 0;
            jaw1.position.set(x + jawOffset - 2, jawY, z + jawW/2);
            this.scene.add(jaw1);
            
            // Jaw along left edge (pointing down)
            const jaw2 = new THREE.Mesh(jawGeom, jawMat);
            jaw2.rotation.x = -Math.PI/2;
            jaw2.rotation.z = Math.PI/2;
            jaw2.position.set(x + jawW/2, jawY, z + jawOffset - 2);
            this.scene.add(jaw2);
        } else if (corner === 'topRight') {
            const jaw1 = new THREE.Mesh(jawGeom, jawMat);
            jaw1.rotation.x = -Math.PI/2;
            jaw1.rotation.z = Math.PI;
            jaw1.position.set(x - jawOffset + 2, jawY, z - jawW/2);
            this.scene.add(jaw1);
            
            const jaw2 = new THREE.Mesh(jawGeom, jawMat);
            jaw2.rotation.x = -Math.PI/2;
            jaw2.rotation.z = Math.PI/2;
            jaw2.position.set(x - jawW/2, jawY, z + jawOffset - 2);
            this.scene.add(jaw2);
        } else if (corner === 'bottomLeft') {
            const jaw1 = new THREE.Mesh(jawGeom, jawMat);
            jaw1.rotation.x = -Math.PI/2;
            jaw1.rotation.z = 0;
            jaw1.position.set(x + jawOffset - 2, jawY, z - jawW/2);
            this.scene.add(jaw1);
            
            const jaw2 = new THREE.Mesh(jawGeom, jawMat);
            jaw2.rotation.x = -Math.PI/2;
            jaw2.rotation.z = -Math.PI/2;
            jaw2.position.set(x + jawW/2, jawY, z - jawOffset + 2);
            this.scene.add(jaw2);
        } else if (corner === 'bottomRight') {
            const jaw1 = new THREE.Mesh(jawGeom, jawMat);
            jaw1.rotation.x = -Math.PI/2;
            jaw1.rotation.z = Math.PI;
            jaw1.position.set(x - jawOffset + 2, jawY, z + jawW/2);
            this.scene.add(jaw1);
            
            const jaw2 = new THREE.Mesh(jawGeom, jawMat);
            jaw2.rotation.x = -Math.PI/2;
            jaw2.rotation.z = -Math.PI/2;
            jaw2.position.set(x - jawW/2, jawY, z - jawOffset + 2);
            this.scene.add(jaw2);
        }
    },
    
    addSidePocket(x, y, z, r, jawY, side) {
        // Pocket hole
        const pocket = new THREE.Mesh(
            new THREE.CylinderGeometry(r, r * 1.2, 12, 24),
            this.materials.pocket
        );
        pocket.position.set(x, y, z);
        this.scene.add(pocket);
        
        // Side pockets have two parallel jaws
        const jawLen = 15;
        const jawH = 5;
        const jawW = 3;
        const jawSpacing = r + 4;
        
        const jawMat = this.materials.cushion;
        
        // Create angled jaw geometry
        const jawShape = new THREE.Shape();
        jawShape.moveTo(0, 0);
        jawShape.lineTo(jawLen, 0);
        jawShape.lineTo(jawLen, jawH * 0.3);
        jawShape.lineTo(0, jawH);
        jawShape.closePath();
        
        const jawGeom = new THREE.ExtrudeGeometry(jawShape, { depth: jawW, bevelEnabled: false });
        
        if (side === 'top') {
            // Left jaw
            const jaw1 = new THREE.Mesh(jawGeom, jawMat);
            jaw1.rotation.x = -Math.PI/2;
            jaw1.rotation.z = Math.PI/2;
            jaw1.position.set(x - jawSpacing, jawY, z + jawW/2);
            this.scene.add(jaw1);
            
            // Right jaw (mirrored)
            const jaw2 = new THREE.Mesh(jawGeom, jawMat);
            jaw2.rotation.x = -Math.PI/2;
            jaw2.rotation.z = Math.PI/2;
            jaw2.position.set(x + jawSpacing + jawW, jawY, z + jawW/2);
            this.scene.add(jaw2);
        } else {
            // Bottom side pocket
            const jaw1 = new THREE.Mesh(jawGeom, jawMat);
            jaw1.rotation.x = -Math.PI/2;
            jaw1.rotation.z = -Math.PI/2;
            jaw1.position.set(x - jawSpacing - jawW, jawY, z - jawW/2);
            this.scene.add(jaw1);
            
            const jaw2 = new THREE.Mesh(jawGeom, jawMat);
            jaw2.rotation.x = -Math.PI/2;
            jaw2.rotation.z = -Math.PI/2;
            jaw2.position.set(x + jawSpacing, jawY, z - jawW/2);
            this.scene.add(jaw2);
        }
    },
    
    addFrame(x, y, z, w, h, d) {
        const frame = new THREE.Mesh(
            new THREE.BoxGeometry(w, h, d),
            this.materials.wood
        );
        frame.position.set(x, y, z);
        frame.castShadow = true;
        this.scene.add(frame);
    },
    
    addLeg(x, y, z, h) {
        const leg = new THREE.Mesh(
            new THREE.CylinderGeometry(6, 7, h, 10),
            this.materials.wood
        );
        leg.position.set(x, y, z);
        leg.castShadow = true;
        this.scene.add(leg);
    },
    
    createBalls() {
        this.balls3D = [];
        const ballR = this.game.standardBallRadius * this.scale;
        const ballY = ballR + 1; // Sit on felt
        
        this.game.balls.forEach((ball, i) => {
            const mat = this.materials[ball.color] || this.materials.white;
            const mesh = new THREE.Mesh(
                new THREE.SphereGeometry(ballR, 24, 24),
                mat
            );
            mesh.castShadow = true;
            mesh.userData = { index: i, num: ball.num };
            this.balls3D.push(mesh);
            this.scene.add(mesh);
        });
        
        this.updateBallPositions();
        console.log('Balls created:', this.balls3D.length);
    },
    
    updateBallPositions() {
        const ballY = this.game.standardBallRadius * this.scale + 1;
        
        this.game.balls.forEach((ball, i) => {
            const mesh = this.balls3D[i];
            if (!mesh) return;
            
            if (ball.potted) {
                mesh.visible = false;
                return;
            }
            
            mesh.visible = true;
            // Convert 2D coords to 3D (centered on table)
            const x = (ball.x - this.game.width/2) * this.scale;
            const z = (ball.y - this.game.height/2) * this.scale;
            mesh.position.set(x, ballY, z);
        });
    },
    
    createLighting() {
        // Ambient light
        const ambient = new THREE.AmbientLight(0xffffff, 0.4);
        this.scene.add(ambient);
        
        // Main overhead light
        const mainLight = new THREE.SpotLight(0xfff5e6, 1.0);
        mainLight.position.set(0, 300, 0);
        mainLight.angle = Math.PI / 3;
        mainLight.penumbra = 0.5;
        mainLight.castShadow = true;
        mainLight.shadow.mapSize.set(2048, 2048);
        mainLight.shadow.camera.near = 100;
        mainLight.shadow.camera.far = 500;
        this.scene.add(mainLight);
        
        // Fill lights
        const fill1 = new THREE.DirectionalLight(0xffffff, 0.3);
        fill1.position.set(-200, 150, 100);
        this.scene.add(fill1);
        
        const fill2 = new THREE.DirectionalLight(0xffffff, 0.3);
        fill2.position.set(200, 150, -100);
        this.scene.add(fill2);
        
        console.log('Lighting created');
    },
    
    setupControls() {
        this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;
        this.controls.dampingFactor = 0.05;
        this.controls.minDistance = 100;
        this.controls.maxDistance = 800;
        this.controls.maxPolarAngle = Math.PI / 2 - 0.1;
        this.controls.target.set(0, 0, 0);
    },
    
    toggleDebugOverlay() {
        if (this.debugOverlay) {
            this.debugOverlay.visible = !this.debugOverlay.visible;
            console.log('Debug overlay:', this.debugOverlay.visible ? 'ON' : 'OFF');
        }
    },
    
    createUI() {
        // Status display
        const status = document.createElement('div');
        status.id = 'status3D';
        status.textContent = '3D View Active';
        status.style.cssText = 'position:fixed;top:10px;left:50%;transform:translateX(-50%);padding:10px 20px;background:rgba(59,130,246,0.9);color:white;border-radius:8px;font-weight:bold;z-index:10001;';
        this.container.appendChild(status);
        
        // Camera buttons
        const camBtns = document.createElement('div');
        camBtns.innerHTML = `
            <button onclick=""Pool3DView.setCameraView('top')"" style=""margin:2px;padding:8px 12px;background:#3b82f6;color:white;border:none;border-radius:4px;cursor:pointer;"">Top</button>
            <button onclick=""Pool3DView.setCameraView('angle')"" style=""margin:2px;padding:8px 12px;background:#3b82f6;color:white;border:none;border-radius:4px;cursor:pointer;"">Angle</button>
            <button onclick=""Pool3DView.setCameraView('low')"" style=""margin:2px;padding:8px 12px;background:#3b82f6;color:white;border:none;border-radius:4px;cursor:pointer;"">Low</button>
            <button onclick=""Pool3DView.toggleDebugOverlay()"" style=""margin:2px;padding:8px 12px;background:#10b981;color:white;border:none;border-radius:4px;cursor:pointer;"">Toggle Bounds</button>
        `;
        camBtns.style.cssText = 'position:fixed;top:50px;left:10px;z-index:10001;';
        this.container.appendChild(camBtns);
        
        // Debug overlay legend
        const legend = document.createElement('div');
        legend.innerHTML = `
            <b>Bounds Overlay:</b><br>
            <span style=""color:#00ff00"">?</span> Table bounds<br>
            <span style=""color:#00ffff"">?</span> Cushion line<br>
            <span style=""color:#ff0000"">?</span> Corner pockets<br>
            <span style=""color:#ffff00"">?</span> Side pockets
        `;
        legend.style.cssText = 'position:fixed;top:50px;right:20px;padding:10px;background:rgba(0,0,0,0.8);color:white;border-radius:8px;font-size:11px;z-index:10001;';
        this.container.appendChild(legend);
        
        // Instructions
        const help = document.createElement('div');
        help.innerHTML = '<b>Controls:</b><br>Drag to rotate<br>Scroll to zoom<br>Click buttons for camera views';
        help.style.cssText = 'position:fixed;bottom:20px;left:20px;padding:15px;background:rgba(0,0,0,0.8);color:white;border-radius:8px;font-size:12px;z-index:10001;';
        this.container.appendChild(help);
    },
    
    setCameraView(view) {
        const W = this.game.width * this.scale;
        const H = this.game.height * this.scale;
        
        let pos, target;
        switch(view) {
            case 'top':
                pos = [0, 400, 0];
                target = [0, 0, 0];
                break;
            case 'angle':
                pos = [W/2 + 100, 200, H/2 + 150];
                target = [0, 0, 0];
                break;
            case 'low':
                pos = [0, 50, H/2 + 200];
                target = [0, 20, 0];
                break;
            default:
                return;
        }
        
        // Animate camera
        const start = {
            x: this.camera.position.x,
            y: this.camera.position.y,
            z: this.camera.position.z,
            tx: this.controls.target.x,
            ty: this.controls.target.y,
            tz: this.controls.target.z
        };
        const t0 = Date.now();
        const duration = 500;
        
        const animate = () => {
            const t = Math.min(1, (Date.now() - t0) / duration);
            const ease = 1 - Math.pow(1 - t, 3);
            
            this.camera.position.set(
                start.x + (pos[0] - start.x) * ease,
                start.y + (pos[1] - start.y) * ease,
                start.z + (pos[2] - start.z) * ease
            );
            this.controls.target.set(
                start.tx + (target[0] - start.tx) * ease,
                start.ty + (target[1] - start.ty) * ease,
                start.tz + (target[2] - start.tz) * ease
            );
            
            if (t < 1) requestAnimationFrame(animate);
        };
        animate();
    },
    
    animate() {
        if (!this.is3DMode) return;
        this.animationId = requestAnimationFrame(() => this.animate());
        
        this.updateBallPositions();
        if (this.controls) this.controls.update();
        if (this.renderer && this.scene && this.camera) {
            this.renderer.render(this.scene, this.camera);
        }
    }
};

console.log('Pool3DView module loaded');
";
    }
}
