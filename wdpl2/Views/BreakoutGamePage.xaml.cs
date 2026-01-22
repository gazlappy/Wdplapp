namespace Wdpl2.Views;

public partial class BreakoutGamePage : ContentPage
{
    public BreakoutGamePage()
    {
        InitializeComponent();
        LoadGame();
    }

    private void LoadGame()
    {
        var html = GenerateBreakoutGameHtml();
        GameWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private void OnNewGameClicked(object? sender, EventArgs e)
    {
        LoadGame();
    }

    private static string GenerateBreakoutGameHtml()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, user-scalable=no'>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            background: #0f172a; 
            font-family: Arial, sans-serif;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
            touch-action: none;
            overflow: hidden;
        }
        #stats {
            display: flex;
            gap: 40px;
            margin-bottom: 15px;
            color: white;
        }
        .stat {
            text-align: center;
        }
        .stat-value {
            font-size: 28px;
            font-weight: bold;
        }
        .stat-label {
            font-size: 12px;
            color: #94a3b8;
        }
        #score .stat-value { color: #ef4444; }
        #lives .stat-value { color: #22c55e; }
        #level .stat-value { color: #3b82f6; }
        
        canvas { 
            background: linear-gradient(180deg, #1e293b 0%, #0f172a 100%);
            border: 3px solid #334155;
            border-radius: 8px;
            box-shadow: 0 0 40px rgba(239, 68, 68, 0.2);
            display: block;
        }
        #overlay {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: rgba(0,0,0,0.95);
            padding: 40px 60px;
            border-radius: 15px;
            text-align: center;
            color: white;
            z-index: 100;
        }
        #overlay h2 {
            font-size: 36px;
            margin-bottom: 15px;
        }
        #overlay p {
            font-size: 18px;
            margin-bottom: 25px;
            color: #94a3b8;
        }
        #startBtn {
            padding: 15px 40px;
            background: #ef4444;
            color: white;
            border: none;
            border-radius: 10px;
            font-size: 20px;
            cursor: pointer;
            font-weight: bold;
        }
        #startBtn:hover { background: #dc2626; }
        .controls-hint {
            margin-top: 15px;
            color: #64748b;
            font-size: 14px;
        }
    </style>
