namespace Wdpl2.Services;

/// <summary>
/// Audio module for pool game - handles realistic sound effects
/// </summary>
public static class PoolAudioModule
{
    public static string GenerateJavaScript()
    {
        return @"
// ============================================
// POOL AUDIO MODULE
// Realistic sound effects system
// ============================================

const PoolAudio = {
    context: null,
    sounds: {},
    initialized: false,
    enabled: false,
    volume: 0.5,
    userInteracted: false,
    
    /**
     * Initialize the Web Audio API
     */
    init() {
        try {
            // Create audio context (webkit prefix for Safari)
            this.context = new (window.AudioContext || window.webkitAudioContext)();
            console.log('? Audio context initialized');
            console.log('   State:', this.context.state);
            console.log('   Sample Rate:', this.context.sampleRate);
            this.generateSounds();
            this.initialized = true;
            
            // Setup user interaction listener to unlock audio
            this.setupUserInteraction();
            
            // Try to start context immediately (works in MAUI WebView)
            if (this.context.state === 'suspended') {
                this.context.resume().then(() => {
                    console.log('?? Audio context auto-resumed (MAUI WebView)');
                }).catch(e => {
                    console.log('?? Auto-resume failed, waiting for user interaction');
                });
            }
        } catch (e) {
            console.error('?? Web Audio API initialization failed:', e);
            this.initialized = false;
        }
    },
    
    /**
     * Setup user interaction listener (required for browser autoplay policy)
     */
    setupUserInteraction() {
        const unlockAudio = async () => {
            if (this.userInteracted) return;
            
            try {
                console.log('?? User interaction detected, unlocking audio...');
                
                // Resume audio context on first user interaction
                if (this.context.state === 'suspended') {
                    await this.context.resume();
                    console.log('   Audio context resumed:', this.context.state);
                }
                
                this.userInteracted = true;
                console.log('?? Audio fully unlocked! Sounds will now play.');
                
                // Play a silent test sound to fully unlock audio
                try {
                    const testOsc = this.context.createOscillator();
                    const testGain = this.context.createGain();
                    testGain.gain.value = 0.001; // Nearly silent
                    testOsc.connect(testGain);
                    testGain.connect(this.context.destination);
                    testOsc.start();
                    testOsc.stop(this.context.currentTime + 0.001);
                    console.log('   Silent test sound played');
                } catch (testError) {
                    console.warn('   Test sound failed:', testError);
                }
                
                // Remove listeners after first interaction
                document.removeEventListener('click', unlockAudio);
                document.removeEventListener('touchstart', unlockAudio);
                document.removeEventListener('touchend', unlockAudio);
                document.removeEventListener('mousedown', unlockAudio);
                document.removeEventListener('keydown', unlockAudio);
                
                // Dispatch custom event to notify audio is ready
                window.dispatchEvent(new Event('audioUnlocked'));
            } catch (e) {
                console.error('? Audio unlock failed:', e);
            }
        };
        
        // Listen for various user interactions (more aggressive for mobile)
        document.addEventListener('click', unlockAudio, { once: false });
        document.addEventListener('touchstart', unlockAudio, { once: false, passive: true });
        document.addEventListener('touchend', unlockAudio, { once: false, passive: true });
        document.addEventListener('mousedown', unlockAudio, { once: false });
        document.addEventListener('keydown', unlockAudio, { once: false });
        
        console.log('?? Audio ready. Touch or click anywhere to enable sounds.');
        
        // For MAUI WebView, try to unlock on page load after a short delay
        setTimeout(() => {
            if (!this.userInteracted && this.context.state === 'suspended') {
                console.log('?? Attempting auto-unlock for MAUI WebView...');
                this.context.resume().then(() => {
                    this.userInteracted = true;
                    console.log('?? Audio auto-unlocked in MAUI WebView!');
                }).catch(e => {
                    console.log('?? Auto-unlock failed, user interaction required');
                });
            }
        }, 500);
    },
    
    /**
     * Generate procedural sounds using Web Audio API
     * This avoids needing external sound files
     */
    generateSounds() {
        // These are procedural sounds - no files needed!
        this.sounds = {
            ballCollision: this.createBallCollisionSound.bind(this),
            cushionBounce: this.createCushionBounceSound.bind(this),
            pocket: this.createPocketSound.bind(this),
            cueHit: this.createCueHitSound.bind(this),
            ballRoll: this.createBallRollSound.bind(this)
        };
    },
    
    /**
     * Play a sound with velocity-based volume
     */
    play(soundName, velocity = 1.0) {
        if (!this.enabled || !this.initialized) {
            console.log('?? Audio disabled or not initialized');
            return;
        }
        
        if (!this.userInteracted) {
            console.log('?? Waiting for user interaction to play sound');
            return;
        }
        
        try {
            // Resume context if suspended (browser autoplay policy)
            if (this.context.state === 'suspended') {
                this.context.resume().then(() => {
                    console.log('Audio context resumed');
                });
            }
            
            const soundGenerator = this.sounds[soundName];
            if (soundGenerator) {
                soundGenerator(velocity);
                console.log(`?? Playing: ${soundName} (velocity: ${velocity.toFixed(2)})`);
            } else {
                console.warn(`Unknown sound: ${soundName}`);
            }
        } catch (e) {
            console.error('Audio playback error:', e);
        }
    },
    
    /**
     * Create ball-to-ball collision sound
     * Realistic click sound based on impact velocity
     */
    createBallCollisionSound(velocity) {
        const now = this.context.currentTime;
        
        // Create oscillator for initial click
        const osc = this.context.createOscillator();
        const gain = this.context.createGain();
        
        // High frequency click (simulates hard surface impact)
        osc.type = 'sine';
        osc.frequency.setValueAtTime(800 + velocity * 1200, now); // Higher pitch for harder hits
        osc.frequency.exponentialRampToValueAtTime(200, now + 0.02);
        
        // Volume envelope - quick attack, fast decay
        const vol = Math.min(velocity * 0.3, 1.0) * this.volume;
        gain.gain.setValueAtTime(vol, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.05);
        
        // Add slight noise burst for realism
        const noise = this.createNoiseNode();
        const noiseGain = this.context.createGain();
        noiseGain.gain.setValueAtTime(vol * 0.2, now);
        noiseGain.gain.exponentialRampToValueAtTime(0.001, now + 0.02);
        
        // Connect and play
        osc.connect(gain);
        noise.connect(noiseGain);
        gain.connect(this.context.destination);
        noiseGain.connect(this.context.destination);
        
        osc.start(now);
        osc.stop(now + 0.05);
        noise.start(now);
        noise.stop(now + 0.02);
    },
    
    /**
     * Create cushion bounce sound
     * Lower, thudding sound with slight rubber bounce
     */
    createCushionBounceSound(velocity) {
        const now = this.context.currentTime;
        
        // Lower frequency thud
        const osc = this.context.createOscillator();
        const gain = this.context.createGain();
        
        osc.type = 'triangle';
        osc.frequency.setValueAtTime(180 + velocity * 120, now);
        osc.frequency.exponentialRampToValueAtTime(80, now + 0.08);
        
        const vol = Math.min(velocity * 0.25, 0.8) * this.volume;
        gain.gain.setValueAtTime(vol, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.1);
        
        // Add rubber-bounce harmonics
        const osc2 = this.context.createOscillator();
        const gain2 = this.context.createGain();
        osc2.type = 'sine';
        osc2.frequency.setValueAtTime(240, now);
        gain2.gain.setValueAtTime(vol * 0.3, now);
        gain2.gain.exponentialRampToValueAtTime(0.001, now + 0.06);
        
        osc.connect(gain);
        osc2.connect(gain2);
        gain.connect(this.context.destination);
        gain2.connect(this.context.destination);
        
        osc.start(now);
        osc.stop(now + 0.1);
        osc2.start(now);
        osc2.stop(now + 0.06);
    },
    
    /**
     * Create pocket drop sound
     * Satisfying clunk as ball drops into pocket
     */
    createPocketSound(velocity) {
        const now = this.context.currentTime;
        
        // Deep clunk
        const osc = this.context.createOscillator();
        const gain = this.context.createGain();
        
        osc.type = 'sine';
        osc.frequency.setValueAtTime(220, now);
        osc.frequency.exponentialRampToValueAtTime(80, now + 0.15);
        
        const vol = 0.4 * this.volume;
        gain.gain.setValueAtTime(vol, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.2);
        
        // Add rattle/roll sound as ball settles
        const noise = this.createNoiseNode();
        const noiseGain = this.context.createGain();
        const filter = this.context.createBiquadFilter();
        filter.type = 'bandpass';
        filter.frequency.value = 400;
        filter.Q.value = 2;
        
        noiseGain.gain.setValueAtTime(0, now + 0.05);
        noiseGain.gain.linearRampToValueAtTime(vol * 0.15, now + 0.1);
        noiseGain.gain.exponentialRampToValueAtTime(0.001, now + 0.4);
        
        osc.connect(gain);
        noise.connect(filter);
        filter.connect(noiseGain);
        gain.connect(this.context.destination);
        noiseGain.connect(this.context.destination);
        
        osc.start(now);
        osc.stop(now + 0.2);
        noise.start(now + 0.05);
        noise.stop(now + 0.4);
    },
    
    /**
     * Create cue stick hitting ball sound
     * Sharp crack/tap sound
     */
    createCueHitSound(power) {
        const now = this.context.currentTime;
        
        // Sharp attack with high frequency
        const osc = this.context.createOscillator();
        const gain = this.context.createGain();
        
        osc.type = 'sine';
        const freq = 600 + power * 400;
        osc.frequency.setValueAtTime(freq, now);
        osc.frequency.exponentialRampToValueAtTime(200, now + 0.03);
        
        const vol = Math.min(power * 0.4, 1.0) * this.volume;
        gain.gain.setValueAtTime(vol, now);
        gain.gain.exponentialRampToValueAtTime(0.001, now + 0.06);
        
        // Add crisp attack noise
        const noise = this.createNoiseNode();
        const noiseGain = this.context.createGain();
        const filter = this.context.createBiquadFilter();
        filter.type = 'highpass';
        filter.frequency.value = 2000;
        
        noiseGain.gain.setValueAtTime(vol * 0.3, now);
        noiseGain.gain.exponentialRampToValueAtTime(0.001, now + 0.02);
        
        osc.connect(gain);
        noise.connect(filter);
        filter.connect(noiseGain);
        gain.connect(this.context.destination);
        noiseGain.connect(this.context.destination);
        
        osc.start(now);
        osc.stop(now + 0.06);
        noise.start(now);
        noise.stop(now + 0.02);
    },
    
    /**
     * Create ball rolling sound (looped)
     * Subtle rumble for moving balls
     */
    createBallRollSound(speed) {
        const now = this.context.currentTime;
        
        // Low frequency rumble
        const noise = this.createNoiseNode();
        const gain = this.context.createGain();
        const filter = this.context.createBiquadFilter();
        
        filter.type = 'bandpass';
        filter.frequency.value = 100 + speed * 50;
        filter.Q.value = 3;
        
        const vol = Math.min(speed * 0.08, 0.15) * this.volume;
        gain.gain.setValueAtTime(vol, now);
        gain.gain.linearRampToValueAtTime(0, now + 0.1);
        
        noise.connect(filter);
        filter.connect(gain);
        gain.connect(this.context.destination);
        
        noise.start(now);
        noise.stop(now + 0.1);
    },
    
    /**
     * Create white noise node
     */
    createNoiseNode() {
        const bufferSize = this.context.sampleRate * 0.1; // 100ms buffer
        const buffer = this.context.createBuffer(1, bufferSize, this.context.sampleRate);
        const data = buffer.getChannelData(0);
        
        // Fill with random values
        for (let i = 0; i < bufferSize; i++) {
            data[i] = Math.random() * 2 - 1;
        }
        
        const noise = this.context.createBufferSource();
        noise.buffer = buffer;
        return noise;
    },
    
    /**
     * Set master volume
     */
    setVolume(vol) {
        this.volume = Math.max(0, Math.min(1, vol));
    },
    
    /**
     * Enable/disable sounds
     */
    setEnabled(enabled) {
        this.enabled = enabled;
        if (enabled && !this.initialized) {
            this.init();
        }
    }
};

console.log('PoolAudio module loaded');
";
    }
}
