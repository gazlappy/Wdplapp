namespace Wdpl2.Services;

/// <summary>
/// Visual effects module for pool game — particle systems, collision flashes,
/// cushion compression, and ball settle animations.
/// </summary>
public static class PoolVisualEffectsModule
{
    public static string GenerateJavaScript()
    {
        return """
// ============================================
// POOL VISUAL EFFECTS MODULE
// Particles, flashes, and dynamic animations
// ============================================

const PoolVFX = {
    // Particle arrays
    chalkParticles: [],
    collisionFlashes: [],
    cushionCompressions: [],

    // Pre-rendered felt noise texture (OffscreenCanvas)
    feltNoiseCanvas: null,
    feltNoiseReady: false,
    woodGrainCanvas: null,
    woodGrainReady: false,

    // Light temperature setting
    lightTemperature: 'warm', // 'warm', 'cool', 'neutral'

    // Feature toggles (controlled from dev settings)
    enableFeltNoise: true,
    enableCushionShadows: true,
    enablePocketNets: true,
    enablePocketStitching: true,
    enableWoodGrain: true,
    enableTableBevel: true,
    enableOverheadReflection: true,
    enableEnvironmentReflection: true,
    enableDynamicShadows: true,
    enableBallSettle: true,
    enableChalkDust: true,
    enableCollisionFlash: true,
    enableCushionCompression: true,
    enableCueInlays: true,

    // Intensity controls
    cushionShadowAlpha: 0.22,
    cushionShadowDepth: 18,
    feltNoiseAlpha: 0.12,
    
    /**
     * Initialize the effects system and pre-render textures
     */
    init() {
        this.generateFeltNoiseTexture();
        this.generateWoodGrainTexture();
        console.log('[PoolVFX] initialized — feltNoise:', this.feltNoiseReady, 'woodGrain:', this.woodGrainReady);
    },
    
    // ==========================================
    // #4  PRE-RENDERED FELT NOISE TEXTURE
    // ==========================================
    generateFeltNoiseTexture() {
        try {
            this.feltNoiseCanvas = document.createElement('canvas');
            this.feltNoiseCanvas.width = 1000;
            this.feltNoiseCanvas.height = 500;
            const ctx = this.feltNoiseCanvas.getContext('2d');
            
            // Generate organic noise
            const imageData = ctx.createImageData(1000, 500);
            const data = imageData.data;
            
            for (let i = 0; i < data.length; i += 4) {
                const px = (i / 4) % 1000;
                const py = Math.floor((i / 4) / 1000);
                
                // Multi-octave noise for organic feel
                const n1 = Math.sin(px * 0.05) * Math.cos(py * 0.07) * 0.3;
                const n2 = Math.sin(px * 0.13 + py * 0.11) * 0.2;
                const n3 = (Math.random() - 0.5) * 0.5;
                const noise = (n1 + n2 + n3) * 0.33;
                
                // Directional nap (fibers aligned along length)
                const nap = Math.sin(px * 0.02 + noise * 2) * 0.15;
                
                const val = 128 + Math.round((noise + nap) * 40);
                data[i] = val;
                data[i + 1] = val;
                data[i + 2] = val;
                data[i + 3] = 50; // Moderate alpha for visible texture
            }
            
            ctx.putImageData(imageData, 0, 0);
            this.feltNoiseReady = true;
        } catch (e) {
            console.warn('Failed to generate felt noise texture:', e);
        }
    },
    
    // ==========================================
    // #9  PRE-RENDERED WOOD GRAIN TEXTURE
    // ==========================================
    generateWoodGrainTexture() {
        try {
            this.woodGrainCanvas = document.createElement('canvas');
            this.woodGrainCanvas.width = 200;
            this.woodGrainCanvas.height = 40;
            const ctx = this.woodGrainCanvas.getContext('2d');
            
            // Base wood color
            ctx.fillStyle = '#7a5c38';
            ctx.fillRect(0, 0, 200, 40);
            
            // Draw grain lines
            for (let i = 0; i < 30; i++) {
                const y = Math.random() * 40;
                const width = 0.5 + Math.random() * 1.5;
                const alpha = 0.1 + Math.random() * 0.2;
                const dark = Math.random() > 0.5;
                
                ctx.strokeStyle = dark
                    ? `rgba(60, 40, 20, ${alpha})`
                    : `rgba(140, 110, 70, ${alpha})`;
                ctx.lineWidth = width;
                ctx.beginPath();
                
                let x = 0;
                ctx.moveTo(x, y);
                while (x < 200) {
                    x += 5 + Math.random() * 15;
                    const yOff = y + (Math.random() - 0.5) * 4;
                    ctx.lineTo(x, yOff);
                }
                ctx.stroke();
            }
            
            // Knot hint
            if (Math.random() > 0.4) {
                const kx = 40 + Math.random() * 120;
                const ky = 10 + Math.random() * 20;
                ctx.strokeStyle = 'rgba(50, 30, 15, 0.2)';
                ctx.lineWidth = 1;
                ctx.beginPath();
                ctx.ellipse(kx, ky, 6 + Math.random() * 4, 3, 0, 0, Math.PI * 2);
                ctx.stroke();
            }
            
            this.woodGrainReady = true;
        } catch (e) {
            console.warn('Failed to generate wood grain texture:', e);
        }
    },
    
    /**
     * Draw the pre-rendered felt noise texture onto the table
     */
    drawFeltNoise(ctx, feltInset, feltWidth, feltHeight) {
        if (!this.enableFeltNoise || !this.feltNoiseReady || !this.feltNoiseCanvas) return;
        
        ctx.save();
        ctx.globalAlpha = this.feltNoiseAlpha;
        ctx.drawImage(this.feltNoiseCanvas, feltInset, feltInset, feltWidth, feltHeight);
        ctx.restore();
    },
    
    /**
     * Draw pre-rendered wood grain on a rail segment
     */
    drawRailWoodGrain(ctx, x, y, width, height) {
        if (!this.enableWoodGrain || !this.woodGrainReady || !this.woodGrainCanvas) return;
        
        ctx.save();
        ctx.globalAlpha = 0.35;
        ctx.globalCompositeOperation = 'multiply';

        // Tile the pattern
        const pattern = ctx.createPattern(this.woodGrainCanvas, 'repeat');
        if (pattern) {
            ctx.fillStyle = pattern;
            ctx.fillRect(x, y, width, height);
        }
        ctx.restore();
    },
    
    // ==========================================
    // #5  CHALK DUST PARTICLES
    // ==========================================
    spawnChalkDust(x, y, power) {
        if (!this.enableChalkDust) return;
        const count = Math.floor(power * 8) + 3;
        for (let i = 0; i < count; i++) {
            this.chalkParticles.push({
                x: x + (Math.random() - 0.5) * 6,
                y: y + (Math.random() - 0.5) * 6,
                vx: (Math.random() - 0.5) * power * 1.5,
                vy: (Math.random() - 0.5) * power * 1.5 - Math.random() * 2,
                life: 1.0,
                decay: 0.015 + Math.random() * 0.02,
                size: 1 + Math.random() * 2.5,
                color: Math.random() > 0.3
                    ? `rgba(100, 160, 220, `  // Blue chalk
                    : `rgba(140, 190, 240, `  // Lighter blue
            });
        }
    },
    
    updateAndDrawChalkDust(ctx) {
        for (let i = this.chalkParticles.length - 1; i >= 0; i--) {
            const p = this.chalkParticles[i];
            p.x += p.vx;
            p.y += p.vy;
            p.vx *= 0.96;
            p.vy *= 0.96;
            p.vy -= 0.03; // Slight upward drift
            p.life -= p.decay;
            
            if (p.life <= 0) {
                this.chalkParticles.splice(i, 1);
                continue;
            }
            
            ctx.fillStyle = p.color + (p.life * 0.6).toFixed(2) + ')';
            ctx.beginPath();
            ctx.arc(p.x, p.y, p.size * p.life, 0, Math.PI * 2);
            ctx.fill();
        }
    },
    
    // ==========================================
    // #6  BALL COLLISION FLASH
    // ==========================================
    spawnCollisionFlash(x, y, intensity) {
        if (!this.enableCollisionFlash) return;
        this.collisionFlashes.push({
            x, y,
            life: 1.0,
            decay: 0.12 + intensity * 0.05,
            size: 4 + intensity * 12,
            intensity: Math.min(1.0, intensity)
        });
    },
    
    updateAndDrawCollisionFlashes(ctx) {
        for (let i = this.collisionFlashes.length - 1; i >= 0; i--) {
            const f = this.collisionFlashes[i];
            f.life -= f.decay;
            
            if (f.life <= 0) {
                this.collisionFlashes.splice(i, 1);
                continue;
            }
            
            const alpha = f.life * f.intensity;
            const size = f.size * (2 - f.life); // Expands as it fades
            
            // Bright core
            const grad = ctx.createRadialGradient(f.x, f.y, 0, f.x, f.y, size);
            grad.addColorStop(0, `rgba(255, 255, 255, ${alpha})`);
            grad.addColorStop(0.3, `rgba(255, 240, 200, ${alpha * 0.7})`);
            grad.addColorStop(0.6, `rgba(255, 220, 150, ${alpha * 0.3})`);
            grad.addColorStop(1, `rgba(255, 200, 100, 0)`);
            
            ctx.fillStyle = grad;
            ctx.beginPath();
            ctx.arc(f.x, f.y, size, 0, Math.PI * 2);
            ctx.fill();
        }
    },
    
    // ==========================================
    // #7  CUSHION COMPRESSION ANIMATION
    // ==========================================
    spawnCushionCompression(x, y, side, intensity) {
        if (!this.enableCushionCompression) return;
        this.cushionCompressions.push({
            x, y, side,
            life: 1.0,
            decay: 0.08,
            maxDeform: Math.min(5, intensity * 3),
            intensity: Math.min(1.0, intensity)
        });
    },
    
    updateAndDrawCushionCompressions(ctx, cushionMargin) {
        for (let i = this.cushionCompressions.length - 1; i >= 0; i--) {
            const c = this.cushionCompressions[i];
            c.life -= c.decay;
            
            if (c.life <= 0) {
                this.cushionCompressions.splice(i, 1);
                continue;
            }
            
            // Spring-back curve: fast compression, slower release
            const deform = c.maxDeform * c.life * Math.sin(c.life * Math.PI);
            const alpha = c.life * 0.4 * c.intensity;
            
            ctx.save();
            ctx.fillStyle = `rgba(20, 100, 40, ${alpha})`;
            
            const w = 18;
            const h = deform;
            
            if (c.side === 'top') {
                ctx.fillRect(c.x - w / 2, cushionMargin - 2, w, h);
            } else if (c.side === 'bottom') {
                ctx.fillRect(c.x - w / 2, ctx.canvas.height - cushionMargin + 2 - h, w, h);
            } else if (c.side === 'left') {
                ctx.fillRect(cushionMargin - 2, c.y - w / 2, h, w);
            } else if (c.side === 'right') {
                ctx.fillRect(ctx.canvas.width - cushionMargin + 2 - h, c.y - w / 2, h, w);
            }
            
            ctx.restore();
        }
    },
    
    // ==========================================
    // #2  CUSHION-CAST SHADOWS ONTO FELT
    // ==========================================
    drawCushionShadows(ctx, width, height, cushionMargin) {
        if (!this.enableCushionShadows) return;
        const shadowDepth = this.cushionShadowDepth;
        const alpha = this.cushionShadowAlpha;
        
        // Top cushion shadow (falls downward)
        const topShadow = ctx.createLinearGradient(0, cushionMargin, 0, cushionMargin + shadowDepth);
        topShadow.addColorStop(0, `rgba(0, 0, 0, ${alpha})`);
        topShadow.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = topShadow;
        ctx.fillRect(cushionMargin, cushionMargin, width - cushionMargin * 2, shadowDepth);
        
        // Bottom cushion shadow (falls upward)
        const bottomShadow = ctx.createLinearGradient(0, height - cushionMargin, 0, height - cushionMargin - shadowDepth);
        bottomShadow.addColorStop(0, `rgba(0, 0, 0, ${alpha})`);
        bottomShadow.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = bottomShadow;
        ctx.fillRect(cushionMargin, height - cushionMargin - shadowDepth, width - cushionMargin * 2, shadowDepth);
        
        // Left cushion shadow (falls rightward)
        const leftShadow = ctx.createLinearGradient(cushionMargin, 0, cushionMargin + shadowDepth, 0);
        leftShadow.addColorStop(0, `rgba(0, 0, 0, ${alpha})`);
        leftShadow.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = leftShadow;
        ctx.fillRect(cushionMargin, cushionMargin, shadowDepth, height - cushionMargin * 2);
        
        // Right cushion shadow (falls leftward)
        const rightShadow = ctx.createLinearGradient(width - cushionMargin, 0, width - cushionMargin - shadowDepth, 0);
        rightShadow.addColorStop(0, `rgba(0, 0, 0, ${alpha})`);
        rightShadow.addColorStop(1, 'rgba(0, 0, 0, 0)');
        ctx.fillStyle = rightShadow;
        ctx.fillRect(width - cushionMargin - shadowDepth, cushionMargin, shadowDepth, height - cushionMargin * 2);
    },
    
    // ==========================================
    // #3  POCKET NETS / BALL CATCHERS
    // ==========================================
    drawPocketNets(ctx, pockets, cushionMargin) {
        if (!this.enablePocketNets || !pockets) return;
        
        pockets.forEach((p, idx) => {
            const isCorner = idx < 4;
            const netRadius = isCorner ? cushionMargin * 0.85 : cushionMargin * 0.7;
            
            ctx.save();
            
            // Dark pocket depth behind net
            ctx.fillStyle = 'rgba(5, 3, 2, 0.6)';
            ctx.beginPath();
            ctx.arc(p.x, p.y, netRadius * 0.7, 0, Math.PI * 2);
            ctx.fill();
            
            // Net mesh pattern
            ctx.strokeStyle = 'rgba(80, 50, 30, 0.5)';
            ctx.lineWidth = 1.2;
            
            const segments = 8;
            const rings = 3;
            
            // Radial threads
            for (let i = 0; i < segments; i++) {
                const angle = (i / segments) * Math.PI * 2;
                ctx.beginPath();
                ctx.moveTo(p.x, p.y);
                ctx.lineTo(
                    p.x + Math.cos(angle) * netRadius * 0.6,
                    p.y + Math.sin(angle) * netRadius * 0.6
                );
                ctx.stroke();
            }
            
            // Concentric rings (sag in the middle for 3D look)
            for (let r = 1; r <= rings; r++) {
                const ringR = (r / rings) * netRadius * 0.55;
                ctx.beginPath();
                ctx.arc(p.x, p.y, ringR, 0, Math.PI * 2);
                ctx.stroke();
            }
            
            // Leather rim reinforcement
            ctx.strokeStyle = 'rgba(60, 35, 15, 0.3)';
            ctx.lineWidth = 2.5;
            ctx.beginPath();
            ctx.arc(p.x, p.y, netRadius * 0.62, 0, Math.PI * 2);
            ctx.stroke();
            
            ctx.restore();
        });
    },
    
    // ==========================================
    // #10  TABLE EDGE BEVEL / CHAMFER
    // ==========================================
    drawTableBevel(ctx, width, height, cushionMargin) {
        if (!this.enableTableBevel) return;
        ctx.save();
        
        // Inner bevel highlight (where rail meets cushion)
        ctx.strokeStyle = 'rgba(255, 240, 210, 0.25)';
        ctx.lineWidth = 2;
        
        // Top edge
        ctx.beginPath();
        ctx.moveTo(cushionMargin + 30, cushionMargin + 1);
        ctx.lineTo(width - cushionMargin - 30, cushionMargin + 1);
        ctx.stroke();
        
        // Bottom edge
        ctx.beginPath();
        ctx.moveTo(cushionMargin + 30, height - cushionMargin - 1);
        ctx.lineTo(width - cushionMargin - 30, height - cushionMargin - 1);
        ctx.stroke();
        
        // Left edge
        ctx.beginPath();
        ctx.moveTo(cushionMargin + 1, cushionMargin + 30);
        ctx.lineTo(cushionMargin + 1, height - cushionMargin - 30);
        ctx.stroke();
        
        // Right edge
        ctx.beginPath();
        ctx.moveTo(width - cushionMargin - 1, cushionMargin + 30);
        ctx.lineTo(width - cushionMargin - 1, height - cushionMargin - 30);
        ctx.stroke();
        
        // Outer shadow line (rail outer edge)
        ctx.strokeStyle = 'rgba(0, 0, 0, 0.3)';
        ctx.lineWidth = 1.5;
        
        ctx.beginPath();
        ctx.moveTo(cushionMargin + 30, cushionMargin - 1);
        ctx.lineTo(width - cushionMargin - 30, cushionMargin - 1);
        ctx.stroke();
        
        ctx.beginPath();
        ctx.moveTo(cushionMargin + 30, height - cushionMargin + 1);
        ctx.lineTo(width - cushionMargin - 30, height - cushionMargin + 1);
        ctx.stroke();
        
        ctx.restore();
    },
    
    // ==========================================
    // #12  POCKET LEATHER STITCHING
    // ==========================================
    drawPocketStitching(ctx, pockets, cushionMargin) {
        if (!this.enablePocketStitching || !pockets) return;
        
        ctx.save();
        ctx.strokeStyle = 'rgba(120, 80, 40, 0.4)';
        ctx.lineWidth = 1.0;
        
        pockets.forEach((p, idx) => {
            const isCorner = idx < 4;
            const stitchRadius = isCorner ? cushionMargin * 1.05 : cushionMargin * 0.9;
            const stitchCount = isCorner ? 16 : 12;
            
            for (let i = 0; i < stitchCount; i++) {
                const angle1 = (i / stitchCount) * Math.PI * 2;
                const angle2 = ((i + 0.5) / stitchCount) * Math.PI * 2;
                
                // Each stitch is a small dash
                ctx.beginPath();
                ctx.moveTo(
                    p.x + Math.cos(angle1) * stitchRadius,
                    p.y + Math.sin(angle1) * stitchRadius
                );
                ctx.lineTo(
                    p.x + Math.cos(angle2) * stitchRadius,
                    p.y + Math.sin(angle2) * stitchRadius
                );
                ctx.stroke();
            }
        });
        
        ctx.restore();
    },
    
    // ==========================================
    // #1  OVERHEAD RECTANGULAR LIGHT REFLECTION
    // ==========================================
    drawOverheadLightReflection(ctx, ball) {
        if (!this.enableOverheadReflection || ball.potted) return;
        
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        
        // Rectangular pool light reflection (elongated horizontally)
        const reflX = ball.x - ball.r * 0.25;
        const reflY = ball.y - ball.r * 0.35;
        const reflW = ball.r * 0.7;
        const reflH = ball.r * 0.2;
        
        // Soft glow around the reflection
        const glowGrad = ctx.createRadialGradient(
            reflX + reflW / 2, reflY + reflH / 2, 0,
            reflX + reflW / 2, reflY + reflH / 2, ball.r * 0.5
        );
        glowGrad.addColorStop(0, 'rgba(255, 255, 250, 0.20)');
        glowGrad.addColorStop(0.5, 'rgba(255, 255, 250, 0.08)');
        glowGrad.addColorStop(1, 'rgba(255, 255, 250, 0)');
        ctx.fillStyle = glowGrad;
        ctx.fillRect(ball.x - ball.r, ball.y - ball.r, ball.r * 2, ball.r * 2);
        
        // The rectangular reflection shape (rounded rectangle)
        ctx.fillStyle = 'rgba(255, 255, 252, 0.28)';
        ctx.beginPath();
        const rr = 2;
        ctx.moveTo(reflX + rr, reflY);
        ctx.lineTo(reflX + reflW - rr, reflY);
        ctx.quadraticCurveTo(reflX + reflW, reflY, reflX + reflW, reflY + rr);
        ctx.lineTo(reflX + reflW, reflY + reflH - rr);
        ctx.quadraticCurveTo(reflX + reflW, reflY + reflH, reflX + reflW - rr, reflY + reflH);
        ctx.lineTo(reflX + rr, reflY + reflH);
        ctx.quadraticCurveTo(reflX, reflY + reflH, reflX, reflY + reflH - rr);
        ctx.lineTo(reflX, reflY + rr);
        ctx.quadraticCurveTo(reflX, reflY, reflX + rr, reflY);
        ctx.fill();
        
        // Brighter center line of the light
        ctx.fillStyle = 'rgba(255, 255, 255, 0.18)';
        ctx.fillRect(reflX + 2, reflY + reflH * 0.3, reflW - 4, reflH * 0.4);
        
        ctx.restore();
    },
    
    // ==========================================
    // #11  ENVIRONMENT MAP REFLECTION BAND
    // ==========================================
    drawEnvironmentReflection(ctx, ball) {
        if (!this.enableEnvironmentReflection || ball.potted) return;
        
        ctx.save();
        ctx.beginPath();
        ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
        ctx.clip();
        
        // Horizontal environment reflection band (simulates room/table reflection)
        const bandY = ball.y + ball.r * 0.1;
        const bandH = ball.r * 0.35;
        
        const envGrad = ctx.createLinearGradient(
            ball.x - ball.r, bandY,
            ball.x + ball.r, bandY
        );
        envGrad.addColorStop(0, 'rgba(40, 80, 40, 0)');
        envGrad.addColorStop(0.15, 'rgba(40, 80, 40, 0.07)');
        envGrad.addColorStop(0.35, 'rgba(40, 90, 40, 0.12)');
        envGrad.addColorStop(0.5, 'rgba(50, 100, 50, 0.15)');
        envGrad.addColorStop(0.65, 'rgba(40, 90, 40, 0.12)');
        envGrad.addColorStop(0.85, 'rgba(40, 80, 40, 0.07)');
        envGrad.addColorStop(1, 'rgba(40, 80, 40, 0)');
        
        ctx.fillStyle = envGrad;
        ctx.fillRect(ball.x - ball.r, bandY - bandH / 2, ball.r * 2, bandH);
        
        ctx.restore();
    },
    
    // ==========================================
    // #8  DYNAMIC SHADOW (speed-based softness)
    // ==========================================
    drawDynamicBallShadow(ctx, ball) {
        if (!this.enableDynamicShadows || ball.potted) return;

        const speed = Math.sqrt((ball.vx || 0) ** 2 + (ball.vy || 0) ** 2);
        
        // Shadow gets softer and more offset at higher speeds
        // (simulates ball lifting slightly off felt)
        const liftFactor = Math.min(1, speed / 15);
        const offsetX = 1 + liftFactor * 3;
        const offsetY = 2 + liftFactor * 5;
        const spreadX = ball.r * (0.5 + liftFactor * 0.6);
        const spreadY = ball.r * (0.35 + liftFactor * 0.4);
        const coreAlpha = 0.5 - liftFactor * 0.2;
        const softAlpha = 0.4 - liftFactor * 0.15;
        
        // #14  BALL SETTLE MICRO-BOUNCE
        let settleOffset = 0;
        if (this.enableBallSettle && speed > 0.01 && speed < 0.8) {
            // Micro-oscillation as ball settles
            const settlePhase = Date.now() * 0.015;
            settleOffset = Math.sin(settlePhase) * speed * 0.5;
        }
        
        // Contact shadow (core)
        ctx.save();
        ctx.fillStyle = `rgba(0, 0, 0, ${coreAlpha})`;
        ctx.beginPath();
        ctx.ellipse(
            ball.x + offsetX, ball.y + offsetY + settleOffset,
            spreadX, spreadY,
            0, 0, Math.PI * 2
        );
        ctx.fill();
        
        // Soft diffuse shadow (outer)
        const shadowGrad = ctx.createRadialGradient(
            ball.x + offsetX + 1, ball.y + offsetY + 1 + settleOffset, ball.r * 0.2,
            ball.x + offsetX + 1, ball.y + offsetY + 1 + settleOffset, ball.r * (1.3 + liftFactor * 0.5)
        );
        shadowGrad.addColorStop(0, `rgba(0, 0, 0, ${softAlpha})`);
        shadowGrad.addColorStop(0.4, `rgba(0, 0, 0, ${softAlpha * 0.5})`);
        shadowGrad.addColorStop(0.7, `rgba(0, 0, 0, ${softAlpha * 0.15})`);
        shadowGrad.addColorStop(1, 'rgba(0, 0, 0, 0)');
        
        ctx.fillStyle = shadowGrad;
        ctx.beginPath();
        ctx.ellipse(
            ball.x + offsetX + 1, ball.y + offsetY + 1 + settleOffset,
            ball.r * (1.3 + liftFactor * 0.5), ball.r * (0.8 + liftFactor * 0.3),
            0, 0, Math.PI * 2
        );
        ctx.fill();
        ctx.restore();
    },
    
    // ==========================================
    // #13  CUE STICK INLAY PATTERNS
    // ==========================================
    drawCueInlays(ctx, cueStartX, cueStartY, cueEndX, cueEndY, aimAngle) {
        if (!this.enableCueInlays) return;
        const perpX = -Math.sin(aimAngle);
        const perpY = Math.cos(aimAngle);
        const dirX = -Math.cos(aimAngle);
        const dirY = -Math.sin(aimAngle);
        
        // Decorative inlay rings on the butt section
        const cueLength = Math.sqrt(
            (cueEndX - cueStartX) ** 2 + (cueEndY - cueStartY) ** 2
        );
        
        // Ring positions (fraction along butt section)
        const ringPositions = [0.35, 0.37, 0.55, 0.57, 0.72, 0.73, 0.74];
        const ringColors = ['#f5e6c8', '#1a1a1a', '#f5e6c8', '#1a1a1a', '#c0a060', '#1a1a1a', '#c0a060'];
        
        ctx.save();
        
        ringPositions.forEach((pos, idx) => {
            const px = cueStartX + dirX * cueLength * (0.55 + pos * 0.45);
            const py = cueStartY + dirY * cueLength * (0.55 + pos * 0.45);
            
            ctx.strokeStyle = ringColors[idx];
            ctx.lineWidth = 1.2;
            ctx.globalAlpha = 0.6;
            ctx.beginPath();
            ctx.moveTo(px + perpX * 6, py + perpY * 6);
            ctx.lineTo(px - perpX * 6, py - perpY * 6);
            ctx.stroke();
        });
        
        ctx.restore();
    },
    
    // ==========================================
    // #15  LIGHT TEMPERATURE
    // ==========================================
    getLightTint() {
        switch (this.lightTemperature) {
            case 'warm':
                return { r: 255, g: 248, b: 220, overlay: 'rgba(255, 245, 210, 0.06)' };
            case 'cool':
                return { r: 220, g: 235, b: 255, overlay: 'rgba(210, 225, 255, 0.06)' };
            default: // neutral
                return { r: 255, g: 255, b: 255, overlay: 'rgba(255, 255, 255, 0)' };
        }
    },
    
    drawLightTemperatureOverlay(ctx, feltInset, feltWidth, feltHeight) {
        if (this.lightTemperature === 'neutral') return;
        const tint = this.getLightTint();
        
        ctx.save();
        ctx.fillStyle = tint.overlay;
        ctx.fillRect(feltInset, feltInset, feltWidth, feltHeight);
        ctx.restore();
    },
    
    // ==========================================
    // MASTER UPDATE — call once per frame
    // ==========================================
    _loggedFirstFrame: false,
    update(ctx, game) {
        if (!game) return;
        if (!this._loggedFirstFrame) {
            console.log('[PoolVFX] first frame update — feltNoise:', this.feltNoiseReady, 'woodGrain:', this.woodGrainReady,
                'chalk:', this.chalkParticles.length, 'flashes:', this.collisionFlashes.length);
            this._loggedFirstFrame = true;
        }

        // Draw particle effects (on top of balls)
        this.updateAndDrawChalkDust(ctx);
        this.updateAndDrawCollisionFlashes(ctx);
        this.updateAndDrawCushionCompressions(ctx, game.cushionMargin);
    }
};

// Auto-init when script loads
if (typeof document !== 'undefined') {
    if (document.readyState === 'loading') {
        document.addEventListener('DOMContentLoaded', () => PoolVFX.init());
    } else {
        PoolVFX.init();
    }
}
""";
    }
}
