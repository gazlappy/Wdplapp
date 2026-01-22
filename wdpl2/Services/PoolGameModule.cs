namespace Wdpl2.Services;

/// <summary>
/// Main game module that coordinates all pool game systems
/// </summary>
public static class PoolGameModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL GAME MAIN MODULE
// Coordinates physics, rendering, and input
// ============================================

class PoolGame {
    constructor(canvas, statusEl) {
        this.canvas = canvas;
        this.ctx = canvas.getContext('2d');
        this.statusEl = statusEl;
        
        // Canvas dimensions
        this.width = 1000;
        this.height = 500;
        
        
        // Scale: 1000px canvas = 72 inches real table
        this.pixelsPerInch = 1000 / 72;
        
        // Ball sizes in pixels
        this.standardBallRadius = (2.0 / 2) * this.pixelsPerInch;
        this.cueBallRadius = (1.875 / 2) * this.pixelsPerInch;
        
        // Pocket sizes
        this.cornerPocketRadius = 1.675 * this.pixelsPerInch + (3.0 * 0.1 * this.pixelsPerInch);
        this.middlePocketRadius = 1.87 * this.pixelsPerInch + (2.5 * 0.1 * this.pixelsPerInch);
        
        // Pocket openings (visual) - controls the gap in the cushions/rails
        // These are multipliers relative to the cushion margin
        this.cornerPocketOpeningMult = 1.6;    // Corner pocket opening multiplier (1.0-2.5)
        this.sidePocketOpeningMult = 1.3;      // Side pocket opening multiplier (1.0-2.0)
        this.pocketDepth = 1.0;
        
        // Legacy properties (kept for compatibility)
        this.cornerPocketOpening = 32;
        this.middlePocketOpening = 34;
        
        // Cushion margin
        this.cushionMargin = 1.5 * this.pixelsPerInch;
        
        // Game state
        this.balls = [];
        this.cueBall = null;
        this.pockets = [];
        
        // Shooting state
        this.isShooting = false;
        this.isAiming = false;
        this.shotPower = 0;
        this.maxPower = 40;
        this.aimAngle = 0;
        this.pullBackDistance = 0;
        this.pushForwardDistance = 0;
        
        // Shot control mode
        this.shotControlMode = 'drag'; // 'drag', 'click', 'slider', 'tap', 'swipe'
        this.powerMultiplier = 1.0;
        this.aimSensitivity = 1.0;
        this.maxPullDistance = 150;
        this.autoAimAssist = false;
        this.showShotPreview = true;
        
        // Click power mode state
        this.clickPowerCharging = false;
        this.clickPowerStartTime = 0;
        this.clickPowerMaxTime = 2000; // 2 seconds to reach max power
        
        // Mouse tracking
        this.mouseX = 0;
        this.mouseY = 0;
        this.dragStartY = 0;
        
        // Developer settings properties
        this.captureThresholdPercent = 0.3;
        this.showPocketZones = true;
        this.showCushionLines = false;
        this.showVelocities = false;
        this.showFps = false;
        this.pocketZoneOpacity = 0.2;
        this.collisionDamping = 0.98;
        this.friction = 0.987;
        
        // Ball in hand touch foul - if true, touching a ball while placing cue ball is a foul
        this.ballInHandTouchFoul = true;
        this.cushionRestitution = 0.78;
        
        // Trajectory prediction settings
        this.showTrajectoryPrediction = true;  // Show predicted ball paths
        this.trajectoryLength = 200;            // Length of prediction lines
        this.trajectorySegments = 15;           // Number of segments for smooth curves
        this.showCollisionPoints = true;        // Show where balls will collide
        this.showGhostBalls = true;             // Show ghost balls at collision points
        
        // Spin control properties
        this.maxSpin = 1.5;
        this.spinEffect = 2.0; // Set to 2.0 for visible but realistic effects
        this.englishTransfer = 0.5;
        this.spinDecayRate = 0.98; // Realistic decay rate
        this.showSpinArrows = true;
        
        // FPS tracking
        this.fps = 0;
        this.frameCount = 0;
        this.lastFpsUpdate = Date.now();
        
        // Ball return tracking
        this.pottedBalls = [];
        this.redsPotted = 0;
        this.yellowsPotted = 0;
        this.blackPotted = false;
        
        // ========== GAME RULES & TURN MANAGEMENT ==========
        // Players
        this.players = [
            { name: 'Player 1', color: null, ballsPotted: 0, onBlack: false },
            { name: 'Player 2', color: null, ballsPotted: 0, onBlack: false }
        ];
        this.currentPlayerIndex = 0;
        
        // Game state
        this.gamePhase = 'break'; // 'break', 'open', 'playing', 'finished'
        this.isBreakShot = true;
        this.tableOpen = true;
        this.waitingForBallsToStop = false;
        this.gameOver = false;
        this.winner = null;
        
        // Shot tracking for rule enforcement
        this.shotInProgress = false;
        this.firstBallHit = null;
        this.ballsPottedThisShot = [];
        this.cueBallPotted = false;
        this.cushionHitAfterContact = false;
        this.anyBallHitCushion = false;
        this.legalBreak = false;
        
        // Break shot tracking - balls that have crossed center line
        this.ballsCrossedCenter = new Set();
        
        // Foul tracking
        this.foulCommitted = false;
        this.foulReason = '';
        this.ballInHand = false;
        this.ballInHandBaulk = true; // Restricted to behind baulk line (start of frame and scratches)
        this.ballInHandTouchFoulTriggered = false; // Prevents multiple touch fouls
        
        // Baulk line position (1/5 of table from break end)
        this.baulkLineX = this.width * 0.2;
        
        this.init();
    }
    
    // ========== PLAYER & TURN MANAGEMENT ==========
    getCurrentPlayer() {
        return this.players[this.currentPlayerIndex];
    }
    
    
    getOpponent() {
        return this.players[1 - this.currentPlayerIndex];
    }
    
    
    switchTurn() {
        const previousPlayer = this.getCurrentPlayer().name;
        this.currentPlayerIndex = 1 - this.currentPlayerIndex;
        const newPlayer = this.getCurrentPlayer().name;
        
        console.log('========================================');
        console.log('>>> TURN SWITCHED <<<');
        console.log('From:', previousPlayer, 'To:', newPlayer);
        console.log('========================================');
        
        this.updateTurnDisplay();
    }
    
