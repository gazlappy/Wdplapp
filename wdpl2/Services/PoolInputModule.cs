namespace Wdpl2.Services;

/// <summary>
/// Input module for pool game - handles mouse and touch controls
/// </summary>
public static class PoolInputModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL INPUT MODULE
// Handles mouse and touch input for cueing
// ============================================


const PoolInput = {
// Ball in hand dragging state
isDraggingCueBall: false,

// Fine tune aim state
fineTuneActive: false,
fineTuneSensitivity: 0.15, // 15% of normal sensitivity when fine tuning
microAdjustStep: 0.002, // Configurable micro-adjustment step
lastAimAngle: 0,

// Prediction line HUD state
predictionHudVisible: false,
predictionLengthStep: 25,
predictionPresets: [
    { label: 'OFF', length: 0, show: false },
    { label: 'SHORT', length: 100, show: true },
    { label: 'MEDIUM', length: 200, show: true },
    { label: 'LONG', length: 350, show: true },
    { label: 'FULL', length: 500, show: true }
],
predictionPresetIndex: 2, // Start on MEDIUM
    
/**
 * Setup keyboard controls for fine-tune aiming
 */
setupKeyboardControls(game) {
    // Create prediction HUD (hidden by default)
    this.createPredictionHud(game);

    // Track fine-tune key state
    document.addEventListener('keydown', (e) => {
        // Period key (.) or > key for fine-tune aiming
        if (e.key === '.' || e.key === '>') {
            if (!this.fineTuneActive) {
                this.fineTuneActive = true;
                this.lastAimAngle = game.aimAngle;
                console.log('Fine-tune aim: ON (15% sensitivity)');

                // Show indicator
                this.showFineTuneIndicator(true);
            }
        }

        // P key: toggle prediction line on/off
        // Shift+P: cycle through presets (OFF / SHORT / MEDIUM / LONG / FULL)
        if (e.key === 'p' || e.key === 'P') {
            e.preventDefault();
            if (e.shiftKey) {
                // Cycle through presets
                this.predictionPresetIndex = (this.predictionPresetIndex + 1) % this.predictionPresets.length;
                const preset = this.predictionPresets[this.predictionPresetIndex];
                game.showTrajectoryPrediction = preset.show;
                if (preset.show) game.trajectoryLength = preset.length;
                this.updatePredictionHud(game);
                this.showPredictionToast(preset.label);
            } else {
                // Simple toggle
                game.showTrajectoryPrediction = !game.showTrajectoryPrediction;
                // Sync preset index
                if (!game.showTrajectoryPrediction) {
                    this.predictionPresetIndex = 0;
                } else {
                    // Find nearest preset
                    const len = game.trajectoryLength;
                    let best = 2;
                    let bestDiff = Infinity;
                    for (let i = 1; i < this.predictionPresets.length; i++) {
                        const diff = Math.abs(this.predictionPresets[i].length - len);
                        if (diff < bestDiff) { bestDiff = diff; best = i; }
                    }
                    this.predictionPresetIndex = best;
                }
                this.updatePredictionHud(game);
                this.showPredictionToast(game.showTrajectoryPrediction ? 'PREDICTION ON' : 'PREDICTION OFF');
            }
        }

        // Arrow keys for micro-adjustments when fine-tuning
        if (this.fineTuneActive && game.isAiming && !game.isShooting) {
            const microStep = this.microAdjustStep; // Use configurable step
            if (e.key === 'ArrowLeft') {
                game.aimAngle -= microStep;
                e.preventDefault();
            } else if (e.key === 'ArrowRight') {
                game.aimAngle += microStep;
                e.preventDefault();
            }
        }
    });
    
    document.addEventListener('keyup', (e) => {
        if (e.key === '.' || e.key === '>') {
            this.fineTuneActive = false;
            console.log('Fine-tune aim: OFF');
            this.showFineTuneIndicator(false);
        }
    });
},

/**
 * Show/hide fine-tune indicator on screen
 */
showFineTuneIndicator(show) {
    let indicator = document.getElementById('fineTuneIndicator');

    if (show) {
        const sensitivityPercent = Math.round(this.fineTuneSensitivity * 100);
        if (!indicator) {
            indicator = document.createElement('div');
            indicator.id = 'fineTuneIndicator';
            indicator.style.cssText = `
                position: fixed;
                top: 60px;
                left: 50%;
                transform: translateX(-50%);
                background: rgba(59, 130, 246, 0.95);
                color: white;
                padding: 10px 20px;
                border-radius: 8px;
                font-weight: bold;
                font-size: 14px;
                text-align: center;
                z-index: 10000;
                box-shadow: 0 4px 12px rgba(0,0,0,0.3);
                animation: pulse 1s infinite;
            `;
            document.body.appendChild(indicator);

            // Add pulse animation if not exists
            if (!document.getElementById('fineTuneStyles')) {
                const style = document.createElement('style');
                style.id = 'fineTuneStyles';
                style.textContent = `
                    @keyframes pulse {
                        0%, 100% { opacity: 1; transform: translateX(-50%) scale(1); }
                        50% { opacity: 0.8; transform: translateX(-50%) scale(1.02); }
                    }
                `;
                document.head.appendChild(style);
            }
        }
        indicator.innerHTML = `🎯 FINE AIM (${sensitivityPercent}%)<br><small>← → for micro-adjust</small>`;
        indicator.style.display = 'block';
    } else if (indicator) {
        indicator.style.display = 'none';
    }
},

/**
 * Create the in-game prediction length HUD
 */
createPredictionHud(game) {
    if (document.getElementById('predictionHud')) return;

    const hud = document.createElement('div');
    hud.id = 'predictionHud';
    hud.style.cssText = `
        position: fixed;
        bottom: 50px;
        right: 15px;
        background: rgba(15, 23, 42, 0.88);
        border: 1px solid rgba(100, 200, 255, 0.35);
        border-radius: 10px;
        padding: 10px 14px;
        color: white;
        font-family: Arial, sans-serif;
        font-size: 12px;
        z-index: 9990;
        min-width: 170px;
        box-shadow: 0 4px 16px rgba(0,0,0,0.4);
        user-select: none;
        transition: opacity 0.2s;
    `;
    hud.innerHTML = `
        <div style=""display:flex;justify-content:space-between;align-items:center;margin-bottom:6px;"">
            <span style=""font-weight:bold;color:rgba(100,200,255,0.9);"">Prediction Line</span>
            <span id=""predictionPresetLabel"" style=""font-size:11px;color:rgba(74,222,128,0.9);font-weight:bold;"">MEDIUM</span>
        </div>
        <div style=""display:flex;align-items:center;gap:8px;"">
            <input type=""range"" id=""predictionLengthSlider"" min=""50"" max=""500"" value=""200"" step=""25""
                style=""flex:1;height:6px;accent-color:rgba(100,200,255,0.8);cursor:pointer;"">
            <span id=""predictionLengthLabel"" style=""min-width:32px;text-align:right;font-weight:bold;"">200</span>
        </div>
        <div style=""margin-top:6px;font-size:10px;color:rgba(255,255,255,0.4);text-align:center;"">
            P = toggle &nbsp;|&nbsp; Shift+P = cycle preset
        </div>
    `;
    document.body.appendChild(hud);

    // Slider event
    const slider = document.getElementById('predictionLengthSlider');
    slider.addEventListener('input', (e) => {
        const val = parseInt(e.target.value);
        game.trajectoryLength = val;
        document.getElementById('predictionLengthLabel').textContent = val;

        // Update dev settings slider if open
        const devSlider = document.getElementById('trajectoryLength');
        if (devSlider) {
            devSlider.value = val;
            const devLabel = document.getElementById('trajectoryLengthValue');
            if (devLabel) devLabel.textContent = val;
        }

        // Find nearest preset
        let best = 1;
        let bestDiff = Infinity;
        for (let i = 1; i < this.predictionPresets.length; i++) {
            const diff = Math.abs(this.predictionPresets[i].length - val);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        this.predictionPresetIndex = best;
        document.getElementById('predictionPresetLabel').textContent = this.predictionPresets[best].label;
    });

    this.updatePredictionHud(game);
},

/**
 * Sync prediction HUD with current game state
 */
updatePredictionHud(game) {
    const hud = document.getElementById('predictionHud');
    if (!hud) return;

    const visible = game.showTrajectoryPrediction;
    hud.style.opacity = visible ? '1' : '0.45';

    const slider = document.getElementById('predictionLengthSlider');
    if (slider) {
        slider.value = game.trajectoryLength;
        slider.disabled = !visible;
    }

    const label = document.getElementById('predictionLengthLabel');
    if (label) label.textContent = visible ? game.trajectoryLength : 'OFF';

    const preset = document.getElementById('predictionPresetLabel');
    if (preset) preset.textContent = this.predictionPresets[this.predictionPresetIndex].label;

    // Also sync dev settings slider
    const devSlider = document.getElementById('trajectoryLength');
    if (devSlider) {
        devSlider.value = game.trajectoryLength;
        const devLabel = document.getElementById('trajectoryLengthValue');
        if (devLabel) devLabel.textContent = game.trajectoryLength;
    }
    const devCheckbox = document.getElementById('showTrajectory');
    if (devCheckbox) devCheckbox.checked = visible;
},

/**
 * Show a brief toast for prediction state changes
 */
showPredictionToast(text) {
    let toast = document.getElementById('predictionToast');
    if (!toast) {
        toast = document.createElement('div');
        toast.id = 'predictionToast';
        toast.style.cssText = `
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: rgba(15, 23, 42, 0.92);
            border: 1px solid rgba(100, 200, 255, 0.5);
            color: rgba(100, 200, 255, 1);
            padding: 12px 28px;
            border-radius: 10px;
            font-weight: bold;
            font-size: 18px;
            font-family: Arial, sans-serif;
            z-index: 20000;
            pointer-events: none;
            transition: opacity 0.3s;
        `;
        document.body.appendChild(toast);
    }
    toast.textContent = text;
    toast.style.opacity = '1';
    clearTimeout(this._predToastTimeout);
    this._predToastTimeout = setTimeout(() => { toast.style.opacity = '0'; }, 800);
},

/**
 * Apply fine-tune sensitivity to aim angle change
 */
applyFineTuneAim(game, newAngle) {
    if (this.fineTuneActive) {
        // Calculate the difference and apply reduced sensitivity
        const angleDiff = newAngle - this.lastAimAngle;
        game.aimAngle = this.lastAimAngle + (angleDiff * this.fineTuneSensitivity);
        this.lastAimAngle = game.aimAngle;
    } else {
        game.aimAngle = newAngle;
        this.lastAimAngle = newAngle;
    }
},

/**
 * Setup mouse controls for pull-back/push-forward cueing
 */
setupMouseControls(canvas, game, statusEl) {
    // Initialize keyboard controls for fine-tune
    this.setupKeyboardControls(game);
    
    canvas.addEventListener('mousemove', (e) => {
        if (!game.cueBall || game.cueBall.potted) return;
            
        const rect = canvas.getBoundingClientRect();
        const scaleX = canvas.width / rect.width;
        const scaleY = canvas.height / rect.height;
        game.mouseX = (e.clientX - rect.left) * scaleX;
        game.mouseY = (e.clientY - rect.top) * scaleY;
            
        // Handle ball-in-hand dragging
        if (game.ballInHand && this.isDraggingCueBall) {
            // Move cue ball with mouse (preview position)
            game.cueBall.x = game.mouseX;
            game.cueBall.y = game.mouseY;
            return;
        }
            
            // Skip all shooting controls if ball-in-hand is active
            if (game.ballInHand) {
                return;
            }
            
            // Skip shooting controls if not using drag mode
            if (game.shotControlMode && game.shotControlMode !== 'drag') {
                // Update aim angle with fine-tune support
                const dx = game.mouseX - game.cueBall.x;
                const dy = game.mouseY - game.cueBall.y;
                const newAngle = Math.atan2(dy, dx);
                this.applyFineTuneAim(game, newAngle);
                game.isAiming = true;
                return;
            }
            
            if (game.isShooting) {
                // Track vertical movement for power
                const deltaY = game.mouseY - game.dragStartY;
                
                if (deltaY > 0) {
                    // Pulling back
                    game.pullBackDistance = Math.min(deltaY, 150);
                    game.shotPower = (game.pullBackDistance / 150) * game.maxPower;
                    game.pushForwardDistance = 0;
                } else {
                    // Pushing forward
                    const pushDist = Math.abs(deltaY);
                    game.pushForwardDistance = Math.min(pushDist, game.pullBackDistance + 50);
                    
                    // Check for contact
                    const cueDistance = 35 + game.pullBackDistance - game.pushForwardDistance;
                    if (cueDistance <= 12) {
                        // CONTACT!
                        const speed = Math.min(game.shotPower, game.maxPower) * 2.5;
                        if (speed > 0.5) {
                            // Start shot tracking for rules
                            if (typeof game.startShot === 'function') {
                                game.startShot();
                            }
                            
                            // PLAY CUE HIT SOUND
                            const hitPower = speed / game.maxPower;
                            console.log(`Cue hit! Power: ${hitPower.toFixed(2)}, Speed: ${speed.toFixed(1)}`);
                            if (typeof PoolAudio !== 'undefined') {
                                PoolAudio.play('cueHit', hitPower);
                            } else {
                                console.warn('PoolAudio not available for cue hit');
                            }

                            // ?? CHALK DUST PARTICLES (#5)
                            if (typeof PoolVFX !== 'undefined' && hitPower > 0.3) {
                                const tipX = game.cueBall.x - Math.cos(game.aimAngle) * 12;
                                const tipY = game.cueBall.y - Math.sin(game.aimAngle) * 12;
                                PoolVFX.spawnChalkDust(tipX, tipY, hitPower);
                            }
                            
                            game.cueBall.vx = Math.cos(game.aimAngle) * speed;
                            game.cueBall.vy = Math.sin(game.aimAngle) * speed;
                            
                            // Apply spin from spin control
                            PoolSpinControl.applySpinToBall(game.cueBall, game.aimAngle);
                            
                            const spinInfo = (Math.abs(PoolSpinControl.spinX) > 0.05 || Math.abs(PoolSpinControl.spinY) > 0.05) 
                                ? ' | Spin: ' + Math.round(game.cueBall.spinMagnitude * 100) + '%' 
                                : '';
                            
                            
                            statusEl.textContent = `Contact! Power: ${speed.toFixed(1)}${spinInfo}`;
                            statusEl.style.background = 'rgba(59, 130, 246, 0.9)';
                        }
                        
                        // Reset
                        game.isShooting = false;
                        game.shotPower = 0;
                        game.pullBackDistance = 0;
                        game.pushForwardDistance = 0;
                    }
                }
            } else {
                // Update aim angle with fine-tune support
                const dx = game.mouseX - game.cueBall.x;
                const dy = game.mouseY - game.cueBall.y;
                const newAngle = Math.atan2(dy, dx);
                this.applyFineTuneAim(game, newAngle);
                game.isAiming = true;
            }
        });
        
        canvas.addEventListener('mousedown', (e) => {
            // Handle ball-in-hand - start dragging
            if (game.ballInHand && game.cueBall && !game.cueBall.potted) {
                const rect = canvas.getBoundingClientRect();
                const scaleX = canvas.width / rect.width;
                const scaleY = canvas.height / rect.height;
                const clickX = (e.clientX - rect.left) * scaleX;
                const clickY = (e.clientY - rect.top) * scaleY;
                
                // Start dragging the cue ball
                this.isDraggingCueBall = true;
                game.cueBall.x = clickX;
                game.cueBall.y = clickY;
                
                statusEl.textContent = 'Drag to position cue ball, release to place';
                statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
                return;
            }
            
            // Skip if not using drag mode
            if (game.shotControlMode && game.shotControlMode !== 'drag') return;
            
            const cue = game.balls.find(b => b.num === 0 && !b.potted);
            if (!cue) return;
            
            // Check if balls are moving
            const ballsMoving = game.balls.some(b => !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01));
            if (ballsMoving) return;
            
            // Lock aim
            const dx = game.mouseX - game.cueBall.x;
            const dy = game.mouseY - game.cueBall.y;
            game.aimAngle = Math.atan2(dy, dx);
            
            // Start drag
            game.dragStartY = game.mouseY;
            game.isShooting = true;
            game.pullBackDistance = 0;
            game.pushForwardDistance = 0;
            game.shotPower = 0;
            
            statusEl.textContent = 'Aim locked! Move DOWN to pull back, UP to strike!';
            statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
        });
        
        canvas.addEventListener('mouseup', (e) => {
            // Handle ball-in-hand placement on release
            if (this.isDraggingCueBall && game.ballInHand) {
                this.isDraggingCueBall = false;
                
                const rect = canvas.getBoundingClientRect();
                const scaleX = canvas.width / rect.width;
                const scaleY = canvas.height / rect.height;
                const releaseX = (e.clientX - rect.left) * scaleX;
                const releaseY = (e.clientY - rect.top) * scaleY;
                
                if (typeof game.placeCueBall === 'function') {
                    const placed = game.placeCueBall(releaseX, releaseY);
                    if (!placed) {
                        statusEl.textContent = 'Invalid position! Try again - avoid other balls';
                        statusEl.style.background = 'rgba(239, 68, 68, 0.9)';
                    }
                }
                return;
            }
            
            // Skip if not using drag mode
            if (game.shotControlMode && game.shotControlMode !== 'drag') return;
            
            if (!game.isShooting) return;
            
            // Shot cancelled
            game.isShooting = false;
            game.shotPower = 0;
            game.pullBackDistance = 0;
            game.pushForwardDistance = 0;
            
            statusEl.textContent = 'Shot cancelled - push forward to make contact!';
            statusEl.style.background = 'rgba(239, 68, 68, 0.9)';
            
            setTimeout(() => {
                if (!game.isShooting && !game.ballInHand) {
                    game.updateTurnDisplay();
                }
            }, 1500);
        });
        
        canvas.addEventListener('mouseleave', () => {
            // Cancel ball dragging if mouse leaves canvas
            if (this.isDraggingCueBall) {
                this.isDraggingCueBall = false;
            }
            game.isAiming = false;
        });
    },
    
    /**
     * Setup touch controls
     */
    setupTouchControls(canvas, game, statusEl) {
        let touchStartX, touchStartY, touchEndX, touchEndY;
        let isTouching = false;
        let isDraggingCueBallTouch = false;
        
        canvas.addEventListener('touchstart', (e) => {
            const touch = e.touches[0];
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const scaleY = canvas.height / rect.height;
            const touchX = (touch.clientX - rect.left) * scaleX;
            const touchY = (touch.clientY - rect.top) * scaleY;
            
            // Handle ball-in-hand - start dragging
            if (game.ballInHand && game.cueBall && !game.cueBall.potted) {
                isDraggingCueBallTouch = true;
                game.cueBall.x = touchX;
                game.cueBall.y = touchY;
                
                statusEl.textContent = 'Drag to position cue ball, release to place';
                statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
                e.preventDefault();
                return;
            }
            
            // Skip if not using drag mode
            if (game.shotControlMode && game.shotControlMode !== 'drag') return;
            
            isTouching = true;
            touchStartX = touch.clientX;
            touchStartY = touch.clientY;
            touchEndX = touch.clientX;
            touchEndY = touch.clientY;
            e.preventDefault();
        }, { passive: false });
        
        canvas.addEventListener('touchmove', (e) => {
            const touch = e.touches[0];
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const scaleY = canvas.height / rect.height;
            const touchX = (touch.clientX - rect.left) * scaleX;
            const touchY = (touch.clientY - rect.top) * scaleY;
            
            // Handle ball-in-hand dragging
            if (isDraggingCueBallTouch && game.ballInHand) {
                game.cueBall.x = touchX;
                game.cueBall.y = touchY;
                e.preventDefault();
                return;
            }
            
            // Skip if not using drag mode
            if (game.shotControlMode && game.shotControlMode !== 'drag') return;
            
            if (!isTouching) return;
            
            touchEndX = touch.clientX;
            touchEndY = touch.clientY;
            
            // Calculate aim direction
            const dx = touchEndX - touchStartX;
            const dy = touchEndY - touchStartY;
            game.aimAngle = Math.atan2(dy, dx);
            
            // Update power
            const distance = Math.min(Math.sqrt(dx * dx + dy * dy), 100);
            game.shotPower = distance / 5;
        });
        
        canvas.addEventListener('touchend', (e) => {
            // Handle ball-in-hand placement on release
            if (isDraggingCueBallTouch && game.ballInHand) {
                isDraggingCueBallTouch = false;
                
                const touch = e.changedTouches[0];
                const rect = canvas.getBoundingClientRect();
                const scaleX = canvas.width / rect.width;
                const scaleY = canvas.height / rect.height;
                const releaseX = (touch.clientX - rect.left) * scaleX;
                const releaseY = (touch.clientY - rect.top) * scaleY;
                
                if (typeof game.placeCueBall === 'function') {
                    const placed = game.placeCueBall(releaseX, releaseY);
                    if (!placed) {
                        statusEl.textContent = 'Invalid position! Try again - avoid other balls';
                        statusEl.style.background = 'rgba(239, 68, 68, 0.9)';
                    }
                }
                return;
            }
            
            // Skip if not using drag mode
            if (game.shotControlMode && game.shotControlMode !== 'drag') return;
            
            isTouching = false;
            
            const cueBall = game.balls.find(b => b.num === 0 && !b.potted);
            if (!cueBall) return;
            
            const dx = touchEndX - touchStartX;
            const dy = touchEndY - touchStartY;
            const dist = Math.sqrt(dx * dx + dy * dy);
            
            if (dist > 5) {
                // Start shot tracking for rules
                if (typeof game.startShot === 'function') {
                    game.startShot();
                }

                const power = Math.min(dist / 15, 20);
                const angle = Math.atan2(dy, dx);
                cueBall.vx = (dx / dist) * power;
                cueBall.vy = (dy / dist) * power;

                // Apply spin from spin control (same as mouse handler)
                if (typeof PoolSpinControl !== 'undefined') {
                    PoolSpinControl.applySpinToBall(cueBall, angle);
                }

                // Play cue hit sound
                const hitPower = power / (game.maxPower || 40);
                if (typeof PoolAudio !== 'undefined') {
                    PoolAudio.play('cueHit', hitPower);
                }

                // Chalk dust particles
                if (typeof PoolVFX !== 'undefined' && hitPower > 0.3) {
                    const tipX = cueBall.x - Math.cos(angle) * 12;
                    const tipY = cueBall.y - Math.sin(angle) * 12;
                    PoolVFX.spawnChalkDust(tipX, tipY, hitPower);
                }

                statusEl.textContent = `Shot fired! Power: ${power.toFixed(1)}`;
                statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
            }
        });
    }
};
";
    }
}
