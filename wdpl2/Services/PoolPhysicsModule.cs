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
    // Constants
    FRICTION: 0.987,  // Slightly less friction for smoother roll
    CUSHION_RESTITUTION: 0.78,  // More realistic bounce
    MIN_VELOCITY: 0.012,
    COLLISION_DAMPING: 0.98,  // Energy loss in collisions
    
    /**
     * Apply friction with rolling rotation and spin effects
     */
    applyFriction(ball) {
        const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
        
        if (speed > this.MIN_VELOCITY) {
            // Store initial direction
            const initialAngle = Math.atan2(ball.vy, ball.vx);
            
            // Apply base friction
            ball.vx *= this.FRICTION;
            ball.vy *= this.FRICTION;
            
            // REALISTIC SPIN PHYSICS:
            // In real pool, spin affects the ball through the Magnus effect and friction
            
            // SIDE SPIN (English) - Magnus effect creates lateral force
            if (ball.spinX !== undefined && Math.abs(ball.spinX) > 0.01) {
                // In real pool, English has NEARLY ZERO effect during roll
                // It's stored on the ball to affect cushions and throw only
                // However, there IS a very subtle parabolic deflection
                const magnusForce = ball.spinX * 0.002; // MINIMAL
                const perpAngle = initialAngle + Math.PI / 2;
                
                // PARABOLIC DEFLECTION: English creates very slight curve over distance
                // This is much less than top/back spin but still follows physics
                if (!ball.englishDistance) ball.englishDistance = 0;
                ball.englishDistance += speed;
                
                // Parabolic factor increases with distance traveled
                const distanceFactor = Math.min(ball.englishDistance / 500, 1.0); // Caps at 500 units
                const parabolicMagnusForce = magnusForce * (1 + distanceFactor * 0.5);
                
                ball.vx += Math.cos(perpAngle) * parabolicMagnusForce;
                ball.vy += Math.sin(perpAngle) * parabolicMagnusForce;
                
                // Spin decays extremely slowly - it stays on the ball for contact
                ball.spinX *= 0.998; // Even slower decay
            }
            
            // TOP/BACK SPIN - This is where real pool physics happens
            if (ball.spinY !== undefined && Math.abs(ball.spinY) > 0.01) {
                // In real pool, the ball initially SLIDES on the cloth with spin
                // Then the spin gradually converts to rolling motion
                // This creates a PARABOLIC PATH as described in physics literature
                
                // Check if ball is still in sliding phase
                if (!ball.slidingComplete) {
                    // Get initial sliding intensity from sweet spot calculation
                    const slidingMultiplier = ball.initialSlidingIntensity || 1.0;
                    
                    // During sliding, spin creates friction in the direction of spin
                    // This creates the initial part of the parabolic curve
                    const spinFriction = ball.spinY * 0.15 * slidingMultiplier;
                    
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
                    
                    // Check if sliding is complete (spin matches roll)
                    // The parabolic curve completes when sliding ends
                    const expectedRoll = speed * 10; // Arbitrary units
                    if (Math.abs(ball.spinY) < expectedRoll * 0.1) {
                        ball.slidingComplete = true;
                        console.log('Parabolic curve complete! Distance:', ball.slideDistance.toFixed(1), 'Final speed:', speed.toFixed(2));
                    }
                } else {
                    // After sliding phase, ball is in pure rolling
                    // Parabolic curve has completed, now just linear with friction
                    ball.spinY *= 0.98;
                }
            }
            
            // Calculate rotation based on velocity
            const dx = ball.vx;
            const dy = ball.vy;
            
            // Calculate rotation axis (perpendicular to direction of movement)
            const rotationSpeed = speed / ball.r;
            
            // Update rotation angle (accumulate over time)
            if (!ball.rotation) ball.rotation = 0;
            ball.rotation += rotationSpeed;
            
            // Store rotation axis (perpendicular to velocity)
            ball.rotationAxisX = -dy / speed;
            ball.rotationAxisY = dx / speed;
            
            // Update position
            ball.x += ball.vx;
            ball.y += ball.vy;
            
            // Store speed for visual effects
            ball.speed = speed;
            
            return true; // Ball is moving
        } else {
            ball.vx = 0;
            ball.vy = 0;
            ball.speed = 0;
            // Clear spin when stopped
            if (ball.spinX !== undefined) ball.spinX = 0;
            if (ball.spinY !== undefined) ball.spinY = 0;
            ball.slidingComplete = false;
            // Reset parabolic tracking
            ball.slideDistance = 0;
            ball.englishDistance = 0;
            return false; // Ball has stopped
        }
    },
    
    /**
     * Handle cushion collisions with rotation and spin effects
     */
    handleCushionBounce(ball, tableWidth, tableHeight, cushionMargin = 20.8) {
        const minX = cushionMargin + ball.r;
        const maxX = tableWidth - cushionMargin - ball.r;
        const minY = cushionMargin + ball.r;
        const maxY = tableHeight - cushionMargin - ball.r;
        
        let bounced = false;
        let bounceAxis = ''; // Track which axis bounced
        
        if (ball.x < minX) {
            ball.x = minX;
            ball.vx = -ball.vx * this.CUSHION_RESTITUTION;
            bounced = true;
            bounceAxis = 'vertical';
        }
        if (ball.x > maxX) {
            ball.x = maxX;
            ball.vx = -ball.vx * this.CUSHION_RESTITUTION;
            bounced = true;
            bounceAxis = 'vertical';
        }
        if (ball.y < minY) {
            ball.y = minY;
            ball.vy = -ball.vy * this.CUSHION_RESTITUTION;
            bounced = true;
            bounceAxis = 'horizontal';
        }
        if (ball.y > maxY) {
            ball.y = maxY;
            ball.vy = -ball.vy * this.CUSHION_RESTITUTION;
            bounced = true;
            bounceAxis = 'horizontal';
        }
        
        // Apply spin effects on cushion bounce - REALISTIC PHYSICS
        if (bounced) {
            // SIDE SPIN (English) - This is WHERE English REALLY matters in real pool
            if (ball.spinX !== undefined && Math.abs(ball.spinX) > 0.01) {
                // English has MAJOR effect on cushion contact - this is the key!
                const englishEffect = ball.spinX * 0.8; // INCREASED from 0.4 to 0.8
                
                if (bounceAxis === 'vertical') {
                    // Hitting vertical cushion - English changes the angle significantly
                    const currentAngle = Math.atan2(ball.vy, ball.vx);
                    const newAngle = currentAngle + (englishEffect * 0.5); // INCREASED from 0.3 to 0.5
                    const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
                    
                    ball.vx = Math.cos(newAngle) * speed * this.CUSHION_RESTITUTION;
                    ball.vy = Math.sin(newAngle) * speed * this.CUSHION_RESTITUTION;
                    
                    console.log('?? English on cushion! Angle change:', (englishEffect * 0.5 * 180 / Math.PI).toFixed(1) + '°');
                } else if (bounceAxis === 'horizontal') {
                    // Hitting horizontal cushion - English changes the angle significantly
                    const currentAngle = Math.atan2(ball.vy, ball.vx);
                    const newAngle = currentAngle + (englishEffect * 0.5); // INCREASED from 0.3 to 0.5
                    const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
                    
                    ball.vx = Math.cos(newAngle) * speed * this.CUSHION_RESTITUTION;
                    ball.vy = Math.sin(newAngle) * speed * this.CUSHION_RESTITUTION;
                    
                    console.log('?? English on cushion! Angle change:', (englishEffect * 0.5 * 180 / Math.PI).toFixed(1) + '°');
                }
                
                // English significantly preserved after cushion (key for multi-rail shots)
                ball.spinX *= 0.9; // INCREASED from 0.85 to 0.9
            }
            
            // TOP/BACK SPIN - Affects rebound speed dramatically
            if (ball.spinY !== undefined && Math.abs(ball.spinY) > 0.01) {
                const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
                
                // Top spin: ball grips and accelerates off cushion
                // Back spin: ball stops dead or even reverses
                const spinEffect = ball.spinY * 1.5; // Very strong effect
                
                if (bounceAxis === 'vertical') {
                    ball.vx = ball.vx * (1 + spinEffect);
                } else if (bounceAxis === 'horizontal') {
                    ball.vy = ball.vy * (1 + spinEffect);
                }
                
                // With max back spin, ball can reverse
                if (ball.spinY < -0.8) {
                    if (bounceAxis === 'vertical') {
                        ball.vx *= -0.3; // Reverse!
                    } else {
                        ball.vy *= -0.3; // Reverse!
                    }
                }
                
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
     */
    handleBallCollision(b1, b2) {
        const dx = b2.x - b1.x;
        const dy = b2.y - b1.y;
        const distSq = dx * dx + dy * dy;
        const minDist = b1.r + b2.r;
        
        // Check if balls are colliding
        if (distSq < minDist * minDist) {
            const dist = Math.sqrt(distSq);
            
            // Normalize collision vector (line of centers)
            const nx = dx / dist;
            const ny = dy / dist;
            
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
                const contactThickness = Math.abs(b1vt) / b1Speed; // 0 = head-on, 1 = glancing
                
                // ===== 90-DEGREE RULE IMPLEMENTATION =====
                // For stationary equal-mass collisions
                if (b2Stationary && Math.abs(b1.r - b2.r) < 0.1) {
                    // Check if ball is rolling (not sliding/stunned)
                    const isRolling = !b1.slidingComplete || (b1.spinY === undefined || Math.abs(b1.spinY) < 0.5);
                    
                    // Transfer normal component to b2, keep tangent with b1
                    const normalSpeed = Math.abs(b1vn) * this.COLLISION_DAMPING;
                    const tangentSpeed = b1vt * this.COLLISION_DAMPING;
                    
                    // Object ball gets the normal component (along line of centers)
                    b2.vx = nx * normalSpeed;
                    b2.vy = ny * normalSpeed;
                    
                    // ===== 30-DEGREE RULE IMPLEMENTATION =====
                    // For a ROLLING ball (natural roll), deflection is approximately 30 degrees from original path
                    // For a STUN shot (no spin), it's 90 degrees (pure tangent)
                    if (isRolling && normalizedCutAngle > 0.1) { // Not a straight-on hit
                        // Calculate 30-degree deflection from original path
                        // The cue ball deflects at 30 deg rather than purely tangent (90 deg)
                        
                        // Tangent direction would be 90 deg, but rolling friction causes approximately 30 deg deflection
                        const tangentAngle = Math.atan2(ty, tx);
                        const originalAngle = b1Angle;
                        
                        // Calculate the natural angle - 30 degrees from original path toward tangent
                        // Determine which direction to deflect (left or right of original)
                        let angleDiff = tangentAngle - originalAngle;
                        // Normalize to -PI to PI
                        while (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
                        while (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;
                        
                        // 30-degree deflection in the direction of the tangent
                        const deflectionAngle = originalAngle + (Math.sign(angleDiff) * Math.PI / 6); // 30 degrees
                        
                        // Apply the 30-degree rule velocity
                        const cueSpeed = Math.sqrt(tangentSpeed * tangentSpeed);
                        b1.vx = Math.cos(deflectionAngle) * cueSpeed;
                        b1.vy = Math.sin(deflectionAngle) * cueSpeed;
                        
                        console.log('30-DEG RULE! Cut:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg, Deflection:', (deflectionAngle * 180 / Math.PI).toFixed(1), 'deg');
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
                    // Standard elastic collision for moving balls
                    const impulse = dvn * this.COLLISION_DAMPING;
                    
                    b1.vx += impulse * nx;
                    b1.vy += impulse * ny;
                    b2.vx -= impulse * nx;
                    b2.vy -= impulse * ny;
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
                        
                        console.log('?? THICK DRAW! Straight back, cut angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg');
                    } else {
                        // For medium cuts, draw back at an angle using tangent
                        // But don't overdo it - reduce the effect
                        const tangentMagnitude = Math.abs(b1vt) * 0.3;
                        let drawDirection;
                        
                        if (b1vt > 0) {
                            drawDirection = Math.atan2(ty, tx);
                        } else {
                            drawDirection = Math.atan2(-ty, -tx);
                        }
                        
                        const drawSpeed = b1Speed * drawStrength * 0.5;
                        b1.vx = Math.cos(drawDirection) * drawSpeed;
                        b1.vy = Math.sin(drawDirection) * drawSpeed;
                        
                        console.log('?? CUT DRAW! Angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg, effectiveness:', (spinEffectiveness * 100).toFixed(0) + '%');
                    }
                } else if (b1.spinY !== undefined && b1.spinY < -0.3) {
                    console.log('?? Draw too thin! Cut angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg, no draw effect');
                }
                
                // If b1 has top spin, it follows through (works on all cuts)
                if (b1.spinY !== undefined && b1.spinY > 0.3) {
                    const followStrength = b1.spinY * 0.4 * Math.max(0.3, spinEffectiveness);
                    
                    // Continue in roughly the original direction
                    const currentSpeed = Math.sqrt(b1.vx * b1.vx + b1.vy * b1.vy);
                    const currentAngle = Math.atan2(b1.vy, b1.vx);
                    
                    // Blend between current direction and original direction
                    const targetAngle = b1Angle * 0.6 + currentAngle * 0.4;
                    
                    b1.vx = Math.cos(targetAngle) * currentSpeed * (1 + followStrength);
                    b1.vy = Math.sin(targetAngle) * currentSpeed * (1 + followStrength);
                    
                    console.log('?? FOLLOW! Cut angle:', (normalizedCutAngle * 180 / Math.PI).toFixed(1), 'deg');
                }
                
                // Side spin creates throw on the object ball - THIS IS KEY!
                if (b1.spinX !== undefined && Math.abs(b1.spinX) > 0.3) {
                    // Throw is a MAJOR effect of English in real pool
                    const throwEffect = b1.spinX * 0.15 * spinEffectiveness;
                    
                    // Apply throw to object ball
                    b2.vx += tx * throwEffect * b1Speed;
                    b2.vy += ty * throwEffect * b1Speed;
                    
                    if (Math.abs(throwEffect) > 0.03) {
                        console.log('?? THROW! Effect:', (throwEffect * 100).toFixed(1) + '%, thickness:', (spinEffectiveness * 100).toFixed(0) + '%');
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
                }
                
                if (speed2 > 0) {
                    b2.rotationAxisX = -b2.vy / speed2;
                    b2.rotationAxisY = b2.vx / speed2;
                }
                
                // Spin is mostly lost after collision
                if (b1.spinY !== undefined) b1.spinY *= 0.1; // Top/back spin mostly gone
                if (b1.spinX !== undefined) b1.spinX *= 0.5; // English more preserved
            }
            
            // Separate overlapping balls more smoothly
            const overlap = minDist - dist;
            if (overlap > 0) {
                // Slightly push balls apart based on overlap
                const separationX = nx * overlap * 0.52;
                const separationY = ny * overlap * 0.52;
                
                b1.x -= separationX;
                b1.y -= separationY;
                b2.x += separationX;
                b2.y += separationY;
            }
            
            return true;
        }
        
        return false;
    },
    
    /**
     * Process all ball collisions
     */
    processCollisions(balls) {
        let collisionOccurred = false;
        
        for (let i = 0; i < balls.length; i++) {
            if (balls[i].potted) continue;
            
            for (let j = i + 1; j < balls.length; j++) {
                if (balls[j].potted) continue;
                
                if (this.handleBallCollision(balls[i], balls[j])) {
                    collisionOccurred = true;
                }
            }
        }
        
        return collisionOccurred;
    }
};
";
    }
}
