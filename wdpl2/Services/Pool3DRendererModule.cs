namespace Wdpl2.Services;

/// <summary>
/// Isometric 3D pool table renderer with playable controls
/// </summary>
public static class Pool3DRendererModule
{
    public static string GenerateJavaScript()
    {
        return """
// ============================================
// ISOMETRIC 3D POOL RENDERER v4.0
// With playable aiming and shooting
// ============================================

const Pool3DRenderer = {
    enabled: false,
    initialized: false,
    canvas2D: null,
    canvas3D: null,
    ctx: null,
    balls: [],
    gameWidth: 1000,
    gameHeight: 500,
    animationId: null,
    cameraAngle: 0.4,
    targetAngle: 0.4,
    isAiming: false,
    aimStartX: 0,
    aimStartY: 0,
    aimEndX: 0,
    aimEndY: 0,
    shotPower: 0,
    maxPower: 25,
    controlPanel: null,
    angleDisplay: null,
    
    init: async function(game) {
        if (this.initialized) return true;
        
        console.log('[3D] v4.0 Initializing...');
        
        this.canvas2D = document.getElementById('poolTable') || document.getElementById('canvas');
        if (!this.canvas2D) {
            console.error('[3D] No canvas found');
            return false;
        }
        
        var container = this.canvas2D.parentElement;
        this.canvas3D = document.createElement('canvas');
        this.canvas3D.id = 'poolTable3D';
        this.canvas3D.width = this.canvas2D.width || 1000;
        this.canvas3D.height = this.canvas2D.height || 500;
        this.canvas3D.style.cssText = 'position:absolute;top:0;left:0;width:100%;height:100%;display:none;z-index:10;cursor:crosshair;';
        
        if (getComputedStyle(container).position === 'static') {
            container.style.position = 'relative';
        }
        
        container.appendChild(this.canvas3D);
        this.ctx = this.canvas3D.getContext('2d');
        
        this.setupInputHandlers();
        this.createControlPanel();
        this.initialized = true;
        console.log('[3D] Ready - drag to aim, release to shoot!');
        return true;
    },
    
    setupInputHandlers: function() {
        var self = this;
        var canvas = this.canvas3D;
        
        function getPos(e) {
            var rect = canvas.getBoundingClientRect();
            var scaleX = canvas.width / rect.width;
            var scaleY = canvas.height / rect.height;
            var clientX = e.touches ? e.touches[0].clientX : e.clientX;
            var clientY = e.touches ? e.touches[0].clientY : e.clientY;
            return {
                x: (clientX - rect.left) * scaleX,
                y: (clientY - rect.top) * scaleY
            };
        }
        
        function onStart(e) {
            if (!self.enabled) return;
            e.preventDefault();
            var pos = getPos(e);
            self.aimStartX = pos.x;
            self.aimStartY = pos.y;
            self.aimEndX = pos.x;
            self.aimEndY = pos.y;
            self.isAiming = true;
            self.shotPower = 0;
        }
        
        function onMove(e) {
            if (!self.enabled || !self.isAiming) return;
            e.preventDefault();
            var pos = getPos(e);
            self.aimEndX = pos.x;
            self.aimEndY = pos.y;
            var dx = self.aimEndX - self.aimStartX;
            var dy = self.aimEndY - self.aimStartY;
            var dist = Math.sqrt(dx * dx + dy * dy);
            self.shotPower = Math.min(dist / 5, self.maxPower);
        }
        
        function onEnd(e) {
            if (!self.enabled || !self.isAiming) return;
            e.preventDefault();
            
            if (self.shotPower > 1 && typeof game !== 'undefined' && game.cueBall && !game.cueBall.potted) {
                var dx = self.aimStartX - self.aimEndX;
                var dy = self.aimStartY - self.aimEndY;
                var dist = Math.sqrt(dx * dx + dy * dy);
                
                if (dist > 5) {
                    var angle = Math.atan2(dy, dx);
                    game.cueBall.vx = Math.cos(angle) * self.shotPower;
                    game.cueBall.vy = Math.sin(angle) * self.shotPower;
                    console.log('[3D] Shot! Power:', self.shotPower.toFixed(1));
                }
            }
            
            self.isAiming = false;
            self.shotPower = 0;
        }
        
        canvas.addEventListener('mousedown', onStart);
        canvas.addEventListener('mousemove', onMove);
        canvas.addEventListener('mouseup', onEnd);
        canvas.addEventListener('mouseleave', onEnd);
        canvas.addEventListener('touchstart', onStart, { passive: false });
        canvas.addEventListener('touchmove', onMove, { passive: false });
        canvas.addEventListener('touchend', onEnd, { passive: false });
    },
    
    createControlPanel: function() {
        var self = this;
        
        this.controlPanel = document.createElement('div');
        this.controlPanel.id = 'pool3DControls';
        this.controlPanel.style.cssText = 'position:fixed;bottom:80px;right:20px;background:rgba(0,0,0,0.85);padding:12px;border-radius:12px;z-index:1001;display:none;flex-direction:column;gap:8px;';
        
        var title = document.createElement('div');
        title.textContent = '?? Camera';
        title.style.cssText = 'color:#4ade80;font-weight:bold;font-size:14px;text-align:center;';
        this.controlPanel.appendChild(title);
        
        this.angleDisplay = document.createElement('div');
        this.angleDisplay.style.cssText = 'color:white;font-size:12px;text-align:center;';
        this.angleDisplay.textContent = 'Angle: 40%';
        this.controlPanel.appendChild(this.angleDisplay);
        
        var upBtn = document.createElement('button');
        upBtn.textContent = '?? Up';
        upBtn.style.cssText = 'padding:12px;font-size:14px;font-weight:bold;border:none;border-radius:8px;background:#3b82f6;color:white;cursor:pointer;';
        upBtn.onclick = function() { self.targetAngle = Math.min(0.8, self.targetAngle + 0.1); };
        this.controlPanel.appendChild(upBtn);
        
        var downBtn = document.createElement('button');
        downBtn.textContent = '?? Down';
        downBtn.style.cssText = 'padding:12px;font-size:14px;font-weight:bold;border:none;border-radius:8px;background:#3b82f6;color:white;cursor:pointer;';
        downBtn.onclick = function() { self.targetAngle = Math.max(0.1, self.targetAngle - 0.1); };
        this.controlPanel.appendChild(downBtn);
        
        var resetBtn = document.createElement('button');
        resetBtn.textContent = '?? Reset';
        resetBtn.style.cssText = 'padding:10px;font-size:12px;border:none;border-radius:8px;background:#6b7280;color:white;cursor:pointer;';
        resetBtn.onclick = function() { self.targetAngle = 0.4; };
        this.controlPanel.appendChild(resetBtn);
        
        document.body.appendChild(this.controlPanel);
    },
    
    updateBalls: function(balls, gameWidth, gameHeight) {
        this.balls = balls || [];
        this.gameWidth = gameWidth || 1000;
        this.gameHeight = gameHeight || 500;
    },
    
    startAnimationLoop: function() {
        var self = this;
        
        function animate() {
            if (!self.enabled) {
                self.animationId = null;
                return;
            }
            
            if (Math.abs(self.cameraAngle - self.targetAngle) > 0.005) {
                self.cameraAngle += (self.targetAngle - self.cameraAngle) * 0.1;
            } else {
                self.cameraAngle = self.targetAngle;
            }
            
            if (self.angleDisplay) {
                self.angleDisplay.textContent = 'Angle: ' + Math.round(self.cameraAngle * 100) + '%';
            }
            
            if (typeof game !== 'undefined' && game.balls) {
                self.updateBalls(game.balls, game.canvas.width, game.canvas.height);
            }
            
            self.render();
            self.animationId = requestAnimationFrame(animate);
        }
        
        if (!this.animationId) {
            animate();
        }
    },
    
    stopAnimationLoop: function() {
        if (this.animationId) {
            cancelAnimationFrame(this.animationId);
            this.animationId = null;
        }
    },
    
    render: function() {
        if (!this.enabled || !this.initialized || !this.ctx) return;
        
        var ctx = this.ctx;
        var w = this.canvas3D.width;
        var h = this.canvas3D.height;
        var gameW = this.gameWidth;
        var gameH = this.gameHeight;
        var scale = Math.min(w / gameW, h / gameH) * 0.95;
        var offsetX = (w - gameW * scale) / 2;
        var offsetY = (h - gameH * scale) / 2;
        var ISO = this.cameraAngle;
        
        var self = this;
        function toScreen(gx, gy, height) {
            height = height || 0;
            var x = offsetX + gx * scale;
            var y = offsetY + gy * scale * ISO + (gameH * scale * (1 - ISO) / 2) - height * 0.5;
            return { x: x, y: y };
        }
        
        var cushion = (typeof game !== 'undefined' && game.cushionMargin) ? game.cushionMargin : 20;
        
        // Background
        var bgGrad = ctx.createLinearGradient(0, 0, 0, h);
        bgGrad.addColorStop(0, '#1a2a3a');
        bgGrad.addColorStop(1, '#0a1520');
        ctx.fillStyle = bgGrad;
        ctx.fillRect(0, 0, w, h);
        
        // Table shadow
        ctx.fillStyle = 'rgba(0,0,0,0.4)';
        var ts1 = toScreen(-10, -10);
        var ts2 = toScreen(gameW + 10, -10);
        var ts3 = toScreen(gameW + 10, gameH + 10);
        var ts4 = toScreen(-10, gameH + 10);
        ctx.beginPath();
        ctx.moveTo(ts1.x + 8, ts1.y + 8);
        ctx.lineTo(ts2.x + 8, ts2.y + 8);
        ctx.lineTo(ts3.x + 8, ts3.y + 8);
        ctx.lineTo(ts4.x + 8, ts4.y + 8);
        ctx.closePath();
        ctx.fill();
        
        // Frame
        ctx.fillStyle = '#4a2f1a';
        var frameW = 15 * scale;
        var f1 = toScreen(-frameW/scale, -frameW/scale);
        var f2 = toScreen(gameW + frameW/scale, -frameW/scale);
        var f3 = toScreen(gameW + frameW/scale, gameH + frameW/scale);
        var f4 = toScreen(-frameW/scale, gameH + frameW/scale);
        ctx.beginPath();
        ctx.moveTo(f1.x, f1.y);
        ctx.lineTo(f2.x, f2.y);
        ctx.lineTo(f3.x, f3.y);
        ctx.lineTo(f4.x, f4.y);
        ctx.closePath();
        ctx.fill();
        
        // Rail
        ctx.fillStyle = '#8B4513';
        var railW = 8 * scale;
        var r1 = toScreen(-railW/scale, -railW/scale, 5);
        var r2 = toScreen(gameW + railW/scale, -railW/scale, 5);
        var r3 = toScreen(gameW + railW/scale, gameH + railW/scale, 5);
        var r4 = toScreen(-railW/scale, gameH + railW/scale, 5);
        ctx.beginPath();
        ctx.moveTo(r1.x, r1.y);
        ctx.lineTo(r2.x, r2.y);
        ctx.lineTo(r3.x, r3.y);
        ctx.lineTo(r4.x, r4.y);
        ctx.closePath();
        ctx.fill();
        
        // Felt
        var feltGrad = ctx.createLinearGradient(offsetX, 0, offsetX + gameW * scale, 0);
        feltGrad.addColorStop(0, '#1e8c45');
        feltGrad.addColorStop(0.5, '#1a7f37');
        feltGrad.addColorStop(1, '#167030');
        ctx.fillStyle = feltGrad;
        var g1 = toScreen(cushion, cushion, 8);
        var g2 = toScreen(gameW - cushion, cushion, 8);
        var g3 = toScreen(gameW - cushion, gameH - cushion, 8);
        var g4 = toScreen(cushion, gameH - cushion, 8);
        ctx.beginPath();
        ctx.moveTo(g1.x, g1.y);
        ctx.lineTo(g2.x, g2.y);
        ctx.lineTo(g3.x, g3.y);
        ctx.lineTo(g4.x, g4.y);
        ctx.closePath();
        ctx.fill();
        
        // Cushions
        this.drawRect(ctx, toScreen, cushion, 0, gameW - cushion * 2, cushion, 12, '#1a7030');
        this.drawRect(ctx, toScreen, cushion, gameH - cushion, gameW - cushion * 2, cushion, 12, '#1a7030');
        this.drawRect(ctx, toScreen, 0, cushion, cushion, gameH - cushion * 2, 12, '#1a7030');
        this.drawRect(ctx, toScreen, gameW - cushion, cushion, cushion, gameH - cushion * 2, 12, '#1a7030');
        
        // Pockets
        var pockets = [];
        if (typeof game !== 'undefined' && game.pockets) {
            for (var i = 0; i < game.pockets.length; i++) {
                pockets.push({ x: game.pockets[i].x, y: game.pockets[i].y, r: game.pockets[i].r });
            }
        } else {
            var pr = 25;
            pockets = [
                { x: cushion, y: cushion, r: pr },
                { x: gameW / 2, y: 0, r: pr },
                { x: gameW - cushion, y: cushion, r: pr },
                { x: cushion, y: gameH - cushion, r: pr },
                { x: gameW / 2, y: gameH, r: pr },
                { x: gameW - cushion, y: gameH - cushion, r: pr }
            ];
        }
        
        for (var i = 0; i < pockets.length; i++) {
            var p = pockets[i];
            var pp = toScreen(p.x, p.y, 8);
            var pocketR = (p.r || 25) * scale;
            ctx.fillStyle = '#000';
            ctx.beginPath();
            ctx.ellipse(pp.x, pp.y, pocketR, pocketR * ISO, 0, 0, Math.PI * 2);
            ctx.fill();
        }
        
        // Sort and draw balls
        var sortedBalls = [];
        for (var i = 0; i < this.balls.length; i++) {
            if (this.balls[i].potted !== true) {
                sortedBalls.push(this.balls[i]);
            }
        }
        sortedBalls.sort(function(a, b) { return a.y - b.y; });
        
        var ballR = (typeof game !== 'undefined' && game.standardBallRadius) ? game.standardBallRadius : 14;
        
        for (var i = 0; i < sortedBalls.length; i++) {
            var ball = sortedBalls[i];
            var br = (ball.r || ballR) * scale;
            var bp = toScreen(ball.x, ball.y, br * 2);
            
            // Shadow
            var sp = toScreen(ball.x + 2/scale, ball.y + 2/scale, 0);
            ctx.fillStyle = 'rgba(0,0,0,0.35)';
            ctx.beginPath();
            ctx.ellipse(sp.x, sp.y, br * 0.9, br * 0.5 * ISO, 0, 0, Math.PI * 2);
            ctx.fill();
            
            // Ball
            var col = this.getBallColor(ball.color);
            var grad = ctx.createRadialGradient(bp.x - br * 0.3, bp.y - br * 0.4, 0, bp.x, bp.y, br);
            grad.addColorStop(0, 'rgba(255,255,255,0.9)');
            grad.addColorStop(0.25, col.light);
            grad.addColorStop(0.6, col.main);
            grad.addColorStop(1, col.dark);
            ctx.fillStyle = grad;
            ctx.beginPath();
            ctx.arc(bp.x, bp.y, br, 0, Math.PI * 2);
            ctx.fill();
            
            // Highlight
            ctx.fillStyle = 'rgba(255,255,255,0.5)';
            ctx.beginPath();
            ctx.ellipse(bp.x - br * 0.3, bp.y - br * 0.35, br * 0.2, br * 0.12, -0.4, 0, Math.PI * 2);
            ctx.fill();
        }
        
        // Draw aiming line
        if (this.isAiming && typeof game !== 'undefined' && game.cueBall && !game.cueBall.potted) {
            var cueBallScreen = toScreen(game.cueBall.x, game.cueBall.y, ballR * scale * 2);
            var dx = this.aimStartX - this.aimEndX;
            var dy = this.aimStartY - this.aimEndY;
            var dist = Math.sqrt(dx * dx + dy * dy);
            
            if (dist > 5) {
                var angle = Math.atan2(dy, dx);
                
                // Cue stick
                var cueLength = 200;
                var cueEndX = cueBallScreen.x - Math.cos(angle) * cueLength;
                var cueEndY = cueBallScreen.y - Math.sin(angle) * cueLength;
                var cueStartX = cueBallScreen.x - Math.cos(angle) * (ballR * scale + dist * 0.3);
                var cueStartY = cueBallScreen.y - Math.sin(angle) * (ballR * scale + dist * 0.3);
                
                ctx.strokeStyle = '#8B4513';
                ctx.lineWidth = 6;
                ctx.lineCap = 'round';
                ctx.beginPath();
                ctx.moveTo(cueStartX, cueStartY);
                ctx.lineTo(cueEndX, cueEndY);
                ctx.stroke();
                
                // Cue tip
                ctx.strokeStyle = '#d4a574';
                ctx.lineWidth = 8;
                ctx.beginPath();
                ctx.moveTo(cueStartX, cueStartY);
                ctx.lineTo(cueStartX - Math.cos(angle) * 15, cueStartY - Math.sin(angle) * 15);
                ctx.stroke();
                
                // Aim line
                ctx.strokeStyle = 'rgba(255,255,255,0.6)';
                ctx.lineWidth = 2;
                ctx.setLineDash([8, 8]);
                ctx.beginPath();
                ctx.moveTo(cueBallScreen.x, cueBallScreen.y);
                ctx.lineTo(cueBallScreen.x + Math.cos(angle) * 300, cueBallScreen.y + Math.sin(angle) * 300);
                ctx.stroke();
                ctx.setLineDash([]);
                
                // Power bar
                var powerPct = Math.min(this.shotPower / this.maxPower, 1);
                var powerColor = powerPct < 0.3 ? '#4ade80' : powerPct < 0.7 ? '#fbbf24' : '#ef4444';
                ctx.fillStyle = 'rgba(0,0,0,0.7)';
                ctx.fillRect(w/2 - 75, h - 50, 150, 25);
                ctx.fillStyle = powerColor;
                ctx.fillRect(w/2 - 73, h - 48, 146 * powerPct, 21);
                ctx.fillStyle = 'white';
                ctx.font = 'bold 12px Arial';
                ctx.textAlign = 'center';
                ctx.fillText('POWER: ' + Math.round(powerPct * 100) + '%', w/2, h - 34);
            }
        }
        
        // HUD
        ctx.fillStyle = 'rgba(0,0,0,0.75)';
        ctx.fillRect(w/2 - 100, 8, 200, 30);
        ctx.fillStyle = '#4ade80';
        ctx.font = 'bold 11px Arial';
        ctx.textAlign = 'center';
        ctx.fillText('?? 3D | Drag to aim & shoot', w/2, 28);
    },
    
    drawRect: function(ctx, toScreen, x, y, width, height, h, color) {
        ctx.fillStyle = color;
        var c1 = toScreen(x, y, h);
        var c2 = toScreen(x + width, y, h);
        var c3 = toScreen(x + width, y + height, h);
        var c4 = toScreen(x, y + height, h);
        ctx.beginPath();
        ctx.moveTo(c1.x, c1.y);
        ctx.lineTo(c2.x, c2.y);
        ctx.lineTo(c3.x, c3.y);
        ctx.lineTo(c4.x, c4.y);
        ctx.closePath();
        ctx.fill();
    },
    
    getBallColor: function(color) {
        var colors = {
            white: { light: '#ffffff', main: '#f5f5f5', dark: '#a0a0a0' },
            red: { light: '#ff8888', main: '#e63946', dark: '#8b0000' },
            yellow: { light: '#ffee88', main: '#ffd700', dark: '#b8860b' },
            black: { light: '#666666', main: '#2a2a2a', dark: '#000000' }
        };
        return colors[color] || colors.white;
    },
    
    toggle: async function() {
        console.log('[3D] Toggle called');
        
        if (!this.initialized) {
            var success = await this.init(typeof game !== 'undefined' ? game : null);
            if (!success) return;
        }
        
        this.enabled = !this.enabled;
        console.log('[3D] Enabled:', this.enabled);
        
        if (this.canvas2D && this.canvas3D) {
            if (this.enabled) {
                this.canvas2D.style.visibility = 'hidden';
                this.canvas3D.style.display = 'block';
                if (this.controlPanel) this.controlPanel.style.display = 'flex';
                if (typeof game !== 'undefined' && game.balls) {
                    this.updateBalls(game.balls, game.canvas.width, game.canvas.height);
                }
                this.startAnimationLoop();
            } else {
                this.canvas2D.style.visibility = 'visible';
                this.canvas3D.style.display = 'none';
                if (this.controlPanel) this.controlPanel.style.display = 'none';
                this.stopAnimationLoop();
            }
        }
        
        this.updateModeIndicator();
        this.updateToggleButton();
    },
    
    updateToggleButton: function() {
        var btn = document.getElementById('toggle3DBtn');
        if (btn) {
            btn.textContent = this.enabled ? '?? 2D View' : '?? 3D View';
            btn.style.background = this.enabled ? '#10b981' : '';
        }
    },
    
    updateModeIndicator: function() {
        var ind = document.getElementById('renderModeIndicator');
        if (!ind) {
            ind = document.createElement('div');
            ind.id = 'renderModeIndicator';
            ind.style.cssText = 'position:fixed;top:10px;left:10px;background:rgba(0,0,0,0.8);padding:8px 12px;border-radius:8px;font-family:monospace;font-size:11px;z-index:10000;pointer-events:none;';
            document.body.appendChild(ind);
        }
        ind.innerHTML = this.enabled ? '?? 3D MODE' : '?? 2D MODE';
        ind.style.color = this.enabled ? '#60a5fa' : '#4ade80';
    },
    
    dispose: function() {
        this.stopAnimationLoop();
        if (this.canvas3D && this.canvas3D.parentElement) {
            this.canvas3D.parentElement.removeChild(this.canvas3D);
        }
        if (this.controlPanel && this.controlPanel.parentElement) {
            this.controlPanel.parentElement.removeChild(this.controlPanel);
        }
        this.initialized = false;
        this.enabled = false;
    }
};

// Keyboard shortcut: Press '3' to toggle
document.addEventListener('keydown', function(e) {
    if (e.key === '3' && !e.ctrlKey && !e.altKey && !e.metaKey) {
        Pool3DRenderer.toggle();
    }
});

console.log('[3D] Pool3DRenderer v4.0 loaded');
""";
    }
}
