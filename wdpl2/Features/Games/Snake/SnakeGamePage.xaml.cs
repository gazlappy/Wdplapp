namespace Wdpl2.Views;

public partial class SnakeGamePage : ContentPage
{
    public SnakeGamePage()
    {
        InitializeComponent();
        LoadGame();
    }

    private void LoadGame()
    {
        var html = GenerateSnakeGameHtml();
        GameWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private void OnNewGameClicked(object? sender, EventArgs e)
    {
        LoadGame();
    }

    private static string GenerateSnakeGameHtml()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, user-scalable=no'>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            background: #1a1a2e; 
            font-family: Arial, sans-serif;
            display: flex;
            flex-direction: column;
            align-items: center;
            justify-content: center;
            min-height: 100vh;
            padding: 20px;
            touch-action: none;
        }
        #gameContainer {
            display: flex;
            flex-direction: column;
            align-items: center;
            gap: 20px;
        }
        #score {
            color: #22c55e;
            font-size: 24px;
            font-weight: bold;
        }
        #highScore {
            color: #94a3b8;
            font-size: 16px;
        }
        canvas { 
            background: #0f172a;
            border: 4px solid #22c55e;
            border-radius: 8px;
            box-shadow: 0 0 30px rgba(34, 197, 94, 0.3);
        }
        #controls {
            display: grid;
            grid-template-columns: repeat(3, 60px);
            grid-template-rows: repeat(3, 60px);
            gap: 5px;
            margin-top: 20px;
        }
        .control-btn {
            width: 60px;
            height: 60px;
            background: #374151;
            border: none;
            border-radius: 10px;
            color: white;
            font-size: 24px;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
        }
        .control-btn:active {
            background: #22c55e;
            transform: scale(0.95);
        }
        .control-btn.empty {
            visibility: hidden;
        }
        #gameOver {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: rgba(0,0,0,0.9);
            padding: 40px;
            border-radius: 15px;
            text-align: center;
            display: none;
            color: white;
            z-index: 100;
        }
        #gameOver h2 {
            color: #ef4444;
            font-size: 32px;
            margin-bottom: 15px;
        }
        #gameOver p {
            font-size: 18px;
            margin-bottom: 20px;
        }
        #restartBtn {
            padding: 15px 30px;
            background: #22c55e;
            color: white;
            border: none;
            border-radius: 10px;
            font-size: 18px;
            cursor: pointer;
            font-weight: bold;
        }
        #startScreen {
            position: absolute;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: rgba(0,0,0,0.9);
            padding: 40px;
            border-radius: 15px;
            text-align: center;
            color: white;
            z-index: 100;
        }
        #startScreen h2 {
            font-size: 48px;
            margin-bottom: 20px;
        }
        #startBtn {
            padding: 15px 40px;
            background: #22c55e;
            color: white;
            border: none;
            border-radius: 10px;
            font-size: 20px;
            cursor: pointer;
            font-weight: bold;
        }
    </style>
