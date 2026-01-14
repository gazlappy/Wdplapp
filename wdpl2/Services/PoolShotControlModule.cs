namespace Wdpl2.Services;

/// <summary>
/// Shot control module - handles different shooting input methods
/// </summary>
public static class PoolShotControlModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL SHOT CONTROL MODULE
// Multiple shot input methods
// ============================================

const PoolShotControl = {
    /**
     * Setup shot controls based on selected mode
     */
    setupShotControls(canvas, game) {
        this.canvas = canvas;
        this.game = game;
        
        // Setup mode-specific controls
        this.setupDragMode();
        this.setupClickMode();
        this.setupSliderMode();
        this.setupTapMode();
        this.setupSwipeMode();
        
        console.log('Shot control modes initialized');
    },
    
    /**
     * Drag & Release mode (default)
     * Pull back and release to shoot
     */
    setupDragMode() {
        // Already implemented in PoolInput module
        // This is the default behavior
    },
    
    /**
     * Click Power mode
     * Click and hold to charge power, release to shoot
     */
    setupClickMode() {
        let powerInterval = null;
        
        const startCharging = (e) => {
            if (this.game.shotControlMode !== 'click') return;
            if (!this.game.cueBall || this.game.cueBall.potted) return;
            
            // Check if balls are moving
            const ballsMoving = this.game.balls.some(b => !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01));
            if (ballsMoving) return;
            
            this.game.clickPowerCharging = true;
            this.game.clickPowerStartTime = Date.now();
            this.game.shotPower = 0;
            
            // Animate power increase
            powerInterval = setInterval(() => {
                if (!this.game.clickPowerCharging) {
                    clearInterval(powerInterval);
                    return;
                }
                
                const elapsed = Date.now() - this.game.clickPowerStartTime;
                const powerPercent = Math.min(elapsed / this.game.clickPowerMaxTime, 1);
                this.game.shotPower = powerPercent * this.game.maxPower;
            }, 16); // 60fps
        };
        
        const releaseShot = (e) => {
            if (this.game.shotControlMode !== 'click') return;
            if (!this.game.clickPowerCharging) return;
            
            this.game.clickPowerCharging = false;
            clearInterval(powerInterval);
            
            // Fire the shot
            if (this.game.shotPower > 0 && this.game.cueBall && !this.game.cueBall.potted) {
                const power = this.game.shotPower * (this.game.powerMultiplier || 1.0);
                this.game.cueBall.vx = Math.cos(this.game.aimAngle) * power;
                this.game.cueBall.vy = Math.sin(this.game.aimAngle) * power;
                
                // Apply spin
                if (typeof PoolSpinControl !== 'undefined') {
                    PoolSpinControl.applySpinToBall(this.game.cueBall, this.game.aimAngle);
                }
                
                this.game.shotPower = 0;
                this.game.isAiming = false;
            }
        };
        
        this.canvas.addEventListener('mousedown', startCharging);
        this.canvas.addEventListener('mouseup', releaseShot);
        this.canvas.addEventListener('touchstart', startCharging);
        this.canvas.addEventListener('touchend', releaseShot);
    },
    
    /**
     * Power Slider mode
     * Use a slider to set power, then click to shoot
     */
    setupSliderMode() {
        // Create slider UI if it doesn't exist
        let sliderPanel = document.getElementById('sliderPowerPanel');
        
        if (!sliderPanel) {
            sliderPanel = document.createElement('div');
            sliderPanel.id = 'sliderPowerPanel';
            sliderPanel.style.cssText = `
                position: fixed;
                bottom: 20px;
                left: 50%;
                transform: translateX(-50%);
                background: rgba(0,0,0,0.8);
                padding: 15px 25px;
                border-radius: 12px;
                display: none;
                flex-direction: column;
                gap: 10px;
                z-index: 1001;
            `;
            
            sliderPanel.innerHTML = `
                <div style='color:white;font-weight:bold;text-align:center;'>Shot Power</div>
                <div style='display:flex;align-items:center;gap:10px;'>
                    <input type='range' id='sliderPowerInput' min='0' max='100' value='50' 
                           style='flex:1;height:8px;'>
                    <span id='sliderPowerValue' style='color:#fbbf24;font-weight:bold;min-width:45px;'>50%</span>
                </div>
                <button id='sliderShootBtn' style='
                    padding:10px;
                    background:linear-gradient(135deg, #10b981 0%, #059669 100%);
                    border:none;
                    border-radius:8px;
                    color:white;
                    font-weight:bold;
                    cursor:pointer;
                '>SHOOT</button>
            `;
            
            document.body.appendChild(sliderPanel);
            
            // Setup slider events
            const slider = document.getElementById('sliderPowerInput');
            const valueDisplay = document.getElementById('sliderPowerValue');
            const shootBtn = document.getElementById('sliderShootBtn');
            
            slider.addEventListener('input', (e) => {
                const power = e.target.value;
                valueDisplay.textContent = power + '%';
                this.game.shotPower = (power / 100) * this.game.maxPower;
            });
            
            shootBtn.addEventListener('click', () => {
                if (this.game.cueBall && !this.game.cueBall.potted && this.game.shotPower > 0) {
                    // Check if balls are moving
                    const ballsMoving = this.game.balls.some(b => !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01));
                    if (ballsMoving) return;
                    
                    const power = this.game.shotPower * (this.game.powerMultiplier || 1.0);
                    this.game.cueBall.vx = Math.cos(this.game.aimAngle) * power;
                    this.game.cueBall.vy = Math.sin(this.game.aimAngle) * power;
                    
                    // Apply spin
                    if (typeof PoolSpinControl !== 'undefined') {
                        PoolSpinControl.applySpinToBall(this.game.cueBall, this.game.aimAngle);
                    }
                    
                    this.game.isAiming = false;
                    slider.value = 50;
                    valueDisplay.textContent = '50%';
                    this.game.shotPower = this.game.maxPower * 0.5;
                }
            });
        }
        
        // Show/hide based on mode
        if (this.game.shotControlMode === 'slider') {
            sliderPanel.style.display = 'flex';
        }
    },
    
    /**
     * Tap & Hold mode
     * Tap to start aiming, hold to build power, release to shoot
     */
    setupTapMode() {
        let holdStartTime = 0;
        let holdInterval = null;
        
        const startHold = (e) => {
            if (this.game.shotControlMode !== 'tap') return;
            if (!this.game.cueBall || this.game.cueBall.potted) return;
            
            // Check if balls are moving
            const ballsMoving = this.game.balls.some(b => !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01));
            if (ballsMoving) return;
            
            holdStartTime = Date.now();
            this.game.shotPower = 0;
            this.game.isAiming = true;
            
            // Build power over time
            holdInterval = setInterval(() => {
                const elapsed = Date.now() - holdStartTime;
                const powerPercent = Math.min(elapsed / 1500, 1); // 1.5 seconds to max
                this.game.shotPower = powerPercent * this.game.maxPower;
            }, 16);
        };
        
        const releaseHold = (e) => {
            if (this.game.shotControlMode !== 'tap') return;
            
            clearInterval(holdInterval);
            
            if (this.game.shotPower > 0 && this.game.cueBall && !this.game.cueBall.potted) {
                const power = this.game.shotPower * (this.game.powerMultiplier || 1.0);
                this.game.cueBall.vx = Math.cos(this.game.aimAngle) * power;
                this.game.cueBall.vy = Math.sin(this.game.aimAngle) * power;
                
                // Apply spin
                if (typeof PoolSpinControl !== 'undefined') {
                    PoolSpinControl.applySpinToBall(this.game.cueBall, this.game.aimAngle);
                }
                
                this.game.shotPower = 0;
                this.game.isAiming = false;
            }
        };
        
        this.canvas.addEventListener('mousedown', startHold);
        this.canvas.addEventListener('mouseup', releaseHold);
        this.canvas.addEventListener('touchstart', startHold);
        this.canvas.addEventListener('touchend', releaseHold);
    },
    
    /**
     * Swipe mode
     * Swipe across the cue ball - speed determines power
     */
    setupSwipeMode() {
        let swipeStart = null;
        let swipeStartTime = 0;
        
        const startSwipe = (e) => {
            if (this.game.shotControlMode !== 'swipe') return;
            if (!this.game.cueBall || this.game.cueBall.potted) return;
            
            // Check if balls are moving
            const ballsMoving = this.game.balls.some(b => !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01));
            if (ballsMoving) return;
            
            const rect = this.canvas.getBoundingClientRect();
            const x = (e.touches ? e.touches[0].clientX : e.clientX) - rect.left;
            const y = (e.touches ? e.touches[0].clientY : e.clientY) - rect.top;
            
            // Scale to canvas coordinates
            const scaleX = this.canvas.width / rect.width;
            const scaleY = this.canvas.height / rect.height;
            
            swipeStart = { x: x * scaleX, y: y * scaleY };
            swipeStartTime = Date.now();
            this.game.isAiming = true;
        };
        
        const endSwipe = (e) => {
            if (this.game.shotControlMode !== 'swipe') return;
            if (!swipeStart) return;
            
            const rect = this.canvas.getBoundingClientRect();
            const x = (e.changedTouches ? e.changedTouches[0].clientX : e.clientX) - rect.left;
            const y = (e.changedTouches ? e.changedTouches[0].clientY : e.clientY) - rect.top;
            
            const scaleX = this.canvas.width / rect.width;
            const scaleY = this.canvas.height / rect.height;
            
            const swipeEnd = { x: x * scaleX, y: y * scaleY };
            const swipeTime = Date.now() - swipeStartTime;
            
            // Calculate swipe distance and speed
            const dx = swipeEnd.x - swipeStart.x;
            const dy = swipeEnd.y - swipeStart.y;
            const distance = Math.sqrt(dx * dx + dy * dy);
            const speed = distance / Math.max(swipeTime, 1); // pixels per ms
            
            // Convert speed to power (cap at maxPower)
            const power = Math.min(speed * 20, this.game.maxPower) * (this.game.powerMultiplier || 1.0);
            
            // Calculate angle from swipe direction
            const angle = Math.atan2(dy, dx);
            
            if (this.game.cueBall && !this.game.cueBall.potted && power > 2) {
                this.game.cueBall.vx = Math.cos(angle) * power;
                this.game.cueBall.vy = Math.sin(angle) * power;
                
                // Apply spin
                if (typeof PoolSpinControl !== 'undefined') {
                    PoolSpinControl.applySpinToBall(this.game.cueBall, angle);
                }
                
                this.game.isAiming = false;
            }
            
            swipeStart = null;
        };
        
        this.canvas.addEventListener('mousedown', startSwipe);
        this.canvas.addEventListener('mouseup', endSwipe);
        this.canvas.addEventListener('touchstart', startSwipe);
        this.canvas.addEventListener('touchend', endSwipe);
    },
    
    /**
     * Update UI based on current shot mode
     */
    updateModeUI() {
        // Show/hide slider panel
        const sliderPanel = document.getElementById('sliderPowerPanel');
        if (sliderPanel) {
            sliderPanel.style.display = this.game.shotControlMode === 'slider' ? 'flex' : 'none';
        }
        
        // Update status message
        const modeMessages = {
            drag: 'Drag back and release to shoot',
            click: 'Click and hold to charge power',
            slider: 'Use slider to set power, then shoot',
            tap: 'Tap and hold to build power',
            swipe: 'Swipe across cue ball to shoot'
        };
        
        const message = modeMessages[this.game.shotControlMode] || 'Click to shoot';
        if (this.game.statusEl) {
            // Don't override game messages, just log
            console.log('Shot mode:', message);
        }
    },
    
    /**
     * Draw mode-specific visual feedback
     */
    drawModeFeedback(ctx) {
        if (!this.game.cueBall || this.game.cueBall.potted) return;
        
        // Click power mode - show charging ring
        if (this.game.shotControlMode === 'click' && this.game.clickPowerCharging) {
            const powerPercent = this.game.shotPower / this.game.maxPower;
            const ringRadius = this.game.cueBall.r + 15;
            
            ctx.strokeStyle = `rgba(74, 222, 128, ${0.6 + powerPercent * 0.4})`;
            ctx.lineWidth = 4;
            ctx.beginPath();
            ctx.arc(
                this.game.cueBall.x,
                this.game.cueBall.y,
                ringRadius,
                -Math.PI / 2,
                -Math.PI / 2 + (powerPercent * Math.PI * 2)
            );
            ctx.stroke();
            
            // Power percentage text
            ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
            ctx.font = 'bold 16px Arial';
            ctx.textAlign = 'center';
            ctx.fillText(
                Math.round(powerPercent * 100) + '%',
                this.game.cueBall.x,
                this.game.cueBall.y - ringRadius - 10
            );
        }
        
        // Tap & hold mode - show power build-up
        if (this.game.shotControlMode === 'tap' && this.game.isAiming && this.game.shotPower > 0) {
            const powerPercent = this.game.shotPower / this.game.maxPower;
            const barWidth = 100;
            const barHeight = 12;
            const barX = this.game.cueBall.x - barWidth / 2;
            const barY = this.game.cueBall.y + this.game.cueBall.r + 25;
            
            // Background
            ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
            ctx.fillRect(barX, barY, barWidth, barHeight);
            
            // Power fill
            const fillColor = powerPercent < 0.5 ? '#4ade80' : powerPercent < 0.8 ? '#fbbf24' : '#ef4444';
            ctx.fillStyle = fillColor;
            ctx.fillRect(barX, barY, barWidth * powerPercent, barHeight);
            
            // Border
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.4)';
            ctx.lineWidth = 1;
            ctx.strokeRect(barX, barY, barWidth, barHeight);
        }
    }
};
";
    }
}
