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
            // Apply base friction
            ball.vx *= this.FRICTION;
            ball.vy *= this.FRICTION;
            
            // Apply spin effects during roll
            if (ball.spinX !== undefined && ball.spinX !== 0) {
                // Side spin (English) causes curve during roll
                const spinCurve = ball.spinX * 0.08; // Continuous curve effect
                const perpAngle = Math.atan2(ball.vy, ball.vx) + Math.PI / 2;
                
                ball.vx += Math.cos(perpAngle) * spinCurve;
                ball.vy += Math.sin(perpAngle) * spinCurve;
            }
            
            if (ball.spinY !== undefined && ball.spinY !== 0) {
                // Top spin accelerates slightly, back spin decelerates
                const spinFriction = 1 + (ball.spinY * 0.002);
                ball.vx *= spinFriction;
                ball.vy *= spinFriction;
                
                // Gradually reduce spin
                ball.spinY *= 0.995;
            }
            
            // Gradually reduce side spin
            if (ball.spinX !== undefined) {
                ball.spinX *= 0.995;
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
        
        // Apply spin effects on cushion bounce
        if (bounced && ball.spinX !== undefined && ball.spinX !== 0) {
            // English affects angle off cushion
            const spinEffect = ball.spinX * 0.3;
            
            if (bounceAxis === 'vertical') {
                // Side cushion - English affects vertical component
                ball.vy += spinEffect * Math.abs(ball.vx);
            } else if (bounceAxis === 'horizontal') {
                // Top/bottom cushion - English affects horizontal component
                ball.vx += spinEffect * Math.abs(ball.vy);
            }
            
            // English gradually reduces after bounce
            ball.spinX *= 0.8;
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
     * Handle ball-to-ball collision with rotation transfer
     */
    handleBallCollision(b1, b2) {
        const dx = b2.x - b1.x;
        const dy = b2.y - b1.y;
        const distSq = dx * dx + dy * dy;
        const minDist = b1.r + b2.r;
        
        // Check if balls are colliding
        if (distSq < minDist * minDist) {
            const dist = Math.sqrt(distSq);
            
            // Normalize collision vector
            const nx = dx / dist;
            const ny = dy / dist;
            
            // Relative velocity
            const dvx = b2.vx - b1.vx;
            const dvy = b2.vy - b1.vy;
            
            // Relative velocity in collision normal direction
            const dvn = dvx * nx + dvy * ny;
            
            // Only resolve if balls are moving toward each other
            if (dvn < 0) {
                // Enhanced elastic collision with slight energy loss
                const impulse = dvn * this.COLLISION_DAMPING;
                
                b1.vx += impulse * nx;
                b1.vy += impulse * ny;
                b2.vx -= impulse * nx;
                b2.vy -= impulse * ny;
                
                // Update rotation axes after collision
                const speed1 = Math.sqrt(b1.vx * b1.vx + b1.vy * b1.vy);
                const speed2 = Math.sqrt(b2.vx * b2.vx + b2.vy * b2.vy);
                
                if (speed1 > 0) {
                    b1.rotationAxisX = -b1.vy / speed1;
                    b1.rotationAxisY = b1.vx / speed1;
                }
                
                if (speed2 > 0) {
                    b2.rotationAxisX = -b2.vy / speed2;
                    b2.rotationAxisY = b2.vx / speed2;
                }
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
