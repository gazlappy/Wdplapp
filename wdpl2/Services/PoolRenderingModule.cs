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
     * Draw the pool table with realistic felt texture and gradient lighting
     * PHASE 3: Felt wear patterns + enhanced atmospheric lighting
     */
    drawTable(ctx, width, height, cushionMargin) {
        // ===== TABLE FRAME (WOODEN APRON) =====
        // Visible wooden border around the entire table
        const frameWidth = 12;
        const frameGradient = ctx.createLinearGradient(0, 0, frameWidth, 0);
        frameGradient.addColorStop(0, '#4a3520');
        frameGradient.addColorStop(0.5, '#5c4530');
        frameGradient.addColorStop(1, '#3a2817');
        
        ctx.fillStyle = frameGradient;
        ctx.fillRect(0, 0, width, height);
        
        // Frame inner shadow
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.5)';
        ctx.lineWidth = 3;
        ctx.strokeRect(frameWidth / 2, frameWidth / 2, width - frameWidth, height - frameWidth);
        
        // ===== REALISTIC FELT WITH RADIAL GRADIENT LIGHTING =====
        // Simulates overhead pool table lamp - light center, darker edges
        const feltInset = frameWidth;
        const feltWidth = width - (feltInset * 2);
        const feltHeight = height - (feltInset * 2);
        const centerX = width / 2;
        const centerY = height / 2;
        const radius = Math.max(feltWidth, feltHeight) * 0.7;
        
        const feltGradient = ctx.createRadialGradient(centerX, centerY, 0, centerX, centerY, radius);
        feltGradient.addColorStop(0, '#1B8B3D');    // Lighter center (tournament green)
        feltGradient.addColorStop(0.4, '#178535');   // Medium
        feltGradient.addColorStop(0.7, '#137A2E');   // Darker
        feltGradient.addColorStop(1, '#0F6426');     // Darkest edges
        
        ctx.fillStyle = feltGradient;
        ctx.fillRect(feltInset, feltInset, feltWidth, feltHeight);
        
        // ===== PHASE 3: FELT WEAR PATTERNS =====
        // Subtle wear in high-traffic areas (break box, rack area)
        this.drawFeltWear(ctx, width, height, feltInset);
        
        // ===== ENHANCED FELT TEXTURE (CLOTH WEAVE) =====
        ctx.save();
        ctx.globalAlpha = 0.05;
        // Vertical threads
        for (let i = 0; i < 70; i++) {
            const x = feltInset + (Math.random() * feltWidth);
            ctx.strokeStyle = i % 3 === 0 ? '#0a4a23' : (i % 3 === 1 ? '#1a8a43' : '#157A35');
            ctx.lineWidth = 0.5;
            ctx.beginPath();
            ctx.moveTo(x, feltInset);
            ctx.lineTo(x + (Math.random() - 0.5) * 10, feltInset + feltHeight);
            ctx.stroke();
        }
        // Horizontal threads
        for (let i = 0; i < 50; i++) {
            const y = feltInset + (Math.random() * feltHeight);
            ctx.strokeStyle = i % 3 === 0 ? '#0a4a23' : (i % 3 === 1 ? '#1a8a43' : '#157A35');
            ctx.lineWidth = 0.5;
            ctx.beginPath();
            ctx.moveTo(feltInset, y);
            ctx.lineTo(feltInset + feltWidth, y + (Math.random() - 0.5) * 10);
            ctx.stroke();
        }
        ctx.restore();
        
        // ===== 3D BEVELED WOODEN RAILS WITH ENHANCED GRAIN =====
        const cushionWidth = cushionMargin;
        
        // RAIL TOP EDGE (lightest - wood finish highlight)
        ctx.strokeStyle = '#C4A571';  // Light oak highlight
        ctx.lineWidth = cushionWidth * 0.25;
        ctx.strokeRect(
            cushionWidth * 0.125, 
            cushionWidth * 0.125, 
            width - cushionWidth * 0.25, 
            height - cushionWidth * 0.25
        );
        
        // ENHANCED WOOD GRAIN TEXTURE
        this.drawWoodGrainRails(ctx, width, height, cushionWidth);
        
        // RAIL MAIN BODY (medium - wood grain)
        const woodGradient = ctx.createLinearGradient(0, 0, cushionWidth, 0);
        woodGradient.addColorStop(0, '#8B6F47');     // Medium brown
        woodGradient.addColorStop(0.3, '#A0826D');   // Lighter
        woodGradient.addColorStop(0.7, '#704E2E');   // Darker
        woodGradient.addColorStop(1, '#5C3D2E');     // Darkest
        
        ctx.strokeStyle = woodGradient;
        ctx.lineWidth = cushionWidth * 0.6;
        ctx.strokeRect(
            cushionWidth * 0.3, 
            cushionWidth * 0.3, 
            width - cushionWidth * 0.6, 
            height - cushionWidth * 0.6
        );
        
        // RAIL BOTTOM EDGE (darkest - shadow)
        ctx.strokeStyle = '#3D2817';  // Deep brown shadow
        ctx.lineWidth = cushionWidth * 0.15;
        ctx.strokeRect(
            cushionWidth * 0.925, 
            cushionWidth * 0.925, 
            width - cushionWidth * 1.85, 
            height - cushionWidth * 1.85
        );
        
        // GREEN RUBBER CUSHION STRIP (the actual playing surface edge)
        ctx.strokeStyle = '#0F6426';  // Dark green rubber
        ctx.lineWidth = 4;
        ctx.strokeRect(
            cushionWidth - 2, 
            cushionWidth - 2, 
            width - cushionWidth * 2 + 4, 
            height - cushionWidth * 2 + 4
        );
        
        // Rubber cushion highlight (shiny rubber)
        ctx.strokeStyle = 'rgba(50, 150, 70, 0.4)';
        ctx.lineWidth = 2;
        ctx.strokeRect(
            cushionWidth - 1, 
            cushionWidth - 1, 
            width - cushionWidth * 2 + 2, 
            height - cushionWidth * 2 + 2
        );
        
        // Inner cushion shadow (depth)
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.5)';
        ctx.lineWidth = 3;
        ctx.strokeRect(
            cushionWidth + 1, 
            cushionWidth + 1, 
            width - cushionWidth * 2 - 2, 
            height - cushionWidth * 2 - 2
        );
        
        // ===== DIAMOND SIGHT MARKERS =====
        // These are the aiming diamonds on professional tables
        this.drawDiamondSights(ctx, width, height, cushionMargin);
        
        // ===== TABLE MARKINGS =====
        // Center line with fade effect
        const lineGrad = ctx.createLinearGradient(0, 0, 0, height);
        lineGrad.addColorStop(0, 'rgba(255, 255, 255, 0)');
        lineGrad.addColorStop(0.1, 'rgba(255, 255, 255, 0.12)');
        lineGrad.addColorStop(0.5, 'rgba(255, 255, 255, 0.15)');
        lineGrad.addColorStop(0.9, 'rgba(255, 255, 255, 0.12)');
        lineGrad.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.strokeStyle = lineGrad;
        ctx.lineWidth = 2;
        ctx.setLineDash([12, 8]);
        ctx.beginPath();
        ctx.moveTo(width / 2, cushionMargin);
        ctx.lineTo(width / 2, height - cushionMargin);
        ctx.stroke();
        ctx.setLineDash([]);
        
        // Head string (break line) at 1/4
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.08)';
        ctx.lineWidth = 1.5;
        ctx.setLineDash([6, 6]);
        ctx.beginPath();
        ctx.moveTo(width * 0.25, cushionMargin + 5);
        ctx.lineTo(width * 0.25, height - cushionMargin - 5);
        ctx.stroke();
        ctx.setLineDash([]);
        
        // Foot spot (where rack goes)
        const footSpotX = width * 0.75;
        const footSpotY = height / 2;
        ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
        ctx.beginPath();
        ctx.arc(footSpotX, footSpotY, 3, 0, Math.PI * 2);
        ctx.fill();
        
        // Head spot (where cue ball breaks from)
        const headSpotX = width * 0.25;
        const headSpotY = height / 2;
        ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
        ctx.beginPath();
        ctx.arc(headSpotX, headSpotY, 3, 0, Math.PI * 2);
        ctx.fill();
        
        // ===== PHASE 3: TABLE MANUFACTURER LOGO =====
        this.drawTableLogo(ctx, width, height, cushionMargin);
    },
    
    /**
     * Draw subtle felt wear patterns in high-traffic areas
     * PHASE 3: Realistic table wear
     */
    drawFeltWear(ctx, width, height, inset) {
        ctx.save();
        ctx.globalAlpha = 0.03;
        
        // Break box area wear (head of table, left side)
        const breakBoxX = width * 0.25;
        const breakBoxY = height / 2;
        const breakBoxGrad = ctx.createRadialGradient(breakBoxX, breakBoxY, 0, breakBoxX, breakBoxY, 80);
        breakBoxGrad.addColorStop(0, 'rgba(0, 0, 0, 0.4)');
        breakBoxGrad.addColorStop(0.6, 'rgba(0, 0, 0, 0.2)');
        breakBoxGrad.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = breakBoxGrad;
        ctx.beginPath();
        ctx.arc(breakBoxX, breakBoxY, 80, 0, Math.PI * 2);
        ctx.fill();
        
        // Rack area wear (foot of table, right side)
        const rackX = width * 0.75;
        const rackY = height / 2;
        const rackGrad = ctx.createRadialGradient(rackX, rackY, 0, rackX, rackY, 100);
        rackGrad.addColorStop(0, 'rgba(0, 0, 0, 0.5)');
        rackGrad.addColorStop(0.5, 'rgba(0, 0, 0, 0.3)');
        rackGrad.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = rackGrad;
        ctx.beginPath();
        ctx.arc(rackX, rackY, 100, 0, Math.PI * 2);
        ctx.fill();
        
        // Center area light wear (common shot paths)
        const centerGrad = ctx.createRadialGradient(width / 2, height / 2, 0, width / 2, height / 2, 150);
        centerGrad.addColorStop(0, 'rgba(0, 0, 0, 0.2)');
        centerGrad.addColorStop(0.7, 'rgba(0, 0, 0, 0.1)');
        centerGrad.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = centerGrad;
        ctx.beginPath();
        ctx.arc(width / 2, height / 2, 150, 0, Math.PI * 2);
        ctx.fill();
        
        ctx.restore();
    },
    
    /**
     * Draw subtle table manufacturer logo
     * PHASE 3: Professional branding
     */
    drawTableLogo(ctx, width, height, margin) {
        ctx.save();
        ctx.globalAlpha = 0.08;
        
        // Logo position (bottom right corner of felt)
        const logoX = width - margin - 60;
        const logoY = height - margin - 25;
        
        // Simple 8-ball logo text
        ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
        ctx.font = 'italic bold 14px Arial';
        ctx.textAlign = 'right';
        ctx.fillText('Championship', logoX, logoY);
        
        ctx.font = 'italic bold 10px Arial';
        ctx.fillText('Professional Series', logoX, logoY + 12);
        
        // Small 8-ball icon
        ctx.fillStyle = 'rgba(0, 0, 0, 0.4)';
        ctx.beginPath();
        ctx.arc(logoX - 70, logoY - 3, 8, 0, Math.PI * 2);
        ctx.fill();
        
        ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.beginPath();
        ctx.arc(logoX - 70, logoY - 3, 5, 0, Math.PI * 2);
        ctx.fill();
        
        ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
        ctx.font = 'bold 6px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('8', logoX - 70, logoY);
        
        ctx.restore();
    },
    
    /**
     * Draw realistic wood grain texture on rails
     * PHASE 2: Detailed wood grain pattern
     */
    drawWoodGrainRails(ctx, width, height, cushionWidth) {
        ctx.save();
        ctx.globalAlpha = 0.15;
        
        // Top rail grain
        for (let i = 0; i < 12; i++) {
            const x = (width / 12) * i;
            ctx.strokeStyle = i % 2 === 0 ? '#6d5436' : '#8B6F47';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.moveTo(x, 0);
            ctx.quadraticCurveTo(x + 20, cushionWidth / 2, x + 5, cushionWidth);
            ctx.stroke();
        }
        
        // Bottom rail grain
        for (let i = 0; i < 12; i++) {
            const x = (width / 12) * i;
            ctx.strokeStyle = i % 2 === 0 ? '#6d5436' : '#8B6F47';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.moveTo(x, height - cushionWidth);
            ctx.quadraticCurveTo(x + 20, height - cushionWidth / 2, x + 5, height);
            ctx.stroke();
        }
        
        // Left rail grain (vertical)
        for (let i = 0; i < 8; i++) {
            const y = (height / 8) * i;
            ctx.strokeStyle = i % 2 === 0 ? '#6d5436' : '#8B6F47';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.moveTo(0, y);
            ctx.quadraticCurveTo(cushionWidth / 2, y + 15, cushionWidth, y + 5);
            ctx.stroke();
        }
        
        // Right rail grain (vertical)
        for (let i = 0; i < 8; i++) {
            const y = (height / 8) * i;
            ctx.strokeStyle = i % 2 === 0 ? '#6d5436' : '#8B6F47';
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.moveTo(width - cushionWidth, y);
            ctx.quadraticCurveTo(width - cushionWidth / 2, y + 15, width, y + 5);
            ctx.stroke();
        }
        
        ctx.restore();
    },
    
    /**
     * Draw diamond sight markers on rails (professional aiming system)
     */
    drawDiamondSights(ctx, width, height, cushionMargin) {
        const diamondSize = 5;
        const diamondColor = 'rgba(255, 255, 255, 0.4)';
        
        // Top rail diamonds
        const topY = cushionMargin / 2;
        for (let i = 1; i <= 7; i++) {
            const x = (width / 8) * i;
            this.drawDiamond(ctx, x, topY, diamondSize, diamondColor);
        }
        
        // Bottom rail diamonds
        const bottomY = height - cushionMargin / 2;
        for (let i = 1; i <= 7; i++) {
            const x = (width / 8) * i;
            this.drawDiamond(ctx, x, bottomY, diamondSize, diamondColor);
        }
        
        // Left rail diamonds
        const leftX = cushionMargin / 2;
        for (let i = 1; i <= 3; i++) {
            const y = (height / 4) * i;
            this.drawDiamond(ctx, leftX, y, diamondSize, diamondColor);
        }
        
        // Right rail diamonds
        const rightX = width - cushionMargin / 2;
        for (let i = 1; i <= 3; i++) {
            const y = (height / 4) * i;
            this.drawDiamond(ctx, rightX, y, diamondSize, diamondColor);
        }
    },
    
    /**
     * Draw a single diamond sight marker
     */
    drawDiamond(ctx, x, y, size, color) {
        ctx.save();
        ctx.translate(x, y);
        ctx.rotate(Math.PI / 4);  // 45-degree rotation for diamond
        
        // Diamond with gradient
        const diamondGrad = ctx.createLinearGradient(-size, -size, size, size);
        diamondGrad.addColorStop(0, color);
        diamondGrad.addColorStop(0.5, color.replace('0.4', '0.6'));
        diamondGrad.addColorStop(1, color);
        
        ctx.fillStyle = diamondGrad;
        ctx.fillRect(-size, -size, size * 2, size * 2);
        
        // Diamond border
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.lineWidth = 0.5;
        ctx.strokeRect(-size, -size, size * 2, size * 2);
        
        ctx.restore();
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
     * PHASE 3: Felt color reflection + enhanced ambient lighting
     */
    drawBall(ctx, ball) {
        if (ball.potted) return;
        
        // ===== REALISTIC BALL SHADOW =====
        // Soft, elliptical shadow on table (not perfect circle due to angle)
        ctx.save();
        ctx.fillStyle = 'rgba(0, 0, 0, 0.35)';
        ctx.beginPath();
        ctx.ellipse(ball.x + 3, ball.y + 4, ball.r * 0.85, ball.r * 0.55, 0, 0, Math.PI * 2);
        ctx.fill();
        
        // Softer outer shadow (penumbra)
        ctx.fillStyle = 'rgba(0, 0, 0, 0.15)';
        ctx.beginPath();
        ctx.ellipse(ball.x + 3, ball.y + 4, ball.r * 1.1, ball.r * 0.7, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
        
        // ===== PHASE 3: FELT COLOR REFLECTION ON BALL BOTTOM =====
        // Green felt reflects onto the bottom of the ball
        const feltReflection = ctx.createRadialGradient(
            ball.x, ball.y + ball.r * 0.5, 0,
            ball.x, ball.y + ball.r * 0.5, ball.r * 0.7
        );
        feltReflection.addColorStop(0, 'rgba(27, 139, 61, 0.15)');  // Tournament green reflection
        feltReflection.addColorStop(0.5, 'rgba(27, 139, 61, 0.08)');
        feltReflection.addColorStop(1, 'rgba(27, 139, 61, 0)');
        
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        ctx.fillStyle = feltReflection;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y + ball.r * 0.5, ball.r * 0.7, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
        
        // ===== GLOSSY BALL WITH REALISTIC LIGHTING =====
        // Light from top-left (simulating overhead lamp)
        const lightOffsetX = -ball.r * 0.4;
        const lightOffsetY = -ball.r * 0.4;
        
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.15,
            ball.x, ball.y, ball.r * 1.15
        );
        
        // Set colors based on ball type with improved gradients
        if (ball.color === 'white') {
            grad.addColorStop(0, '#ffffff');
            grad.addColorStop(0.2, '#fafafa');
            grad.addColorStop(0.5, '#f0f0f0');
            grad.addColorStop(0.8, '#d0d0d0');
            grad.addColorStop(1, '#a0a0a0');
        } else if (ball.color === 'red') {
            grad.addColorStop(0, '#ff9999');
            grad.addColorStop(0.15, '#ff7777');
            grad.addColorStop(0.5, '#e63946');
            grad.addColorStop(0.8, '#c1121f');
            grad.addColorStop(1, '#780000');
        } else if (ball.color === 'yellow') {
            grad.addColorStop(0, '#fff9c4');
            grad.addColorStop(0.15, '#ffe066');
            grad.addColorStop(0.5, '#ffd43b');
            grad.addColorStop(0.8, '#fab005');
            grad.addColorStop(1, '#a67c00');
        } else { // black
            grad.addColorStop(0, '#555555');
            grad.addColorStop(0.2, '#3a3a3a');
            grad.addColorStop(0.5, '#2a2a2a');
            grad.addColorStop(0.8, '#1a1a1a');
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
            
            ctx.globalAlpha = 0.06;
            
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
        
        // ===== ENHANCED SPECULAR HIGHLIGHT (GLOSSY SHINE) =====
        // Two-layer highlight for more realistic shine
        // Primary highlight (intense)
        const specular1 = ctx.createRadialGradient(
            ball.x + lightOffsetX * 0.7, ball.y + lightOffsetY * 0.7, 0,
            ball.x + lightOffsetX * 0.7, ball.y + lightOffsetY * 0.7, ball.r * 0.35
        );
        specular1.addColorStop(0, 'rgba(255, 255, 255, 0.95)');
        specular1.addColorStop(0.2, 'rgba(255, 255, 255, 0.7)');
        specular1.addColorStop(0.5, 'rgba(255, 255, 255, 0.3)');
        specular1.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.fillStyle = specular1;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Secondary highlight (softer, larger)
        const specular2 = ctx.createRadialGradient(
            ball.x + lightOffsetX * 0.5, ball.y + lightOffsetY * 0.5, 0,
            ball.x + lightOffsetX * 0.5, ball.y + lightOffsetY * 0.5, ball.r * 0.6
        );
        specular2.addColorStop(0, 'rgba(255, 255, 255, 0.4)');
        specular2.addColorStop(0.3, 'rgba(255, 255, 255, 0.2)');
        specular2.addColorStop(0.7, 'rgba(255, 255, 255, 0.05)');
        specular2.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.fillStyle = specular2;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Ball outline for definition
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.25)';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.stroke();
        
        // ===== PHASE 3: ENHANCED AMBIENT OCCLUSION =====
        // Darker bottom edge with more realistic falloff
        const aoGradient = ctx.createRadialGradient(
            ball.x, ball.y + ball.r * 0.4, ball.r * 0.2,
            ball.x, ball.y + ball.r * 0.4, ball.r * 1.0
        );
        aoGradient.addColorStop(0, 'rgba(0, 0, 0, 0)');
        aoGradient.addColorStop(0.5, 'rgba(0, 0, 0, 0.08)');
        aoGradient.addColorStop(0.8, 'rgba(0, 0, 0, 0.15)');
        aoGradient.addColorStop(1, 'rgba(0, 0, 0, 0.25)');
        
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        ctx.fillStyle = aoGradient;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y + ball.r * 0.4, ball.r * 1.0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
        
        // Draw spin indicator if ball has active spin - ENHANCED VISIBILITY
        if ((ball.spinX !== undefined && Math.abs(ball.spinX) > 0.05) || 
            (ball.spinY !== undefined && Math.abs(ball.spinY) > 0.05)) {
            
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
            ctx.lineWidth = 4;
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
