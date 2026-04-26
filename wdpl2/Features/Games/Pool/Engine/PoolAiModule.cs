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
    // - aimNoise:        baseline std-dev of aim error (radians), scaled up on harder cuts at shot time.
    // - powerNoise:      +/- multiplier on chosen power.
    // - maxPowerFrac:    cap on chosen power as a fraction of game.maxPower.
    // - ignoreBlockers:  skip line-of-sight checks (easy only).
    // - safetyChance:    probability of opting for a safety when no great shot exists.
    // - maxConsidered:   how many top-ranked shots to sample from.
    // - positionPlay:    enable post-collision cue prediction & next-shot bonus.
    // - scratchAvoidance: penalty multiplier when predicted cue path enters a pocket.
    // - selectionBias:   how strongly to bias toward the best of the top-N (1=uniform, higher=greedier).
    profiles: {
        easy:   { aimNoise: 0.075, powerNoise: 0.28, maxPowerFrac: 0.85, ignoreBlockers: true,  safetyChance: 0.05, maxConsidered: 3,  positionPlay: false, scratchAvoidance: 0.8, selectionBias: 1.5 },
        medium: { aimNoise: 0.028, powerNoise: 0.15, maxPowerFrac: 0.90, ignoreBlockers: false, safetyChance: 0.10, maxConsidered: 6,  positionPlay: false, scratchAvoidance: 1.3, selectionBias: 3.0 },
        hard:   { aimNoise: 0.008, powerNoise: 0.05, maxPowerFrac: 0.95, ignoreBlockers: false, safetyChance: 0.18, maxConsidered: 10, positionPlay: true,  scratchAvoidance: 1.8, selectionBias: 8.0 },
    },

    // Maximum makeable cut angle (radians). ~75 degrees - thinner cuts are rejected.
    MAX_CUT: Math.PI * 0.42,

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

            // Special case: opening break shot. Don't try to ghost-ball pot from a rack.
            if (this.game.gamePhase === 'break') {
                const breakShot = this.chooseBreakShot(profile);
                if (breakShot) {
                    // Use straight power on the break (no cut-scaled aim noise).
                    const aimSampleB = ((Math.random() + Math.random()) - 1);
                    const aimB = breakShot.aim + aimSampleB * profile.aimNoise * 1.2;
                    const powerSampleB = ((Math.random() + Math.random()) - 1);
                    const powerB = Math.max(4, Math.min(this.game.maxPower,
                                       breakShot.power * (1 + powerSampleB * profile.powerNoise * 0.5)));
                    // Slight top spin on the break drives the cue forward through the rack
                    // for better dispersion and helps it stay near the centre after impact.
                    this.fireShot(aimB, powerB, { x: 0, y: 0.4 });
                    return;
                }
            }

            const shot = this.chooseBestShot(profile);

            // Decide between best-pot and safety
            const useSafety = !shot || (Math.random() < profile.safetyChance && shot.score < 60);
            const finalShot = useSafety ? this.chooseSafetyShot() : shot;

            if (!finalShot) {
                // Last resort: tap toward the nearest legal ball with enough power to reach
                // a cushion after contact. Aiming at angle 0 with power 8 (the previous fallback)
                // would frequently fire into empty space and foul with 'Failed to hit any ball'.
                const targetsLR = this.getLegalTargetBalls();
                if (targetsLR.length > 0) {
                    let near = targetsLR[0];
                    let nd = Math.hypot(near.x - this.game.cueBall.x, near.y - this.game.cueBall.y);
                    for (let i = 1; i < targetsLR.length; i++) {
                        const d = Math.hypot(targetsLR[i].x - this.game.cueBall.x, targetsLR[i].y - this.game.cueBall.y);
                        if (d < nd) { nd = d; near = targetsLR[i]; }
                    }
                    const aimLR = Math.atan2(near.y - this.game.cueBall.y, near.x - this.game.cueBall.x);
                    this.fireShot(aimLR, this.game.maxPower * 0.55);
                } else {
                    // No legal targets at all -- this should only happen if game state is broken
                    // (e.g. 8-ball was incorrectly removed without ending the game). Stop the AI
                    // rather than fire pointless tap shots forever.
                    console.error('[AI] No legal targets and game still active. Disabling AI to prevent foul loop. Balls on table:',
                                  this.game.balls.filter(b => !b.potted).map(b => b.num + b.color));
                    this.disable();
                }
                return;
            }

            // Aim noise scales with cut difficulty: thin cuts are missed more often than hangers.
            // Use a sum-of-two-uniforms (~triangular) for a softer, more human-feeling distribution.
            const cut = finalShot.cutAngle || 0;
            const cutFactor = 1 + (cut / this.MAX_CUT) * 1.4; // 1.0 .. ~2.4
            const aimSample = ((Math.random() + Math.random()) - 1); // ~triangular in [-1, 1]
            const aim = finalShot.aim + aimSample * profile.aimNoise * cutFactor;

            // Power noise also softened, and reduced for safeties (don't want to over-hit a safety).
            const powerSample = ((Math.random() + Math.random()) - 1);
            const powerNoise = profile.powerNoise * (finalShot.safety ? 0.5 : 1);
            const powerScale = 1 + powerSample * powerNoise;
            const power = Math.max(4, Math.min(this.game.maxPower, finalShot.power * powerScale));

            // Spin selection - choose top/back/english to support the shot intent.
            const spin = this.chooseSpin(finalShot, profile);

            this.fireShot(aim, power, spin);
        } catch (e) {
            console.error('[AI] takeShot error:', e);
        } finally {
            this.showThinking(false);
            this._busy = false;
        }
    },

    /**
     * Choose a spin vector (x = english/side, y = top/back) appropriate for the shot.
     * Returns { x, y } in [-1, 1].
     *
     * Strategy:
     *  - Safety: medium back spin so the cue dies after impact, with side english biased
     *    toward leaving the cue in the safe zone.
     *  - Bank safety: top spin so the cue carries through the cushion route.
     *  - Hangers (very close to pocket, small cut): mild back spin to avoid scratch.
     *  - Position play (hard, target hit, next ball known via cueEndX/Y): top spin if the
     *    next ball is further along the shot line (follow), back spin if it's behind (draw).
     *  - Big cuts: stun (no spin) so energy goes into the object ball.
     *  - Default: light top spin for natural roll.
     *
     * Easy/medium AI uses a fraction of the chosen spin so it doesn't look superhuman;
     * hard uses the full amount.
     */
    chooseSpin(shot, profile) {
        if (!shot) return { x: 0, y: 0 };
        const g = this.game;
        const cue = g.cueBall;

        // Difficulty-scaled cap on spin magnitude
        const cap = (this.config.difficulty === 'hard') ? 0.85
                  : (this.config.difficulty === 'medium') ? 0.55
                  : 0.30;

        let sx = 0, sy = 0;

        if (shot.safety) {
            if (shot.bank) {
                // Bank safety: a bit of top spin keeps the cue rolling along the rail.
                sy = 0.35;
            } else {
                // Direct safety: back spin to park the cue after contact. Keep side
                // english to a minimum -- the squirt physics deflect the cue several
                // degrees on heavy english, which makes the AI miss the intended ball
                // entirely and frequently hit an opponent ball first.
                sy = -0.55;
                // No side english on safeties (was the cause of multiple opponent-first fouls).
                sx = 0;
            }
        } else if (shot.breakShot) {
            sy = 0.40;
        } else {
            const cut = shot.cutAngle || 0;
            const dist = shot.distance || 0;
            const distFrac = Math.min(1, dist / 900);

            if (cut > this.MAX_CUT * 0.7) {
                // Thin cut: stun. Avoid heavy english -- squirt deflection causes more
                // misses than the throw compensation gains. A tiny outside touch only.
                sy = -0.10;
                const dx = shot.obj.x - cue.x, dy = shot.obj.y - cue.y;
                const ax = Math.cos(shot.aim), ay = Math.sin(shot.aim);
                const cross = ax * dy - ay * dx;
                sx = Math.sign(cross) * 0.10;
            } else if (cut < this.MAX_CUT * 0.15 && dist < 200) {
                // Near-straight close-range pot (potential scratch): use back spin.
                sy = -0.45;
            } else if (profile.positionPlay && shot.cueEndX !== undefined) {
                // Position play - decide follow vs draw based on cue post-contact direction
                // versus where the next ball would be more useful. For simplicity, prefer
                // top spin on long shots (cue carries forward into the table) and back
                // spin on short shots (parks cue near current spot for next play).
                sy = (distFrac > 0.5) ? 0.45 : -0.30;
            } else {
                // Generic medium pot: mild top spin for natural roll. No side english
                // by default -- the squirt cost outweighs throw compensation at this skill.
                sy = 0.25;
                sx = 0;
            }
        }

        // Apply difficulty cap and slight noise so spin doesn't look robotic
        const noiseX = (Math.random() - 0.5) * 0.08;
        const noiseY = (Math.random() - 0.5) * 0.08;
        sx = Math.max(-1, Math.min(1, sx * cap + noiseX));
        sy = Math.max(-1, Math.min(1, sy * cap + noiseY));
        return { x: sx, y: sy };
    },

    fireShot(aim, power, spin) {
        const g = this.game;
        g.aimAngle = aim;
        // Pre-set spin on the shared controller BEFORE startShot/applySpinToBall pipeline runs.
        // spin is { x, y } in [-1, 1]; defaults to no spin if omitted.
        const sx = (spin && typeof spin.x === 'number') ? Math.max(-1, Math.min(1, spin.x)) : 0;
        const sy = (spin && typeof spin.y === 'number') ? Math.max(-1, Math.min(1, spin.y)) : 0;
        if (typeof PoolSpinControl !== 'undefined') {
            PoolSpinControl.spinX = sx;
            PoolSpinControl.spinY = sy;
        }
        if (typeof g.startShot === 'function') g.startShot();
        // Match the player's shot pipeline: actual cue velocity = power * powerMultiplier.
        // The AI was previously omitting the multiplier, which made every shot (especially the
        // break) noticeably weaker than a human shot at the same nominal power.
        const mult = (typeof g.powerMultiplier === 'number' && g.powerMultiplier > 0) ? g.powerMultiplier : 1.0;
        const v = power * mult;
        g.cueBall.vx = Math.cos(aim) * v;
        g.cueBall.vy = Math.sin(aim) * v;
        if (typeof PoolSpinControl !== 'undefined' && typeof PoolSpinControl.applySpinToBall === 'function') {
            try { PoolSpinControl.applySpinToBall(g.cueBall, aim); } catch (e) { /* ignore */ }
        }
        g.isAiming = false;
        g.isShooting = false;
        console.log('[AI] Shot fired - aim:', aim.toFixed(3), 'power:', power.toFixed(2), 'velocity:', v.toFixed(2), 'spin:', sx.toFixed(2), sy.toFixed(2));
    },

    // ---------- TARGET SELECTION ----------
    // Returns true if a ball position lies inside the playable area. Used to filter
    // out 'escaped' or stuck-outside-table balls so the AI doesn't keep firing at one.
    _onTable(b) {
        const g = this.game;
        if (!g) return true;
        return b.x > 0 && b.x < g.width && b.y > 0 && b.y < g.height;
    },

    getLegalTargetBalls() {
        const g = this.game;
        const player = g.players[g.currentPlayerIndex];
        const live = g.balls.filter(b => !b.potted && b.num !== 0 && this._onTable(b));

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

        // Weighted pick: better shots much more likely than worse ones.
        // weight_i = (1 / (i+1)) ^ selectionBias  -> bias=1 uniform-ish, higher = greedier.
        const bias = profile.selectionBias || 1;
        const weights = top.map((_, i) => Math.pow(1 / (i + 1), bias));
        const total = weights.reduce((a, b) => a + b, 0);
        let r = Math.random() * total;
        for (let i = 0; i < top.length; i++) {
            r -= weights[i];
            if (r <= 0) return top[i];
        }
        return top[0];
    },

    evaluateShot(cue, obj, pocket, profile) {
        const g = this.game;

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
        const m = g.cushionMargin + cue.r;
        if (ghost.x < m || ghost.x > g.width - m ||
            ghost.y < m || ghost.y > g.height - m) {
            return null;
        }

        // Reject if any other ball would already be sitting on the ghost-ball spot.
        for (const b of g.balls) {
            if (b === obj || b === cue || b.potted) continue;
            if (Math.hypot(ghost.x - b.x, ghost.y - b.y) < cue.r + b.r) return null;
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
        if (cutAngle > this.MAX_CUT) return null; // too thin to make

        // Line-of-sight: cue->ghost and obj->pocket
        if (!profile.ignoreBlockers) {
            if (this.pathBlocked(cue, ghost, cue.r, [obj])) return null;
            if (this.pathBlocked(obj, pocket, obj.r, [cue])) return null;
        }

        // Pocket approach quality: dot of (obj->pocket travel dir) with (centre->pocket OUTWARD dir).
        // The object ball needs to be moving outward (toward the rail/pocket); a high dot means it
        // enters the pocket along its 'mouth', a strongly negative dot means the line crosses the
        // pocket from the wrong side (which only happens for unrealistic geometry).
        const pcx = pocket.x - (g.width  / 2);
        const pcy = pocket.y - (g.height / 2);
        const pcLen = Math.hypot(pcx, pcy) || 1;
        const approachDot = nOP.x * (pcx / pcLen) + nOP.y * (pcy / pcLen);
        if (approachDot < -0.35) return null; // wrong-side approach

        // Power: enough to roll the object the rest of the way, scaled up by 1/cos(cut)
        // because energy transfer to the object falls off with the cut angle.
        const total = dCG + dOP;
        const distFrac = Math.min(1, total / 1400);
        const cutCos = Math.max(0.25, Math.cos(cutAngle));
        let power = (0.42 + distFrac * 0.45) * g.maxPower * profile.maxPowerFrac / cutCos;
        power = Math.min(power, g.maxPower * profile.maxPowerFrac);

        // -------- SCORE --------
        const cutPenalty   = (cutAngle / this.MAX_CUT) * 60;         // 0..60
        const distPenalty  = Math.min(40, total / 35);               // 0..40
        const approachBonus = Math.max(0, approachDot) * 18;         // 0..~18 for clean line-in
        let score = 100 - cutPenalty - distPenalty + approachBonus;

        // -------- POST-COLLISION CUE PREDICTION (stun-shot approximation) --------
        // Cue continues perpendicular to the line of centers at impact, with magnitude ~= power*sin(cut).
        const perpSign = Math.sign(dx * (-nOP.y) + dy * nOP.x) || 1;
        const perpX = -nOP.y * perpSign;
        const perpY =  nOP.x * perpSign;
        const cuePostSpeed = power * Math.sin(cutAngle);
        const cueRollDist  = Math.min(420, cuePostSpeed * 9); // px before friction stops it
        const cueEndX = ghost.x + perpX * cueRollDist;
        const cueEndY = ghost.y + perpY * cueRollDist;

        // Scratch check: does the predicted cue path pass through any pocket?
        const pocketR = (g.pockets[0] && g.pockets[0].r) || 22;
        let scratchRisk = 0;
        for (const p of g.pockets) {
            // Distance from segment ghost->cueEnd to pocket center
            const sx = cueEndX - ghost.x, sy = cueEndY - ghost.y;
            const segLen = Math.hypot(sx, sy);
            if (segLen < 1) break;
            const ux = sx / segLen, uy = sy / segLen;
            const t = (p.x - ghost.x) * ux + (p.y - ghost.y) * uy;
            const tt = Math.max(0, Math.min(segLen, t));
            const cx = ghost.x + ux * tt, cy = ghost.y + uy * tt;
            const d = Math.hypot(p.x - cx, p.y - cy);
            if (d < pocketR + cue.r) {
                // Closer to pocket center => higher risk
                scratchRisk = Math.max(scratchRisk, 1 - (d / (pocketR + cue.r)));
            }
        }
        score -= scratchRisk * 80 * profile.scratchAvoidance;

        // -------- POSITION PLAY (Hard only) --------
        // Reward shots that leave the cue near a future legal target with line-of-sight.
        if (profile.positionPlay) {
            const player = g.players[g.currentPlayerIndex];
            const futureTargets = g.balls.filter(b => {
                if (b.potted || b === obj || b.num === 0) return false;
                if (player && player.onBlack) return b.num === 8;
                if (g.tableOpen) return b.num !== 8;
                if (player && player.color) return b.color === player.color;
                return b.num !== 8;
            });

            let bestNext = Infinity;
            for (const ft of futureTargets) {
                const d = Math.hypot(ft.x - cueEndX, ft.y - cueEndY);
                // Penalty if the next ball is hidden from the predicted cue spot
                const blocked = this.pathBlocked({ x: cueEndX, y: cueEndY }, ft, cue.r, [obj]);
                const eff = blocked ? d * 1.6 + 200 : d;
                if (eff < bestNext) bestNext = eff;
            }
            if (bestNext !== Infinity) {
                // 0..20 bonus: best when next ball is within ~150px and visible
                score += Math.max(0, 20 - bestNext / 30);
            }
        }

        // -------- TACTICAL BONUSES --------
        const player = g.players[g.currentPlayerIndex];
        if (player && player.color) {
            const remaining = g.balls.filter(b => !b.potted && b.color === player.color).length;
            if (remaining === 1) score += 10; // last colour ball -> opens up the black
        }
        if (obj.num === 8 && player && player.onBlack) score += 30; // game-winning shot

        return {
            obj, pocket, aim, power,
            cutAngle, distance: total,
            cueEndX, cueEndY,
            score,
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
            // Small positive margin so the AI doesn't try to thread impossibly tight gaps.
            if (dist < b.r + radius + 1.5) return true;
        }
        return false;
    },

    // ---------- BREAK SHOT ----------
    // Smash the head ball of the rack at near-max power with a small angular offset
    // so the cue strikes it slightly off-centre for a better spread (~1-3 degrees).
    chooseBreakShot(profile) {
        const g = this.game;
        const cue = g.cueBall;
        if (!cue) return null;

        const rack = g.balls.filter(b => !b.potted && b.num !== 0);
        if (rack.length === 0) return null;

        // Apex of the rack = the ball closest to the cue ball (at break, that's the head ball).
        let apex = rack[0];
        let bestDist = Math.hypot(apex.x - cue.x, apex.y - cue.y);
        for (let i = 1; i < rack.length; i++) {
            const d = Math.hypot(rack[i].x - cue.x, rack[i].y - cue.y);
            if (d < bestDist) { bestDist = d; apex = rack[i]; }
        }

        // Direction cue -> apex.
        const baseAim = Math.atan2(apex.y - cue.y, apex.x - cue.x);

        // A perfectly square apex hit was producing only 1 ball that crossed the centre line
        // (need 3 'break points' = potted + crossed). A small offset (~1-1.5 deg) drives much
        // more lateral motion through the rack so balls return past the centre line.
        const offsetMagnitude = (this.config.difficulty === 'hard') ? 0.022   // ~1.3 deg
                                : (this.config.difficulty === 'medium') ? 0.030 // ~1.7 deg
                                : 0.040;                                       // ~2.3 deg
        const side = (Math.random() < 0.5) ? -1 : 1;
        const aim = baseAim + side * offsetMagnitude;

        // Power: the break should be an absolute commit. Use full maxPower so that the player's
        // powerMultiplier (applied in fireShot) gives the same impact as a human pulling the slider
        // all the way back. Easier difficulties hold back slightly to feel more human.
        const powerFrac = (this.config.difficulty === 'hard') ? 1.00
                          : (this.config.difficulty === 'medium') ? 0.95
                          : 0.85;
        const power = g.maxPower * powerFrac;

        return { obj: apex, pocket: null, aim, power, cutAngle: 0, distance: bestDist, score: 200, breakShot: true };
    },

    // ---------- BANK / LEGAL-CONTACT HELPERS ----------
    // Reflect a point across one of the four cushion rails. Returns null if rail name unknown.
    _reflectAcrossRail(p, rail) {
        const g = this.game;
        const m = g.cushionMargin;
        switch (rail) {
            case 'top':    return { x: p.x,                    y: 2 * m - p.y };
            case 'bottom': return { x: p.x,                    y: 2 * (g.height - m) - p.y };
            case 'left':   return { x: 2 * m - p.x,            y: p.y };
            case 'right':  return { x: 2 * (g.width  - m) - p.x, y: p.y };
        }
        return null;
    },

    // Try to find a one-cushion bank from cue to target. Returns {aim, contact, distance} or null.
    tryBankContact(cue, target) {
        const g = this.game;
        const m = g.cushionMargin;
        const rails = ['top', 'bottom', 'left', 'right'];
        let best = null;
        for (const rail of rails) {
            const virtTarget = this._reflectAcrossRail(target, rail);
            if (!virtTarget) continue;

            // Find where the cue->virtTarget line crosses the rail (the contact point).
            const dx = virtTarget.x - cue.x;
            const dy = virtTarget.y - cue.y;
            let t = -1;
            if (rail === 'top')    t = (m - cue.y) / dy;
            if (rail === 'bottom') t = ((g.height - m) - cue.y) / dy;
            if (rail === 'left')   t = (m - cue.x) / dx;
            if (rail === 'right')  t = ((g.width  - m) - cue.x) / dx;
            if (!isFinite(t) || t <= 0.02 || t >= 1) continue;

            const contact = { x: cue.x + dx * t, y: cue.y + dy * t };
            // Contact point must lie on the playable extent of the rail (allow for cue radius so
            // we don't aim into a corner pocket and foul-scratch).
            const railMargin = m + cue.r;
            if (contact.x < railMargin || contact.x > g.width  - railMargin) continue;
            if (contact.y < railMargin || contact.y > g.height - railMargin) continue;

            // Both legs of the path must be clear of other balls.
            if (this.pathBlocked(cue, contact, cue.r, [target])) continue;
            if (this.pathBlocked(contact, target, cue.r, [cue])) continue;

            const aim = Math.atan2(contact.y - cue.y, contact.x - cue.x);
            const dist = Math.hypot(contact.x - cue.x, contact.y - cue.y) +
                         Math.hypot(target.x - contact.x, target.y - contact.y);
            if (!best || dist < best.distance) best = { aim, contact, distance: dist, rail };
        }
        return best;
    },

    // Distance from a point to the nearest opponent ball - bigger = better safety.
    _minDistToBalls(p, balls) {
        let best = Infinity;
        for (const b of balls) {
            const d = Math.hypot(p.x - b.x, p.y - b.y);
            if (d < best) best = d;
        }
        return best;
    },

    // ---------- SAFETY SHOT ----------
    // Goal: make a legal contact with one of our own legal balls and leave the cue ball
    // far from the OPPONENT's legal balls. Prefer direct LOS; if everything is blocked,
    // try a one-cushion bank rather than firing blind into other balls (which would foul).
    chooseSafetyShot() {
        const g = this.game;
        const cue = g.cueBall;
        const targets = this.getLegalTargetBalls();
        if (targets.length === 0) return null;

        // Identify opponent balls so we can score 'safe-zone' candidate end positions.
        const opponentIdx = (g.currentPlayerIndex + 1) % g.players.length;
        const opp = g.players[opponentIdx];
        const oppBalls = g.balls.filter(b => {
            if (b.potted || b.num === 0 || b.num === 8) return false;
            if (opp && opp.color) return b.color === opp.color;
            return true;
        });

        let safeX = g.width / 2, safeY = g.height / 2;
        if (oppBalls.length > 0) {
            let cx = 0, cy = 0;
            for (const b of oppBalls) { cx += b.x; cy += b.y; }
            cx /= oppBalls.length; cy /= oppBalls.length;
            safeX = (cx < g.width / 2) ? g.width  - g.cushionMargin * 2 : g.cushionMargin * 2;
            safeY = (cy < g.height / 2) ? g.height - g.cushionMargin * 2 : g.cushionMargin * 2;
        }

        // Build candidates with clear direct line of sight first.
        const direct = [];
        for (const t of targets) {
            if (!this.pathBlocked(cue, t, cue.r, [t])) {
                direct.push({ target: t, dist: Math.hypot(t.x - cue.x, t.y - cue.y), bank: null });
            }
        }

        let chosen = null;
        if (direct.length > 0) {
            // Pick the closest legal ball with a clear line.
            direct.sort((a, b) => a.dist - b.dist);
            chosen = direct[0];
        } else {
            // No direct contact possible without fouling - try a one-cushion bank.
            let bestBank = null;
            for (const t of targets) {
                const bank = this.tryBankContact(cue, t);
                if (bank && (!bestBank || bank.distance < bestBank.distance)) {
                    bestBank = { target: t, bank };
                }
            }
            if (bestBank) {
                chosen = { target: bestBank.target, dist: bestBank.bank.distance, bank: bestBank.bank };
            }
        }

        if (!chosen) {
            // Absolute last resort: every direct line is blocked and no bank works. Instead of
            // firing at the nearest ball (which often has an OPPONENT ball in front of it -
            // a guaranteed wrong-ball-first foul), score each legal target by how SAFE the
            // contact is. The cheapest path is one where the only blocker is the target itself
            // or one of our own colour. Distance is a tiebreaker.
            const player = g.players[g.currentPlayerIndex];
            const myColor = player ? player.color : null;
            const scoreTap = (t) => {
                const dx = t.x - cue.x, dy = t.y - cue.y;
                const len = Math.hypot(dx, dy) || 1;
                const nx = dx / len, ny = dy / len;
                let badBlockers = 0;
                for (const b of g.balls) {
                    if (b.potted || b === t || b.num === 0) continue;
                    const px = b.x - cue.x, py = b.y - cue.y;
                    const tt = px * nx + py * ny;
                    if (tt <= 0 || tt >= len) continue;
                    const cx = cue.x + nx * tt, cy = cue.y + ny * tt;
                    if (Math.hypot(b.x - cx, b.y - cy) < b.r + cue.r) {
                        // Penalise opponent balls heavily; own colour and the 8-ball lightly.
                        const isMine = myColor && b.color === myColor;
                        const isBlack = b.num === 8;
                        badBlockers += isMine ? 1 : (isBlack ? 5 : 20);
                    }
                }
                return badBlockers * 1000 + len;
            };
            let nearest = targets[0];
            let bestScore = scoreTap(nearest);
            for (let i = 1; i < targets.length; i++) {
                const sc = scoreTap(targets[i]);
                if (sc < bestScore) { bestScore = sc; nearest = targets[i]; }
            }
            const nd = Math.hypot(nearest.x - cue.x, nearest.y - cue.y);
            chosen = { target: nearest, dist: nd, bank: null };
        }

        // Compute aim + power.
        let aim;
        let powerScale;
        if (chosen.bank) {
            // Bank shot: aim at the contact point on the rail. Needs more power because
            // cushions absorb energy.
            aim = chosen.bank.aim;
            powerScale = Math.min(0.85, Math.max(0.55, chosen.bank.distance / 700));
        } else {
            // Direct safety: aim slightly off the contact ball so the cue deflects toward the safe zone.
            // The angular offset MUST stay smaller than the angle subtended by the target ball at the cue
            // (asin((R+r)/dist)), otherwise the cue misses entirely and we foul on 'no ball hit'.
            const toContact = Math.atan2(chosen.target.y - cue.y, chosen.target.x - cue.x);
            const toSafe    = Math.atan2(safeY - chosen.target.y, safeX - chosen.target.x);
            let bias = toSafe - toContact;
            while (bias >  Math.PI) bias -= Math.PI * 2;
            while (bias < -Math.PI) bias += Math.PI * 2;
            const distC = Math.max(1, Math.hypot(chosen.target.x - cue.x, chosen.target.y - cue.y));
            const sumR = chosen.target.r + cue.r;
            const maxOffset = Math.asin(Math.min(0.99, sumR / distC)) * 0.6; // 60% of the contact-cone half-angle
            aim = toContact + Math.sign(bias || 1) * Math.min(maxOffset, Math.abs(bias) * 0.5);
            // Power floor lifted: the cue needs enough velocity that, after losing ~half on impact,
            // it can still reach at least one cushion (cushion-after-contact is a foul rule).
            powerScale = Math.min(0.70, Math.max(0.45, chosen.dist / 900));
        }

        const power = g.maxPower * powerScale;
        return {
            obj: chosen.target, pocket: null, aim, power,
            cutAngle: 0, distance: chosen.dist,
            score: 0, safety: true, bank: !!chosen.bank,
        };
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
        btn.style.cssText = 'position:fixed;top:60px;left:10px;width:150px;padding:10px 14px;background:rgba(75,85,99,0.95);color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:9998;font-size:13px;text-align:center;box-shadow:0 4px 12px rgba(0,0,0,0.3);transition:all .2s;';
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
                el.style.cssText = 'position:fixed;top:60px;left:170px;padding:6px 12px;background:rgba(16,185,129,0.9);color:white;border-radius:6px;font-size:12px;font-weight:bold;z-index:9998;box-shadow:0 2px 8px rgba(0,0,0,0.3);';
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
