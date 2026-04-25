namespace Wdpl2.Services;

/// <summary>
/// AI opponent module for the pool game.
/// Picks a legal target ball + pocket using ghost-ball geometry,
/// applies difficulty-based aim/power noise, and falls back to a safety
/// shot when no clean pot exists. Also handles ball-in-hand placement.
/// </summary>
public static class PoolAiModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL AI OPPONENT MODULE
// Plays as one of the two players using simple
// ghost-ball aiming with line-of-sight checks.
// ============================================

const PoolAI = {
    game: null,
    config: {
        // Per-seat: true = that player is controlled by the AI.
        // [false, false]=off, [false,true]=AI is P2 (default human-vs-AI),
        // [true,false]=AI is P1, [true,true]=AI vs AI demo mode.
        aiPlayers: [false, false],
        difficulty: 'medium',     // 'easy' | 'medium' | 'hard' (used for both seats)
        thinkTimeMs: 900,         // visible 'thinking' delay before shot
    },
    _scheduled: false,
    _busy: false,

    // Cycle order for the toggle button
    _modeOrder: ['off', 'p2', 'p1', 'both'],
    _modeLabels: { off: 'AI: OFF', p2: 'AI: P2', p1: 'AI: P1', both: 'AI vs AI' },
    _modeColors: {
        off:  'rgba(75,85,99,0.95)',
        p2:   'linear-gradient(135deg,#10b981,#059669)',
        p1:   'linear-gradient(135deg,#3b82f6,#1d4ed8)',
        both: 'linear-gradient(135deg,#a855f7,#7e22ce)',
    },

    // Difficulty profiles
    profiles: {
        easy:   { aimNoise: 0.085, powerNoise: 0.30, maxPowerFrac: 0.85, ignoreBlockers: true,  safetyChance: 0.05, maxConsidered: 3 },
        medium: { aimNoise: 0.035, powerNoise: 0.18, maxPowerFrac: 0.90, ignoreBlockers: false, safetyChance: 0.10, maxConsidered: 5 },
        hard:   { aimNoise: 0.012, powerNoise: 0.08, maxPowerFrac: 0.95, ignoreBlockers: false, safetyChance: 0.20, maxConsidered: 8 },
    },

    init(game) {
        this.game = game;
        this.createToggleButton();
        // First check in case AI starts the frame
        this.onTurnChanged();
        console.log('[AI] PoolAI initialized');
    },

    /**
     * Set which seats are AI-controlled.
     *   mode: 'off' | 'p1' | 'p2' | 'both'
     *   difficulty (optional): 'easy' | 'medium' | 'hard'
     */
    setMode(mode, difficulty) {
        switch (mode) {
            case 'off':  this.config.aiPlayers = [false, false]; break;
            case 'p1':   this.config.aiPlayers = [true, false]; break;
            case 'p2':   this.config.aiPlayers = [false, true]; break;
            case 'both': this.config.aiPlayers = [true, true]; break;
            default:
                console.warn('[AI] Unknown mode:', mode);
                return;
        }
        if (difficulty && this.profiles[difficulty]) this.config.difficulty = difficulty;
        this.updateToggleButton();
        console.log('[AI] Mode set to', mode, '(' + this.config.difficulty + ')');
        this.onTurnChanged();
    },

    // Convenience wrappers (kept for backward compat with earlier console API)
    enable(difficulty)  { this.setMode('p2', difficulty); },
    disable()           { this.setMode('off'); },

    setDifficulty(difficulty) {
        if (this.profiles[difficulty]) {
            this.config.difficulty = difficulty;
            this.updateToggleButton();
            console.log('[AI] Difficulty:', difficulty);
        }
    },

    _currentMode() {
        const a = this.config.aiPlayers;
        if (!a[0] && !a[1]) return 'off';
        if (a[0] && a[1])   return 'both';
        return a[1] ? 'p2' : 'p1';
    },

    cycleMode() {
        const cur = this._currentMode();
        const idx = this._modeOrder.indexOf(cur);
        const next = this._modeOrder[(idx + 1) % this._modeOrder.length];
        this.setMode(next);
    },

    isAiTurn() {
        if (!this.game) return false;
        if (this.game.gameOver || this.game.gamePhase === 'finished') return false;
        const idx = this.game.currentPlayerIndex;
        return !!(this.config.aiPlayers && this.config.aiPlayers[idx]);
    },

    // Called whenever the turn might have changed
    onTurnChanged() {
        if (!this.isAiTurn()) return;
        if (this._scheduled || this._busy) return;
        this._scheduled = true;

        // Wait until balls stop moving, then think, then shoot
        const waitForStop = () => {
            const moving = this.game.balls.some(b => !b.potted && (Math.abs(b.vx) > 0.01 || Math.abs(b.vy) > 0.01));
            if (moving || this.game.shotInProgress) {
                setTimeout(waitForStop, 80);
                return;
            }
            // Re-check turn (might have flipped while waiting)
            if (!this.isAiTurn()) { this._scheduled = false; return; }
            this.showThinking(true);
            setTimeout(() => this.takeShot(), this.config.thinkTimeMs);
        };
        setTimeout(waitForStop, 60);
    },

    takeShot() {
        try {
            this._busy = true;
            this._scheduled = false;
            if (!this.isAiTurn()) return;
            if (!this.game.cueBall || this.game.cueBall.potted) return;

            // Handle ball-in-hand first
            if (this.game.ballInHand) {
                this.placeCueBallSomewhereLegal();
            }

            const profile = this.profiles[this.config.difficulty] || this.profiles.medium;
            const shot = this.chooseBestShot(profile);

            // Decide between best-pot and safety
            const useSafety = !shot || (Math.random() < profile.safetyChance && shot.score < 60);
            const finalShot = useSafety ? this.chooseSafetyShot() : shot;

            if (!finalShot) {
                // Last resort: random small tap toward the rack
                this.fireShot(0, 8);
                return;
            }

            // Apply aim noise (radians) and power noise (multiplier)
            const aim = finalShot.aim + (Math.random() - 0.5) * 2 * profile.aimNoise;
            const powerScale = 1 + (Math.random() - 0.5) * 2 * profile.powerNoise;
            const power = Math.max(4, Math.min(this.game.maxPower, finalShot.power * powerScale));

            this.fireShot(aim, power);
        } catch (e) {
            console.error('[AI] takeShot error:', e);
        } finally {
            this.showThinking(false);
            this._busy = false;
        }
    },

    fireShot(aim, power) {
        const g = this.game;
        g.aimAngle = aim;
        if (typeof g.startShot === 'function') g.startShot();
        g.cueBall.vx = Math.cos(aim) * power;
        g.cueBall.vy = Math.sin(aim) * power;
        if (typeof PoolSpinControl !== 'undefined' && typeof PoolSpinControl.applySpinToBall === 'function') {
            // No spin from AI (keeps it simple and predictable)
            try { PoolSpinControl.applySpinToBall(g.cueBall, aim); } catch (e) { /* ignore */ }
        }
        g.isAiming = false;
        g.isShooting = false;
        console.log('[AI] Shot fired - aim:', aim.toFixed(3), 'power:', power.toFixed(2));
    },

    // ---------- TARGET SELECTION ----------
    getLegalTargetBalls() {
        const g = this.game;
        const player = g.players[g.currentPlayerIndex];
        const live = g.balls.filter(b => !b.potted && b.num !== 0);

        // On the black -> only black is legal
        if (player && player.onBlack) return live.filter(b => b.num === 8);

        // Table open -> any non-black
        if (g.tableOpen) return live.filter(b => b.num !== 8);

        // Player has a colour assigned
        if (player && player.color) return live.filter(b => b.color === player.color);

        // Fallback (should not normally happen)
        return live.filter(b => b.num !== 8);
    },

    chooseBestShot(profile) {
        const g = this.game;
        const cue = g.cueBall;
        const targets = this.getLegalTargetBalls();
        if (targets.length === 0) return null;

        const candidates = [];
        for (const obj of targets) {
            for (const pocket of g.pockets) {
                const candidate = this.evaluateShot(cue, obj, pocket, profile);
                if (candidate) candidates.push(candidate);
            }
        }
        if (candidates.length === 0) return null;

        candidates.sort((a, b) => b.score - a.score);
        const top = candidates.slice(0, profile.maxConsidered);
        // Easy / medium: pick somewhat randomly from top to feel more human
        const pickIndex = (this.config.difficulty === 'hard') ? 0 : Math.floor(Math.random() * top.length);
        return top[pickIndex];
    },

    evaluateShot(cue, obj, pocket, profile) {
        // Ghost-ball position: where the cue ball must arrive to send the object toward the pocket.
        const objToPocket = { x: pocket.x - obj.x, y: pocket.y - obj.y };
        const dOP = Math.hypot(objToPocket.x, objToPocket.y);
        if (dOP < 1) return null;
        const nOP = { x: objToPocket.x / dOP, y: objToPocket.y / dOP };

        const ghost = {
            x: obj.x - nOP.x * (obj.r + cue.r),
            y: obj.y - nOP.y * (obj.r + cue.r),
        };

        // Bounds check: ghost must be inside the playable area (with margin).
        const m = this.game.cushionMargin + cue.r;
        if (ghost.x < m || ghost.x > this.game.width - m ||
            ghost.y < m || ghost.y > this.game.height - m) {
            return null;
        }

        // Aim direction (cue -> ghost)
        const dx = ghost.x - cue.x;
        const dy = ghost.y - cue.y;
        const dCG = Math.hypot(dx, dy);
        if (dCG < 1) return null;
        const aim = Math.atan2(dy, dx);

        // Cut angle: 0 = straight, PI/2 = impossibly thin
        const dot = (nOP.x * (dx / dCG)) + (nOP.y * (dy / dCG));
        const cutAngle = Math.acos(Math.max(-1, Math.min(1, dot))); // 0..PI
        if (cutAngle > Math.PI * 0.48) return null; // too thin to make

        // Line-of-sight: cue->ghost and obj->pocket
        if (!profile.ignoreBlockers) {
            if (this.pathBlocked(cue, ghost, cue.r, [obj])) return null;
            if (this.pathBlocked(obj, pocket, obj.r, [cue])) return null;
        }

        // Power: enough to roll the object the rest of the way + cushion damping
        const total = dCG + dOP;
        // Map total distance (px) to power (units of game.maxPower).
        // Calibrated so a half-table shot = ~55% power, full table = ~80%.
        const distFrac = Math.min(1, total / 1400);
        let power = (0.45 + distFrac * 0.45) * this.game.maxPower * profile.maxPowerFrac;

        // Score: prefer straight shots, short distance, near-pocket objects
        const cutPenalty = (cutAngle / (Math.PI * 0.48)) * 60;     // 0..60
        const distPenalty = Math.min(40, total / 35);              // 0..40
        const baseScore = 100 - cutPenalty - distPenalty;

        // Bonus if pocketing the ball that puts player on the black
        let bonus = 0;
        const player = this.game.players[this.game.currentPlayerIndex];
        if (player && player.color) {
            const remaining = this.game.balls.filter(b => !b.potted && b.color === player.color).length;
            if (remaining === 1) bonus += 10; // last colour ball
        }
        if (obj.num === 8 && player && player.onBlack) bonus += 25; // game-winning shot

        return {
            obj, pocket, aim, power,
            cutAngle, distance: total,
            score: baseScore + bonus,
        };
    },

    // Returns true if any ball (other than ignored ones) intersects the segment from->to
    pathBlocked(from, to, radius, ignore) {
        const g = this.game;
        const dx = to.x - from.x;
        const dy = to.y - from.y;
        const len = Math.hypot(dx, dy);
        if (len < 1) return false;
        const nx = dx / len, ny = dy / len;

        for (const b of g.balls) {
            if (b.potted) continue;
            if (ignore && ignore.indexOf(b) !== -1) continue;
            // Project ball center onto the segment
            const px = b.x - from.x;
            const py = b.y - from.y;
            const t = px * nx + py * ny;
            if (t <= 0 || t >= len) continue; // not within the segment
            const closestX = from.x + nx * t;
            const closestY = from.y + ny * t;
            const dist = Math.hypot(b.x - closestX, b.y - closestY);
            if (dist < b.r + radius - 1) return true;
        }
        return false;
    },

    // ---------- SAFETY SHOT ----------
    chooseSafetyShot() {
        const g = this.game;
        const cue = g.cueBall;
        const targets = this.getLegalTargetBalls();
        if (targets.length === 0) return null;

        // Pick the closest legal target and tap it gently toward the far end
        let best = null;
        let bestDist = Infinity;
        for (const t of targets) {
            const d = Math.hypot(t.x - cue.x, t.y - cue.y);
            if (d < bestDist) { bestDist = d; best = t; }
        }
        if (!best) return null;

        const aim = Math.atan2(best.y - cue.y, best.x - cue.x);
        const power = g.maxPower * 0.35;
        return { obj: best, pocket: null, aim, power, cutAngle: 0, distance: bestDist, score: 0, safety: true };
    },

    // ---------- BALL IN HAND ----------
    placeCueBallSomewhereLegal() {
        const g = this.game;
        const cue = g.cueBall;
        const targets = this.getLegalTargetBalls();

        // Default candidate spots: behind baulk if restricted, else a grid
        const spots = [];
        const margin = g.cushionMargin + cue.r + 4;

        if (g.ballInHandBaulk) {
            // Sample inside the baulk D
            const xMax = g.baulkLineX - cue.r - 2;
            for (let y = margin; y <= g.height - margin; y += 30) {
                for (let x = margin; x <= xMax; x += 30) {
                    spots.push({ x, y });
                }
            }
        } else {
            for (let y = margin; y <= g.height - margin; y += 60) {
                for (let x = margin; x <= g.width - margin; x += 60) {
                    spots.push({ x, y });
                }
            }
        }

        // Score each spot by best-shot quality from there
        const profile = this.profiles[this.config.difficulty] || this.profiles.medium;
        let bestSpot = null;
        let bestScore = -Infinity;

        // Save and restore cue ball state while we test positions
        const origX = cue.x, origY = cue.y;
        for (const s of spots) {
            // Reject overlapping
            let overlap = false;
            for (const b of g.balls) {
                if (b === cue || b.potted) continue;
                if (Math.hypot(s.x - b.x, s.y - b.y) < cue.r + b.r + 2) { overlap = true; break; }
            }
            if (overlap) continue;

            cue.x = s.x; cue.y = s.y;
            let bestHere = -Infinity;
            for (const obj of targets) {
                for (const pocket of g.pockets) {
                    const cand = this.evaluateShot(cue, obj, pocket, profile);
                    if (cand && cand.score > bestHere) bestHere = cand.score;
                }
            }
            if (bestHere > bestScore) { bestScore = bestHere; bestSpot = s; }
        }
        cue.x = origX; cue.y = origY;

        // Fallback if nothing scored: middle of legal area
        if (!bestSpot) {
            bestSpot = g.ballInHandBaulk
                ? { x: g.baulkLineX / 2, y: g.height / 2 }
                : { x: g.width / 4, y: g.height / 2 };
        }

        cue.x = bestSpot.x;
        cue.y = bestSpot.y;
        cue.vx = 0; cue.vy = 0;
        g.ballInHand = false;
        g.ballInHandBaulk = false;
        if (typeof g.updateTurnDisplay === 'function') g.updateTurnDisplay();
        console.log('[AI] Placed cue ball at', bestSpot.x.toFixed(0), bestSpot.y.toFixed(0));
    },

    // ---------- UI: toggle button + thinking indicator ----------
    createToggleButton() {
        if (document.getElementById('aiToggleBtn')) return;
        const btn = document.createElement('button');
        btn.id = 'aiToggleBtn';
        btn.style.cssText = 'position:fixed;top:10px;left:140px;padding:10px 16px;background:rgba(75,85,99,0.95);color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:9998;font-size:13px;box-shadow:0 4px 12px rgba(0,0,0,0.3);transition:all .2s;';
        btn.title = 'Click: cycle AI mode (OFF / P2 / P1 / AI-vs-AI). Right-click: cycle difficulty.';
        btn.addEventListener('click', () => this.cycleMode());
        btn.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            const order = ['easy', 'medium', 'hard'];
            const idx = order.indexOf(this.config.difficulty);
            this.setDifficulty(order[(idx + 1) % order.length]);
        });
        document.body.appendChild(btn);
        this.updateToggleButton();
    },

    updateToggleButton() {
        const btn = document.getElementById('aiToggleBtn');
        if (!btn) return;
        const mode = this._currentMode();
        btn.style.background = this._modeColors[mode];
        const label = this._modeLabels[mode];
        btn.textContent = (mode === 'off') ? label : (label + ' [' + this.config.difficulty.toUpperCase() + ']');
    },

    showThinking(show) {
        let el = document.getElementById('aiThinkingIndicator');
        if (show) {
            if (!el) {
                el = document.createElement('div');
                el.id = 'aiThinkingIndicator';
                el.style.cssText = 'position:fixed;top:55px;left:140px;padding:6px 12px;background:rgba(16,185,129,0.9);color:white;border-radius:6px;font-size:12px;font-weight:bold;z-index:9998;box-shadow:0 2px 8px rgba(0,0,0,0.3);';
                document.body.appendChild(el);
            }
            el.textContent = 'AI thinking...';
            el.style.display = 'block';
        } else if (el) {
            el.style.display = 'none';
        }
    },
};
";
    }
}
