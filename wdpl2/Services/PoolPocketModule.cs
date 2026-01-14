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
     * Draw UK-style pockets with rounded jaws and realistic depth
     */
    drawPockets(ctx, pockets) {
        pockets.forEach(p => {
            const pocketRadius = p.r || 22.2;  // Default to middle pocket size
            const isCorner = p.type === 'corner';
            
            // Draw pocket jaws (shoulders) - UK specification
            if (isCorner) {
                this.drawCornerPocketJaws(ctx, p, pocketRadius);
            } else {
                this.drawMiddlePocketJaws(ctx, p, pocketRadius);
            }
            
            // Outer shadow/glow - creates depth effect
            const outerGrad = ctx.createRadialGradient(p.x, p.y, pocketRadius, p.x, p.y, pocketRadius + 8);
            outerGrad.addColorStop(0, 'rgba(0, 0, 0, 0.6)');
            outerGrad.addColorStop(1, 'rgba(0, 0, 0, 0)');
            ctx.fillStyle = outerGrad;
            ctx.beginPath();
            ctx.arc(p.x, p.y, pocketRadius + 8, 0, Math.PI * 2);
            ctx.fill();
            
            // Leather/rubber ring around pocket edge
            const ringGrad = ctx.createRadialGradient(
                p.x, p.y, pocketRadius - 3, 
                p.x, p.y, pocketRadius + 2
            );
            ringGrad.addColorStop(0, '#2a2a2a');
            ringGrad.addColorStop(0.5, '#1a1a1a');
            ringGrad.addColorStop(1, '#0a0a0a');
            ctx.fillStyle = ringGrad;
            ctx.beginPath();
            ctx.arc(p.x, p.y, pocketRadius + 2, 0, Math.PI * 2);
            ctx.fill();
            
            // Inner pocket depth - looks like a hole
            const pocketGrad = ctx.createRadialGradient(
                p.x - 3, p.y - 3, 0,
                p.x, p.y, pocketRadius
            );
            pocketGrad.addColorStop(0, '#1a1a1a');
            pocketGrad.addColorStop(0.7, '#0a0a0a');
            pocketGrad.addColorStop(1, '#000000');
            
            ctx.fillStyle = pocketGrad;
            ctx.beginPath();
            ctx.arc(p.x, p.y, pocketRadius, 0, Math.PI * 2);
            ctx.fill();
            
            // Subtle rim highlight showing tight opening
            ctx.strokeStyle = 'rgba(80, 80, 80, 0.4)';
            ctx.lineWidth = 1;
            ctx.beginPath();
            ctx.arc(p.x - 2, p.y - 2, pocketRadius - 2, 0, Math.PI * 2);
            ctx.stroke();
            
            // Draw size label for UK specification
            if (p.type) {
                this.drawPocketLabel(ctx, p, isCorner, pocketRadius);
            }
        });
    },
    
    /**
     * Draw corner pocket jaws (rounded Supreme shoulders)
     */
    drawCornerPocketJaws(ctx, pocket, pocketRadius) {
        const taperDist = pocket.taperDist || 3.0; // 3 inch taper for corners
        const taperPx = taperDist * (1000 / 72); // Convert to pixels
        const shoulderRadius = pocketRadius * 0.4; // Rounded shoulder curve
        
        ctx.save();
        ctx.translate(pocket.x, pocket.y);
        
        // Determine corner rotation based on position
        let rotation = 0;
        if (pocket.x < 100 && pocket.y < 100) rotation = 0; // Top-left
        else if (pocket.x > 900 && pocket.y < 100) rotation = Math.PI / 2; // Top-right
        else if (pocket.x < 100 && pocket.y > 400) rotation = -Math.PI / 2; // Bottom-left
        else if (pocket.x > 900 && pocket.y > 400) rotation = Math.PI; // Bottom-right
        
        ctx.rotate(rotation);
        
        // Draw rounded Supreme shoulders (pronounced convex curves)
        // These prevent cheating the pocket - balls bounce away
        
        // Left shoulder curve
        ctx.fillStyle = '#8B4513';
        ctx.beginPath();
        ctx.arc(-pocketRadius - shoulderRadius, -pocketRadius, shoulderRadius, 0, Math.PI / 2);
        ctx.lineTo(-pocketRadius, -pocketRadius - shoulderRadius);
        ctx.closePath();
        ctx.fill();
        
        // Top shoulder curve
        ctx.beginPath();
        ctx.arc(-pocketRadius, -pocketRadius - shoulderRadius, shoulderRadius, Math.PI / 2, Math.PI);
        ctx.lineTo(-pocketRadius - shoulderRadius, -pocketRadius);
        ctx.closePath();
        ctx.fill();
        
        // Draw cushion taper (starts 3 inches from end for corners)
        const cushionGrad = ctx.createLinearGradient(-pocketRadius, 0, -pocketRadius - taperPx, 0);
        cushionGrad.addColorStop(0, '#A0522D');
        cushionGrad.addColorStop(1, '#8B4513');
        
        // Left cushion with taper
        ctx.fillStyle = cushionGrad;
        ctx.fillRect(-pocketRadius - taperPx, -pocketRadius - 5, taperPx, 10);
        
        // Top cushion with taper
        const cushionGrad2 = ctx.createLinearGradient(0, -pocketRadius, 0, -pocketRadius - taperPx);
        cushionGrad2.addColorStop(0, '#A0522D');
        cushionGrad2.addColorStop(1, '#8B4513');
        ctx.fillStyle = cushionGrad2;
        ctx.fillRect(-pocketRadius - 5, -pocketRadius - taperPx, 10, taperPx);
        
        // Highlight rounded shoulders
        ctx.strokeStyle = 'rgba(139, 69, 19, 0.5)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(-pocketRadius - shoulderRadius, -pocketRadius, shoulderRadius, 0, Math.PI / 2);
        ctx.stroke();
        
        ctx.beginPath();
        ctx.arc(-pocketRadius, -pocketRadius - shoulderRadius, shoulderRadius, Math.PI / 2, Math.PI);
        ctx.stroke();
        
        ctx.restore();
    },
    
    /**
     * Draw middle pocket jaws (rounded Supreme shoulders)
     */
    drawMiddlePocketJaws(ctx, pocket, pocketRadius) {
        const taperDist = pocket.taperDist || 2.5; // 2.5 inch taper for centre
        const taperPx = taperDist * (1000 / 72); // Convert to pixels
        const shoulderRadius = pocketRadius * 0.35; // Rounded shoulder curve
        
        // Draw rounded Supreme shoulders on both sides
        // Centre pockets are WIDER (95mm vs 85mm corners)
        
        // Left rounded shoulder
        ctx.fillStyle = '#8B4513';
        ctx.beginPath();
        ctx.arc(pocket.x - pocketRadius - shoulderRadius, pocket.y, shoulderRadius, -Math.PI / 2, Math.PI / 2);
        ctx.lineTo(pocket.x - pocketRadius, pocket.y + shoulderRadius);
        ctx.lineTo(pocket.x - pocketRadius, pocket.y - shoulderRadius);
        ctx.closePath();
        ctx.fill();
        
        // Right rounded shoulder
        ctx.beginPath();
        ctx.arc(pocket.x + pocketRadius + shoulderRadius, pocket.y, shoulderRadius, Math.PI / 2, -Math.PI / 2);
        ctx.lineTo(pocket.x + pocketRadius, pocket.y - shoulderRadius);
        ctx.lineTo(pocket.x + pocketRadius, pocket.y + shoulderRadius);
        ctx.closePath();
        ctx.fill();
        
        // Draw cushion taper (starts 2.5 inches from end for centre)
        const cushionGrad1 = ctx.createLinearGradient(pocket.x - pocketRadius, pocket.y, pocket.x - pocketRadius - taperPx, pocket.y);
        cushionGrad1.addColorStop(0, '#A0522D');
        cushionGrad1.addColorStop(1, '#8B4513');
        
        // Left cushion with taper
        ctx.fillStyle = cushionGrad1;
        ctx.fillRect(pocket.x - pocketRadius - taperPx, pocket.y - 6, taperPx, 12);
        
        const cushionGrad2 = ctx.createLinearGradient(pocket.x + pocketRadius, pocket.y, pocket.x + pocketRadius + taperPx, pocket.y);
        cushionGrad2.addColorStop(0, '#A0522D');
        cushionGrad2.addColorStop(1, '#8B4513');
        
        // Right cushion with taper
        ctx.fillStyle = cushionGrad2;
        ctx.fillRect(pocket.x + pocketRadius, pocket.y - 6, taperPx, 12);
        
        // Highlight rounded shoulders
        ctx.strokeStyle = 'rgba(139, 69, 19, 0.5)';
        ctx.lineWidth = 2;
        ctx.beginPath();
        ctx.arc(pocket.x - pocketRadius - shoulderRadius, pocket.y, shoulderRadius, -Math.PI / 2, Math.PI / 2);
        ctx.stroke();
        
        ctx.beginPath();
        ctx.arc(pocket.x + pocketRadius + shoulderRadius, pocket.y, shoulderRadius, Math.PI / 2, -Math.PI / 2);
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
