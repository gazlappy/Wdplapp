namespace Wdpl2.Services;

/// <summary>
/// Rendering module for pool game - handles drawing table, balls, cue stick
/// ENHANCED: Realistic lighting, shadows, and textures
/// </summary>
public static class PoolRenderingModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL RENDERING MODULE (ENHANCED)
// Realistic graphics with lighting and shadows
// ============================================

const PoolRendering = {
    /**
     * Draw the pool table with realistic felt texture
     */
    drawTable(ctx, width, height, cushionMargin) {
        // Draw felt with subtle texture
        ctx.fillStyle = '#0d5c2b';
        ctx.fillRect(0, 0, width, height);
        
        // Add subtle felt grain pattern
        ctx.save();
        ctx.globalAlpha = 0.03;
        for (let i = 0; i < 50; i++) {
            ctx.strokeStyle = i % 2 === 0 ? '#0a4a23' : '#10642f';
            ctx.lineWidth = 0.5;
            ctx.beginPath();
            ctx.moveTo(Math.random() * width, 0);
            ctx.lineTo(Math.random() * width, height);
            ctx.stroke();
        }
        ctx.restore();
        
        // Center line with fade effect
        const lineGrad = ctx.createLinearGradient(0, 0, 0, height);
        lineGrad.addColorStop(0, 'rgba(255, 255, 255, 0)');
        lineGrad.addColorStop(0.1, 'rgba(255, 255, 255, 0.15)');
        lineGrad.addColorStop(0.5, 'rgba(255, 255, 255, 0.2)');
        lineGrad.addColorStop(0.9, 'rgba(255, 255, 255, 0.15)');
        lineGrad.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.strokeStyle = lineGrad;
        ctx.lineWidth = 3;
        ctx.setLineDash([15, 10]);
        ctx.beginPath();
        ctx.moveTo(width / 2, 0);
        ctx.lineTo(width / 2, height);
        ctx.stroke();
        ctx.setLineDash([]);
        
        // Break line (head string) at 1/4 from left
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.1)';
        ctx.lineWidth = 2;
        ctx.setLineDash([8, 8]);
        ctx.beginPath();
        ctx.moveTo(width * 0.25, cushionMargin);
        ctx.lineTo(width * 0.25, height - cushionMargin);
        ctx.stroke();
        ctx.setLineDash([]);
        
        // Cushions with 3D effect - properly sized
        const cushionWidth = cushionMargin;
        const cushionGrad = ctx.createLinearGradient(0, 0, cushionWidth, 0);
        cushionGrad.addColorStop(0, '#5c3317');
        cushionGrad.addColorStop(0.3, '#8B4513');
        cushionGrad.addColorStop(0.7, '#A0522D');
        cushionGrad.addColorStop(1, '#6d3e1f');
        
        ctx.strokeStyle = cushionGrad;
        ctx.lineWidth = cushionWidth;
        ctx.strokeRect(
            cushionWidth / 2, 
            cushionWidth / 2, 
            width - cushionWidth, 
            height - cushionWidth
        );
        
        // Inner cushion shadow
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.lineWidth = 3;
        ctx.strokeRect(
            cushionWidth + 2, 
            cushionWidth + 2, 
            width - cushionWidth * 2 - 4, 
            height - cushionWidth * 2 - 4
        );
        
        // Table edge highlight
        ctx.strokeStyle = 'rgba(139, 90, 43, 0.6)';
        ctx.lineWidth = 2;
        ctx.strokeRect(
            cushionWidth + 5, 
            cushionWidth + 5, 
            width - cushionWidth * 2 - 10, 
            height - cushionWidth * 2 - 10
        );
    },
    
    /**
     * Draw pockets - delegates to PoolPockets module
     */
    drawPockets(ctx, pockets, game) {
        PoolPockets.drawPockets(ctx, pockets);
        
        // Draw developer visualizations if enabled
        if (game && game.showCaptureZones) {
            // Draw capture zone circles (the actual physics collision detection area)
            pockets.forEach(p => {
                ctx.strokeStyle = 'rgba(255, 100, 100, 0.6)';
                ctx.lineWidth = 2;
                ctx.setLineDash([5, 5]);
                ctx.beginPath();
                ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
                ctx.stroke();
                ctx.setLineDash([]);
                
                // Label capture zone
                ctx.fillStyle = 'rgba(255, 100, 100, 0.8)';
                ctx.font = 'bold 10px Arial';
                ctx.textAlign = 'center';
                ctx.fillText('Capture: ' + p.r.toFixed(1) + 'px', p.x, p.y - p.r - 8);
            });
        }
        
        if (game && game.showPocketZones) {
            // Draw visual opening circles (what the player sees as the pocket opening)
            pockets.forEach(p => {
                const opening = p.type === 'corner' ? 
                    (game.cornerPocketOpening || p.r) : 
                    (game.middlePocketOpening || p.r);
                
                ctx.strokeStyle = 'rgba(100, 255, 100, 0.4)';
                ctx.lineWidth = 2;
                ctx.setLineDash([8, 4]);
                ctx.beginPath();
                ctx.arc(p.x, p.y, opening, 0, Math.PI * 2);
                ctx.stroke();
                ctx.setLineDash([]);
                
                // Label visual opening
                ctx.fillStyle = 'rgba(100, 255, 100, 0.8)';
                ctx.font = 'bold 10px Arial';
                ctx.textAlign = 'center';
                ctx.fillText('Opening: ' + opening.toFixed(1) + 'px', p.x, p.y + opening + 16);
            });
        }
    },
    
    /**
     * Draw a ball with realistic 3D rolling effect
     */
    drawBall(ctx, ball) {
        if (ball.potted) return;
        
        // Ball shadow on table
        ctx.save();
        ctx.fillStyle = 'rgba(0, 0, 0, 0.25)';
        ctx.beginPath();
        ctx.ellipse(ball.x + 2, ball.y + 3, ball.r * 0.9, ball.r * 0.6, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
        
        // Main ball gradient (light from top-left)
        const lightOffsetX = -ball.r * 0.35;
        const lightOffsetY = -ball.r * 0.35;
        
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.2,
            ball.x, ball.y, ball.r * 1.1
        );
        
        // Set colors based on ball type
        if (ball.color === 'white') {
            grad.addColorStop(0, '#ffffff');
            grad.addColorStop(0.3, '#f5f5f5');
            grad.addColorStop(0.7, '#e0e0e0');
            grad.addColorStop(1, '#b0b0b0');
        } else if (ball.color === 'red') {
            grad.addColorStop(0, '#ff9999');
            grad.addColorStop(0.2, '#ff6b6b');
            grad.addColorStop(0.6, '#e63946');
            grad.addColorStop(1, '#9d0208');
        } else if (ball.color === 'yellow') {
            grad.addColorStop(0, '#fff9c4');
            grad.addColorStop(0.2, '#ffd43b');
            grad.addColorStop(0.6, '#fab005');
            grad.addColorStop(1, '#c17900');
        } else { // black
            grad.addColorStop(0, '#4a4a4a');
            grad.addColorStop(0.3, '#2a2a2a');
            grad.addColorStop(0.7, '#1a1a1a');
            grad.addColorStop(1, '#000000');
        }
        
        // Draw ball base
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Draw number that rotates with the ball
        if (ball.num > 0 && ball.rotation !== undefined) {
            ctx.save();
            ctx.beginPath();
            ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
            ctx.clip();
            
            // Calculate number position based on rotation
            const numberAngle = ball.rotation;
            
            // Calculate 3D position of number on sphere
            const numberDepth = Math.cos(numberAngle);
            
            // Only draw if number is on visible hemisphere
            if (numberDepth > -0.3) {
                const numberX = ball.x + Math.sin(numberAngle) * ball.r * 0.3;
                const numberY = ball.y;
                
                // Scale and fade based on depth (3D effect)
                const scale = 0.7 + (numberDepth * 0.3);
                const alpha = Math.max(0, (numberDepth + 0.3) / 1.3);
                
                ctx.globalAlpha = alpha;
                
                // White circle for number - scaled based on depth
                const circleRadius = ball.r * 0.5 * scale;
                const numGrad = ctx.createRadialGradient(
                    numberX - 1, numberY - 1, 0,
                    numberX, numberY, circleRadius
                );
                numGrad.addColorStop(0, '#ffffff');
                numGrad.addColorStop(0.8, '#f0f0f0');
                numGrad.addColorStop(1, '#d0d0d0');
                
                ctx.fillStyle = numGrad;
                ctx.beginPath();
                ctx.arc(numberX, numberY, circleRadius, 0, Math.PI * 2);
                ctx.fill();
                
                // Number shadow
                ctx.fillStyle = 'rgba(0, 0, 0, 0.2)';
                ctx.font = 'bold ' + (9 * scale) + 'px Arial';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'middle';
                ctx.fillText(ball.num, numberX + 0.5, numberY + 0.5);
                
                // Number
                ctx.fillStyle = '#1a1a1a';
                ctx.font = 'bold ' + (9 * scale) + 'px Arial';
                ctx.fillText(ball.num, numberX, numberY);
            }
            
            ctx.restore();
        }
        
        // Draw subtle rolling texture - simplified
        if (ball.rotation !== undefined) {
            ctx.save();
            ctx.beginPath();
            ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
            ctx.clip();
            
            ctx.globalAlpha = 0.08;
            
            // Simple rolling lines that move with rotation
            const numLines = 8;
            for (let i = 0; i < numLines; i++) {
                const lineAngle = (i / numLines) * Math.PI * 2 + ball.rotation;
                const depth = Math.cos(lineAngle);
                
                // Only draw lines on visible hemisphere
                if (depth > 0) {
                    const lineAlpha = depth * 0.6;
                    ctx.strokeStyle = 'rgba(0, 0, 0, ' + lineAlpha + ')';
                    ctx.lineWidth = 1;
                    
                    // Draw arc across ball surface
                    ctx.beginPath();
                    const startAngle = lineAngle - Math.PI / 2;
                    const endAngle = lineAngle + Math.PI / 2;
                    ctx.arc(ball.x, ball.y, ball.r * 0.8, startAngle, endAngle);
                    ctx.stroke();
                }
            }
            
            ctx.restore();
        }
        
        // Specular highlight (shiny spot) - always on top
        const specular = ctx.createRadialGradient(
            ball.x + lightOffsetX * 0.8, ball.y + lightOffsetY * 0.8, 0,
            ball.x + lightOffsetX * 0.8, ball.y + lightOffsetY * 0.8, ball.r * 0.4
        );
        specular.addColorStop(0, 'rgba(255, 255, 255, 0.9)');
        specular.addColorStop(0.3, 'rgba(255, 255, 255, 0.6)');
        specular.addColorStop(0.6, 'rgba(255, 255, 255, 0.2)');
        specular.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.fillStyle = specular;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Ball outline for definition
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.2)';
        ctx.lineWidth = 0.5;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.stroke();
        
        // Draw spin indicator if ball has active spin - ENHANCED VISIBILITY
        if ((ball.spinX !== undefined && Math.abs(ball.spinX) > 0.05) || 
            (ball.spinY !== undefined && Math.abs(ball.spinY) > 0.05)) {
            
            // Draw spin arrow on ball
            const spinMag = Math.sqrt(
                (ball.spinX || 0) * (ball.spinX || 0) + 
                (ball.spinY || 0) * (ball.spinY || 0)
            );
            const spinAngle = Math.atan2(-(ball.spinY || 0), ball.spinX || 0);
            
            const arrowLength = ball.r * 0.8 * spinMag;
            const arrowEndX = ball.x + Math.cos(spinAngle) * arrowLength;
            const arrowEndY = ball.y + Math.sin(spinAngle) * arrowLength;
            
            // ENHANCED: Different colors for different spin types
            let spinColor = 'rgba(255, 100, 100, 0.9)'; // Default red
            if (Math.abs(ball.spinY) > Math.abs(ball.spinX)) {
                // Top/back spin dominant
                spinColor = ball.spinY > 0 ? 'rgba(100, 255, 100, 0.9)' : 'rgba(100, 100, 255, 0.9)';
            }
            
            // Draw glow effect around ball with spin
            const glowGrad = ctx.createRadialGradient(ball.x, ball.y, ball.r, ball.x, ball.y, ball.r + 15);
            glowGrad.addColorStop(0, spinColor.replace('0.9', (spinMag * 0.3).toString()));
            glowGrad.addColorStop(1, spinColor.replace('0.9', '0'));
            ctx.fillStyle = glowGrad;
            ctx.beginPath();
            ctx.arc(ball.x, ball.y, ball.r + 15, 0, Math.PI * 2);
            ctx.fill();
            
            // Arrow shaft - thicker and more visible
            ctx.strokeStyle = spinColor;
            ctx.lineWidth = 4; // Increased from 3 to 4
            ctx.beginPath();
            ctx.moveTo(ball.x, ball.y);
            ctx.lineTo(arrowEndX, arrowEndY);
            ctx.stroke();
            
            // Arrow head - larger
            ctx.fillStyle = spinColor;
            ctx.beginPath();
            ctx.moveTo(arrowEndX, arrowEndY);
            ctx.lineTo(
                arrowEndX - 8 * Math.cos(spinAngle - Math.PI / 6),
                arrowEndY - 8 * Math.sin(spinAngle - Math.PI / 6)
            );
            ctx.lineTo(
                arrowEndX - 8 * Math.cos(spinAngle + Math.PI / 6),
                arrowEndY - 8 * Math.sin(spinAngle + Math.PI / 6)
            );
            ctx.closePath();
            ctx.fill();
            
            // Add spin magnitude text with background and TYPE
            ctx.fillStyle = 'rgba(0, 0, 0, 0.8)';
            ctx.fillRect(ball.x - 35, ball.y - ball.r - 25, 70, 16);
            
            ctx.fillStyle = 'rgba(255, 255, 255, 0.95)';
            ctx.font = 'bold 11px Arial';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            
            let spinText = Math.round(spinMag * 100) + '%';
            if (Math.abs(ball.spinY) > Math.abs(ball.spinX)) {
                spinText += ball.spinY > 0 ? ' TOP' : ' BACK';
            } else if (Math.abs(ball.spinX) > 0.1) {
                spinText += ball.spinX > 0 ? ' R' : ' L';
            }
            
            ctx.fillText(spinText, ball.x, ball.y - ball.r - 17);
        }
    },
    
    /**
     * Draw aim line with fade and trajectory prediction
     */
    drawAimLine(ctx, cueBall, aimAngle, length = 300) {
        // Main aim line with gradient fade
        const lineGrad = ctx.createLinearGradient(
            cueBall.x, cueBall.y,
            cueBall.x + Math.cos(aimAngle) * length,
            cueBall.y + Math.sin(aimAngle) * length
        );
        lineGrad.addColorStop(0, 'rgba(255, 255, 255, 0.8)');
        lineGrad.addColorStop(0.7, 'rgba(255, 255, 255, 0.4)');
        lineGrad.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.strokeStyle = lineGrad;
        ctx.lineWidth = 3;
        ctx.setLineDash([12, 8]);
        ctx.beginPath();
        ctx.moveTo(cueBall.x, cueBall.y);
        ctx.lineTo(
            cueBall.x + Math.cos(aimAngle) * length,
            cueBall.y + Math.sin(aimAngle) * length
        );
        ctx.stroke();
        ctx.setLineDash([]);
        
        // Target point indicator
        const targetX = cueBall.x + Math.cos(aimAngle) * 40;
        const targetY = cueBall.y + Math.sin(aimAngle) * 40;
        
        ctx.strokeStyle = 'rgba(255, 255, 100, 0.6)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(targetX, targetY, cueBall.r, 0, Math.PI * 2);
        ctx.stroke();
    },
    
    /**
     * Draw realistic wooden cue stick
     */
    drawCueStick(ctx, cueBall, aimAngle, pullBackDistance, pushForwardDistance) {
        const baseDist = 35;
        const cueDistance = baseDist + pullBackDistance - pushForwardDistance;
        const cueLength = 200;
        
        const cueStartX = cueBall.x - Math.cos(aimAngle) * cueDistance;
        const cueStartY = cueBall.y - Math.sin(aimAngle) * cueDistance;
        const cueEndX = cueBall.x - Math.cos(aimAngle) * (cueDistance + cueLength);
        const cueEndY = cueBall.y - Math.sin(aimAngle) * (cueDistance + cueLength);
        
        // Cue shadow
        ctx.save();
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.2)';
        ctx.lineWidth = 12;
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(cueStartX + 2, cueStartY + 2);
        ctx.lineTo(cueEndX + 2, cueEndY + 2);
        ctx.stroke();
        ctx.restore();
        
        // Main cue stick with wood grain
        const grad = ctx.createLinearGradient(cueStartX, cueStartY, cueEndX, cueEndY);
        grad.addColorStop(0, '#e6c9a8');
        grad.addColorStop(0.15, '#d4a574');
        grad.addColorStop(0.4, '#c19461');
        grad.addColorStop(0.6, '#8b6f47');
        grad.addColorStop(0.85, '#6d5436');
        grad.addColorStop(1, '#5a4a3a');
        
        ctx.strokeStyle = grad;
        ctx.lineWidth = 11;
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(cueStartX, cueStartY);
        ctx.lineTo(cueEndX, cueEndY);
        ctx.stroke();
        
        // Wood grain highlights
        ctx.strokeStyle = 'rgba(210, 180, 140, 0.3)';
        ctx.lineWidth = 9;
        ctx.beginPath();
        ctx.moveTo(cueStartX, cueStartY);
        ctx.lineTo(cueEndX + Math.cos(aimAngle) * 20, cueEndY + Math.sin(aimAngle) * 20);
        ctx.stroke();
        
        // Cue tip leather with 3D effect
        const tipGrad = ctx.createRadialGradient(
            cueStartX - 1, cueStartY - 1, 0,
            cueStartX, cueStartY, 8
        );
        tipGrad.addColorStop(0, '#7ba3d6');
        tipGrad.addColorStop(0.5, '#6495ED');
        tipGrad.addColorStop(1, '#4169E1');
        
        ctx.fillStyle = tipGrad;
        ctx.beginPath();
        ctx.arc(cueStartX, cueStartY, 7, 0, Math.PI * 2);
        ctx.fill();
        
        // Tip highlight
        ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.beginPath();
        ctx.arc(cueStartX - 2, cueStartY - 2, 3, 0, Math.PI * 2);
        ctx.fill();
        
        // Contact glow effect
        const distanceToContact = cueDistance - 12;
        if (distanceToContact < 15) {
            const glowIntensity = 1 - (distanceToContact / 15);
            ctx.save();
            ctx.fillStyle = `rgba(255, 215, 0, ${glowIntensity * 0.5})`;
            ctx.beginPath();
            ctx.arc(cueStartX, cueStartY, 15, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }
        
        // Ghost guide with motion blur
        if (pullBackDistance > 10) {
            const ghostStartX = cueBall.x - Math.cos(aimAngle) * baseDist;
            const ghostStartY = cueBall.y - Math.sin(aimAngle) * baseDist;
            
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.15)';
            ctx.lineWidth = 10;
            ctx.setLineDash([8, 8]);
            ctx.beginPath();
            ctx.moveTo(ghostStartX, ghostStartY);
            ctx.lineTo(cueStartX, cueStartY);
            ctx.stroke();
            ctx.setLineDash([]);
        }
        
        // Contact indicator
        const contactPointX = cueBall.x - Math.cos(aimAngle) * 12;
        const contactPointY = cueBall.y - Math.sin(aimAngle) * 12;
        const contactColor = distanceToContact < 5 ? 'rgba(255, 215, 0, 0.9)' : 'rgba(255, 255, 255, 0.5)';
        
        ctx.strokeStyle = contactColor;
        ctx.lineWidth = 2;
        ctx.setLineDash([4, 4]);
        ctx.beginPath();
        ctx.arc(contactPointX, contactPointY, 10, 0, Math.PI * 2);
        ctx.stroke();
        ctx.setLineDash([]);
    },
    
    /**
     * Draw enhanced power meter with 3D effect
     */
    drawPowerMeter(ctx, cueBall, shotPower, maxPower) {
        const meterX = cueBall.x + 40;
        const meterY = cueBall.y - 60;
        const meterHeight = 120;
        const meterWidth = 16;
        
        // Meter shadow
        ctx.fillStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.fillRect(meterX + 2, meterY + 2, meterWidth, meterHeight);
        
        // Meter background with gradient
        const bgGrad = ctx.createLinearGradient(meterX, 0, meterX + meterWidth, 0);
        bgGrad.addColorStop(0, 'rgba(30, 30, 30, 0.9)');
        bgGrad.addColorStop(0.5, 'rgba(20, 20, 20, 0.9)');
        bgGrad.addColorStop(1, 'rgba(30, 30, 30, 0.9)');
        ctx.fillStyle = bgGrad;
        ctx.fillRect(meterX, meterY, meterWidth, meterHeight);
        
        // Power fill with smooth gradient
        const powerPercent = shotPower / maxPower;
        const fillHeight = meterHeight * powerPercent;
        const powerGrad = ctx.createLinearGradient(meterX, meterY + meterHeight, meterX, meterY);
        
        if (powerPercent < 0.3) {
            powerGrad.addColorStop(0, '#4ade80');
            powerGrad.addColorStop(1, '#22c55e');
        } else if (powerPercent < 0.7) {
            powerGrad.addColorStop(0, '#fbbf24');
            powerGrad.addColorStop(1, '#f59e0b');
        } else {
            powerGrad.addColorStop(0, '#f87171');
            powerGrad.addColorStop(1, '#ef4444');
        }
        
        ctx.fillStyle = powerGrad;
        ctx.fillRect(meterX + 2, meterY + meterHeight - fillHeight, meterWidth - 4, fillHeight);
        
        // Glass effect overlay
        const glassGrad = ctx.createLinearGradient(meterX, 0, meterX + meterWidth, 0);
        glassGrad.addColorStop(0, 'rgba(255, 255, 255, 0.1)');
        glassGrad.addColorStop(0.5, 'rgba(255, 255, 255, 0.2)');
        glassGrad.addColorStop(1, 'rgba(255, 255, 255, 0.1)');
        ctx.fillStyle = glassGrad;
        ctx.fillRect(meterX, meterY, meterWidth * 0.4, meterHeight);
        
        // Border with 3D effect
        ctx.strokeStyle = 'rgba(100, 100, 100, 0.8)';
        ctx.lineWidth = 2;
        ctx.strokeRect(meterX, meterY, meterWidth, meterHeight);
        
        // Inner border highlight
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
        ctx.lineWidth = 1;
        ctx.strokeRect(meterX + 1, meterY + 1, meterWidth - 2, meterHeight - 2);
        
        // Power percentage with shadow
        ctx.shadowColor = 'black';
        ctx.shadowBlur = 4;
        ctx.fillStyle = 'white';
        ctx.font = 'bold 14px Arial';
        ctx.textAlign = 'center';
        ctx.fillText(Math.round(powerPercent * 100) + '%', meterX + meterWidth / 2, meterY - 10);
        ctx.shadowBlur = 0;
        
        // Label
        ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
        ctx.font = 'bold 11px Arial';
        ctx.fillText('POWER', meterX + meterWidth / 2, meterY + meterHeight + 18);
    }
};
";
    }
}
