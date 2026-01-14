namespace Wdpl2.Services;

/// <summary>
/// Main game module that coordinates all pool game systems
/// </summary>
public static class PoolGameModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL GAME MAIN MODULE
// Coordinates physics, rendering, and input
// ============================================

class PoolGame {
    constructor(canvas, statusEl) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.statusEl = statusEl;
        
        // Canvas dimensions
        this.width = 1000;
        this.height = 500;
        
        // Scale: 1000px canvas = 72 inches real table
        this.pixelsPerInch = 1000 / 72;
        
        // Ball sizes in pixels
        this.standardBallRadius = (2.0 / 2) * this.pixelsPerInch;
        this.cueBallRadius = (1.875 / 2) * this.pixelsPerInch;
        
        // Pocket sizes
        this.cornerPocketRadius = 1.675 * this.pixelsPerInch + (3.0 * 0.1 * this.pixelsPerInch);
        this.middlePocketRadius = 1.87 * this.pixelsPerInch + (2.5 * 0.1 * this.pixelsPerInch);
        
        // Pocket openings (visual) - slightly larger than capture zones
        this.cornerPocketOpening = 32;
        this.middlePocketOpening = 34;
        this.pocketDepth = 1.0;
        
        // Cushion margin
        this.cushionMargin = 1.5 * this.pixelsPerInch;
        
        // Game state
        this.balls = [];
        this.cueBall = null;
        this.pockets = [];
        
        // Shooting state
        this.isShooting = false;
        this.isAiming = false;
        this.shotPower = 0;
        this.maxPower = 40;
        this.aimAngle = 0;
        this.pullBackDistance = 0;
        this.pushForwardDistance = 0;
        
        // Shot control mode
        this.shotControlMode = 'drag'; // 'drag', 'click', 'slider', 'tap', 'swipe'
        this.powerMultiplier = 1.0;
        this.aimSensitivity = 1.0;
        this.maxPullDistance = 150;
        this.autoAimAssist = false;
        this.showShotPreview = true;
        
        // Click power mode state
        this.clickPowerCharging = false;
        this.clickPowerStartTime = 0;
        this.clickPowerMaxTime = 2000; // 2 seconds to reach max power
        
        // Mouse tracking
        this.mouseX = 0;
        this.mouseY = 0;
        this.dragStartY = 0;
        
        // Developer settings properties
        this.captureThresholdPercent = 0.3;
        this.showPocketZones = true;
        this.showCushionLines = false;
        this.showVelocities = false;
        this.showFps = false;
        this.pocketZoneOpacity = 0.2;
        this.collisionDamping = 0.98;
        this.friction = 0.987;
        this.cushionRestitution = 0.78;
        
        // Spin control properties
        this.maxSpin = 1.5;
        this.spinEffect = 2.0; // Set to 2.0 for visible but realistic effects
        this.englishTransfer = 0.5;
        this.spinDecayRate = 0.98; // Realistic decay rate
        this.showSpinArrows = true;
        
        // FPS tracking
        this.fps = 0;
        this.frameCount = 0;
        this.lastFpsUpdate = Date.now();
        
