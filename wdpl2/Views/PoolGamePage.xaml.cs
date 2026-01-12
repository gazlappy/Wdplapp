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
                this.balls.push({
                    x: 200, y: 250,
                    vx: 0, vy: 0,
                    r: this.ballRadius,
                    color: 'white',
                    num: 0
                });
                
                // Triangle rack
                const startX = 700, startY = 250, gap = this.ballRadius * 2 + 0.5;
                const positions = [
                    [0, 0],
                    [1, -0.5], [1, 0.5],
                    [2, -1], [2, 0], [2, 1],
                    [3, -1.5], [3, -0.5], [3, 0.5], [3, 1.5],
                    [4, -2], [4, -1], [4, 0], [4, 1], [4, 2]
                ];
                
                // Black at apex
                this.balls.push({
                    x: startX,
                    y: startY,
                    vx: 0, vy: 0,
                    r: this.ballRadius,
                    color: 'black',
                    num: 8
                });
                
                // Reds and yellows
                for (let i = 1; i < positions.length; i++) {
                    const [row, col] = positions[i];
                    const isRed = (i % 2 === 1);
                    this.balls.push({
                        x: startX + row * gap,
                        y: startY + col * gap,
                        vx: 0, vy: 0,
                        r: this.ballRadius,
                        color: isRed ? 'red' : 'yellow',
                        num: isRed ? i : i + 8
                    });
                }
                
                statusEl.textContent = `? Game Ready! ${this.balls.length} balls on table. Click anywhere to shoot!`;
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
        
        // Click/touch to shoot
        canvas.addEventListener('click', (e) => {
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const scaleY = canvas.height / rect.height;
            const x = (e.clientX - rect.left) * scaleX;
            const y = (e.clientY - rect.top) * scaleY;
            
            const cue = game.balls.find(b => b.num === 0 && !b.potted);
            if (!cue) {
                statusEl.textContent = '?? Cue ball has been potted!';
                statusEl.style.background = 'rgba(239, 68, 68, 0.9)';
                return;
            }
            
            const dx = x - cue.x;
            const dy = y - cue.y;
            const dist = Math.sqrt(dx * dx + dy * dy);
            
            if (dist > 5) {
                const power = Math.min(dist / 15, 20);
                cue.vx = (dx / dist) * power;
                cue.vy = (dy / dist) * power;
                statusEl.textContent = `?? Shot fired! Power: ${power.toFixed(1)}`;
                statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
            }
        });
        
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
