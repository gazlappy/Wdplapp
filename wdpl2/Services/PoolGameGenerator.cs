using System.Text;

namespace Wdpl2.Services
{
    /// <summary>
    /// Generates an embedded UK 8-ball pool game following EPA International Rules
    /// Optimized for smooth 60fps performance
    /// </summary>
    public static class PoolGameGenerator
    {
        public static string GeneratePoolGameHtml(string leagueName)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <title>UK 8-Ball Pool - {leagueName}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ font-family: 'Segoe UI', sans-serif; background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); min-height: 100vh; padding: 20px; }}
        .game-container {{ max-width: 1400px; margin: 0 auto; background: rgba(255,255,255,0.95); border-radius: 16px; padding: 20px; box-shadow: 0 20px 60px rgba(0,0,0,0.3); }}
        .game-header {{ display: flex; justify-content: space-between; align-items: center; margin-bottom: 20px; padding-bottom: 20px; border-bottom: 2px solid #e0e0e0; }}
        .game-header h1 {{ font-size: 2rem; color: #1e3c72; }}
        .controls {{ display: flex; gap: 10px; }}
        .btn {{ padding: 10px 20px; border: none; border-radius: 8px; font-size: 1rem; font-weight: 600; cursor: pointer; transition: all 0.3s; background: #3B82F6; color: white; }}
        .btn:hover {{ background: #2563EB; transform: translateY(-2px); }}
        .game-info {{ display: grid; grid-template-columns: 1fr auto 1fr; gap: 20px; margin-bottom: 20px; align-items: center; }}
        .player-panel {{ background: linear-gradient(135deg, #f5f7fa 0%, #e8edf3 100%); padding: 20px; border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.1); transition: all 0.3s; }}
        .player-panel.active {{ background: linear-gradient(135deg, #4ade80 0%, #22c55e 100%); color: white; transform: scale(1.05); }}
        .player-balls {{ font-size: 1.5rem; font-weight: bold; margin: 10px 0; }}
        .game-status {{ text-align: center; padding: 20px; }}
        .turn-indicator {{ font-size: 1.3rem; font-weight: bold; color: #1e3c72; }}
        .foul-indicator {{ background: #ef4444; color: white; padding: 10px 20px; border-radius: 8px; margin-top: 10px; animation: pulse 1s infinite; }}
        @keyframes pulse {{ 0%, 100% {{ opacity: 1; }} 50% {{ opacity: 0.7; }} }}
        .table-container {{ position: relative; background: #8B4513; padding: 40px; border-radius: 16px; margin-bottom: 20px; }}
        #poolTable {{ display: block; margin: 0 auto; background: #1a7f37; border-radius: 8px; cursor: crosshair; }}
        .power-bar-container {{ position: absolute; bottom: 10px; left: 50%; transform: translateX(-50%); width: 300px; background: rgba(0,0,0,0.7); padding: 15px; border-radius: 12px; }}
        .power-bar {{ width: 100%; height: 20px; background: rgba(255,255,255,0.2); border-radius: 10px; overflow: hidden; }}
        .power-fill {{ height: 100%; background: linear-gradient(90deg, #4ade80 0%, #fbbf24 50%, #ef4444 100%); transition: width 0.05s; }}
        .instructions {{ background: #f8fafc; padding: 20px; border-radius: 12px; border: 2px dashed #cbd5e1; }}
        .instructions ul {{ list-style: none; padding-left: 0; }}
        .instructions li {{ padding: 8px 0; color: #475569; }}
        .modal {{ display: none; position: fixed; z-index: 1000; left: 0; top: 0; width: 100%; height: 100%; background: rgba(0,0,0,0.7); overflow-y: auto; }}
        .modal-content {{ background: white; margin: 30px auto; padding: 30px; border-radius: 16px; width: 90%; max-width: 700px; max-height: 90vh; overflow-y: auto; }}
        .close {{ color: #aaa; float: right; font-size: 28px; font-weight: bold; cursor: pointer; }}
        .close:hover {{ color: #ef4444; }}
        @media (max-width: 768px) {{ .game-info {{ grid-template-columns: 1fr; }} }}
    </style>
</head>
<body>
    <div class=""game-container"">
        <div class=""game-header"">
            <h1>?? UK 8-Ball Pool</h1>
            <div class=""controls"">
                <button id=""newGameBtn"" class=""btn"">New Game</button>
                <button id=""rulesBtn"" class=""btn"">EPA Rules</button>
                <button id=""ballInHandBtn"" class=""btn"">? Move Cue Ball</button>
                <button class=""btn"" onclick=""window.location.href='index.html'"">?? Back</button>
            </div>
        </div>
        <div class=""game-info"">
            <div class=""player-panel"" id=""player1Panel"">
                <h3>Player 1</h3>
                <div class=""player-balls"" id=""player1Balls"">-</div>
                <div id=""player1Status""></div>
            </div>
            <div class=""game-status"">
                <div class=""turn-indicator"" id=""turnIndicator"">Player 1's Turn</div>
                <div id=""gameMessage"">Legal Break: 3+ points required</div>
                <div id=""shotInfo"" style=""font-size:0.9rem;color:#888;margin-top:5px"">Click and hold to shoot</div>
                <div class=""foul-indicator"" id=""foulIndicator"" style=""display:none"">?? FOUL - Ball in Hand</div>
            </div>
            <div class=""player-panel"" id=""player2Panel"">
                <h3>Player 2</h3>
                <div class=""player-balls"" id=""player2Balls"">-</div>
                <div id=""player2Status""></div>
            </div>
        </div>
        <div class=""table-container"">
            <canvas id=""poolTable"" width=""1200"" height=""600""></canvas>
            <div class=""power-bar-container"" id=""powerBarContainer"" style=""display:none"">
                <div style=""color:white;font-weight:bold;margin-bottom:8px;text-align:center"">Shot Power</div>
                <div class=""power-bar""><div class=""power-fill"" id=""powerFill""></div></div>
            </div>
        </div>
        <div class=""instructions"">
            <p><strong>EPA International Rules (Simplified):</strong></p>
            <ul>
                <li><strong>Legal Break:</strong> 3+ points (1 per ball potted, 1 per ball past center)</li>
                <li><strong>Table Open:</strong> First pot decides colors (reds/yellows)</li>
                <li><strong>Legal Shot:</strong> Hit your color first, then pot OR hit cushion</li>
                <li><strong>Fouls:</strong> In-off, no cushion, wrong ball = Ball in Hand</li>
                <li><strong>Win:</strong> Pot all your colors, then legally pot black</li>
            </ul>
        </div>
    </div>
    <div id=""rulesModal"" class=""modal"">
        <div class=""modal-content"">
            <span class=""close"" id=""closeRules"">&times;</span>
            <h2>EPA International 8-Ball Rules</h2>
            <div style=""margin-top:20px;clear:both"">
                <h3 style=""color:#1e3c72"">The Break</h3>
                <p><strong>Legal Break:</strong> Score 3+ points (1 per ball potted/past center)</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Table Open</h3>
                <p>After break, first legal pot decides groups</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Legal Shot</h3>
                <p>Hit your color first, then pot a ball OR hit cushion</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Fouls</h3>
                <p>In-off, no cushion hit, wrong ball first = Ball in Hand anywhere</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Winning</h3>
                <p>Clear all your colors, then legally pot the black 8-ball</p>
                <p style=""margin-top:15px;padding:10px;background:#f0f9ff;border-left:4px solid #3B82F6""><em>Full rules: <a href=""https://www.epa.org.uk/rules/international_2b.php"" target=""_blank"">www.epa.org.uk</a></em></p>
            </div>
        </div>
    </div>
    <script>{GetPoolGameJS()}</script>
    <div id=""debugInfo"" style=""position:fixed;top:10px;right:10px;background:rgba(0,0,0,0.8);color:lime;padding:10px;border-radius:8px;font-family:monospace;font-size:12px;z-index:10000;"">
        Initializing...
    </div>
</body>
</html>";
        }
        
        private static string GetPoolGameJS()
        {
            return @"
class PoolGame {
    constructor() {
        console.log('PoolGame constructor started');
        this.canvas = document.getElementById('poolTable');
        if (!this.canvas) {
            throw new Error('Canvas element not found!');
        }
        console.log('Canvas found:', this.canvas.width, 'x', this.canvas.height);
        
        this.ctx = this.canvas.getContext('2d', {alpha: false});
        if (!this.ctx) {
            throw new Error('Could not get 2D context!');
        }
        console.log('Context acquired');
        
        this.width = 1200;
        this.height = 600;
        this.balls = [];
        this.cueBall = null;
        this.isAiming = false;
        this.isShooting = false;
        this.shotPower = 0;
        this.maxPower = 80;
        this.aimAngle = 0;
        this.pullBackDistance = 0;
        this.pushForwardDistance = 0;
        this.dragStartX = 0;
        this.dragStartY = 0;
        this.dragStartTime = 0;
        this.lastMouseTime = 0;
        this.initialDotProduct = 0;
        this.ballInHand = false;
        this.tableOpen = true;
        this.isBreak = true;
        this.currentPlayer = 1;
        this.player1Balls = null;
        this.player2Balls = null;
        this.player1Potted = [];
        this.player2Potted = [];
        this.gameOver = false;
        this.winner = null;
        this.friction = 0.980;
        this.cushionRestitution = 0.75;
        this.pocketRadius = 28;
        this.ballRadius = 12;
        this.centerLineY = 300;
        this.pockets = [
            {x: 30, y: 30}, {x: 600, y: 20}, {x: 1170, y: 30},
            {x: 30, y: 570}, {x: 600, y: 580}, {x: 1170, y: 570}
        ];
        
        console.log('Properties initialized');
        this.setupEventListeners();
        console.log('Event listeners setup');
        this.initGame();
        console.log('Game initialized with', this.balls.length, 'balls');
    }
    
    initGame() {
        this.balls = [];
        this.player1Balls = null;
        this.player2Balls = null;
        this.player1Potted = [];
        this.player2Potted = [];
        this.currentPlayer = 1;
        this.gameOver = false;
        this.winner = null;
        this.tableOpen = true;
        this.isBreak = true;
        this.ballInHand = false;
        
        this.cueBall = {x: 240, y: 300, vx: 0, vy: 0, radius: this.ballRadius, color: 'white', type: 'cue', number: 0};
        this.balls.push(this.cueBall);
        
        const startX = 780, startY = 300, spacing = this.ballRadius * 2 + 0.5;
        
        // EXACT EPA UK 8-BALL RACK:
        // Row 1: R | Row 2: YR | Row 3: R8Y | Row 4: YRYR | Row 5: RYYRY
        const rackPattern = [
            // Row 1 (apex): RED
            {row: 0, col: 0, color: 'red', num: 1},
            
            // Row 2: YELLOW, RED
            {row: 1, col: -0.5, color: 'yellow', num: 9},
            {row: 1, col: 0.5, color: 'red', num: 2},
            
            // Row 3: RED, BLACK, YELLOW
            {row: 2, col: -1, color: 'red', num: 3},
            {row: 2, col: 0, color: 'black', num: 8},
            {row: 2, col: 1, color: 'yellow', num: 10},
            
            // Row 4: YELLOW, RED, YELLOW, RED
            {row: 3, col: -1.5, color: 'yellow', num: 11},
            {row: 3, col: -0.5, color: 'red', num: 4},
            {row: 3, col: 0.5, color: 'yellow', num: 12},
            {row: 3, col: 1.5, color: 'red', num: 5},
            
            // Row 5 (back): RED, YELLOW, YELLOW, RED, YELLOW
            {row: 4, col: -2, color: 'red', num: 6},
            {row: 4, col: -1, color: 'yellow', num: 13},
            {row: 4, col: 0, color: 'yellow', num: 14},
            {row: 4, col: 1, color: 'red', num: 7},
            {row: 4, col: 2, color: 'yellow', num: 15}
        ];
        
        // Place balls in exact EPA pattern
        rackPattern.forEach(ball => {
            this.balls.push(this.createBall(
                startX + ball.row * spacing,
                startY + ball.col * spacing,
                ball.color,
                ball.num
            ));
        });
        
        this.updateUI();
        this.animate();
        
        // DEBUG: Show initialization complete
        document.getElementById('debugInfo').textContent = 'Game Initialized. Click to Start!';
    }
    
    createBall(x, y, color, number) {
        return {x, y, vx: 0, vy: 0, radius: this.ballRadius, color, type: color, number, passedCenterLine: false};
    }
    
    setupEventListeners() {
        this.canvas.addEventListener('mousemove', (e) => this.handleMouseMove(e));
        this.canvas.addEventListener('mousedown', () => this.handleMouseDown());
        this.canvas.addEventListener('mouseup', () => this.handleMouseUp());
        document.getElementById('newGameBtn').addEventListener('click', () => this.initGame());
        document.getElementById('rulesBtn').addEventListener('click', () => document.getElementById('rulesModal').style.display = 'block');
        document.getElementById('ballInHandBtn').addEventListener('click', () => this.enableBallInHand());
        document.getElementById('closeRules').addEventListener('click', () => document.getElementById('rulesModal').style.display = 'none');
        window.addEventListener('click', (e) => {
            if (e.target === document.getElementById('rulesModal')) document.getElementById('rulesModal').style.display = 'none';
        });
    }
    
    handleMouseMove(e) {
        const rect = this.canvas.getBoundingClientRect();
        this.mouseX = e.clientX - rect.left;
        this.mouseY = e.clientY - rect.top;
        
        if (this.ballInHand) {
            // Just update mouse position for ghost ball preview
            return;
        }
        
        if (!this.cueBall || this.cueBall.potted) return;
        
        if (this.isShooting && this.canShoot()) {
            // Aim is LOCKED - only track VERTICAL (Y-axis) mouse movement for power
            const deltaY = this.mouseY - this.dragStartY;
            
            if (deltaY > 0) {
                // Mouse moved DOWN = Pulling back
                this.pullBackDistance = Math.min(deltaY, 150);
                this.shotPower = (this.pullBackDistance / 150) * this.maxPower;
                this.pushForwardDistance = 0;
            } else {
                // Mouse moved UP = Pushing forward
                const pushDist = Math.abs(deltaY);
                this.pushForwardDistance = Math.min(pushDist, this.pullBackDistance + 50);
                
                // Check if cue has made contact with ball
                const cueDistance = 35 + this.pullBackDistance - this.pushForwardDistance;
                if (cueDistance <= 12) { // Contact threshold
                    // CONTACT MADE!
                    const speed = Math.min(this.shotPower, this.maxPower) * 1.2;
                    if (speed > 1) {
                        this.cueBall.vx = Math.cos(this.aimAngle) * speed;
                        this.cueBall.vy = Math.sin(this.aimAngle) * speed;
                        document.getElementById('shotInfo').textContent = `Contact! Power: ${speed.toFixed(1)}`;
                    }
                    
                    // Reset shooting state
                    this.isShooting = false;
                    this.isAiming = false;
                    this.shotPower = 0;
                    this.pullBackDistance = 0;
                    this.pushForwardDistance = 0;
                    document.getElementById('powerBarContainer').style.display = 'none';
                }
            }
            
            // Track time for velocity calculation
            this.lastMouseTime = Date.now();
        } else if (!this.isShooting && this.canShoot()) {
            // Update aim angle when not shooting - but NOT during ball-in-hand
            if (!this.ballInHand) {
                this.isAiming = true;
                this.aimAngle = Math.atan2(this.mouseY - this.cueBall.y, this.mouseX - this.cueBall.x);
            }
        }
    }
    
    handleMouseDown() {
        // Handle ball-in-hand placement FIRST
        if (this.ballInHand) {
            // Check if placement is valid (not on top of another ball)
            const minDist = this.ballRadius * 2 + 2;
            let validPlacement = true;
            
            for (let ball of this.balls) {
                if (ball.potted || ball.number === 0) continue;
                const dx = this.mouseX - ball.x;
                const dy = this.mouseY - ball.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                if (dist < minDist) {
                    validPlacement = false;
                    break;
                }
            }
            
            // Check if placement is within table bounds
            const margin = 30 + this.ballRadius;
            if (this.mouseX < margin || this.mouseX > this.width - margin ||
                this.mouseY < margin || this.mouseY > this.height - margin) {
                validPlacement = false;
            }
            
            if (validPlacement) {
                // Place the cue ball
                this.cueBall.x = this.mouseX;
                this.cueBall.y = this.mouseY;
                this.cueBall.potted = false;
                this.cueBall.vx = 0;
                this.cueBall.vy = 0;
                this.ballInHand = false;
                
                document.getElementById('shotInfo').textContent = 'Cue ball placed! Click and hold to shoot';
                document.getElementById('gameMessage').textContent = 'Ready to shoot';
                document.getElementById('foulIndicator').style.display = 'none';
            } else {
                document.getElementById('shotInfo').textContent = 'Invalid placement! Move away from other balls';
            }
            
            // IMPORTANT: Return to prevent shooting logic
            return;
        }
        
        // Only continue to shooting logic if NOT in ball-in-hand mode
        if (this.canShoot() && !this.isShooting) {
            // LOCK AIM ANGLE
            const dx = this.mouseX - this.cueBall.x;
            const dy = this.mouseY - this.cueBall.y;
            this.aimAngle = Math.atan2(dy, dx);
            
            // Store initial Y position for vertical tracking
            this.dragStartY = this.mouseY;
            
            this.isShooting = true;
            this.dragStartX = this.mouseX;
            this.pullBackDistance = 0;
            this.pushForwardDistance = 0;
            this.shotPower = 0;
            this.dragStartTime = Date.now();
            this.lastMouseTime = Date.now();
            document.getElementById('shotInfo').textContent = 'Aim locked! Move DOWN to pull back, UP to strike!';
            document.getElementById('powerBarContainer').style.display = 'block';
        }
    }
    
    handleMouseUp() {
        // Ignore mouseup during ball-in-hand mode
        if (this.ballInHand) return;
        
        if (this.isShooting) {
            // Mouse released without contact - cancel shot
            this.isShooting = false;
            this.isAiming = false;
            this.shotPower = 0;
            this.pullBackDistance = 0;
            this.pushForwardDistance = 0;
            document.getElementById('powerBarContainer').style.display = 'none';
            document.getElementById('shotInfo').textContent = 'Shot cancelled - push forward to contact!';
            
            setTimeout(() => {
                if (!this.isShooting && this.canShoot()) {
                    document.getElementById('shotInfo').textContent = 'Click and hold to shoot';
                }
            }, 1500);
        }
    }
    
    canShoot() {
        return !this.gameOver && this.balls.every(b => Math.abs(b.vx) < 0.05 && Math.abs(b.vy) < 0.05);
    }
    
    animate() {
        this.ctx.clearRect(0, 0, this.width, this.height);
        this.ctx.fillStyle = '#1a7f37';
        this.ctx.fillRect(0, 0, this.width, this.height);
        this.ctx.strokeStyle = '#8B4513';
        this.ctx.lineWidth = 20;
        this.ctx.strokeRect(10, 10, this.width - 20, this.height - 20);
        
        // Draw pockets with VERY VISIBLE capture zones
        this.pockets.forEach(p => {
            // Large capture zone visualization (shows exactly where balls will pot)
            const captureRadius = this.pocketRadius + 2;
            this.ctx.fillStyle = 'rgba(255, 0, 0, 0.2)';  // Red tint to show capture zone
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, captureRadius, 0, Math.PI * 2);
            this.ctx.fill();
            
            // Outer shadow
            this.ctx.fillStyle = 'rgba(0, 0, 0, 0.6)';
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, this.pocketRadius + 2, 0, Math.PI * 2);
            this.ctx.fill();
            
            // Pocket hole (pure black)
            this.ctx.fillStyle = '#000';
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, this.pocketRadius, 0, Math.PI * 2);
            this.ctx.fill();
            
            // Pocket rim highlight
            this.ctx.strokeStyle = '#333';
            this.ctx.lineWidth = 2;
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, this.pocketRadius - 1, 0, Math.PI * 2);
            this.ctx.stroke();
            
            // DEBUG: Draw capture threshold circle
            this.ctx.strokeStyle = 'rgba(255, 255, 0, 0.5)';  // Yellow outline
            this.ctx.lineWidth = 2;
            this.ctx.setLineDash([4, 4]);
            this.ctx.beginPath();
            this.ctx.arc(p.x, p.y, captureRadius, 0, Math.PI * 2);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
        });
        
        this.updatePhysics();
        this.balls.forEach(b => this.drawBall(b));
        
        if (this.isAiming && !this.isShooting && this.canShoot() && !this.ballInHand) {
            const dist = 400;
            this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.7)';
            this.ctx.lineWidth = 2;
            this.ctx.setLineDash([10, 10]);
            this.ctx.beginPath();
            this.ctx.moveTo(this.cueBall.x, this.cueBall.y);
            this.ctx.lineTo(this.cueBall.x + Math.cos(this.aimAngle) * dist, this.cueBall.y + Math.sin(this.aimAngle) * dist);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
            
            // Draw target ball preview
            const guideX = this.cueBall.x + Math.cos(this.aimAngle) * (this.ballRadius * 2 + 2);
            const guideY = this.cueBall.y + Math.sin(this.aimAngle) * (this.ballRadius * 2 + 2);
            this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.4)';
            this.ctx.lineWidth = 2;
            this.ctx.setLineDash([5, 5]);
            this.ctx.beginPath();
            this.ctx.arc(guideX, guideY, this.ballRadius, 0, Math.PI * 2);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
        }
        
        if (this.isShooting) {
            // Cue stick visualization based on pull-back and push-forward
            const baseDist = 35;
            const pullBack = this.pullBackDistance;
            const pushForward = this.pushForwardDistance;
            
            // Cue position: pulled back by pullBackDistance, then pushed forward by pushForwardDistance
            const cueDistance = baseDist + pullBack - pushForward;
            const cueStartX = this.cueBall.x - Math.cos(this.aimAngle) * cueDistance;
            const cueStartY = this.cueBall.y - Math.sin(this.aimAngle) * cueDistance;
            const cueEndX = this.cueBall.x - Math.cos(this.aimAngle) * (cueDistance + 200);
            const cueEndY = this.cueBall.y - Math.sin(this.aimAngle) * (cueDistance + 200);
            
            // Cue stick gradient (wood texture)
            const grad = this.ctx.createLinearGradient(cueStartX, cueStartY, cueEndX, cueEndY);
            grad.addColorStop(0, '#d4a574');
            grad.addColorStop(0.8, '#8b6f47');
            grad.addColorStop(1, '#5a4a3a');
            
            // Draw cue stick
            this.ctx.strokeStyle = grad;
            this.ctx.lineWidth = 11;
            this.ctx.lineCap = 'round';
            this.ctx.beginPath();
            this.ctx.moveTo(cueStartX, cueStartY);
            this.ctx.lineTo(cueEndX, cueEndY);
            this.ctx.stroke();
            
            // Cue tip with glow when close to contact
            const distanceToContact = cueDistance - 12;
            if (distanceToContact < 10) {
                this.ctx.fillStyle = 'rgba(100, 149, 237, 0.5)';
                this.ctx.beginPath();
                this.ctx.arc(cueStartX, cueStartY, 12, 0, Math.PI * 2);
                this.ctx.fill();
            }
            
            this.ctx.fillStyle = '#6495ED';
            this.ctx.beginPath();
            this.ctx.arc(cueStartX, cueStartY, 7, 0, Math.PI * 2);
            this.ctx.fill();
            
            // Visual guide showing pull-back amount
            if (pullBack > 10) {
                const ghostStartX = this.cueBall.x - Math.cos(this.aimAngle) * baseDist;
                const ghostStartY = this.cueBall.y - Math.sin(this.aimAngle) * baseDist;
                this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.3)';
                this.ctx.lineWidth = 8;
                this.ctx.setLineDash([10, 10]);
                this.ctx.beginPath();
                this.ctx.moveTo(ghostStartX, ghostStartY);
                this.ctx.lineTo(cueStartX, cueStartY);
                this.ctx.stroke();
                this.ctx.setLineDash([]);
            }
            
            // Contact indicator (golden when very close)
            const contactPointX = this.cueBall.x - Math.cos(this.aimAngle) * 12;
            const contactPointY = this.cueBall.y - Math.sin(this.aimAngle) * 12;
            this.ctx.strokeStyle = distanceToContact < 5 ? 'rgba(255, 215, 0, 0.8)' : 'rgba(255, 255, 255, 0.4)';
            this.ctx.lineWidth = 3;
            this.ctx.setLineDash([5, 5]);
            this.ctx.beginPath();
            this.ctx.arc(contactPointX, contactPointY, 8, 0, Math.PI * 2);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
            
            // Power meter update
            const powerPercent = Math.min(this.shotPower / this.maxPower, 1.0);
            document.getElementById('powerFill').style.width = (powerPercent * 100) + '%';
        }
        
        if (this.ballInHand) {
            this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.9)';
            this.ctx.lineWidth = 3;
            this.ctx.setLineDash([5, 5]);
            this.ctx.beginPath();
            this.ctx.arc(this.cueBall.x, this.cueBall.y, this.ballRadius + 10, 0, Math.PI * 2);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
        }
        
        requestAnimationFrame(() => this.animate());
    }
    
    drawBall(ball) {
        const grad = this.ctx.createRadialGradient(ball.x - 4, ball.y - 4, 0, ball.x, ball.y, ball.radius);
        if (ball.color === 'white') { grad.addColorStop(0, '#fff'); grad.addColorStop(1, '#ccc'); }
        else if (ball.color === 'red') { grad.addColorStop(0, '#ff6b6b'); grad.addColorStop(1, '#c92a2a'); }
        else if (ball.color === 'yellow') { grad.addColorStop(0, '#ffd43b'); grad.addColorStop(1, '#fab005'); }
        else if (ball.color === 'black') { grad.addColorStop(0, '#4a4a4a'); grad.addColorStop(1, '#000'); }
        
        this.ctx.fillStyle = grad;
        this.ctx.beginPath();
        this.ctx.arc(ball.x, ball.y, ball.radius, 0, Math.PI * 2);
        this.ctx.fill();
        
        if (ball.number > 0) {
            this.ctx.fillStyle = 'white';
            this.ctx.beginPath();
            this.ctx.arc(ball.x, ball.y, ball.radius * 0.55, 0, Math.PI * 2);
            this.ctx.fill();
            this.ctx.fillStyle = 'black';
            this.ctx.font = 'bold 10px Arial';
            this.ctx.textAlign = 'center';
            this.ctx.textBaseline = 'middle';
            this.ctx.fillText(ball.number, ball.x, ball.y);
        }
    }
    
    updatePhysics() {
        if (!this.canShoot()) {
            let cueBallPotted = false, pottedBalls = [], firstHit = null, cushionHit = false;
            
            this.balls.forEach(ball => {
                ball.vx *= this.friction;
                ball.vy *= this.friction;
                if (Math.abs(ball.vx) < 0.04) ball.vx = 0;  // Stop sooner
                if (Math.abs(ball.vy) < 0.04) ball.vy = 0;
                ball.x += ball.vx;
                ball.y += ball.vy;
                
                if (this.isBreak && !ball.passedCenterLine && ball.type !== 'cue' && ball.y < this.centerLineY) {
                    ball.passedCenterLine = true;
                }
                
                const minX = 30 + ball.radius, maxX = this.width - 30 - ball.radius;
                const minY = 30 + ball.radius, maxY = this.height - 30 - ball.radius;
                
                if (ball.x < minX) { ball.x = minX; ball.vx = -ball.vx * this.cushionRestitution; cushionHit = true; }
                if (ball.x > maxX) { ball.x = maxX; ball.vx = -ball.vx * this.cushionRestitution; cushionHit = true; }
                if (ball.y < minY) { ball.y = minY; ball.vy = -ball.vy * this.cushionRestitution; cushionHit = true; }
                if (ball.y > maxY) { ball.y = maxY; ball.vy = -ball.vy * this.cushionRestitution; cushionHit = true; }
                
                // FIXED: Much more forgiving pocket detection with proper removal
                this.pockets.forEach(p => {
                    const dx = ball.x - p.x;
                    const dy = ball.y - p.y;
                    const dist = Math.sqrt(dx * dx + dy * dy);
                    
                    // Very forgiving pocket detection - if ball center gets within this distance
                    const captureThreshold = this.pocketRadius + 2;  // Just needs to touch pocket edge
                    
                    if (dist <= captureThreshold) {
                        // Mark ball for removal
                        if (ball.type === 'cue') {
                            cueBallPotted = true;
                            console.log('CUE BALL POTTED! Distance:', dist.toFixed(2), 'Threshold:', captureThreshold);
                        } else {
                            // Only add if not already in the list
                            const alreadyPotted = pottedBalls.some(pb => pb === ball);
                            if (!alreadyPotted) {
                                pottedBalls.push(ball);
                                console.log('BALL POTTED:', ball.color, ball.number, 'Distance:', dist.toFixed(2), 'Threshold:', captureThreshold);
                                // Stop ball movement immediately when potted
                                ball.vx = 0;
                                ball.vy = 0;
                            }
                        }
                    }
                });
            });
            
            for (let i = 0; i < this.balls.length; i++) {
                for (let j = i + 1; j < this.balls.length; j++) {
                    const b1 = this.balls[i], b2 = this.balls[j];
                    const dx = b2.x - b1.x, dy = b2.y - b1.y;
                    const distSq = dx * dx + dy * dy;
                    const minDist = b1.radius + b2.radius;
                    
                    if (distSq < minDist * minDist) {
                        if (b1.type === 'cue' && !firstHit) firstHit = b2.type;
                        else if (b2.type === 'cue' && !firstHit) firstHit = b1.type;
                        
                        const dist = Math.sqrt(distSq);
                        const nx = dx / dist, ny = dy / dist;
                        
                        // Enhanced collision response
                        const rvx = b2.vx - b1.vx;
                        const rvy = b2.vy - b1.vy;
                        const rvn = rvx * nx + rvy * ny;
                        
                        if (rvn < 0) {
                            const impulse = 2 * rvn / 2;
                            b1.vx += impulse * nx * 0.98;  // Slight energy retention
                            b1.vy += impulse * ny * 0.98;
                            b2.vx -= impulse * nx * 0.98;
                            b2.vy -= impulse * ny * 0.98;
                        }
                        
                        const overlap = minDist - dist;
                        if (overlap > 0) {
                            b1.x -= nx * overlap * 0.5;
                            b1.y -= ny * overlap * 0.5;
                            b2.x += nx * overlap * 0.5;
                            b2.y += ny * overlap * 0.5;
                        }
                    }
                }
            }
            
            if (this.balls.every(b => b.vx === 0 && b.vy === 0)) {
                this.handleShotComplete(cueBallPotted, pottedBalls, firstHit, cushionHit);
            }
        }
    }
    
    handleShotComplete(cueBallPotted, pottedBalls, firstHit, cushionHit) {
        let foul = false, message = '';
        
        if (this.isBreak) {
            let breakPoints = pottedBalls.length;
            this.balls.forEach(b => { if (b.passedCenterLine && b.type !== 'cue') breakPoints++; });
            
            if (breakPoints < 3) {
                message = 'Illegal Break! Re-rack';
                this.initGame();
                return;
            }
            this.isBreak = false;
        }
        
        pottedBalls.forEach(ball => {
            const idx = this.balls.indexOf(ball);
            if (idx > -1) {
                this.balls.splice(idx, 1);
                if (ball.type === 'black') {
                    this.balls.push(this.createBall(780, 300, 'black', 8));
                    message = 'Black re-spotted';
                } else {
                    if (this.currentPlayer === 1) this.player1Potted.push(ball);
                    else this.player2Potted.push(ball);
                }
            }
        });
        
        if (this.tableOpen && pottedBalls.length > 0 && !foul) {
            const pottedRed = pottedBalls.some(b => b.type === 'red');
            const pottedYellow = pottedBalls.some(b => b.type === 'yellow');
            
            if (pottedRed && !pottedYellow) {
                if (this.currentPlayer === 1) { this.player1Balls = 'red'; this.player2Balls = 'yellow'; }
                else { this.player2Balls = 'red'; this.player1Balls = 'yellow'; }
                this.tableOpen = false;
                message = 'Groups assigned!';
            } else if (pottedYellow && !pottedRed) {
                if (this.currentPlayer === 1) { this.player1Balls = 'yellow'; this.player2Balls = 'red'; }
                else { this.player2Balls = 'yellow'; this.player1Balls = 'red'; }
                this.tableOpen = false;
                message = 'Groups assigned!';
            }
        }
        
        if (cueBallPotted) {
            foul = true;
            message = 'Foul: In-Off!';
            this.cueBall.x = 240;
            this.cueBall.y = 300;
            this.cueBall.vx = 0;
            this.cueBall.vy = 0;
        }
        
        if (!foul && !this.tableOpen && firstHit) {
            const playerBalls = this.currentPlayer === 1 ? this.player1Balls : this.player2Balls;
            const playerPotted = this.currentPlayer === 1 ? this.player1Potted : this.player2Potted;
            const allCleared = playerPotted.filter(b => b.type === playerBalls).length === 7;
            
            if (allCleared && firstHit !== 'black') { foul = true; message = 'Foul: Must hit black!'; }
            else if (!allCleared && firstHit !== playerBalls) { foul = true; message = 'Foul: Wrong ball first!'; }
            
            if (!foul && pottedBalls.length === 0 && !cushionHit) { foul = true; message = 'Foul: No cushion!'; }
        }
        
        const pottedBlack = pottedBalls.some(b => b.type === 'black');
        if (pottedBlack && !this.tableOpen) {
            const playerBalls = this.currentPlayer === 1 ? this.player1Balls : this.player2Balls;
            const playerPotted = this.currentPlayer === 1 ? this.player1Potted : this.player2Potted;
            const allCleared = playerPotted.filter(b => b.type === playerBalls).length === 7;
            
            if (!allCleared || foul) {
                this.gameOver = true;
                this.winner = this.currentPlayer === 1 ? 2 : 1;
                message = 'Loss of Frame! Player ' + this.winner + ' wins!';
            } else {
                this.gameOver = true;
                this.winner = this.currentPlayer;
                message = '?? Player ' + this.winner + ' wins!';
            }
        }
        
        if (!this.gameOver) {
            if (foul) {
                this.ballInHand = true;
                this.currentPlayer = this.currentPlayer === 1 ? 2 : 1;
                document.getElementById('foulIndicator').style.display = 'block';
                document.getElementById('shotInfo').textContent = 'Click to place cue ball';
                setTimeout(() => { document.getElementById('foulIndicator').style.display = 'none'; }, 2000);
            } else if (pottedBalls.filter(b => b.type !== 'black').length === 0) {
                this.currentPlayer = this.currentPlayer === 1 ? 2 : 1;
            }
        }
        
        this.updateUI(message);
        if this.canShoot() && !this.ballInHand) {
            document.getElementById('shotInfo').textContent = 'Click and hold to shoot';
        }
    }
    
    updateUI(message = '') {
        document.getElementById('turnIndicator').textContent = 'Player ' + this.currentPlayer + ""'s Turn"";
        document.getElementById('player1Panel').classList.toggle('active', this.currentPlayer === 1);
        document.getElementById('player2Panel').classList.toggle('active', this.currentPlayer === 2);
        
        if (message) {
            document.getElementById('gameMessage').textContent = message;
        }
        
        const p1Reds = this.player1Potted.filter(b => b.type === 'red').length;
        const p1Yellows = this.player1Potted.filter(b => b.type === 'yellow').length;
        const p2Reds = this.player2Potted.filter(b => b.type === 'red').length;
        const p2Yellows = this.player2Potted.filter(b => b.type === 'yellow').length;
        
        if (this.player1Balls === 'red') {
            document.getElementById('player1Balls').textContent = '?? Reds: ' + p1Reds + '/7';
        } else if (this.player1Balls === 'yellow') {
            document.getElementById('player1Balls').textContent = '?? Yellows: ' + p1Yellows + '/7';
        } else {
            document.getElementById('player1Balls').textContent = '-';
        }
        
        if (this.player2Balls === 'red') {
            document.getElementById('player2Balls').textContent = '?? Reds: ' + p2Reds + '/7';
        } else if (this.player2Balls === 'yellow') {
            document.getElementById('player2Balls').textContent = '?? Yellows: ' + p2Yellows + '/7';
        } else {
            document.getElementById('player2Balls').textContent = '-';
        }
    }
    
    enableBallInHand() {
        if (!this.cueBall) return;
        
        // Stop all balls first
        this.balls.forEach(b => { b.vx = 0; b.vy = 0; });
        
        // Enable ball-in-hand mode
        this.ballInHand = true;
        document.getElementById('shotInfo').textContent = 'Click to place cue ball';
        document.getElementById('gameMessage').textContent = 'Ball in hand - click to place';
    }
}

window.addEventListener('load', function() {
    try {
        document.getElementById('debugInfo').textContent = 'Creating game...';
        const game = new PoolGame();
        document.getElementById('debugInfo').textContent = 'Game loaded! Balls: ' + game.balls.length;
        setTimeout(() => { document.getElementById('debugInfo').style.display = 'none'; }, 3000);
    } catch(e) {
        document.getElementById('debugInfo').textContent = 'ERROR: ' + e.message;
        console.error('Game init error:', e);
    }
});
";
        }
    }
}
