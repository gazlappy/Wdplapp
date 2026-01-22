namespace Wdpl2.Views;

public partial class RetroFpsGamePage : ContentPage
{
    public RetroFpsGamePage()
    {
        InitializeComponent();
        LoadGame();
    }

    private void LoadGame()
    {
        var html = GenerateRetroFpsGameHtml();
        GameWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private void OnNewGameClicked(object? sender, EventArgs e)
    {
        LoadGame();
    }

    private static string GenerateRetroFpsGameHtml()
    {
        return """
<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, user-scalable=no'>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            background: #000; 
            font-family: 'Courier New', monospace;
            overflow: hidden;
            touch-action: none;
        }
        #gameContainer {
            display: flex;
            flex-direction: column;
            align-items: center;
            height: 100vh;
        }
        canvas { 
            display: block;
            image-rendering: pixelated;
            image-rendering: crisp-edges;
        }
        #hud {
            position: fixed;
            bottom: 0;
            left: 0;
            right: 0;
            height: 80px;
            background: linear-gradient(0deg, #1a0a0a 0%, #2a1515 100%);
            border-top: 4px solid #8b0000;
            display: flex;
            justify-content: space-around;
            align-items: center;
            padding: 10px 20px;
        }
        .hud-item {
            text-align: center;
            color: #ff6b6b;
        }
        .hud-label {
            font-size: 12px;
            color: #888;
            text-transform: uppercase;
        }
        .hud-value {
            font-size: 28px;
            font-weight: bold;
            color: #ef4444;
            text-shadow: 0 0 10px #ff0000;
        }
        .health { color: #22c55e; text-shadow: 0 0 10px #00ff00; }
        .ammo { color: #eab308; text-shadow: 0 0 10px #ffff00; }
        .kills { color: #ef4444; text-shadow: 0 0 10px #ff0000; }
        
        #weapon {
            position: fixed;
            bottom: 80px;
            left: 50%;
            transform: translateX(-50%);
            font-size: 80px;
            filter: drop-shadow(0 0 20px rgba(255,0,0,0.5));
            transition: transform 0.1s;
            z-index: 100;
        }
        #weapon.firing {
            transform: translateX(-50%) scale(1.2) rotate(-10deg);
        }
        
        #mobileControls {
            position: fixed;
            bottom: 100px;
            left: 0;
            right: 0;
            display: flex;
            justify-content: space-between;
            padding: 0 20px;
            pointer-events: none;
        }
        .control-group {
            display: flex;
            flex-direction: column;
            gap: 10px;
            pointer-events: auto;
        }
        .control-row {
            display: flex;
            gap: 10px;
            justify-content: center;
        }
        .mobile-btn {
            width: 60px;
            height: 60px;
            background: rgba(255, 100, 100, 0.3);
            border: 2px solid rgba(255, 100, 100, 0.6);
            border-radius: 10px;
            color: white;
            font-size: 24px;
            display: flex;
            align-items: center;
            justify-content: center;
            cursor: pointer;
            user-select: none;
        }
        .mobile-btn:active {
            background: rgba(255, 100, 100, 0.6);
        }
        .fire-btn {
            width: 100px;
            height: 100px;
            background: rgba(255, 0, 0, 0.4);
            border-color: rgba(255, 0, 0, 0.8);
            font-size: 40px;
        }
        
        #minimap {
            position: fixed;
            top: 10px;
            right: 10px;
            border: 2px solid #8b0000;
            background: rgba(0,0,0,0.8);
        }
        
        #message {
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            background: rgba(0,0,0,0.95);
            color: #ef4444;
            padding: 40px 60px;
            border: 4px solid #8b0000;
            text-align: center;
            display: none;
            z-index: 1000;
        }
        #message h2 {
            font-size: 48px;
            margin-bottom: 20px;
            text-shadow: 0 0 20px #ff0000;
        }
        #message p {
            font-size: 18px;
            color: #888;
            margin-bottom: 20px;
        }
        #message button {
            padding: 15px 40px;
            background: #dc2626;
            color: white;
            border: none;
            font-size: 20px;
            cursor: pointer;
            font-family: 'Courier New', monospace;
        }
        
        #crosshair {
            position: fixed;
            top: 50%;
            left: 50%;
            transform: translate(-50%, -50%);
            color: #ef4444;
            font-size: 30px;
            text-shadow: 0 0 10px #ff0000;
            pointer-events: none;
            z-index: 50;
        }
    </style>
</head>
<body>
    <div id="gameContainer">
        <canvas id="canvas"></canvas>
        <canvas id="minimap" width="150" height="150"></canvas>
        <div id="crosshair">+</div>
        <div id="weapon">??</div>
        
        <div id="hud">
            <div class="hud-item">
                <div class="hud-label">Health</div>
                <div class="hud-value health" id="health">100</div>
            </div>
            <div class="hud-item">
                <div class="hud-label">Ammo</div>
                <div class="hud-value ammo" id="ammo">50</div>
            </div>
            <div class="hud-item">
                <div class="hud-label">Kills</div>
                <div class="hud-value kills" id="kills">0</div>
            </div>
            <div class="hud-item">
                <div class="hud-label">Level</div>
                <div class="hud-value" id="level">1</div>
            </div>
        </div>
        
        <div id="mobileControls">
            <div class="control-group">
                <div class="control-row">
                    <button class="mobile-btn" data-key="w">?</button>
                </div>
                <div class="control-row">
                    <button class="mobile-btn" data-key="a">?</button>
                    <button class="mobile-btn" data-key="s">?</button>
                    <button class="mobile-btn" data-key="d">?</button>
                </div>
            </div>
            <div class="control-group">
                <div class="control-row">
                    <button class="mobile-btn" data-key="ArrowLeft">?</button>
                    <button class="mobile-btn fire-btn" data-key=" ">??</button>
                    <button class="mobile-btn" data-key="ArrowRight">?</button>
                </div>
            </div>
        </div>
        
        <div id="message">
            <h2 id="msgTitle">DUNGEON BLASTER</h2>
            <p id="msgText">Kill all demons to advance!</p>
            <button onclick="startGame()">START</button>
        </div>
    </div>

    <script>
        // Canvas setup
        const canvas = document.getElementById('canvas');
        const ctx = canvas.getContext('2d');
        const minimapCanvas = document.getElementById('minimap');
        const minimapCtx = minimapCanvas.getContext('2d');
        
        // Game dimensions
        const WIDTH = 800;
        const HEIGHT = 500;
        canvas.width = WIDTH;
        canvas.height = HEIGHT;
        
        // Map (1 = wall, 0 = empty, 2 = door)
        let map = [];
        const MAP_SIZE = 16;
        const TILE_SIZE = 64;
        
        // Player
        let player = {
            x: 1.5 * TILE_SIZE,
            y: 1.5 * TILE_SIZE,
            angle: 0,
            health: 100,
            ammo: 50,
            kills: 0,
            level: 1
        };
        
        // Enemies
        let enemies = [];
        
        // Game state
        let gameRunning = false;
        let keys = {};
        
        // Raycasting settings
        const FOV = Math.PI / 3; // 60 degrees
        const NUM_RAYS = 200;
        const MAX_DEPTH = 16 * TILE_SIZE;
        
        // Generate random map
        function generateMap() {
            map = [];
            for (let y = 0; y < MAP_SIZE; y++) {
                map[y] = [];
                for (let x = 0; x < MAP_SIZE; x++) {
                    // Borders are always walls
                    if (x === 0 || y === 0 || x === MAP_SIZE - 1 || y === MAP_SIZE - 1) {
                        map[y][x] = 1;
                    } else if (x === 1 && y === 1) {
                        // Player start - always empty
                        map[y][x] = 0;
                    } else {
                        // Random walls (30% chance)
                        map[y][x] = Math.random() < 0.3 ? 1 : 0;
                    }
                }
            }
            
            // Ensure path exists (simple flood fill check)
            ensurePathExists();
        }
        
        function ensurePathExists() {
            // Clear some walls to ensure navigability
            for (let i = 0; i < 20; i++) {
                const x = 1 + Math.floor(Math.random() * (MAP_SIZE - 2));
                const y = 1 + Math.floor(Math.random() * (MAP_SIZE - 2));
                map[y][x] = 0;
            }
        }
        
        // Spawn enemies
        function spawnEnemies() {
            enemies = [];
            const numEnemies = 3 + player.level * 2;
            
            for (let i = 0; i < numEnemies; i++) {
                let ex, ey;
                do {
                    ex = 2 + Math.floor(Math.random() * (MAP_SIZE - 4));
                    ey = 2 + Math.floor(Math.random() * (MAP_SIZE - 4));
                } while (map[ey][ex] !== 0);
                
                enemies.push({
                    x: (ex + 0.5) * TILE_SIZE,
                    y: (ey + 0.5) * TILE_SIZE,
                    health: 30 + player.level * 10,
                    speed: 1 + player.level * 0.2,
                    damage: 5 + player.level * 2,
                    lastAttack: 0,
                    type: Math.floor(Math.random() * 3) // Different enemy types
                });
            }
        }
        
        // Cast a single ray
        function castRay(angle) {
            const sin = Math.sin(angle);
            const cos = Math.cos(angle);
            
            let distance = 0;
            let hitWall = false;
            let wallType = 0;
            
            while (!hitWall && distance < MAX_DEPTH) {
                distance += 1;
                
                const testX = player.x + cos * distance;
                const testY = player.y + sin * distance;
                
                const mapX = Math.floor(testX / TILE_SIZE);
                const mapY = Math.floor(testY / TILE_SIZE);
                
                if (mapX < 0 || mapX >= MAP_SIZE || mapY < 0 || mapY >= MAP_SIZE) {
                    hitWall = true;
                    wallType = 1;
                } else if (map[mapY][mapX] > 0) {
                    hitWall = true;
                    wallType = map[mapY][mapX];
                }
            }
            
            return { distance, wallType };
        }
        
        // Render 3D view
        function render3D() {
            // Sky
            const skyGradient = ctx.createLinearGradient(0, 0, 0, HEIGHT / 2);
            skyGradient.addColorStop(0, '#1a0a0a');
            skyGradient.addColorStop(1, '#3d1515');
            ctx.fillStyle = skyGradient;
            ctx.fillRect(0, 0, WIDTH, HEIGHT / 2);
            
            // Floor
            const floorGradient = ctx.createLinearGradient(0, HEIGHT / 2, 0, HEIGHT);
            floorGradient.addColorStop(0, '#2a1a1a');
            floorGradient.addColorStop(1, '#0a0505');
            ctx.fillStyle = floorGradient;
            ctx.fillRect(0, HEIGHT / 2, WIDTH, HEIGHT / 2);
            
            // Cast rays and draw walls
            const rayWidth = WIDTH / NUM_RAYS;
            
            for (let i = 0; i < NUM_RAYS; i++) {
                const rayAngle = player.angle - FOV / 2 + (i / NUM_RAYS) * FOV;
                const ray = castRay(rayAngle);
                
                // Fix fisheye effect
                const correctedDist = ray.distance * Math.cos(rayAngle - player.angle);
                
                // Calculate wall height
                const wallHeight = Math.min((TILE_SIZE * HEIGHT) / correctedDist, HEIGHT * 2);
                const wallTop = (HEIGHT - wallHeight) / 2;
                
                // Wall shading based on distance
                const shade = Math.max(0, 1 - correctedDist / MAX_DEPTH);
                const r = Math.floor(139 * shade);
                const g = Math.floor(0 * shade);
                const b = Math.floor(0 * shade);
                
                ctx.fillStyle = `rgb(${r}, ${g}, ${b})`;
                ctx.fillRect(i * rayWidth, wallTop, rayWidth + 1, wallHeight);
                
                // Wall edge highlight
                if (i > 0) {
                    ctx.fillStyle = `rgba(255, 100, 100, ${shade * 0.1})`;
                    ctx.fillRect(i * rayWidth, wallTop, 1, wallHeight);
                }
            }
        }
        
        // Render enemies (simple sprites)
        function renderEnemies() {
            // Sort enemies by distance (far to near)
            const sortedEnemies = enemies.map(e => {
                const dx = e.x - player.x;
                const dy = e.y - player.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                const angle = Math.atan2(dy, dx);
                return { ...e, dist, angle };
            }).sort((a, b) => b.dist - a.dist);
            
            sortedEnemies.forEach(enemy => {
                // Check if enemy is in FOV
                let angleDiff = enemy.angle - player.angle;
                while (angleDiff > Math.PI) angleDiff -= Math.PI * 2;
                while (angleDiff < -Math.PI) angleDiff += Math.PI * 2;
                
                if (Math.abs(angleDiff) < FOV / 2 + 0.2) {
                    // Calculate screen position
                    const screenX = WIDTH / 2 + (angleDiff / FOV) * WIDTH;
                    
                    // Calculate size based on distance
                    const size = Math.min((TILE_SIZE * HEIGHT * 0.8) / enemy.dist, HEIGHT);
                    const screenY = HEIGHT / 2 - size / 4;
                    
                    // Check if enemy is visible (not behind wall)
                    const ray = castRay(enemy.angle);
                    if (enemy.dist < ray.distance) {
                        // Draw enemy sprite
                        const shade = Math.max(0.3, 1 - enemy.dist / MAX_DEPTH);
                        
                        // Enemy body (different colors for types)
                        const colors = ['#ff4444', '#44ff44', '#4444ff'];
                        const baseColor = colors[enemy.type % 3];
                        
                        ctx.fillStyle = baseColor;
                        ctx.globalAlpha = shade;
                        
                        // Simple demon sprite
                        ctx.beginPath();
                        ctx.arc(screenX, screenY + size * 0.3, size * 0.3, 0, Math.PI * 2);
                        ctx.fill();
                        
                        // Eyes
                        ctx.fillStyle = '#ffff00';
                        ctx.beginPath();
                        ctx.arc(screenX - size * 0.1, screenY + size * 0.25, size * 0.06, 0, Math.PI * 2);
                        ctx.arc(screenX + size * 0.1, screenY + size * 0.25, size * 0.06, 0, Math.PI * 2);
                        ctx.fill();
                        
                        // Horns
                        ctx.fillStyle = baseColor;
                        ctx.beginPath();
                        ctx.moveTo(screenX - size * 0.25, screenY + size * 0.1);
                        ctx.lineTo(screenX - size * 0.15, screenY - size * 0.1);
                        ctx.lineTo(screenX - size * 0.1, screenY + size * 0.15);
                        ctx.fill();
                        ctx.beginPath();
                        ctx.moveTo(screenX + size * 0.25, screenY + size * 0.1);
                        ctx.lineTo(screenX + size * 0.15, screenY - size * 0.1);
                        ctx.lineTo(screenX + size * 0.1, screenY + size * 0.15);
                        ctx.fill();
                        
                        ctx.globalAlpha = 1;
                        
                        // Health bar
                        const maxHealth = 30 + player.level * 10;
                        const healthPercent = enemy.health / maxHealth;
                        ctx.fillStyle = '#333';
                        ctx.fillRect(screenX - size * 0.3, screenY - 10, size * 0.6, 6);
                        ctx.fillStyle = healthPercent > 0.5 ? '#22c55e' : healthPercent > 0.25 ? '#eab308' : '#ef4444';
                        ctx.fillRect(screenX - size * 0.3, screenY - 10, size * 0.6 * healthPercent, 6);
                    }
                }
            });
        }
        
        // Render minimap
        function renderMinimap() {
            const scale = minimapCanvas.width / MAP_SIZE;
            
            minimapCtx.fillStyle = '#000';
            minimapCtx.fillRect(0, 0, minimapCanvas.width, minimapCanvas.height);
            
            // Draw map
            for (let y = 0; y < MAP_SIZE; y++) {
                for (let x = 0; x < MAP_SIZE; x++) {
                    if (map[y][x] === 1) {
                        minimapCtx.fillStyle = '#8b0000';
                        minimapCtx.fillRect(x * scale, y * scale, scale, scale);
                    }
                }
            }
            
            // Draw enemies
            minimapCtx.fillStyle = '#ff0000';
            enemies.forEach(e => {
                const ex = (e.x / TILE_SIZE) * scale;
                const ey = (e.y / TILE_SIZE) * scale;
                minimapCtx.beginPath();
                minimapCtx.arc(ex, ey, 3, 0, Math.PI * 2);
                minimapCtx.fill();
            });
            
            // Draw player
            const px = (player.x / TILE_SIZE) * scale;
            const py = (player.y / TILE_SIZE) * scale;
            
            minimapCtx.fillStyle = '#22c55e';
            minimapCtx.beginPath();
            minimapCtx.arc(px, py, 4, 0, Math.PI * 2);
            minimapCtx.fill();
            
            // Player direction
            minimapCtx.strokeStyle = '#22c55e';
            minimapCtx.lineWidth = 2;
            minimapCtx.beginPath();
            minimapCtx.moveTo(px, py);
            minimapCtx.lineTo(px + Math.cos(player.angle) * 10, py + Math.sin(player.angle) * 10);
            minimapCtx.stroke();
        }
        
        // Update game logic
        function update() {
            if (!gameRunning) return;
            
            const moveSpeed = 3;
            const rotSpeed = 0.05;
            
            // Rotation
            if (keys['ArrowLeft'] || keys['q']) player.angle -= rotSpeed;
            if (keys['ArrowRight'] || keys['e']) player.angle += rotSpeed;
            
            // Movement
            let dx = 0, dy = 0;
            if (keys['w'] || keys['ArrowUp']) {
                dx += Math.cos(player.angle) * moveSpeed;
                dy += Math.sin(player.angle) * moveSpeed;
            }
            if (keys['s'] || keys['ArrowDown']) {
                dx -= Math.cos(player.angle) * moveSpeed;
                dy -= Math.sin(player.angle) * moveSpeed;
            }
            if (keys['a']) {
                dx += Math.cos(player.angle - Math.PI / 2) * moveSpeed;
                dy += Math.sin(player.angle - Math.PI / 2) * moveSpeed;
            }
            if (keys['d']) {
                dx += Math.cos(player.angle + Math.PI / 2) * moveSpeed;
                dy += Math.sin(player.angle + Math.PI / 2) * moveSpeed;
            }
            
            // Collision detection
            const newX = player.x + dx;
            const newY = player.y + dy;
            const margin = 10;
            
            const mapX = Math.floor(newX / TILE_SIZE);
            const mapY = Math.floor(newY / TILE_SIZE);
            
            if (map[Math.floor(player.y / TILE_SIZE)][mapX] === 0) {
                player.x = newX;
            }
            if (map[mapY][Math.floor(player.x / TILE_SIZE)] === 0) {
                player.y = newY;
            }
            
            // Update enemies
            updateEnemies();
            
            // Check death
            if (player.health <= 0) {
                gameOver(false);
            }
            
            // Check win
            if (enemies.length === 0) {
                nextLevel();
            }
            
            updateHUD();
        }
        
        function updateEnemies() {
            const now = Date.now();
            
            enemies.forEach(enemy => {
                // Move towards player
                const dx = player.x - enemy.x;
                const dy = player.y - enemy.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                
                if (dist > 50) {
                    const moveX = (dx / dist) * enemy.speed;
                    const moveY = (dy / dist) * enemy.speed;
                    
                    const newX = enemy.x + moveX;
                    const newY = enemy.y + moveY;
                    
                    const mapX = Math.floor(newX / TILE_SIZE);
                    const mapY = Math.floor(newY / TILE_SIZE);
                    
                    if (map[mapY] && map[mapY][mapX] === 0) {
                        enemy.x = newX;
                        enemy.y = newY;
                    }
                }
                
                // Attack player if close
                if (dist < 60 && now - enemy.lastAttack > 1000) {
                    player.health -= enemy.damage;
                    enemy.lastAttack = now;
                    
                    // Screen flash
                    ctx.fillStyle = 'rgba(255, 0, 0, 0.3)';
                    ctx.fillRect(0, 0, WIDTH, HEIGHT);
                }
            });
        }
        
        // Shooting
        let canShoot = true;
        function shoot() {
            if (!gameRunning || !canShoot || player.ammo <= 0) return;
            
            canShoot = false;
            player.ammo--;
            
            // Weapon animation
            const weapon = document.getElementById('weapon');
            weapon.classList.add('firing');
            setTimeout(() => weapon.classList.remove('firing'), 100);
            
            // Check if hitting any enemy
            enemies.forEach((enemy, index) => {
                const dx = enemy.x - player.x;
                const dy = enemy.y - player.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                const angle = Math.atan2(dy, dx);
                
                let angleDiff = angle - player.angle;
                while (angleDiff > Math.PI) angleDiff -= Math.PI * 2;
                while (angleDiff < -Math.PI) angleDiff += Math.PI * 2;
                
                // Hit detection (cone in front of player)
                if (Math.abs(angleDiff) < 0.15 && dist < MAX_DEPTH * 0.8) {
                    // Check if wall between player and enemy
                    const ray = castRay(angle);
                    if (dist < ray.distance) {
                        enemy.health -= 20;
                        
                        if (enemy.health <= 0) {
                            enemies.splice(index, 1);
                            player.kills++;
                            
                            // Chance to drop ammo
                            if (Math.random() < 0.3) {
                                player.ammo += 10;
                            }
                        }
                    }
                }
            });
            
            setTimeout(() => canShoot = true, 200);
        }
        
        function updateHUD() {
            document.getElementById('health').textContent = Math.max(0, player.health);
            document.getElementById('ammo').textContent = player.ammo;
            document.getElementById('kills').textContent = player.kills;
            document.getElementById('level').textContent = player.level;
        }
        
        function nextLevel() {
            player.level++;
            player.health = Math.min(100, player.health + 25);
            player.ammo += 20;
            
            showMessage('LEVEL ' + player.level, 'More demons await...', () => {
                generateMap();
                player.x = 1.5 * TILE_SIZE;
                player.y = 1.5 * TILE_SIZE;
                spawnEnemies();
            });
        }
        
        function gameOver(won) {
            gameRunning = false;
            if (won) {
                showMessage('VICTORY!', `Kills: ${player.kills}`, startGame);
            } else {
                showMessage('YOU DIED', `Kills: ${player.kills}`, startGame);
            }
        }
        
        function showMessage(title, text, callback) {
            document.getElementById('msgTitle').textContent = title;
            document.getElementById('msgText').textContent = text;
            document.getElementById('message').style.display = 'block';
            
            const btn = document.querySelector('#message button');
            btn.onclick = () => {
                document.getElementById('message').style.display = 'none';
                if (callback) callback();
            };
        }
        
        function startGame() {
            player = {
                x: 1.5 * TILE_SIZE,
                y: 1.5 * TILE_SIZE,
                angle: 0,
                health: 100,
                ammo: 50,
                kills: 0,
                level: 1
            };
            
            generateMap();
            spawnEnemies();
            
            document.getElementById('message').style.display = 'none';
            gameRunning = true;
        }
        
        // Game loop
        function gameLoop() {
            update();
            render3D();
            renderEnemies();
            renderMinimap();
            requestAnimationFrame(gameLoop);
        }
        
        // Input handling
        document.addEventListener('keydown', e => {
            keys[e.key] = true;
            if (e.key === ' ') {
                e.preventDefault();
                shoot();
            }
        });
        
        document.addEventListener('keyup', e => {
            keys[e.key] = false;
        });
        
        // Mobile controls
        document.querySelectorAll('.mobile-btn').forEach(btn => {
            const key = btn.dataset.key;
            
            btn.addEventListener('touchstart', e => {
                e.preventDefault();
                keys[key] = true;
                if (key === ' ') shoot();
            });
            
            btn.addEventListener('touchend', e => {
                e.preventDefault();
                keys[key] = false;
            });
            
            btn.addEventListener('mousedown', () => {
                keys[key] = true;
                if (key === ' ') shoot();
            });
            
            btn.addEventListener('mouseup', () => {
                keys[key] = false;
            });
        });
        
        // Mouse look (for desktop)
        let mouseLocked = false;
        canvas.addEventListener('click', () => {
            if (!mouseLocked) {
                canvas.requestPointerLock?.();
            } else {
                shoot();
            }
        });
        
        document.addEventListener('pointerlockchange', () => {
            mouseLocked = document.pointerLockElement === canvas;
        });
        
        document.addEventListener('mousemove', e => {
            if (mouseLocked && gameRunning) {
                player.angle += e.movementX * 0.003;
            }
        });
        
        // Show start screen
        showMessage('DUNGEON BLASTER', 'WASD to move, Mouse/Arrows to look, Space/Click to shoot', null);
        
        // Start game loop
        gameLoop();
    </script>
</body>
</html>
""";
    }
}