    updateTurnDisplay() {
        const player = this.getCurrentPlayer();
        let turnText = player.name + ' - Turn';
        
        if (player.color) {
            turnText += ' (' + player.color.toUpperCase() + 'S)';
        } else if (this.tableOpen) {
            turnText += ' (Table Open)';
        }
        
        if (player.onBlack) {
            turnText += ' - ON THE BLACK!';
        }
        
        if (this.ballInHand) {
            turnText += ' - BALL IN HAND';
        }
        
        this.statusEl.textContent = turnText;
        this.statusEl.style.background = player.color === 'red' ? 'rgba(220, 38, 38, 0.9)' : 
                                         player.color === 'yellow' ? 'rgba(234, 179, 8, 0.9)' : 
                                         'rgba(59, 130, 246, 0.9)';
    }
    
    // ========== SHOT TRACKING ==========
    startShot() {
        console.log('========================================');
        console.log('>>> SHOT STARTED <<<');
        console.log('Player:', this.getCurrentPlayer().name);
        console.log('========================================');
        
        this.shotInProgress = true;
        this.firstBallHit = null;
        this.ballsPottedThisShot = [];
        this.cueBallPotted = false;
        this.cushionHitAfterContact = false;
        this.anyBallHitCushion = false;
        this.foulCommitted = false;
        this.foulReason = '';
        
        // Reset break tracking
        this.ballsCrossedCenter = new Set();
    }
    
    recordFirstBallHit(ball) {
        if (!this.firstBallHit && ball.num !== 0) {
            this.firstBallHit = ball;
            console.log('First ball hit:', ball.color, ball.num);
        }
    }
    
    
    recordCushionHit() {
        if (this.firstBallHit) {
            this.cushionHitAfterContact = true;
        }
        this.anyBallHitCushion = true;
    }
    
    recordBallPotted(ball) {
        this.ballsPottedThisShot.push(ball);
        
        if (ball.num === 0) {
            this.cueBallPotted = true;
            console.log('?? Cue ball potted (scratch)!');
        } else {
            console.log('?? Ball potted this shot:', ball.color, ball.num);
        }
    }
    
    // ========== RULE ENFORCEMENT ==========
    evaluateShot() {
        const player = this.getCurrentPlayer();
        console.log('========================================');
        console.log('=== EVALUATING SHOT ===');
        console.log('Current Player:', player.name);
        console.log('Phase:', this.gamePhase);
        console.log('First ball hit:', this.firstBallHit ? this.firstBallHit.color + ' ' + this.firstBallHit.num : 'NONE');
        console.log('Balls potted this shot:', this.ballsPottedThisShot.length);
        this.ballsPottedThisShot.forEach(b => console.log('  - ' + b.color + ' ' + b.num));
        console.log('Cue ball potted:', this.cueBallPotted);
        console.log('Cushion hit after contact:', this.cushionHitAfterContact);
        console.log('========================================');
        
        // Check for cue ball potted (scratch) - ball in hand anywhere after the break
        if (this.cueBallPotted) {
            this.commitScratchFoul('Cue ball potted (scratch)', false);
            return;
        }
        
        // Break shot evaluation
        if (this.gamePhase === 'break') {
            this.evaluateBreakShot();
            return;
        }
        
        // Check if no ball was hit
        if (!this.firstBallHit) {
            this.commitFoul('Failed to hit any ball');
            return;
        }
        
        // Check if black was hit first illegally
        if (this.firstBallHit.num === 8) {
            // Can only hit black first if on the black
            if (!player.onBlack) {
                this.commitFoul('Hit black ball first - not allowed until on the black');
                return;
            }
        }
        
        // Check if wrong ball hit first (when colors assigned)
        if (!this.tableOpen && player.color) {
            if (this.firstBallHit.color !== player.color && this.firstBallHit.num !== 8) {
                // Exception: if player is on the black (already checked above)
                this.commitFoul('Hit opponent ball first (' + this.firstBallHit.color + ')');
                return;
            }
        }
        
        // Check for legal shot (pot or cushion)
        const pottedNonCue = this.ballsPottedThisShot.filter(b => b.num !== 0);
        if (pottedNonCue.length === 0 && !this.cushionHitAfterContact) {
            this.commitFoul('No pot and no cushion hit after contact');
            return;
        }
        
        // Process potted balls and determine if turn continues
        this.processPottedBalls();
    }
    
    evaluateBreakShot() {
        const ballsPotted = this.ballsPottedThisShot.filter(b => b.num !== 0);
        
        // Check for cue ball potted on break - scratch with baulk restriction
        if (this.cueBallPotted) {
            this.gamePhase = 'open';
            this.tableOpen = true;
            this.commitScratchFoul('Cue ball potted on break', true);
            return;
        }
        
        // Check if black was potted on break
        const blackPotted = ballsPotted.find(b => b.num === 8);
        if (blackPotted) {
            // Re-rack and re-break (simplification: just reset)
            this.statusEl.textContent = 'Black potted on break! Re-racking...';
            setTimeout(() => this.resetRack(), 2000);
            return;
        }
        
        // Calculate break points (EPA rules):
        // - 1 point per ball potted
        // - 1 point per ball that crossed the center line at ANY point (even if it rolled back)
        // - Need 3 points for a legal break
        
        let breakPoints = 0;
        
        // Points for potted balls
        breakPoints += ballsPotted.length;
        console.log('Break points from potted balls:', ballsPotted.length);
        
        // Points for balls that crossed center line (tracked during the shot)
        // This counts balls that crossed at ANY point, even if they rolled back
        const ballsCrossedCount = this.ballsCrossedCenter.size;
        breakPoints += ballsCrossedCount;
        console.log('Break points from balls that crossed center:', ballsCrossedCount);
        console.log('Balls that crossed:', Array.from(this.ballsCrossedCenter));
        console.log('Total break points:', breakPoints);
        
        // Need at least 3 points for a legal break
        const REQUIRED_BREAK_POINTS = 3;
        
        if (breakPoints >= REQUIRED_BREAK_POINTS) {
            this.legalBreak = true;
            console.log('Legal break -', breakPoints, 'points achieved');
            
            // If balls were potted, assign colors
            if (ballsPotted.length > 0) {
                const firstPotted = ballsPotted[0];
                this.assignColors(firstPotted.color);
                this.gamePhase = 'playing';
                this.tableOpen = false;
                // Player continues (potted a ball on break)
                this.updateTurnDisplay();
                return;
            }
            
            // Legal break but no pot - table open, switch turn
            this.gamePhase = 'open';
            this.tableOpen = true;
            this.switchTurn();
            return;
        }
        
        // Illegal break - not enough points
        this.commitFoul('Illegal break - only ' + breakPoints + ' points (need ' + REQUIRED_BREAK_POINTS + ')');
        this.gamePhase = 'open';
        this.tableOpen = true;
        // Note: switchTurn already called in commitFoul
    }
    
