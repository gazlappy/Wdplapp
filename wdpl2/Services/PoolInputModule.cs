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
    
/**
 * Setup mouse controls for pull-back/push-forward cueing
 */
setupMouseControls(canvas, game, statusEl) {
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
                // Update aim angle
                const dx = game.mouseX - game.cueBall.x;
                const dy = game.mouseY - game.cueBall.y;
                game.aimAngle = Math.atan2(dy, dx);
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
                // Update aim angle
                const dx = game.mouseX - game.cueBall.x;
                const dy = game.mouseY - game.cueBall.y;
                game.aimAngle = Math.atan2(dy, dx);
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
                cueBall.vx = (dx / dist) * power;
                cueBall.vy = (dy / dist) * power;
                
                statusEl.textContent = `Shot fired! Power: ${power.toFixed(1)}`;
                statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
            }
        });
    }
};
";
    }
}
