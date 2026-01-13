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
     * Draw corner pocket jaws (angled cushions)
     */
    drawCornerPocketJaws(ctx, pocket, pocketRadius) {
        const jawLength = pocketRadius * 1.4;
        const jawWidth = 10;
        const taperWidth = 4; // Taper into pocket
        
        ctx.save();
        ctx.translate(pocket.x, pocket.y);
        
        // Determine corner rotation based on position
        let rotation = 0;
        if (pocket.x < 100 && pocket.y < 100) rotation = 0; // Top-left
        else if (pocket.x > 900 && pocket.y < 100) rotation = Math.PI / 2; // Top-right
        else if (pocket.x < 100 && pocket.y > 400) rotation = -Math.PI / 2; // Bottom-left
        else if (pocket.x > 900 && pocket.y > 400) rotation = Math.PI; // Bottom-right
        
        ctx.rotate(rotation);
        
        // Draw tapered edges leading into pocket (creates wider opening effect)
        ctx.fillStyle = '#6B4423';
        ctx.beginPath();
        ctx.moveTo(-pocketRadius - taperWidth, -pocketRadius - taperWidth);
        ctx.lineTo(-pocketRadius + taperWidth, -pocketRadius + taperWidth);
        ctx.lineTo(-pocketRadius - jawLength, -pocketRadius);
        ctx.closePath();
        ctx.fill();
        
        ctx.beginPath();
        ctx.moveTo(-pocketRadius - taperWidth, -pocketRadius - taperWidth);
        ctx.lineTo(-pocketRadius + taperWidth, -pocketRadius + taperWidth);
        ctx.lineTo(-pocketRadius, -pocketRadius - jawLength);
        ctx.closePath();
        ctx.fill();
        
        // Left jaw cushion
        const jawGrad1 = ctx.createLinearGradient(-jawLength, 0, 0, 0);
        jawGrad1.addColorStop(0, '#8B4513');
        jawGrad1.addColorStop(1, '#A0522D');
        ctx.fillStyle = jawGrad1;
        ctx.fillRect(-jawLength, -pocketRadius, jawLength, jawWidth);
        
        // Top jaw cushion
        const jawGrad2 = ctx.createLinearGradient(0, -jawLength, 0, 0);
        jawGrad2.addColorStop(0, '#8B4513');
        jawGrad2.addColorStop(1, '#A0522D');
        ctx.fillStyle = jawGrad2;
        ctx.fillRect(-pocketRadius, -jawLength, jawWidth, jawLength);
        
        // Rounded corner where jaws meet
        ctx.fillStyle = '#8B4513';
        ctx.beginPath();
        ctx.arc(-pocketRadius + jawWidth/2, -pocketRadius + jawWidth/2, jawWidth/2, 0, Math.PI * 2);
        ctx.fill();
        
        ctx.restore();
    },
    
    /**
     * Draw middle pocket jaws (straight sides)
     */
    drawMiddlePocketJaws(ctx, pocket, pocketRadius) {
        const jawLength = pocketRadius * 1.0;
        const jawWidth = 8;
        const taperWidth = 3;
        
        // Draw tapered edges (chamfers) leading to pocket
        ctx.fillStyle = '#6B4423';
        
        // Left taper
        ctx.beginPath();
        ctx.moveTo(pocket.x - pocketRadius - taperWidth, pocket.y - taperWidth);
        ctx.lineTo(pocket.x - pocketRadius + taperWidth, pocket.y + taperWidth);
        ctx.lineTo(pocket.x - pocketRadius - jawLength, pocket.y + jawWidth/2);
        ctx.lineTo(pocket.x - pocketRadius - jawLength, pocket.y - jawWidth/2);
        ctx.closePath();
        ctx.fill();
        
        // Right taper
        ctx.beginPath();
        ctx.moveTo(pocket.x + pocketRadius + taperWidth, pocket.y - taperWidth);
        ctx.lineTo(pocket.x + pocketRadius - taperWidth, pocket.y + taperWidth);
        ctx.lineTo(pocket.x + pocketRadius + jawLength, pocket.y + jawWidth/2);
        ctx.lineTo(pocket.x + pocketRadius + jawLength, pocket.y - jawWidth/2);
        ctx.closePath();
        ctx.fill();
        
        // Left jaw cushion
        ctx.fillStyle = '#8B4513';
        ctx.fillRect(pocket.x - pocketRadius - jawLength, pocket.y - jawWidth/2, jawLength, jawWidth);
        
        // Right jaw cushion
        ctx.fillRect(pocket.x + pocketRadius, pocket.y - jawWidth/2, jawLength, jawWidth);
    },
    
    /**
     * Draw pocket size label
     */
    drawPocketLabel(ctx, pocket, isCorner, pocketRadius) {
        ctx.fillStyle = 'rgba(255, 255, 255, 0.2)';
        ctx.font = '8px Arial';
        ctx.textAlign = 'center';
        const size = isCorner ? '3.5in' : '3.2in';
        ctx.fillText(size, pocket.x, pocket.y + pocketRadius + 12);
    }
};
";
    }
}
