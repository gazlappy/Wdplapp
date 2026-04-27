namespace wdpl2.Tests.Features.Games.Pool.Engine;

using Wdpl2.Services;
using Xunit;

public class PoolAudioModuleTests
{
    [Fact]
    public void GenerateJavaScript_WhenCalled_ReturnsNonEmptyString()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.NotNull(result);
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsPoolAudioObjectDeclaration()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("const PoolAudio = {", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsInitFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("init() {", result);
        Assert.Contains("this.context = new (window.AudioContext || window.webkitAudioContext)()", result);
        Assert.Contains("this.generateSounds();", result);
        Assert.Contains("this.initialized = true;", result);
        Assert.Contains("this.setupUserInteraction();", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsSetupUserInteractionFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("setupUserInteraction() {", result);
        Assert.Contains("const unlockAudio = async () => {", result);
        Assert.Contains("if (this.userInteracted) return;", result);
        Assert.Contains("this.userInteracted = true;", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsGenerateSoundsFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("generateSounds() {", result);
        Assert.Contains("this.sounds = {", result);
        Assert.Contains("ballCollision: this.createBallCollisionSound.bind(this)", result);
        Assert.Contains("cushionBounce: this.createCushionBounceSound.bind(this)", result);
        Assert.Contains("pocket: this.createPocketSound.bind(this)", result);
        Assert.Contains("cueHit: this.createCueHitSound.bind(this)", result);
        Assert.Contains("ballRoll: this.createBallRollSound.bind(this)", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsPlayFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("play(soundName, velocity = 1.0) {", result);
        Assert.Contains("if (!this.enabled || !this.initialized)", result);
        Assert.Contains("if (!this.userInteracted)", result);
        Assert.Contains("const soundGenerator = this.sounds[soundName];", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsCreateBallCollisionSoundFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createBallCollisionSound(velocity) {", result);
        Assert.Contains("const now = this.context.currentTime;", result);
        Assert.Contains("const osc = this.context.createOscillator();", result);
        Assert.Contains("osc.frequency.setValueAtTime(800 + velocity * 1200, now)", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsCreateCushionBounceSoundFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createCushionBounceSound(velocity) {", result);
        Assert.Contains("osc.type = 'triangle';", result);
        Assert.Contains("osc.frequency.setValueAtTime(180 + velocity * 120, now)", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsCreatePocketSoundFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createPocketSound(velocity) {", result);
        Assert.Contains("osc.frequency.setValueAtTime(220, now)", result);
        Assert.Contains("osc.frequency.exponentialRampToValueAtTime(80, now + 0.15)", result);
        Assert.Contains("const vol = 0.4 * this.volume;", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsCreateCueHitSoundFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createCueHitSound(power) {", result);
        Assert.Contains("const freq = 600 + power * 400;", result);
        Assert.Contains("filter.type = 'highpass';", result);
        Assert.Contains("filter.frequency.value = 2000;", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsCreateBallRollSoundFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createBallRollSound(speed) {", result);
        Assert.Contains("filter.type = 'bandpass';", result);
        Assert.Contains("filter.frequency.value = 100 + speed * 50;", result);
        Assert.Contains("filter.Q.value = 3;", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsCreateNoiseNodeFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createNoiseNode() {", result);
        Assert.Contains("const bufferSize = this.context.sampleRate * 0.1;", result);
        Assert.Contains("const buffer = this.context.createBuffer(1, bufferSize, this.context.sampleRate);", result);
        Assert.Contains("data[i] = Math.random() * 2 - 1;", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsSetVolumeFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("setVolume(vol) {", result);
        Assert.Contains("this.volume = Math.max(0, Math.min(1, vol));", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsSetEnabledFunction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("setEnabled(enabled) {", result);
        Assert.Contains("this.enabled = enabled;", result);
        Assert.Contains("if (enabled && !this.initialized) {", result);
        Assert.Contains("this.init();", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsInitialPropertyDeclarations()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("context: null,", result);
        Assert.Contains("sounds: {},", result);
        Assert.Contains("initialized: false,", result);
        Assert.Contains("enabled: false,", result);
        Assert.Contains("volume: 0.5,", result);
        Assert.Contains("userInteracted: false,", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsModuleLoadedConsoleLog()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("console.log('PoolAudio module loaded');", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsHeaderComment()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("// POOL AUDIO MODULE", result);
        Assert.Contains("// Realistic sound effects system", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsEventListenersForUserInteraction()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("document.addEventListener('click', unlockAudio", result);
        Assert.Contains("document.addEventListener('touchstart', unlockAudio", result);
        Assert.Contains("document.addEventListener('touchend', unlockAudio", result);
        Assert.Contains("document.addEventListener('mousedown', unlockAudio", result);
        Assert.Contains("document.addEventListener('keydown', unlockAudio", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsEventRemovalCode()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("document.removeEventListener('click', unlockAudio);", result);
        Assert.Contains("document.removeEventListener('touchstart', unlockAudio);", result);
        Assert.Contains("document.removeEventListener('touchend', unlockAudio);", result);
        Assert.Contains("document.removeEventListener('mousedown', unlockAudio);", result);
        Assert.Contains("document.removeEventListener('keydown', unlockAudio);", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsAudioContextResumeLogic()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("if (this.context.state === 'suspended')", result);
        Assert.Contains("this.context.resume()", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsCustomEventDispatch()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("window.dispatchEvent(new Event('audioUnlocked'));", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsTestSoundCreation()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("const testOsc = this.context.createOscillator();", result);
        Assert.Contains("const testGain = this.context.createGain();", result);
        Assert.Contains("testGain.gain.value = 0.001;", result);
        Assert.Contains("testOsc.start();", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsNoiseCreationWithRandomValues()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("for (let i = 0; i < bufferSize; i++) {", result);
        Assert.Contains("data[i] = Math.random() * 2 - 1;", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsMAUIWebViewAutoUnlock()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("setTimeout(() => {", result);
        Assert.Contains("if (!this.userInteracted && this.context.state === 'suspended')", result);
        Assert.Contains("console.log('[Audio] Attempting auto-unlock for MAUI WebView...');", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsAllSoundTypes()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("ballCollision", result);
        Assert.Contains("cushionBounce", result);
        Assert.Contains("pocket", result);
        Assert.Contains("cueHit", result);
        Assert.Contains("ballRoll", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsFilterConfiguration()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("const filter = this.context.createBiquadFilter();", result);
        Assert.Contains("filter.type = 'bandpass';", result);
        Assert.Contains("filter.frequency.value = 400;", result);
        Assert.Contains("filter.Q.value = 2;", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsConsoleLogging()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("console.log('? Audio context initialized');", result);
        Assert.Contains("console.log('[Audio] User interaction detected, unlocking audio...');", result);
        Assert.Contains("console.log('[Audio] Audio fully unlocked! Sounds will now play.');", result);
        Assert.Contains("console.error('[Audio] Web Audio API initialization failed:', e);", result);
    }

    [Fact]
    public void GenerateJavaScript_WhenCalled_ContainsVolumeCalculations()
    {
        // Act
        var result = PoolAudioModule.GenerateJavaScript();

        // Assert
        Assert.Contains("const vol = Math.min(velocity * 0.3, 1.0) * this.volume;", result);
        Assert.Contains("const vol = Math.min(velocity * 0.25, 0.8) * this.volume;", result);
        Assert.Contains("const vol = Math.min(power * 0.4, 1.0) * this.volume;", result);
        Assert.Contains("const vol = Math.min(speed * 0.08, 0.15) * this.volume;", result);
    }
}