        this.init();
    }
    
    init() {
        this.repositionPockets();
        this.resetRack();
        
        // Initialize audio system with visual feedback
        if (typeof PoolAudio !== 'undefined') {
            PoolAudio.init();
            PoolAudio.setEnabled(true);
            PoolAudio.setVolume(0.7); // Slightly louder default
            console.log('?? Audio system initialized');
            
            // Add audio status indicator
            this.createAudioStatusIndicator();
        } else {
            console.warn('?? PoolAudio module not loaded');
        }
        
        // Setup input
        PoolInput.setupMouseControls(this.canvas, this, this.statusEl);
        PoolInput.setupTouchControls(this.canvas, this, this.statusEl);
        
        // Setup spin control
        PoolSpinControl.setupSpinControl(this.canvas, this);
        
        // Setup shot control modes
        if (typeof PoolShotControl !== 'undefined') {
            PoolShotControl.setupShotControls(this.canvas, this);
        }
        
        // Setup developer settings (F2 to toggle)
        if (typeof PoolDevSettings !== 'undefined') {
            try {
                PoolDevSettings.init(this);
                console.log('Developer settings initialized - Press F2 to open');
            } catch (e) {
                console.error('Failed to initialize dev settings:', e);
            }
        } else {
            console.warn('PoolDevSettings not available');
        }
        
        // Start animation
        this.animate();
    }
    
    createAudioStatusIndicator() {
        const audioBtn = document.createElement('button');
        audioBtn.id = 'audioTestBtn';
        audioBtn.innerHTML = '?? <span>Click to Enable Sound</span>';
        audioBtn.style.cssText = 'position:fixed;top:10px;right:10px;padding:12px 20px;background:rgba(239,68,68,0.9);color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:10000;font-size:14px;transition:all 0.3s;box-shadow:0 4px 12px rgba(0,0,0,0.5);';
        
        const updateStatus = () => {
            if (typeof PoolAudio !== 'undefined') {
                if (PoolAudio.userInteracted && PoolAudio.context.state === 'running') {
                    audioBtn.innerHTML = '?? <span>Sound Enabled</span>';
                    audioBtn.style.background = 'rgba(16, 185, 129, 0.9)';
                    return true;
                } else if (PoolAudio.context.state === 'suspended') {
                    audioBtn.innerHTML = '?? <span>Tap to Enable</span>';
                    audioBtn.style.background = 'rgba(251, 191, 36, 0.9)';
                } else {
                    audioBtn.innerHTML = '?? <span>Click to Enable</span>';
                    audioBtn.style.background = 'rgba(239, 68, 68, 0.9)';
                }
            }
            return false;
        };
        
        audioBtn.addEventListener('click', async () => {
            console.log('?? Audio test button clicked');
            if (typeof PoolAudio !== 'undefined') {
                try {
                    if (PoolAudio.context.state === 'suspended') {
                        await PoolAudio.context.resume();
                        console.log('   Context resumed from button click');
                    }
                    PoolAudio.userInteracted = true;
                    
                    console.log('   Playing test sound...');
                    PoolAudio.play('cueHit', 0.8);
                    
                    setTimeout(() => {
                        if (updateStatus()) {
                            console.log('? Audio fully working!');
                            setTimeout(() => {
                                audioBtn.style.opacity = '0';
                                setTimeout(() => audioBtn.remove(), 300);
                            }, 2000);
                        }
                    }, 100);
                } catch (e) {
                    console.error('? Audio test failed:', e);
                    audioBtn.innerHTML = '? <span>Audio Error</span>';
                }
            }
        });
        
        document.body.appendChild(audioBtn);
        
        const statusInterval = setInterval(() => {
            if (updateStatus()) {
                setTimeout(() => {
                    audioBtn.style.opacity = '0';
                    setTimeout(() => {
                        audioBtn.remove();
                        clearInterval(statusInterval);
                    }, 300);
                }, 1500);
            }
        }, 500);
        
        window.addEventListener('audioUnlocked', () => {
            console.log('?? Audio unlocked event received');
            updateStatus();
        });
        
        updateStatus();
    }
    
    repositionPockets() {
        this.pockets = [
            {x: this.cushionMargin * 0.5, y: this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.width - this.cushionMargin * 0.5, y: this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.cushionMargin * 0.5, y: this.height - this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.width - this.cushionMargin * 0.5, y: this.height - this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.width / 2, y: this.cushionMargin * 0.3, r: this.middlePocketRadius, type: 'middle', taperDist: 2.5},
            {x: this.width / 2, y: this.height - this.cushionMargin * 0.3, r: this.middlePocketRadius, type: 'middle', taperDist: 2.5}
        ];
    }
    
    resetRack() {
        this.balls = [];
        
        const breakLineX = this.width * 0.25;
        
        this.cueBall = {
            x: breakLineX, 
            y: this.height / 2,
            vx: 0, vy: 0,
            r: this.cueBallRadius,
            color: 'white',
            num: 0,
            rotation: 0,
            rotationAxisX: 0,
            rotationAxisY: 1
        };
        this.balls.push(this.cueBall);
        
        const rackX = this.width * 0.75;
        const rackY = this.height / 2;
        const gap = this.standardBallRadius * 2 + 0.5;
        
        const rackPattern = [
            {x: rackX + gap * 0, y: rackY + 0, color: 'red', num: 1},
            {x: rackX + gap * 1, y: rackY - gap * 0.5, color: 'yellow', num: 9},
            {x: rackX + gap * 1, y: rackY + gap * 0.5, color: 'red', num: 2},
            {x: rackX + gap * 2, y: rackY - gap * 1, color: 'red', num: 3},
            {x: rackX + gap * 2, y: rackY + 0, color: 'black', num: 8},
            {x: rackX + gap * 2, y: rackY + gap * 1, color: 'yellow', num: 10},
            {x: rackX + gap * 3, y: rackY - gap * 1.5, color: 'yellow', num: 11},
            {x: rackX + gap * 3, y: rackY - gap * 0.5, color: 'red', num: 4},
            {x: rackX + gap * 3, y: rackY + gap * 0.5, color: 'yellow', num: 12},
            {x: rackX + gap * 3, y: rackY + gap * 1.5, color: 'red', num: 5},
            {x: rackX + gap * 4, y: rackY - gap * 2, color: 'red', num: 6},
            {x: rackX + gap * 4, y: rackY - gap * 1, color: 'yellow', num: 13},
            {x: rackX + gap * 4, y: rackY + 0, color: 'yellow', num: 14},
            {x: rackX + gap * 4, y: rackY + gap * 1, color: 'red', num: 7},
            {x: rackX + gap * 4, y: rackY + gap * 2, color: 'yellow', num: 15}
        ];
        
        rackPattern.forEach(ball => {
            this.balls.push({
                x: ball.x,
                y: ball.y,
                vx: 0, vy: 0,
                r: this.standardBallRadius,
                color: ball.color,
                num: ball.num,
                rotation: 0,
                rotationAxisX: 0,
                rotationAxisY: 1
            });
        });
        
        this.statusEl.textContent = 'UK 8-Ball | Press F2 for Dev Settings | ' + this.balls.length + ' balls ready!';
        this.statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
    }
    
    stopBalls() {
        this.balls.forEach(b => {
            b.vx = 0;
            b.vy = 0;
        });
        this.statusEl.textContent = 'All balls stopped';
        this.statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
    }
    
    animate() {
        // Draw table
        PoolRendering.drawTable(this.ctx, this.width, this.height, this.cushionMargin);
        PoolRendering.drawPockets(this.ctx, this.pockets, this);
        
        // Physics
        let moving = false;
        let activeBalls = 0;
        
        this.balls.forEach(ball => {
            if (ball.potted) return;
            
            activeBalls++;
            
            // Store position history for trail effect (if ball has spin)
            if ((ball.spinX && Math.abs(ball.spinX) > 0.05) || (ball.spinY && Math.abs(ball.spinY) > 0.05)) {
                if (!ball.trail) ball.trail = [];
                ball.trail.push({ x: ball.x, y: ball.y });
                if (ball.trail.length > 20) ball.trail.shift(); // Keep last 20 positions
            } else {
                ball.trail = []; // Clear trail when no spin
            }
            
            // Apply physics
            if (PoolPhysics.applyFriction(ball)) {
                moving = true;
            }
            
            PoolPhysics.handleCushionBounce(ball, this.width, this.height, this.cushionMargin);
            
            // Check pockets
            for (let i = 0; i < this.pockets.length; i++) {
                const p = this.pockets[i];
                const dx = ball.x - p.x;
                const dy = ball.y - p.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                
                const pocketRadius = p.r || 29.5;
                const captureThreshold = ball.r * this.captureThresholdPercent;
                
                if (dist <= pocketRadius - captureThreshold) {
                    if (!ball.potted) {
                        ball.potted = true;
                        ball.vx = 0;
                        ball.vy = 0;
                        
                        // ?? PLAY POCKET SOUND
                        console.log(`?? Ball ${ball.num} potted!`);
                        if (typeof PoolAudio !== 'undefined') {
                            PoolAudio.play('pocket', 1.0);
                        } else {
                            console.warn('?? PoolAudio not available for pocket sound');
                        }
                        
                        console.log('Ball potted:', ball.color, ball.num);
                        this.statusEl.textContent = 'Ball ' + ball.num + ' potted!';
                        this.statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
                    }
                    break;
                }
            }
        });
        
        // Handle collisions
        PoolPhysics.processCollisions(this.balls);
        
        // Draw trails first (under balls)
        this.balls.forEach(ball => {
            if (!ball.potted && ball.trail && ball.trail.length > 1) {
                this.ctx.strokeStyle = 'rgba(255, 100, 100, 0.3)';
                this.ctx.lineWidth = 2;
                this.ctx.beginPath();
                this.ctx.moveTo(ball.trail[0].x, ball.trail[0].y);
                for (let i = 1; i < ball.trail.length; i++) {
                    const alpha = (i / ball.trail.length) * 0.3;
                    this.ctx.strokeStyle = `rgba(255, 100, 100, ${alpha})`;
                    this.ctx.lineTo(ball.trail[i].x, ball.trail[i].y);
                }
                this.ctx.stroke();
            }
        });
        
        // Draw balls
        this.balls.forEach(ball => {
            if (!ball.potted) {
                PoolRendering.drawBall(this.ctx, ball);
            }
        });
        
        // Draw aim line
        if (this.isAiming && !moving && this.cueBall && !this.cueBall.potted) {
            PoolRendering.drawAimLine(this.ctx, this.cueBall, this.aimAngle);
        }
        
        // Draw cue stick
        if (this.isShooting && this.cueBall && !this.cueBall.potted) {
            PoolRendering.drawCueStick(
                this.ctx,
                this.cueBall,
                this.aimAngle,
                this.pullBackDistance,
                this.pushForwardDistance
            );
            
            PoolRendering.drawPowerMeter(
                this.ctx,
                this.cueBall,
                this.shotPower,
                this.maxPower
            );
        }
        
        // Draw spin control overlay
        PoolSpinControl.drawSpinControl(this.ctx);
        
        // Draw shot control mode feedback
        if (typeof PoolShotControl !== 'undefined') {
            PoolShotControl.drawModeFeedback(this.ctx);
        }
        
        // Update status
        if (moving) {
            this.statusEl.textContent = 'Balls rolling... (' + activeBalls + ' on table)';
            this.statusEl.style.background = 'rgba(59, 130, 246, 0.9)';
        } else if (!moving && activeBalls > 0 && !this.isShooting) {
            this.statusEl.textContent = 'Ready! ' + activeBalls + ' balls. Press F2 for settings.';
            this.statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
        }
        
        // Continue animation
        requestAnimationFrame(() => this.animate());
    }
}

