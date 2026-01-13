using Microsoft.Maui.Controls;
using System.IO;
using System.Text;

namespace Wdpl2.Views;

public partial class PoolGamePage : ContentPage
{
    private const string GameHtml = @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, user-scalable=no'>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            background: #1e3c72; 
            font-family: Arial, sans-serif;
            overflow: hidden;
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 20px;
        }
        #status {
            color: white;
            background: rgba(0,0,0,0.9);
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 10px;
            font-size: 18px;
            font-weight: bold;
            text-align: center;
            width: 100%;
            max-width: 900px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.5);
        }
        canvas { 
            background: #1a7f37;
            border: 15px solid #8B4513;
            border-radius: 8px;
            cursor: crosshair;
            display: block;
            width: 100%;
            max-width: 1000px;
            height: auto;
            box-shadow: 0 8px 24px rgba(0,0,0,0.3);
        }
        #controls {
            margin-top: 15px;
            display: flex;
            gap: 10px;
        }
        button {
            padding: 12px 24px;
            background: #3B82F6;
            color: white;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            font-weight: bold;
            cursor: pointer;
        }
        button:hover { background: #2563EB; }
        button:active { transform: scale(0.95); }
    </style>
</head>
<body>
    <div id='status'>?? Loading Pool Game...</div>
    <canvas id='canvas' width='1000' height='500'></canvas>
    <div id='controls'>
        <button onclick='game.stopBalls()'>?? Stop All Balls</button>
        <button onclick='game.resetRack()'>?? Reset Rack</button>
    </div>
    
    <script>
        const statusEl = document.getElementById('status');
        const canvas = document.getElementById('canvas');
        const ctx = canvas.getContext('2d');
        
        if (!ctx) {
            statusEl.textContent = '? ERROR: Cannot get canvas context';
            statusEl.style.background = '#EF4444';
            throw new Error('Canvas context not available');
        }
        
        const game = {
            width: 1000,
            height: 500,
            balls: [],
            pockets: [],
            friction: 0.985,
            pocketRadius: 22,
            ballRadius: 10,
            animationFrameId: null,
            isShooting: false,
            isAiming: false,
            shotPower: 0,
            maxPower: 40,
            aimAngle: 0,
            mouseX: 0,
            mouseY: 0,
            cueBall: null,
            pullBackDistance: 0,
            pushForwardDistance: 0,
            dragStartX: 0,
            dragStartY: 0,
            dragStartTime: 0,
            lastMouseTime: 0,
            initialDotProduct: 0,
            
            init() {
                // Pockets
                this.pockets = [
                    {x: 25, y: 25}, {x: 500, y: 20}, {x: 975, y: 25},
                    {x: 25, y: 475}, {x: 500, y: 480}, {x: 975, y: 475}
                ];
                
                this.resetRack();
            },
            
            resetRack() {
                this.balls = [];
                
                // Cue ball
                this.cueBall = {
                    x: 200, y: 250,
                    vx: 0, vy: 0,
                    r: this.ballRadius,
                    color: 'white',
                    num: 0
                };
                this.balls.push(this.cueBall);
                
                // EXACT EPA UK 8-BALL RACK from your image:
                // Row 1: R
                // Row 2: Y R
                // Row 3: R B Y
                // Row 4: Y R Y R
                // Row 5: R Y Y R Y
                
                const startX = 700, startY = 250, gap = this.ballRadius * 2 + 0.5;
                
                const rackPattern = [
                    // Row 1 (apex): RED
                    {x: startX + gap * 0, y: startY + 0, color: 'red', num: 1},
                    
                    // Row 2: YELLOW, RED
                    {x: startX + gap * 1, y: startY - gap * 0.5, color: 'yellow', num: 9},
                    {x: startX + gap * 1, y: startY + gap * 0.5, color: 'red', num: 2},
                    
                    // Row 3: RED, BLACK, YELLOW
                    {x: startX + gap * 2, y: startY - gap * 1, color: 'red', num: 3},
                    {x: startX + gap * 2, y: startY + 0, color: 'black', num: 8},
                    {x: startX + gap * 2, y: startY + gap * 1, color: 'yellow', num: 10},
                    
                    // Row 4: YELLOW, RED, YELLOW, RED
                    {x: startX + gap * 3, y: startY - gap * 1.5, color: 'yellow', num: 11},
                    {x: startX + gap * 3, y: startY - gap * 0.5, color: 'red', num: 4},
                    {x: startX + gap * 3, y: startY + gap * 0.5, color: 'yellow', num: 12},
                    {x: startX + gap * 3, y: startY + gap * 1.5, color: 'red', num: 5},
                    
                    // Row 5 (back): RED, YELLOW, YELLOW, RED, YELLOW
                    {x: startX + gap * 4, y: startY - gap * 2, color: 'red', num: 6},
                    {x: startX + gap * 4, y: startY - gap * 1, color: 'yellow', num: 13},
                    {x: startX + gap * 4, y: startY + 0, color: 'yellow', num: 14},
                    {x: startX + gap * 4, y: startY + gap * 1, color: 'red', num: 7},
                    {x: startX + gap * 4, y: startY + gap * 2, color: 'yellow', num: 15}
                ];
                
                // Add all balls according to the exact pattern
                rackPattern.forEach(ball => {
                    this.balls.push({
                        x: ball.x,
                        y: ball.y,
                        vx: 0, vy: 0,
                        r: this.ballRadius,
                        color: ball.color,
                        num: ball.num
                    });
                });
                
                statusEl.textContent = `? EPA Rack: R | YR | R8Y | YRYR | RYYRY | ${this.balls.length} balls ready!`;
                statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
            },
            
            stopBalls() {
                this.balls.forEach(b => {
                    b.vx = 0;
                    b.vy = 0;
                });
                statusEl.textContent = '?? All balls stopped';
                statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
            },
            
            animate() {
                // Clear
                ctx.fillStyle = '#1a7f37';
                ctx.fillRect(0, 0, this.width, this.height);
                
                // Center line
                ctx.strokeStyle = 'rgba(255, 255, 255, 0.2)';
                ctx.lineWidth = 2;
                ctx.setLineDash([10, 10]);
                ctx.beginPath();
                ctx.moveTo(this.width / 2, 0);
                ctx.lineTo(this.width / 2, this.height);
                ctx.stroke();
                ctx.setLineDash([]);
                
                // Cushions
                ctx.strokeStyle = '#8B4513';
                ctx.lineWidth = 12;
                ctx.strokeRect(6, 6, this.width - 12, this.height - 12);
                
                // Pockets with capture zones
                this.pockets.forEach(p => {
                    // Red capture zone
                    ctx.fillStyle = 'rgba(255, 0, 0, 0.25)';
                    ctx.beginPath();
                    ctx.arc(p.x, p.y, this.pocketRadius + 2, 0, Math.PI * 2);
                    ctx.fill();
                    
                    // Yellow threshold ring
                    ctx.strokeStyle = 'rgba(255, 255, 0, 0.5)';
                    ctx.lineWidth = 2;
                    ctx.beginPath();
                    ctx.arc(p.x, p.y, this.pocketRadius + 2, 0, Math.PI * 2);
                    ctx.stroke();
                    
                    // Black pocket
                    ctx.fillStyle = '#000';
                    ctx.beginPath();
                    ctx.arc(p.x, p.y, this.pocketRadius, 0, Math.PI * 2);
                    ctx.fill();
                });
                
                // Physics & drawing
                let moving = false;
                let activeBalls = 0;
                
                this.balls.forEach(ball => {
                    if (ball.potted) return;
                    
                    activeBalls++;
                    
                    // Apply friction
                    if (Math.abs(ball.vx) > 0.015 || Math.abs(ball.vy) > 0.015) {
                        ball.vx *= this.friction;
                        ball.vy *= this.friction;
                        ball.x += ball.vx;
                        ball.y += ball.vy;
                        moving = true;
                    } else {
                        ball.vx = 0;
                        ball.vy = 0;
                    }
                    
                    // Cushion bounce
                    const minX = 20 + ball.r;
                    const maxX = this.width - 20 - ball.r;
                    const minY = 20 + ball.r;
                    const maxY = this.height - 20 - ball.r;
                    
                    if (ball.x < minX) { ball.x = minX; ball.vx = -ball.vx * 0.75; }
                    if (ball.x > maxX) { ball.x = maxX; ball.vx = -ball.vx * 0.75; }
                    if (ball.y < minY) { ball.y = minY; ball.vy = -ball.vy * 0.75; }
                    if (ball.y > maxY) { ball.y = maxY; ball.vy = -ball.vy * 0.75; }
                    
                    // Check pockets
                    this.pockets.forEach(p => {
                        const dx = ball.x - p.x;
                        const dy = ball.y - p.y;
                        const dist = Math.sqrt(dx * dx + dy * dy);
                        if (dist < this.pocketRadius + 2) {
                            ball.potted = true;
                            ball.vx = ball.vy = 0;
                            statusEl.textContent = `?? Ball ${ball.num} potted! ${activeBalls - 1} balls remaining`;
                            statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
                        }
                    });
                });
                
                // BALL-TO-BALL COLLISION DETECTION
                for (let i = 0; i < this.balls.length; i++) {
                    if (this.balls[i].potted) continue;
                    
                    for (let j = i + 1; j < this.balls.length; j++) {
                        if (this.balls[j].potted) continue;
                        
                        const b1 = this.balls[i];
                        const b2 = this.balls[j];
                        
                        const dx = b2.x - b1.x;
                        const dy = b2.y - b1.y;
                        const distSq = dx * dx + dy * dy;
                        const minDist = b1.r + b2.r;
                        
                        // Check if balls are colliding
                        if (distSq < minDist * minDist) {
                            const dist = Math.sqrt(distSq);
                            
                            // Normalize collision vector
                            const nx = dx / dist;
                            const ny = dy / dist;
                            
                            // Relative velocity
                            const dvx = b2.vx - b1.vx;
                            const dvy = b2.vy - b1.vy;
                            
                            // Relative velocity in collision normal direction
                            const dvn = dvx * nx + dvy * ny;
                            
                            // Only resolve if balls are moving toward each other
                            if (dvn < 0) {
                                // Elastic collision with equal masses
                                const impulse = dvn;
                                
                                b1.vx += impulse * nx;
                                b1.vy += impulse * ny;
                                b2.vx -= impulse * nx;
                                b2.vy -= impulse * ny;
                            }
                            
                            // Separate overlapping balls
                            const overlap = minDist - dist;
                            if (overlap > 0) {
                                const separationX = nx * overlap * 0.5;
                                const separationY = ny * overlap * 0.5;
                                
                                b1.x -= separationX;
                                b1.y -= separationY;
                                b2.x += separationX;
                                b2.y += separationY;
                            }
                        }
                    }
                }
                
                // Draw all balls
                this.balls.forEach(ball => {
                    if (ball.potted) return;
                    
                    // Draw ball
                    const grad = ctx.createRadialGradient(
                        ball.x - 3, ball.y - 3, 0,
                        ball.x, ball.y, ball.r
                    );
                    
                    if (ball.color === 'white') {
                        grad.addColorStop(0, '#fff');
                        grad.addColorStop(1, '#ccc');
                    } else if (ball.color === 'red') {
                        grad.addColorStop(0, '#ff6b6b');
                        grad.addColorStop(1, '#c92a2a');
                    } else if (ball.color === 'yellow') {
                        grad.addColorStop(0, '#ffd43b');
                        grad.addColorStop(1, '#fab005');
                    } else {
                        grad.addColorStop(0, '#555');
                        grad.addColorStop(1, '#000');
                    }
                    
                    ctx.fillStyle = grad;
                    ctx.beginPath();
                    ctx.arc(ball.x, ball.y, ball.r, 0, Math.PI * 2);
                    ctx.fill();
                    
                    // Ball number
                    if (ball.num > 0) {
                        ctx.fillStyle = 'white';
                        ctx.beginPath();
                        ctx.arc(ball.x, ball.y, ball.r * 0.5, 0, Math.PI * 2);
                        ctx.fill();
                        
                        ctx.fillStyle = 'black';
                        ctx.font = 'bold 8px Arial';
                        ctx.textAlign = 'center';
                        ctx.textBaseline = 'middle';
                        ctx.fillText(ball.num, ball.x, ball.y);
                    }
                });
                
                // Draw aim line when aiming
                if (this.isAiming && !moving && this.cueBall && !this.cueBall.potted) {
                    const dist = 300;
                    ctx.strokeStyle = 'rgba(255, 255, 255, 0.6)';
                    ctx.lineWidth = 2;
                    ctx.setLineDash([10, 5]);
                    ctx.beginPath();
                    ctx.moveTo(this.cueBall.x, this.cueBall.y);
                    ctx.lineTo(
                        this.cueBall.x + Math.cos(this.aimAngle) * dist,
                        this.cueBall.y + Math.sin(this.aimAngle) * dist
                    );
                    ctx.stroke();
                    ctx.setLineDash([]);
                }
                
                // Draw cue stick when shooting
                if (this.isShooting && this.cueBall && !this.cueBall.potted) {
                    // Cue visualization based on pull-back and push-forward
                    const baseDist = 35;
                    const pullBack = this.pullBackDistance;
                    const pushForward = this.pushForwardDistance;
                    
                    // Cue position: pulled back, then pushed forward
                    const cueDistance = baseDist + pullBack - pushForward;
                    const cueStartX = this.cueBall.x - Math.cos(this.aimAngle) * cueDistance;
                    const cueStartY = this.cueBall.y - Math.sin(this.aimAngle) * cueDistance;
                    const cueEndX = this.cueBall.x - Math.cos(this.aimAngle) * (cueDistance + 180);
                    const cueEndY = this.cueBall.y - Math.sin(this.aimAngle) * (cueDistance + 180);
                    
                    // Cue stick gradient
                    const grad = ctx.createLinearGradient(cueStartX, cueStartY, cueEndX, cueEndY);
                    grad.addColorStop(0, '#d4a574');
                    grad.addColorStop(0.8, '#8b6f47');
                    grad.addColorStop(1, '#5a4a3a');
                    
                    ctx.strokeStyle = grad;
                    ctx.lineWidth = 10;
                    ctx.lineCap = 'round';
                    ctx.beginPath();
                    ctx.moveTo(cueStartX, cueStartY);
                    ctx.lineTo(cueEndX, cueEndY);
                    ctx.stroke();
                    
                    // Cue tip (blue chalk) - glow when close to contact
                    const distanceToContact = cueDistance - 12;
                    if (distanceToContact < 10) {
                        // Close to contact - add glow effect
                        ctx.fillStyle = 'rgba(100, 149, 237, 0.5)';
                        ctx.beginPath();
                        ctx.arc(cueStartX, cueStartY, 12, 0, Math.PI * 2);
                        ctx.fill();
                    }
                    
                    ctx.fillStyle = '#6495ED';
                    ctx.beginPath();
                    ctx.arc(cueStartX, cueStartY, 6, 0, Math.PI * 2);
                    ctx.fill();
                    
                    // Ghost guide showing pull-back
                    if (pullBack > 10) {
                        const ghostStartX = this.cueBall.x - Math.cos(this.aimAngle) * baseDist;
                        const ghostStartY = this.cueBall.y - Math.sin(this.aimAngle) * baseDist;
                        ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
                        ctx.lineWidth = 8;
                        ctx.setLineDash([10, 10]);
                        ctx.beginPath();
                        ctx.moveTo(ghostStartX, ghostStartY);
                        ctx.lineTo(cueStartX, cueStartY);
                        ctx.stroke();
                        ctx.setLineDash([]);
                    }
                    
                    // Contact indicator line (shows where contact will happen)
                    const contactPointX = this.cueBall.x - Math.cos(this.aimAngle) * 12;
                    const contactPointY = this.cueBall.y - Math.sin(this.aimAngle) * 12;
                    ctx.strokeStyle = distanceToContact < 5 ? 'rgba(255, 215, 0, 0.8)' : 'rgba(255, 255, 255, 0.4)';
                    ctx.lineWidth = 3;
                    ctx.setLineDash([5, 5]);
                    ctx.beginPath();
                    ctx.arc(contactPointX, contactPointY, 8, 0, Math.PI * 2);
                    ctx.stroke();
                    ctx.setLineDash([]);
                    
                    // Power meter
                    const meterX = this.cueBall.x + 35;
                    const meterY = this.cueBall.y - 50;
                    const meterHeight = 100;
                    const meterWidth = 12;
                    
                    // Meter background
                    ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
                    ctx.fillRect(meterX, meterY, meterWidth, meterHeight);
                    
                    // Meter fill
                    const powerPercent = this.shotPower / this.maxPower;
                    const fillHeight = meterHeight * powerPercent;
                    const powerGrad = ctx.createLinearGradient(meterX, meterY + meterHeight, meterX, meterY);
                    powerGrad.addColorStop(0, '#4ade80');
                    powerGrad.addColorStop(0.5, '#fbbf24');
                    powerGrad.addColorStop(1, '#ef4444');
                    
                    ctx.fillStyle = powerGrad;
                    ctx.fillRect(meterX, meterY + meterHeight - fillHeight, meterWidth, fillHeight);
                    
                    // Meter border
                    ctx.strokeStyle = 'rgba(255, 255, 255, 0.8)';
                    ctx.lineWidth = 2;
                    ctx.strokeRect(meterX, meterY, meterWidth, meterHeight);
                    
                    // Power percentage
                    ctx.fillStyle = 'white';
                    ctx.font = 'bold 12px Arial';
                    ctx.textAlign = 'center';
                    ctx.shadowColor = 'black';
                    ctx.shadowBlur = 4;
                    ctx.fillText(Math.round(powerPercent * 100) + '%', meterX + meterWidth / 2, meterY - 8);
                    ctx.shadowBlur = 0;
                }
                
                if (moving) {
                    statusEl.textContent = `? Balls rolling... (${activeBalls} on table)`;
                    statusEl.style.background = 'rgba(59, 130, 246, 0.9)';
                } else if (!moving && activeBalls > 0) {
                    statusEl.textContent = `? Ready to shoot! ${activeBalls} balls on table. Click to shoot!`;
                    statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
                }
                
                this.animationFrameId = requestAnimationFrame(() => this.animate());
            }
        };
        
        // Click-and-drag cue mechanic (pull back then push forward)
        let powerUpInterval = null;
        
        canvas.addEventListener('mousemove', (e) => {
            if (!game.cueBall || game.cueBall.potted) return;
            
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const scaleY = canvas.height / rect.height;
            game.mouseX = (e.clientX - rect.left) * scaleX;
            game.mouseY = (e.clientY - rect.top) * scaleY;
            
            if (game.isShooting) {
                // Aim is LOCKED - only track VERTICAL (Y-axis) mouse movement for power
                const deltaY = game.mouseY - game.dragStartY;
                
                if (deltaY > 0) {
                    // Mouse moved DOWN = Pulling back (away from table top)
                    game.pullBackDistance = Math.min(deltaY, 150);
                    game.shotPower = (game.pullBackDistance / 150) * game.maxPower;
                    game.pushForwardDistance = 0;
                } else {
                    // Mouse moved UP = Pushing forward (toward table top)
                    const pushDist = Math.abs(deltaY);
                    game.pushForwardDistance = Math.min(pushDist, game.pullBackDistance + 50);
                    
                    // Check if cue has made contact with ball (pushed far enough forward)
                    const cueDistance = 35 + game.pullBackDistance - game.pushForwardDistance;
                    if (cueDistance <= 12) { // Contact threshold
                        // CONTACT MADE! Fire the ball
                        const speed = Math.min(game.shotPower, game.maxPower) * 2.5;
                        if (speed > 0.5) {
                            game.cueBall.vx = Math.cos(game.aimAngle) * speed;
                            game.cueBall.vy = Math.sin(game.aimAngle) * speed;
                            statusEl.textContent = `?? Contact! Power: ${speed.toFixed(1)}`;
                            statusEl.style.background = 'rgba(59, 130, 246, 0.9)';
                        }
                        
                        // Reset shooting state
                        game.isShooting = false;
                        game.shotPower = 0;
                        game.pullBackDistance = 0;
                        game.pushForwardDistance = 0;
                    }
                }
                
                game.lastMouseTime = Date.now();
            } else {
                // Update aim angle when not shooting (aim is free)
                const dx = game.mouseX - game.cueBall.x;
                const dy = game.mouseY - game.cueBall.y;
                game.aimAngle = Math.atan2(dy, dx);
                game.isAiming = true;
            }
        });
        
        canvas.addEventListener('mousedown', (e) => {
            const cue = game.balls.find(b => b.num === 0 && !b.potted);
            if (!cue) return;
            
            // Check if any balls are moving
            const ballsMoving = game.balls.some(b => !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01));
            if (ballsMoving) return;
            
            // LOCK AIM ANGLE at current mouse position
            const dx = game.mouseX - game.cueBall.x;
            const dy = game.mouseY - game.cueBall.y;
            game.aimAngle = Math.atan2(dy, dx);
            
            // Store initial Y position for vertical tracking
            game.dragStartY = game.mouseY;
            
            // Start drag
            game.isShooting = true;
            game.dragStartX = game.mouseX;
            game.pullBackDistance = 0;
            game.pushForwardDistance = 0;
            game.shotPower = 0;
            game.dragStartTime = Date.now();
            game.lastMouseTime = Date.now();
            
            statusEl.textContent = '?? Aim locked! Move mouse DOWN to pull back, UP to strike!';
            statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
        });
        
        canvas.addEventListener('mouseup', (e) => {
            if (!game.isShooting) return;
            
            // Mouse released without making contact - reset
            game.isShooting = false;
            game.shotPower = 0;
            game.pullBackDistance = 0;
            game.pushForwardDistance = 0;
            
            statusEl.textContent = '?? Shot cancelled - push forward to make contact!';
            statusEl.style.background = 'rgba(239, 68, 68, 0.9)';
            
            setTimeout(() => {
                if (!game.isShooting) {
                    statusEl.textContent = '? Ready to shoot! Click and drag to aim & shoot.';
                    statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
                }
            }, 1500);
        });
        
        canvas.addEventListener('mouseleave', () => {
            game.isAiming = false;
        });
        
        // MAUI Integrations
        window.onload = () => {
            setTimeout(() => {
                document.body.style.opacity = 1;
            }, 100);
        };
        
        // Start game
        try {
            game.init();
            game.animate();
            console.log('Pool game initialized successfully');
        } catch (e) {
            statusEl.textContent = '? ERROR: ' + e.message;
            statusEl.style.background = '#EF4444';
            console.error('Pool game error:', e);
        }
        
        // Touch cue control
        let touchStartX, touchStartY, touchEndX, touchEndY;
        let isTouching = false;
        
        canvas.addEventListener('touchstart', (e) => {
            isTouching = true;
            const touch = e.touches[0];
            touchStartX = touch.clientX;
            touchStartY = touch.clientY;
            touchEndX = touch.clientX;
            touchEndY = touch.clientY;
            
            // Disable scrolling
            e.preventDefault();
        }, { passive: false });
        
        canvas.addEventListener('touchmove', (e) => {
            if (!isTouching) return;
            
            const touch = e.touches[0];
            touchEndX = touch.clientX;
            touchEndY = touch.clientY;
            
            // Calculate aim direction
            const dx = touchEndX - touchStartX;
            const dy = touchEndY - touchStartY;
            game.aimAngle = Math.atan2(dy, dx);
            
            // Update shot power based on distance
            const distance = Math.min(Math.sqrt(dx * dx + dy * dy), 100);
            game.shotPower = distance / 5;
            
            // Cue stick draws BACK as power increases (away from cue ball)
            const pullBackDistance = 35 + (game.shotPower / game.maxPower) * 100;
            const cueStartX = game.balls[0].x - Math.cos(game.aimAngle) * pullBackDistance;
            const cueStartY = game.balls[0].y - Math.sin(game.aimAngle) * pullBackDistance;
            const cueEndX = game.balls[0].x - Math.cos(game.aimAngle) * (pullBackDistance + 200);
            const cueEndY = game.balls[0].y - Math.sin(game.aimAngle) * (pullBackDistance + 200);
            
            // Draw cue stick
            const grad = ctx.createLinearGradient(cueStartX, cueStartY, cueEndX, cueEndY);
            grad.addColorStop(0, '#d4a574');
            grad.addColorStop(0.8, '#8b6f47');
            grad.addColorStop(1, '#5a4a3a');
            
            ctx.strokeStyle = grad;
            ctx.lineWidth = 11;
            ctx.lineCap = 'round';
            ctx.beginPath();
            ctx.moveTo(cueStartX, cueStartY);
            ctx.lineTo(cueEndX, cueEndY);
            ctx.stroke();
            
            // Cue tip
            ctx.fillStyle = '#6495ED';
            ctx.beginPath();
            ctx.arc(cueStartX, cueStartY, 7, 0, Math.PI * 2);
            ctx.fill();
        });
        
        canvas.addEventListener('touchend', (e) => {
            isTouching = false;
            
            // Find cue ball
            const cueBall = game.balls.find(b => b.num === 0 && !b.potted);
            if (!cueBall) return;
            
            // Calculate final shot velocity
            const dx = touchEndX - touchStartX;
            const dy = touchEndY - touchStartY;
            const dist = Math.sqrt(dx * dx + dy * dy);
            
            if (dist > 5) {
                const power = Math.min(dist / 15, 20);
                cueBall.vx = (dx / dist) * power;
                cueBall.vy = (dy / dist) * power;
                
                statusEl.textContent = `?? Shot fired! Power: ${power.toFixed(1)}`;
                statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
            }
        });
    </script>
</body>
</html>";

    public PoolGamePage()
    {
        InitializeComponent();
        LoadGame();
        
        ResetBtn.Clicked += (s, e) => LoadGame();
    }

    private void LoadGame()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== PoolGamePage.LoadGame() ===");
            System.Diagnostics.Debug.WriteLine($"HTML Length: {GameHtml.Length} chars");
            
            // Use HtmlWebViewSource for inline HTML
            var htmlSource = new HtmlWebViewSource
            {
                Html = GameHtml
            };
            
            GameWebView.Source = htmlSource;
            
            System.Diagnostics.Debug.WriteLine("WebView source set successfully");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in LoadGame: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Load Error", 
                    $"Failed to load pool game:\n\n{ex.Message}\n\nCheck Debug Output for details.", 
                    "OK");
            });
        }
    }
}
