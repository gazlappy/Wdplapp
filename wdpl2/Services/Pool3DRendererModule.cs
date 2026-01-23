namespace Wdpl2.Services;

/// <summary>
/// Three.js 3D Renderer Module - Proof of Concept
/// With fallback 2D rendering when Three.js can't load
/// </summary>
public static class Pool3DRendererModule
{
    public static string GenerateJavaScript()
    {
        return """
// ============================================
// POOL 3D RENDERER MODULE
// With fallback when Three.js unavailable
// ============================================

const Pool3DRenderer = {
    enabled: false,
    initialized: false,
    fallbackMode: false,
    canvas2D: null,
    canvas2DId: null,
    canvas3D: null,
    ctx: null,
    scene: null,
    camera: null,
    renderer: null,
    ballMeshes: {},
    materials: {},
    game: null,
    balls: [],
    
    config: {
        ballRadius: 0.026,
        tableWidth: 1.854,
        tableHeight: 0.927
    },
    
    async init(game) {
        if (this.initialized) return true;
        
        console.log('[3D] Initializing...');
        this.game = game;
        
        // Create the 3D canvas
        if (!this.createCanvas()) {
            console.error('[3D] Failed to create canvas');
            return false;
        }
        
        // Try to load Three.js
        let threeLoaded = false;
        if (typeof THREE === 'undefined') {
            console.log('[3D] Attempting to load Three.js...');
            try {
                await this.loadScript('https://cdnjs.cloudflare.com/ajax/libs/three.js/r128/three.min.js');
                threeLoaded = typeof THREE !== 'undefined';
                console.log('[3D] Three.js load result:', threeLoaded);
            } catch (e) {
                console.warn('[3D] Three.js load failed:', e);
            }
        } else {
            threeLoaded = true;
        }
        
        if (threeLoaded) {
            console.log('[3D] Using Three.js mode');
            this.fallbackMode = false;
            this.initThreeJS();
        } else {
            console.log('[3D] Using fallback 2D mode');
            this.fallbackMode = true;
            this.ctx = this.canvas3D.getContext('2d');
        }
        
        this.initialized = true;
        console.log('[3D] Ready! Fallback:', this.fallbackMode);
        return true;
    },
    
    loadScript(url) {
        return new Promise((resolve, reject) => {
            const script = document.createElement('script');
            script.src = url;
            script.onload = () => {
                console.log('[3D] Script loaded');
                resolve();
            };
            script.onerror = (e) => {
                console.error('[3D] Script error:', e);
                reject(e);
            };
            document.head.appendChild(script);
        });
    },
    
    createCanvas() {
        const canvas = document.getElementById('poolTable') || document.getElementById('canvas');
        if (!canvas) {
            console.error('[3D] No 2D canvas found');
            return false;
        }
        
        this.canvas2DId = canvas.id;
        this.canvas2D = canvas;
        
        const container = document.getElementById('canvasWrapper') || 
                          document.querySelector('.canvas-wrapper') || 
                          canvas.parentElement;
        
        const width = canvas.width || 800;
        const height = canvas.height || 400;
        
        console.log('[3D] Creating 3D canvas:', width, 'x', height);
        
        this.canvas3D = document.createElement('canvas');
        this.canvas3D.id = 'poolTable3D';
        this.canvas3D.width = width;
        this.canvas3D.height = height;
        this.canvas3D.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;display:none;z-index:10;';
        
        if (getComputedStyle(container).position === 'static') {
            container.style.position = 'relative';
        }
        
        container.appendChild(this.canvas3D);
        console.log('[3D] Canvas appended');
        return true;
    },
    
    initThreeJS() {
        const w = this.canvas3D.width;
        const h = this.canvas3D.height;
        
        this.renderer = new THREE.WebGLRenderer({ canvas: this.canvas3D, antialias: true });
        this.renderer.setSize(w, h);
        this.renderer.setClearColor(0x1a7f37);
        
        this.scene = new THREE.Scene();
        this.scene.background = new THREE.Color(0x1a7f37);
        
        const aspect = w / h;
        this.camera = new THREE.OrthographicCamera(-aspect, aspect, 1, -1, 0.1, 10);
        this.camera.position.set(0, 3, 0);
        this.camera.lookAt(0, 0, 0);
        this.camera.up.set(0, 0, -1);
        
        this.scene.add(new THREE.AmbientLight(0xffffff, 1));
        
        // Table felt
        const feltGeo = new THREE.PlaneGeometry(this.config.tableWidth, this.config.tableHeight);
        const feltMat = new THREE.MeshBasicMaterial({ color: 0x1a7f37, side: THREE.DoubleSide });
        const felt = new THREE.Mesh(feltGeo, feltMat);
        felt.rotation.x = -Math.PI / 2;
        this.scene.add(felt);
        
        // Rails
        const railMat = new THREE.MeshBasicMaterial({ color: 0x8B4513 });
        const tw = this.config.tableWidth / 2;
        const th = this.config.tableHeight / 2;
        
        [[-tw-0.025, 0], [tw+0.025, 0], [0, -th-0.025], [0, th+0.025]].forEach((pos, i) => {
            const geo = new THREE.BoxGeometry(i<2 ? 0.05 : tw*2, 0.04, i<2 ? th*2 : 0.05);
            const mesh = new THREE.Mesh(geo, railMat);
            mesh.position.set(pos[0], 0.02, pos[1]);
            this.scene.add(mesh);
        });
        
        this.materials = {
            white: new THREE.MeshBasicMaterial({ color: 0xffffff }),
            red: new THREE.MeshBasicMaterial({ color: 0xff3333 }),
            yellow: new THREE.MeshBasicMaterial({ color: 0xffdd00 }),
            black: new THREE.MeshBasicMaterial({ color: 0x222222 })
        };
        
        this.renderer.render(this.scene, this.camera);
        console.log('[3D] Three.js ready');
    },
    
    updateBalls(balls, gameWidth, gameHeight) {
        if (!this.initialized) return;
        this.balls = balls;
        this.gameWidth = gameWidth;
        this.gameHeight = gameHeight;
        
        if (!this.fallbackMode && this.scene) {
            balls.forEach(ball => {
                if (!this.ballMeshes[ball.id]) {
                    const geo = new THREE.SphereGeometry(this.config.ballRadius, 12, 12);
                    const mat = this.materials[ball.color] || this.materials.white;
                    const mesh = new THREE.Mesh(geo, mat.clone());
                    this.scene.add(mesh);
                    this.ballMeshes[ball.id] = mesh;
                }
                
                const mesh = this.ballMeshes[ball.id];
                mesh.visible = !ball.potted;
                
                if (!ball.potted) {
                    const x = (ball.x / gameWidth - 0.5) * this.config.tableWidth;
                    const z = (ball.y / gameHeight - 0.5) * this.config.tableHeight;
                    mesh.position.set(x, this.config.ballRadius, z);
                }
            });
        }
    },
    
    render() {
        if (!this.enabled || !this.initialized) return;
        
        if (this.fallbackMode) {
            this.renderFallback();
        } else if (this.renderer && this.scene && this.camera) {
            this.renderer.render(this.scene, this.camera);
        }
    },
    
    renderFallback() {
        const ctx = this.ctx;
        if (!ctx) return;
        
        const w = this.canvas3D.width;
        const h = this.canvas3D.height;
        
        // Green background
        ctx.fillStyle = '#1a7f37';
        ctx.fillRect(0, 0, w, h);
        
        // Brown border
        ctx.strokeStyle = '#8B4513';
        ctx.lineWidth = 20;
        ctx.strokeRect(10, 10, w-20, h-20);
        
        // Draw balls
        if (this.balls) {
            const scaleX = w / (this.gameWidth || 1000);
            const scaleY = h / (this.gameHeight || 500);
            const ballR = 15 * Math.min(scaleX, scaleY);
            
            this.balls.forEach(ball => {
                if (ball.potted) return;
                
                const x = ball.x * scaleX;
                const y = ball.y * scaleY;
                
                ctx.beginPath();
                ctx.arc(x, y, ballR, 0, Math.PI * 2);
                
                if (ball.color === 'white') ctx.fillStyle = '#ffffff';
                else if (ball.color === 'red') ctx.fillStyle = '#ff3333';
                else if (ball.color === 'yellow') ctx.fillStyle = '#ffdd00';
                else if (ball.color === 'black') ctx.fillStyle = '#222222';
                else ctx.fillStyle = '#ffffff';
                
                ctx.fill();
                ctx.strokeStyle = '#000';
                ctx.lineWidth = 1;
                ctx.stroke();
            });
        }
        
        // Label
        ctx.fillStyle = 'rgba(0,0,0,0.5)';
        ctx.fillRect(w/2 - 80, 10, 160, 30);
        ctx.fillStyle = '#fff';
        ctx.font = '14px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('3D Mode (Fallback)', w/2, 30);
    },
    
    async toggle() {
        console.log('[3D] Toggle, init:', this.initialized);
        
        if (!this.initialized) {
            await this.init(window.game);
        }
        
        this.enabled = !this.enabled;
        
            const c2d = document.getElementById(this.canvas2DId);
            const c3d = this.canvas3D;
        
            if (c2d && c3d) {
                // Use visibility instead of display so the wrapper doesn't collapse
                if (this.enabled) {
                    c2d.style.visibility = 'hidden';
                    c2d.style.position = 'absolute';  // Remove from flow but keep space
                    c3d.style.display = 'block';
                    c3d.style.visibility = 'visible';
                } else {
                    c2d.style.visibility = 'visible';
                    c2d.style.position = '';  // Restore normal flow
                    c3d.style.display = 'none';
                }
                console.log('[3D] Toggled to:', this.enabled ? '3D' : '2D');
            }
        
            this.updateModeIndicator();
            this.updateToggleButton();
        
            // Immediate render
            if (this.enabled) {
                this.render();
            }
        },
    
    updateToggleButton() {
        const btn = document.getElementById('toggle3DBtn');
        if (btn) {
            btn.textContent = this.enabled ? '?? 2D View' : '?? 3D View';
            btn.style.background = this.enabled ? '#10b981' : '';
        }
    },
    
    updateModeIndicator() {
        let ind = document.getElementById('renderModeIndicator');
        if (!ind) {
            ind = document.createElement('div');
            ind.id = 'renderModeIndicator';
            ind.style.cssText = 'position:fixed;top:10px;left:10px;background:rgba(0,0,0,0.8);color:#4ade80;padding:8px 12px;border-radius:8px;font-family:monospace;font-size:12px;z-index:10000;pointer-events:none;';
            document.body.appendChild(ind);
        }
        
        const mode = this.enabled ? (this.fallbackMode ? '3D (Fallback)' : '3D (WebGL)') : '2D';
        ind.innerHTML = '?? ' + mode + '<br><small>Press 3 to toggle</small>';
        ind.style.color = this.enabled ? '#60a5fa' : '#4ade80';
    },
    
    onResize() {
        // TODO: handle resize
    },
    
    dispose() {
        if (this.renderer) this.renderer.dispose();
        if (this.canvas3D && this.canvas3D.parentElement) {
            this.canvas3D.parentElement.removeChild(this.canvas3D);
        }
        this.initialized = false;
        this.enabled = false;
    }
};

document.addEventListener('keydown', (e) => {
    if (e.key === '3' && !e.ctrlKey && !e.altKey && !e.metaKey) {
        Pool3DRenderer.toggle();
    }
});

console.log('[3D] Module loaded');
""";
    }
}
