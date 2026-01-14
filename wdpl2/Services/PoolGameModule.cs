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
        
        // FPS tracking
        this.fps = 0;
        this.frameCount = 0;
        this.lastFpsUpdate = Date.now();
        
        this.init();
    }
    
    init() {
        this.repositionPockets();
        this.resetRack();
        
        // Setup input
        PoolInput.setupMouseControls(this.canvas, this, this.statusEl);
        PoolInput.setupTouchControls(this.canvas, this, this.statusEl);
        
        // Setup spin control
        PoolSpinControl.setupSpinControl(this.canvas, this);
        
        // Initialize developer settings (F2 to toggle)
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
