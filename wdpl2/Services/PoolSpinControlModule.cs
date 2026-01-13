namespace Wdpl2.Services;

/// <summary>
/// Spin control module for pool game - visual ball spin selector
/// </summary>
public static class PoolSpinControlModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL SPIN CONTROL MODULE
// Visual ball overlay for applying spin
// ============================================

const PoolSpinControl = {
    // Spin control state
    spinX: 0,  // -1 to 1 (left to right English)
    spinY: 0,  // -1 to 1 (bottom to top spin)
    ballOverlayRadius: 35,
    overlayX: 60,
    overlayY: 60,
    isDragging: false,
    
    /**
     * Draw the spin control overlay
     */
    drawSpinControl(ctx) {
        const centerX = this.overlayX;
        const centerY = this.overlayY;
        const radius = this.ballOverlayRadius;
        
        // Semi-transparent background panel
        ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
        ctx.fillRect(centerX - radius - 10, centerY - radius - 10, radius * 2 + 20, radius * 2 + 35);
        
        // Label
        ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
        ctx.font = 'bold 11px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('SPIN', centerX, centerY + radius + 20);
        
        // Draw white ball
        const ballGrad = ctx.createRadialGradient(
            centerX - radius * 0.3, centerY - radius * 0.3, 0,
            centerX, centerY, radius
        );
        ballGrad.addColorStop(0, '#ffffff');
        ballGrad.addColorStop(0.7, '#e0e0e0');
        ballGrad.addColorStop(1, '#a0a0a0');
        
        ctx.fillStyle = ballGrad;
        ctx.beginPath();
        ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
        ctx.fill();
        
        // Ball outline
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(centerX, centerY, radius, 0, Math.PI * 2);
        ctx.stroke();
        
        // Draw center point
        ctx.fillStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.beginPath();
        ctx.arc(centerX, centerY, 3, 0, Math.PI * 2);
        ctx.fill();
        
        // Draw guide circles
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.15)';
        ctx.lineWidth = 1;
        for (let i = 1; i <= 2; i++) {
            ctx.beginPath();
            ctx.arc(centerX, centerY, radius * (i / 3), 0, Math.PI * 2);
            ctx.stroke();
        }
        
        // Draw crosshair lines
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.2)';
        ctx.lineWidth = 1;
        ctx.setLineDash([3, 3]);
        
        // Horizontal
        ctx.beginPath();
        ctx.moveTo(centerX - radius, centerY);
        ctx.lineTo(centerX + radius, centerY);
        ctx.stroke();
        
        // Vertical
        ctx.beginPath();
        ctx.moveTo(centerX, centerY - radius);
        ctx.lineTo(centerX, centerY + radius);
        ctx.stroke();
        
        ctx.setLineDash([]);
        
        // Calculate crosshair position based on spin
        const crosshairX = centerX + this.spinX * (radius - 5);
        const crosshairY = centerY - this.spinY * (radius - 5); // Inverted Y for intuitive control
        
        // Draw crosshair shadow
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.4)';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(crosshairX - 8, crosshairY);
        ctx.lineTo(crosshairX + 8, crosshairY);
        ctx.moveTo(crosshairX, crosshairY - 8);
        ctx.lineTo(crosshairX, crosshairY + 8);
        ctx.stroke();
        
        // Draw crosshair
        ctx.strokeStyle = this.isDragging ? '#ff4444' : '#ff6b6b';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(crosshairX - 8, crosshairY);
        ctx.lineTo(crosshairX + 8, crosshairY);
        ctx.moveTo(crosshairX, crosshairY - 8);
        ctx.lineTo(crosshairX, crosshairY + 8);
        ctx.stroke();
        
        // Crosshair center dot
        ctx.fillStyle = this.isDragging ? '#ff0000' : '#ff4444';
        ctx.beginPath();
        ctx.arc(crosshairX, crosshairY, 3, 0, Math.PI * 2);
        ctx.fill();
        
        // Draw spin direction arrow if spin is applied
        if (this.spinX !== 0 || this.spinY !== 0) {
            const arrowStartX = centerX;
            const arrowStartY = centerY;
            const arrowEndX = crosshairX;
            const arrowEndY = crosshairY;
            const arrowLength = Math.sqrt(
                (arrowEndX - arrowStartX) ** 2 + 
                (arrowEndY - arrowStartY) ** 2
            );
            
            if (arrowLength > 5) {
                // Arrow line
                ctx.strokeStyle = 'rgba(255, 107, 107, 0.6)';
                ctx.lineWidth = 2;
                ctx.beginPath();
                ctx.moveTo(arrowStartX, arrowStartY);
                ctx.lineTo(arrowEndX, arrowEndY);
                ctx.stroke();
                
                // Arrow head
                const angle = Math.atan2(arrowEndY - arrowStartY, arrowEndX - arrowStartX);
                ctx.fillStyle = 'rgba(255, 107, 107, 0.8)';
                ctx.beginPath();
                ctx.moveTo(arrowEndX, arrowEndY);
                ctx.lineTo(
                    arrowEndX - 8 * Math.cos(angle - Math.PI / 6),
                    arrowEndY - 8 * Math.sin(angle - Math.PI / 6)
                );
                ctx.lineTo(
                    arrowEndX - 8 * Math.cos(angle + Math.PI / 6),
                    arrowEndY - 8 * Math.sin(angle + Math.PI / 6)
                );
                ctx.closePath();
                ctx.fill();
            }
        }
        
        // Display spin values
        ctx.fillStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.font = '9px Arial';
        ctx.textAlign = 'left';
        
        const spinPower = Math.sqrt(this.spinX * this.spinX + this.spinY * this.spinY);
        const spinPercent = Math.round(spinPower * 100);
        
        if (spinPercent > 0) {
            ctx.fillText('Spin: ' + spinPercent + '%', centerX - radius - 5, centerY + radius + 32);
        }
    },
    
    /**
     * Check if mouse is over spin control
     */
    isOverSpinControl(mouseX, mouseY) {
        const dx = mouseX - this.overlayX;
        const dy = mouseY - this.overlayY;
        const distance = Math.sqrt(dx * dx + dy * dy);
        return distance <= this.ballOverlayRadius;
    },
    
    /**
     * Update spin based on mouse position
     */
    updateSpin(mouseX, mouseY) {
        const dx = mouseX - this.overlayX;
        const dy = mouseY - this.overlayY;
        
        // Clamp to circle
        const distance = Math.sqrt(dx * dx + dy * dy);
        const maxDistance = this.ballOverlayRadius - 5;
        
        if (distance > maxDistance) {
            const scale = maxDistance / distance;
            this.spinX = (dx * scale) / maxDistance;
            this.spinY = -(dy * scale) / maxDistance; // Inverted Y
        } else {
            this.spinX = dx / maxDistance;
            this.spinY = -dy / maxDistance; // Inverted Y
        }
        
        // Clamp values
        this.spinX = Math.max(-1, Math.min(1, this.spinX));
        this.spinY = Math.max(-1, Math.min(1, this.spinY));
    },
    
    /**
     * Reset spin to center
     */
    resetSpin() {
        this.spinX = 0;
        this.spinY = 0;
    },
    
    /**
     * Apply spin to ball velocity
     * Called when ball is shot
     */
    applySpinToBall(ball, aimAngle) {
        if (!ball) return;
        
        // Store spin values for physics calculations
        ball.spinX = this.spinX;
        ball.spinY = this.spinY;
        
        const currentSpeed = Math.sqrt(ball.vx * ball.vx + ball.vy * ball.vy);
        
        // Apply English (side spin) - affects direction immediately
        if (this.spinX !== 0) {
            const spinEffect = this.spinX * 0.25; // 25% maximum deflection
            const perpAngle = aimAngle + Math.PI / 2;
            
            ball.vx += Math.cos(perpAngle) * spinEffect * currentSpeed;
            ball.vy += Math.sin(perpAngle) * spinEffect * currentSpeed;
        }
        
        // Top/back spin affects initial velocity
        if (this.spinY !== 0) {
            const speedModifier = 1 + (this.spinY * 0.15); // +/- 15% speed
            ball.vx *= speedModifier;
            ball.vy *= speedModifier;
        }
        
        // Store spin magnitude for visual feedback
        ball.spinMagnitude = Math.sqrt(this.spinX * this.spinX + this.spinY * this.spinY);
    },
    
    /**
     * Setup event handlers for spin control
     */
    setupSpinControl(canvas, game) {
        let spinDragging = false;
        
        canvas.addEventListener('mousedown', (e) => {
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const scaleY = canvas.height / rect.height;
            const mouseX = (e.clientX - rect.left) * scaleX;
            const mouseY = (e.clientY - rect.top) * scaleY;
            
            if (this.isOverSpinControl(mouseX, mouseY)) {
                spinDragging = true;
                this.isDragging = true;
                this.updateSpin(mouseX, mouseY);
                e.stopPropagation();
            }
        }, true);
        
        canvas.addEventListener('mousemove', (e) => {
            if (spinDragging) {
                const rect = canvas.getBoundingClientRect();
                const scaleX = canvas.width / rect.width;
                const scaleY = canvas.height / rect.height;
                const mouseX = (e.clientX - rect.left) * scaleX;
                const mouseY = (e.clientY - rect.top) * scaleY;
                
                this.updateSpin(mouseX, mouseY);
                e.stopPropagation();
            }
        }, true);
        
        canvas.addEventListener('mouseup', (e) => {
            if (spinDragging) {
                spinDragging = false;
                this.isDragging = false;
                e.stopPropagation();
            }
        }, true);
        
        // Double-click to reset spin
        canvas.addEventListener('dblclick', (e) => {
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const scaleY = canvas.height / rect.height;
            const mouseX = (e.clientX - rect.left) * scaleX;
            const mouseY = (e.clientY - rect.top) * scaleY;
            
            if (this.isOverSpinControl(mouseX, mouseY)) {
                this.resetSpin();
                e.stopPropagation();
            }
        }, true);
    }
};
";
    }
}
