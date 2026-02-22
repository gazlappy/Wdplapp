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

    // ===== PRE-RENDERED FELT NOISE TEXTURE (#4) =====
    if (typeof PoolVFX !== 'undefined') {
        PoolVFX.drawFeltNoise(ctx, feltInset, feltWidth, feltHeight);
    }

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

        // ===== CUSHION-CAST SHADOWS (#2) =====
        if (typeof PoolVFX !== 'undefined') {
            PoolVFX.drawCushionShadows(ctx, width, height, cushionMargin);
        }

        // ===== TABLE EDGE BEVEL (#10) =====
        if (typeof PoolVFX !== 'undefined') {
            PoolVFX.drawTableBevel(ctx, width, height, cushionMargin);
        }

        // ===== LIGHT TEMPERATURE OVERLAY (#15) =====
        if (typeof PoolVFX !== 'undefined') {
            const frameW = 12;
            PoolVFX.drawLightTemperatureOverlay(ctx, frameW, width - frameW * 2, height - frameW * 2);
        }
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
     * Draw a small rounded cap at a rail endpoint (where rail meets pocket)
     */
    drawRailCap(ctx, cx, cy, r, color) {
        ctx.fillStyle = color;
        ctx.beginPath();
        ctx.arc(cx, cy, r, 0, Math.PI * 2);
        ctx.fill();
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
        
        // ========== 2. RAIL-POCKET TRANSITIONS ==========
        // Round off the hard-cut rail ends where they meet pocket openings
        const transR = railWidth * 0.35;
        ctx.fillStyle = railColor;

        // Top rail — left ends
        this.drawRailCap(ctx, corners[0].x + cornerPocketOpening, railWidth / 2, transR, railColor);
        this.drawRailCap(ctx, sides[0].x - sidePocketOpening, railWidth / 2, transR, railColor);
        // Top rail — right ends
        this.drawRailCap(ctx, sides[0].x + sidePocketOpening, railWidth / 2, transR, railColor);
        this.drawRailCap(ctx, corners[1].x - cornerPocketOpening, railWidth / 2, transR, railColor);
        // Bottom rail
        this.drawRailCap(ctx, corners[2].x + cornerPocketOpening, height - railWidth / 2, transR, railColor);
        this.drawRailCap(ctx, sides[1].x - sidePocketOpening, height - railWidth / 2, transR, railColor);
        this.drawRailCap(ctx, sides[1].x + sidePocketOpening, height - railWidth / 2, transR, railColor);
        this.drawRailCap(ctx, corners[3].x - cornerPocketOpening, height - railWidth / 2, transR, railColor);
        // Left rail
        this.drawRailCap(ctx, railWidth / 2, corners[0].y + cornerPocketOpening, transR, railColor);
        this.drawRailCap(ctx, railWidth / 2, corners[2].y - cornerPocketOpening, transR, railColor);
        // Right rail
        this.drawRailCap(ctx, width - railWidth / 2, corners[1].y + cornerPocketOpening, transR, railColor);
        this.drawRailCap(ctx, width - railWidth / 2, corners[3].y - cornerPocketOpening, transR, railColor);

        // ========== 2b. RAIL 3D EFFECTS ==========
        // Wood grain texture on rails (#9)
        if (typeof PoolVFX !== 'undefined') {
            // Top rails
            PoolVFX.drawRailWoodGrain(ctx, corners[0].x + cornerPocketOpening, 0,
                sides[0].x - sidePocketOpening - corners[0].x - cornerPocketOpening, railWidth);
            PoolVFX.drawRailWoodGrain(ctx, sides[0].x + sidePocketOpening, 0,
                corners[1].x - cornerPocketOpening - sides[0].x - sidePocketOpening, railWidth);
            // Bottom rails
            PoolVFX.drawRailWoodGrain(ctx, corners[2].x + cornerPocketOpening, height - railWidth,
                sides[1].x - sidePocketOpening - corners[2].x - cornerPocketOpening, railWidth);
            PoolVFX.drawRailWoodGrain(ctx, sides[1].x + sidePocketOpening, height - railWidth,
                corners[3].x - cornerPocketOpening - sides[1].x - sidePocketOpening, railWidth);
            // Left rail
            PoolVFX.drawRailWoodGrain(ctx, 0, corners[0].y + cornerPocketOpening,
                railWidth, corners[2].y - cornerPocketOpening - corners[0].y - cornerPocketOpening);
            // Right rail
            PoolVFX.drawRailWoodGrain(ctx, width - railWidth, corners[1].y + cornerPocketOpening,
                railWidth, corners[3].y - cornerPocketOpening - corners[1].y - cornerPocketOpening);
        }

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
        
        // ========== 3. DRAW POCKET MOUTH OPENINGS ON FELT ==========
        // These are the visible pocket entries from the playing surface.
        // On a real table, you see a dark opening at each pocket through the cushion gap.

        // Corner pocket mouths (quarter-circle facing into the corner)
        corners.forEach((p, idx) => {
            const mouthR = cornerPocketR * 0.65;
            const mouthGrad = ctx.createRadialGradient(p.x, p.y, mouthR * 0.2, p.x, p.y, mouthR);
            mouthGrad.addColorStop(0, 'rgba(0, 0, 0, 0.85)');
            mouthGrad.addColorStop(0.4, 'rgba(5, 5, 5, 0.7)');
            mouthGrad.addColorStop(0.7, 'rgba(15, 12, 8, 0.45)');
            mouthGrad.addColorStop(1, 'rgba(20, 18, 12, 0)');

            ctx.fillStyle = mouthGrad;
            ctx.beginPath();
            ctx.arc(p.x, p.y, mouthR, 0, Math.PI * 2);
            ctx.fill();
        });

        // Side pocket mouths (half-ellipse facing into the rail)
        sides.forEach((p, idx) => {
            const mouthRx = sidePocketR * 0.7;
            const mouthRy = sidePocketR * 0.45;
            const mouthGrad = ctx.createRadialGradient(p.x, p.y, mouthRy * 0.15, p.x, p.y, mouthRx);
            mouthGrad.addColorStop(0, 'rgba(0, 0, 0, 0.85)');
            mouthGrad.addColorStop(0.35, 'rgba(5, 5, 5, 0.65)');
            mouthGrad.addColorStop(0.65, 'rgba(15, 12, 8, 0.35)');
            mouthGrad.addColorStop(1, 'rgba(20, 18, 12, 0)');

            ctx.fillStyle = mouthGrad;
            ctx.beginPath();
            ctx.ellipse(p.x, p.y, mouthRx, mouthRy, 0, 0, Math.PI * 2);
            ctx.fill();
        });

        // ========== 4. DRAW POCKET DEPTH (CLIPPED TO RAIL ZONE) ==========
        // The deep pocket visuals (leather, depth gradient, jaws) are clipped
        // to the rail area so they don't bleed onto the playing surface.
        ctx.save();
        ctx.beginPath();
        // Outer rect — full canvas
        ctx.rect(0, 0, width, height);
        // Inner rect — playing surface (counterclockwise to cut a hole via evenodd)
        ctx.rect(railWidth, height - railWidth, width - 2 * railWidth, -(height - 2 * railWidth));
        ctx.clip('evenodd');

        // Corner pockets — depth and leather (now clipped to rail zone)
        corners.forEach((p, idx) => {
            const isLeft = (idx === 0 || idx === 2);
            const isTop = (idx === 0 || idx === 1);
            const pocketR = cornerPocketR * 1.15;

            // Depth gradient
            const depthGrad = ctx.createRadialGradient(
                p.x, p.y, cornerPocketR * 0.15,
                p.x, p.y, pocketR
            );
            depthGrad.addColorStop(0, '#020202');
            depthGrad.addColorStop(0.3, '#080808');
            depthGrad.addColorStop(0.6, '#121212');
            depthGrad.addColorStop(1, '#282420');

            ctx.fillStyle = depthGrad;
            ctx.beginPath();
            ctx.arc(p.x, p.y, pocketR, 0, Math.PI * 2);
            ctx.fill();

            // Leather/rubber surround
            const leatherGrad = ctx.createRadialGradient(
                p.x - 1, p.y - 1, cornerPocketR * 0.85,
                p.x, p.y, pocketR
            );
            leatherGrad.addColorStop(0, '#1a1a1a');
            leatherGrad.addColorStop(0.3, '#2a2015');
            leatherGrad.addColorStop(0.7, '#352a20');
            leatherGrad.addColorStop(1, '#151008');

            ctx.strokeStyle = leatherGrad;
            ctx.lineWidth = cornerPocketR * 0.22;
            ctx.beginPath();
            ctx.arc(p.x, p.y, pocketR - 1, 0, Math.PI * 2);
            ctx.stroke();

            // Jaw facets
            const jawLen = cornerPocketR * 0.7;
            const jawWidth = 3;
            const diag = cornerPocketR * 0.75;

            ctx.strokeStyle = '#1a1410';
            ctx.lineWidth = jawWidth;
            ctx.lineCap = 'round';

            // Jaw 1 (horizontal side)
            const jaw1StartX = p.x + (isLeft ? diag : -diag);
            const jaw1StartY = p.y;
            const jaw1EndX = p.x + (isLeft ? cornerPocketOpening * 0.55 : -cornerPocketOpening * 0.55);
            const jaw1EndY = p.y + (isTop ? -jawLen * 0.15 : jawLen * 0.15);
            ctx.beginPath();
            ctx.moveTo(jaw1StartX, jaw1StartY);
            ctx.lineTo(jaw1EndX, jaw1EndY);
            ctx.stroke();

            // Jaw 2 (vertical side)
            const jaw2StartX = p.x;
            const jaw2StartY = p.y + (isTop ? diag : -diag);
            const jaw2EndX = p.x + (isLeft ? -jawLen * 0.15 : jawLen * 0.15);
            const jaw2EndY = p.y + (isTop ? cornerPocketOpening * 0.55 : -cornerPocketOpening * 0.55);
            ctx.beginPath();
            ctx.moveTo(jaw2StartX, jaw2StartY);
            ctx.lineTo(jaw2EndX, jaw2EndY);
            ctx.stroke();
        });

        // Side pockets — depth and leather (clipped to rail zone)
        sides.forEach((p, idx) => {
            const isTop = (idx === 0);
            const pocketRx = sidePocketR * 1.25;
            const pocketRy = sidePocketR * 0.9;

            // Depth gradient
            const depthGrad = ctx.createRadialGradient(
                p.x, p.y, sidePocketR * 0.1,
                p.x, p.y, pocketRx
            );
            depthGrad.addColorStop(0, '#020202');
            depthGrad.addColorStop(0.35, '#0a0a0a');
            depthGrad.addColorStop(0.65, '#181818');
            depthGrad.addColorStop(1, '#302a24');

            ctx.fillStyle = depthGrad;
            ctx.beginPath();
            ctx.ellipse(p.x, p.y, pocketRx, pocketRy, 0, 0, Math.PI * 2);
            ctx.fill();

            // Leather surround
            const leatherGrad = ctx.createRadialGradient(
                p.x - 1, p.y - 1, sidePocketR * 0.75,
                p.x, p.y, pocketRx
            );
            leatherGrad.addColorStop(0, '#1a1a1a');
            leatherGrad.addColorStop(0.4, '#282018');
            leatherGrad.addColorStop(0.7, '#352a20');
            leatherGrad.addColorStop(1, '#151010');

            ctx.strokeStyle = leatherGrad;
            ctx.lineWidth = sidePocketR * 0.2;
            ctx.beginPath();
            ctx.ellipse(p.x, p.y, pocketRx - 2, pocketRy - 2, 0, 0, Math.PI * 2);
            ctx.stroke();

            // Side pocket jaw facets
            const jawLen = sidePocketR * 0.6;
            const jawWidth = 3;
            const jawSpread = sidePocketOpening * 0.5;

            ctx.strokeStyle = '#1a1410';
            ctx.lineWidth = jawWidth;
            ctx.lineCap = 'round';

            ctx.beginPath();
            ctx.moveTo(p.x - sidePocketR * 0.95, p.y);
            ctx.lineTo(p.x - jawSpread, p.y + (isTop ? -jawLen * 0.5 : jawLen * 0.5));
            ctx.stroke();

            ctx.beginPath();
            ctx.moveTo(p.x + sidePocketR * 0.95, p.y);
            ctx.lineTo(p.x + jawSpread, p.y + (isTop ? -jawLen * 0.5 : jawLen * 0.5));
            ctx.stroke();
        });

        ctx.restore(); // Remove rail-zone clip
        
        
        // ========== 4. DRAW GREEN CUSHIONS ==========
        // Realistic rubber bumpers with 3D profile and tapered ends

        const cushionWidth = 8;
        const cushionInset = railWidth - cushionWidth / 2 - 1;

        // Helper: draw a cushion segment with 3D effect and optional tapered ends
        const drawCushionSegment = (x1, y1, x2, y2, isHorizontal, taperStart, taperEnd) => {
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
            ctx.moveTo(x1, y1 - (isHorizontal ? 2 : 0));
            ctx.lineTo(x2, y2 - (isHorizontal ? 2 : 0));
            if (!isHorizontal) {
                ctx.moveTo(x1 - 2, y1);
                ctx.lineTo(x2 - 2, y2);
            }
            ctx.stroke();

            // Subtle rubber texture line
            ctx.strokeStyle = this.darkenColor(cushionColor, 8);
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(x1, y1 + (isHorizontal ? 1 : 0));
            ctx.lineTo(x2, y2 + (isHorizontal ? 1 : 0));
            if (!isHorizontal) {
                ctx.moveTo(x1 + 1, y1);
                ctx.lineTo(x2 + 1, y2);
            }
            ctx.stroke();

            // Draw tapered/curved nose at each end (where cushion meets pocket)
            const taperLen = 8;
            const taperColor = this.darkenColor(cushionColor, 10);

            if (taperStart) {
                ctx.fillStyle = taperColor;
                ctx.beginPath();
                if (isHorizontal) {
                    // Taper curves inward toward the felt
                    ctx.moveTo(x1, y1 - cushionWidth / 2);
                    ctx.quadraticCurveTo(x1 - taperLen * 0.6, y1, x1 - taperLen, y1 + cushionWidth * 0.4);
                    ctx.lineTo(x1, y1 + cushionWidth / 2);
                    ctx.closePath();
                } else {
                    ctx.moveTo(x1 - cushionWidth / 2, y1);
                    ctx.quadraticCurveTo(x1, y1 - taperLen * 0.6, x1 + cushionWidth * 0.4, y1 - taperLen);
                    ctx.lineTo(x1 + cushionWidth / 2, y1);
                    ctx.closePath();
                }
                ctx.fill();
            }

            if (taperEnd) {
                ctx.fillStyle = taperColor;
                ctx.beginPath();
                if (isHorizontal) {
                    ctx.moveTo(x2, y2 - cushionWidth / 2);
                    ctx.quadraticCurveTo(x2 + taperLen * 0.6, y2, x2 + taperLen, y2 + cushionWidth * 0.4);
                    ctx.lineTo(x2, y2 + cushionWidth / 2);
                    ctx.closePath();
                } else {
                    ctx.moveTo(x2 - cushionWidth / 2, y2);
                    ctx.quadraticCurveTo(x2, y2 + taperLen * 0.6, x2 + cushionWidth * 0.4, y2 + taperLen);
                    ctx.lineTo(x2 + cushionWidth / 2, y2);
                    ctx.closePath();
                }
                ctx.fill();
            }
        };

        // Top cushions (tapered at both ends — pocket side)
        drawCushionSegment(
            corners[0].x + cornerPocketOpening + 3, cushionInset,
            sides[0].x - sidePocketOpening - 3, cushionInset,
            true, true, true
        );
        drawCushionSegment(
            sides[0].x + sidePocketOpening + 3, cushionInset,
            corners[1].x - cornerPocketOpening - 3, cushionInset,
            true, true, true
        );

        // Bottom cushions
        drawCushionSegment(
            corners[2].x + cornerPocketOpening + 3, height - cushionInset,
            sides[1].x - sidePocketOpening - 3, height - cushionInset,
            true, true, true
        );
        drawCushionSegment(
            sides[1].x + sidePocketOpening + 3, height - cushionInset,
            corners[3].x - cornerPocketOpening - 3, height - cushionInset,
            true, true, true
        );

        // Left cushion
        drawCushionSegment(
            cushionInset, corners[0].y + cornerPocketOpening + 3,
            cushionInset, corners[2].y - cornerPocketOpening - 3,
            false, true, true
        );

        // Right cushion
        drawCushionSegment(
            width - cushionInset, corners[1].y + cornerPocketOpening + 3,
            width - cushionInset, corners[3].y - cornerPocketOpening - 3,
            false, true, true
        );
        
            ctx.restore();
        },
    
    /**
     * Draw pockets - simple circles for physics zones
     */
    drawPockets(ctx, pockets, game) {
        // Draw pocket nets (#3)
        if (typeof PoolVFX !== 'undefined' && game) {
            PoolVFX.drawPocketNets(ctx, pockets, game.cushionMargin || 21);
        }

        // Draw pocket leather stitching (#12)
        if (typeof PoolVFX !== 'undefined' && game) {
            PoolVFX.drawPocketStitching(ctx, pockets, game.cushionMargin || 21);
        }

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

        // ===== DYNAMIC SHADOW (#8 + #14) =====
        if (typeof PoolVFX !== 'undefined') {
            PoolVFX.drawDynamicBallShadow(ctx, ball);
        } else {
            // Fallback static shadow
            ctx.save();
            ctx.fillStyle = 'rgba(0, 0, 0, 0.5)';
            ctx.beginPath();
            ctx.ellipse(ball.x + 1, ball.y + 2, ball.r * 0.5, ball.r * 0.35, 0, 0, Math.PI * 2);
            ctx.fill();

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
        }
        
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

        // ===== OVERHEAD RECTANGULAR LIGHT REFLECTION (#1) =====
        if (typeof PoolVFX !== 'undefined') {
            PoolVFX.drawOverheadLightReflection(ctx, ball);
        }

        // ===== ENVIRONMENT MAP REFLECTION BAND (#11) =====
        if (typeof PoolVFX !== 'undefined') {
            PoolVFX.drawEnvironmentReflection(ctx, ball);
        }
    },
    
    // Ball with cream base and colored center band (American/UK style)
    drawBallWithBand(ctx, ball, lightOffsetX, lightOffsetY, colorBase, colorRange) {
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();

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

        // Draw the colored center band — uses pole orientation for 3D wrapping
        this.drawColorBand(ctx, ball, lightOffsetX, lightOffsetY, colorBase, colorRange, 0.55);

        ctx.restore();
    },

    // Red Ball - cream base with maroon band
    drawUKRedBall(ctx, ball, lightOffsetX, lightOffsetY) {
        this.drawBallWithBand(ctx, ball, lightOffsetX, lightOffsetY,
            { r: 45, g: 8, b: 18 }, { r: 115, g: 32, b: 48 });
    },
    
    // Draw a colored region as proper 3D surface quads on the sphere.
    // The region is defined in the ball's LOCAL coordinate system
    // and rotated into world space by the ball's quaternion. Each visible quad
    // is shaded by the overhead light, giving proper sphere curvature.
    // colorBase/colorRange define the color: rgb = base + diffuse * range
    // bandH controls coverage: 0.55 = equator band, 0.98 = full sphere (solid)
    drawColorBand(ctx, ball, lightOffsetX, lightOffsetY, colorBase, colorRange, bandH) {
        ctx.save();

        const hasRotation = typeof PoolBallRotation !== 'undefined' && ball.rotQ;
        const q = hasRotation ? ball.rotQ : { w: 1, x: 0, y: 0, z: 0 };
        const r = ball.r;

        // Region covers local Y from -bandH to +bandH
        if (bandH === undefined) bandH = 0.55;

        // Light direction (normalized) for shading
        const lx = lightOffsetX / r, ly = lightOffsetY / r, lz = 0.85;
        const lLen = Math.sqrt(lx * lx + ly * ly + lz * lz);
        const ldx = lx / lLen, ldy = ly / lLen, ldz = lz / lLen;

        // Number of segments around the ball and across the region
        const stepsAround = 28;
        const stepsAcross = bandH > 0.8 ? 12 : 6; // more slices for full-sphere coverage

        // Pre-compute ring points for each latitude stripe in the band
        // rings[row][col] = rotated 3D point on unit sphere
        const rings = new Array(stepsAcross + 1);
        for (let row = 0; row <= stepsAcross; row++) {
            rings[row] = new Array(stepsAround + 1);
            const localY = -bandH + (row / stepsAcross) * (bandH * 2);
            const ringR = Math.sqrt(Math.max(0, 1 - localY * localY));

            for (let col = 0; col <= stepsAround; col++) {
                const t = (col / stepsAround) * Math.PI * 2;
                const localPt = {
                    x: ringR * Math.cos(t),
                    y: localY,
                    z: ringR * Math.sin(t)
                };
                rings[row][col] = PoolBallRotation.rotatePointByQuaternion(localPt, q);
            }
        }

        // Draw visible quads with per-quad lighting
        for (let row = 0; row < stepsAcross; row++) {
            for (let col = 0; col < stepsAround; col++) {
                const p00 = rings[row][col];
                const p10 = rings[row][col + 1];
                const p01 = rings[row + 1][col];
                const p11 = rings[row + 1][col + 1];

                // Average depth — skip quads on the back of the ball
                const avgZ = (p00.z + p10.z + p01.z + p11.z) * 0.25;
                if (avgZ < -0.08) continue;

                // Surface normal ≈ average position (points on unit sphere)
                const nx = (p00.x + p10.x + p01.x + p11.x) * 0.25;
                const ny = (p00.y + p10.y + p01.y + p11.y) * 0.25;
                const nz = (p00.z + p10.z + p01.z + p11.z) * 0.25;

                // Diffuse lighting
                const diffuse = Math.max(0.12, nx * ldx + ny * ldy + nz * ldz);

                // Band color modulated by light
                const cr = Math.round(colorBase.r + diffuse * colorRange.r);
                const cg = Math.round(colorBase.g + diffuse * colorRange.g);
                const cb = Math.round(colorBase.b + diffuse * colorRange.b);

                // Fade quads near the silhouette edge
                const alpha = Math.min(1, Math.max(0.15, (avgZ + 0.1) / 0.35));

                ctx.fillStyle = `rgba(${cr},${cg},${cb},${alpha})`;
                ctx.beginPath();
                ctx.moveTo(ball.x + p00.x * r, ball.y + p00.y * r);
                ctx.lineTo(ball.x + p10.x * r, ball.y + p10.y * r);
                ctx.lineTo(ball.x + p11.x * r, ball.y + p11.y * r);
                ctx.lineTo(ball.x + p01.x * r, ball.y + p01.y * r);
                ctx.closePath();
                ctx.fill();
            }
        }

        ctx.restore();
    },
    
    
    
    
    
    
    
    
    
    
    // UK Yellow Ball - solid yellow using 3D quad rendering with per-quad lighting
    drawUKYellowBall(ctx, ball, lightOffsetX, lightOffsetY) {
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        // Base fill to cover polar gaps where quads don't reach
        ctx.fillStyle = '#8a6e00';
        ctx.fill();
        this.drawColorBand(ctx, ball, lightOffsetX, lightOffsetY,
            { r: 130, g: 100, b: 0 }, { r: 125, g: 100, b: 8 }, 0.98);
        ctx.restore();
    },
    
    // Black Ball - solid black using 3D quad rendering with per-quad lighting
    drawBlackBall(ctx, ball, lightOffsetX, lightOffsetY) {
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        // Base fill to cover polar gaps where quads don't reach
        ctx.fillStyle = '#0a0a10';
        ctx.fill();
        this.drawColorBand(ctx, ball, lightOffsetX, lightOffsetY,
            { r: 5, g: 5, b: 8 }, { r: 38, g: 40, b: 46 }, 0.98);
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
    // Uses the ball's quaternion rotation to place spots accurately on the sphere surface
    drawCueBallSpots(ctx, ball) {
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();

        const hasRotation = typeof PoolBallRotation !== 'undefined' && ball.rotQ;
        const q = hasRotation ? ball.rotQ : { w: 1, x: 0, y: 0, z: 0 };

        // 6 spots fixed on the ball surface (unit sphere, like dice faces)
        const spotDefs = [
            { x: 0, y: 0, z: 1 },     // Front
            { x: 0, y: 0, z: -1 },    // Back
            { x: 1, y: 0, z: 0 },     // Right
            { x: -1, y: 0, z: 0 },    // Left
            { x: 0, y: 1, z: 0 },     // Bottom
            { x: 0, y: -1, z: 0 }     // Top
        ];

        const spotRadius = ball.r * 0.13;
        const depthFactor = 0.75;
        const rotFn = hasRotation ? PoolBallRotation.rotatePointByQuaternion.bind(PoolBallRotation) : null;

        spotDefs.forEach(localPos => {
            // Rotate spot from ball-local to world space using the quaternion
            const world = rotFn 
                ? rotFn(localPos, q) 
                : localPos; // fallback: no rotation

            // Only draw spots on the visible hemisphere
            if (world.z < -0.05) return;

            const screenX = ball.x + world.x * ball.r * depthFactor;
            const screenY = ball.y + world.y * ball.r * depthFactor;

            // Perspective: spots further from viewer are smaller and fainter
            const facing = Math.max(0, (world.z + 0.1) / 1.1);
            const scale = 0.5 + facing * 0.5;
            const alpha = Math.max(0.05, Math.min(0.85, facing));

            // Draw as a small filled circle with 3D shading
            const sr = spotRadius * scale;

            ctx.globalAlpha = alpha;

            // Spot shadow (slightly offset)
            ctx.fillStyle = 'rgba(10, 20, 30, 0.3)';
            ctx.beginPath();
            ctx.arc(screenX + 0.5, screenY + 0.5, sr * 1.05, 0, Math.PI * 2);
            ctx.fill();

            // Main spot - dark blue-gray like real Aramith cue balls
            const spotGrad = ctx.createRadialGradient(
                screenX - sr * 0.25, screenY - sr * 0.25, 0,
                screenX, screenY, sr
            );
            spotGrad.addColorStop(0, '#3a4a5a');
            spotGrad.addColorStop(0.6, '#2a3848');
            spotGrad.addColorStop(1, '#1a2838');

            ctx.fillStyle = spotGrad;
            ctx.beginPath();
            ctx.arc(screenX, screenY, sr, 0, Math.PI * 2);
            ctx.fill();

            // Tiny highlight on each spot for gloss
            ctx.fillStyle = `rgba(255, 255, 255, ${alpha * 0.2})`;
            ctx.beginPath();
            ctx.arc(screenX - sr * 0.2, screenY - sr * 0.2, sr * 0.3, 0, Math.PI * 2);
            ctx.fill();
        });

        ctx.globalAlpha = 1;
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
                const depthFactor = 0.78;
                
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

            // Small precise contact point marker
            const pulse = 0.7 + Math.sin(Date.now() / 300) * 0.3;

            // Subtle glow
            const glowGrad = ctx.createRadialGradient(
                collisionPoint.x, collisionPoint.y, 0,
                collisionPoint.x, collisionPoint.y, 14
            );
            glowGrad.addColorStop(0, `rgba(255, 215, 0, ${0.5 * pulse})`);
            glowGrad.addColorStop(1, 'rgba(255, 215, 0, 0)');
            ctx.fillStyle = glowGrad;
            ctx.beginPath();
            ctx.arc(collisionPoint.x, collisionPoint.y, 14, 0, Math.PI * 2);
            ctx.fill();

            // Diamond marker at contact point
            const s = 4;
            ctx.fillStyle = `rgba(255, 215, 0, ${0.85 * pulse})`;
            ctx.beginPath();
            ctx.moveTo(collisionPoint.x, collisionPoint.y - s);
            ctx.lineTo(collisionPoint.x + s, collisionPoint.y);
            ctx.lineTo(collisionPoint.x, collisionPoint.y + s);
            ctx.lineTo(collisionPoint.x - s, collisionPoint.y);
            ctx.closePath();
            ctx.fill();

            ctx.restore();
        }
        
        // Draw ghost ball at collision point
        if (game.showGhostBalls) {
            ctx.save();

            // Ghost ball for cue ball position at impact
            const ghostCueBallX = collisionPoint.cueBallX != null ? collisionPoint.cueBallX : (collisionPoint.x - Math.cos(impactAngle) * cueBall.r);
            const ghostCueBallY = collisionPoint.cueBallY != null ? collisionPoint.cueBallY : (collisionPoint.y - Math.sin(impactAngle) * cueBall.r);

            // Ghost cue ball — filled semi-transparent with dashed outline
            ctx.fillStyle = 'rgba(255, 255, 255, 0.15)';
            ctx.beginPath();
            ctx.arc(ghostCueBallX, ghostCueBallY, cueBall.r, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = 'rgba(255, 255, 255, 0.7)';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([4, 4]);
            ctx.beginPath();
            ctx.arc(ghostCueBallX, ghostCueBallY, cueBall.r, 0, Math.PI * 2);
            ctx.stroke();
            ctx.setLineDash([]);

            // Highlighted ring on object ball
            const objColor = this.getBallColor(objectBall) || 'rgba(255, 200, 100, 0.8)';
            ctx.strokeStyle = objColor;
            ctx.lineWidth = 2;
            ctx.beginPath();
            ctx.arc(objectBall.x, objectBall.y, objectBall.r + 3, 0, Math.PI * 2);
            ctx.stroke();

            // Line of centers (ghost cue ball → object ball) — thin solid
            ctx.strokeStyle = 'rgba(255, 215, 0, 0.35)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(ghostCueBallX, ghostCueBallY);
            ctx.lineTo(objectBall.x, objectBall.y);
            ctx.stroke();

            // === CUT ANGLE indicator ===
            const cutDx = objectBall.x - ghostCueBallX;
            const cutDy = objectBall.y - ghostCueBallY;
            const lineOfCentersAngle = Math.atan2(cutDy, cutDx);
            let cutAngleRad = Math.abs(aimAngle - lineOfCentersAngle);
            while (cutAngleRad > Math.PI) cutAngleRad = 2 * Math.PI - cutAngleRad;
            const cutAngleDeg = cutAngleRad * 180 / Math.PI;

            // Show cut fraction (full = 0°, half = 30°, quarter = ~49°, thin = 60°+)
            let cutLabel;
            if (cutAngleDeg < 5) cutLabel = 'FULL';
            else if (cutAngleDeg < 20) cutLabel = '3/4';
            else if (cutAngleDeg < 35) cutLabel = '1/2';
            else if (cutAngleDeg < 52) cutLabel = '1/4';
            else cutLabel = 'THIN';

            ctx.font = 'bold 11px Arial';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'middle';
            ctx.fillStyle = 'rgba(255, 215, 0, 0.85)';
            const labelX = (ghostCueBallX + objectBall.x) / 2;
            const labelY = (ghostCueBallY + objectBall.y) / 2 - 12;
            ctx.fillText(cutLabel + ' (' + Math.round(cutAngleDeg) + '°)', labelX, labelY);

            ctx.restore();
        }
        
        // Calculate object ball trajectory after collision using proper physics
        // The object ball travels along the line connecting the ball centers at impact
        const ghostCueBallX = collisionPoint.cueBallX;
        const ghostCueBallY = collisionPoint.cueBallY;

        if (ghostCueBallX == null || ghostCueBallY == null) return;
        
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

            // === POCKET TARGETING ===
            // Check if object ball path leads toward a pocket and highlight it
            if (game.pockets) {
                let bestPocket = null;
                let bestPocketDist = Infinity;
                const trajDirX = objNx;
                const trajDirY = objNy;

                for (const pocket of game.pockets) {
                    const toPocketX = pocket.x - objectBall.x;
                    const toPocketY = pocket.y - objectBall.y;
                    const proj = toPocketX * trajDirX + toPocketY * trajDirY;
                    if (proj < 0) continue; // Pocket is behind the trajectory

                    // Perpendicular distance from pocket center to trajectory line
                    const closestX = objectBall.x + trajDirX * proj;
                    const closestY = objectBall.y + trajDirY * proj;
                    const perpDist = Math.sqrt((pocket.x - closestX) ** 2 + (pocket.y - closestY) ** 2);
                    const pocketR = pocket.r || 28;

                    // Ball goes in if its center passes within capture range of pocket center
                    if (perpDist < pocketR * 0.8 && proj < bestPocketDist) {
                        bestPocketDist = proj;
                        bestPocket = pocket;
                    }
                }

                if (bestPocket) {
                    ctx.save();
                    const pr = (bestPocket.r || 28) + 6;
                    const pulse = 1 + Math.sin(Date.now() / 250) * 0.15;

                    // Green glow around target pocket
                    const pGrad = ctx.createRadialGradient(
                        bestPocket.x, bestPocket.y, pr * 0.3 * pulse,
                        bestPocket.x, bestPocket.y, pr * pulse
                    );
                    pGrad.addColorStop(0, 'rgba(74, 222, 128, 0.4)');
                    pGrad.addColorStop(0.6, 'rgba(74, 222, 128, 0.15)');
                    pGrad.addColorStop(1, 'rgba(74, 222, 128, 0)');
                    ctx.fillStyle = pGrad;
                    ctx.beginPath();
                    ctx.arc(bestPocket.x, bestPocket.y, pr * pulse, 0, Math.PI * 2);
                    ctx.fill();

                    // Pocket ring
                    ctx.strokeStyle = 'rgba(74, 222, 128, 0.6)';
                    ctx.lineWidth = 2;
                    ctx.beginPath();
                    ctx.arc(bestPocket.x, bestPocket.y, (bestPocket.r || 28) * pulse, 0, Math.PI * 2);
                    ctx.stroke();

                    ctx.restore();
                }
            }

            // Draw predicted trajectory path starting from object ball's CURRENT position
            // (The ball will move in this direction after being hit)
            this.drawPredictedPath(
                ctx,
                objectBall,
                trajectoryAngle,
                tableWidth,
                tableHeight,
                cushionMargin,
                game,
                allBalls
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
        // Determine active spin from the spin control
        const spinY = (typeof PoolSpinControl !== 'undefined') ? (PoolSpinControl.spinY || 0) : 0;

        // Angle between aim direction and line of centers
        let angleDiff = aimAngle - objectAngle;
        while (angleDiff > Math.PI) angleDiff -= 2 * Math.PI;
        while (angleDiff < -Math.PI) angleDiff += 2 * Math.PI;

        const cutAngle = Math.abs(angleDiff);

        // ===== STUN deflection: tangent component (perpendicular to line of centers) =====
        // This is the base 90-degree rule component
        let tangentAngle;
        if (angleDiff >= 0) {
            tangentAngle = objectAngle - Math.PI / 2;
        } else {
            tangentAngle = objectAngle + Math.PI / 2;
        }
        const tangentStrength = Math.sin(cutAngle); // 0 at full ball, 1 at 90° cut

        // ===== SPIN adjusts the deflection angle =====
        // Top spin (follow): cue ball curves toward the original aim direction
        //   → blends between tangent (90°) and follow-through (aim direction)
        // Back spin (draw): cue ball reverses along the approach line
        //   → overrides tangent with a draw-back direction
        // No spin (stun): pure tangent at 90° to object ball path

        let deflectionAngle;
        let showDeflection = true;

        if (spinY > 0.15) {
            // FOLLOW: blend from tangent toward the aim direction
            // Strength of follow depends on spin amount and cut angle
            const followBlend = Math.min(1, spinY * 2); // 0-1 blend factor
            // At full follow, the cue ball continues roughly in the aim direction
            // The cut angle still affects it — thinner cuts = less follow effect
            const followAngle = aimAngle;
            deflectionAngle = tangentAngle + (followAngle - tangentAngle) * followBlend * (1 - tangentStrength * 0.3);
        } else if (spinY < -0.15) {
            // DRAW: cue ball comes back toward the player
            const drawBlend = Math.min(1, Math.abs(spinY) * 2);
            if (cutAngle < Math.PI / 6) {
                // Thick hit: draw straight back
                deflectionAngle = aimAngle + Math.PI;
            } else {
                // Cut with draw: blend between tangent and back-direction
                const backAngle = aimAngle + Math.PI;
                deflectionAngle = tangentAngle + (backAngle - tangentAngle) * drawBlend * 0.6;
            }
        } else {
            // STUN: pure tangent (90-degree rule)
            deflectionAngle = tangentAngle;
            // Don't show for nearly straight shots (cue ball stops)
            if (tangentStrength < 0.1) showDeflection = false;
        }

        if (!showDeflection) return;

        // Use drawPredictedPath for proper cushion bounces on the cue ball path
        // Create a temporary ball object at the ghost position
        const tempCueBall = { x: ghostX, y: ghostY, r: cueBall.r, color: 'white' };

        // Scale prediction length: thinner cuts = more cue ball movement
        const baseLength = (game.trajectoryLength || 200) * 0.5;
        const lengthScale = Math.max(0.2, tangentStrength + Math.abs(spinY) * 0.5);
        const savedLength = game.trajectoryLength;
        game.trajectoryLength = baseLength * lengthScale;

        this.drawCueBallPredictedPath(ctx, tempCueBall, deflectionAngle, tableWidth, tableHeight, cushionMargin, game);

        game.trajectoryLength = savedLength;
    },

    /**
     * Draw predicted cue ball path (white dashed line with bounces)
     */
    drawCueBallPredictedPath(ctx, ball, angle, tableWidth, tableHeight, cushionMargin, game) {
        ctx.save();

        const minX = cushionMargin + ball.r;
        const maxX = tableWidth - cushionMargin - ball.r;
        const minY = cushionMargin + ball.r;
        const maxY = tableHeight - cushionMargin - ball.r;

        let x = ball.x;
        let y = ball.y;
        let dirX = Math.cos(angle);
        let dirY = Math.sin(angle);
        let remainingLength = game.trajectoryLength || 100;

        const maxBounces = 2;
        let bounceCount = 0;
        let segIdx = 0;

        while (remainingLength > 0 && bounceCount <= maxBounces) {
            let distToWall = Infinity;
            let hitWall = null;

            if (dirX > 0) { const d = (maxX - x) / dirX; if (d > 0 && d < distToWall) { distToWall = d; hitWall = 'right'; } }
            else if (dirX < 0) { const d = (minX - x) / dirX; if (d > 0 && d < distToWall) { distToWall = d; hitWall = 'left'; } }
            if (dirY > 0) { const d = (maxY - y) / dirY; if (d > 0 && d < distToWall) { distToWall = d; hitWall = 'bottom'; } }
            else if (dirY < 0) { const d = (minY - y) / dirY; if (d > 0 && d < distToWall) { distToWall = d; hitWall = 'top'; } }

            const segLen = Math.min(distToWall, remainingLength);
            const endX = x + dirX * segLen;
            const endY = y + dirY * segLen;

            // Draw segment
            const alpha = Math.max(0.15, 0.55 - segIdx * 0.15);
            ctx.strokeStyle = `rgba(255, 255, 255, ${alpha})`;
            ctx.lineWidth = 2;
            ctx.setLineDash([5, 5]);
            ctx.beginPath();
            ctx.moveTo(x, y);
            ctx.lineTo(endX, endY);
            ctx.stroke();

            x = endX;
            y = endY;
            remainingLength -= segLen;
            segIdx++;

            if (hitWall && remainingLength > 0.1) {
                bounceCount++;
                if (hitWall === 'left' || hitWall === 'right') dirX = -dirX * 0.78;
                else dirY = -dirY * 0.78;
                const mag = Math.sqrt(dirX * dirX + dirY * dirY);
                if (mag > 0) { dirX /= mag; dirY /= mag; }
                remainingLength *= 0.78;
            }
        }

        // End-point circle
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.4)';
        ctx.lineWidth = 1.5;
        ctx.setLineDash([3, 3]);
        ctx.beginPath();
        ctx.arc(x, y, ball.r, 0, Math.PI * 2);
        ctx.stroke();

        ctx.setLineDash([]);
        ctx.restore();
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
    drawPredictedPath(ctx, ball, angle, tableWidth, tableHeight, cushionMargin, game, allBalls) {
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
        const maxBounces = 3;
        let bounceCount = 0;
        let hitBall = false;

        // Trace path with cushion bounces and ball-blocking
        while (remainingLength > 0 && bounceCount < maxBounces && !hitBall) {
            // Check for blocking balls along this direction
            let distToBlocker = Infinity;
            if (allBalls) {
                for (const other of allBalls) {
                    if (other === ball || other.potted) continue;
                    // Ray from (x,y) in (dirX,dirY) direction, check circle intersection
                    const dx = other.x - x;
                    const dy = other.y - y;
                    const proj = dx * dirX + dy * dirY;
                    if (proj < 0) continue; // Behind us
                    const closestX = x + dirX * proj;
                    const closestY = y + dirY * proj;
                    const perpDistSq = (other.x - closestX) ** 2 + (other.y - closestY) ** 2;
                    const combinedR = ball.r + other.r;
                    if (perpDistSq < combinedR * combinedR) {
                        const halfChord = Math.sqrt(combinedR * combinedR - perpDistSq);
                        const hitDist = proj - halfChord;
                        if (hitDist > 1 && hitDist < distToBlocker) {
                            distToBlocker = hitDist;
                        }
                    }
                }
            }

            // Calculate distance to nearest cushion
            let distToWall = Infinity;
            let hitWall = null;

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

            // If a ball blocks before the wall, terminate at that ball
            if (distToBlocker < distToWall && distToBlocker < remainingLength) {
                const endX = x + dirX * distToBlocker;
                const endY = y + dirY * distToBlocker;
                segments.push({ startX: x, startY: y, endX, endY, bounce: bounceCount });
                hitBall = true;
                break;
            }
            
            // Determine segment length
            const segmentLength = Math.min(distToWall, remainingLength);
            const endX = x + dirX * segmentLength;
            const endY = y + dirY * segmentLength;
            
            segments.push({ startX: x, startY: y, endX, endY, bounce: bounceCount });
            
            x = endX;
            y = endY;
            remainingLength -= segmentLength;
            
            // Handle cushion bounce — only if we actually reached the wall
            // and still have remaining prediction length
            if (hitWall && remainingLength > 0.1) {
                bounceCount++;

                // Reflect direction: flip normal component, apply restitution to angle
                if (hitWall === 'left' || hitWall === 'right') {
                    dirX = -dirX * 0.78;
                } else {
                    dirY = -dirY * 0.78;
                }

                // Normalize direction back to unit length
                const mag = Math.sqrt(dirX * dirX + dirY * dirY);
                if (mag > 0) {
                    dirX /= mag;
                    dirY /= mag;
                }

                // Shorten remaining length to account for energy loss on bounce
                remainingLength *= 0.78;
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
    drawAimLine(ctx, cueBall, aimAngle, length = 300, allBalls) {
        // Calculate how far the aim line should extend
        // If there's a ball in the way, terminate at the ghost cue ball position
        let aimLength = length;

        if (allBalls) {
            const hitResult = this.findFirstBallHit(cueBall, aimAngle, allBalls);
            if (hitResult && hitResult.collisionPoint.cueBallX != null) {
                const dx = hitResult.collisionPoint.cueBallX - cueBall.x;
                const dy = hitResult.collisionPoint.cueBallY - cueBall.y;
                aimLength = Math.sqrt(dx * dx + dy * dy);
            }
        }

        // Main aim line with gradient fade — terminates at first ball
        const endX = cueBall.x + Math.cos(aimAngle) * aimLength;
        const endY = cueBall.y + Math.sin(aimAngle) * aimLength;

        const lineGrad = ctx.createLinearGradient(
            cueBall.x, cueBall.y, endX, endY
        );
        lineGrad.addColorStop(0, 'rgba(255, 255, 255, 0.8)');
        lineGrad.addColorStop(0.7, 'rgba(255, 255, 255, 0.5)');
        lineGrad.addColorStop(1, 'rgba(255, 255, 255, 0.3)');

        ctx.strokeStyle = lineGrad;
        ctx.lineWidth = 3;
        ctx.setLineDash([12, 8]);
        ctx.beginPath();
        ctx.moveTo(cueBall.x, cueBall.y);
        ctx.lineTo(endX, endY);
        ctx.stroke();
        ctx.setLineDash([]);

        // Target point indicator (close to cue ball for aiming reference)
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

        // ===== CUE INLAY PATTERNS (#13) =====
        if (typeof PoolVFX !== 'undefined') {
            PoolVFX.drawCueInlays(ctx, cueStartX, cueStartY, cueEndX, cueEndY, aimAngle);
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