    processPottedBalls() {
        const player = this.getCurrentPlayer();
        const pottedNonCue = this.ballsPottedThisShot.filter(b => b.num !== 0);
        
        console.log('Processing potted balls for:', player.name);
        console.log('Potted non-cue balls:', pottedNonCue.length);
        console.log('First ball hit:', this.firstBallHit ? this.firstBallHit.color : 'none');
        console.log('Table open:', this.tableOpen);
        
        // Check for open table scenarios
        // Rule: On an open table, you must pot the same color as the first ball you hit to claim that color
        if (this.tableOpen && pottedNonCue.length > 0 && this.firstBallHit) {
            // Check if ANY potted ball matches the first ball hit color
            const matchingPottedBall = pottedNonCue.find(b => b.num !== 8 && b.color === this.firstBallHit.color);
            const otherColorPotted = pottedNonCue.find(b => b.num !== 8 && b.color !== this.firstBallHit.color);
            
            if (matchingPottedBall) {
                // Potted the same color as first hit - player claims this color!
                console.log('Open table: Hit ' + this.firstBallHit.color + ', potted ' + this.firstBallHit.color + ' - Player claims this color!');
                this.assignColors(this.firstBallHit.color);
                this.gamePhase = 'playing';
                this.tableOpen = false;
                
                // Count the matching ball(s) for the player
                let matchCount = 0;
                pottedNonCue.forEach(ball => {
                    if (ball.num !== 8 && ball.color === this.firstBallHit.color) {
                        matchCount++;
                    }
                });
                this.getCurrentPlayer().ballsPotted += matchCount;
                
                // If other color was also potted, count for opponent
                if (otherColorPotted) {
                    let otherCount = 0;
                    pottedNonCue.forEach(ball => {
                        if (ball.num !== 8 && ball.color !== this.firstBallHit.color) {
                            otherCount++;
                        }
                    });
                    this.getOpponent().ballsPotted += otherCount;
                }
                
                // Check if player is now on the black
                this.checkIfOnBlack();
                
                // Player continues turn
                console.log('CONTINUE TURN - potted matching color on open table');
                this.updateTurnDisplay();
                return;
                
            } else if (otherColorPotted) {
                // Only potted the OTHER color (not the one hit first)
                // This is a LOSS OF TURN but NOT a foul - table remains open
                console.log('Open table mismatch: Hit ' + this.firstBallHit.color + ', potted ' + otherColorPotted.color);
                console.log('This is a LOSS OF TURN (not a foul)');
                console.log('Table REMAINS OPEN');
                
                // Table stays open - do NOT assign colors
                // Show loss of turn message (not foul)
                this.showLossOfTurnMessage('Hit ' + this.firstBallHit.color + ' first, potted ' + otherColorPotted.color + ' - Table still open');
                this.switchTurn();
                return;
            }
        }
        
        // Table is open with no first ball hit tracking, or other scenarios
        // First pot determines colors
        if (this.tableOpen && pottedNonCue.length > 0) {
            const firstPotted = pottedNonCue.find(b => b.num !== 8);
            if (firstPotted) {
                this.assignColors(firstPotted.color);
                this.gamePhase = 'playing';
                this.tableOpen = false;
            }
        }
        
        let continueTurn = false;
        let pottedOwnBall = false;
        let pottedOpponentBall = false;
        let pottedBlack = false;
        
        pottedNonCue.forEach(ball => {
            if (ball.num === 8) {
                pottedBlack = true;
            } else if (ball.color === player.color) {
                pottedOwnBall = true;
                player.ballsPotted++;
            } else {
                pottedOpponentBall = true;
                this.getOpponent().ballsPotted++;
            }
        });
        
        // Check for win/loss on black
        if (pottedBlack) {
            this.handleBlackPotted(player);
            return;
        }
        
        // Potting opponent's ball when table is NOT open
        // If player hit their OWN ball first, it's just a loss of turn (not a foul)
        // If player hit OPPONENT ball first, it's already handled as a foul in evaluateShot()
        if (pottedOpponentBall && !this.tableOpen) {
            // Check if we hit our own color first
            const hitOwnColorFirst = this.firstBallHit && this.firstBallHit.color === player.color;
            
            if (hitOwnColorFirst) {
                // Hit own ball first, but potted opponent's ball - LOSS OF TURN (not foul)
                console.log('Hit own color (' + player.color + ') first, but potted opponent ball');
                console.log('This is a LOSS OF TURN (not a foul)');
                
                this.checkIfOnBlack();
                this.showLossOfTurnMessage('Hit ' + player.color + ' first, potted opponent ball');
                this.switchTurn();
                return;
            } else {
                // Hit opponent's ball first and potted it - this should have been caught earlier
                // but handle it as a foul just in case
                this.commitFoul('Potted opponent ball');
                return;
            }
        }
        
        // Check if player is now on the black
        this.checkIfOnBlack();
        
        // Continue turn if potted own ball legally
        if (pottedOwnBall && !this.foulCommitted) {
            continueTurn = true;
            console.log('CONTINUE TURN - ' + player.name + ' potted own ball');
            this.updateTurnDisplay();
        }
        
        if (!continueTurn) {
            console.log('SWITCHING TURN - no own ball potted');
            this.switchTurn();
        }
    }
    
    showLossOfTurnMessage(reason) {
        const msg = document.createElement('div');
        msg.innerHTML = `
            <div style=""font-size:28px;font-weight:bold;color:#F59E0B;"">LOSS OF TURN</div>
            <div style=""font-size:16px;margin-top:10px;"">${reason}</div>
            <div style=""font-size:14px;margin-top:10px;color:#94a3b8;"">Not a foul - no ball in hand</div>
        `;
        msg.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);background:rgba(0,0,0,0.95);color:white;padding:30px;border-radius:15px;z-index:10000;text-align:center;box-shadow:0 0 30px rgba(245,158,11,0.5);';
        document.body.appendChild(msg);
        
