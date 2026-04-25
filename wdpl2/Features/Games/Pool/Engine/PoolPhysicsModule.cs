namespace Wdpl2.Services;

/// <summary>
/// Physics module for pool game - handles ball movement, collisions, and friction
/// ENHANCED: Realistic rolling with rotation tracking
/// </summary>
public static class PoolPhysicsModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL PHYSICS MODULE (ENHANCED)
// Realistic ball physics with rotation
// ============================================

const PoolPhysics = {
    // Constants - WPA 2026 OFFICIAL STANDARDS
    FRICTION: 0.987,  // (legacy) retained for any external reference
    CUSHION_RESTITUTION: 0.95,  // WPA 2026: 0.92-0.98 (using mid-high)
    MIN_VELOCITY: 0.05,  // Slightly higher so the ball snaps cleanly to rest at the end of its roll
    COLLISION_DAMPING: 0.96,  // WPA 2026: 0.92-0.98 coefficient of restitution

    // Realistic deceleration model (replaces pure multiplicative FRICTION).
    // A rolling ball loses speed at an approximately CONSTANT rate per frame
    // (rolling resistance), not exponentially -- exponential decay is what made
    // the balls feel like they were gliding on glass forever.
    ROLLING_DECEL: 0.055,   // px/frame (~3.3 px/s^2 at 60fps) for a fully rolling ball
    SLIDING_DECEL: 0.110,   // px/frame -- faster decel while sliding (kinetic friction)
    VISCOUS_DRAG: 0.992,    // small velocity-proportional component (air + cloth viscous drag)
    
    // WPA 2026 Physical Constants
    BALL_TO_BALL_FRICTION: 0.055,  // WPA 2026: 0.03-0.08 (determines throw)
    BALL_TO_CLOTH_SLIDING: 0.25,   // WPA 2026: 0.15-0.40 (sliding friction)
    ROLLING_RESISTANCE: 0.010,     // WPA 2026: 0.005-0.015
    MOMENT_OF_INERTIA_FACTOR: 0.4, // 2/5 for solid sphere (2/5 = 0.4)
    
    // Spin limits - WPA 2026
    MISCUE_LIMIT: 0.5,             // Max offset = 0.5 * radius
    MAX_SPIN_RPM: 4000,            // Max RPM: 3000-5000
    SPIN_DECAY_RATE: 10,           // rad/sec^2: 5-15
    
    // Ball specifications - WPA 2026
    STANDARD_BALL_MASS: 163,       // grams (156-170g range, using mid-point)
    STANDARD_BALL_DIAMETER: 57.15, // mm (2.25 inches)
    CUE_BALL_MASS_VARIANCE: 1.05,  // Commercial cue balls can be 5% heavier
    OBJECT_BALL_MASS_VARIANCE: 1.05, // Object balls can also vary (1.025 to 1.075 actual range)
    
    /**
     * Apply friction with rolling rotation and spin effects
     * ENHANCED: WPA 2026 standards with moment of inertia and 5/7 rule
     */
    applyFriction(ball) {
        const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
        
        if (speed > this.MIN_VELOCITY) {
            // Store initial direction
            const initialAngle = Math.atan2(ball.vy, ball.vx);

            // ===== REALISTIC DECELERATION =====
            // Real pool balls decelerate at a near-constant rate from rolling resistance,
            // with a small extra component from viscous drag. Pure multiplicative friction
            // (v *= 0.987) feels 'glassy' because it never actually reaches zero.
            // Sliding balls (mismatched spin/translation) decelerate faster.
            const isSliding = !ball.slidingComplete;
            const constDecel = isSliding ? this.SLIDING_DECEL : this.ROLLING_DECEL;
            const newSpeed = Math.max(0, speed * this.VISCOUS_DRAG - constDecel);
            const ratio = newSpeed / speed;
            ball.vx *= ratio;
            ball.vy *= ratio;
            
            // WPA 2026: Calculate angular velocity (omega = v/r)
            if (!ball.omega) ball.omega = 0; // Angular velocity in rad/sec
            
            // Check if ball is in pure rolling (5/7 rule)
            const expectedOmega = speed / (ball.r || 14);
            const isPureRolling = Math.abs(ball.omega - expectedOmega) < 0.1;
            
            if (isPureRolling && !ball.slidingComplete) {
                console.log('5/7 RULE! Ball transitioned to pure rolling, v = omega * R');
                ball.slidingComplete = true;
            }
            
            // REALISTIC SPIN PHYSICS:
            // In real pool, spin affects the ball through the Magnus effect and friction
            
            // SIDE SPIN (English) - Magnus effect creates lateral force
            if (ball.spinX !== undefined && Math.abs(ball.spinX) > 0.01) {
                // In real pool, English has NEARLY ZERO effect during roll
                // It's stored on the ball to affect cushions and throw only
                const magnusForce = ball.spinX * 0.002; // MINIMAL
                const perpAngle = initialAngle + Math.PI / 2;
                
                ball.vx += Math.cos(perpAngle) * magnusForce;
                ball.vy += Math.sin(perpAngle) * magnusForce;
                
                // WPA 2026: Spin decay rate 5-15 rad/sec^2
                // Convert to per-frame decay (assuming 60fps)
                const spinDecayPerFrame = this.SPIN_DECAY_RATE / 60;
                const currentSpinSpeed = Math.abs(ball.spinX);
                const decayFactor = Math.max(0, 1 - (spinDecayPerFrame / Math.max(currentSpinSpeed, 0.01)));
                ball.spinX *= decayFactor;
            }
            
            // TOP/BACK SPIN - This is where real pool physics happens
            if (ball.spinY !== undefined && Math.abs(ball.spinY) > 0.01) {
                // In real pool, the ball initially SLIDES on the cloth with spin
                // Then the spin gradually converts to rolling motion
                // This creates a PARABOLIC PATH as described in physics literature
                
                // WPA 2026: Ball-to-cloth sliding friction (0.15-0.40)
                const slidingFriction = this.BALL_TO_CLOTH_SLIDING;
                
                // Check if ball is still in sliding phase
                if (!ball.slidingComplete) {
                    // Get initial sliding intensity from sweet spot calculation
                    const slidingMultiplier = ball.initialSlidingIntensity || 1.0;
                    
                    // During sliding, spin creates friction in the direction of spin
                    // This creates the initial part of the parabolic curve
                    const spinFriction = ball.spinY * 0.15 * slidingMultiplier * slidingFriction;
                    
                    // PARABOLIC CURVE: Acceleration is proportional to spin and decreases over time
                    // As spin converts to roll, the curve flattens out
                    const spinToRollRatio = Math.abs(ball.spinY) / 2.0; // 0 to 1 scale
                    const curveIntensity = spinToRollRatio * 0.2 * slidingMultiplier; // Parabolic curve factor
                    
                    ball.vx += Math.cos(initialAngle) * (spinFriction + curveIntensity);
                    ball.vy += Math.sin(initialAngle) * (spinFriction + curveIntensity);
                    
                    // Spin decays faster during sliding (converts to linear motion)
                    // Decay rate affected by strike quality
                    const decayRate = 0.92 + (slidingMultiplier * 0.05); // Worse strikes decay faster
                    ball.spinY *= decayRate;
                    
                    // Track distance traveled in sliding phase for parabolic calculation
                    if (!ball.slideDistance) ball.slideDistance = 0;
                    ball.slideDistance += speed;
                    
                    // Check if sliding is complete (spin matches roll) - 5/7 RULE
                    // The parabolic curve completes when sliding ends
                    const expectedRoll = speed * 10; // Arbitrary units
                    if (Math.abs(ball.spinY) < expectedRoll * 0.1) {
                        ball.slidingComplete = true;
                        ball.omega = speed / (ball.r || 14); // Set proper angular velocity
                        console.log('Parabolic curve complete! Distance:', ball.slideDistance.toFixed(1), 'Final speed:', speed.toFixed(2), 'Pure roll achieved');
                    }
                } else {
                    // After sliding phase, ball is in pure rolling
                    // Parabolic curve has completed, now just linear with friction
                    // WPA 2026: Apply spin decay
                    const spinDecayPerFrame = this.SPIN_DECAY_RATE / 60;
                    const currentSpinSpeed = Math.abs(ball.spinY);
                    const decayFactor = Math.max(0, 1 - (spinDecayPerFrame / Math.max(currentSpinSpeed, 0.01)));
                    ball.spinY *= decayFactor;
                    
                    
                    // Update angular velocity to match linear velocity (pure roll)
                    ball.omega = speed / (ball.r || 14);
                }
            }

            // NATURAL ROLLING TRANSITION (center-contact shots)
            // On a real table, even without applied spin, cloth friction
            // gradually converts a sliding ball to pure rolling.
            // This ensures the 30-degree rule correctly kicks in
            // only after the ball has traveled sufficient distance.
            if (!ball.slidingComplete && (ball.spinY === undefined || Math.abs(ball.spinY) <= 0.01)) {
                if (!ball.naturalRollProgress) ball.naturalRollProgress = 0;
                // Transition takes ~40 frames (~0.67s at 60fps)
                // Faster balls transition sooner (more friction contact)
                const transitionRate = 0.025 * (1 + speed * 0.02);
                ball.naturalRollProgress += transitionRate;
                if (ball.naturalRollProgress >= 1.0) {
                    ball.slidingComplete = true;
                    ball.omega = speed / (ball.r || 14);
                    ball.naturalRollProgress = 1.0;
                }
            }


            // ===== BALL ROTATION (delegated to PoolBallRotation module) =====
            // Uses proper kinematic rolling and Rodrigues' rotation formula.
            // Skipped on LOW quality preset for performance on weak devices.
            const rotationOn = (typeof PoolQuality === 'undefined') || PoolQuality.isRotationEnabled();
            if (rotationOn && typeof PoolBallRotation !== 'undefined') {
                PoolBallRotation.updateRotation(ball);
            }

            // NOTE: Position update (ball.x += ball.vx) is handled by
            // processCollisions() sub-stepping to prevent tunneling.
            // Do NOT move the ball here.

            // Store speed for visual effects
            ball.speed = speed;
            
            return true; // Ball is moving
        } else {
            ball.vx = 0;
            ball.vy = 0;
            ball.speed = 0;
            ball.omega = 0; // Angular velocity
            // Clear spin when stopped
            if (ball.spinX !== undefined) ball.spinX = 0;
            if (ball.spinY !== undefined) ball.spinY = 0;
            ball.slidingComplete = false;
            // Reset parabolic tracking
            ball.slideDistance = 0;
            ball.englishDistance = 0;
            ball.naturalRollProgress = 0;
            return false; // Ball has stopped
        }
    },
    
    /**
     * Handle cushion collisions with pocket gaps
     * The cushion has openings at each pocket — balls pass through into the pocket
     */
    handleCushionBounce(ball, tableWidth, tableHeight, cushionMargin = 20.8, pockets = null) {
        const minX = cushionMargin + ball.r;
        const maxX = tableWidth - cushionMargin - ball.r;
        const minY = cushionMargin + ball.r;
        const maxY = tableHeight - cushionMargin - ball.r;
        const cm = cushionMargin;
        const w = tableWidth;
        const h = tableHeight;

        // Get pocket opening half-widths from the pocket objects
        const cGap = (pockets && pockets[0]) ? (pockets[0].gapHalf || 22) : 22;
        const sGap = (pockets && pockets[4]) ? (pockets[4].gapHalf || 24) : 24;

        // Per-wall gap checks using actual pocket opening sizes
        // TOP wall gap: ball.x near TL corner OR near top-center side pocket OR near TR corner
        const inTopGap =
            ball.x < cm + cGap ||
            ball.x > w - cm - cGap ||
            Math.abs(ball.x - w / 2) < sGap;

        // BOTTOM wall gap: same pattern
        const inBotGap = inTopGap; // symmetric

        // LEFT wall gap: ball.y near TL corner OR near BL corner
        const inLeftGap =
            ball.y < cm + cGap ||
            ball.y > h - cm - cGap;

        // RIGHT wall gap: same pattern
        const inRightGap = inLeftGap; // symmetric

        let bounced = false;
        let bounceAxis = '';

        // Each wall only skips bounce if ball is in THAT wall's gap
        if (ball.x < minX && !inLeftGap) {
            ball.x = minX;
            ball.vx = -ball.vx;
            bounced = true;
            bounceAxis = 'vertical';
        }
        if (ball.x > maxX && !inRightGap) {
            ball.x = maxX;
            ball.vx = -ball.vx;
            bounced = true;
            bounceAxis = 'vertical';
        }
        if (ball.y < minY && !inTopGap) {
            ball.y = minY;
            ball.vy = -ball.vy;
            bounced = true;
            bounceAxis = 'horizontal';
        }
        if (ball.y > maxY && !inBotGap) {
            ball.y = maxY;
            ball.vy = -ball.vy;
            bounced = true;
            bounceAxis = 'horizontal';
        }

        // Safety: keep ball on canvas
        const edge = ball.r * 0.5;
        if (ball.x < -edge) { ball.x = -edge; ball.vx = 0; }
        if (ball.x > w + edge) { ball.x = w + edge; ball.vx = 0; }
        if (ball.y < -edge) { ball.y = -edge; ball.vy = 0; }
        if (ball.y > h + edge) { ball.y = h + edge; ball.vy = 0; }
        
        // Apply spin effects on cushion bounce - REALISTIC PHYSICS
        if (bounced) {
            const impactSpeed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);

            // PLAY CUSHION BOUNCE SOUND
            console.log(`[Physics] Cushion bounce! Speed: ${impactSpeed.toFixed(2)}`);
            if (typeof PoolAudio !== 'undefined') {
                PoolAudio.play('cushionBounce', impactSpeed / 20);
            } else {
                console.warn('[Physics] PoolAudio not available for cushion bounce');
            }

            // CUSHION COMPRESSION EFFECT (#7) -- gated by quality
            const vfxOn = (typeof PoolQuality === 'undefined') || PoolQuality.isVfxEnabled();
            if (vfxOn && typeof PoolVFX !== 'undefined' && impactSpeed > 1) {
                let side = '';
                if (ball.x <= minX + 2) side = 'left';
                else if (ball.x >= maxX - 2) side = 'right';
                else if (ball.y <= minY + 2) side = 'top';
                else if (ball.y >= maxY - 2) side = 'bottom';
                if (side) {
                    PoolVFX.spawnCushionCompression(ball.x, ball.y, side, impactSpeed / 15);
                }
            }
            
            // ===== RAIL GRAB PHYSICS =====
            // Restitution is applied ONCE here (not in the initial flip above)

            const speedFactor = Math.min(impactSpeed / 20, 1.0); // 0 to 1 scale

            // Base restitution — apply once to the bounced component
            const restitution = this.CUSHION_RESTITUTION;
            if (bounceAxis === 'vertical') {
                ball.vx *= restitution;
            } else if (bounceAxis === 'horizontal') {
                ball.vy *= restitution;
            }

            // SIDE SPIN (English) — changes rebound angle, not speed
            if (ball.spinX !== undefined && Math.abs(ball.spinX) > 0.01) {
                const railGrabEnglish = ball.spinX * (0.6 + speedFactor * 0.3);

                // English adjusts the rebound angle
                const currentAngle = Math.atan2(ball.vy, ball.vx);
                const angleDelta = railGrabEnglish * 0.35; // Max ~20 degrees at full english
                const newAngle = currentAngle + angleDelta;
                const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);

                ball.vx = Math.cos(newAngle) * speed;
                ball.vy = Math.sin(newAngle) * speed;

                // English decay
                ball.spinX *= (0.8 + speedFactor * 0.1);
            }

            // TOP/BACK SPIN — adjusts rebound angle, capped speed effect
            if (ball.spinY !== undefined && Math.abs(ball.spinY) > 0.01) {
                // Top spin: shallower angle off cushion (more forward)
                // Back spin: steeper angle (more perpendicular)
                // Effect is an angle change, speed stays close to restitution value
                const spinAngleEffect = ball.spinY * 0.25; // Modest angle adjustment
                const currentAngle = Math.atan2(ball.vy, ball.vx);
                const newAngle = currentAngle + spinAngleEffect;
                const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);

                // Slight speed adjustment: top spin adds a little, back spin takes a little
                // Capped so it never exceeds ±15% of current speed
                const speedAdj = 1 + Math.max(-0.15, Math.min(0.15, ball.spinY * 0.2));

                ball.vx = Math.cos(newAngle) * speed * speedAdj;
                ball.vy = Math.sin(newAngle) * speed * speedAdj;

                // Spin mostly lost after cushion impact
                ball.spinY *= 0.3;
            }
        }
        
        // Update rotation axis when bouncing
        if (bounced) {
            const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
            if (speed > 0) {
                ball.rotationAxisX = -ball.vy / speed;
                ball.rotationAxisY = ball.vx / speed;
            }
        }
        
        return bounced;
    },
    
    /**
     * Check if ball is in a pocket - UK 8-ball has very tight tolerances
     */
    checkPocket(ball, pockets) {
        for (let pocket of pockets) {
            const dx = ball.x - pocket.x;
            const dy = ball.y - pocket.y;
            const dist = Math.sqrt(dx * dx + dy * dy);
            
            const pocketRadius = pocket.r || 22;
            
            // UK pockets are MUCH tighter than American
            // Ball must be more precisely centered to drop
            // With 2 inch ball and 3.5 inch corner pocket = 1.5 inch clearance (0.75 inch each side)
            // With 2 inch ball and 3.2 inch middle pocket = 1.2 inch clearance (0.6 inch each side) - very tight!
            
            // Ball needs to be 60% into pocket to drop (UK spec)
            // This is tighter than American pools which is typically 50%
            const captureThreshold = ball.r * 0.6;
            
            if (dist < pocketRadius - captureThreshold) {
                return true;
            }
        }
        return false;
    },
    
    /**
     * Handle ball-to-ball collision with rotation and spin transfer
     * ENHANCED: Implements 90-degree rule for equal-mass collisions
     * @param {boolean} canResolveVelocity - If false, only separate (don't change velocity)
     * @returns {{ resolved: boolean }} - resolved=true if velocity was changed
     */
    handleBallCollision(b1, b2, canResolveVelocity = true) {
        const dx = b2.x - b1.x;
        const dy = b2.y - b1.y;
        const distSq = dx * dx + dy * dy;
        const minDist = b1.r + b2.r;

        // Check if balls are colliding
        if (distSq < minDist * minDist) {
            const dist = Math.sqrt(distSq);

            // Prevent division by zero
            if (dist < 0.001) {
                b2.x += 0.1;
                b2.y += 0.1;
                return { resolved: false };
            }

            // Normalize collision vector (line of centers)
            const nx = dx / dist;
            const ny = dy / dist;

            // ===== ALWAYS SEPARATE OVERLAPPING BALLS =====
            // Do this first, before velocity resolution, to prevent interpenetration.
            // Always fully separate (no cap) -- if we leave balls overlapping the next
            // sub-step will re-detect the same collision, which causes jittery paths
            // and 'sliding along each other' artifacts.
            const overlap = minDist - dist;
            if (overlap > 0) {
                const separationX = nx * overlap * 0.5;
                const separationY = ny * overlap * 0.5;

                b1.x -= separationX;
                b1.y -= separationY;
                b2.x += separationX;
                b2.y += separationY;
            }

            // If velocity resolution is blocked (ball already resolved this sub-step),
            // just do position correction above and return
            if (!canResolveVelocity) {
                return { resolved: false };
            }

            // Tangent vector (perpendicular to collision)
            const tx = -ny;
            const ty = nx;

            // Relative velocity
            const dvx = b2.vx - b1.vx;
            const dvy = b2.vy - b1.vy;

            // Relative velocity in collision normal direction
            const dvn = dvx * nx + dvy * ny;

            // Only resolve if balls are moving toward each other
            if (dvn < 0) {
                // Store pre-collision info
                const b1Speed = Math.sqrt(b1.vx * b1.vx + b1.vy * b1.vy);
                const b1Angle = Math.atan2(b1.vy, b1.vx);
                
                // PLAY BALL COLLISION SOUND
                const collisionVelocity = Math.abs(dvn) / 10;
                console.log(`[Physics] Ball collision detected! Velocity: ${collisionVelocity.toFixed(2)}`);
                if (typeof PoolAudio !== 'undefined') {
                    PoolAudio.play('ballCollision', collisionVelocity);
                } else {
                    console.warn('[Physics] PoolAudio not available for collision');
                }

                // COLLISION FLASH EFFECT (#6) -- gated by quality
                const vfxFlash = (typeof PoolQuality === 'undefined') || PoolQuality.isVfxEnabled();
                if (vfxFlash && typeof PoolVFX !== 'undefined' && collisionVelocity > 0.15) {
                    const flashX = (b1.x + b2.x) / 2;
                    const flashY = (b1.y + b2.y) / 2;
                    PoolVFX.spawnCollisionFlash(flashX, flashY, collisionVelocity);
                }
                
                // Check if b2 is stationary (90-degree rule applies)
                const b2Speed = Math.sqrt(b2.vx * b2.vx + b2.vy * b2.vy);
                const b2Stationary = b2Speed < 0.1; // Nearly stationary
                
                // Calculate CUT ANGLE (angle between approach and collision)
                const collisionAngle = Math.atan2(ny, nx);
                const cutAngle = Math.abs(collisionAngle - b1Angle);
                const normalizedCutAngle = Math.min(cutAngle, Math.PI * 2 - cutAngle);
                
                // Decompose b1 velocity into normal and tangent components
                const b1vn = b1.vx * nx + b1.vy * ny; // Normal component (into collision)
                const b1vt = b1.vx * tx + b1.vy * ty; // Tangent component (along surface)
                
                // Calculate contact thickness (how much of ball overlaps)
                // Guard against division by zero when b1 is stationary
                const contactThickness = b1Speed > 0.001 ? Math.abs(b1vt) / b1Speed : 0; // 0 = head-on, 1 = glancing
                
                // Store original velocities before modification
                const b1vx_orig = b1.vx;
                const b1vy_orig = b1.vy;
                const b2vx_orig = b2.vx;
                const b2vy_orig = b2.vy;
                
                // ===== 90-DEGREE RULE IMPLEMENTATION =====
                // For stationary equal-mass collisions
                if (b2Stationary && Math.abs(b1.r - b2.r) < 0.1) {
                    // Check if ball is rolling (not sliding/stunned)
                    // Only true when the sliding-to-rolling transition has genuinely completed.
                    // A freshly-shot ball (even with center contact) starts SLIDING
                    // and only becomes rolling after traveling sufficient distance.
                    const isRolling = b1.slidingComplete === true;
                    
                    // Transfer normal component to b2, keep tangent with b1
                    const normalSpeed = Math.abs(b1vn) * this.COLLISION_DAMPING;
                    const tangentSpeed = b1vt * this.COLLISION_DAMPING;
                    
                    // Object ball gets the normal component (along line of centers)
                    b2.vx = nx * normalSpeed;
                    b2.vy = ny * normalSpeed;
                    
                    // ===== 30-DEGREE RULE IMPLEMENTATION =====
                    // For a ROLLING ball (natural roll), deflection is approximately 30 degrees from original path
                    // For a STUN shot (no spin), it's 90 degrees (pure tangent)
                    if (isRolling && normalizedCutAngle > 0.02) { // Not a perfectly straight hit
                        // Calculate which side of the original path the cue ball deflects to.
                        const tangentAngle = Math.atan2(ty, tx);
                        const originalAngle = b1Angle;
                        let angleDiff = tangentAngle - originalAngle;
                        while (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
                        while (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;
                        const sign = Math.sign(angleDiff) || 1;

                        // Smooth deflection magnitude that varies with cut angle.
                        //   - thick (cut < 30 deg): deflection grows ~ linearly from 0 -> 30 deg
                        //   - half-ball (30-60 deg): plateau around 30 deg (the classic rule)
                        //   - thin (cut > 60 deg): bend back toward stun (~90 deg) because
                        //     so little energy transfers that the cue barely deflects past tangent
                        const cutDeg = normalizedCutAngle * 180 / Math.PI;
                        let deflectionDeg;
                        if (cutDeg < 30) {
                            deflectionDeg = cutDeg;                       // 0 .. 30
                        } else if (cutDeg < 60) {
                            deflectionDeg = 30;                            // plateau
                        } else {
                            deflectionDeg = 30 + (cutDeg - 60) * 1.5;     // climb toward 75-90
                        }
                        deflectionDeg = Math.min(85, deflectionDeg);
                        const deflectionAngle = originalAngle + sign * deflectionDeg * Math.PI / 180;

                        // Cue ball keeps the stun speed (|tangent component| with damping).
                        // This is energy-correct: object ball got the normal component,
                        // cue retains the tangent component but rolling friction redirects it.
                        const cueSpeed = Math.abs(tangentSpeed);
                        b1.vx = Math.cos(deflectionAngle) * cueSpeed;
                        b1.vy = Math.sin(deflectionAngle) * cueSpeed;

                        console.log('30-DEG RULE! Cut:', cutDeg.toFixed(1), 'deg, Deflection:', deflectionDeg.toFixed(1), 'deg');
                    } else {
                        // Stun shot or straight-on: Pure tangent (90-degree rule only)
                        b1.vx = tx * tangentSpeed;
                        b1.vy = ty * tangentSpeed;

                        if (isRolling) {
                            console.log('90-DEG RULE! Straight-on hit, cue tangent:', (Math.atan2(b1.vy, b1.vx) * 180 / Math.PI).toFixed(1), 'deg');
                        } else {
                            console.log('STUN SHOT! 90-degree separation, no natural roll');
                        }
                    }
                } else {
                    // ===== STANDARD ELASTIC COLLISION FOR MOVING BALLS =====
                    // Use proper physics formula for elastic collision
                    
                    // Decompose velocities into normal and tangent components
                    const b1vn_orig = b1vx_orig * nx + b1vy_orig * ny;
                    const b1vt_orig = b1vx_orig * tx + b1vy_orig * ty;
                    const b2vn_orig = b2vx_orig * nx + b2vy_orig * ny;
                    const b2vt_orig = b2vx_orig * tx + b2vy_orig * ty;
                    
                    // For equal mass elastic collision, velocities swap in normal direction
                    // Apply damping for energy loss
                    const b1vn_new = b2vn_orig * this.COLLISION_DAMPING;
                    const b2vn_new = b1vn_orig * this.COLLISION_DAMPING;
                    
                    // Tangent components remain unchanged (no friction perpendicular to collision)
                    const b1vt_new = b1vt_orig;
                    const b2vt_new = b2vt_orig;
                    
                    // Recompose velocities
                    b1.vx = b1vn_new * nx + b1vt_new * tx;
                    b1.vy = b1vn_new * ny + b1vt_new * ty;
                    b2.vx = b2vn_new * nx + b2vt_new * tx;
                    b2.vy = b2vn_new * ny + b2vt_new * ty;
                }
                
                // REALISTIC SPIN TRANSFER WITH CUT ANGLE CONSIDERATION
                
                // Only apply draw/follow on THICK HITS (near center)
                // Thin cuts don't transfer much spin
                const spinEffectiveness = Math.max(0, 1 - contactThickness * 2);
                
                // If b1 (cue ball) has back spin AND it's a thick enough hit
                if (b1.spinY !== undefined && b1.spinY < -0.3 && spinEffectiveness > 0.3) {
                    // Back spin causes the ball to grip and draw back
                    const drawStrength = Math.abs(b1.spinY) * 0.6 * spinEffectiveness;
                    
                    // For THICK hits (near head-on), draw straight back from where it came
                    if (normalizedCutAngle < Math.PI / 6) { // Less than 30 degrees = thick hit
                        // Draw back in the opposite of approach direction
                        const drawAngle = b1Angle + Math.PI;
                        const drawSpeed = b1Speed * drawStrength * 0.8;
                        
                        b1.vx = Math.cos(drawAngle) * drawSpeed;
                        b1.vy = Math.sin(drawAngle) * drawSpeed;
                        
                        console.log('[Physics] THICK DRAW! Straight back, cut angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg');
                    } else {
                        // For medium cuts, draw ADDS a backward/tangential pull on top of the
                        // collision result. (Overwriting would wipe out the 30-degree-rule deflection.)
                        const tangentMagnitude = Math.abs(b1vt) * 0.3;
                        let drawDirection;

                        if (b1vt > 0) {
                            drawDirection = Math.atan2(ty, tx);
                        } else {
                            drawDirection = Math.atan2(-ty, -tx);
                        }

                        const drawSpeed = b1Speed * drawStrength * 0.5;
                        b1.vx += Math.cos(drawDirection) * drawSpeed * 0.5;
                        b1.vy += Math.sin(drawDirection) * drawSpeed * 0.5;

                        console.log('[Physics] CUT DRAW! Angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg, effectiveness:', (spinEffectiveness * 100).toFixed(0) + '%');
                    }
                } else if (b1.spinY !== undefined && b1.spinY < -0.3) {
                    console.log('[Physics] Draw too thin! Cut angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg, no draw effect');
                }
                
                // If b1 has top spin, it follows through (works on all cuts)
                if (b1.spinY !== undefined && b1.spinY > 0.3) {
                    const followStrength = b1.spinY * 0.4 * Math.max(0.3, spinEffectiveness);

                    // Continue in roughly the original direction
                    const currentSpeed = Math.sqrt(b1.vx * b1.vx + b1.vy * b1.vy);
                    const currentAngle = Math.atan2(b1.vy, b1.vx);

                    // Proper angular interpolation (handles ±PI wrapping)
                    let angleDelta = b1Angle - currentAngle;
                    while (angleDelta > Math.PI) angleDelta -= 2 * Math.PI;
                    while (angleDelta < -Math.PI) angleDelta += 2 * Math.PI;
                    const targetAngle = currentAngle + angleDelta * followStrength;

                    // Follow changes DIRECTION toward the original approach,
                    // but does NOT increase speed (energy was transferred to object ball)
                    b1.vx = Math.cos(targetAngle) * currentSpeed;
                    b1.vy = Math.sin(targetAngle) * currentSpeed;

                    console.log('FOLLOW! Cut angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg');
                }
                
                // Side spin creates throw on the object ball - THIS IS KEY!
                if (b1.spinX !== undefined && Math.abs(b1.spinX) > 0.3) {
                    // ===== ENHANCED THROW MODEL (FIT + SIT) - WPA 2026 =====
                    // FIT (Friction-Induced Throw): Maximum at 1/2-ball hit (30-degree cut)
                    // SIT (Spin-Induced Throw): English deflects object ball
                    // WPA 2026: Ball-to-ball friction coefficient 0.03-0.08
                    
                    // Calculate FIT based on cut angle
                    // Maximum throw occurs at 30-degree cut angle (half-ball hit)
                    const cutAngleDegrees = normalizedCutAngle * 180 / Math.PI;
                    const optimalCutAngle = 30; // degrees for max throw
                    
                    // Throw curve: peaks at 30 degrees, reduces at thin and thick hits
                    const cutAngleFactor = 1 - Math.abs(cutAngleDegrees - optimalCutAngle) / optimalCutAngle;
                    const fitFactor = Math.max(0, cutAngleFactor) * this.BALL_TO_BALL_FRICTION * 1.5; // Realistic: ~3-5 deg max throw
                    
                    // SIT (Spin-Induced Throw): English effect
                    const sitFactor = b1.spinX * 0.15 * spinEffectiveness; // SIT contribution
                    
                    // Combined throw effect
                    const totalThrow = fitFactor + sitFactor;
                    
                    // Apply throw to object ball
                    b2.vx += tx * totalThrow * b1Speed;
                    b2.vy += ty * totalThrow * b1Speed;
                    
                    if (Math.abs(totalThrow) > 0.05) {
                        console.log('THROW (WPA 2026)! FIT:', (fitFactor * 100).toFixed(1) + '%, SIT:', (sitFactor * 100).toFixed(1) + '%, Total:', (totalThrow * 100).toFixed(1) + '%, Cut:', cutAngleDegrees.toFixed(1) + ' deg');
                    }
                }
                
                // Update rotation axes after collision
                const speed1 = Math.sqrt(b1.vx * b1.vx + b1.vy * b1.vy);
                const speed2 = Math.sqrt(b2.vx * b2.vx + b2.vy * b2.vy);
                
                if (speed1 > 0) {
                    b1.rotationAxisX = -b1.vy / speed1;
                    b1.rotationAxisY = b1.vx / speed1;

                    // Reset sliding phase for draw/follow to work again
                    b1.slidingComplete = false;
                    b1.naturalRollProgress = 0;
                }
                
                if (speed2 > 0) {
                    b2.rotationAxisX = -b2.vy / speed2;
                    b2.rotationAxisY = b2.vx / speed2;
                }
                
                // Spin is mostly lost after collision
                if (b1.spinY !== undefined) b1.spinY *= 0.1; // Top/back spin mostly gone
                if (b1.spinX !== undefined) b1.spinX *= 0.5; // English more preserved

                return { resolved: true };
            }

            // Balls overlapping but moving apart — separation was already done above
            return { resolved: false };
        }

        return { resolved: false };
    },
    
    
    /**
     * Process all ball collisions with integrated sub-stepped movement.
     * Moves balls in small increments and checks for collisions at each step
     * to prevent tunneling at high speeds.
     * Limits each ball to one velocity-resolving collision per sub-step
     * to prevent cascading energy amplification in packed clusters.
     * @param {Array} balls - Array of ball objects
     * @param {Object} game - Game instance for tracking first ball hit
     */
    processCollisions(balls, game = null) {
        let collisionOccurred = false;
        let firstBallHit = null;

        // Find the maximum speed of any ball
        let maxSpeed = 0;
        for (const ball of balls) {
            if (ball.potted) continue;
            const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
            if (speed > maxSpeed) maxSpeed = speed;
        }

        // Calculate number of sub-steps needed based on speed
        // Each sub-step should move a ball no more than 40% of its radius
        const minBallRadius = balls.reduce((min, b) => b.potted ? min : Math.min(min, b.r), Infinity);
        let subSteps = Math.max(1, Math.ceil(maxSpeed / (minBallRadius * 0.4)));
        // Quality cap -- prevents very fast shots from spending huge CPU on a low-end device
        const subStepCap = (typeof PoolQuality !== 'undefined' && PoolQuality.config && PoolQuality.config.maxSubSteps) || 20;
        if (subSteps > subStepCap) subSteps = subStepCap;
        const dt = 1.0 / subSteps;

        // Sub-stepped integration: move + detect at each step
        for (let step = 0; step < subSteps; step++) {
            // Move all balls by fractional step
            for (const ball of balls) {
                if (ball.potted) continue;
                ball.x += ball.vx * dt;
                ball.y += ball.vy * dt;
            }

            // Track which balls have had velocity resolved this sub-step.
            // A ball that was already resolved only gets position separation,
            // not another velocity change. This prevents cascading in clusters.
            const resolvedThisStep = new Set();

            // Check all ball-ball collisions at this sub-position
            for (let i = 0; i < balls.length; i++) {
                if (balls[i].potted) continue;

                for (let j = i + 1; j < balls.length; j++) {
                    if (balls[j].potted) continue;

                    // Allow velocity resolution only if neither ball was already resolved
                    const canResolve = !resolvedThisStep.has(i) && !resolvedThisStep.has(j);

                    const result = this.handleBallCollision(balls[i], balls[j], canResolve);

                    if (result.resolved) {
                        resolvedThisStep.add(i);
                        resolvedThisStep.add(j);
                        collisionOccurred = true;

                        // Track first ball hit by cue ball for rule enforcement
                        if (game && !firstBallHit) {
                            if (balls[i].num === 0) {
                                firstBallHit = balls[j];
                            } else if (balls[j].num === 0) {
                                firstBallHit = balls[i];
                            }
                        }
                    }
                }
            }
        }

        return { occurred: collisionOccurred, firstBallHit: firstBallHit };
    },
    
    /**
     * Handle pocket jaw collisions - angled jaw faces at pocket openings
     * Balls that hit the jaw edges bounce off at realistic angles
     * @param {Object} ball - The ball to check
     * @param {Array} pockets - Array of pocket objects
     * @param {Object} game - Game instance for settings
     * @returns {boolean} - Whether a jaw collision occurred
     */
    handlePocketJawCollision(ball, pockets, game) {
        if (!pockets || !game) return false;

        const cm = game.cushionMargin || 21;
        const w = game.width || 1000;
        const h = game.height || 500;
        const restitution = (game.cushionRestitution || 0.78) * 0.8;

        // Pocket opening half-widths
        const cornerHalf = (game.cornerPocketOpening || 45) / 2;
        const sideHalf = (game.sidePocketOpening || 49) / 2;

        // Build jaw lines from cushion endpoints toward pocket centers
        const jawDepth = cm * 0.6;
        const allJaws = [];

        // Corner pocket jaws — from the end of each cushion segment toward the pocket
        // Top-left
        allJaws.push(
            {x1: cm + cornerHalf, y1: cm, x2: cm + cornerHalf * 0.3, y2: cm - jawDepth * 0.5},
            {x1: cm, y1: cm + cornerHalf, x2: cm - jawDepth * 0.5, y2: cm + cornerHalf * 0.3}
        );
        // Top-right
        allJaws.push(
            {x1: w - cm - cornerHalf, y1: cm, x2: w - cm - cornerHalf * 0.3, y2: cm - jawDepth * 0.5},
            {x1: w - cm, y1: cm + cornerHalf, x2: w - cm + jawDepth * 0.5, y2: cm + cornerHalf * 0.3}
        );
        // Bottom-left
        allJaws.push(
            {x1: cm + cornerHalf, y1: h - cm, x2: cm + cornerHalf * 0.3, y2: h - cm + jawDepth * 0.5},
            {x1: cm, y1: h - cm - cornerHalf, x2: cm - jawDepth * 0.5, y2: h - cm - cornerHalf * 0.3}
        );
        // Bottom-right
        allJaws.push(
            {x1: w - cm - cornerHalf, y1: h - cm, x2: w - cm - cornerHalf * 0.3, y2: h - cm + jawDepth * 0.5},
            {x1: w - cm, y1: h - cm - cornerHalf, x2: w - cm + jawDepth * 0.5, y2: h - cm - cornerHalf * 0.3}
        );

        // Side pocket jaws
        // Top-center
        allJaws.push(
            {x1: w / 2 - sideHalf, y1: cm, x2: w / 2 - sideHalf * 0.4, y2: cm - jawDepth * 0.6},
            {x1: w / 2 + sideHalf, y1: cm, x2: w / 2 + sideHalf * 0.4, y2: cm - jawDepth * 0.6}
        );
        // Bottom-center
        allJaws.push(
            {x1: w / 2 - sideHalf, y1: h - cm, x2: w / 2 - sideHalf * 0.4, y2: h - cm + jawDepth * 0.6},
            {x1: w / 2 + sideHalf, y1: h - cm, x2: w / 2 + sideHalf * 0.4, y2: h - cm + jawDepth * 0.6}
        );

        for (let i = 0; i < allJaws.length; i++) {
            const jaw = allJaws[i];
            const angle = Math.atan2(jaw.y2 - jaw.y1, jaw.x2 - jaw.x1);
            if (this.checkLineCollision(ball, jaw.x1, jaw.y1, jaw.x2, jaw.y2, angle, restitution)) {
                // Play cushion sound for jaw hit
                const impactSpeed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
                if (typeof PoolAudio !== 'undefined') {
                    PoolAudio.play('cushionBounce', impactSpeed / 25);
                }
                if (typeof PoolVFX !== 'undefined' && impactSpeed > 1) {
                    PoolVFX.spawnCushionCompression(
                        (jaw.x1 + jaw.x2) / 2, (jaw.y1 + jaw.y2) / 2,
                        jaw.y1 === jaw.y2 ? (jaw.y1 < h / 2 ? 'top' : 'bottom') : (jaw.x1 < w / 2 ? 'left' : 'right'),
                        impactSpeed / 20
                    );
                }
                return true;
            }
        }

        return false;
    },
    
    /**
     * Check if ball collides with a line segment and handle the bounce
     */
    checkLineCollision(ball, x1, y1, x2, y2, normalAngle, restitution) {
        // Vector from line start to end
        const lineVx = x2 - x1;
        const lineVy = y2 - y1;
        const lineLen = Math.sqrt(lineVx * lineVx + lineVy * lineVy);
        
        if (lineLen < 0.001) return false;
        
        // Unit vector along line
        const lineUnitX = lineVx / lineLen;
        const lineUnitY = lineVy / lineLen;
        
        // Vector from line start to ball
        const toBallX = ball.x - x1;
        const toBallY = ball.y - y1;
        
        // Project ball position onto line
        const projection = toBallX * lineUnitX + toBallY * lineUnitY;
        
        // Clamp projection to line segment
        const clampedProj = Math.max(0, Math.min(lineLen, projection));
        
        // Closest point on line to ball
        const closestX = x1 + lineUnitX * clampedProj;
        const closestY = y1 + lineUnitY * clampedProj;
        
        // Distance from ball to closest point
        const distX = ball.x - closestX;
        const distY = ball.y - closestY;
        const dist = Math.sqrt(distX * distX + distY * distY);
        
        // Check if collision (ball touching line)
        if (dist < ball.r && dist > 0) {
            // Calculate collision normal (perpendicular to line, pointing away from ball)
            const nx = distX / dist;
            const ny = distY / dist;
            
            // Check if ball is moving toward the line
            const velToward = ball.vx * (-nx) + ball.vy * (-ny);
            
            if (velToward > 0) {
                // Reflect velocity off the jaw
                const dot = ball.vx * nx + ball.vy * ny;
                ball.vx = (ball.vx - 2 * dot * nx) * restitution;
                ball.vy = (ball.vy - 2 * dot * ny) * restitution;
                
                // Push ball out of collision
                const overlap = ball.r - dist;
                ball.x += nx * overlap * 1.1;
                ball.y += ny * overlap * 1.1;
                
                return true;
            }
        }
        
        return false;
    }
};
";
    }
}