</head>
<body>
    <div id='gameContainer'>
        <div id='score'>Score: 0</div>
        <div id='highScore'>High Score: 0</div>
        <canvas id='canvas' width='400' height='400'></canvas>
        
        <!-- Touch controls for mobile -->
        <div id='controls'>
            <div class='control-btn empty'></div>
            <button class='control-btn' onclick='changeDirection(""up"")'>??</button>
            <div class='control-btn empty'></div>
            <button class='control-btn' onclick='changeDirection(""left"")'>??</button>
            <div class='control-btn empty'></div>
            <button class='control-btn' onclick='changeDirection(""right"")'>??</button>
            <div class='control-btn empty'></div>
            <button class='control-btn' onclick='changeDirection(""down"")'>??</button>
            <div class='control-btn empty'></div>
        </div>
    </div>
    
    <div id='startScreen'>
        <h2>??</h2>
        <p>Use arrow keys or buttons to control the snake</p>
        <button id='startBtn' onclick='startGame()'>Start Game</button>
    </div>
    
    <div id='gameOver'>
        <h2>Game Over!</h2>
        <p id='finalScore'>Score: 0</p>
        <button id='restartBtn' onclick='startGame()'>Play Again</button>
    </div>

    <script>
        const canvas = document.getElementById('canvas');
        const ctx = canvas.getContext('2d');
        const gridSize = 20;
        const tileCount = canvas.width / gridSize;
        
        let snake = [];
        let food = {};
        let dx = 0;
        let dy = 0;
        let score = 0;
        let highScore = parseInt(localStorage.getItem('snakeHighScore') || '0');
        let gameLoop = null;
        let gameSpeed = 100;
        
        document.getElementById('highScore').textContent = 'High Score: ' + highScore;
        
        function startGame() {
            document.getElementById('startScreen').style.display = 'none';
            document.getElementById('gameOver').style.display = 'none';
            
            // Reset game state
            snake = [
                { x: 10, y: 10 },
                { x: 9, y: 10 },
                { x: 8, y: 10 }
            ];
            dx = 1;
            dy = 0;
            score = 0;
            gameSpeed = 100;
            updateScore();
            placeFood();
            
            if (gameLoop) clearInterval(gameLoop);
            gameLoop = setInterval(gameStep, gameSpeed);
        }
        
        function placeFood() {
            food = {
                x: Math.floor(Math.random() * tileCount),
                y: Math.floor(Math.random() * tileCount)
            };
            // Make sure food doesn't spawn on snake
            for (let segment of snake) {
                if (segment.x === food.x && segment.y === food.y) {
                    placeFood();
                    return;
                }
            }
        }
        
        function gameStep() {
            // Move snake
            const head = { x: snake[0].x + dx, y: snake[0].y + dy };
            
            // Check wall collision
            if (head.x < 0 || head.x >= tileCount || head.y < 0 || head.y >= tileCount) {
                gameOver();
                return;
            }
            
            // Check self collision
            for (let segment of snake) {
                if (head.x === segment.x && head.y === segment.y) {
                    gameOver();
                    return;
                }
            }
            
            snake.unshift(head);
            
            // Check food collision
            if (head.x === food.x && head.y === food.y) {
                score += 10;
                updateScore();
                placeFood();
                
                // Speed up every 50 points
                if (score % 50 === 0 && gameSpeed > 50) {
                    gameSpeed -= 10;
                    clearInterval(gameLoop);
                    gameLoop = setInterval(gameStep, gameSpeed);
                }
            } else {
                snake.pop();
            }
            
            draw();
        }
        
        function draw() {
            // Clear canvas
            ctx.fillStyle = '#0f172a';
            ctx.fillRect(0, 0, canvas.width, canvas.height);
            
            // Draw grid (subtle)
            ctx.strokeStyle = '#1e293b';
            ctx.lineWidth = 0.5;
            for (let i = 0; i <= tileCount; i++) {
                ctx.beginPath();
                ctx.moveTo(i * gridSize, 0);
                ctx.lineTo(i * gridSize, canvas.height);
                ctx.stroke();
                ctx.beginPath();
                ctx.moveTo(0, i * gridSize);
                ctx.lineTo(canvas.width, i * gridSize);
                ctx.stroke();
            }
            
            // Draw food
            ctx.fillStyle = '#ef4444';
            ctx.beginPath();
            ctx.arc(
                food.x * gridSize + gridSize / 2,
                food.y * gridSize + gridSize / 2,
                gridSize / 2 - 2,
                0,
                Math.PI * 2
            );
            ctx.fill();
            
            // Draw snake
            snake.forEach((segment, index) => {
                const gradient = ctx.createRadialGradient(
                    segment.x * gridSize + gridSize / 2,
                    segment.y * gridSize + gridSize / 2,
                    0,
                    segment.x * gridSize + gridSize / 2,
                    segment.y * gridSize + gridSize / 2,
                    gridSize / 2
                );
                
                if (index === 0) {
                    // Head
                    gradient.addColorStop(0, '#4ade80');
                    gradient.addColorStop(1, '#22c55e');
                } else {
                    // Body - fade color based on position
                    const fade = 1 - (index / snake.length) * 0.5;
                    gradient.addColorStop(0, `rgba(74, 222, 128, ${fade})`);
                    gradient.addColorStop(1, `rgba(34, 197, 94, ${fade})`);
                }
                
                ctx.fillStyle = gradient;
                ctx.fillRect(
                    segment.x * gridSize + 1,
                    segment.y * gridSize + 1,
                    gridSize - 2,
                    gridSize - 2
                );
                
                // Draw eyes on head
                if (index === 0) {
                    ctx.fillStyle = 'white';
                    const eyeSize = 3;
                    const eyeOffset = 5;
                    
                    if (dx === 1) { // Moving right
                        ctx.fillRect(segment.x * gridSize + 12, segment.y * gridSize + 5, eyeSize, eyeSize);
                        ctx.fillRect(segment.x * gridSize + 12, segment.y * gridSize + 12, eyeSize, eyeSize);
                    } else if (dx === -1) { // Moving left
                        ctx.fillRect(segment.x * gridSize + 5, segment.y * gridSize + 5, eyeSize, eyeSize);
                        ctx.fillRect(segment.x * gridSize + 5, segment.y * gridSize + 12, eyeSize, eyeSize);
                    } else if (dy === -1) { // Moving up
                        ctx.fillRect(segment.x * gridSize + 5, segment.y * gridSize + 5, eyeSize, eyeSize);
                        ctx.fillRect(segment.x * gridSize + 12, segment.y * gridSize + 5, eyeSize, eyeSize);
                    } else { // Moving down
                        ctx.fillRect(segment.x * gridSize + 5, segment.y * gridSize + 12, eyeSize, eyeSize);
                        ctx.fillRect(segment.x * gridSize + 12, segment.y * gridSize + 12, eyeSize, eyeSize);
                    }
                }
            });
        }
        
        function updateScore() {
            document.getElementById('score').textContent = 'Score: ' + score;
            if (score > highScore) {
                highScore = score;
                localStorage.setItem('snakeHighScore', highScore.toString());
                document.getElementById('highScore').textContent = 'High Score: ' + highScore;
            }
        }
        
        function gameOver() {
            clearInterval(gameLoop);
            document.getElementById('finalScore').textContent = 'Score: ' + score;
            document.getElementById('gameOver').style.display = 'block';
        }
        
        function changeDirection(dir) {
            switch(dir) {
                case 'up':
                    if (dy !== 1) { dx = 0; dy = -1; }
                    break;
                case 'down':
                    if (dy !== -1) { dx = 0; dy = 1; }
                    break;
                case 'left':
                    if (dx !== 1) { dx = -1; dy = 0; }
                    break;
                case 'right':
                    if (dx !== -1) { dx = 1; dy = 0; }
                    break;
            }
        }
        
        // Keyboard controls
        document.addEventListener('keydown', (e) => {
            switch(e.key) {
                case 'ArrowUp': changeDirection('up'); e.preventDefault(); break;
                case 'ArrowDown': changeDirection('down'); e.preventDefault(); break;
                case 'ArrowLeft': changeDirection('left'); e.preventDefault(); break;
                case 'ArrowRight': changeDirection('right'); e.preventDefault(); break;
            }
        });
        
        // Touch swipe controls
        let touchStartX = 0;
        let touchStartY = 0;
        
        canvas.addEventListener('touchstart', (e) => {
            touchStartX = e.touches[0].clientX;
            touchStartY = e.touches[0].clientY;
        });
        
        canvas.addEventListener('touchend', (e) => {
            const touchEndX = e.changedTouches[0].clientX;
            const touchEndY = e.changedTouches[0].clientY;
            
            const diffX = touchEndX - touchStartX;
            const diffY = touchEndY - touchStartY;
            
            if (Math.abs(diffX) > Math.abs(diffY)) {
                if (diffX > 30) changeDirection('right');
                else if (diffX < -30) changeDirection('left');
            } else {
                if (diffY > 30) changeDirection('down');
                else if (diffY < -30) changeDirection('up');
            }
        });
        
        // Initial draw
        draw();
    </script>
</body>
</html>";
    }
}
