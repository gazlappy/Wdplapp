namespace Wdpl2.Services;

/// <summary>
/// Shot recorder + trace overlay for diagnosing trajectory issues.
/// Captures per-frame ball positions plus key events for every shot,
/// renders the paths as overlay lines, and exports JSON for offline analysis.
/// </summary>
public static class PoolReplayModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL REPLAY / TRACE RECORDER MODULE
// Records every shot's full ball trajectory + events
// for visual review on the table and JSON export.
// ============================================

const PoolReplay = {
    game: null,
    config: {
        recording: true,        // capture new shots
        sampleEvery: 1,         // capture every Nth physics frame (1=every frame, 2=half, etc.)
        maxShots: 100,          // cap stored history (oldest dropped)
        traceMode: 'latest',    // 'hidden' | 'all' | 'latest'
        showEvents: true,       // draw markers for cushion/collision/pot events
    },
    shots: [],
    _current: null,
    _frameCounter: 0,
    _selectedShotId: null,

    init(game) {
        this.game = game;
        this.createToggleButtons();
        console.log('[Replay] PoolReplay initialized. Use the REC and TRACES buttons.');
    },

    // ---------- RECORDING ----------
    beginRecord() {
        if (!this.config.recording || !this.game) return;
        const cue = this.game.cueBall;
        if (!cue) return;

        this._current = {
            id: (this.shots.length > 0 ? this.shots[this.shots.length - 1].id + 1 : 1),
            timestamp: Date.now(),
            player: this.game.getCurrentPlayer ? this.game.getCurrentPlayer().name : 'Unknown',
            playerIndex: this.game.currentPlayerIndex,
            phase: this.game.gamePhase,
            initial: {
                cueBall: { x: cue.x, y: cue.y },
                aim: this.game.aimAngle,
                aimDeg: (this.game.aimAngle * 180 / Math.PI),
                power: Math.hypot(cue.vx || 0, cue.vy || 0),
                spinX: cue.spinX || 0,
                spinY: cue.spinY || 0,
                ballPositions: this.game.balls
                    .filter(b => !b.potted)
                    .map(b => ({ num: b.num, color: b.color, x: b.x, y: b.y })),
            },
            // One entry per ball that was on the table at start of shot.
            paths: this.game.balls.filter(b => !b.potted).map(b => ({
                num: b.num,
                color: b.color,
                frames: [{ x: b.x, y: b.y }],
            })),
            events: [],
            result: null,
            frameCount: 0,
        };
        this._frameCounter = 0;
    },

    captureFrame() {
        if (!this._current) return;
        this._frameCounter++;
        if ((this._frameCounter % this.config.sampleEvery) !== 0) return;

        // Append current position for each tracked ball
        const byNum = {};
        for (const path of this._current.paths) byNum[path.num] = path;

        for (const b of this.game.balls) {
            const path = byNum[b.num];
            if (!path) continue;
            if (b.potted) {
                // Lock to last known pre-pot position (already handled when pot event logged)
                continue;
            }
            path.frames.push({ x: b.x, y: b.y });
        }
        this._current.frameCount = this._frameCounter;
    },

    logEvent(type, data) {
        if (!this._current) return;
        this._current.events.push(Object.assign(
            { frame: this._frameCounter, type: type },
            data || {}
        ));
    },

    endRecord() {
        if (!this._current) return;
        const c = this._current;
        // Capture result
        c.result = {
            firstBallHit: this.game.firstBallHit ? {
                num: this.game.firstBallHit.num,
                color: this.game.firstBallHit.color,
            } : null,
            pottedBalls: (this.game.ballsPottedThisShot || []).map(b => ({ num: b.num, color: b.color })),
            cueBallPotted: !!this.game.cueBallPotted,
            foul: !!this.game.foulCommitted,
            foulReason: this.game.foulReason || '',
            cushionHitAfterContact: !!this.game.cushionHitAfterContact,
            durationFrames: c.frameCount,
        };

        this.shots.push(c);
        if (this.shots.length > this.config.maxShots) this.shots.shift();
        this._selectedShotId = c.id;

        console.log('[Replay] Shot #' + c.id + ' recorded:',
            'frames=' + c.frameCount,
            'events=' + c.events.length,
            'potted=' + c.result.pottedBalls.length,
            (c.result.foul ? 'FOUL: ' + c.result.foulReason : ''));

        this._current = null;
        this.updateToggleButtons();
    },

    // ---------- RENDERING ----------
    drawTraces(ctx) {
        if (this.config.traceMode === 'hidden' || this.shots.length === 0) return;
        const shotsToDraw = this.config.traceMode === 'latest'
            ? [this.shots[this.shots.length - 1]]
            : this.shots;

        ctx.save();
        // Older shots fade; latest is brightest
        const total = shotsToDraw.length;
        for (let s = 0; s < total; s++) {
            const shot = shotsToDraw[s];
            const ageFactor = total === 1 ? 1 : (0.25 + 0.75 * (s / (total - 1)));

            for (const path of shot.paths) {
                if (!path.frames || path.frames.length < 2) continue;
                const isCue = (path.num === 0);
                const baseColor = this._traceColor(path.color, path.num);

                ctx.strokeStyle = baseColor.replace('ALPHA', (0.35 * ageFactor).toFixed(2));
                ctx.lineWidth = isCue ? 2.0 : 1.4;
                ctx.setLineDash(isCue ? [] : [4, 3]);
                ctx.beginPath();
                ctx.moveTo(path.frames[0].x, path.frames[0].y);
                for (let i = 1; i < path.frames.length; i++) {
                    ctx.lineTo(path.frames[i].x, path.frames[i].y);
                }
                ctx.stroke();

                // Start marker (small filled dot at first position)
                ctx.fillStyle = baseColor.replace('ALPHA', (0.65 * ageFactor).toFixed(2));
                ctx.beginPath();
                ctx.arc(path.frames[0].x, path.frames[0].y, isCue ? 4 : 2.5, 0, Math.PI * 2);
                ctx.fill();
            }
            ctx.setLineDash([]);

            // Event markers (cushion + collision + pot)
            if (this.config.showEvents) {
                for (const ev of shot.events) {
                    this._drawEventMarker(ctx, shot, ev, ageFactor);
                }
            }
        }
        ctx.restore();
    },

    _traceColor(color, num) {
        if (num === 0) return 'rgba(255,255,255,ALPHA)';
        if (num === 8) return 'rgba(40,40,40,ALPHA)';
        if (color === 'red') return 'rgba(220,38,38,ALPHA)';
        if (color === 'yellow') return 'rgba(234,179,8,ALPHA)';
        return 'rgba(180,180,180,ALPHA)';
    },

    _drawEventMarker(ctx, shot, ev, ageFactor) {
        // Find ball position at the event's frame
        let pos = null;
        if (ev.ballNum !== undefined) {
            const path = shot.paths.find(p => p.num === ev.ballNum);
            if (path && path.frames.length > 0) {
                const idx = Math.min(Math.floor(ev.frame / this.config.sampleEvery), path.frames.length - 1);
                pos = path.frames[idx];
            }
        } else if (ev.balls && ev.balls.length === 2) {
            const p1 = shot.paths.find(p => p.num === ev.balls[0]);
            const p2 = shot.paths.find(p => p.num === ev.balls[1]);
            if (p1 && p2 && p1.frames.length > 0 && p2.frames.length > 0) {
                const i1 = Math.min(Math.floor(ev.frame / this.config.sampleEvery), p1.frames.length - 1);
                const i2 = Math.min(Math.floor(ev.frame / this.config.sampleEvery), p2.frames.length - 1);
                pos = { x: (p1.frames[i1].x + p2.frames[i2].x) / 2,
                        y: (p1.frames[i1].y + p2.frames[i2].y) / 2 };
            }
        }
        if (!pos) return;

        ctx.save();
        const a = (0.7 * ageFactor).toFixed(2);
        if (ev.type === 'cushion') {
            ctx.strokeStyle = 'rgba(255,200,0,' + a + ')';
            ctx.lineWidth = 1.2;
            ctx.beginPath();
            ctx.arc(pos.x, pos.y, 5, 0, Math.PI * 2);
            ctx.stroke();
        } else if (ev.type === 'collision') {
            ctx.fillStyle = 'rgba(255,80,80,' + a + ')';
            ctx.beginPath();
            ctx.arc(pos.x, pos.y, 4, 0, Math.PI * 2);
            ctx.fill();
        } else if (ev.type === 'pot') {
            ctx.fillStyle = 'rgba(0,255,128,' + a + ')';
            ctx.beginPath();
            ctx.arc(pos.x, pos.y, 6, 0, Math.PI * 2);
            ctx.fill();
            ctx.strokeStyle = 'rgba(0,255,128,' + (parseFloat(a) + 0.2).toFixed(2) + ')';
            ctx.lineWidth = 2;
            ctx.stroke();
        } else if (ev.type === 'foul') {
            ctx.fillStyle = 'rgba(255,0,0,' + a + ')';
            ctx.font = 'bold 14px Arial';
            ctx.fillText('FOUL', pos.x + 6, pos.y - 6);
        } else if (ev.type === 'firstHit') {
            ctx.strokeStyle = 'rgba(100,200,255,' + a + ')';
            ctx.lineWidth = 1.5;
            ctx.setLineDash([2,2]);
            ctx.beginPath();
            ctx.arc(pos.x, pos.y, 8, 0, Math.PI * 2);
            ctx.stroke();
            ctx.setLineDash([]);
        }
        ctx.restore();
    },

    // ---------- API ----------
    setRecording(on) {
        this.config.recording = !!on;
        this.updateToggleButtons();
    },

    cycleTraceMode() {
        const order = ['hidden', 'latest', 'all'];
        const idx = order.indexOf(this.config.traceMode);
        this.config.traceMode = order[(idx + 1) % order.length];
        this.updateToggleButtons();
    },

    show(id) {
        // Highlight a single shot by id (sets traceMode to 'latest' and reorders)
        const shot = this.shots.find(s => s.id === id);
        if (!shot) { console.warn('[Replay] No shot with id', id); return; }
        // Move to end so 'latest' picks it
        this.shots = this.shots.filter(s => s.id !== id).concat([shot]);
        this.config.traceMode = 'latest';
        this.updateToggleButtons();
    },

    summary() {
        return this.shots.map(s => ({
            id: s.id,
            player: s.player,
            phase: s.phase,
            aimDeg: +s.initial.aimDeg.toFixed(1),
            power: +s.initial.power.toFixed(2),
            firstHit: s.result && s.result.firstBallHit ? s.result.firstBallHit.color + s.result.firstBallHit.num : '-',
            potted: s.result ? s.result.pottedBalls.map(b => b.color + b.num).join(',') || '-' : '-',
            foul: s.result ? s.result.foul : false,
            frames: s.frameCount,
            events: s.events.length,
        }));
    },

    export() {
        const json = JSON.stringify(this.shots, null, 2);
        const sizeKb = (json.length / 1024).toFixed(1);
        if (navigator.clipboard && navigator.clipboard.writeText) {
            navigator.clipboard.writeText(json).then(
                () => {
                    console.log('[Replay] Exported ' + this.shots.length + ' shots to clipboard (' + sizeKb + ' KB).');
                    this._showToast('Exported ' + this.shots.length + ' shot' + (this.shots.length === 1 ? '' : 's') + ' (' + sizeKb + ' KB) to clipboard', 'success');
                },
                (err) => {
                    console.warn('[Replay] Clipboard failed:', err);
                    console.log(json);
                    this._showToast('Clipboard blocked - JSON dumped to console', 'warn');
                }
            );
        } else {
            console.log('[Replay] JSON dump (' + sizeKb + ' KB):\n' + json);
            this._showToast('JSON dumped to console (' + sizeKb + ' KB)', 'warn');
        }
        return json;
    },

    _showToast(message, kind) {
        let toast = document.getElementById('replayToast');
        if (!toast) {
            toast = document.createElement('div');
            toast.id = 'replayToast';
            toast.style.cssText = 'position:fixed;top:55px;left:50%;transform:translateX(-50%);padding:10px 20px;color:white;border-radius:8px;font-weight:bold;font-size:13px;z-index:20000;box-shadow:0 4px 14px rgba(0,0,0,0.4);transition:opacity .25s;pointer-events:none;';
            document.body.appendChild(toast);
        }
        const palette = {
            success: 'linear-gradient(135deg,#10b981,#047857)',
            warn:    'linear-gradient(135deg,#f59e0b,#b45309)',
            error:   'linear-gradient(135deg,#ef4444,#991b1b)',
        };
        toast.style.background = palette[kind] || palette.success;
        toast.textContent = message;
        toast.style.opacity = '1';
        clearTimeout(this._toastTimer);
        this._toastTimer = setTimeout(() => { if (toast) toast.style.opacity = '0'; }, 2400);
    },

    clear() {
        const n = this.shots.length;
        this.shots = [];
        this._selectedShotId = null;
        this.updateToggleButtons();
        console.log('[Replay] Cleared ' + n + ' shot' + (n === 1 ? '' : 's') + ' from history.');
        this._showToast('Cleared ' + n + ' shot' + (n === 1 ? '' : 's'), 'warn');
    },

    // ---------- UI ----------
    createToggleButtons() {
        if (document.getElementById('replayRecBtn')) return;

        const recBtn = document.createElement('button');
        recBtn.id = 'replayRecBtn';
        recBtn.style.cssText = 'position:fixed;top:10px;left:380px;padding:10px 14px;color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:9998;font-size:12px;box-shadow:0 4px 12px rgba(0,0,0,0.3);transition:all .2s;';
        recBtn.title = 'Toggle shot recording on/off.';
        recBtn.addEventListener('click', () => this.setRecording(!this.config.recording));
        document.body.appendChild(recBtn);

        const traceBtn = document.createElement('button');
        traceBtn.id = 'replayTraceBtn';
        traceBtn.style.cssText = 'position:fixed;top:10px;left:475px;padding:10px 14px;color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:9998;font-size:12px;box-shadow:0 4px 12px rgba(0,0,0,0.3);transition:all .2s;';
        traceBtn.title = 'Cycle trace overlay: HIDDEN / LATEST / ALL\nRight-click: console.table the summary';
        traceBtn.addEventListener('click', () => this.cycleTraceMode());
        traceBtn.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            console.table(this.summary());
            this._showToast('Summary printed to console', 'success');
        });
        document.body.appendChild(traceBtn);

        // EXPORT: click to copy JSON to clipboard. Right-click to clear all shots.
        const exportBtn = document.createElement('button');
        exportBtn.id = 'replayExportBtn';
        exportBtn.style.cssText = 'position:fixed;top:10px;left:585px;padding:10px 14px;color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:9998;font-size:12px;box-shadow:0 4px 12px rgba(0,0,0,0.3);transition:all .2s;background:linear-gradient(135deg,#0d9488,#0f766e);';
        exportBtn.title = 'Click: copy recorded SHOT TRACES (ball paths) as JSON to clipboard.\nRight-click: clear shot history.\n\nNote: this is for replay traces only. For dev SETTINGS export, open F2 -> Actions -> Export.';
        exportBtn.addEventListener('click', () => {
            if (this.shots.length === 0) {
                this._showToast('Nothing to export -- no shots recorded yet', 'warn');
                return;
            }
            this.export();
        });
        exportBtn.addEventListener('contextmenu', (e) => {
            e.preventDefault();
            if (this.shots.length === 0) {
                this._showToast('Already empty', 'warn');
                return;
            }
            if (window.confirm('Clear ' + this.shots.length + ' recorded shot' + (this.shots.length === 1 ? '' : 's') + '?')) {
                this.clear();
            }
        });
        document.body.appendChild(exportBtn);

        this.updateToggleButtons();
    },

    updateToggleButtons() {
        const recBtn = document.getElementById('replayRecBtn');
        if (recBtn) {
            const on = this.config.recording;
            recBtn.style.background = on ? 'linear-gradient(135deg,#dc2626,#991b1b)' : 'rgba(75,85,99,0.95)';
            recBtn.textContent = (on ? '\u25CF REC' : 'REC OFF') + ' (' + this.shots.length + ')';
        }
        const traceBtn = document.getElementById('replayTraceBtn');
        if (traceBtn) {
            const map = {
                hidden:  { label: 'TRACES OFF', bg: 'rgba(75,85,99,0.95)' },
                latest:  { label: 'TRACE LATEST', bg: 'linear-gradient(135deg,#0ea5e9,#0369a1)' },
                all:     { label: 'TRACE ALL',    bg: 'linear-gradient(135deg,#8b5cf6,#6d28d9)' },
            };
            const m = map[this.config.traceMode] || map.hidden;
            traceBtn.style.background = m.bg;
            traceBtn.textContent = m.label;
        }
        const exportBtn = document.getElementById('replayExportBtn');
        if (exportBtn) {
            const has = this.shots.length > 0;
            exportBtn.style.background = has
                ? 'linear-gradient(135deg,#0d9488,#0f766e)'
                : 'rgba(75,85,99,0.95)';
            exportBtn.textContent = 'EXPORT REC (' + this.shots.length + ')';
            exportBtn.style.opacity = has ? '1' : '0.7';
        }
    },
};
";
    }
}
