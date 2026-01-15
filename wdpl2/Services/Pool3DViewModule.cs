namespace Wdpl2.Services;

/// <summary>
/// 3D View module for pool game - PLAYABLE 3D rendering with full game controls
/// Uses Three.js for WebGL rendering
/// </summary>
public static class Pool3DViewModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL 3D VIEW MODULE - PLAYABLE VERSION
// Three.js based 3D rendering with full game controls
// ============================================

const Pool3DView = {
    is3DMode: false,
    isPlayMode: true,
    scene: null,
    camera: null,
    renderer: null,
    controls: null,
    game: null,
    balls3D: [],
    table3D: null,
    animationId: null,
    container: null,
    scale: 0.5,
    
    // Game state
    gameState: 'idle', // 'idle', 'aiming', 'powering'
    cueStick: null,
    aimLine: null,
    ghostBall: null,
    aimAngle: 0,
    shotPower: 0,
    dragStartY: 0,
    
    raycaster: null,
    mouse: null,
    tableTopPlane: null,
    
    statusDisplay: null,
    modeButton: null,
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
        if (this.game.canvas) this.game.canvas.style.display = 'none';
        document.querySelectorAll('#status,#controls,.ball-return-window').forEach(e => { if(e) e.style.display = 'none'; });
        
        this.container = document.createElement('div');
        this.container.id = 'pool3DContainer';
        this.container.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;z-index:100;background:#1a1a2e;';
        document.body.appendChild(this.container);
        
        this.setupScene();
        this.createMaterials();
        this.createTable();
        this.createBalls();
        this.createCueStick();
        this.createAimVisuals();
        this.createLighting();
        this.setupControls();
        this.setupInput();
        this.createUI();
        this.animate();
        this.setPlayMode(true);
    },
    
    disable3D() {
        if (this.game.canvas) this.game.canvas.style.display = 'block';
        document.querySelectorAll('#status,#controls,.ball-return-window').forEach(e => { if(e) e.style.display = ''; });
        if (this.container) { this.container.remove(); this.container = null; }
        if (this.animationId) { cancelAnimationFrame(this.animationId); this.animationId = null; }
        if (this.renderer) { this.renderer.dispose(); this.renderer = null; }
        this.scene = null; this.camera = null; this.controls = null; this.balls3D = [];
        this.gameState = 'idle';
    },
    
    setupScene() {
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x1a1a2e);
        
        this.camera = new THREE.PerspectiveCamera(50, window.innerWidth/window.innerHeight, 0.1, 10000);
        this.camera.position.set(0, 300, 300);
        this.camera.lookAt(0, 0, 0);
        
        this.renderer = new THREE.WebGLRenderer({ antialias: true });
        this.renderer.setSize(window.innerWidth, window.innerHeight);
        this.renderer.setPixelRatio(Math.min(window.devicePixelRatio, 2));
        this.renderer.shadowMap.enabled = true;
        this.container.appendChild(this.renderer.domElement);
        
        this.raycaster = new THREE.Raycaster();
        this.mouse = new THREE.Vector2();
        this.tableTopPlane = new THREE.Plane(new THREE.Vector3(0, 1, 0), -8);
        
        window.addEventListener('resize', () => {
            if (!this.is3DMode) return;
            this.camera.aspect = window.innerWidth / window.innerHeight;
            this.camera.updateProjectionMatrix();
            this.renderer.setSize(window.innerWidth, window.innerHeight);
        });
    },
    
    createMaterials() {
        this.materials = {
            felt: new THREE.MeshStandardMaterial({ color: 0x0d5c2e, roughness: 0.9 }),
            wood: new THREE.MeshStandardMaterial({ color: 0x4a3520, roughness: 0.7 }),
            rail: new THREE.MeshStandardMaterial({ color: 0xC4B998, roughness: 0.5 }),
            cushion: new THREE.MeshStandardMaterial({ color: 0x1B7A3A, roughness: 0.6 }),
            white: new THREE.MeshStandardMaterial({ color: 0xffffff, roughness: 0.2 }),
            red: new THREE.MeshStandardMaterial({ color: 0xDC2626, roughness: 0.2 }),
            yellow: new THREE.MeshStandardMaterial({ color: 0xEAB308, roughness: 0.2 }),
            black: new THREE.MeshStandardMaterial({ color: 0x1a1a1a, roughness: 0.2 }),
            pocket: new THREE.MeshStandardMaterial({ color: 0x000000, roughness: 1.0 }),
            cue: new THREE.MeshStandardMaterial({ color: 0xD4A574, roughness: 0.4 }),
            cueTip: new THREE.MeshStandardMaterial({ color: 0x4169E1, roughness: 0.8 })
        };
    },
    
    createTable() {
        const w = this.game.width * this.scale;
        const h = this.game.height * this.scale;
        const cm = this.game.cushionMargin * this.scale;
        
        this.table3D = new THREE.Group();
        
        // Felt
        const felt = new THREE.Mesh(new THREE.BoxGeometry(w-cm*2, 2, h-cm*2), this.materials.felt);
        felt.position.y = 1; felt.receiveShadow = true;
        this.table3D.add(felt);
        
        // Frame
        const frame = new THREE.Mesh(new THREE.BoxGeometry(w+40, 30, h+40), this.materials.wood);
        frame.position.y = -15;
        this.table3D.add(frame);
        
        // Rails
        [[0, -h/2+cm/2, w-cm*3, cm], [0, h/2-cm/2, w-cm*3, cm]].forEach(([x,z,rw,rd]) => {
            const rail = new THREE.Mesh(new THREE.BoxGeometry(rw,15,rd), this.materials.rail);
            rail.position.set(x, 9, z); rail.castShadow = true;
            this.table3D.add(rail);
        });
        [[-w/2+cm/2, 0, cm, h-cm*3], [w/2-cm/2, 0, cm, h-cm*3]].forEach(([x,z,rw,rd]) => {
            const rail = new THREE.Mesh(new THREE.BoxGeometry(rw,15,rd), this.materials.rail);
            rail.position.set(x, 9, z); rail.castShadow = true;
            this.table3D.add(rail);
        });
        
        // Cushions
        [[0, -h/2+cm-2.5, w-cm*4, 5], [0, h/2-cm+2.5, w-cm*4, 5]].forEach(([x,z,cw,cd]) => {
            const cush = new THREE.Mesh(new THREE.BoxGeometry(cw,8,cd), this.materials.cushion);
            cush.position.set(x, 6, z);
            this.table3D.add(cush);
        });
        [[-w/2+cm-2.5, 0, 5, h-cm*4], [w/2-cm+2.5, 0, 5, h-cm*4]].forEach(([x,z,cw,cd]) => {
            const cush = new THREE.Mesh(new THREE.BoxGeometry(cw,8,cd), this.materials.cushion);
            cush.position.set(x, 6, z);
            this.table3D.add(cush);
        });
        
        // Pockets
        [[-w/2+cm*0.7,-h/2+cm*0.7], [w/2-cm*0.7,-h/2+cm*0.7], [-w/2+cm*0.7,h/2-cm*0.7], [w/2-cm*0.7,h/2-cm*0.7], [0,-h/2+cm*0.4], [0,h/2-cm*0.4]].forEach(([x,z]) => {
            const p = new THREE.Mesh(new THREE.CylinderGeometry(12,12,10,32), this.materials.pocket);
            p.position.set(x, 0, z);
            this.table3D.add(p);
        });
        
        // Floor
        const floor = new THREE.Mesh(new THREE.PlaneGeometry(2000,2000), new THREE.MeshStandardMaterial({color:0x2a2a3e,roughness:0.8}));
        floor.rotation.x = -Math.PI/2; floor.position.y = -35; floor.receiveShadow = true;
        this.scene.add(floor);
        
        this.scene.add(this.table3D);
    },
    
    createBalls() {
        this.balls3D = [];
        const r = this.game.standardBallRadius * this.scale * 0.9;
        
        this.game.balls.forEach((ball, i) => {
            const mat = this.materials[ball.color] || this.materials.white;
            const radius = ball.num === 0 ? r * 0.95 : r;
            const mesh = new THREE.Mesh(new THREE.SphereGeometry(radius, 32, 32), mat);
            mesh.castShadow = true;
            mesh.userData = { index: i, num: ball.num, radius };
            this.balls3D.push(mesh);
            this.scene.add(mesh);
        });
        this.updateBallPositions();
    },
    
    createCueStick() {
        this.cueStick = new THREE.Group();
        
        const shaft = new THREE.Mesh(new THREE.CylinderGeometry(1.5, 3, 180, 16), this.materials.cue);
        shaft.rotation.x = Math.PI/2; shaft.position.z = 100;
        this.cueStick.add(shaft);
        
        const tip = new THREE.Mesh(new THREE.CylinderGeometry(1.5, 1.5, 5, 16), this.materials.cueTip);
        tip.rotation.x = Math.PI/2; tip.position.z = 7;
        this.cueStick.add(tip);
        
        const ferrule = new THREE.Mesh(new THREE.CylinderGeometry(1.8, 1.8, 3, 16), new THREE.MeshStandardMaterial({color:0xffffff}));
        ferrule.rotation.x = Math.PI/2; ferrule.position.z = 11;
        this.cueStick.add(ferrule);
        
        this.cueStick.visible = false;
        this.scene.add(this.cueStick);
    },
    
    createAimVisuals() {
        const geom = new THREE.BufferGeometry();
        geom.setAttribute('position', new THREE.Float32BufferAttribute([0,0,0, 0,0,-300], 3));
        this.aimLine = new THREE.Line(geom, new THREE.LineBasicMaterial({color:0xffffff,transparent:true,opacity:0.7}));
        this.aimLine.visible = false;
        this.scene.add(this.aimLine);
        
        this.ghostBall = new THREE.Mesh(
            new THREE.SphereGeometry(this.game.standardBallRadius * this.scale * 0.9, 16, 16),
            new THREE.MeshBasicMaterial({color:0xffffff,transparent:true,opacity:0.3,wireframe:true})
        );
        this.ghostBall.visible = false;
        this.scene.add(this.ghostBall);
    },
    
    createLighting() {
        this.scene.add(new THREE.AmbientLight(0xffffff, 0.4));
        
        const spot = new THREE.SpotLight(0xfff5e6, 1.0);
        spot.position.set(0, 350, 0);
        spot.angle = Math.PI/3; spot.penumbra = 0.3;
        spot.castShadow = true;
        spot.shadow.mapSize.set(2048, 2048);
        this.scene.add(spot);
        
        const shade = new THREE.Mesh(
            new THREE.CylinderGeometry(40, 60, 30, 32, 1, true),
            new THREE.MeshStandardMaterial({color:0x228B22,side:THREE.DoubleSide})
        );
        shade.position.set(0, 320, 0);
        this.scene.add(shade);
    },
    
    setupControls() {
        this.controls = new THREE.OrbitControls(this.camera, this.renderer.domElement);
        this.controls.enableDamping = true;
        this.controls.dampingFactor = 0.05;
        this.controls.minDistance = 100;
        this.controls.maxDistance = 800;
        this.controls.maxPolarAngle = Math.PI/2 - 0.1;
        this.controls.target.set(0, 0, 0);
        this.controls.enabled = false;
    },
    
    setupInput() {
        const el = this.renderer.domElement;
        el.addEventListener('mousedown', e => this.onMouseDown(e));
        el.addEventListener('mousemove', e => this.onMouseMove(e));
        el.addEventListener('mouseup', e => this.onMouseUp(e));
        el.addEventListener('contextmenu', e => e.preventDefault());
        
        document.addEventListener('keydown', e => {
            if (!this.is3DMode) return;
            if (e.key === 'Escape') this.cancelShot();
            if (e.key === ' ') { e.preventDefault(); this.setPlayMode(!this.isPlayMode); }
        });
    },
    
    onMouseDown(e) {
        this.updateMouse(e);
        if (!this.isPlayMode) return;
        
        if (e.button === 0) {
            if (!this.canShoot()) return;
            if (this.game.ballInHand) { this.placeCueBall(); return; }
            this.gameState = 'aiming';
            this.updateAim();
        } else if (e.button === 2) {
            this.controls.enabled = true;
        }
    },
    
    onMouseMove(e) {
        this.updateMouse(e);
        if (this.gameState === 'aiming') this.updateAim();
        else if (this.gameState === 'powering') {
            this.shotPower = Math.min(this.game.maxPower, Math.max(0, (e.clientY - this.dragStartY) * 0.3));
            this.updateCuePosition();
            this.updatePowerDisplay();
        }
    },
    
    onMouseUp(e) {
        if (e.button === 2) { if (this.isPlayMode) this.controls.enabled = false; return; }
        if (e.button !== 0) return;
        
        if (this.gameState === 'aiming') {
            this.gameState = 'powering';
            this.dragStartY = e.clientY;
            this.shotPower = 0;
        } else if (this.gameState === 'powering') {
            if (this.shotPower > 1) this.executeShot();
            else this.cancelShot();
        }
    },
    
    updateMouse(e) {
        this.mouse.x = (e.clientX / window.innerWidth) * 2 - 1;
        this.mouse.y = -(e.clientY / window.innerHeight) * 2 + 1;
    },
    
    canShoot() {
        if (this.game.gameOver || !this.game.cueBall || this.game.cueBall.potted) return false;
        for (const b of this.game.balls) {
            if (!b.potted && (Math.abs(b.vx) > 0.1 || Math.abs(b.vy) > 0.1)) return false;
        }
        return true;
    },
    
    updateAim() {
        const cue = this.getCueBall3D();
        if (!cue) return;
        
        this.raycaster.setFromCamera(this.mouse, this.camera);
        const pt = new THREE.Vector3();
        this.raycaster.ray.intersectPlane(this.tableTopPlane, pt);
        if (!pt) return;
        
        this.aimAngle = Math.atan2(pt.z - cue.position.z, pt.x - cue.position.x);
        
        this.updateAimLine(cue.position);
        this.updateCuePosition();
        this.updateGhostBall(cue.position);
        
        this.cueStick.visible = true;
        this.aimLine.visible = true;
    },
    
    updateAimLine(pos) {
        const len = 250;
        const arr = this.aimLine.geometry.attributes.position.array;
        arr[0] = pos.x; arr[1] = pos.y; arr[2] = pos.z;
        arr[3] = pos.x + Math.cos(this.aimAngle) * len;
        arr[4] = pos.y;
        arr[5] = pos.z + Math.sin(this.aimAngle) * len;
        this.aimLine.geometry.attributes.position.needsUpdate = true;
    },
    
    updateCuePosition() {
        const cue = this.getCueBall3D();
        if (!cue) return;
        
        const pull = this.gameState === 'powering' ? this.shotPower * 2 : 0;
        const dist = 15 + pull;
        
        this.cueStick.position.set(
            cue.position.x - Math.cos(this.aimAngle) * dist,
            cue.position.y,
            cue.position.z - Math.sin(this.aimAngle) * dist
        );
        this.cueStick.rotation.y = -this.aimAngle - Math.PI/2;
    },
    
    updateGhostBall(pos) {
        const cueBallR = this.game.cueBallRadius * this.scale;
        const dx = Math.cos(this.aimAngle);
        const dz = Math.sin(this.aimAngle);
        
        let closest = null, closestDist = Infinity;
        
        this.game.balls.forEach((b, i) => {
            if (b.potted || b.num === 0) return;
            const b3d = this.balls3D[i];
            if (!b3d?.visible) return;
            
            const combined = cueBallR + b.r * this.scale;
            const toX = b3d.position.x - pos.x;
            const toZ = b3d.position.z - pos.z;
            const dot = toX * dx + toZ * dz;
            
            if (dot > 0) {
                const cx = pos.x + dx * dot;
                const cz = pos.z + dz * dot;
                const dist = Math.hypot(b3d.position.x - cx, b3d.position.z - cz);
                
                if (dist < combined && dot < closestDist) {
                    closestDist = dot;
                    const off = Math.sqrt(combined * combined - dist * dist);
                    closest = { x: pos.x + dx * (dot - off), z: pos.z + dz * (dot - off) };
                }
            }
        });
        
        if (closest) {
            this.ghostBall.position.set(closest.x, pos.y, closest.z);
            this.ghostBall.visible = true;
        } else {
            this.ghostBall.visible = false;
        }
    },
    
    executeShot() {
        const power = this.shotPower * (this.game.powerMultiplier || 1.0);
        this.game.cueBall.vx = Math.cos(-this.aimAngle + Math.PI) * power;
        this.game.cueBall.vy = Math.sin(-this.aimAngle + Math.PI) * power;
        this.game.startShot();
        if (typeof PoolAudio !== 'undefined') PoolAudio.play('cueHit', Math.min(1.0, power/30));
        console.log('3D Shot: power=' + power.toFixed(1));
        this.cancelShot();
    },
    
    placeCueBall() {
        this.raycaster.setFromCamera(this.mouse, this.camera);
        const pt = new THREE.Vector3();
        this.raycaster.ray.intersectPlane(this.tableTopPlane, pt);
        if (pt) {
            const x = (pt.x / this.scale) + this.game.width / 2;
            const y = (pt.z / this.scale) + this.game.height / 2;
            if (this.game.placeCueBall(x, y)) this.updateBallPositions();
        }
    },
    
    cancelShot() {
        this.gameState = 'idle';
        this.shotPower = 0;
        this.cueStick.visible = false;
        this.aimLine.visible = false;
        this.ghostBall.visible = false;
        this.updatePowerDisplay();
    },
    
    getCueBall3D() {
        return this.balls3D.find(b => b.userData.num === 0);
    },
    
    updateBallPositions() {
        this.game.balls.forEach((b, i) => {
            const m = this.balls3D[i];
            if (!m) return;
            if (b.potted) { m.visible = false; return; }
            m.visible = true;
            m.position.set(
                (b.x - this.game.width/2) * this.scale,
                m.userData.radius + 2,
                (b.y - this.game.height/2) * this.scale
            );
            if (Math.abs(b.vx) > 0.1 || Math.abs(b.vy) > 0.1) {
                m.rotation.x += b.vy * 0.01;
                m.rotation.z -= b.vx * 0.01;
            }
        });
    },
    
    setPlayMode(play) {
        this.isPlayMode = play;
        this.controls.enabled = !play;
        if (this.modeButton) {
            this.modeButton.innerHTML = play ? '?? PLAY MODE' : '??? VIEW MODE';
            this.modeButton.style.background = play ? 'linear-gradient(135deg,#22c55e,#16a34a)' : 'linear-gradient(135deg,#3b82f6,#2563eb)';
        }
        if (!play) this.cancelShot();
    },
    
    createUI() {
        // Status
        this.statusDisplay = document.createElement('div');
        this.statusDisplay.style.cssText = 'position:fixed;top:10px;left:50%;transform:translateX(-50%);padding:12px 25px;background:rgba(0,0,0,0.85);color:white;border-radius:10px;font-size:16px;font-weight:bold;z-index:10001;';
        this.container.appendChild(this.statusDisplay);
        
        // Mode button
        this.modeButton = document.createElement('button');
        this.modeButton.innerHTML = '?? PLAY MODE';
        this.modeButton.style.cssText = 'position:fixed;top:60px;left:10px;padding:10px 15px;background:linear-gradient(135deg,#22c55e,#16a34a);color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:10001;font-size:12px;';
        this.modeButton.onclick = () => this.setPlayMode(!this.isPlayMode);
        this.container.appendChild(this.modeButton);
        
        // Power meter
        const pw = document.createElement('div');
        pw.innerHTML = `<div style='font-size:11px;margin-bottom:5px;opacity:0.8'>POWER</div><div style='width:25px;height:180px;background:#333;border-radius:5px;overflow:hidden'><div id='power3DFill' style='width:100%;height:0%;background:linear-gradient(to top,#22c55e,#eab308,#ef4444);transition:height 0.05s'></div></div><div id='power3DVal' style='margin-top:5px;font-weight:bold'>0%</div>`;
        pw.style.cssText = 'position:fixed;right:20px;top:50%;transform:translateY(-50%);background:rgba(0,0,0,0.85);padding:12px;border-radius:10px;color:white;text-align:center;z-index:10001;';
        this.container.appendChild(pw);
        
        // Help
        const help = document.createElement('div');
        help.innerHTML = '<b>?? Play Mode:</b><br>• Left-click to aim<br>• Release, drag down for power<br>• Release to shoot<br>• Right-click rotates view<br>• SPACE toggles mode<br>• ESC cancels shot';
        help.style.cssText = 'position:fixed;bottom:20px;left:20px;padding:12px 15px;background:rgba(0,0,0,0.85);color:white;border-radius:10px;font-size:11px;line-height:1.6;z-index:10001;';
        this.container.appendChild(help);
        
        // Camera buttons
        const cams = document.createElement('div');
        cams.innerHTML = ['Top','Player 1','Player 2','Low'].map(v => `<button class='cam3d' data-view='${v.toLowerCase().replace(' ','')}'>${v}</button>`).join('');
        cams.style.cssText = 'position:fixed;top:110px;left:10px;display:flex;flex-direction:column;gap:4px;z-index:10001;';
        const st = document.createElement('style');
        st.textContent = '.cam3d{padding:6px 10px;background:rgba(59,130,246,0.8);color:white;border:none;border-radius:5px;cursor:pointer;font-size:11px;font-weight:bold;}';
        document.head.appendChild(st);
        cams.querySelectorAll('.cam3d').forEach(b => b.onclick = () => this.setCameraView(b.dataset.view));
        this.container.appendChild(cams);
    },
    
    updateStatusDisplay() {
        if (!this.statusDisplay) return;
        const p = this.game.getCurrentPlayer();
        let t = p.name + (p.color ? ` (${p.color.toUpperCase()}S)` : this.game.tableOpen ? ' - Table Open' : '');
        if (p.onBlack) t += ' - ON BLACK!';
        if (this.game.ballInHand) t = 'CLICK TO PLACE CUE BALL';
        if (this.game.gameOver) t = this.game.winner ? this.game.winner.name + ' WINS!' : 'GAME OVER';
        this.statusDisplay.textContent = t;
        this.statusDisplay.style.background = this.game.ballInHand ? 'rgba(34,197,94,0.9)' : this.game.gameOver ? 'rgba(139,92,246,0.9)' : p.color === 'red' ? 'rgba(220,38,38,0.9)' : p.color === 'yellow' ? 'rgba(234,179,8,0.9)' : 'rgba(59,130,246,0.9)';
    },
    
    updatePowerDisplay() {
        const fill = document.getElementById('power3DFill');
        const val = document.getElementById('power3DVal');
        if (fill && val) {
            const pct = (this.shotPower / this.game.maxPower) * 100;
            fill.style.height = pct + '%';
            val.textContent = Math.round(pct) + '%';
        }
    },
    
    setCameraView(view) {
        const w = this.game.width * this.scale, h = this.game.height * this.scale;
        const views = {
            top: [[0,450,0],[0,0,0]],
            player1: [[-w/2-80,120,0],[w/4,0,0]],
            player2: [[w/2+80,120,0],[-w/4,0,0]],
            low: [[0,40,h/2+180],[0,15,0]]
        };
        const v = views[view];
        if (v) this.animateCamera(v[0], v[1]);
    },
    
    animateCamera(pos, target) {
        const s = {px:this.camera.position.x,py:this.camera.position.y,pz:this.camera.position.z,tx:this.controls.target.x,ty:this.controls.target.y,tz:this.controls.target.z};
        const t0 = Date.now();
        const anim = () => {
            const p = Math.min(1, (Date.now()-t0)/600);
            const e = 1 - Math.pow(1-p, 3);
            this.camera.position.set(s.px+(pos[0]-s.px)*e, s.py+(pos[1]-s.py)*e, s.pz+(pos[2]-s.pz)*e);
            this.controls.target.set(s.tx+(target[0]-s.tx)*e, s.ty+(target[1]-s.ty)*e, s.tz+(target[2]-s.tz)*e);
            if (p < 1) requestAnimationFrame(anim);
        };
        anim();
    },
    
    animate() {
        if (!this.is3DMode) return;
        this.animationId = requestAnimationFrame(() => this.animate());
        this.updateBallPositions();
        this.updateStatusDisplay();
        if (this.controls) this.controls.update();
        if (this.renderer) this.renderer.render(this.scene, this.camera);
    }
};

console.log('Pool3DView module loaded (PLAYABLE)');
";
    }
}