</head>
<body>
    <div id='stats'>
        <div class='stat' id='score'>
            <div class='stat-value'>0</div>
            <div class='stat-label'>SCORE</div>
        </div>
        <div class='stat' id='lives'>
            <div class='stat-value'>??????</div>
            <div class='stat-label'>LIVES</div>
        </div>
        <div class='stat' id='level'>
            <div class='stat-value'>1</div>
            <div class='stat-label'>LEVEL</div>
        </div>
    </div>
    
    <canvas id='canvas' width='600' height='500'></canvas>
    
    <div id='overlay'>
        <h2>?? Brick Breaker</h2>
        <p>Break all the bricks to advance!</p>
        <button id='startBtn' onclick='startGame()'>Start Game</button>
        <div class='controls-hint'>Use mouse/touch or arrow keys to move paddle</div>
    </div>

    <script>
        const canvas = document.getElementById('canvas');
        const ctx = canvas.getContext('2d');
        
        // Game objects
        let paddle = { x: 250, y: 470, width: 100, height: 12, speed: 8 };
        let ball = { x: 300, y: 400, radius: 8, dx: 4, dy: -4, speed: 5 };
        let bricks = [];
        
        // Game state
        let score = 0;
        let lives = 3;
        let level = 1;
        let gameRunning = false;
        let animationId = null;
        
        // Brick configuration
        const brickRows = 5;
        const brickCols = 10;
        const brickWidth = 54;
        const brickHeight = 20;
        const brickPadding = 4;
        const brickOffsetTop = 50;
        const brickOffsetLeft = 15;
        
        // Colors for brick rows
        const brickColors = ['#ef4444', '#f97316', '#eab308', '#22c55e', '#3b82f6'];
        
        function createBricks() {
            bricks = [];
            for (let row = 0; row < brickRows; row++) {
                for (let col = 0; col < brickCols; col++) {
                    // Add some gaps randomly for variety
                    if (level > 1 && Math.random() < 0.1) continue;
                    
                    bricks.push({
                        x: brickOffsetLeft + col * (brickWidth + brickPadding),
                        y: brickOffsetTop + row * (brickHeight + brickPadding),
                        width: brickWidth,
                        height: brickHeight,
                        color: brickColors[row],
                        points: (brickRows - row) * 10,
                        hits: row < 2 && level > 2 ? 2 : 1 // Top rows need 2 hits on level 3+
                    });
                }
            }
        }
        
        function resetBall() {
            ball.x = paddle.x + paddle.width / 2;
            ball.y = paddle.y - ball.radius - 5;
            ball.dx = (Math.random() > 0.5 ? 1 : -1) * ball.speed;
            ball.dy = -ball.speed;
        }
        
        function startGame() {
            document.getElementById('overlay').style.display = 'none';
            score = 0;
            lives = 3;
            level = 1;
            ball.speed = 5;
            paddle.x = canvas.width / 2 - paddle.width / 2;
            
            createBricks();
            resetBall();
            updateStats();
            
            gameRunning = true;
            if (animationId) cancelAnimationFrame(animationId);
            gameLoop();
        }
        
        function nextLevel() {
            level++;
            ball.speed += 0.5;
            createBricks();
            resetBall();
            updateStats();
        }
        
        function updateStats() {
            document.querySelector('#score .stat-value').textContent = score;
            document.querySelector('#lives .stat-value').textContent = '??'.repeat(lives);
            document.querySelector('#level .stat-value').textContent = level;
        }
        
        function gameLoop() {
            if (!gameRunning) return;
            
            update();
            draw();
            animationId = requestAnimationFrame(gameLoop);
        }
        
        function update() {
            // Move ball
            ball.x += ball.dx;
            ball.y += ball.dy;
            
            // Wall collisions
            if (ball.x - ball.radius < 0 || ball.x + ball.radius > canvas.width) {
                ball.dx = -ball.dx;
            }
            if (ball.y - ball.radius < 0) {
                ball.dy = -ball.dy;
            }
            
            // Ball falls below paddle
            if (ball.y + ball.radius > canvas.height) {
                lives--;
                updateStats();
                
                if (lives <= 0) {
                    gameOver();
                    return;
                }
                
                resetBall();
            }
            
            // Paddle collision
            if (ball.y + ball.radius > paddle.y &&
                ball.y - ball.radius < paddle.y + paddle.height &&
                ball.x > paddle.x &&
                ball.x < paddle.x + paddle.width) {
                
                // Calculate bounce angle based on where ball hit paddle
                const hitPos = (ball.x - paddle.x) / paddle.width;
                const angle = (hitPos - 0.5) * Math.PI * 0.7; // -60 to +60 degrees
                
                const speed = Math.sqrt(ball.dx * ball.dx + ball.dy * ball.dy);
                ball.dx = Math.sin(angle) * speed;
                ball.dy = -Math.abs(Math.cos(angle) * speed);
                
                ball.y = paddle.y - ball.radius;
            }
            
            // Brick collisions
            for (let i = bricks.length - 1; i >= 0; i--) {
                const brick = bricks[i];
                
                if (ball.x + ball.radius > brick.x &&
                    ball.x - ball.radius < brick.x + brick.width &&
                    ball.y + ball.radius > brick.y &&
                    ball.y - ball.radius < brick.y + brick.height) {
                    
                    // Determine collision side
                    const overlapLeft = ball.x + ball.radius - brick.x;
                    const overlapRight = brick.x + brick.width - (ball.x - ball.radius);
                    const overlapTop = ball.y + ball.radius - brick.y;
                    const overlapBottom = brick.y + brick.height - (ball.y - ball.radius);
                    
                    const minOverlapX = Math.min(overlapLeft, overlapRight);
                    const minOverlapY = Math.min(overlapTop, overlapBottom);
                    
                    if (minOverlapX < minOverlapY) {
                        ball.dx = -ball.dx;
                    } else {
                        ball.dy = -ball.dy;
                    }
                    
                    brick.hits--;
                    
                    if (brick.hits <= 0) {
                        score += brick.points;
                        bricks.splice(i, 1);
                    } else {
                        // Darken brick color for damaged bricks
                        brick.color = '#6b7280';
                    }
                    
                    updateStats();
                    
                    // Check win
                    if (bricks.length === 0) {
                        nextLevel();
                    }
                    
                    break;
                }
            }
        }
        
        function draw() {
            // Clear
            ctx.fillStyle = '#0f172a';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            
            // Draw bricks
            bricks.forEach(brick => {
                const gradient = ctx.createLinearGradient(brick.x, brick.y, brick.x, brick.y + brick.height);
                gradient.addColorStop(0, brick.color);
                gradient.addColorStop(1, shadeColor(brick.color, -30));
                
                ctx.fillStyle = gradient;
                ctx.fillRect(brick.x, brick.y, brick.width, brick.height);
                
                // Brick highlight
                ctx.fillStyle = 'rgba(255,255,255,0.2)';
                ctx.fillRect(brick.x, brick.y, brick.width, 3);
            });
            
            // Draw paddle
            const paddleGradient = ctx.createLinearGradient(paddle.x, paddle.y, paddle.x, paddle.y + paddle.height);
            paddleGradient.addColorStop(0, '#e2e8f0');
            paddleGradient.addColorStop(1, '#94a3b8');
            ctx.fillStyle = paddleGradient;
            ctx.fillRect(paddle.x, paddle.y, paddle.width, paddle.height);
            
            // Draw ball
            ctx.beginPath();
            ctx.arc(ball.x, ball.y, ball.radius, 0, Math.PI * 2);
            const ballGradient = ctx.createRadialGradient(ball.x - 2, ball.y - 2, 0, ball.x, ball.y, ball.radius);
            ballGradient.addColorStop(0, '#ffffff');
            ballGradient.addColorStop(1, '#94a3b8');
            ctx.fillStyle = ballGradient;
            ctx.fill();
        }
        
        function shadeColor(color, percent) {
            const num = parseInt(color.replace('#', ''), 16);
            const amt = Math.round(2.55 * percent);
            const R = (num >> 16) + amt;
            const G = (num >> 8 & 0x00FF) + amt;
            const B = (num & 0x0000FF) + amt;
            return '#' + (0x1000000 + 
                (R < 255 ? R < 1 ? 0 : R : 255) * 0x10000 + 
                (G < 255 ? G < 1 ? 0 : G : 255) * 0x100 + 
                (B < 255 ? B < 1 ? 0 : B : 255)
            ).toString(16).slice(1);
        }
        
        function gameOver() {
            gameRunning = false;
            const overlay = document.getElementById('overlay');
            overlay.querySelector('h2').textContent = '?? Game Over!';
            overlay.querySelector('p').textContent = `Final Score: ${score}`;
            overlay.querySelector('#startBtn').textContent = 'Play Again';
            overlay.style.display = 'block';
        }
        
        // Controls
        let keys = {};
        
        document.addEventListener('keydown', (e) => {
            keys[e.key] = true;
            if (e.key === 'ArrowLeft' || e.key === 'ArrowRight') {
                e.preventDefault();
            }
        });
        
        document.addEventListener('keyup', (e) => {
            keys[e.key] = false;
        });
        
        // Mouse control
        canvas.addEventListener('mousemove', (e) => {
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const mouseX = (e.clientX - rect.left) * scaleX;
            paddle.x = mouseX - paddle.width / 2;
            
            // Keep paddle in bounds
            if (paddle.x < 0) paddle.x = 0;
            if (paddle.x + paddle.width > canvas.width) paddle.x = canvas.width - paddle.width;
        });
        
        // Touch control
        canvas.addEventListener('touchmove', (e) => {
            e.preventDefault();
            const rect = canvas.getBoundingClientRect();
            const scaleX = canvas.width / rect.width;
            const touchX = (e.touches[0].clientX - rect.left) * scaleX;
            paddle.x = touchX - paddle.width / 2;
            
            if (paddle.x < 0) paddle.x = 0;
            if (paddle.x + paddle.width > canvas.width) paddle.x = canvas.width - paddle.width;
        });
        
        // Keyboard movement in game loop
        setInterval(() => {
            if (!gameRunning) return;
            
            if (keys['ArrowLeft'] && paddle.x > 0) {
                paddle.x -= paddle.speed;
            }
            if (keys['ArrowRight'] && paddle.x + paddle.width < canvas.width) {
                paddle.x += paddle.speed;
            }
        }, 16);
        
        // Initial draw
        createBricks();
        resetBall();
        draw();
    </script>
</body>
</html>";
    }
}
