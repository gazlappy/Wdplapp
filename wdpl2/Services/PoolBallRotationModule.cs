namespace Wdpl2.Services;

/// <summary>
/// Ball rotation module for pool game - handles visual rotation tracking for stripes and numbers
/// Uses proper 3D mathematics: Kinematic Rolling + Rodrigues' Rotation Formula
/// Reference: BallRotationInfo.md
/// </summary>
public static class PoolBallRotationModule
{
    public static string GenerateJavaScript()
    {
        return """
// ============================================
// POOL BALL ROTATION MODULE (Advanced 3D Math)
// 
// Uses proper kinematic rolling and Rodrigues' rotation formula
// for accurate ball rotation visualization
//
// Key Concepts:
// 1. Kinematic Rolling: ? = d/r, axis = normalize(n × v)
// 2. Rodrigues Formula: v' = v·cos(?) + (k×v)·sin(?) + k·(k·v)·(1-cos(?))
// 3. Quaternion tracking for smooth rotation without gimbal lock
// ============================================

const PoolBallRotation = {
    
// Surface normal for pool table (pointing up toward viewer in top-down view)
SURFACE_NORMAL: { x: 0, y: 0, z: 1 },
    
// ===== VISUAL SENSITIVITY SETTINGS =====
// Single multiplier for BOTH stripe and number - they must move together!
VISUAL_MULTIPLIER: 2.5,         // How much to amplify rotation for visibility
MIN_VISIBLE_SPEED: 0.05,        // Minimum speed to show rotation
    
/**
 * Initialize rotation properties on a ball
 * Sets up quaternion rotation and reference points
 * @param {Object} ball - The ball object to initialize
 */
initBall(ball) {
    // ===== QUATERNION ROTATION STATE =====
    // Quaternion (w, x, y, z) for smooth rotation without gimbal lock
    // Identity quaternion = no rotation
    ball.rotQ = { w: 1, x: 0, y: 0, z: 0 };
        
    // ===== REFERENCE POINTS ON BALL SURFACE =====
    // These are unit vectors that we rotate to track ball orientation
        
        
        // Number/logo position - starts facing viewer (0, 0, 1)
        ball.numPosX = 0;
        ball.numPosY = 0;
        ball.numPosZ = 1;
        
        // North pole reference - for tracking stripe orientation (0, -1, 0)
        // This point starts at the "top" of the ball
        ball.polePosX = 0;
        ball.polePosY = -1;
        ball.polePosZ = 0;
        
        
        // Equator reference point - for tracking stripe position (1, 0, 0)
        ball.eqPosX = 1;
        ball.eqPosY = 0;
        ball.eqPosZ = 0;
        
        // ===== ANGULAR VELOCITY =====
        ball.omega = 0;           // Angular speed (rad/s)
        ball.rotAxisX = 0;        // Current rotation axis
        ball.rotAxisY = 0;
        ball.rotAxisZ = 0;
        
        // ===== CUMULATIVE ROTATION TRACKING =====
        ball.totalRotation = 0;   // Total radians rotated (for effects)
    },
    
    /**
     * Calculate rotation axis from velocity using kinematic rolling
     * axis = normalize(surfaceNormal × velocity)
     * @param {number} vx - X velocity
     * @param {number} vy - Y velocity
     * @returns {Object} Normalized rotation axis {x, y, z}
     */
    calculateRotationAxis(vx, vy) {
        // Cross product: (0, 0, 1) × (vx, vy, 0) = (-vy, vx, 0)
        const axisX = -vy;
        const axisY = vx;
        const axisZ = 0;
        
        // Normalize
        const len = Math.sqrt(axisX * axisX + axisY * axisY + axisZ * axisZ);
        if (len < 0.0001) {
            return { x: 0, y: 0, z: 1 }; // Default axis if no movement
        }
        
        return {
            x: axisX / len,
            y: axisY / len,
            z: axisZ / len
        };
    },
    
    /**
     * Apply Rodrigues' Rotation Formula to rotate a point
     * v' = v·cos(?) + (k×v)·sin(?) + k·(k·v)·(1-cos(?))
     * 
     * @param {Object} point - Point to rotate {x, y, z}
     * @param {Object} axis - Unit rotation axis {x, y, z}
     * @param {number} angle - Rotation angle in radians
     * @returns {Object} Rotated point {x, y, z}
     */
    rotatePointRodrigues(point, axis, angle) {
        const cos_a = Math.cos(angle);
        const sin_a = Math.sin(angle);
        const one_minus_cos = 1 - cos_a;
        
        const px = point.x;
        const py = point.y;
        const pz = point.z;
        
        const kx = axis.x;
        const ky = axis.y;
        const kz = axis.z;
        
        // k · v (dot product)
        const dot = kx * px + ky * py + kz * pz;
        
        // k × v (cross product)
        const crossX = ky * pz - kz * py;
        const crossY = kz * px - kx * pz;
        const crossZ = kx * py - ky * px;
        
        // Rodrigues formula
        return {
            x: px * cos_a + crossX * sin_a + kx * dot * one_minus_cos,
            y: py * cos_a + crossY * sin_a + ky * dot * one_minus_cos,
            z: pz * cos_a + crossZ * sin_a + kz * dot * one_minus_cos
        };
    },
    
    /**
     * Normalize a point to keep it on the unit sphere
     * @param {Object} point - Point to normalize {x, y, z}
     * @returns {Object} Normalized point
     */
    normalizePoint(point) {
        const len = Math.sqrt(point.x * point.x + point.y * point.y + point.z * point.z);
        if (len < 0.0001) {
            return { x: 0, y: 0, z: 1 };
        }
        return {
            x: point.x / len,
            y: point.y / len,
            z: point.z / len
        };
    },
    
    /**
     * Update quaternion rotation state
     * q_new = q_delta * q_current
     * @param {Object} ball - Ball object
     * @param {Object} axis - Rotation axis
     * @param {number} angle - Rotation angle
     */
    updateQuaternion(ball, axis, angle) {
        // Create delta quaternion from axis-angle
        const halfAngle = angle / 2;
        const sinHalf = Math.sin(halfAngle);
        const cosHalf = Math.cos(halfAngle);
        
        const dq = {
            w: cosHalf,
            x: axis.x * sinHalf,
            y: axis.y * sinHalf,
            z: axis.z * sinHalf
        };
        
        // Multiply: q_new = dq * q_current
        const q = ball.rotQ;
        ball.rotQ = {
            w: dq.w * q.w - dq.x * q.x - dq.y * q.y - dq.z * q.z,
            x: dq.w * q.x + dq.x * q.w + dq.y * q.z - dq.z * q.y,
            y: dq.w * q.y - dq.x * q.z + dq.y * q.w + dq.z * q.x,
            z: dq.w * q.z + dq.x * q.y - dq.y * q.x + dq.z * q.w
        };
        
        // Normalize quaternion to prevent drift
        const qLen = Math.sqrt(
            ball.rotQ.w * ball.rotQ.w + 
            ball.rotQ.x * ball.rotQ.x + 
            ball.rotQ.y * ball.rotQ.y + 
            ball.rotQ.z * ball.rotQ.z
        );
        if (qLen > 0.0001) {
            ball.rotQ.w /= qLen;
            ball.rotQ.x /= qLen;
            ball.rotQ.y /= qLen;
            ball.rotQ.z /= qLen;
        }
    },
    
    /**
     * Rotate a point using quaternion
     * @param {Object} point - Point to rotate
     * @param {Object} q - Quaternion
     * @returns {Object} Rotated point
     */
    rotatePointByQuaternion(point, q) {
        // Convert point to quaternion (0, x, y, z)
        const px = point.x, py = point.y, pz = point.z;
        
        // q * p * q^(-1)
        // First: q * p
        const qpW = -q.x * px - q.y * py - q.z * pz;
        const qpX = q.w * px + q.y * pz - q.z * py;
        const qpY = q.w * py - q.x * pz + q.z * px;
        const qpZ = q.w * pz + q.x * py - q.y * px;
        
        // Then: (q * p) * q^(-1), where q^(-1) = (w, -x, -y, -z) for unit quaternion
        return {
            x: qpW * (-q.x) + qpX * q.w + qpY * (-q.z) - qpZ * (-q.y),
            y: qpW * (-q.y) - qpX * (-q.z) + qpY * q.w + qpZ * (-q.x),
            z: qpW * (-q.z) + qpX * (-q.y) - qpY * (-q.x) + qpZ * q.w
        };
    },
    
    
    /**
     * Main update function - call each frame for moving balls
     * Calculates rotation based on velocity and updates all reference points
     * 
     * @param {Object} ball - The ball object to update
     */
    updateRotation(ball) {
        const speed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
        
        if (speed < this.MIN_VISIBLE_SPEED) {
            ball.omega = 0;
            return;
        }
        
        // Initialize if needed
        if (ball.rotQ === undefined) {
            this.initBall(ball);
        }
        
        // ===== KINEMATIC ROLLING CALCULATION =====
        // Rotation angle ? = distance / radius = speed / radius (per frame)
        const rotationAngle = speed / ball.r;
        
        // Apply visual multiplier for better visibility at slow speeds
        // SAME multiplier for stripe AND number so they stay together!
        const visualAngle = rotationAngle * this.VISUAL_MULTIPLIER;
        
        // Rotation axis = normalize(surfaceNormal × velocity)
        const axis = this.calculateRotationAxis(ball.vx, ball.vy);
        
        // Store current rotation state
        ball.omega = speed / ball.r;
        ball.rotAxisX = axis.x;
        ball.rotAxisY = axis.y;
        ball.rotAxisZ = axis.z;
        ball.totalRotation += rotationAngle;
        
        // ===== UPDATE QUATERNION (with visual amplification) =====
        this.updateQuaternion(ball, axis, visualAngle);
        
        // ===== ROTATE ALL REFERENCE POINTS USING RODRIGUES =====
        // All points use the SAME visualAngle so everything moves together
        
        // Rotate number position
        const numPos = this.rotatePointRodrigues(
            { x: ball.numPosX, y: ball.numPosY, z: ball.numPosZ },
            axis,
            visualAngle
        );
        const normNum = this.normalizePoint(numPos);
        ball.numPosX = normNum.x;
        ball.numPosY = normNum.y;
        ball.numPosZ = normNum.z;
        
        // Rotate pole position (for stripe orientation)
        const polePos = this.rotatePointRodrigues(
            { x: ball.polePosX, y: ball.polePosY, z: ball.polePosZ },
            axis,
            visualAngle
        );
        const normPole = this.normalizePoint(polePos);
        ball.polePosX = normPole.x;
        ball.polePosY = normPole.y;
        ball.polePosZ = normPole.z;
        
        // Rotate equator reference point
        const eqPos = this.rotatePointRodrigues(
            { x: ball.eqPosX, y: ball.eqPosY, z: ball.eqPosZ },
            axis,
            visualAngle
        );
        const normEq = this.normalizePoint(eqPos);
        ball.eqPosX = normEq.x;
        ball.eqPosY = normEq.y;
        ball.eqPosZ = normEq.z;
    },
    
    /**
     * Get the vertical offset for the stripe based on rotation
     * Uses numPosY which tracks the same rotation as the number
     * @param {Object} ball - The ball object
     * @returns {number} Offset factor (-1 to 1)
     */
    getStripeOffset(ball) {
        // Use numPosY - this is the SAME point we use for the number position
        // So the stripe and number always move together as one unit
        // numPosY goes from 0 (front) to ±1 (top/bottom) as ball rotates
        const numY = ball.numPosY !== undefined ? ball.numPosY : 0;
        return -numY;  // Negate so positive Y velocity moves stripe down
    },
    
    /**
     * Get stripe tilt angle for 3D effect
     * @param {Object} ball - The ball object
     * @returns {number} Tilt angle in radians
     */
    getStripeTilt(ball) {
        const poleX = ball.polePosX !== undefined ? ball.polePosX : 0;
        const poleZ = ball.polePosZ !== undefined ? ball.polePosZ : 0;
        return Math.atan2(poleX, poleZ);
    },
    
    /**
     * Get the visibility/facing factor for the number
     * @param {Object} ball - The ball object
     * @returns {number} Visibility factor (-1 to 1, positive = facing viewer)
     */
    getNumberVisibility(ball) {
        // numPosZ indicates how much the number faces the viewer
        return ball.numPosZ !== undefined ? ball.numPosZ : 1;
    },
    
    /**
     * Get 2D screen position offset for the number
     * @param {Object} ball - The ball object
     * @returns {Object} {x, y} offset from ball center (normalized -1 to 1)
     */
    getNumberScreenOffset(ball) {
        return {
            x: ball.numPosX !== undefined ? ball.numPosX : 0,
            y: ball.numPosY !== undefined ? ball.numPosY : 0
        };
    },
    
    /**
     * Get the 3D position of the number on the ball surface
     * @param {Object} ball - The ball object
     * @returns {Object} {x, y, z} position on unit sphere
     */
    getNumberPosition3D(ball) {
        return {
            x: ball.numPosX !== undefined ? ball.numPosX : 0,
            y: ball.numPosY !== undefined ? ball.numPosY : 0,
            z: ball.numPosZ !== undefined ? ball.numPosZ : 1
        };
    },
    
    /**
     * Check if the number should be drawn (is it on the visible side?)
     * @param {Object} ball - The ball object
     * @param {number} threshold - Visibility threshold (default -0.15)
     * @returns {boolean} True if number should be drawn
     */
    isNumberVisible(ball, threshold = -0.15) {
        return this.getNumberVisibility(ball) > threshold;
    },
    
    /**
     * Get perspective scale for the number based on depth
     * @param {Object} ball - The ball object
     * @returns {number} Scale factor (0.5 to 1.0)
     */
    getNumberScale(ball) {
        const visibility = this.getNumberVisibility(ball);
        return 0.5 + Math.max(0, visibility) * 0.5;
    },
    
    /**
     * Get alpha/opacity for the number based on visibility
     * @param {Object} ball - The ball object
     * @returns {number} Alpha value (0 to 1)
     */
    getNumberAlpha(ball) {
        const visibility = this.getNumberVisibility(ball);
        return Math.max(0, Math.min(1, (visibility + 0.2) / 1.2));
    },
    
    /**
     * Reset rotation state when ball stops (keeps current orientation)
     * @param {Object} ball - The ball object
     */
    onBallStopped(ball) {
        ball.omega = 0;
        ball.rotAxisX = 0;
        ball.rotAxisY = 0;
        ball.rotAxisZ = 0;
    },
    
    /**
     * Reset rotation to initial state
     * @param {Object} ball - The ball object
     */
    resetRotation(ball) {
        this.initBall(ball);
    },
    
    /**
     * Get debug info for the ball's rotation state
     * @param {Object} ball - The ball object
     * @returns {Object} Debug information
     */
    getDebugInfo(ball) {
        return {
            omega: ball.omega || 0,
            totalRotation: ball.totalRotation || 0,
            numPos: { x: ball.numPosX, y: ball.numPosY, z: ball.numPosZ },
            polePos: { x: ball.polePosX, y: ball.polePosY, z: ball.polePosZ },
            quaternion: ball.rotQ,
            stripeOffset: this.getStripeOffset(ball),
            numberVisible: this.isNumberVisible(ball)
        };
    }
};
""";
    }
}