        setTimeout(() => {
            msg.style.opacity = '0';
            msg.style.transition = 'opacity 0.5s';
            setTimeout(() => msg.remove(), 500);
        }, 2000);
    }
    
    
    assignColors(color) {
        const player = this.getCurrentPlayer();
        const opponent = this.getOpponent();
        
        player.color = color;
        opponent.color = color === 'red' ? 'yellow' : 'red';
        
        console.log('?? Colors assigned:', player.name, '=', player.color, '|', opponent.name, '=', opponent.color);
        
        this.showColorAssignment();
    }
    
    showColorAssignment() {
        const player = this.getCurrentPlayer();
        const opponent = this.getOpponent();
        
        const msg = document.createElement('div');
        msg.innerHTML = `
            <div style=""text-align:center;font-size:20px;font-weight:bold;margin-bottom:10px;"">COLORS ASSIGNED!</div>
            <div style=""display:flex;justify-content:space-around;"">
                <div style=""text-align:center;"">
                    <div style=""font-size:16px;"">${player.name}</div>
                    <div style=""width:40px;height:40px;border-radius:50%;margin:10px auto;background:${player.color === 'red' ? '#DC2626' : '#EAB308'};""></div>
                    <div style=""font-size:14px;text-transform:uppercase;"">${player.color}s</div>
                </div>
                <div style=""text-align:center;"">
                    <div style=""font-size:16px;"">${opponent.name}</div>
                    <div style=""width:40px;height:40px;border-radius:50%;margin:10px auto;background:${opponent.color === 'red' ? '#DC2626' : '#EAB308'};""></div>
                    <div style=""font-size:14px;text-transform:uppercase;"">${opponent.color}s</div>
                </div>
            </div>
        `;
        msg.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);background:rgba(0,0,0,0.95);color:white;padding:30px;border-radius:15px;z-index:10000;box-shadow:0 0 30px rgba(0,0,0,0.8);';
        document.body.appendChild(msg);
        
        setTimeout(() => {
            msg.style.opacity = '0';
            msg.style.transition = 'opacity 0.5s';
            setTimeout(() => msg.remove(), 500);
        }, 2500);
    }
    
    checkIfOnBlack() {
        this.players.forEach(player => {
            if (player.color) {
                const remainingBalls = this.balls.filter(b => 
                    !b.potted && b.color === player.color
                ).length;
                
                player.onBlack = remainingBalls === 0;
                
                if (player.onBlack) {
                    console.log('?? ' + player.name + ' is now ON THE BLACK!');
                }
            }
        });
    }
    
    handleBlackPotted(player) {
        if (!player.onBlack) {
            // Potted black too early - lose!
            this.gameOver = true;
            this.winner = this.getOpponent();
            this.showGameOver(this.winner.name + ' WINS!', player.name + ' potted the black too early!');
        } else if (this.foulCommitted || this.cueBallPotted) {
            // Potted black on a foul - lose!
            this.gameOver = true;
            this.winner = this.getOpponent();
            this.showGameOver(this.winner.name + ' WINS!', player.name + ' fouled while potting the black!');
        } else {
            // Legal black pot - win!
            this.gameOver = true;
            this.winner = player;
            this.showGameOver(player.name + ' WINS!', 'Congratulations!');
        }
    }
    
    showGameOver(title, subtitle) {
        this.gamePhase = 'finished';
        
        const overlay = document.createElement('div');
        overlay.innerHTML = `
            <div style=""text-align:center;"">
                <div style=""font-size:48px;font-weight:bold;margin-bottom:20px;text-shadow:2px 2px 4px rgba(0,0,0,0.5);"">${title}</div>
                <div style=""font-size:24px;margin-bottom:30px;opacity:0.9;"">${subtitle}</div>
                <button onclick=""game.newGame()"" style=""padding:15px 40px;font-size:20px;background:#10B981;color:white;border:none;border-radius:10px;cursor:pointer;font-weight:bold;"">New Game</button>
            </div>
        `;
        overlay.style.cssText = 'position:fixed;top:0;left:0;width:100%;height:100%;background:rgba(0,0,0,0.85);display:flex;align-items:center;justify-content:center;z-index:10000;color:white;';
        overlay.id = 'gameOverOverlay';
        document.body.appendChild(overlay);
    }
    
    commitFoul(reason) {
        this.foulCommitted = true;
        this.foulReason = reason;
        this.ballInHand = true;
        this.ballInHandBaulk = false; // Regular fouls allow placement anywhere
        this.ballInHandTouchFoulTriggered = false; // Reset for new ball in hand
        
        console.log('FOUL:', reason);
        
        
        
        this.showFoulMessage(reason, false);
        this.switchTurn();
    }
    
    commitScratchFoul(reason, restrictToBaulk = false) {
        this.foulCommitted = true;
        this.foulReason = reason;
        
        console.log('SCRATCH FOUL:', reason, restrictToBaulk ? '(baulk restricted)' : '(anywhere)');
        
        // Handle cue ball respawn - only restrict to baulk on break
        this.handleCueBallPotted(restrictToBaulk);
        
        this.showFoulMessage(reason, restrictToBaulk);
        this.switchTurn();
    }
    
    commitBallInHandTouchFoul(touchedBall) {
        // Touched a ball while placing the cue ball - foul!
        this.foulCommitted = true;
        this.foulReason = 'Touched ' + touchedBall.color + ' ball while placing cue ball';
        
        console.log('BALL IN HAND TOUCH FOUL:', this.foulReason);
        
        // Show special foul message
        const msg = document.createElement('div');
        msg.innerHTML = `
            <div style=""font-size:32px;font-weight:bold;color:#EF4444;"">FOUL!</div>
            <div style=""font-size:18px;margin-top:10px;"">Touched ${touchedBall.color} ball while placing cue ball</div>
            <div style=""font-size:16px;margin-top:15px;color:#10B981;"">${this.getOpponent().name} gets ball in hand anywhere</div>
        `;
        msg.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);background:rgba(0,0,0,0.95);color:white;padding:30px;border-radius:15px;z-index:10000;text-align:center;box-shadow:0 0 30px rgba(239,68,68,0.5);';
        document.body.appendChild(msg);
        
        setTimeout(() => {
            msg.style.opacity = '0';
            msg.style.transition = 'opacity 0.5s';
            setTimeout(() => msg.remove(), 500);
        }, 2000);
        
        // Ball in hand passes to opponent - anywhere on the table
        this.ballInHand = true;
        this.ballInHandBaulk = false;
        this.ballInHandTouchFoulTriggered = false; // Reset for next player
        
        // Move cue ball to a neutral position
        this.cueBall.x = this.width / 4;
        this.cueBall.y = this.height / 2;
        this.cueBall.vx = 0;
        this.cueBall.vy = 0;
        
        this.switchTurn();
    }
    
    showFoulMessage(reason, isBaulkRestricted = false) {
        const msg = document.createElement('div');
        const placementText = isBaulkRestricted ? 'ball in hand (behind baulk)' : 'ball in hand anywhere';
        msg.innerHTML = `
            <div style=""font-size:32px;font-weight:bold;color:#EF4444;"">FOUL!</div>
            <div style=""font-size:18px;margin-top:10px;"">${reason}</div>
            <div style=""font-size:16px;margin-top:15px;color:#10B981;"">${this.getCurrentPlayer().name} gets ${placementText}</div>
        `;
        msg.style.cssText = 'position:fixed;top:50%;left:50%;transform:translate(-50%,-50%);background:rgba(0,0,0,0.95);color:white;padding:30px;border-radius:15px;z-index:10000;text-align:center;box-shadow:0 0 30px rgba(239,68,68,0.5);';
        document.body.appendChild(msg);
        
        setTimeout(() => {
            msg.style.opacity = '0';
            msg.style.transition = 'opacity 0.5s';
            setTimeout(() => msg.remove(), 500);
        }, 2000);
    }
    
    handleCueBallPotted(restrictToBaulk = false) {
        // Respawn cue ball for ball in hand
        this.cueBall.potted = false;
        this.cueBall.vx = 0;
        this.cueBall.vy = 0;
        this.ballInHand = true;
        this.ballInHandTouchFoulTriggered = false; // Reset for new ball in hand
        
        // Only restrict to baulk on break - after break, ball in hand anywhere
        this.ballInHandBaulk = restrictToBaulk;
        
        if (restrictToBaulk) {
            // Place in middle of baulk area
            this.cueBall.x = this.baulkLineX / 2;
            this.cueBall.y = this.height / 2;
        } else {
            // Place in center of table for anywhere placement
            this.cueBall.x = this.width / 4;
            this.cueBall.y = this.height / 2;
        }
    }
    
    placeCueBall(x, y) {
        if (!this.ballInHand) return false;
        
        // Check baulk line restriction
        if (this.ballInHandBaulk && x > this.baulkLineX) {
            console.log('Cannot place cue ball here - must be behind baulk line');
            return false;
        }
        
        // Check if position is valid (not overlapping other balls)
        const minDist = this.cueBall.r * 2 + 2;
        for (const ball of this.balls) {
            if (ball === this.cueBall || ball.potted) continue;
            const dx = x - ball.x;
            const dy = y - ball.y;
            const dist = Math.sqrt(dx * dx + dy * dy);
            if (dist < minDist) {
                console.log('Cannot place cue ball here - too close to another ball');
                return false;
            }
        }
        
        // Check bounds
        const margin = this.cushionMargin + this.cueBall.r;
        if (x < margin || x > this.width - margin || y < margin || y > this.height - margin) {
            console.log('Cannot place cue ball outside play area');
            return false;
        }
        
        this.cueBall.x = x;
        this.cueBall.y = y;
        this.ballInHand = false;
        this.ballInHandBaulk = false;
        
        console.log('Cue ball placed at:', x, y);
        this.updateTurnDisplay();
        return true;
    }
    
    newGame() {
        // Remove game over overlay
        const overlay = document.getElementById('gameOverOverlay');
        if (overlay) overlay.remove();
        
        // Reset players
        this.players = [
            { name: 'Player 1', color: null, ballsPotted: 0, onBlack: false },
            { name: 'Player 2', color: null, ballsPotted: 0, onBlack: false }
        ];
        this.currentPlayerIndex = 0;
        
        // Reset game state
        this.gamePhase = 'break';
        this.isBreakShot = true;
        this.tableOpen = true;
        this.gameOver = false;
        this.winner = null;
        
        // Reset rack (this will set ballInHand and ballInHandBaulk)
        this.resetRack();
        this.updateTurnDisplay();
    }
    
    
    init() {
        this.repositionPockets();
        this.resetRack();
        
        // Initialize audio system with visual feedback
        if (typeof PoolAudio !== 'undefined') {
            PoolAudio.init();
            PoolAudio.setEnabled(true);
            PoolAudio.setVolume(0.7); // Slightly louder default
            console.log('Audio system initialized');
            
            // Add audio status indicator
            this.createAudioStatusIndicator();
        } else {
            console.warn('PoolAudio module not loaded');
        }
        
        // Setup input
        PoolInput.setupMouseControls(this.canvas, this, this.statusEl);
        PoolInput.setupTouchControls(this.canvas, this, this.statusEl);
        
        // Setup spin control
        PoolSpinControl.setupSpinControl(this.canvas, this);
        
        // Setup shot control modes
        if (typeof PoolShotControl !== 'undefined') {
            PoolShotControl.setupShotControls(this.canvas, this);
        }
        
        // Setup developer settings (F2 to toggle)
        if (typeof PoolDevSettings !== 'undefined') {
            try {
                PoolDevSettings.init(this);
                console.log('Developer settings initialized - Press F2 to open');
            } catch (e) {
                console.error('Failed to initialize dev settings:', e);
            }
        } else {
            console.warn('PoolDevSettings not available');
        }
        
        // Show initial turn display
        this.updateTurnDisplay();
        
        // Start animation
        this.animate();
    }
    
    createAudioStatusIndicator() {
        const audioBtn = document.createElement('button');
        audioBtn.id = 'audioTestBtn';
        audioBtn.innerHTML = '?? <span>Click to Enable Sound</span>';
        audioBtn.style.cssText = 'position:fixed;top:10px;right:10px;padding:12px 20px;background:rgba(239,68,68,0.9);color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:10000;font-size:14px;transition:all 0.3s;box-shadow:0 4px 12px rgba(0,0,0,0.5);';
        
        const updateStatus = () => {
            if (typeof PoolAudio !== 'undefined') {
                if (PoolAudio.userInteracted && PoolAudio.context.state === 'running') {
                    audioBtn.innerHTML = '?? <span>Sound Enabled</span>';
                    audioBtn.style.background = 'rgba(16, 185, 129, 0.9)';
                    return true;
                } else if (PoolAudio.context.state === 'suspended') {
                    audioBtn.innerHTML = '?? <span>Tap to Enable</span>';
                    audioBtn.style.background = 'rgba(251, 191, 36, 0.9)';
                } else {
                    audioBtn.innerHTML = '?? <span>Click to Enable</span>';
                    audioBtn.style.background = 'rgba(239, 68, 68, 0.9)';
                }
            }
            return false;
        };
        
        audioBtn.addEventListener('click', async () => {
            console.log('?? Audio test button clicked');
            if (typeof PoolAudio !== 'undefined') {
                try {
                    if (PoolAudio.context.state === 'suspended') {
                        await PoolAudio.context.resume();
                        console.log('   Context resumed from button click');
                    }
                    PoolAudio.userInteracted = true;
                    
                    console.log('   Playing test sound...');
                    PoolAudio.play('cueHit', 0.8);
                    
                    setTimeout(() => {
                        if (updateStatus()) {
                            console.log('? Audio fully working!');
                            setTimeout(() => {
                                audioBtn.style.opacity = '0';
                                setTimeout(() => audioBtn.remove(), 300);
                            }, 2000);
                        }
                    }, 100);
                } catch (e) {
                    console.error('? Audio test failed:', e);
                    audioBtn.innerHTML = '? <span>Audio Error</span>';
                }
            }
        });
        
        document.body.appendChild(audioBtn);
        
        const statusInterval = setInterval(() => {
            if (updateStatus()) {
                setTimeout(() => {
                    audioBtn.style.opacity = '0';
                    setTimeout(() => {
                        audioBtn.remove();
                        clearInterval(statusInterval);
                    }, 300);
                }, 1500);
            }
        }, 500);
        
        window.addEventListener('audioUnlocked', () => {
            console.log('?? Audio unlocked event received');
            updateStatus();
        });
        
        updateStatus();
    }
    
    repositionPockets() {
        this.pockets = [
            {x: this.cushionMargin * 0.5, y: this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.width - this.cushionMargin * 0.5, y: this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.cushionMargin * 0.5, y: this.height - this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.width - this.cushionMargin * 0.5, y: this.height - this.cushionMargin * 0.5, r: this.cornerPocketRadius, type: 'corner', taperDist: 3.0},
            {x: this.width / 2, y: this.cushionMargin * 0.3, r: this.middlePocketRadius, type: 'middle', taperDist: 2.5},
            {x: this.width / 2, y: this.height - this.cushionMargin * 0.3, r: this.middlePocketRadius, type: 'middle', taperDist: 2.5}
        ];
    }
    
    resetRack() {
        this.balls = [];
        
        // Clear ball return tray
        this.clearBallReturnTray();
        
        const breakLineX = this.width * 0.25;
        
        this.cueBall = {
            x: breakLineX, 
            y: this.height / 2,
            vx: 0, vy: 0,
            r: this.cueBallRadius,
            color: 'white',
            num: 0,
            rotation: 0,
            rotationAxisX: 0,
            rotationAxisY: 1
        };
        this.balls.push(this.cueBall);
        
        const rackX = this.width * 0.75;
        const rackY = this.height / 2;
        const gap = this.standardBallRadius * 2 + 0.5;
        
        const rackPattern = [
            {x: rackX + gap * 0, y: rackY + 0, color: 'red', num: 1},
            {x: rackX + gap * 1, y: rackY - gap * 0.5, color: 'yellow', num: 9},
            {x: rackX + gap * 1, y: rackY + gap * 0.5, color: 'red', num: 2},
            {x: rackX + gap * 2, y: rackY - gap * 1, color: 'red', num: 3},
            {x: rackX + gap * 2, y: rackY + 0, color: 'black', num: 8},
            {x: rackX + gap * 2, y: rackY + gap * 1, color: 'yellow', num: 10},
            {x: rackX + gap * 3, y: rackY - gap * 1.5, color: 'yellow', num: 11},
            {x: rackX + gap * 3, y: rackY - gap * 0.5, color: 'red', num: 4},
            {x: rackX + gap * 3, y: rackY + gap * 0.5, color: 'yellow', num: 12},
            {x: rackX + gap * 3, y: rackY + gap * 1.5, color: 'red', num: 5},
            {x: rackX + gap * 4, y: rackY - gap * 2, color: 'red', num: 6},
            {x: rackX + gap * 4, y: rackY - gap * 1, color: 'yellow', num: 13},
            {x: rackX + gap * 4, y: rackY + 0, color: 'yellow', num: 14},
            {x: rackX + gap * 4, y: rackY + gap * 1, color: 'red', num: 7},
            {x: rackX + gap * 4, y: rackY + gap * 2, color: 'yellow', num: 15}
        ];
        
        rackPattern.forEach(ball => {
            this.balls.push({
                x: ball.x,
                y: ball.y,
                vx: 0, vy: 0,
                r: this.standardBallRadius,
                color: ball.color,
                num: ball.num,
                rotation: 0,
                rotationAxisX: 0,
                rotationAxisY: 1
            });
        });
        
        // Ball in hand behind baulk line at start of frame
        this.ballInHand = true;
        this.ballInHandBaulk = true;
        this.ballInHandTouchFoulTriggered = false;
        
        this.statusEl.textContent = 'Place cue ball behind baulk line to break';
        this.statusEl.style.background = 'rgba(16, 185, 129, 0.9)';
    }
    
    stopBalls() {
        this.balls.forEach(b => {
            b.vx = 0;
            b.vy = 0;
        });
        this.statusEl.textContent = 'All balls stopped';
        this.statusEl.style.background = 'rgba(251, 191, 36, 0.9)';
    }
    
    updateBallReturnTray(ball) {
        // Don't add white ball (cue ball) to the return tray
        if (ball.num === 0) {
            return;
        }
        
        // Track potted ball
        this.pottedBalls.push({
            num: ball.num,
            color: ball.color,
            time: Date.now()
        });
        
        // Update counts
        if (ball.color === 'red') {
            this.redsPotted++;
        } else if (ball.color === 'yellow') {
            this.yellowsPotted++;
        } else if (ball.num === 8) {
            this.blackPotted = true;
        }
        
        // Get tray element
        const tray = document.getElementById('ballReturnTray');
        if (!tray) return;
        
        // Remove empty message
        const emptyMsg = tray.querySelector('.ball-return-empty');
        if (emptyMsg) {
            emptyMsg.remove();
        }
        
        // Create ball element
        const ballEl = document.createElement('div');
        ballEl.className = `potted-ball ${ball.color}`;
        ballEl.setAttribute('data-ball-num', ball.num);
        
        // Add number for non-white balls
        if (ball.num > 0) {
            const numberEl = document.createElement('div');
            numberEl.className = 'potted-ball-number';
            numberEl.textContent = ball.num;
            ballEl.appendChild(numberEl);
        }
        
        // Add tooltip
        ballEl.title = `Ball ${ball.num} (${ball.color})`;
        
        // Add to tray
        tray.appendChild(ballEl);
        
        // Update stats
        this.updateBallReturnStats();
        
        console.log(`?? Ball ${ball.num} added to return tray. Reds: ${this.redsPotted}, Yellows: ${this.yellowsPotted}, Black: ${this.blackPotted}`);
    }
    
    updateBallReturnStats() {
        const redsEl = document.getElementById('redsPotted');
        const yellowsEl = document.getElementById('yellowsPotted');
        const blackEl = document.getElementById('blackPotted');
        
        if (redsEl) redsEl.textContent = `${this.redsPotted}/7`;
        if (yellowsEl) yellowsEl.textContent = `${this.yellowsPotted}/7`;
        if (blackEl) blackEl.textContent = this.blackPotted ? '1/1' : '0/1';
    }
    
    clearBallReturnTray() {
        // Reset tracking
        this.pottedBalls = [];
        this.redsPotted = 0;
        this.yellowsPotted = 0;
        this.blackPotted = false;
        
        // Clear tray
        const tray = document.getElementById('ballReturnTray');
        if (tray) {
            tray.innerHTML = '<div class=""ball-return-empty"">No balls potted yet</div>';
        }
        
        // Reset stats
        this.updateBallReturnStats();
        
        console.log('?? Ball return tray cleared');
    }
    
    animate() {
        // Draw table
        PoolRendering.drawTable(this.ctx, this.width, this.height, this.cushionMargin);
        PoolRendering.drawPockets(this.ctx, this.pockets, this);
        
        // Physics
        let moving = false;
        let activeBalls = 0;
        const centerX = this.width / 2;
        
        this.balls.forEach(ball => {
            if (ball.potted) return;
            
            activeBalls++;
            
            // Track balls crossing center line during break (for 3-point rule)
            // Once a ball crosses, it counts even if it rolls back
            if (this.gamePhase === 'break' && this.shotInProgress && ball.num !== 0) {
                if (ball.x < centerX && !this.ballsCrossedCenter.has(ball.num)) {
                    this.ballsCrossedCenter.add(ball.num);
                    console.log('Ball', ball.num, 'crossed center line! Total crossed:', this.ballsCrossedCenter.size);
                }
            }
            
            // Store position history for trail effect (if ball has spin)
            if ((ball.spinX && Math.abs(ball.spinX) > 0.05) || (ball.spinY && Math.abs(ball.spinY) > 0.05)) {
                if (!ball.trail) ball.trail = [];
                ball.trail.push({ x: ball.x, y: ball.y });
                if (ball.trail.length > 20) ball.trail.shift(); // Keep last 20 positions
            } else {
                ball.trail = []; // Clear trail when no spin
            }
            
            // Apply physics
            if (PoolPhysics.applyFriction(ball)) {
                moving = true;
            }
            
            // Track cushion hits for rule enforcement
            const cushionHit = PoolPhysics.handleCushionBounce(ball, this.width, this.height, this.cushionMargin);
            if (cushionHit && this.shotInProgress) {
                this.recordCushionHit();
            }
            
            // Check pocket jaw collisions (balls bouncing off angled pocket edges)
            const jawHit = PoolPhysics.handlePocketJawCollision(ball, this.pockets, this);
            if (jawHit) {
                moving = true; // Ball is still moving after jaw bounce
            }
            
            // Check pockets
            for (let i = 0; i < this.pockets.length; i++) {
                const p = this.pockets[i];
                const dx = ball.x - p.x;
                const dy = ball.y - p.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                
                
                const pocketRadius = p.r || 29.5;
                const captureThreshold = ball.r * this.captureThresholdPercent;
                
                if (dist <= pocketRadius - captureThreshold) {
                    if (!ball.potted) {
                        ball.potted = true;
                        ball.vx = 0;
                        ball.vy = 0;
                        
                        // ?? PLAY POCKET SOUND
                        console.log(`?? Ball ${ball.num} potted!`);
                        if (typeof PoolAudio !== 'undefined') {
                            PoolAudio.play('pocket', 1.0);
                        } else {
                            console.warn('?? PoolAudio not available for pocket sound');
                        }
                        
                        // Update ball return tray
                        this.updateBallReturnTray(ball);
                        
                        // Track for rule enforcement
                        this.recordBallPotted(ball);
                        
                        console.log('Ball potted:', ball.color, ball.num);
                    }
                    break;
                }
            }
        });
        
        
        // Handle collisions (with first ball hit tracking)
        const collision = PoolPhysics.processCollisions(this.balls, this);
        if (collision && collision.firstBallHit && this.shotInProgress && !this.firstBallHit) {
            this.recordFirstBallHit(collision.firstBallHit);
        }
        
        // Draw trails first (under balls)
        this.balls.forEach(ball => {
            if (!ball.potted && ball.trail && ball.trail.length > 1) {
                this.ctx.strokeStyle = 'rgba(255, 100, 100, 0.3)';
                this.ctx.lineWidth = 2;
                this.ctx.beginPath();
                this.ctx.moveTo(ball.trail[0].x, ball.trail[0].y);
                for (let i = 1; i < ball.trail.length; i++) {
                    const alpha = (i / ball.trail.length) * 0.3;
                    this.ctx.strokeStyle = `rgba(255, 100, 100, ${alpha})`;
                    this.ctx.lineTo(ball.trail[i].x, ball.trail[i].y);
                }
                this.ctx.stroke();
            }
        });
        
        // Draw balls
        this.balls.forEach(ball => {
            if (!ball.potted) {
                PoolRendering.drawBall(this.ctx, ball);
            }
        });
        
        // Draw aim line
        if (this.isAiming && !moving && this.cueBall && !this.cueBall.potted) {
            PoolRendering.drawAimLine(this.ctx, this.cueBall, this.aimAngle);
            
            // Draw trajectory predictions for object balls
            if (this.showTrajectoryPrediction) {
                PoolRendering.drawTrajectoryPredictions(
                    this.ctx, 
                    this.cueBall, 
                    this.aimAngle, 
                    this.balls,
                    this.width,
                    this.height,
                    this.cushionMargin,
                    this
                );
            }
        }
        
        // Draw cue stick
        if (this.isShooting && this.cueBall && !this.cueBall.potted) {
            PoolRendering.drawCueStick(
                this.ctx,
                this.cueBall,
                this.aimAngle,
                this.pullBackDistance,
                this.pushForwardDistance
            );
            
            PoolRendering.drawPowerMeter(
                this.ctx,
                this.cueBall,
                this.shotPower,
                this.maxPower
            );
        }
        
        // Draw spin control overlay
        PoolSpinControl.drawSpinControl(this.ctx);
        
        // Draw shot control mode feedback
        if (typeof PoolShotControl !== 'undefined') {
            PoolShotControl.drawModeFeedback(this.ctx);
        }
        
        
        // Draw ball-in-hand indicator and baulk line
        if (this.ballInHand && this.cueBall && !this.cueBall.potted) {
            this.ctx.save();
            
            // Draw baulk line and zone if restricted
            if (this.ballInHandBaulk) {
                // Draw baulk line
                this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.6)';
                this.ctx.lineWidth = 2;
                this.ctx.setLineDash([10, 5]);
                this.ctx.beginPath();
                this.ctx.moveTo(this.baulkLineX, this.cushionMargin);
                this.ctx.lineTo(this.baulkLineX, this.height - this.cushionMargin);
                this.ctx.stroke();
                this.ctx.setLineDash([]);
                
                // Shade the valid baulk area
                this.ctx.fillStyle = 'rgba(16, 185, 129, 0.15)';
                this.ctx.fillRect(
                    this.cushionMargin, 
                    this.cushionMargin, 
                    this.baulkLineX - this.cushionMargin, 
                    this.height - this.cushionMargin * 2
                );
                
                // Draw D (semicircle) at baulk line - traditional UK pool
                const dRadius = (this.height - this.cushionMargin * 2) * 0.29;
                const dCenterY = this.height / 2;
                this.ctx.strokeStyle = 'rgba(255, 255, 255, 0.5)';
                this.ctx.lineWidth = 2;
                this.ctx.beginPath();
                this.ctx.arc(this.baulkLineX, dCenterY, dRadius, Math.PI * 0.5, Math.PI * 1.5);
                this.ctx.stroke();
                
                // Label
                this.ctx.fillStyle = 'rgba(255, 255, 255, 0.7)';
                this.ctx.font = 'bold 12px Arial';
                this.ctx.textAlign = 'center';
                this.ctx.fillText('BAULK', this.baulkLineX / 2, this.cushionMargin + 20);
            }
            
            // Draw pulsing circle around cue ball
            const pulseSize = Math.sin(Date.now() / 200) * 5 + 15;
            this.ctx.strokeStyle = '#10B981';
            this.ctx.lineWidth = 3;
            this.ctx.setLineDash([8, 4]);
            this.ctx.beginPath();
            this.ctx.arc(this.cueBall.x, this.cueBall.y, this.cueBall.r + pulseSize, 0, Math.PI * 2);
            this.ctx.stroke();
            this.ctx.setLineDash([]);
            
            // Check if current position is valid
            let isValidPosition = true;
            let invalidReason = '';
            
            // Check baulk restriction
            if (this.ballInHandBaulk && this.cueBall.x > this.baulkLineX) {
                isValidPosition = false;
                invalidReason = 'BEHIND BAULK LINE';
            }
            
            // Check ball overlap and detect touches for foul
            const minDist = this.cueBall.r * 2 + 2;
            let touchingBall = null;
            for (const ball of this.balls) {
                if (ball === this.cueBall || ball.potted) continue;
                const dx = this.cueBall.x - ball.x;
                const dy = this.cueBall.y - ball.y;
                const dist = Math.sqrt(dx * dx + dy * dy);
                
                // Check if balls are actually touching (collision distance)
                const touchDist = this.cueBall.r + ball.r;
                if (dist <= touchDist) {
                    touchingBall = ball;
                }
                
                if (dist < minDist) {
                    isValidPosition = false;
                    invalidReason = 'TOO CLOSE TO BALL';
                    break;
                }
            }
            
            // If cue ball touched another ball while in hand, commit foul (if enabled)
            if (touchingBall && this.ballInHandTouchFoul && !this.ballInHandTouchFoulTriggered) {
                this.ballInHandTouchFoulTriggered = true; // Prevent multiple triggers
                console.log('Ball in hand touched ball:', touchingBall.color, touchingBall.num);
                
                // Commit foul after a short delay to show the touch
                setTimeout(() => {
                    this.commitBallInHandTouchFoul(touchingBall);
                }, 100);
            }
            
            // Check bounds
            const margin = this.cushionMargin + this.cueBall.r;
            if (this.cueBall.x < margin || this.cueBall.x > this.width - margin || 
                this.cueBall.y < margin || this.cueBall.y > this.height - margin) {
                isValidPosition = false;
                invalidReason = 'OUT OF BOUNDS';
            }
            
            // Draw indicator text
            this.ctx.fillStyle = isValidPosition ? '#10B981' : '#EF4444';
            this.ctx.font = 'bold 16px Arial';
            this.ctx.textAlign = 'center';
            this.ctx.fillText(
                isValidPosition ? 'DRAG TO PLACE' : invalidReason, 
                this.cueBall.x, 
                this.cueBall.y - this.cueBall.r - 25
            );
            
            // Draw inner indicator
            if (!isValidPosition) {
                this.ctx.strokeStyle = '#EF4444';
                this.ctx.lineWidth = 2;
                this.ctx.beginPath();
                this.ctx.arc(this.cueBall.x, this.cueBall.y, this.cueBall.r + 5, 0, Math.PI * 2);
                this.ctx.stroke();
            }
            
            this.ctx.restore();
        }
        
        
        // Evaluate shot when balls stop moving after a shot was taken
        if (!moving && this.shotInProgress && !this.gameOver) {
            this.shotInProgress = false;
            this.evaluateShot();
        }
        
        // Update status based on game state
        if (this.gameOver) {
            // Game over - don't update status
        } else if (moving && this.shotInProgress) {
            const player = this.getCurrentPlayer();
            this.statusEl.textContent = 'Balls rolling... (' + player.name + ')';
            this.statusEl.style.background = 'rgba(59, 130, 246, 0.9)';
        } else if (!this.isShooting && !this.ballInHand && !this.shotInProgress) {
            this.updateTurnDisplay();
        }
        
        
        // Continue animation
        requestAnimationFrame(() => this.animate());
    }
}

