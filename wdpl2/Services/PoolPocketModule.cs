namespace Wdpl2.Services;

/// <summary>
/// Pocket rendering module for pool game - handles UK-style pocket rendering
/// </summary>
public static class PoolPocketModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL POCKET MODULE
// UK 8-ball pocket rendering with jaws
// ============================================

const PoolPockets = {
    /**
     * Draw all pockets with realistic 3D appearance
     * PHASE 3: Add chalk dust marks near pockets
     */
    drawPockets(ctx, pockets, showDebug = false) {
        pockets.forEach(pocket => {
            // PHASE 3: Draw chalk marks before pocket (so pocket renders on top)
            this.drawChalkMarks(ctx, pocket);
            
            this.drawRealisticPocket(ctx, pocket);
        });
        
        // Debug visualization if enabled
        if (showDebug) {
            pockets.forEach(p => {
                // Capture zone (physics)
                ctx.strokeStyle = 'rgba(255, 100, 100, 0.5)';
                ctx.lineWidth = 2;
                ctx.setLineDash([5, 5]);
                ctx.beginPath();
                ctx.arc(p.x, p.y, p.r, 0, Math.PI * 2);
                ctx.stroke();
                ctx.setLineDash([]);
            });
        }
    },
    
    /**
     * Draw subtle chalk dust marks near pocket
     * PHASE 3: Realistic table wear details
     */
    drawChalkMarks(ctx, pocket) {
        ctx.save();
        ctx.globalAlpha = 0.04;
        
        // Random chalk dust particles around pocket
        for (let i = 0; i < 8; i++) {
            const angle = (Math.PI * 2 * i) / 8 + Math.random() * 0.5;
            const distance = pocket.r * 1.2 + Math.random() * 15;
            const x = pocket.x + Math.cos(angle) * distance;
            const y = pocket.y + Math.sin(angle) * distance;
            const size = 1 + Math.random() * 2;
            
            // Light blue/white chalk dust
            const chalkGrad = ctx.createRadialGradient(x, y, 0, x, y, size * 2);
            chalkGrad.addColorStop(0, 'rgba(200, 220, 255, 0.6)');
            chalkGrad.addColorStop(0.5, 'rgba(180, 200, 230, 0.3)');
            chalkGrad.addColorStop(1, 'rgba(160, 180, 200, 0)');
            
            ctx.fillStyle = chalkGrad;
            ctx.beginPath();
            ctx.arc(x, y, size * 2, 0, Math.PI * 2);
            ctx.fill();
        }
        
        // Concentrated chalk area (where hands rest near pocket)
        if (pocket.type === 'corner') {
            const chalkZone = ctx.createRadialGradient(
                pocket.x, pocket.y, pocket.r * 1.5,
                pocket.x, pocket.y, pocket.r * 2.5
            );
            chalkZone.addColorStop(0, 'rgba(200, 220, 255, 0.15)');
            chalkZone.addColorStop(0.6, 'rgba(180, 200, 230, 0.08)');
            chalkZone.addColorStop(1, 'rgba(160, 180, 200, 0)');
            
            ctx.fillStyle = chalkZone;
            ctx.beginPath();
            ctx.arc(pocket.x, pocket.y, pocket.r * 2.5, 0, Math.PI * 2);
            ctx.fill();
        }
        
        ctx.restore();
    },
    
    /**
     * Draw a single pocket with 3D leather appearance and chrome bracket
     * PHASE 2: Professional quality pocket rendering
     */
    drawRealisticPocket(ctx, pocket) {
        const isCorner = pocket.type === 'corner';
        const visualRadius = pocket.r * 1.1; // Visual opening slightly larger
        
        // ===== POCKET DEPTH (DARK INNER SHADOW) =====
        // Creates the illusion of depth by having multiple shadow layers
        const depthLayers = 5;
        for (let i = depthLayers; i > 0; i--) {
            const layerRadius = visualRadius * (1 - (i * 0.12));
            const darkness = 0.15 + (i * 0.12);
            
            ctx.fillStyle = `rgba(0, 0, 0, ${darkness})`;
            ctx.beginPath();
            ctx.arc(pocket.x, pocket.y, layerRadius, 0, Math.PI * 2);
            ctx.fill();
        }
        
        // ===== LEATHER POCKET MATERIAL =====
        // Dark brown leather texture with subtle gradient
        const leatherGradient = ctx.createRadialGradient(
            pocket.x - visualRadius * 0.3, pocket.y - visualRadius * 0.3, 0,
            pocket.x, pocket.y, visualRadius
        );
        leatherGradient.addColorStop(0, '#3a2817');    // Lighter brown (highlight)
        leatherGradient.addColorStop(0.4, '#2d1f12');  // Medium brown
        leatherGradient.addColorStop(0.7, '#1f160c');  // Dark brown
        leatherGradient.addColorStop(1, '#0a0603');    // Almost black (shadow)
        
        ctx.fillStyle = leatherGradient;
        ctx.beginPath();
        ctx.arc(pocket.x, pocket.y, visualRadius * 0.95, 0, Math.PI * 2);
        ctx.fill();
        
        // ===== LEATHER TEXTURE (SUBTLE GRAIN) =====
        ctx.save();
        ctx.beginPath();
        ctx.arc(pocket.x, pocket.y, visualRadius * 0.95, 0, Math.PI * 2);
        ctx.clip();
        
        ctx.globalAlpha = 0.15;
        for (let i = 0; i < 15; i++) {
            const angle = (Math.PI * 2 * i) / 15;
            ctx.strokeStyle = i % 2 === 0 ? '#4a3520' : '#2a1f10';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.moveTo(pocket.x, pocket.y);
            ctx.lineTo(
                pocket.x + Math.cos(angle) * visualRadius,
                pocket.y + Math.sin(angle) * visualRadius
            );
            ctx.stroke();
        }
        ctx.restore();
        
        // ===== CHROME/METAL BRACKET =====
        // Corner brackets have different shapes than side pockets
        if (isCorner) {
            this.drawCornerBracket(ctx, pocket, visualRadius);
        } else {
            this.drawSideBracket(ctx, pocket, visualRadius);
        }
        
        // ===== POCKET OPENING HIGHLIGHT =====
        // Bright edge where felt meets pocket (from overhead lighting)
        ctx.strokeStyle = 'rgba(100, 150, 100, 0.3)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(pocket.x, pocket.y, visualRadius * 1.02, 0, Math.PI * 2);
        ctx.stroke();
        
        // ===== INNER SHADOW RING =====
        // Final shadow at the edge for definition
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.6)';
        ctx.lineWidth = 3;
        ctx.beginPath();
        ctx.arc(pocket.x, pocket.y, visualRadius * 0.92, 0, Math.PI * 2);
        ctx.stroke();
    },
    
    /**
     * Draw chrome corner bracket (L-shaped)
     */
    drawCornerBracket(ctx, pocket, radius) {
        const bracketSize = radius * 0.4;
        const bracketWidth = 4;
        
        // Determine corner position (0=TL, 1=TR, 2=BR, 3=BL)
        let cornerType = 0; // Default top-left
        if (pocket.x > 500 && pocket.y < 100) cornerType = 1; // Top-right
        else if (pocket.x > 500 && pocket.y > 400) cornerType = 2; // Bottom-right
        else if (pocket.x < 100 && pocket.y > 400) cornerType = 3; // Bottom-left
        
        // Chrome gradient (metallic appearance)
        const chromeGrad = ctx.createLinearGradient(
            pocket.x - bracketSize, pocket.y - bracketSize,
            pocket.x + bracketSize, pocket.y + bracketSize
        );
        chromeGrad.addColorStop(0, '#c0c0c0');
        chromeGrad.addColorStop(0.3, '#e8e8e8');
        chromeGrad.addColorStop(0.5, '#ffffff');
        chromeGrad.addColorStop(0.7, '#d0d0d0');
        chromeGrad.addColorStop(1, '#a0a0a0');
        
        ctx.strokeStyle = chromeGrad;
        ctx.lineWidth = bracketWidth;
        ctx.lineCap = 'round';
        ctx.lineJoin = 'round';
        
        // Draw L-shaped bracket based on corner
        ctx.beginPath();
        switch (cornerType) {
            case 0: // Top-left
                ctx.moveTo(pocket.x - radius - 5, pocket.y - radius + bracketSize);
                ctx.lineTo(pocket.x - radius - 5, pocket.y - radius - 5);
                ctx.lineTo(pocket.x - radius + bracketSize, pocket.y - radius - 5);
                break;
            case 1: // Top-right
                ctx.moveTo(pocket.x + radius - bracketSize, pocket.y - radius - 5);
                ctx.lineTo(pocket.x + radius + 5, pocket.y - radius - 5);
                ctx.lineTo(pocket.x + radius + 5, pocket.y - radius + bracketSize);
                break;
            case 2: // Bottom-right
                ctx.moveTo(pocket.x + radius + 5, pocket.y + radius - bracketSize);
                ctx.lineTo(pocket.x + radius + 5, pocket.y + radius + 5);
                ctx.lineTo(pocket.x + radius - bracketSize, pocket.y + radius + 5);
                break;
            case 3: // Bottom-left
                ctx.moveTo(pocket.x - radius + bracketSize, pocket.y + radius + 5);
                ctx.lineTo(pocket.x - radius - 5, pocket.y + radius + 5);
                ctx.lineTo(pocket.x - radius - 5, pocket.y + radius - bracketSize);
                break;
        }
        ctx.stroke();
        
        // Chrome highlight (shiny edge)
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.8)';
        ctx.lineWidth = 1.5;
        ctx.stroke();
    },
    
    /**
     * Draw chrome side bracket (arc-shaped)
     */
    drawSideBracket(ctx, pocket, radius) {
        const bracketArc = Math.PI / 3; // 60 degrees
        const bracketWidth = 5;
        
        // Determine if top or bottom middle pocket
        const isTop = pocket.y < 200;
        const startAngle = isTop ? Math.PI * 0.7 : -Math.PI * 0.3;
        const endAngle = isTop ? Math.PI * 0.3 : Math.PI * 0.7;
        
        // Chrome gradient
        const chromeGrad = ctx.createRadialGradient(
            pocket.x, pocket.y, radius * 0.8,
            pocket.x, pocket.y, radius * 1.2
        );
        chromeGrad.addColorStop(0, '#ffffff');
        chromeGrad.addColorStop(0.4, '#e0e0e0');
        chromeGrad.addColorStop(0.7, '#c0c0c0');
        chromeGrad.addColorStop(1, '#a0a0a0');
        
        // Main bracket arc
        ctx.strokeStyle = chromeGrad;
        ctx.lineWidth = bracketWidth;
        ctx.lineCap = 'round';
        ctx.beginPath();
        ctx.arc(pocket.x, pocket.y, radius * 1.05, startAngle, endAngle);
        ctx.stroke();
        
        // Highlight
        ctx.strokeStyle = 'rgba(255, 255, 255, 0.7)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(pocket.x, pocket.y, radius * 1.05, startAngle, endAngle);
        ctx.stroke();
    },
    
    /**
     * Draw pocket size label
     */
    drawPocketLabel(ctx, pocket, isCorner, pocketRadius) {
        ctx.fillStyle = 'rgba(255, 255, 255, 0.3)';
        ctx.font = '9px Arial';
        ctx.textAlign = 'center';
        // Supreme specifications: Corner 85mm, Centre 95mm
        const size = isCorner ? '85mm' : '95mm';
        ctx.fillText(size, pocket.x, pocket.y + pocketRadius + 14);
        ctx.font = '7px Arial';
        ctx.fillStyle = 'rgba(255, 255, 255, 0.2)';
        ctx.fillText('Supreme', pocket.x, pocket.y + pocketRadius + 22);
    }
};
";
    }
}