// Initialize game
let game;
window.addEventListener('load', () => {
    try {
        // Try both canvas IDs for compatibility
        let canvas = document.getElementById('canvas');
        if (!canvas) {
            canvas = document.getElementById('poolTable');
        }
        
        let statusEl = document.getElementById('status');
        if (!statusEl) {
            statusEl = document.getElementById('shotInfo');
        }
        
        if (!canvas) {
            console.error('Canvas element not found (tried: canvas, poolTable)');
            return;
        }
        
        if (!statusEl) {
            // Create a status element if it doesn't exist
            statusEl = document.createElement('div');
            statusEl.id = 'status';
            statusEl.style.cssText = 'position:fixed;bottom:10px;left:50%;transform:translateX(-50%);padding:10px 20px;background:rgba(16,185,129,0.9);color:white;border-radius:8px;font-weight:bold;z-index:100;';
            document.body.appendChild(statusEl);
        }
        
        game = new PoolGame(canvas, statusEl);
        console.log('Pool game initialized successfully');
        
        // Hide debug info after a few seconds
        const debugInfo = document.getElementById('debugInfo');
        if (debugInfo) {
            debugInfo.textContent = 'Game loaded! Press F2 for settings';
            setTimeout(() => { debugInfo.style.display = 'none'; }, 3000);
        }
    } catch (e) {
        console.error('Pool game error:', e);
        const debugInfo = document.getElementById('debugInfo');
        if (debugInfo) {
            debugInfo.textContent = 'ERROR: ' + e.message;
            debugInfo.style.color = '#EF4444';
        }
    }
});
";
    }
}
