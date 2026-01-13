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
        
        // Real-world dimensions
        // UK 8-Ball Table: 6ft x 3ft = 72 inches x 36 inches = 1829mm x 914mm
        // UK Standard ball: 2 inches (50.8mm) diameter
        // UK Cue ball: 1 7/8 inches (48mm) diameter
        // UK Corner pockets: 3.5 inches (89mm) opening
        // UK Middle pockets: 3.2 inches (81mm) opening (tighter!)
        
        // Canvas dimensions
        this.width = 1000;
        this.height = 500;
        
        // Scale: 1000px canvas = 72 inches real table
        // 1 pixel = 0.072 inches or 1.829mm
        this.pixelsPerInch = 1000 / 72; // ~13.89 pixels per inch
        
        // Ball sizes in pixels
        this.standardBallRadius = (2.0 / 2) * this.pixelsPerInch; // ~13.89px radius = 27.78px diameter
        this.cueBallRadius = (1.875 / 2) * this.pixelsPerInch; // ~13.02px radius = 26.04px diameter
        
        // UK Pocket sizes: Much tighter than American pools!
        // Corner pockets: 3.5 inches opening (only 1.5 inches clearance with 2 inch ball)
        // Middle pockets: 3.2 inches opening (only 1.2 inches clearance!) - very tight
        // NOTE: Real tables have cushion tapers that effectively increase pocket openings by ~15-20%
        // We adjust for this by making visual pockets slightly larger
        this.cornerPocketRadius = (3.5 / 2) * this.pixelsPerInch * 1.15; // ~28px radius (accounting for taper)
        this.middlePocketRadius = (3.2 / 2) * this.pixelsPerInch * 1.15; // ~25.5px radius (accounting for taper)
        
        // Cushion height: typically 1.5 inches from playing surface
        this.cushionMargin = 1.5 * this.pixelsPerInch; // ~20.8px
        
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
        
        this.init();
    }
    
    init() {
        // Initialize pockets at corners and middle pockets
        // UK 8-Ball has MUCH tighter pockets than American pool
        // Corner pockets: 3.5 inches (only 1.5 inches clearance with 2 inch ball!)
        // Middle pockets: 3.2 inches (only 1.2 inches clearance!) - extremely tight
        
        this.pockets = [
            // Corner pockets - 3.5 inches opening
            {x: this.cushionMargin * 0.5, y: this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner'},
            {x: this.width - this.cushionMargin * 0.5, y: this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner'},
            {x: this.cushionMargin * 0.5, y: this.height - this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner'},
            {x: this.width - this.cushionMargin * 0.5, y: this.height - this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner'},
            
            // Middle pockets - 3.2 inches opening (tighter than corners!)
            {x: this.width / 2, y: this.cushionMargin * 0.3, r: this.middlePocketRadius, type: 'middle'},
            {x: this.width / 2, y: this.height - this.cushionMargin * 0.3, r: this.middlePocketRadius, type: 'middle'}
        ];
        
        // Reset rack
        this.resetRack();
        
        // Setup input
        PoolInput.setupMouseControls(this.canvas, this, this.statusEl);
        PoolInput.setupTouchControls(this.canvas, this, this.statusEl);
        
        // Setup spin control
        PoolSpinControl.setupSpinControl(this.canvas, this);
        
        // Start animation
        this.animate();
    }
    
    resetRack() {
        this.balls = [];
        
        // Cue ball position: ~1/4 from left end (breaking position)
        const breakLineX = this.width * 0.25;
        
        // Cue ball (smaller, regulation size)
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
        
        // EPA UK 8-BALL RACK
        // Rack position: ~3/4 from left (foot spot)
        const rackX = this.width * 0.75;
        const rackY = this.height / 2;
        
        // Gap between balls should be minimal (just touching)
        const gap = this.standardBallRadius * 2 + 0.5; // 0.5px gap for separation
        
        const rackPattern = [
            // Row 1: RED (apex)
            {x: rackX + gap * 0, y: rackY + 0, color: 'red', num: 1},
            
            // Row 2: YELLOW, RED
            {x: rackX + gap * 1, y: rackY - gap * 0.5, color: 'yellow', num: 9},
            {x: rackX + gap * 1, y: rackY + gap * 0.5, color: 'red', num: 2},
            
            // Row 3: RED, BLACK, YELLOW
            {x: rackX + gap * 2, y: rackY - gap * 1, color: 'red', num: 3},
            {x: rackX + gap * 2, y: rackY + 0, color: 'black', num: 8},
            {x: rackX + gap * 2, y: rackY + gap * 1, color: 'yellow', num: 10},
            
            // Row 4: YELLOW, RED, YELLOW, RED
            {x: rackX + gap * 3, y: rackY - gap * 1.5, color: 'yellow', num: 11},
            {x: rackX + gap * 3, y: rackY - gap * 0.5, color: 'red', num: 4},
            {x: rackX + gap * 3, y: rackY + gap * 0.5, color: 'yellow', num: 12},
            {x: rackX + gap * 3, y: rackY + gap * 1.5, color: 'red', num: 5},
            
            // Row 5: RED, YELLOW, YELLOW, RED, YELLOW (back)
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
        
        this.statusEl.textContent = 'UK 8-Ball (6ftx3ft) | Balls: 2in | Pockets: 3.5in corner, 3.2in middle (with cushion taper) | ' + this.balls.length + ' ready!';
        this.statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
    }
    
    stopBalls() {
        this.balls.forEach(b => {
            b.vx = 0;
            b.vy = 0;
        });
        this.statusEl.textContent = '?? All balls stopped';
        this.statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
    }
    
    animate() {
        // Draw table
        PoolRendering.drawTable(this.ctx, this.width, this.height, this.cushionMargin);
        PoolRendering.drawPockets(this.ctx, this.pockets);
        
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
            
            // Check pockets - each pocket now has its own radius
            // UK spec: Ball must be significantly into pocket (50% of ball radius)
            // This is still tighter than American pools but more playable
            for (let pocket of this.pockets) {
                const dx = ball.x - pocket.x;
                const dy = ball.y - pocket.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                
                // UK-style pocket capture: 50% of ball radius into pocket
                // This means ball center must be (pocket.r - ball.r * 0.5) from pocket center
                const captureThreshold = ball.r * 0.5;
                
                if (dist < pocket.r - captureThreshold) {
                    ball.potted = true;
                    ball.vx = ball.vy = 0;
                    this.statusEl.textContent = `? Ball ${ball.num} potted! ${activeBalls - 1} balls remaining`;
                    this.statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
                    break;
                }
            }
        });
        
        // Handle collisions
        PoolPhysics.processCollisions(this.balls);
        
        // Draw balls
        this.balls.forEach(ball => {
            PoolRendering.drawBall(this.ctx, ball);
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
        
        // Draw spin control overlay (always visible)
        PoolSpinControl.drawSpinControl(this.ctx);
        
        // Update status
        if (moving) {
            this.statusEl.textContent = `?? Balls rolling... (${activeBalls} on table)`;
            this.statusEl.style.background = 'rgba(59, 130, 246, 0.9)';
        } else if (!moving && activeBalls > 0 && !this.isShooting) {
            this.statusEl.textContent = `? Ready to shoot! ${activeBalls} balls on table. Click to shoot!`;
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
        const canvas = document.getElementById('canvas');
        const statusEl = document.getElementById('status');
        
        if (!canvas || !statusEl) {
            console.error('Canvas or status element not found');
            return;
        }
        
        game = new PoolGame(canvas, statusEl);
        console.log('Pool game initialized successfully');
    } catch (e) {
        console.error('Pool game error:', e);
        document.getElementById('status').textContent = '? ERROR: ' + e.message;
        document.getElementById('status').style.background = '#EF4444';
    }
});
";
    }
}
