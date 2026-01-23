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
        
    // ===== OVERHEAD TABLE LIGHT SIMULATION =====
    // Realistic pool table lighting - bright center with warm tones, gradual falloff
    const feltInset = frameWidth;
    const feltWidth = width - (feltInset * 2);
    const feltHeight = height - (feltInset * 2);
    const centerX = width / 2;
    const centerY = height / 2;
        
    // Primary felt color with overhead light hotspot
    const radius = Math.max(feltWidth, feltHeight) * 0.65;
    const feltGradient = ctx.createRadialGradient(centerX, centerY, 0, centerX, centerY, radius);
    feltGradient.addColorStop(0, this.adjustColor(clothColor, 18));    // Bright center (overhead light)
    feltGradient.addColorStop(0.2, this.adjustColor(clothColor, 12));  // Still bright
    feltGradient.addColorStop(0.4, this.adjustColor(clothColor, 5));   // Transition
    feltGradient.addColorStop(0.65, clothColor);                        // Base color
    feltGradient.addColorStop(0.85, this.adjustColor(clothColor, -8)); // Slight shadow
    feltGradient.addColorStop(1, this.adjustColor(clothColor, -18));   // Dark edges
        
    ctx.fillStyle = feltGradient;
    ctx.fillRect(feltInset, feltInset, feltWidth, feltHeight);
    
    // Secondary warm light glow (simulates incandescent pool light)
    const warmGlow = ctx.createRadialGradient(
        centerX, centerY - feltHeight * 0.05, feltWidth * 0.1,
        centerX, centerY, feltWidth * 0.5
    );
    warmGlow.addColorStop(0, 'rgba(255, 248, 220, 0.08)');  // Warm center
    warmGlow.addColorStop(0.3, 'rgba(255, 245, 200, 0.04)');
    warmGlow.addColorStop(0.6, 'rgba(255, 240, 180, 0.02)');
    warmGlow.addColorStop(1, 'rgba(255, 240, 180, 0)');
    
    ctx.fillStyle = warmGlow;
    ctx.fillRect(feltInset, feltInset, feltWidth, feltHeight);
        
    // Edge vignette for depth (darker corners)
    const vignetteGrad = ctx.createRadialGradient(
        centerX, centerY, Math.min(feltWidth, feltHeight) * 0.35,
        centerX, centerY, Math.max(feltWidth, feltHeight) * 0.75
    );
    vignetteGrad.addColorStop(0, 'rgba(0, 0, 0, 0)');
    vignetteGrad.addColorStop(0.6, 'rgba(0, 0, 0, 0.02)');
    vignetteGrad.addColorStop(0.8, 'rgba(0, 0, 0, 0.06)');
    vignetteGrad.addColorStop(1, 'rgba(0, 0, 0, 0.15)');
        
    ctx.fillStyle = vignetteGrad;
    ctx.fillRect(feltInset, feltInset, feltWidth, feltHeight);
        
    // ===== PHASE 3: FELT WEAR PATTERNS =====
    this.drawFeltWear(ctx, width, height, feltInset);
        
    // ===== ENHANCED FELT TEXTURE =====
    // Realistic baize weave pattern with directional nap
    ctx.save();
        
    // Layer 1: Fine warp threads (lengthwise)
    ctx.globalAlpha = 0.035;
    const warpColor1 = this.darkenColor(clothColor, 25);
    const warpColor2 = this.lightenColor(clothColor, 8);
    
    for (let i = 0; i < 150; i++) {
        const x = feltInset + (feltWidth / 150) * i;
        const offset = Math.sin(i * 0.3) * 1.5;
        ctx.strokeStyle = i % 2 === 0 ? warpColor1 : warpColor2;
        ctx.lineWidth = 0.5;
        ctx.beginPath();
        ctx.moveTo(x + offset, feltInset);
        ctx.lineTo(x - offset, feltInset + feltHeight);
        ctx.stroke();
    }
    
    // Layer 2: Fine weft threads (crosswise) - slightly denser
    ctx.globalAlpha = 0.03;
    const weftColor1 = this.darkenColor(clothColor, 20);
    const weftColor2 = this.lightenColor(clothColor, 5);
    
    for (let i = 0; i < 100; i++) {
        const y = feltInset + (feltHeight / 100) * i;
        const offset = Math.cos(i * 0.4) * 1;
        ctx.strokeStyle = i % 2 === 0 ? weftColor1 : weftColor2;
        ctx.lineWidth = 0.4;
        ctx.beginPath();
        ctx.moveTo(feltInset, y + offset);
        ctx.lineTo(feltInset + feltWidth, y - offset);
        ctx.stroke();
    }
    
    // Layer 3: Subtle nap direction shimmer (directional light on fibers)
    // Creates the appearance that fibers are aligned in one direction
    ctx.globalAlpha = 0.015;
    const napGradient = ctx.createLinearGradient(feltInset, feltInset, feltInset + feltWidth, feltInset);
    napGradient.addColorStop(0, 'rgba(255, 255, 255, 0)');
    napGradient.addColorStop(0.3, 'rgba(255, 255, 255, 0.4)');
    napGradient.addColorStop(0.5, 'rgba(255, 255, 255, 0.6)');
    napGradient.addColorStop(0.7, 'rgba(255, 255, 255, 0.4)');
    napGradient.addColorStop(1, 'rgba(255, 255, 255, 0)');
    
    ctx.fillStyle = napGradient;
    ctx.fillRect(feltInset, feltInset, feltWidth, feltHeight);
    
    // Layer 4: Micro noise texture for that organic feel
    ctx.globalAlpha = 0.02;
    for (let i = 0; i < 60; i++) {
        const x = feltInset + Math.random() * feltWidth;
        const y = feltInset + Math.random() * feltHeight;
        const size = 0.5 + Math.random() * 1.5;
        
        ctx.fillStyle = Math.random() > 0.5 ? 'rgba(0,0,0,0.3)' : 'rgba(255,255,255,0.2)';
        ctx.beginPath();
        ctx.arc(x, y, size, 0, Math.PI * 2);
        ctx.fill();
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
        // Realistic pockets with leather/rubber linings and depth
        
        // Corner pockets - with realistic depth and leather surround
        corners.forEach(p => {
            // Deepest part - absolute black
            ctx.fillStyle = '#000000';
            ctx.beginPath();
            ctx.arc(p.x, p.y, cornerPocketR * 0.5, 0, Math.PI * 2);
            ctx.fill();
            
            // Depth gradient - creates 3D hole effect
            const depthGrad = ctx.createRadialGradient(
                p.x, p.y, cornerPocketR * 0.3,
                p.x, p.y, cornerPocketR * 1.1
            );
            depthGrad.addColorStop(0, '#050505');
            depthGrad.addColorStop(0.3, '#0a0a0a');
            depthGrad.addColorStop(0.6, '#151515');
            depthGrad.addColorStop(0.85, '#252525');
            depthGrad.addColorStop(1, '#353535');
            
            ctx.fillStyle = depthGrad;
            ctx.beginPath();
            ctx.arc(p.x, p.y, cornerPocketR, 0, Math.PI * 2);
            ctx.fill();
            
            // Leather/rubber surround ring
            const leatherGrad = ctx.createRadialGradient(
                p.x - 2, p.y - 2, cornerPocketR * 0.85,
                p.x, p.y, cornerPocketR * 1.15
            );
            leatherGrad.addColorStop(0, '#1a1a1a');
            leatherGrad.addColorStop(0.3, '#252015');  // Dark brown leather
            leatherGrad.addColorStop(0.6, '#302820');
            leatherGrad.addColorStop(0.9, '#201810');
            leatherGrad.addColorStop(1, '#100805');
            
            ctx.strokeStyle = leatherGrad;
            ctx.lineWidth = cornerPocketR * 0.25;
            ctx.beginPath();
            ctx.arc(p.x, p.y, cornerPocketR * 1.02, 0, Math.PI * 2);
            ctx.stroke();
            
            // Highlight on leather edge (catches light)
            ctx.strokeStyle = 'rgba(255, 240, 220, 0.08)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.arc(p.x - 1, p.y - 1, cornerPocketR * 1.1, Math.PI * 0.8, Math.PI * 1.5);
            ctx.stroke();
        });
        
        // Side pockets - slightly different shape and depth
        sides.forEach(p => {
            // Deepest part
            ctx.fillStyle = '#000000';
            ctx.beginPath();
            ctx.arc(p.x, p.y, sidePocketR * 0.4, 0, Math.PI * 2);
            ctx.fill();
            
            // Depth gradient
            const depthGrad = ctx.createRadialGradient(
                p.x, p.y, sidePocketR * 0.25,
                p.x, p.y, sidePocketR * 1.05
            );
            depthGrad.addColorStop(0, '#050505');
            depthGrad.addColorStop(0.35, '#0c0c0c');
            depthGrad.addColorStop(0.65, '#181818');
            depthGrad.addColorStop(0.9, '#282828');
            depthGrad.addColorStop(1, '#383838');
            
            ctx.fillStyle = depthGrad;
            ctx.beginPath();
            ctx.arc(p.x, p.y, sidePocketR, 0, Math.PI * 2);
            ctx.fill();
            
            // Leather surround
            const leatherGrad = ctx.createRadialGradient(
                p.x - 2, p.y - 2, sidePocketR * 0.8,
                p.x, p.y, sidePocketR * 1.1
            );
            leatherGrad.addColorStop(0, '#1a1a1a');
            leatherGrad.addColorStop(0.4, '#282018');
            leatherGrad.addColorStop(0.7, '#352820');
            leatherGrad.addColorStop(1, '#151010');
            
            ctx.strokeStyle = leatherGrad;
            ctx.lineWidth = sidePocketR * 0.22;
            ctx.beginPath();
            ctx.arc(p.x, p.y, sidePocketR * 0.98, 0, Math.PI * 2);
            ctx.stroke();
            
            // Leather highlight
            ctx.strokeStyle = 'rgba(255, 240, 220, 0.06)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.arc(p.x - 1, p.y - 1, sidePocketR * 1.05, Math.PI * 0.7, Math.PI * 1.4);
            ctx.stroke();
        });
        
        
        // ========== 4. DRAW GREEN CUSHIONS ==========
        // Realistic rubber bumpers with 3D profile
        
        const cushionWidth = 8;  // Slightly thicker for better 3D effect
        const cushionInset = railWidth - cushionWidth / 2 - 1;
        
        // Helper function to draw a cushion segment with 3D effect
        const drawCushionSegment = (x1, y1, x2, y2, isHorizontal) => {
            // Shadow underneath cushion
            ctx.strokeStyle = 'rgba(0, 0, 0, 0.25)';
            ctx.lineWidth = cushionWidth + 2;
            ctx.lineCap = 'round';
            ctx.beginPath();
            ctx.moveTo(x1 + (isHorizontal ? 0 : 1), y1 + (isHorizontal ? 1 : 0));
            ctx.lineTo(x2 + (isHorizontal ? 0 : 1), y2 + (isHorizontal ? 1 : 0));
            ctx.stroke();
            
            // Main cushion body (darker rubber base)
            ctx.strokeStyle = this.darkenColor(cushionColor, 15);
            ctx.lineWidth = cushionWidth;
            ctx.beginPath();
            ctx.moveTo(x1, y1);
            ctx.lineTo(x2, y2);
            ctx.stroke();
            
            // Top highlight (rubber shine)
            ctx.strokeStyle = this.lightenColor(cushionColor, 20);
            ctx.lineWidth = 2.5;
            ctx.beginPath();
            ctx.moveTo(x1, y1 - (isHorizontal ? 2 : 0) + (isHorizontal ? 0 : 0));
            ctx.lineTo(x2, y2 - (isHorizontal ? 2 : 0) + (isHorizontal ? 0 : 0));
            if (!isHorizontal) {
                ctx.moveTo(x1 - 2, y1);
                ctx.lineTo(x2 - 2, y2);
            }
            ctx.stroke();
            
            // Subtle rubber texture line
            ctx.strokeStyle = this.darkenColor(cushionColor, 8);
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x1, y1 + (isHorizontal ? 1 : 0) - (isHorizontal ? 0 : 0));
            ctx.lineTo(x2, y2 + (isHorizontal ? 1 : 0) - (isHorizontal ? 0 : 0));
            if (!isHorizontal) {
                ctx.moveTo(x1 + 1, y1);
                ctx.lineTo(x2 + 1, y2);
            }
            ctx.stroke();
        };
        
        // Top cushions (2 segments with gap for side pocket)
        drawCushionSegment(
            corners[0].x + cornerPocketOpening + 3, cushionInset,
            sides[0].x - sidePocketOpening - 3, cushionInset,
            true
        );
        drawCushionSegment(
            sides[0].x + sidePocketOpening + 3, cushionInset,
            corners[1].x - cornerPocketOpening - 3, cushionInset,
            true
        );
        
        // Bottom cushions
        drawCushionSegment(
            corners[2].x + cornerPocketOpening + 3, height - cushionInset,
            sides[1].x - sidePocketOpening - 3, height - cushionInset,
            true
        );
        drawCushionSegment(
            sides[1].x + sidePocketOpening + 3, height - cushionInset,
            corners[3].x - cornerPocketOpening - 3, height - cushionInset,
            true
        );
        
        // Left cushion
            drawCushionSegment(
                cushionInset, corners[0].y + cornerPocketOpening + 3,
                cushionInset, corners[2].y - cornerPocketOpening - 3,
                false
            );
        
            // Right cushion
            drawCushionSegment(
                width - cushionInset, corners[1].y + cornerPocketOpening + 3,
                width - cushionInset, corners[3].y - cornerPocketOpening - 3,
                false
            );
        
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
     * PHASE 4: Ultra-realistic lighting with subsurface scattering hints
     */
    drawBall(ctx, ball) {
        if (ball.potted) return;
        
        // Calculate ball speed for motion effects
        const speed = Math.sqrt((ball.vx || 0) * (ball.vx || 0) + (ball.vy || 0) * (ball.vy || 0));
        
        // ===== CONTACT SHADOW (Dark core directly under ball) =====
        ctx.save();
        ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
        ctx.beginPath();
        ctx.ellipse(ball.x + 1, ball.y + 2, ball.r * 0.5, ball.r * 0.35, 0, 0, Math.PI * 2);
        ctx.fill();
        
        // ===== SOFT SHADOW (Larger diffuse shadow) =====
        const shadowGrad = ctx.createRadialGradient(
            ball.x + 3, ball.y + 4, ball.r * 0.3,
            ball.x + 3, ball.y + 4, ball.r * 1.3
        );
        shadowGrad.addColorStop(0, 'rgba(0, 0, 0, 0.4)');
        shadowGrad.addColorStop(0.4, 'rgba(0, 0, 0, 0.2)');
        shadowGrad.addColorStop(0.7, 'rgba(0, 0, 0, 0.08)');
        shadowGrad.addColorStop(1, 'rgba(0, 0, 0, 0)');
        
        ctx.fillStyle = shadowGrad;
        ctx.beginPath();
        ctx.ellipse(ball.x + 3, ball.y + 4, ball.r * 1.3, ball.r * 0.8, 0, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
        
        // ===== MOTION BLUR HINT FOR FAST BALLS =====
        if (speed > 5) {
            const blurAlpha = Math.min(0.15, speed / 80);
            const angle = Math.atan2(ball.vy || 0, ball.vx || 0);
            const blurLength = Math.min(ball.r * 0.8, speed * 0.5);
            
            ctx.save();
            ctx.globalAlpha = blurAlpha;
            ctx.fillStyle = ball.color === 'white' ? '#cccccc' : (ball.color === 'red' ? '#882020' : (ball.color === 'yellow' ? '#aa9900' : '#333333'));
            ctx.beginPath();
            ctx.ellipse(
                ball.x - Math.cos(angle) * blurLength * 0.5, 
                ball.y - Math.sin(angle) * blurLength * 0.5, 
                ball.r + blurLength * 0.2, 
                ball.r, 
                angle, 0, Math.PI * 2
            );
            ctx.fill();
            ctx.restore();
        }
        
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
    
    
    
    
    
    
    
    
    
    
    // UK Yellow Ball - Solid bright yellow with phenolic resin look
    drawUKYellowBall(ctx, ball, lightOffsetX, lightOffsetY) {
        // Base gradient with more depth
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.05,
            ball.x, ball.y, ball.r * 1.05
        );
        grad.addColorStop(0, '#fff8a0');  // Bright highlight
        grad.addColorStop(0.1, '#ffed70');
        grad.addColorStop(0.25, '#ffe033');
        grad.addColorStop(0.5, '#ffd700');
        grad.addColorStop(0.7, '#e6b800');
        grad.addColorStop(0.85, '#cc9900');
        grad.addColorStop(1, '#8a6500');
        
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Subsurface scattering simulation - warm glow on shadow side
        const sssGrad = ctx.createRadialGradient(
            ball.x - lightOffsetX * 0.8, ball.y - lightOffsetY * 0.8, 0,
            ball.x - lightOffsetX * 0.8, ball.y - lightOffsetY * 0.8, ball.r * 0.7
        );
        sssGrad.addColorStop(0, 'rgba(255, 200, 50, 0.15)');
        sssGrad.addColorStop(0.5, 'rgba(255, 180, 30, 0.08)');
        sssGrad.addColorStop(1, 'rgba(255, 180, 30, 0)');
        
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        ctx.fillStyle = sssGrad;
        ctx.beginPath();
        ctx.arc(ball.x - lightOffsetX * 0.8, ball.y - lightOffsetY * 0.8, ball.r * 0.7, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
    },
    
    // Black Ball - Deep glossy black with rich reflections
    drawBlackBall(ctx, ball, lightOffsetX, lightOffsetY) {
        // Deep black base with subtle blue tint (like real phenolic balls)
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.05,
            ball.x, ball.y, ball.r * 1.05
        );
        grad.addColorStop(0, '#606875');   // Slight blue-gray highlight
        grad.addColorStop(0.15, '#404550');
        grad.addColorStop(0.3, '#2a2d33');
        grad.addColorStop(0.5, '#1a1c20');
        grad.addColorStop(0.7, '#0f1012');
        grad.addColorStop(1, '#050506');
        
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Environment reflection hint (shows depth)
        const envReflect = ctx.createLinearGradient(
            ball.x - ball.r, ball.y - ball.r * 0.3,
            ball.x + ball.r, ball.y + ball.r * 0.3
        );
        envReflect.addColorStop(0, 'rgba(80, 100, 80, 0)');
        envReflect.addColorStop(0.3, 'rgba(80, 100, 80, 0.05)');
        envReflect.addColorStop(0.5, 'rgba(80, 100, 80, 0.08)');
        envReflect.addColorStop(0.7, 'rgba(80, 100, 80, 0.05)');
        envReflect.addColorStop(1, 'rgba(80, 100, 80, 0)');
        
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        ctx.fillStyle = envReflect;
        ctx.fillRect(ball.x - ball.r, ball.y - ball.r, ball.r * 2, ball.r * 2);
        ctx.restore();
    },
    
    // Cue Ball - Premium white with subtle warmth and perfect gloss
    drawCueBall(ctx, ball, lightOffsetX, lightOffsetY) {
        // Base white/cream gradient with more realistic falloff
        const grad = ctx.createRadialGradient(
            ball.x + lightOffsetX, ball.y + lightOffsetY, ball.r * 0.03,
            ball.x, ball.y, ball.r * 1.05
        );
        grad.addColorStop(0, '#ffffff');   // Pure white hotspot
        grad.addColorStop(0.1, '#fefefc');
        grad.addColorStop(0.25, '#faf8f4');
        grad.addColorStop(0.5, '#f4f0e8');
        grad.addColorStop(0.7, '#e8e2d8');
        grad.addColorStop(0.85, '#d8d0c4');
        grad.addColorStop(1, '#b8b0a0');
        
        ctx.fillStyle = grad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Subtle warm rim lighting (simulates overhead table light)
        const rimLight = ctx.createRadialGradient(
            ball.x, ball.y, ball.r * 0.75,
            ball.x, ball.y, ball.r
        );
        rimLight.addColorStop(0, 'rgba(255, 250, 240, 0)');
        rimLight.addColorStop(0.7, 'rgba(255, 248, 235, 0.1)');
        rimLight.addColorStop(1, 'rgba(255, 245, 225, 0.2)');
        
        ctx.fillStyle = rimLight;
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
    
    // Draw specular highlights for glossy shine - simulates overhead pool table light
    drawSpecularHighlights(ctx, ball, lightOffsetX, lightOffsetY) {
        // Primary specular highlight (sharp, bright main light reflection)
        const specular1 = ctx.createRadialGradient(
            ball.x + lightOffsetX * 0.65, ball.y + lightOffsetY * 0.65, 0,
            ball.x + lightOffsetX * 0.65, ball.y + lightOffsetY * 0.65, ball.r * 0.25
        );
        specular1.addColorStop(0, 'rgba(255, 255, 255, 1.0)');  // Hot center
        specular1.addColorStop(0.15, 'rgba(255, 255, 255, 0.9)');
        specular1.addColorStop(0.4, 'rgba(255, 255, 255, 0.4)');
        specular1.addColorStop(0.7, 'rgba(255, 255, 255, 0.1)');
        specular1.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.fillStyle = specular1;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Secondary highlight (soft bloom around primary)
        const specular2 = ctx.createRadialGradient(
            ball.x + lightOffsetX * 0.55, ball.y + lightOffsetY * 0.55, ball.r * 0.1,
            ball.x + lightOffsetX * 0.55, ball.y + lightOffsetY * 0.55, ball.r * 0.5
        );
        specular2.addColorStop(0, 'rgba(255, 255, 255, 0.35)');
        specular2.addColorStop(0.3, 'rgba(255, 255, 255, 0.15)');
        specular2.addColorStop(0.6, 'rgba(255, 255, 255, 0.05)');
        specular2.addColorStop(1, 'rgba(255, 255, 255, 0)');
        
        ctx.fillStyle = specular2;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Tertiary highlight - subtle secondary reflection (simulates rectangular pool light)
        const specular3 = ctx.createRadialGradient(
            ball.x + lightOffsetX * 0.3, ball.y + lightOffsetY * 0.2, 0,
            ball.x + lightOffsetX * 0.3, ball.y + lightOffsetY * 0.2, ball.r * 0.2
        );
        specular3.addColorStop(0, 'rgba(255, 255, 250, 0.2)');
        specular3.addColorStop(0.5, 'rgba(255, 255, 250, 0.05)');
        specular3.addColorStop(1, 'rgba(255, 255, 250, 0)');
        
        ctx.fillStyle = specular3;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        
        // Rim lighting effect (subtle edge glow from ambient light)
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        
        const rimGrad = ctx.createRadialGradient(
            ball.x, ball.y, ball.r * 0.85,
            ball.x, ball.y, ball.r * 1.0
        );
        rimGrad.addColorStop(0, 'rgba(255, 255, 255, 0)');
        rimGrad.addColorStop(0.5, 'rgba(255, 255, 255, 0.03)');
        rimGrad.addColorStop(1, 'rgba(255, 255, 255, 0.08)');
        
        ctx.fillStyle = rimGrad;
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.fill();
        ctx.restore();
        
        // Subtle ball outline for definition (very soft)
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.18)';
        ctx.lineWidth = 0.75;
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
     * Draw realistic wooden cue stick with professional details
     */
    drawCueStick(ctx, cueBall, aimAngle, pullBackDistance, pushForwardDistance) {
        const baseDist = 35;
        const cueDistance = baseDist + pullBackDistance - pushForwardDistance;
        const cueLength = 220;  // Slightly longer for better proportions
        
        const cueStartX = cueBall.x - Math.cos(aimAngle) * cueDistance;
        const cueStartY = cueBall.y - Math.sin(aimAngle) * cueDistance;
        const cueEndX = cueBall.x - Math.cos(aimAngle) * (cueDistance + cueLength);
        const cueEndY = cueBall.y - Math.sin(aimAngle) * (cueDistance + cueLength);
        
        // Calculate perpendicular for 3D effect
        const perpX = -Math.sin(aimAngle);
        const perpY = Math.cos(aimAngle);
        
        // ===== CUE SHADOW =====
        ctx.save();
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.25)';
        ctx.lineWidth = 14;
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(cueStartX + 3, cueStartY + 3);
        ctx.lineTo(cueEndX + 3, cueEndY + 3);
        ctx.stroke();
        ctx.restore();
        
        // ===== MAIN CUE BODY WITH TAPER =====
        // The cue tapers from ~13mm at tip to ~29mm at butt
        
        // Shaft section (lighter wood - maple)
        const shaftLength = cueLength * 0.55;
        const shaftEndX = cueStartX - Math.cos(aimAngle) * shaftLength;
        const shaftEndY = cueStartY - Math.sin(aimAngle) * shaftLength;
        
        // Shaft gradient - light maple
        const shaftGrad = ctx.createLinearGradient(cueStartX, cueStartY, shaftEndX, shaftEndY);
        shaftGrad.addColorStop(0, '#faf0dc');   // Very light at tip
        shaftGrad.addColorStop(0.2, '#f5e5c8');
        shaftGrad.addColorStop(0.5, '#e8d4b0');
        shaftGrad.addColorStop(0.8, '#dcc498');
        shaftGrad.addColorStop(1, '#d0b880');   // Transition to joint
        
        ctx.strokeStyle = shaftGrad;
        ctx.lineWidth = 9;  // Thinner shaft
        ctx.lineCap = 'butt';
        ctx.beginPath();
        ctx.moveTo(cueStartX - Math.cos(aimAngle) * 8, cueStartY - Math.sin(aimAngle) * 8); // Start after ferrule
        ctx.lineTo(shaftEndX, shaftEndY);
        ctx.stroke();
        
        // Shaft wood grain effect
        ctx.save();
        ctx.strokeStyle = 'rgba(160, 130, 90, 0.15)';
        ctx.lineWidth = 1;
        for (let i = 0; i < 4; i++) {
            const offset = (i - 1.5) * 2;
            ctx.beginPath();
            ctx.moveTo(
                cueStartX - Math.cos(aimAngle) * 10 + perpX * offset, 
                cueStartY - Math.sin(aimAngle) * 10 + perpY * offset
            );
            ctx.lineTo(
                shaftEndX + perpX * offset * 0.8, 
                shaftEndY + perpY * offset * 0.8
            );
            ctx.stroke();
        }
        ctx.restore();
        
        // Joint ring (metal/ivory collar)
        const jointWidth = 8;
        ctx.fillStyle = '#c0c0c0';  // Silver
        ctx.strokeStyle = '#909090';
        ctx.lineWidth = 1;
        ctx.beginPath();
        ctx.arc(shaftEndX, shaftEndY, 6, 0, Math.PI * 2);
        ctx.fill();
        ctx.stroke();
        
        // Joint highlight
        ctx.fillStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.beginPath();
        ctx.arc(shaftEndX - 1, shaftEndY - 1, 2, 0, Math.PI * 2);
        ctx.fill();
        
        // Butt section (darker hardwood with wrap)
        const buttStartX = shaftEndX - Math.cos(aimAngle) * 4;
        const buttStartY = shaftEndY - Math.sin(aimAngle) * 4;
        
        // Butt gradient - darker hardwood
        const buttGrad = ctx.createLinearGradient(buttStartX, buttStartY, cueEndX, cueEndY);
        buttGrad.addColorStop(0, '#8b6f47');
        buttGrad.addColorStop(0.1, '#7a5c38');
        buttGrad.addColorStop(0.3, '#6d4c2a');
        buttGrad.addColorStop(0.5, '#5a3d20');
        buttGrad.addColorStop(0.7, '#4d3318');
        buttGrad.addColorStop(0.9, '#3a2510');
        buttGrad.addColorStop(1, '#2a1a08');
        
        ctx.strokeStyle = buttGrad;
        ctx.lineWidth = 13;  // Thicker butt
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.moveTo(buttStartX, buttStartY);
        ctx.lineTo(cueEndX, cueEndY);
        ctx.stroke();
        
        // Wrap section (leather or linen grip) - about 1/3 up the butt
        const wrapStart = 0.4;
        const wrapEnd = 0.7;
        const wrapStartX = buttStartX - Math.cos(aimAngle) * (cueLength * 0.45 * wrapStart);
        const wrapStartY = buttStartY - Math.sin(aimAngle) * (cueLength * 0.45 * wrapStart);
        const wrapEndX = buttStartX - Math.cos(aimAngle) * (cueLength * 0.45 * wrapEnd);
        const wrapEndY = buttStartY - Math.sin(aimAngle) * (cueLength * 0.45 * wrapEnd);
        
        ctx.strokeStyle = '#1a1a1a';  // Black Irish linen
        ctx.lineWidth = 14;
        ctx.beginPath();
        ctx.moveTo(wrapStartX, wrapStartY);
        ctx.lineTo(wrapEndX, wrapEndY);
        ctx.stroke();
        
        // Wrap texture lines
        ctx.strokeStyle = 'rgba(60, 60, 60, 0.8)';
        ctx.lineWidth = 1;
        const wrapDist = Math.sqrt(Math.pow(wrapEndX - wrapStartX, 2) + Math.pow(wrapEndY - wrapStartY, 2));
        for (let i = 0; i < wrapDist; i += 4) {
            const t = i / wrapDist;
            const x = wrapStartX + (wrapEndX - wrapStartX) * t;
            const y = wrapStartY + (wrapEndY - wrapStartY) * t;
            ctx.beginPath();
            ctx.moveTo(x + perpX * 6, y + perpY * 6);
            ctx.lineTo(x - perpX * 6, y - perpY * 6);
            ctx.stroke();
        }
        
        // ===== FERRULE (white/ivory section before tip) =====
        const ferruleLength = 8;
        const ferruleEndX = cueStartX - Math.cos(aimAngle) * ferruleLength;
        const ferruleEndY = cueStartY - Math.sin(aimAngle) * ferruleLength;
        
        const ferruleGrad = ctx.createLinearGradient(cueStartX, cueStartY, ferruleEndX, ferruleEndY);
        ferruleGrad.addColorStop(0, '#f8f8f0');
        ferruleGrad.addColorStop(0.5, '#f0ece0');
        ferruleGrad.addColorStop(1, '#e8e0d0');
        
        ctx.strokeStyle = ferruleGrad;
        ctx.lineWidth = 8;
        ctx.lineCap = 'butt';
        ctx.beginPath();
        ctx.moveTo(cueStartX, cueStartY);
        ctx.lineTo(ferruleEndX, ferruleEndY);
        ctx.stroke();
        
        // ===== CUE TIP (blue chalk-covered leather) =====
        const tipGrad = ctx.createRadialGradient(
            cueStartX - perpX * 1, cueStartY - perpY * 1, 0,
            cueStartX, cueStartY, 6
        );
        tipGrad.addColorStop(0, '#8bb8e8');   // Light blue (chalk)
        tipGrad.addColorStop(0.4, '#6a9fd4');
        tipGrad.addColorStop(0.7, '#5080b8');
        tipGrad.addColorStop(1, '#3a6090');   // Darker edge
        
        ctx.fillStyle = tipGrad;
        ctx.beginPath();
        ctx.arc(cueStartX, cueStartY, 5, 0, Math.PI * 2);
        ctx.fill();
        
        // Tip highlight (rounded dome of tip)
        ctx.fillStyle = 'rgba(255, 255, 255, 0.35)';
        ctx.beginPath();
        ctx.ellipse(cueStartX - 1.5, cueStartY - 1.5, 2, 1.5, aimAngle, 0, Math.PI * 2);
        ctx.fill();
        
        // ===== CONTACT GLOW EFFECT =====
        const distanceToContact = cueDistance - 12;
        if (distanceToContact < 15) {
            const glowIntensity = 1 - (distanceToContact / 15);
            ctx.save();
            const glowGrad = ctx.createRadialGradient(
                cueStartX, cueStartY, 0,
                cueStartX, cueStartY, 20
            );
            glowGrad.addColorStop(0, `rgba(255, 230, 150, ${glowIntensity * 0.6})`);
            glowGrad.addColorStop(0.5, `rgba(255, 215, 100, ${glowIntensity * 0.3})`);
            glowGrad.addColorStop(1, 'rgba(255, 200, 50, 0)');
            ctx.fillStyle = glowGrad;
            ctx.beginPath();
            ctx.arc(cueStartX, cueStartY, 20, 0, Math.PI * 2);
            ctx.fill();
            ctx.restore();
        }
        
        // ===== GHOST GUIDE (shows rest position) =====
        if (pullBackDistance > 10) {
            const ghostStartX = cueBall.x - Math.cos(aimAngle) * baseDist;
            const ghostStartY = cueBall.y - Math.sin(aimAngle) * baseDist;
            
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.12)';
            ctx.lineWidth = 8;
            ctx.setLineDash([6, 6]);
            ctx.beginPath();
            ctx.moveTo(ghostStartX, ghostStartY);
            ctx.lineTo(cueStartX, cueStartY);
            ctx.stroke();
            ctx.setLineDash([]);
        }
        
        // ===== CONTACT POINT INDICATOR =====
        const contactPointX = cueBall.x - Math.cos(aimAngle) * 12;
        const contactPointY = cueBall.y - Math.sin(aimAngle) * 12;
        const contactColor = distanceToContact < 5 ? 'rgba(255, 215, 0, 0.9)' : 'rgba(255, 255, 255, 0.4)';
        
        ctx.strokeStyle = contactColor;
        ctx.lineWidth = 1.5;
        ctx.setLineDash([3, 3]);
        ctx.beginPath();
        ctx.arc(contactPointX, contactPointY, 8, 0, Math.PI * 2);
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
