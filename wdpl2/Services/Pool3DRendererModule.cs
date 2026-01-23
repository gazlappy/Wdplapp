namespace Wdpl2.Services;

/// <summary>
/// Isometric 3D pool table renderer with playable controls
/// Links to all game settings from the 2D version including all shot control modes
/// </summary>
public static class Pool3DRendererModule
{
    public static string GenerateJavaScript()
    {
        return """
// ============================================
// ISOMETRIC 3D POOL RENDERER v6.0
// Full dev settings + all shot control modes
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
    controlPanel: null,
    angleDisplay: null,
    
    // Shot state (shared across modes)
    mouseX: 0,
    mouseY: 0,
    aimAngle: 0,
    shotPower: 0,
    isAiming: false,
    
    // Drag mode state
    isShooting: false,
    dragStartY: 0,
    pullBackDistance: 0,
    pushForwardDistance: 0,
    
    // Click/Tap mode state
    clickPowerCharging: false,
    clickPowerStartTime: 0,
    clickPowerInterval: null,
    
    // Swipe mode state
    swipeStart: null,
    swipeStartTime: 0,
    
    // Ball in hand dragging state (same as 2D)
    isDraggingCueBall: false,
    
    // Linked game settings (read from game object)
    getSettings: function() {
        if (typeof game === 'undefined') {
            return {
                standardBallRadius: 14,
                cushionMargin: 30,
                maxPower: 40,
                friction: 0.987,
                showPocketZones: false,
                showCushionLines: false,
                showVelocities: false,
                showFps: false,
                pocketZoneOpacity: 0.2,
                showTrajectoryPrediction: true,
                trajectoryLength: 200,
                trajectorySegments: 15,
                showCollisionPoints: true,
                showGhostBalls: true,
                showSpinArrows: true,
                shotControlMode: 'drag',
                powerMultiplier: 1.0,
                clickPowerMaxTime: 2000,
                aimSensitivity: 1.0,
                maxPullDistance: 150,
                collisionDamping: 0.98,
                cushionRestitution: 0.78,
                ballInHandTouchFoul: true
            };
        }
        return {
            standardBallRadius: game.standardBallRadius || 14,
            cushionMargin: game.cushionMargin || 30,
            maxPower: game.maxPower || 40,
            friction: game.friction || 0.987,
            showPocketZones: game.showPocketZones || false,
            showCushionLines: game.showCushionLines || false,
            showVelocities: game.showVelocities || false,
            showFps: game.showFps || false,
            pocketZoneOpacity: game.pocketZoneOpacity || 0.2,
            showTrajectoryPrediction: game.showTrajectoryPrediction !== false,
            trajectoryLength: game.trajectoryLength || 200,
            trajectorySegments: game.trajectorySegments || 15,
            showCollisionPoints: game.showCollisionPoints !== false,
            showGhostBalls: game.showGhostBalls !== false,
            showSpinArrows: game.showSpinArrows !== false,
            shotControlMode: game.shotControlMode || 'drag',
            powerMultiplier: game.powerMultiplier || 1.0,
            clickPowerMaxTime: game.clickPowerMaxTime || 2000,
            aimSensitivity: game.aimSensitivity || 1.0,
            maxPullDistance: game.maxPullDistance || 150,
            collisionDamping: game.collisionDamping || 0.98,
            cushionRestitution: game.cushionRestitution || 0.78,
            ballInHandTouchFoul: game.ballInHandTouchFoul !== false
        };
    },
    
    getShotMode: function() {
        return this.getSettings().shotControlMode;
    },
    
    init: async function(game) {
        if (this.initialized) return true;
        
        console.log('[3D] v5.0 Initializing with full dev settings...');
        
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
        console.log('[3D] Ready - all dev settings linked!');
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
        
        function getEndPos(e) {
            var rect = canvas.getBoundingClientRect();
            var scaleX = canvas.width / rect.width;
            var scaleY = canvas.height / rect.height;
            var clientX = e.changedTouches ? e.changedTouches[0].clientX : e.clientX;
            var clientY = e.changedTouches ? e.changedTouches[0].clientY : e.clientY;
            return {
                x: (clientX - rect.left) * scaleX,
                y: (clientY - rect.top) * scaleY
            };
        }
        
        function screenToGame(sx, sy) {
            var w = canvas.width;
            var h = canvas.height;
            var gameW = self.gameWidth;
            var gameH = self.gameHeight;
            var scale = Math.min(w / gameW, h / gameH) * 0.95;
            var offsetX = (w - gameW * scale) / 2;
            var offsetY = (h - gameH * scale) / 2;
            var ISO = self.cameraAngle;
            var gx = (sx - offsetX) / scale;
            var gy = (sy - offsetY - (gameH * scale * (1 - ISO) / 2)) / (scale * ISO);
            return { x: gx, y: gy };
        }
        
        function ballsAreMoving() {
            if (typeof game === 'undefined') return false;
            return game.balls.some(function(b) { 
                return !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01); 
            });
        }
        
        function fireShot(power, angle) {
            if (typeof game === 'undefined' || !game.cueBall || game.cueBall.potted) return;
            if (power < 0.5) return;
            if (game.ballInHand) return; // Don't shoot during ball-in-hand
            
            var settings = self.getSettings();
            var finalPower = power * (settings.powerMultiplier || 1.0);
            
            // Start shot tracking for rules (same as 2D)
            if (typeof game.startShot === 'function') {
                game.startShot();
            }
            
            game.cueBall.vx = Math.cos(angle) * finalPower;
            game.cueBall.vy = Math.sin(angle) * finalPower;
            
            // Apply spin if available (same as 2D)
            if (typeof PoolSpinControl !== 'undefined') {
                PoolSpinControl.applySpinToBall(game.cueBall, angle);
            }
            
            // Play sound (same as 2D)
            if (typeof PoolAudio !== 'undefined') {
                PoolAudio.play('cueHit', Math.min(finalPower / settings.maxPower, 1));
            }
            
            console.log('[3D] Shot! Mode:', self.getShotMode(), 'Power:', finalPower.toFixed(1));
        }
        
        // ==========================================
        // MOUSE MOVE - Update aim angle (all modes)
        // ==========================================
        canvas.addEventListener('mousemove', function(e) {
            if (!self.enabled) return;
            var pos = getPos(e);
            self.mouseX = pos.x;
            self.mouseY = pos.y;
            
            if (typeof game === 'undefined' || !game.cueBall || game.cueBall.potted) return;
            
            var gamePos = screenToGame(pos.x, pos.y);
            var mode = self.getShotMode();
            
            // Handle ball-in-hand dragging (same as 2D)
            if (game.ballInHand && self.isDraggingCueBall) {
                game.cueBall.x = gamePos.x;
                game.cueBall.y = gamePos.y;
                return;
            }
            
            // Skip shooting controls if ball-in-hand is active
            if (game.ballInHand) return;
            
            // DRAG MODE - pull back / push forward
            if (mode === 'drag' && self.isShooting) {
                var deltaY = pos.y - self.dragStartY;
                var settings = self.getSettings();
                
                if (deltaY > 0) {
                    self.pullBackDistance = Math.min(deltaY, settings.maxPullDistance);
                    self.shotPower = (self.pullBackDistance / settings.maxPullDistance) * settings.maxPower;
                    self.pushForwardDistance = 0;
                } else {
                    var pushDist = Math.abs(deltaY);
                    self.pushForwardDistance = Math.min(pushDist, self.pullBackDistance + 50);
                    
                    var cueDistance = 35 + self.pullBackDistance - self.pushForwardDistance;
                    if (cueDistance <= 12) {
                        fireShot(self.shotPower * 2.5, self.aimAngle);
                        self.isShooting = false;
                        self.shotPower = 0;
                        self.pullBackDistance = 0;
                        self.pushForwardDistance = 0;
                    }
                }
            } else if (!self.isShooting && !self.clickPowerCharging) {
                // Update aim angle when not in shooting mode
                var dx = gamePos.x - game.cueBall.x;
                var dy = gamePos.y - game.cueBall.y;
                self.aimAngle = Math.atan2(dy, dx);
                self.isAiming = true;
            }
        });
        
        // ==========================================
        // MOUSE DOWN - Start shot based on mode
        // ==========================================
        canvas.addEventListener('mousedown', function(e) {
            if (!self.enabled) return;
            e.preventDefault();
            
            if (typeof game === 'undefined' || !game.cueBall || game.cueBall.potted) return;
            
            var pos = getPos(e);
            var gamePos = screenToGame(pos.x, pos.y);
            
            // Handle ball-in-hand - start dragging (same as 2D)
            if (game.ballInHand) {
                self.isDraggingCueBall = true;
                game.cueBall.x = gamePos.x;
                game.cueBall.y = gamePos.y;
                return;
            }
            
            if (ballsAreMoving()) return;
            
            var mode = self.getShotMode();
            var settings = self.getSettings();
            
            // Lock aim angle
            var dx = gamePos.x - game.cueBall.x;
            var dy = gamePos.y - game.cueBall.y;
            self.aimAngle = Math.atan2(dy, dx);
            
            if (mode === 'drag') {
                // DRAG MODE - start pull back
                self.dragStartY = pos.y;
                self.isShooting = true;
                self.pullBackDistance = 0;
                self.pushForwardDistance = 0;
                self.shotPower = 0;
            } else if (mode === 'click' || mode === 'tap') {
                // CLICK/TAP MODE - start charging
                self.clickPowerCharging = true;
                self.clickPowerStartTime = Date.now();
                self.shotPower = 0;
                
                self.clickPowerInterval = setInterval(function() {
                    if (!self.clickPowerCharging) {
                        clearInterval(self.clickPowerInterval);
                        return;
                    }
                    var elapsed = Date.now() - self.clickPowerStartTime;
                    var maxTime = mode === 'tap' ? 1500 : settings.clickPowerMaxTime;
                    var powerPercent = Math.min(elapsed / maxTime, 1);
                    self.shotPower = powerPercent * settings.maxPower;
                }, 16);
            } else if (mode === 'swipe') {
                // SWIPE MODE - record start position
                self.swipeStart = { x: pos.x, y: pos.y };
                self.swipeStartTime = Date.now();
            }
        });
        
        // ==========================================
        // MOUSE UP - Fire shot based on mode
        // ==========================================
        canvas.addEventListener('mouseup', function(e) {
            if (!self.enabled) return;
            
            // Handle ball-in-hand placement on release (same as 2D)
            if (self.isDraggingCueBall && game.ballInHand) {
                self.isDraggingCueBall = false;
                
                var pos = getEndPos(e);
                var gamePos = screenToGame(pos.x, pos.y);
                
                if (typeof game.placeCueBall === 'function') {
                    var placed = game.placeCueBall(gamePos.x, gamePos.y);
                    if (!placed) {
                        console.log('[3D] Invalid cue ball position');
                    }
                }
                return;
            }
            
            // Skip if ball-in-hand (don't shoot)
            if (typeof game !== 'undefined' && game.ballInHand) return;
            
            var mode = self.getShotMode();
            var settings = self.getSettings();
            
            if (mode === 'drag') {
                // DRAG MODE - cancel if not contact
                self.isShooting = false;
                self.shotPower = 0;
                self.pullBackDistance = 0;
                self.pushForwardDistance = 0;
            } else if (mode === 'click' || mode === 'tap') {
                // CLICK/TAP MODE - fire on release
                clearInterval(self.clickPowerInterval);
                if (self.clickPowerCharging && self.shotPower > 0) {
                    fireShot(self.shotPower, self.aimAngle);
                }
                self.clickPowerCharging = false;
                self.shotPower = 0;
            } else if (mode === 'swipe' && self.swipeStart) {
                // SWIPE MODE - calculate speed and fire
                var pos = getEndPos(e);
                var swipeTime = Date.now() - self.swipeStartTime;
                
                var dx = pos.x - self.swipeStart.x;
                var dy = pos.y - self.swipeStart.y;
                var distance = Math.sqrt(dx * dx + dy * dy);
                var speed = distance / Math.max(swipeTime, 1);
                
                var power = Math.min(speed * 20, settings.maxPower);
                var angle = Math.atan2(dy, dx);
                
                if (power > 2) {
                    fireShot(power, angle);
                }
                self.swipeStart = null;
            } else if (mode === 'slider') {
                // SLIDER MODE - just update aim
                self.isAiming = true;
            }
        });
        
        canvas.addEventListener('mouseleave', function(e) {
            if (!self.enabled) return;
            self.isShooting = false;
            self.clickPowerCharging = false;
            clearInterval(self.clickPowerInterval);
            self.shotPower = 0;
            self.pullBackDistance = 0;
            self.pushForwardDistance = 0;
            self.swipeStart = null;
            self.isDraggingCueBall = false;
        });
        
        // ==========================================
        // TOUCH EVENTS (mirror mouse events)
        // ==========================================
        canvas.addEventListener('touchstart', function(e) {
            if (!self.enabled) return;
            e.preventDefault();
            
            if (typeof game === 'undefined' || !game.cueBall || game.cueBall.potted) return;
            
            var pos = getPos(e);
            var gamePos = screenToGame(pos.x, pos.y);
            
            // Handle ball-in-hand - start dragging (same as 2D)
            if (game.ballInHand) {
                self.isDraggingCueBall = true;
                game.cueBall.x = gamePos.x;
                game.cueBall.y = gamePos.y;
                return;
            }
            
            if (ballsAreMoving()) return;
            
            var mode = self.getShotMode();
            var settings = self.getSettings();
            
            var dx = gamePos.x - game.cueBall.x;
            var dy = gamePos.y - game.cueBall.y;
            self.aimAngle = Math.atan2(dy, dx);
            
            if (mode === 'drag') {
                self.dragStartY = pos.y;
                self.isShooting = true;
                self.pullBackDistance = 0;
                self.pushForwardDistance = 0;
                self.shotPower = 0;
            } else if (mode === 'click' || mode === 'tap') {
                self.clickPowerCharging = true;
                self.clickPowerStartTime = Date.now();
                self.shotPower = 0;
                self.clickPowerInterval = setInterval(function() {
                    if (!self.clickPowerCharging) {
                        clearInterval(self.clickPowerInterval);
                        return;
                    }
                    var elapsed = Date.now() - self.clickPowerStartTime;
                    var maxTime = mode === 'tap' ? 1500 : settings.clickPowerMaxTime;
                    self.shotPower = Math.min(elapsed / maxTime, 1) * settings.maxPower;
                }, 16);
            } else if (mode === 'swipe') {
                self.swipeStart = { x: pos.x, y: pos.y };
                self.swipeStartTime = Date.now();
            }
        }, { passive: false });
        
        canvas.addEventListener('touchmove', function(e) {
            if (!self.enabled) return;
            e.preventDefault();
            
            var pos = getPos(e);
            var gamePos = screenToGame(pos.x, pos.y);
            
            // Handle ball-in-hand dragging (same as 2D)
            if (game.ballInHand && self.isDraggingCueBall) {
                game.cueBall.x = gamePos.x;
                game.cueBall.y = gamePos.y;
                return;
            }
            
            var mode = self.getShotMode();
            var settings = self.getSettings();
            
            if (mode === 'drag' && self.isShooting) {
                var deltaY = pos.y - self.dragStartY;
                
                if (deltaY > 0) {
                    self.pullBackDistance = Math.min(deltaY, settings.maxPullDistance);
                    self.shotPower = (self.pullBackDistance / settings.maxPullDistance) * settings.maxPower;
                    self.pushForwardDistance = 0;
                } else {
                    var pushDist = Math.abs(deltaY);
                    self.pushForwardDistance = Math.min(pushDist, self.pullBackDistance + 50);
                    
                    var cueDistance = 35 + self.pullBackDistance - self.pushForwardDistance;
                    if (cueDistance <= 12) {
                        fireShot(self.shotPower * 2.5, self.aimAngle);
                        self.isShooting = false;
                        self.shotPower = 0;
                        self.pullBackDistance = 0;
                        self.pushForwardDistance = 0;
                    }
                }
            }
        }, { passive: false });
        
        canvas.addEventListener('touchend', function(e) {
            if (!self.enabled) return;
            
            // Handle ball-in-hand placement on release (same as 2D)
            if (self.isDraggingCueBall && game.ballInHand) {
                self.isDraggingCueBall = false;
                
                var pos = getEndPos(e);
                var gamePos = screenToGame(pos.x, pos.y);
                
                if (typeof game.placeCueBall === 'function') {
                    var placed = game.placeCueBall(gamePos.x, gamePos.y);
                    if (!placed) {
                        console.log('[3D] Invalid cue ball position');
                    }
                }
                return;
            }
            
            var mode = self.getShotMode();
            var settings = self.getSettings();
            
            if (mode === 'drag') {
                self.isShooting = false;
                self.shotPower = 0;
                self.pullBackDistance = 0;
                self.pushForwardDistance = 0;
            } else if (mode === 'click' || mode === 'tap') {
                clearInterval(self.clickPowerInterval);
                if (self.clickPowerCharging && self.shotPower > 0) {
                    fireShot(self.shotPower, self.aimAngle);
                }
                self.clickPowerCharging = false;
                self.shotPower = 0;
            } else if (mode === 'swipe' && self.swipeStart) {
                var pos = getEndPos(e);
                var swipeTime = Date.now() - self.swipeStartTime;
                var dx = pos.x - self.swipeStart.x;
                var dy = pos.y - self.swipeStart.y;
                var distance = Math.sqrt(dx * dx + dy * dy);
                var speed = distance / Math.max(swipeTime, 1);
                var power = Math.min(speed * 20, settings.maxPower);
                var angle = Math.atan2(dy, dx);
                if (power > 2) {
                    fireShot(power, angle);
                }
                self.swipeStart = null;
            }
        }, { passive: false });
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
        
        // Get linked settings from game
        var settings = this.getSettings();
        var cushion = settings.cushionMargin;
        var ballR = settings.standardBallRadius;
        
        var self = this;
        function toScreen(gx, gy, height) {
            height = height || 0;
            var x = offsetX + gx * scale;
            var y = offsetY + gy * scale * ISO + (gameH * scale * (1 - ISO) / 2) - height * 0.5;
            return { x: x, y: y };
        }
        
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
        
        // Cushions (use settings)
        this.drawRect(ctx, toScreen, cushion, 0, gameW - cushion * 2, cushion, 12, '#1a7030');
        this.drawRect(ctx, toScreen, cushion, gameH - cushion, gameW - cushion * 2, cushion, 12, '#1a7030');
        this.drawRect(ctx, toScreen, 0, cushion, cushion, gameH - cushion * 2, 12, '#1a7030');
        this.drawRect(ctx, toScreen, gameW - cushion, cushion, cushion, gameH - cushion * 2, 12, '#1a7030');
        
        // Show cushion lines if enabled
        if (settings.showCushionLines) {
            ctx.strokeStyle = 'rgba(255,255,0,0.5)';
            ctx.lineWidth = 2;
            var cl1 = toScreen(cushion, cushion, 10);
            var cl2 = toScreen(gameW - cushion, cushion, 10);
            var cl3 = toScreen(gameW - cushion, gameH - cushion, 10);
            var cl4 = toScreen(cushion, gameH - cushion, 10);
            ctx.beginPath();
            ctx.moveTo(cl1.x, cl1.y);
            ctx.lineTo(cl2.x, cl2.y);
            ctx.lineTo(cl3.x, cl3.y);
            ctx.lineTo(cl4.x, cl4.y);
            ctx.closePath();
            ctx.stroke();
        }
        
        // Pockets (use game pockets if available)
        var pockets = [];
        if (typeof game !== 'undefined' && game.pockets) {
            for (var i = 0; i < game.pockets.length; i++) {
                pockets.push({ x: game.pockets[i].x, y: game.pockets[i].y, r: game.pockets[i].r });
            }
        } else {
            var pr = ballR * 1.8;
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
            var pocketR = (p.r || ballR * 1.8) * scale;
            
            // Show pocket zones if enabled
            if (settings.showPocketZones) {
                ctx.fillStyle = 'rgba(255,0,0,' + settings.pocketZoneOpacity + ')';
                ctx.beginPath();
                ctx.ellipse(pp.x, pp.y, pocketR * 1.5, pocketR * 1.5 * ISO, 0, 0, Math.PI * 2);
                ctx.fill();
            }
            
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
            
            // Show velocities if enabled
            if (settings.showVelocities && (ball.vx || ball.vy)) {
                var vx = ball.vx || 0;
                var vy = ball.vy || 0;
                var speed = Math.sqrt(vx * vx + vy * vy);
                if (speed > 0.1) {
                    ctx.strokeStyle = '#00ff00';
                    ctx.lineWidth = 2;
                    ctx.beginPath();
                    ctx.moveTo(bp.x, bp.y);
                    ctx.lineTo(bp.x + vx * 5, bp.y + vy * 5 * ISO);
                    ctx.stroke();
                }
            }
            
            // Show spin arrows if enabled (for cue ball)
            if (settings.showSpinArrows && ball.color === 'white' && typeof game !== 'undefined') {
                var spinX = game.spinX || 0;
                var spinY = game.spinY || 0;
                if (Math.abs(spinX) > 0.01 || Math.abs(spinY) > 0.01) {
                    ctx.strokeStyle = '#ff00ff';
                    ctx.lineWidth = 2;
                    ctx.beginPath();
                    ctx.moveTo(bp.x, bp.y);
                    ctx.lineTo(bp.x + spinX * 20, bp.y + spinY * 20 * ISO);
                ctx.stroke();
                    // Arrow head
                    ctx.fillStyle = '#ff00ff';
                    ctx.beginPath();
                ctx.arc(bp.x + spinX * 20, bp.y + spinY * 20 * ISO, 3, 0, Math.PI * 2);
                    ctx.fill();
                }
            }
        }
        
        // Draw cue stick and aiming (same as 2D pull-back system)
        if (typeof game !== 'undefined' && game.cueBall && !game.cueBall.potted) {
            var cueBallScreen = toScreen(game.cueBall.x, game.cueBall.y, ballR * scale * 2);
            var angle = this.aimAngle;
            
            // Check if balls are moving
            var ballsMoving = game.balls.some(function(b) { 
                return !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01); 
            });
            
            // Ball-in-hand indicator (same as 2D)
            if (game.ballInHand && !ballsMoving) {
                // Pulsing highlight around cue ball
                var pulse = Math.sin(Date.now() / 200) * 0.3 + 0.7;
                ctx.strokeStyle = 'rgba(16, 185, 129, ' + pulse + ')';
                ctx.lineWidth = 4;
                ctx.beginPath();
                ctx.arc(cueBallScreen.x, cueBallScreen.y, ballR * scale + 8, 0, Math.PI * 2);
                ctx.stroke();
                
                // Drag indicator arrows
                ctx.fillStyle = 'rgba(16, 185, 129, 0.8)';
                var arrowDist = ballR * scale + 20;
                for (var i = 0; i < 4; i++) {
                    var arrowAngle = (i * Math.PI / 2);
                    var ax = cueBallScreen.x + Math.cos(arrowAngle) * arrowDist;
                    var ay = cueBallScreen.y + Math.sin(arrowAngle) * arrowDist;
                    ctx.beginPath();
                    ctx.moveTo(ax + Math.cos(arrowAngle) * 8, ay + Math.sin(arrowAngle) * 8);
                    ctx.lineTo(ax + Math.cos(arrowAngle - 2.5) * 6, ay + Math.sin(arrowAngle - 2.5) * 6);
                    ctx.lineTo(ax + Math.cos(arrowAngle + 2.5) * 6, ay + Math.sin(arrowAngle + 2.5) * 6);
                    ctx.closePath();
                    ctx.fill();
                }
                
                // Ball-in-hand message
                ctx.fillStyle = 'rgba(0,0,0,0.8)';
                ctx.fillRect(w/2 - 100, h - 50, 200, 30);
                ctx.fillStyle = '#10b981';
                ctx.font = 'bold 14px Arial';
                ctx.textAlign = 'center';
                var bihText = game.ballInHandBaulk ? 'BALL IN HAND (Behind Baulk)' : 'BALL IN HAND (Anywhere)';
                ctx.fillText(bihText, w/2, h - 30);
            }
            
            if (!ballsMoving && !game.ballInHand) {
                // Calculate cue position based on pull-back/push-forward
                var cueDistance = 35 + this.pullBackDistance - this.pushForwardDistance;
                var cueLength = 200;
                
                // Cue tip position (distance from ball)
                var tipX = cueBallScreen.x - Math.cos(angle) * (ballR * scale + cueDistance * 0.5);
                var tipY = cueBallScreen.y - Math.sin(angle) * (ballR * scale + cueDistance * 0.5);
                
                // Cue end position
                var endX = tipX - Math.cos(angle) * cueLength;
                var endY = tipY - Math.sin(angle) * cueLength;
                
                // Cue stick body (wood)
                ctx.strokeStyle = '#8B4513';
                ctx.lineWidth = 6;
                ctx.lineCap = 'round';
                ctx.beginPath();
                ctx.moveTo(tipX, tipY);
                ctx.lineTo(endX, endY);
                ctx.stroke();
                
                // Cue wrap (grip area)
                var wrapStartX = tipX - Math.cos(angle) * 100;
                var wrapStartY = tipY - Math.sin(angle) * 100;
                var wrapEndX = tipX - Math.cos(angle) * 150;
                var wrapEndY = tipY - Math.sin(angle) * 150;
                ctx.strokeStyle = '#2a1a0a';
                ctx.lineWidth = 7;
                ctx.beginPath();
                ctx.moveTo(wrapStartX, wrapStartY);
                ctx.lineTo(wrapEndX, wrapEndY);
                ctx.stroke();
                
                // Cue tip (ivory/leather)
                ctx.strokeStyle = '#d4a574';
                ctx.lineWidth = 8;
                ctx.beginPath();
                ctx.moveTo(tipX, tipY);
                ctx.lineTo(tipX + Math.cos(angle) * 8, tipY + Math.sin(angle) * 8);
                ctx.stroke();
                
                // Advanced trajectory prediction with collision detection
                if (settings.showTrajectoryPrediction) {
                    var hitResult = this.findFirstBallHit(game.cueBall, angle, this.balls);
                    
                    if (hitResult) {
                        // We have a collision - draw line to collision point
                        var collisionScreen = toScreen(hitResult.collisionPoint.cueBallX, hitResult.collisionPoint.cueBallY, ballR * scale * 2);
                        
                        // Aim line to collision
                        ctx.strokeStyle = 'rgba(255,255,255,0.7)';
                        ctx.lineWidth = 2;
                        ctx.setLineDash([8, 8]);
                        ctx.beginPath();
                        ctx.moveTo(cueBallScreen.x, cueBallScreen.y);
                        ctx.lineTo(collisionScreen.x, collisionScreen.y);
                        ctx.stroke();
                        ctx.setLineDash([]);
                        
                        // Ghost cue ball at collision point
                        if (settings.showGhostBalls) {
                            ctx.strokeStyle = 'rgba(255,255,255,0.5)';
                            ctx.lineWidth = 2;
                            ctx.setLineDash([5, 5]);
                            ctx.beginPath();
                            ctx.arc(collisionScreen.x, collisionScreen.y, ballR * scale, 0, Math.PI * 2);
                            ctx.stroke();
                            ctx.setLineDash([]);
                            
                            // Ghost object ball (highlighted)
                            var objectBallScreen = toScreen(hitResult.ball.x, hitResult.ball.y, ballR * scale * 2);
                            var objCol = this.getBallColor(hitResult.ball.color);
                            ctx.strokeStyle = objCol.main;
                            ctx.lineWidth = 3;
                            ctx.setLineDash([5, 5]);
                            ctx.beginPath();
                            ctx.arc(objectBallScreen.x, objectBallScreen.y, ballR * scale + 4, 0, Math.PI * 2);
                            ctx.stroke();
                            ctx.setLineDash([]);
                        }
                        
                        // Collision point indicator (pulsing)
                        var pulseSize = 4 + Math.sin(Date.now() / 200) * 2;
                        var collisionPointScreen = toScreen(hitResult.collisionPoint.x, hitResult.collisionPoint.y, ballR * scale);
                        
                        // Glow
                        var glowGrad = ctx.createRadialGradient(
                            collisionPointScreen.x, collisionPointScreen.y, 0,
                            collisionPointScreen.x, collisionPointScreen.y, 20
                        );
                        glowGrad.addColorStop(0, 'rgba(255, 215, 0, 0.6)');
                        glowGrad.addColorStop(0.5, 'rgba(255, 215, 0, 0.3)');
                        glowGrad.addColorStop(1, 'rgba(255, 215, 0, 0)');
                        ctx.fillStyle = glowGrad;
                        ctx.beginPath();
                        ctx.arc(collisionPointScreen.x, collisionPointScreen.y, 20, 0, Math.PI * 2);
                        ctx.fill();
                        
                        // Cross marker
                        ctx.strokeStyle = 'rgba(255, 215, 0, 0.9)';
                        ctx.lineWidth = 2;
                        ctx.beginPath();
                        ctx.moveTo(collisionPointScreen.x - pulseSize * 2, collisionPointScreen.y);
                        ctx.lineTo(collisionPointScreen.x + pulseSize * 2, collisionPointScreen.y);
                        ctx.moveTo(collisionPointScreen.x, collisionPointScreen.y - pulseSize * 2);
                        ctx.lineTo(collisionPointScreen.x, collisionPointScreen.y + pulseSize * 2);
                        ctx.stroke();
                        
                        // Object ball trajectory after collision
                        var ghostCueBallX = hitResult.collisionPoint.cueBallX;
                        var ghostCueBallY = hitResult.collisionPoint.cueBallY;
                        var objDx = hitResult.ball.x - ghostCueBallX;
                        var objDy = hitResult.ball.y - ghostCueBallY;
                        var objDist = Math.sqrt(objDx * objDx + objDy * objDy);
                        
                        if (objDist > 0.1) {
                            var objNx = objDx / objDist;
                            var objNy = objDy / objDist;
                            var trajectoryAngle = Math.atan2(objNy, objNx);
                            
                            // Draw object ball predicted path
                            var objTrajectoryLength = 150;
                            var objEndX = hitResult.ball.x + objNx * objTrajectoryLength;
                            var objEndY = hitResult.ball.y + objNy * objTrajectoryLength;
                            var objEndScreen = toScreen(objEndX, objEndY, ballR * scale * 2);
                            
                            var objCol = this.getBallColor(hitResult.ball.color);
                            ctx.strokeStyle = objCol.main.replace(')', ', 0.6)').replace('rgb', 'rgba');
                            ctx.lineWidth = 3;
                            ctx.setLineDash([6, 6]);
                            ctx.beginPath();
                            ctx.moveTo(objectBallScreen.x, objectBallScreen.y);
                            ctx.lineTo(objEndScreen.x, objEndScreen.y);
                            ctx.stroke();
                            ctx.setLineDash([]);
                            
                            // Arrow head for object ball direction
                            var arrowSize = 10;
                            var arrowAngle = Math.atan2(objEndScreen.y - objectBallScreen.y, objEndScreen.x - objectBallScreen.x);
                            ctx.fillStyle = objCol.main;
                            ctx.beginPath();
                            ctx.moveTo(objEndScreen.x, objEndScreen.y);
                            ctx.lineTo(objEndScreen.x - arrowSize * Math.cos(arrowAngle - 0.4), objEndScreen.y - arrowSize * Math.sin(arrowAngle - 0.4));
                            ctx.lineTo(objEndScreen.x - arrowSize * Math.cos(arrowAngle + 0.4), objEndScreen.y - arrowSize * Math.sin(arrowAngle + 0.4));
                            ctx.closePath();
                            ctx.fill();
                            
                            // Cue ball deflection path (90 degrees to object ball for stun)
                            var cueBallDeflectAngle = trajectoryAngle + Math.PI / 2;
                            // Determine which side based on original aim
                            var crossProduct = Math.cos(angle) * objNy - Math.sin(angle) * objNx;
                            if (crossProduct < 0) {
                                cueBallDeflectAngle = trajectoryAngle - Math.PI / 2;
                            }
                            
                            var cueDeflectLength = 80;
                            var cueDeflectEndX = ghostCueBallX + Math.cos(cueBallDeflectAngle) * cueDeflectLength;
                            var cueDeflectEndY = ghostCueBallY + Math.sin(cueBallDeflectAngle) * cueDeflectLength;
                            var cueDeflectScreen = toScreen(cueDeflectEndX, cueDeflectEndY, ballR * scale * 2);
                            
                            ctx.strokeStyle = 'rgba(200, 200, 255, 0.5)';
                            ctx.lineWidth = 2;
                            ctx.setLineDash([4, 4]);
                            ctx.beginPath();
                            ctx.moveTo(collisionScreen.x, collisionScreen.y);
                            ctx.lineTo(cueDeflectScreen.x, cueDeflectScreen.y);
                            ctx.stroke();
                            ctx.setLineDash([]);
                        }
                    } else {
                        // No collision - draw straight line to max distance
                        var endX = cueBallScreen.x + Math.cos(angle) * settings.trajectoryLength;
                        var endY = cueBallScreen.y + Math.sin(angle) * settings.trajectoryLength * ISO;
                        
                        ctx.strokeStyle = 'rgba(255,255,255,0.6)';
                        ctx.lineWidth = 2;
                        ctx.setLineDash([8, 8]);
                        ctx.beginPath();
                        ctx.moveTo(cueBallScreen.x, cueBallScreen.y);
                        ctx.lineTo(endX, endY);
                        ctx.stroke();
                        ctx.setLineDash([]);
                        
                        // Ghost ball at end
                        if (settings.showGhostBalls) {
                            ctx.strokeStyle = 'rgba(255,255,255,0.3)';
                            ctx.lineWidth = 2;
                            ctx.beginPath();
                            ctx.arc(endX, endY, ballR * scale, 0, Math.PI * 2);
                            ctx.stroke();
                        }
                    }
                }
                
                // Power bar (show when charging in any mode)
                var showPowerBar = (this.isShooting && this.pullBackDistance > 0) || this.clickPowerCharging;
                if (showPowerBar && this.shotPower > 0) {
                    var maxPwr = settings.maxPower;
                    var powerPct = Math.min(this.shotPower / maxPwr, 1);
                    var powerColor = powerPct < 0.3 ? '#4ade80' : powerPct < 0.7 ? '#fbbf24' : '#ef4444';
                    var mode = this.getShotMode();
                    
                    ctx.fillStyle = 'rgba(0,0,0,0.8)';
                    ctx.fillRect(w/2 - 80, h - 60, 160, 35);
                    
                    // Power bar background
                    ctx.fillStyle = 'rgba(255,255,255,0.2)';
                    ctx.fillRect(w/2 - 73, h - 53, 146, 21);
                    
                    // Power bar fill
                    ctx.fillStyle = powerColor;
                    ctx.fillRect(w/2 - 73, h - 53, 146 * powerPct, 21);
                    
                    // Power text
                    ctx.fillStyle = 'white';
                    ctx.font = 'bold 12px Arial';
                    ctx.textAlign = 'center';
                    ctx.fillText('POWER: ' + Math.round(powerPct * 100) + '%', w/2, h - 38);
                    
                    // Mode-specific instructions
                    ctx.font = '10px Arial';
                    ctx.fillStyle = '#fbbf24';
                    var modeHint = mode === 'drag' ? '? Pull back | ? Strike!' :
                                   mode === 'click' || mode === 'tap' ? 'Release to shoot!' :
                                   'Building power...';
                    ctx.fillText(modeHint, w/2, h - 25);
                }
            }
        }
        
        // HUD with shot mode info
        var mode = this.getShotMode();
        var modeNames = {
            drag: 'Drag ??',
            click: 'Click & Hold',
            tap: 'Tap & Hold',
            swipe: 'Swipe',
            slider: 'Slider'
        };
        var modeHints = {
            drag: 'Click to lock, drag ??',
            click: 'Hold to charge, release',
            tap: 'Hold to charge, release',
            swipe: 'Swipe direction & speed',
            slider: 'Use slider, click shoot'
        };
        
        ctx.fillStyle = 'rgba(0,0,0,0.75)';
        ctx.fillRect(w/2 - 140, 8, 280, 30);
        ctx.fillStyle = '#4ade80';
        ctx.font = 'bold 11px Arial';
        ctx.textAlign = 'center';
        var hudText = '?? 3D | ' + (modeNames[mode] || mode) + ' | ' + (modeHints[mode] || '');
        if (settings.showFps && typeof game !== 'undefined' && game.fps) {
            hudText += ' | ' + Math.round(game.fps) + ' FPS';
        }
        ctx.fillText(hudText, w/2, 28);
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
    
    // Find the first ball that would be hit along the aim line
    findFirstBallHit: function(cueBall, aimAngle, allBalls) {
        if (!cueBall || !allBalls) return null;
        
        var dirX = Math.cos(aimAngle);
        var dirY = Math.sin(aimAngle);
        var closestHit = null;
        var closestDist = Infinity;
        
        for (var i = 0; i < allBalls.length; i++) {
            var ball = allBalls[i];
            if (ball === cueBall || ball.potted || ball.color === 'white') continue;
            
            // Vector from cue ball to target ball
            var dx = ball.x - cueBall.x;
            var dy = ball.y - cueBall.y;
            
            // Project onto aim direction
            var dot = dx * dirX + dy * dirY;
            
            // Ball must be in front of cue ball
            if (dot <= 0) continue;
            
            // Closest point on aim line to target ball center
            var closestX = cueBall.x + dirX * dot;
            var closestY = cueBall.y + dirY * dot;
            
            // Distance from line to ball center
            var lineDistX = ball.x - closestX;
            var lineDistY = ball.y - closestY;
            var lineDist = Math.sqrt(lineDistX * lineDistX + lineDistY * lineDistY);
            
            // Combined radius for collision
            var combinedR = (cueBall.r || 14) + (ball.r || 14);
            
            // Check if line passes close enough to hit
            if (lineDist < combinedR) {
                // Calculate exact collision point using circle-line intersection
                var a = dirX * dirX + dirY * dirY;
                var b = 2 * (dirX * (cueBall.x - ball.x) + dirY * (cueBall.y - ball.y));
                var c = (cueBall.x - ball.x) * (cueBall.x - ball.x) + 
                        (cueBall.y - ball.y) * (cueBall.y - ball.y) - 
                        combinedR * combinedR;
                
                var discriminant = b * b - 4 * a * c;
                
                if (discriminant >= 0) {
                    var t = (-b - Math.sqrt(discriminant)) / (2 * a);
                    
                    if (t > 0 && t < closestDist) {
                        closestDist = t;
                        
                        // Cue ball position at collision
                        var cueBallAtCollisionX = cueBall.x + dirX * t;
                        var cueBallAtCollisionY = cueBall.y + dirY * t;
                        
                        // Contact point (on the surface between balls)
                        var contactDx = ball.x - cueBallAtCollisionX;
                        var contactDy = ball.y - cueBallAtCollisionY;
                        var contactDist = Math.sqrt(contactDx * contactDx + contactDy * contactDy);
                        var contactNx = contactDx / contactDist;
                        var contactNy = contactDy / contactDist;
                        
                        var contactX = cueBallAtCollisionX + contactNx * (cueBall.r || 14);
                        var contactY = cueBallAtCollisionY + contactNy * (cueBall.r || 14);
                        
                        closestHit = {
                            ball: ball,
                            collisionPoint: {
                                x: contactX,
                                y: contactY,
                                cueBallX: cueBallAtCollisionX,
                                cueBallY: cueBallAtCollisionY
                            },
                            impactAngle: Math.atan2(contactNy, contactNx),
                            distance: t
                        };
                    }
                }
            }
        }
        
        return closestHit;
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

console.log('[3D] Pool3DRenderer v5.0 loaded - all dev settings linked!');
""";
    }
}