// Initialize game
let game;
window.addEventListener('load', () => {
    try {
        // Try both canvas IDs for compatibility
        let canvas = document.getElementById('canvas');
        if (!canvas) {
            canvas = document.getElementById('poolTable');
        }
        
        let statusEl = document.getElementById('status');
        if (!statusEl) {
            statusEl = document.getElementById('shotInfo');
        }
        
        if (!canvas) {
            console.error('Canvas element not found (tried: canvas, poolTable)');
            return;
        }
        
        if (!statusEl) {
            // Create a status element if it doesn't exist
            statusEl = document.createElement('div');
            statusEl.id = 'status';
            statusEl.style.cssText = 'position:fixed;bottom:10px;left:50%;transform:translateX(-50%);padding:10px 20px;background:rgba(16,185,129,0.9);color:white;border-radius:8px;font-weight:bold;z-index:100;';
            document.body.appendChild(statusEl);
        }
        
        game = new PoolGame(canvas, statusEl);
        console.log('Pool game initialized successfully');
        
        // Hide debug info after a few seconds
        const debugInfo = document.getElementById('debugInfo');
        if (debugInfo) {
            debugInfo.textContent = 'Game loaded! Press F2 for settings';
            setTimeout(() => { debugInfo.style.display = 'none'; }, 3000);
        }
    } catch (e) {
        console.error('Pool game error:', e);
        const debugInfo = document.getElementById('debugInfo');
        if (debugInfo) {
            debugInfo.textContent = 'ERROR: ' + e.message;
            debugInfo.style.color = '#EF4444';
        }
    }
});
";
    }
}
