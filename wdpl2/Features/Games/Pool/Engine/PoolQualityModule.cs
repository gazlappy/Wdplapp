namespace Wdpl2.Services;

/// <summary>
/// Quality preset module for the pool game.
/// Toggles expensive rendering / effects subsystems so the game can run smoothly
/// on lower-end devices. Presets: low, medium, high, ultra.
/// </summary>
public static class PoolQualityModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL QUALITY MODULE
// Performance presets for lower-end devices.
// Other modules read from PoolQuality.config to
// decide whether to spawn/draw expensive things.
// ============================================

const PoolQuality = {
    game: null,

    // Live config -- read by physics, rendering and game module.
    // Defaults match HIGH preset. Other modules can short-circuit
    // when a flag is false.
    config: {
        preset: 'high',
        vfx: true,                  // particles, flashes, cushion compressions
        trails: true,               // moving-ball trails
        ballRotation: true,         // 3D number rotation on balls
        trajectory: true,           // aim-time path prediction
        trajectorySegments: 15,
        trajectoryLength: 200,
        spinArrows: true,
        ballInHandPulse: true,      // per-frame sin() pulse around cue ball when in hand
        renderEveryNthFrame: 1,     // 1 = every frame, 2 = every other (~30fps), 3 = every 3rd (~20fps)
        maxSubSteps: 12,            // cap collision sub-steps per frame
    },

    presets: {
        low: {
            preset: 'low',
            vfx: false,
            trails: false,
            ballRotation: false,
            trajectory: false,
            trajectorySegments: 4,
            trajectoryLength: 120,
            spinArrows: false,
            ballInHandPulse: false,
            renderEveryNthFrame: 2,  // ~30fps render (physics still 60fps)
            maxSubSteps: 6,
        },
        medium: {
            preset: 'medium',
            vfx: false,
            trails: true,
            ballRotation: true,
            trajectory: true,
            trajectorySegments: 8,
            trajectoryLength: 160,
            spinArrows: true,
            ballInHandPulse: true,
            renderEveryNthFrame: 1,
            maxSubSteps: 8,
        },
        high: {
            preset: 'high',
            vfx: true,
            trails: true,
            ballRotation: true,
            trajectory: true,
            trajectorySegments: 15,
            trajectoryLength: 200,
            spinArrows: true,
            ballInHandPulse: true,
            renderEveryNthFrame: 1,
            maxSubSteps: 12,
        },
        ultra: {
            preset: 'ultra',
            vfx: true,
            trails: true,
            ballRotation: true,
            trajectory: true,
            trajectorySegments: 25,
            trajectoryLength: 300,
            spinArrows: true,
            ballInHandPulse: true,
            renderEveryNthFrame: 1,
            maxSubSteps: 20,
        },
    },

    init(game) {
        this.game = game;
        // Restore previously saved preset (if any)
        let saved = null;
        try { saved = localStorage.getItem('poolQualityPreset'); } catch (e) { /* ignore */ }
        const initial = (saved && this.presets[saved]) ? saved : this.detectDefaultPreset();
        this.set(initial);
        this.createToggleButton();
        console.log('[Quality] PoolQuality initialized with preset:', initial);
    },

    // Heuristic: pick a default based on device pixel ratio + cores.
    // Touch-only devices with low core count get MEDIUM by default.
    detectDefaultPreset() {
        try {
            const cores = navigator.hardwareConcurrency || 4;
            const isTouchOnly = (navigator.maxTouchPoints || 0) > 0 && !window.matchMedia('(hover: hover)').matches;
            if (cores <= 2) return 'low';
            if (cores <= 4 || isTouchOnly) return 'medium';
            return 'high';
        } catch (e) {
            return 'high';
        }
    },

    set(presetName) {
        const preset = this.presets[presetName];
        if (!preset) {
            console.warn('[Quality] Unknown preset:', presetName);
            return;
        }
        // Replace config in place so other modules' references stay valid
        Object.keys(preset).forEach(k => { this.config[k] = preset[k]; });

        // Mirror flags onto the game object so existing per-game settings stay in sync
        if (this.game) {
            this.game.showTrajectoryPrediction = !!preset.trajectory;
            this.game.trajectoryLength = preset.trajectoryLength;
            this.game.trajectorySegments = preset.trajectorySegments;
            this.game.showSpinArrows = !!preset.spinArrows;
        }

        try { localStorage.setItem('poolQualityPreset', presetName); } catch (e) { /* ignore */ }
        this.updateToggleButton();
        console.log('[Quality] Preset set to:', presetName, this.config);
    },

    cycle() {
        const order = ['low', 'medium', 'high', 'ultra'];
        const idx = order.indexOf(this.config.preset);
        this.set(order[(idx + 1) % order.length]);
    },

    // Convenience for other modules
    isVfxEnabled()        { return this.config.vfx !== false; },
    isTrailsEnabled()     { return this.config.trails !== false; },
    isRotationEnabled()   { return this.config.ballRotation !== false; },
    isTrajectoryEnabled() { return this.config.trajectory !== false; },

    // Determines if THIS frame should render (used by animate() to skip frames at LOW)
    shouldRenderThisFrame(frameCount) {
        const n = this.config.renderEveryNthFrame || 1;
        return n <= 1 || (frameCount % n) === 0;
    },

    // ---------- UI ----------
    createToggleButton() {
        if (document.getElementById('qualityToggleBtn')) return;
        const btn = document.createElement('button');
        btn.id = 'qualityToggleBtn';
        btn.style.cssText = 'position:fixed;top:110px;left:10px;width:150px;padding:10px 14px;background:rgba(75,85,99,0.95);color:white;border:none;border-radius:8px;font-weight:bold;cursor:pointer;z-index:9998;font-size:13px;text-align:center;box-shadow:0 4px 12px rgba(0,0,0,0.3);transition:all .2s;';
        btn.title = 'Click to cycle quality: LOW / MEDIUM / HIGH / ULTRA. Lower it if the game feels slow.';
        btn.addEventListener('click', () => this.cycle());
        document.body.appendChild(btn);
        this.updateToggleButton();
    },

    updateToggleButton() {
        const btn = document.getElementById('qualityToggleBtn');
        if (!btn) return;
        const colors = {
            low:    'linear-gradient(135deg,#ef4444,#b91c1c)',
            medium: 'linear-gradient(135deg,#f59e0b,#b45309)',
            high:   'linear-gradient(135deg,#10b981,#047857)',
            ultra:  'linear-gradient(135deg,#8b5cf6,#6d28d9)',
        };
        btn.style.background = colors[this.config.preset] || 'rgba(75,85,99,0.95)';
        btn.textContent = 'QUALITY: ' + (this.config.preset || 'high').toUpperCase();
    },
};
";
    }
}
