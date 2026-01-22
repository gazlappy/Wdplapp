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
 * Helper to darken/lighten colors
 */
adjustColor(hex, percent) {
    const num = parseInt(hex.replace('#', ''), 16);
    const r = Math.min(255, Math.max(0, (num >> 16) + Math.round(255 * percent / 100)));
    const g = Math.min(255, Math.max(0, ((num >> 8) & 0x00FF) + Math.round(255 * percent / 100)));
    const b = Math.min(255, Math.max(0, (num & 0x0000FF) + Math.round(255 * percent / 100)));
    return '#' + (0x1000000 + r * 0x10000 + g * 0x100 + b).toString(16).slice(1);
},
    
/**
 * Convert hex to RGB object
 */
hexToRgb(hex) {
    const result = /^#?([a-f\d]{2})([a-f\d]{2})([a-f\d]{2})$/i.exec(hex);
    return result ? {
        r: parseInt(result[1], 16),
        g: parseInt(result[2], 16),
        b: parseInt(result[3], 16)
    } : null;
},
    
/**
 * Lighten a hex color
 */
lightenColor(hex, percent) {
    const rgb = this.hexToRgb(hex);
    if (!rgb) return hex;
    const r = Math.min(255, rgb.r + Math.round((255 - rgb.r) * percent / 100));
    const g = Math.min(255, rgb.g + Math.round((255 - rgb.g) * percent / 100));
    const b = Math.min(255, rgb.b + Math.round((255 - rgb.b) * percent / 100));
    return '#' + ((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1);
},
    
/**
 * Darken a hex color
 */
darkenColor(hex, percent) {
    const rgb = this.hexToRgb(hex);
    if (!rgb) return hex;
    const r = Math.max(0, Math.round(rgb.r * (100 - percent) / 100));
    const g = Math.max(0, Math.round(rgb.g * (100 - percent) / 100));
    const b = Math.max(0, Math.round(rgb.b * (100 - percent) / 100));
    return '#' + ((1 << 24) + (r << 16) + (g << 8) + b).toString(16).slice(1);
},
    
/**
 * Draw the pool table with realistic felt texture and gradient lighting
 * Supports custom colors from game settings
 */
drawTable(ctx, width, height, cushionMargin, game) {
    // Get custom colors from game settings (or use defaults)
    const clothColor = (game && game.clothColor) || '#1a7f37';
    const railColor = (game && game.railColor) || '#8B4513';
        
    // ===== TABLE FRAME (WOODEN APRON) =====
    const frameWidth = 12;
    const frameGradient = ctx.createLinearGradient(0, 0, frameWidth, 0);
    frameGradient.addColorStop(0, this.adjustColor(railColor, -20));
    frameGradient.addColorStop(0.5, railColor);
    frameGradient.addColorStop(1, this.adjustColor(railColor, -30));
        
    ctx.fillStyle = frameGradient;
    ctx.fillRect(0, 0, width, height);
        
    // Frame inner shadow
    ctx.strokeStyle = 'rgba(0, 0, 0, 0.5)';
    ctx.lineWidth = 3;
    ctx.strokeRect(frameWidth / 2, frameWidth / 2, width - frameWidth, height - frameWidth);
        
    // ===== AMBIENT ROOM LIGHTING EFFECT =====
    const vignetteGrad = ctx.createRadialGradient(
        width / 2, height / 2, Math.min(width, height) * 0.3,
        width / 2, height / 2, Math.max(width, height) * 0.8
    );
    vignetteGrad.addColorStop(0, 'rgba(0, 0, 0, 0)');
    vignetteGrad.addColorStop(0.7, 'rgba(0, 0, 0, 0.02)');
    vignetteGrad.addColorStop(1, 'rgba(0, 0, 0, 0.12)');
        
    ctx.fillStyle = vignetteGrad;
    ctx.fillRect(0, 0, width, height);
        
    // ===== REALISTIC FELT WITH RADIAL GRADIENT LIGHTING =====
    const feltInset = frameWidth;
    const feltWidth = width - (feltInset * 2);
    const feltHeight = height - (feltInset * 2);
    const centerX = width / 2;
    const centerY = height / 2;
    const radius = Math.max(feltWidth, feltHeight) * 0.7;
        
    const feltGradient = ctx.createRadialGradient(centerX, centerY, 0, centerX, centerY, radius);
    feltGradient.addColorStop(0, this.adjustColor(clothColor, 15));    // Lighter center
    feltGradient.addColorStop(0.4, this.adjustColor(clothColor, 5));   // Medium
    feltGradient.addColorStop(0.7, clothColor);                         // Base color
    feltGradient.addColorStop(1, this.adjustColor(clothColor, -15));   // Darker edges
        
    ctx.fillStyle = feltGradient;
    ctx.fillRect(feltInset, feltInset, feltWidth, feltHeight);
        
    // ===== PHASE 3: FELT WEAR PATTERNS =====
    this.drawFeltWear(ctx, width, height, feltInset);
        
    // ===== ENHANCED FELT TEXTURE WITH CUSTOM COLOR =====
    ctx.save();
    ctx.globalAlpha = 0.05;
        
    const napAngle = 0;
        
        // Vertical threads with nap direction
        for (let i = 0; i < 100; i++) {
            const x = feltInset + (feltWidth / 100) * i;
            const variation = (Math.sin(i * 0.5) * 2);
            ctx.strokeStyle = i % 3 === 0 ? '#0a4a23' : (i % 3 === 1 ? '#1a8a43' : '#157A35');
            ctx.lineWidth = 0.6;
            ctx.beginPath();
            ctx.moveTo(x, feltInset);
            ctx.quadraticCurveTo(
                x + variation + Math.cos(napAngle) * 3, 
                feltInset + feltHeight / 2, 
                x + variation * 2, 
                feltInset + feltHeight
            );
            ctx.stroke();
        }
        
        // Horizontal threads with nap direction (denser)
        for (let i = 0; i < 80; i++) {
            const y = feltInset + (feltHeight / 80) * i;
            const variation = (Math.cos(i * 0.7) * 2);
            ctx.strokeStyle = i % 3 === 0 ? '#0a4a23' : (i % 3 === 1 ? '#1a8a43' : '#157A35');
            ctx.lineWidth = 0.6;
            ctx.beginPath();
            ctx.moveTo(feltInset, y);
            ctx.quadraticCurveTo(
                feltInset + feltWidth / 2, 
                y + variation + Math.sin(napAngle) * 3, 
                feltInset + feltWidth, 
                y + variation * 2
            );
            ctx.stroke();
        }
        
        // Add subtle cross-hatch for depth
        ctx.globalAlpha = 0.02;
        for (let i = 0; i < 30; i++) {
            const x = feltInset + Math.random() * feltWidth;
            const y = feltInset + Math.random() * feltHeight;
            const size = 10 + Math.random() * 20;
            
            ctx.strokeStyle = '#0F6426';
            ctx.lineWidth = 0.8;
            ctx.beginPath();
            ctx.moveTo(x - size, y - size);
            ctx.lineTo(x + size, y + size);
            ctx.moveTo(x + size, y - size);
            ctx.lineTo(x - size, y + size);
            ctx.stroke();
        }
        
        ctx.restore();
        
        // ===== SIMPLE CUSHIONS AND POCKETS =====
        this.drawSimpleCushions(ctx, width, height, cushionMargin);
        
        // ===== RAIL BOLTS / SCREWS (PROFESSIONAL DETAIL) =====
        this.drawRailBolts(ctx, width, height, cushionMargin);
        
        // ===== DIAMOND SIGHT MARKERS =====
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
     * Draw UK-style pool table with proper pocket cutouts
     * Based on reference image - cream rails, black pockets, green cushions
     */
    drawSimpleCushions(ctx, width, height, cushionMargin) {
        const railWidth = cushionMargin;
        
        // Pocket hole sizes (the black circles)
        const cornerPocketR = railWidth * 0.9;
        const sidePocketR = railWidth * 0.75;
        
        // Pocket OPENING sizes (the gap in the cushions) - use game settings if available
        // These control how wide the opening is in the rails/cushions
        const cornerOpeningMult = (typeof game !== 'undefined' && game.cornerPocketOpeningMult) ? game.cornerPocketOpeningMult : 1.6;
        const sideOpeningMult = (typeof game !== 'undefined' && game.sidePocketOpeningMult) ? game.sidePocketOpeningMult : 1.3;
        
        const cornerPocketOpening = railWidth * cornerOpeningMult;
        const sidePocketOpening = railWidth * sideOpeningMult;
        
        // Colors - use custom colors from game settings if available
        const customRailColor = (typeof game !== 'undefined' && game.railColor) ? game.railColor : '#C4B998';
        const customClothColor = (typeof game !== 'undefined' && game.clothColor) ? game.clothColor : '#1B7A3A';
        
        const railColor = customRailColor;
        const railLight = this.adjustColor(customRailColor, 15);
        const railDark = this.adjustColor(customRailColor, -15);
        const railEdge = this.adjustColor(customRailColor, -25);
        const cushionColor = this.adjustColor(customClothColor, -5);
        const cushionLight = this.adjustColor(customClothColor, 10);
        const pocketColor = '#000000';      // Black pocket holes
        
        // Corner pocket positions (at actual corners)
        const corners = [
            { x: railWidth * 0.7, y: railWidth * 0.7 },                    // Top-left
            { x: width - railWidth * 0.7, y: railWidth * 0.7 },            // Top-right
            { x: railWidth * 0.7, y: height - railWidth * 0.7 },           // Bottom-left
            { x: width - railWidth * 0.7, y: height - railWidth * 0.7 }    // Bottom-right
        ];
        
        // Side pocket positions
        const sides = [
            { x: width / 2, y: railWidth * 0.4 },                          // Top-middle
            { x: width / 2, y: height - railWidth * 0.4 }                  // Bottom-middle
        ];
        
        
        ctx.save();
        
        // ========== 1. DRAW OUTER RAIL FRAME ==========
        // This is the cream/tan wooden frame around the table
        // Use pocket OPENING size for rail gaps (not hole size)
        
        // Top rail
        ctx.fillStyle = railColor;
        ctx.beginPath();
        ctx.moveTo(corners[0].x + cornerPocketOpening, 0);
        ctx.lineTo(sides[0].x - sidePocketOpening, 0);
        ctx.lineTo(sides[0].x - sidePocketOpening, railWidth);
        ctx.lineTo(corners[0].x + cornerPocketOpening, railWidth);
        ctx.closePath();
        ctx.fill();
        
        ctx.beginPath();
        ctx.moveTo(sides[0].x + sidePocketOpening, 0);
        ctx.lineTo(corners[1].x - cornerPocketOpening, 0);
        ctx.lineTo(corners[1].x - cornerPocketOpening, railWidth);
        ctx.lineTo(sides[0].x + sidePocketOpening, railWidth);
        ctx.closePath();
        ctx.fill();
        
        // Bottom rail
        ctx.beginPath();
        ctx.moveTo(corners[2].x + cornerPocketOpening, height);
        ctx.lineTo(sides[1].x - sidePocketOpening, height);
        ctx.lineTo(sides[1].x - sidePocketOpening, height - railWidth);
        ctx.lineTo(corners[2].x + cornerPocketOpening, height - railWidth);
        ctx.closePath();
        ctx.fill();
        
        ctx.beginPath();
        ctx.moveTo(sides[1].x + sidePocketOpening, height);
        ctx.lineTo(corners[3].x - cornerPocketOpening, height);
        ctx.lineTo(corners[3].x - cornerPocketOpening, height - railWidth);
        ctx.lineTo(sides[1].x + sidePocketOpening, height - railWidth);
        ctx.closePath();
        ctx.fill();
        
        // Left rail
        ctx.beginPath();
        ctx.moveTo(0, corners[0].y + cornerPocketOpening);
        ctx.lineTo(0, corners[2].y - cornerPocketOpening);
        ctx.lineTo(railWidth, corners[2].y - cornerPocketOpening);
        ctx.lineTo(railWidth, corners[0].y + cornerPocketOpening);
        ctx.closePath();
        ctx.fill();
        
        // Right rail
        ctx.beginPath();
        ctx.moveTo(width, corners[1].y + cornerPocketOpening);
        ctx.lineTo(width, corners[3].y - cornerPocketOpening);
        ctx.lineTo(width - railWidth, corners[3].y - cornerPocketOpening);
        ctx.lineTo(width - railWidth, corners[1].y + cornerPocketOpening);
        ctx.closePath();
        ctx.fill();
        
        // ========== 2. RAIL 3D EFFECTS ==========
        // Top edge highlights
        ctx.strokeStyle = railLight;
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.moveTo(corners[0].x + cornerPocketOpening, 2);
        ctx.lineTo(sides[0].x - sidePocketOpening, 2);
        ctx.moveTo(sides[0].x + sidePocketOpening, 2);
        ctx.lineTo(corners[1].x - cornerPocketOpening, 2);
        ctx.stroke();
        
        // Inner edge shadows
        ctx.strokeStyle = railDark;
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.moveTo(corners[0].x + cornerPocketOpening, railWidth - 1);
        ctx.lineTo(sides[0].x - sidePocketOpening, railWidth - 1);
        ctx.moveTo(sides[0].x + sidePocketOpening, railWidth - 1);
        ctx.lineTo(corners[1].x - cornerPocketOpening, railWidth - 1);
        ctx.stroke();
        
        // Bottom rail inner edge
        ctx.beginPath();
        ctx.moveTo(corners[2].x + cornerPocketOpening, height - railWidth + 1);
        ctx.lineTo(sides[1].x - sidePocketOpening, height - railWidth + 1);
        ctx.moveTo(sides[1].x + sidePocketOpening, height - railWidth + 1);
        ctx.lineTo(corners[3].x - cornerPocketOpening, height - railWidth + 1);
        ctx.stroke();
        
        // ========== 3. DRAW POCKET HOLES ==========
        // Black circles at corner and side positions (use hole size, not opening size)
        
        
        // Corner pockets - with slight shadow/depth
        corners.forEach(p => {
            // Outer shadow
            ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
            ctx.beginPath();
            ctx.arc(p.x, p.y, cornerPocketR * 1.15, 0, Math.PI * 2);
            ctx.fill();
            
            // Main black hole
            ctx.fillStyle = pocketColor;
            ctx.beginPath();
            ctx.arc(p.x, p.y, cornerPocketR, 0, Math.PI * 2);
            ctx.fill();
            
            // Inner depth
            ctx.fillStyle = '#1a1a1a';
            ctx.beginPath();
            ctx.arc(p.x, p.y, cornerPocketR * 0.7, 0, Math.PI * 2);
            ctx.fill();
        });
        
        // Side pockets
        sides.forEach(p => {
            // Outer shadow
            ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
            ctx.beginPath();
            ctx.arc(p.x, p.y, sidePocketR * 1.15, 0, Math.PI * 2);
            ctx.fill();
            
            // Main black hole
            ctx.fillStyle = pocketColor;
            ctx.beginPath();
            ctx.arc(p.x, p.y, sidePocketR, 0, Math.PI * 2);
            ctx.fill();
            
            // Inner depth
            ctx.fillStyle = '#1a1a1a';
            ctx.beginPath();
            ctx.arc(p.x, p.y, sidePocketR * 0.7, 0, Math.PI * 2);
            ctx.fill();
        });
        
        
        // ========== 4. DRAW GREEN CUSHIONS ==========
        // These are the rubber bumpers that balls bounce off
        // They run along the inner edge of the rail and STOP at each pocket opening
        
        const cushionWidth = 6;
        const cushionInset = railWidth - cushionWidth / 2 - 2;
        
        ctx.strokeStyle = cushionColor;
        ctx.lineWidth = cushionWidth;
        ctx.lineCap = 'round';
        
        // Top cushions (2 segments with gap for side pocket)
        ctx.beginPath();
        ctx.moveTo(corners[0].x + cornerPocketOpening + 3, cushionInset);
        ctx.lineTo(sides[0].x - sidePocketOpening - 3, cushionInset);
        ctx.stroke();
        
        ctx.beginPath();
        ctx.moveTo(sides[0].x + sidePocketOpening + 3, cushionInset);
        ctx.lineTo(corners[1].x - cornerPocketOpening - 3, cushionInset);
        ctx.stroke();
        
        // Bottom cushions
        ctx.beginPath();
        ctx.moveTo(corners[2].x + cornerPocketOpening + 3, height - cushionInset);
        ctx.lineTo(sides[1].x - sidePocketOpening - 3, height - cushionInset);
        ctx.stroke();
        
        ctx.beginPath();
        ctx.moveTo(sides[1].x + sidePocketOpening + 3, height - cushionInset);
        ctx.lineTo(corners[3].x - cornerPocketOpening - 3, height - cushionInset);
        ctx.stroke();
        
        // Left cushion
        ctx.beginPath();
        ctx.moveTo(cushionInset, corners[0].y + cornerPocketOpening + 3);
        ctx.lineTo(cushionInset, corners[2].y - cornerPocketOpening - 3);
        ctx.stroke();
        
        // Right cushion
        ctx.beginPath();
        ctx.moveTo(width - cushionInset, corners[1].y + cornerPocketOpening + 3);
        ctx.lineTo(width - cushionInset, corners[3].y - cornerPocketOpening - 3);
        ctx.stroke();
        
        // Cushion highlights (lighter green on top edge)
        ctx.strokeStyle = cushionLight;
        ctx.lineWidth = 2;
        
        // Top cushion highlights
        ctx.beginPath();
        ctx.moveTo(corners[0].x + cornerPocketOpening + 3, cushionInset - 2);
        ctx.lineTo(sides[0].x - sidePocketOpening - 3, cushionInset - 2);
        ctx.moveTo(sides[0].x + sidePocketOpening + 3, cushionInset - 2);
        ctx.lineTo(corners[1].x - cornerPocketOpening - 3, cushionInset - 2);
        ctx.stroke();
        
        ctx.restore();
    },
    
    /**
     * Draw pockets - simple circles for physics zones
     */
    drawPockets(ctx, pockets, game) {
        // Draw pocket debug zones if enabled
        if (game && game.showPocketZones) {
            pockets.forEach(p => {
                ctx.strokeStyle = 'rgba(100, 255, 100, 0.4)';
                ctx.lineWidth = 2;
                ctx.setLineDash([8, 4]);
                ctx.beginPath();
                ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
                ctx.stroke();
                ctx.setLineDash([]);
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
        ctx.save();
        ctx.fillStyle = 'rgba(0, 0, 0, 0.35)';
        ctx.beginPath();
        ctx.ellipse(ball.x + 3, ball.y + 4, ball.r * 0.85, ball.r * 0.55, 0, 0, Math.PI * 2);
        ctx.fill();
        
        ctx.fillStyle = 'rgba(0, 0, 0, 0.15)';
        ctx.beginPath();
        ctx.ellipse(ball.x + 3, ball.y + 4, ball.r * 1.1, ball.r * 0.7, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
        
        // ===== FELT COLOR REFLECTION ON BALL BOTTOM =====
        const feltReflection = ctx.createRadialGradient(
            ball.x, ball.y + ball.r * 0.5, 0,
            ball.x, ball.y + ball.r * 0.5, ball.r * 0.7
        );
        feltReflection.addColorStop(0, 'rgba(27, 139, 61, 0.15)');
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
        
        // Light position for gradients
        const lightOffsetX = -ball.r * 0.4;
        const lightOffsetY = -ball.r * 0.4;
        
        // ===== UK-STYLE BALL RENDERING =====
        if (ball.color === 'red') {
            // UK RED BALLS: Maroon with cream stripe band around middle
            this.drawUKRedBall(ctx, ball, lightOffsetX, lightOffsetY);
        } else if (ball.color === 'yellow') {
            // UK YELLOW BALLS: Solid bright yellow
            this.drawUKYellowBall(ctx, ball, lightOffsetX, lightOffsetY);
        } else if (ball.color === 'black' || ball.num === 8) {
            // BLACK BALL: Solid black with gloss
            this.drawBlackBall(ctx, ball, lightOffsetX, lightOffsetY);
        } else if (ball.color === 'white') {
            // CUE BALL: White/cream
            this.drawCueBall(ctx, ball, lightOffsetX, lightOffsetY);
        } else {
            // Fallback
            this.drawGenericBall(ctx, ball, lightOffsetX, lightOffsetY);
        }
        
        // ===== DRAW NUMBER CIRCLE =====
        this.drawBallNumber(ctx, ball);
        
        // ===== SPECULAR HIGHLIGHTS =====
        this.drawSpecularHighlights(ctx, ball, lightOffsetX, lightOffsetY);
    },
    
    // Red Ball - American style with cream polar caps and maroon center band
    drawUKRedBall(ctx, ball, lightOffsetX, lightOffsetY) {
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        
        // Get stripe offset from rotation module (uses pole position tracking)
        const stripeOffset = typeof PoolBallRotation !== 'undefined' 
            ? PoolBallRotation.getStripeOffset(ball) 
            : 0;
        
        // Base cream/ivory color (the polar caps)
        const baseGrad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.1,
            ball.x, ball.y, ball.r * 1.1
        );
        baseGrad.addColorStop(0, '#fffef8');
        baseGrad.addColorStop(0.2, '#faf5e8');
        baseGrad.addColorStop(0.5, '#f0e8d8');
        baseGrad.addColorStop(0.8, '#d8d0c0');
        baseGrad.addColorStop(1, '#b8b0a0');
        
        ctx.fillStyle = baseGrad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Draw the maroon center band - uses proper 3D rotation tracking
        this.drawRedBallMaroonBand(ctx, ball, lightOffsetX, lightOffsetY, stripeOffset);
        
        ctx.restore();
    },
    
    // Draw the maroon band - fixed width horizontal stripe that rolls with the ball
    drawRedBallMaroonBand(ctx, ball, lightOffsetX, lightOffsetY, stripeOffset) {
        ctx.save();
        
        // Calculate band position from rotation module's stripe offset
        // stripeOffset is -1 to 1, representing how far the pole has tilted
        const bandOffset = stripeOffset * ball.r * 0.85;
        const bandCenterY = ball.y + bandOffset;
        
        // Create maroon gradient centered on the band
        const maroonGrad = ctx.createRadialGradient(
            ball.x + lightOffsetX * 0.5, bandCenterY + lightOffsetY * 0.5, ball.r * 0.1,
            ball.x, bandCenterY, ball.r * 1.1
        );
        maroonGrad.addColorStop(0, '#a04555');
        maroonGrad.addColorStop(0.15, '#8b2538');
        maroonGrad.addColorStop(0.4, '#6d1a2d');
        maroonGrad.addColorStop(0.7, '#5a1525');
        maroonGrad.addColorStop(1, '#3a0815');
        
        // Draw band as a fixed width horizontal stripe - same width all the way around
        // Band height is 60% of ball diameter (30% above and below center)
        const bandHalfHeight = ball.r * 0.60;
        
        ctx.fillStyle = maroonGrad;
        ctx.beginPath();
        ctx.rect(ball.x - ball.r, bandCenterY - bandHalfHeight, ball.r * 2, bandHalfHeight * 2);
        ctx.fill();
        
        // Add subtle shading to the band for 3D effect
        const bandShadow = ctx.createLinearGradient(
            ball.x, bandCenterY - bandHalfHeight,
            ball.x, bandCenterY + bandHalfHeight
        );
        bandShadow.addColorStop(0, 'rgba(255,200,200,0.15)');
        bandShadow.addColorStop(0.3, 'rgba(0,0,0,0)');
        bandShadow.addColorStop(0.7, 'rgba(0,0,0,0.1)');
        bandShadow.addColorStop(1, 'rgba(0,0,0,0.2)');
        
        ctx.fillStyle = bandShadow;
        ctx.beginPath();
        ctx.rect(ball.x - ball.r, bandCenterY - bandHalfHeight, ball.r * 2, bandHalfHeight * 2);
        ctx.fill();
        
        ctx.restore();
    },
    
    
    
    
    
    
    
    
    
    
    // UK Yellow Ball - Solid bright yellow
    drawUKYellowBall(ctx, ball, lightOffsetX, lightOffsetY) {
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.1,
            ball.x, ball.y, ball.r * 1.1
        );
        grad.addColorStop(0, '#ffed80');
        grad.addColorStop(0.15, '#ffe033');
        grad.addColorStop(0.4, '#ffd700');
        grad.addColorStop(0.7, '#e6b800');
        grad.addColorStop(1, '#997a00');
        
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
    },
    
    // Black Ball
    drawBlackBall(ctx, ball, lightOffsetX, lightOffsetY) {
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.1,
            ball.x, ball.y, ball.r * 1.1
        );
        grad.addColorStop(0, '#555555');
        grad.addColorStop(0.2, '#3a3a3a');
        grad.addColorStop(0.5, '#2a2a2a');
        grad.addColorStop(0.8, '#1a1a1a');
        grad.addColorStop(1, '#000000');
        
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
    },
    
    // Cue Ball - White/cream with dark spots for spin tracking
    drawCueBall(ctx, ball, lightOffsetX, lightOffsetY) {
        // Base white/cream gradient
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.1,
            ball.x, ball.y, ball.r * 1.1
        );
        grad.addColorStop(0, '#ffffff');
        grad.addColorStop(0.2, '#fefefa');
        grad.addColorStop(0.5, '#f8f5f0');
        grad.addColorStop(0.8, '#e8e4dc');
        grad.addColorStop(1, '#c8c4bc');
        
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Draw dark spots for spin tracking (like real cue balls)
        this.drawCueBallSpots(ctx, ball);
    },
    
    // Draw the characteristic dark spots on a cue ball
    drawCueBallSpots(ctx, ball) {
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        
        // Get the 3D rotation state (default to facing viewer)
        const numX = ball.numPosX !== undefined ? ball.numPosX : 0;
        const numY = ball.numPosY !== undefined ? ball.numPosY : 0;
        const numZ = ball.numPosZ !== undefined ? ball.numPosZ : 1;
        
        // Define 6 spots positioned on a sphere (like dice positions)
        // These are unit vectors pointing to spot positions
        const spotPositions = [
            { x: 0, y: 0, z: 1 },    // Front
            { x: 0, y: 0, z: -1 },   // Back
            { x: 1, y: 0, z: 0 },    // Right
            { x: -1, y: 0, z: 0 },   // Left
            { x: 0, y: 1, z: 0 },    // Bottom
            { x: 0, y: -1, z: 0 }    // Top
        ];
        
        // Get rotation matrix from the tracked position
        // We use the numPos as the Z-axis of our rotation
        const zx = numX, zy = numY, zz = numZ;
        
        // Create orthonormal basis (simplified rotation)
        let yx, yy, yz;
        if (Math.abs(zz) > 0.9) {
            // Z is pointing mostly up/down, use X as reference
            yx = 0; yy = 1; yz = 0;
        } else {
            // Cross product with up vector to get Y
            const upX = 0, upY = 0, upZ = 1;
            yx = zy * upZ - zz * upY;
            yy = zz * upX - zx * upZ;
            yz = zx * upY - zy * upX;
            const yLen = Math.sqrt(yx*yx + yy*yy + yz*yz);
            if (yLen > 0.001) { yx /= yLen; yy /= yLen; yz /= yLen; }
        }
        
        // X axis = Y cross Z
        const xx = yy * zz - yz * zy;
        const xy = yz * zx - yx * zz;
        const xz = yx * zy - yy * zx;
        
        // Draw each spot
        const spotRadius = ball.r * 0.12;
        const depthFactor = 0.7;
        
        spotPositions.forEach((spot, index) => {
            // Transform spot position by rotation matrix
            const rotX = spot.x * xx + spot.y * yx + spot.z * zx;
            const rotY = spot.x * xy + spot.y * yy + spot.z * zy;
            const rotZ = spot.x * xz + spot.y * yz + spot.z * zz;
            
            // Only draw if spot is on visible hemisphere
            if (rotZ > -0.1) {
                const screenX = ball.x + rotX * ball.r * depthFactor;
                const screenY = ball.y + rotY * ball.r * depthFactor;
                
                // Perspective scaling and alpha based on depth
                const perspective = Math.max(0.3, (rotZ + 0.1) / 1.1);
                const scale = 0.6 + perspective * 0.4;
                const alpha = Math.max(0.1, Math.min(0.9, (rotZ + 0.2) / 0.8));
                
                // Draw bowtie/cross shaped spot
                ctx.save();
                ctx.globalAlpha = alpha;
                ctx.translate(screenX, screenY);
                ctx.scale(scale, scale);
                
                // Dark blue-gray color like real cue ball spots
                ctx.fillStyle = '#2a3a4a';
                
                // Draw bowtie shape (two triangles pointing at each other)
                const spotSize = spotRadius * 1.5;
                
                // First triangle (left half)
                ctx.beginPath();
                ctx.moveTo(-spotSize, -spotSize * 0.6);
                ctx.lineTo(0, 0);
                ctx.lineTo(-spotSize, spotSize * 0.6);
                ctx.closePath();
                ctx.fill();
                
                // Second triangle (right half)
                ctx.beginPath();
                ctx.moveTo(spotSize, -spotSize * 0.6);
                ctx.lineTo(0, 0);
                ctx.lineTo(spotSize, spotSize * 0.6);
                ctx.closePath();
                ctx.fill();
                
                // Small center dot
                ctx.beginPath();
                ctx.arc(0, 0, spotSize * 0.2, 0, Math.PI * 2);
                ctx.fill();
                
                ctx.restore();
            }
        });
        
        ctx.restore();
    },
    
    // Generic fallback ball
    drawGenericBall(ctx, ball, lightOffsetX, lightOffsetY) {
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.1,
            ball.x, ball.y, ball.r * 1.1
        );
        grad.addColorStop(0, '#cccccc');
        grad.addColorStop(0.5, '#999999');
        grad.addColorStop(1, '#666666');
        
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
    },
    
    
    // Draw ball number with cream circle and black ring (American style for reds)
    drawBallNumber(ctx, ball) {
        if (ball.num <= 0) return;
        
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        
        // Use rotation module for number position and visibility
        const useRotationModule = typeof PoolBallRotation !== 'undefined';
        
        let numberVisible, numberX, numberY, scale, alpha;
        
        if (useRotationModule) {
            // Get values from rotation module
            numberVisible = PoolBallRotation.isNumberVisible(ball);
            
            if (numberVisible) {
                const screenOffset = PoolBallRotation.getNumberScreenOffset(ball);
                const depthFactor = 0.65;
                
                numberX = ball.x + screenOffset.x * ball.r * depthFactor;
                numberY = ball.y + screenOffset.y * ball.r * depthFactor;
                
                scale = PoolBallRotation.getNumberScale(ball);
                alpha = PoolBallRotation.getNumberAlpha(ball);
            }
        } else {
            // Fallback - number always visible at center
            numberVisible = true;
            numberX = ball.x;
            numberY = ball.y;
            scale = 1;
            alpha = 1;
        }
        
        // Only draw if number is on visible side
        if (numberVisible) {
            ctx.globalAlpha = alpha;
            
            // American style: cream circle with black ring outline
            const circleRadius = ball.r * 0.42 * scale;
            const ringWidth = circleRadius * 0.18;
            
            // Outer black ring
            ctx.fillStyle = '#1a1a1a';
            ctx.beginPath();
            ctx.arc(numberX, numberY, circleRadius, 0, Math.PI * 2);
            ctx.fill();
            
            // Inner cream/ivory circle
            const innerGrad = ctx.createRadialGradient(
                numberX - 1, numberY - 1, 0,
                numberX, numberY, circleRadius - ringWidth
            );
            innerGrad.addColorStop(0, '#fffef8');
            innerGrad.addColorStop(0.3, '#faf8f0');
            innerGrad.addColorStop(0.7, '#f0ebe0');
            innerGrad.addColorStop(1, '#e8e0d5');
            
            ctx.fillStyle = innerGrad;
            ctx.beginPath();
            ctx.arc(numberX, numberY, circleRadius - ringWidth, 0, Math.PI * 2);
            ctx.fill();
            
            // Subtle highlight on the cream circle
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
            ctx.lineWidth = 0.5;
            ctx.stroke();
            
            // BLACK number text
            const fontSize = Math.round(10 * scale);
            
            ctx.fillStyle = '#1a1a1a';
            ctx.font = 'bold ' + fontSize + 'px Arial';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillText(ball.num, numberX, numberY + 0.5);
        }
        
        ctx.restore();
    },
    
    // Draw specular highlights for glossy shine
    drawSpecularHighlights(ctx, ball, lightOffsetX, lightOffsetY) {
        // Primary highlight
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
     * Get color string for a ball (used in trajectory prediction)
     */
    getBallColor(ball) {
        if (ball.color === 'red') return 'rgba(220, 38, 38, 0.8)';
        if (ball.color === 'yellow') return 'rgba(234, 179, 8, 0.8)';
        if (ball.color === 'black') return 'rgba(50, 50, 50, 0.8)';
        if (ball.color === 'white') return 'rgba(255, 255, 255, 0.8)';
        return 'rgba(200, 200, 200, 0.8)';
    },
    
    /**
     * Draw trajectory predictions for object balls
     * Shows where balls will go when hit by the cue ball
     */
    drawTrajectoryPredictions(ctx, cueBall, aimAngle, allBalls, tableWidth, tableHeight, cushionMargin, game) {
        // Find which ball will be hit first
        const hitResult = this.findFirstBallHit(cueBall, aimAngle, allBalls);
        
        if (!hitResult) return;
        
        const { ball: objectBall, collisionPoint, impactAngle } = hitResult;
        
        // Draw collision point indicator
        if (game.showCollisionPoints) {
            ctx.save();
            
            // Pulsing collision point
            const pulseSize = 3 + Math.sin(Date.now() / 200) * 2;
            
            // Outer glow
            const glowGrad = ctx.createRadialGradient(
                collisionPoint.x, collisionPoint.y, 0,
                collisionPoint.x, collisionPoint.y, 25
            );
            glowGrad.addColorStop(0, 'rgba(255, 215, 0, 0.6)');
            glowGrad.addColorStop(0.5, 'rgba(255, 215, 0, 0.3)');
            glowGrad.addColorStop(1, 'rgba(255, 215, 0, 0)');
            ctx.fillStyle = glowGrad;
            ctx.beginPath();
            ctx.arc(collisionPoint.x, collisionPoint.y, 25, 0, Math.PI * 2);
            ctx.fill();
            
            // Collision point cross
            ctx.strokeStyle = 'rgba(255, 215, 0, 0.9)';
            ctx.lineWidth = 3;
            ctx.beginPath();
            ctx.moveTo(collisionPoint.x - pulseSize * 3, collisionPoint.y);
            ctx.lineTo(collisionPoint.x + pulseSize * 3, collisionPoint.y);
            ctx.moveTo(collisionPoint.x, collisionPoint.y - pulseSize * 3);
            ctx.lineTo(collisionPoint.x, collisionPoint.y + pulseSize * 3);
            ctx.stroke();
            
            // Center dot
            ctx.fillStyle = 'rgba(255, 255, 255, 0.9)';
            ctx.beginPath();
            ctx.arc(collisionPoint.x, collisionPoint.y, pulseSize, 0, Math.PI * 2);
            ctx.fill();
            
            ctx.restore();
        }
        
        // Draw ghost ball at collision point
        if (game.showGhostBalls) {
            ctx.save();
            ctx.globalAlpha = 0.4;
            
            // Ghost ball for cue ball position at impact
            // Use the precise position calculated in findFirstBallHit
            const ghostCueBallX = collisionPoint.cueBallX || (collisionPoint.x - Math.cos(impactAngle) * cueBall.r);
            const ghostCueBallY = collisionPoint.cueBallY || (collisionPoint.y - Math.sin(impactAngle) * cueBall.r);
            
            // Draw ghost cue ball (where cue ball will be at impact)
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.8)';
            ctx.lineWidth = 2;
            ctx.setLineDash([5, 5]);
            ctx.beginPath();
            ctx.arc(ghostCueBallX, ghostCueBallY, cueBall.r, 0, Math.PI * 2);
            ctx.stroke();
            ctx.setLineDash([]);
            
            // Draw ghost object ball (where object ball will go)
            // The impact angle points from cue ball to object ball at collision
            ctx.strokeStyle = this.getBallColor(objectBall) || 'rgba(255, 200, 100, 0.8)';
            ctx.lineWidth = 2;
            ctx.setLineDash([5, 5]);
            ctx.beginPath();
            ctx.arc(objectBall.x, objectBall.y, objectBall.r, 0, Math.PI * 2);
            ctx.stroke();
            ctx.setLineDash([]);
            
            // Connection line from cue ball to ghost position
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
            ctx.lineWidth = 1;
            ctx.setLineDash([4, 4]);
            ctx.beginPath();
            ctx.moveTo(cueBall.x, cueBall.y);
            ctx.lineTo(ghostCueBallX, ghostCueBallY);
            ctx.stroke();
            ctx.setLineDash([]);
            
            ctx.restore();
        }
        
        // Calculate object ball trajectory after collision using proper physics
        // The object ball travels along the line connecting the ball centers at impact
        const ghostCueBallX = collisionPoint.cueBallX;
        const ghostCueBallY = collisionPoint.cueBallY;
        
        if (!ghostCueBallX || !ghostCueBallY) return;
        
        // The object ball trajectory is along the line from ghost cue ball to object ball center
        // This is the fundamental physics of pool - object ball travels along line of centers
        const objDx = objectBall.x - ghostCueBallX;
        const objDy = objectBall.y - ghostCueBallY;
        const objDist = Math.sqrt(objDx * objDx + objDy * objDy);
        
        if (objDist > 0.1) {
            // Normalize the direction
            const objNx = objDx / objDist;
            const objNy = objDy / objDist;
            
            // Object ball trajectory angle - this is the key physics!
            // Object ball travels along the line of centers at impact
            const trajectoryAngle = Math.atan2(objNy, objNx);
            
            // Draw predicted trajectory path starting from object ball's CURRENT position
            // (The ball will move in this direction after being hit)
            this.drawPredictedPath(
                ctx,
                objectBall,
                trajectoryAngle,
                tableWidth,
                tableHeight,
                cushionMargin,
                game
            );
            
            // Also draw cue ball deflection path (90 degrees for stun shot, varies with spin)
            this.drawCueBallDeflection(
                ctx,
                cueBall,
                ghostCueBallX,
                ghostCueBallY,
                aimAngle,
                trajectoryAngle,
                tableWidth,
                tableHeight,
                cushionMargin,
                game
            );
        }
    },
    
    /**
     * Draw the predicted cue ball path after collision
     */
    drawCueBallDeflection(ctx, cueBall, ghostX, ghostY, aimAngle, objectAngle, tableWidth, tableHeight, cushionMargin, game) {
        // For a stun shot (no top/bottom spin), the cue ball deflects at 90 degrees to the object ball path
        // The tangent line is perpendicular to the line of centers
        
        // Calculate the deflection angle (perpendicular to object ball direction)
        // Cue ball goes in the direction that conserves momentum
        let deflectionAngle;
        
        // Determine which side of the object ball path the cue ball goes
        // Based on the approach angle relative to the contact line
        const angleDiff = aimAngle - objectAngle;
        
        // Normalize angle difference to -PI to PI
        let normalizedDiff = angleDiff;
        while (normalizedDiff > Math.PI) normalizedDiff -= 2 * Math.PI;
        while (normalizedDiff < -Math.PI) normalizedDiff += 2 * Math.PI;
        
        // Cue ball deflects perpendicular to object ball path
        // Direction depends on which side of center the cue ball hits
        if (normalizedDiff >= 0) {
            deflectionAngle = objectAngle - Math.PI / 2;
        } else {
            deflectionAngle = objectAngle + Math.PI / 2;
        }
        
        // For a cut shot, the cue ball path is shorter (energy transferred to object ball)
        // The thinner the cut, the more the cue ball continues forward
        const cutAngle = Math.abs(normalizedDiff);
        const deflectionStrength = Math.sin(cutAngle); // 0 for straight shot, 1 for 90-degree cut
        
        // Only show deflection line if it's a significant cut (not a straight-on shot)
        if (deflectionStrength > 0.1) {
            ctx.save();
            
            const deflectionLength = (game.trajectoryLength || 200) * 0.6 * deflectionStrength;
            const endX = ghostX + Math.cos(deflectionAngle) * deflectionLength;
            const endY = ghostY + Math.sin(deflectionAngle) * deflectionLength;
            
            // Clamp to table bounds
            const minX = cushionMargin + cueBall.r;
            const maxX = tableWidth - cushionMargin - cueBall.r;
            const minY = cushionMargin + cueBall.r;
            const maxY = tableHeight - cushionMargin - cueBall.r;
            
            const clampedEndX = Math.max(minX, Math.min(maxX, endX));
            const clampedEndY = Math.max(minY, Math.min(maxY, endY));
            
            // Draw cue ball deflection path
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
            ctx.lineWidth = 2;
            ctx.setLineDash([6, 4]);
            ctx.beginPath();
            ctx.moveTo(ghostX, ghostY);
            ctx.lineTo(clampedEndX, clampedEndY);
            ctx.stroke();
            ctx.setLineDash([]);
            
            ctx.restore();
        }
    },
    
    /**
     * Find which ball will be hit first by the cue ball
     * Uses proper geometric ray-circle intersection
     */
    findFirstBallHit(cueBall, aimAngle, allBalls) {
        let closestDist = Infinity;
        let closestBall = null;
        let closestCollision = null;
        let actualImpactAngle = aimAngle;
        
        // Ray from cue ball in aim direction
        const rayDirX = Math.cos(aimAngle);
        const rayDirY = Math.sin(aimAngle);
        
        allBalls.forEach(ball => {
            if (ball === cueBall || ball.potted) return;
            
            // Vector from cue ball to object ball center
            const dx = ball.x - cueBall.x;
            const dy = ball.y - cueBall.y;
            
            // Project onto ray direction (dot product)
            const projection = dx * rayDirX + dy * rayDirY;
            
            // Only consider balls in front of cue ball
            if (projection < 0) return;
            
            // Find closest point on ray to ball center
            const closestX = cueBall.x + rayDirX * projection;
            const closestY = cueBall.y + rayDirY * projection;
            
            // Perpendicular distance from ball center to ray
            const perpDistSq = (ball.x - closestX) * (ball.x - closestX) +
                               (ball.y - closestY) * (ball.y - closestY);
            const perpDist = Math.sqrt(perpDistSq);
            
            // Combined radii - this is the hit threshold
            const combinedRadii = cueBall.r + ball.r;
            
            // Check if ray intersects ball
            if (perpDist <= combinedRadii) {
                // Use proper ray-circle intersection formula
                // Find the exact point where cue ball edge touches object ball edge
                
                // Distance along ray to collision point
                // Using: d = projection - sqrt(combinedRadii^2 - perpDist^2)
                const halfChord = Math.sqrt(combinedRadii * combinedRadii - perpDistSq);
                const collisionDist = projection - halfChord;
                
                if (collisionDist > 0 && collisionDist < closestDist) {
                    closestDist = collisionDist;
                    closestBall = ball;
                    
                    // Position of cue ball center at moment of collision
                    const cueBallAtImpactX = cueBall.x + rayDirX * collisionDist;
                    const cueBallAtImpactY = cueBall.y + rayDirY * collisionDist;
                    
                    // The collision point is on the line between the two ball centers
                    // at a distance of cueBall.r from the cue ball center
                    const impactDx = ball.x - cueBallAtImpactX;
                    const impactDy = ball.y - cueBallAtImpactY;
                    const impactDist = Math.sqrt(impactDx * impactDx + impactDy * impactDy);
                    
                    // Normalize the impact vector
                    const impactNx = impactDx / impactDist;
                    const impactNy = impactDy / impactDist;
                    
                    // Collision point on the surface of both balls
                    closestCollision = {
                        x: cueBallAtImpactX + impactNx * cueBall.r,
                        y: cueBallAtImpactY + impactNy * cueBall.r,
                        // Also store cue ball position at impact for ghost ball
                        cueBallX: cueBallAtImpactX,
                        cueBallY: cueBallAtImpactY
                    };
                    
                    // The actual impact angle is the angle from cue ball to object ball at impact
                    actualImpactAngle = Math.atan2(impactDy, impactDx);
                }
            }
        });
        
        if (closestBall && closestCollision) {
            return {
                ball: closestBall,
                collisionPoint: closestCollision,
                impactAngle: actualImpactAngle
            };
        }
        
        return null;
    },
    
    /**
     * Draw predicted path for a ball including cushion bounces
     */
    drawPredictedPath(ctx, ball, angle, tableWidth, tableHeight, cushionMargin, game) {
        ctx.save();
        
        const minX = cushionMargin + ball.r;
        const maxX = tableWidth - cushionMargin - ball.r;
        const minY = cushionMargin + ball.r;
        const maxY = tableHeight - cushionMargin - ball.r;
        
        let x = ball.x;
        let y = ball.y;
        let dirX = Math.cos(angle);
        let dirY = Math.sin(angle);
        let remainingLength = game.trajectoryLength || 200;
        
        const segments = [];
        const maxBounces = 3; // Limit number of bounces to predict
        let bounceCount = 0;
        
        // Trace path with cushion bounces
        while (remainingLength > 0 && bounceCount < maxBounces) {
            // Calculate distance to nearest cushion
            let distToWall = Infinity;
            let hitWall = null;
            
            // Check all four walls
            if (dirX > 0) {
                const d = (maxX - x) / dirX;
                if (d > 0 && d < distToWall) {
                    distToWall = d;
                    hitWall = 'right';
                }
            } else if (dirX < 0) {
                const d = (minX - x) / dirX;
                if (d > 0 && d < distToWall) {
                    distToWall = d;
                    hitWall = 'left';
                }
            }
            
            if (dirY > 0) {
                const d = (maxY - y) / dirY;
                if (d > 0 && d < distToWall) {
                    distToWall = d;
                    hitWall = 'bottom';
                }
            } else if (dirY < 0) {
                const d = (minY - y) / dirY;
                if (d > 0 && d < distToWall) {
                    distToWall = d;
                    hitWall = 'top';
                }
            }
            
            // Determine segment length
            const segmentLength = Math.min(distToWall, remainingLength);
            const endX = x + dirX * segmentLength;
            const endY = y + dirY * segmentLength;
            
            segments.push({ startX: x, startY: y, endX, endY, bounce: bounceCount });
            
            x = endX;
            y = endY;
            remainingLength -= segmentLength;
            
            // Handle cushion bounce
            if (distToWall < remainingLength && hitWall) {
                bounceCount++;
                
                // Reflect direction based on which wall was hit
                if (hitWall === 'left' || hitWall === 'right') {
                    dirX = -dirX * 0.78; // Apply restitution
                } else {
                    dirY = -dirY * 0.78;
                }
                
                // Normalize direction
                const mag = Math.sqrt(dirX * dirX + dirY * dirY);
                if (mag > 0) {
                    dirX /= mag;
                    dirY /= mag;
                }
            }
        }
        
        // Draw all segments with fading
        segments.forEach((seg, index) => {
            const alpha = 1 - (index / segments.length) * 0.7;
            
            // Color changes after each bounce
            let color;
            if (seg.bounce === 0) {
                color = `rgba(100, 200, 255, ${alpha * 0.8})`;  // Blue for first segment
            } else if (seg.bounce === 1) {
                color = `rgba(255, 200, 100, ${alpha * 0.7})`;  // Orange after first bounce
            } else {
                color = `rgba(255, 100, 100, ${alpha * 0.6})`;  // Red after second bounce
            }
            
            // Draw line
            ctx.strokeStyle = color;
            ctx.lineWidth = 3;
            ctx.setLineDash([8, 6]);
            ctx.beginPath();
            ctx.moveTo(seg.startX, seg.startY);
            ctx.lineTo(seg.endX, seg.endY);
            ctx.stroke();
            
            // Draw dots along the line for better visibility
            const numDots = 5;
            ctx.fillStyle = color;
            for (let i = 0; i <= numDots; i++) {
                const t = i / numDots;
                const dotX = seg.startX + (seg.endX - seg.startX) * t;
                const dotY = seg.startY + (seg.endY - seg.startY) * t;
                const dotSize = 2 * alpha;
                
                ctx.beginPath();
                ctx.arc(dotX, dotY, dotSize, 0, Math.PI * 2);
                ctx.fill();
            }
        });
        
        // Draw end point indicator
        if (segments.length > 0) {
            const lastSeg = segments[segments.length - 1];
            const endGrad = ctx.createRadialGradient(
                lastSeg.endX, lastSeg.endY, 0,
                lastSeg.endX, lastSeg.endY, ball.r + 5
            );
            endGrad.addColorStop(0, 'rgba(100, 200, 255, 0.5)');
            endGrad.addColorStop(1, 'rgba(100, 200, 255, 0)');
            
            ctx.fillStyle = endGrad;
            ctx.beginPath();
            ctx.arc(lastSeg.endX, lastSeg.endY, ball.r + 5, 0, Math.PI * 2);
            ctx.fill();
            
            // Dotted circle at end
            ctx.strokeStyle = 'rgba(100, 200, 255, 0.7)';
            ctx.lineWidth = 2;
            ctx.setLineDash([4, 4]);
            ctx.beginPath();
            ctx.arc(lastSeg.endX, lastSeg.endY, ball.r, 0, Math.PI * 2);
            ctx.stroke();
        }
        
        ctx.setLineDash([]);
        ctx.restore();
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
    },
    
    /**
     * Draw rail bolts/screws on the wooden rails
     * PHASE 3: Professional detail
     */
    drawRailBolts(ctx, width, height, margin) {
        ctx.save();
        
        const boltRadius = 3;
        const boltColor = '#4a3520';
        const boltHighlight = '#6d5436';
        
        // Top rail bolts
        const topY = margin * 0.3;
        for (let i = 1; i <= 11; i++) {
            const x = (width / 12) * i;
            this.drawBolt(ctx, x, topY, boltRadius, boltColor, boltHighlight);
        }
        
        // Bottom rail bolts
        const bottomY = height - margin * 0.3;
        for (let i = 1; i <= 11; i++) {
            const x = (width / 12) * i;
            this.drawBolt(ctx, x, bottomY, boltRadius, boltColor, boltHighlight);
        }
        
        // Left rail bolts
        const leftX = margin * 0.3;
        for (let i = 1; i <= 7; i++) {
            const y = (height / 8) * i;
            this.drawBolt(ctx, leftX, y, boltRadius, boltColor, boltHighlight);
        }
        
        // Right rail bolts
        const rightX = width - margin * 0.3;
        for (let i = 1; i <= 7; i++) {
            const y = (height / 4) * i;
            this.drawBolt(ctx, rightX, y, boltRadius, boltColor, boltHighlight);
        }
        
        ctx.restore();
    },
    
    /**
     * Draw a single rail bolt with 3D effect
     */
    drawBolt(ctx, x, y, radius, color, highlightColor) {
        // Bolt shadow
        ctx.fillStyle = 'rgba(0, 0, 0, 0.4)';
        ctx.beginPath();
        ctx.arc(x + 0.5, y + 0.5, radius, 0, Math.PI * 2);
        ctx.fill();
        
        // Bolt body with gradient
        const boltGrad = ctx.createRadialGradient(x - 1, y - 1, 0, x, y, radius);
        boltGrad.addColorStop(0, highlightColor);
        boltGrad.addColorStop(0.5, color);
        boltGrad.addColorStop(1, color);
        
        ctx.fillStyle = boltGrad;
        ctx.beginPath();
        ctx.arc(x, y, radius, 0, Math.PI * 2);
        ctx.fill();
        
        // Bolt groove (Phillips head screw)
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.6)';
        ctx.lineWidth = 0.8;
        ctx.beginPath();
        ctx.moveTo(x - radius * 0.6, y);
        ctx.lineTo(x + radius * 0.6, y);
        ctx.moveTo(x, y - radius * 0.6);
        ctx.lineTo(x, y + radius * 0.6);
        ctx.stroke();
    }
};
";
    }
}
