using Wdpl2.Services;
using Xunit;

namespace wdpl2.Tests.Features.Games.Pool.Engine;

public class PoolGameModuleTests
{
    [Fact]
    public void GenerateJavaScript_ReturnsNonNullString()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.NotNull(result);
    }

    [Fact]
    public void GenerateJavaScript_ReturnsNonEmptyString()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.NotEmpty(result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolGameClass()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("class PoolGame", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsConstructor()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("constructor(canvas, statusEl)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGetCurrentPlayerMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("getCurrentPlayer()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGetOpponentMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("getOpponent()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsSwitchTurnMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("switchTurn()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsUpdateTurnDisplayMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("updateTurnDisplay()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsStartShotMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("startShot()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRecordFirstBallHitMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("recordFirstBallHit(ball)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRecordCushionHitMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("recordCushionHit()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRecordBallPottedMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("recordBallPotted(ball)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEvaluateShotMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("evaluateShot()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsEvaluateBreakShotMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("evaluateBreakShot()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRespotBlackMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("respotBlack()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShowRespotMessageMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showRespotMessage()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsProcessPottedBallsMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("processPottedBalls()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShowLossOfTurnMessageMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showLossOfTurnMessage(reason)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsAssignColorsMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("assignColors(color)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShowColorAssignmentMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showColorAssignment()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCheckIfOnBlackMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("checkIfOnBlack()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsHandleBlackPottedMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("handleBlackPotted(player)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShowFrameWinMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showFrameWin(winnerName, subtitle, p1Frames, p2Frames, framesToWin)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShowMatchWinMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showMatchWin(winnerName, matchName, p1Frames, p2Frames)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsNextFrameMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("nextFrame()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsNewMatchMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("newMatch()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsResetFrameStateMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("resetFrameState()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShowGameOverMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showGameOver(title, subtitle)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCommitFoulMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("commitFoul(reason)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCommitScratchFoulMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("commitScratchFoul(reason, restrictToBaulk = false)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCommitBallInHandTouchFoulMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("commitBallInHandTouchFoul(touchedBall)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShowFoulMessageMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("showFoulMessage(reason, isBaulkRestricted = false)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsHandleCueBallPottedMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("handleCueBallPotted(restrictToBaulk = false)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPlaceCueBallMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("placeCueBall(x, y)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsNewGameMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("newGame()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsInitMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("init()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCreateAudioStatusIndicatorMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("createAudioStatusIndicator()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRepositionPocketsMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("repositionPockets()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsResetRackMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("resetRack()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsStopBallsMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("stopBalls()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsUpdateBallReturnTrayMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("updateBallReturnTray(ball)", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsUpdateBallReturnStatsMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("updateBallReturnStats()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsClearBallReturnTrayMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("clearBallReturnTray()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsAnimateMethod()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("animate()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsWindowLoadEventListener()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("window.addEventListener('load'", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCommentHeader()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("POOL GAME MAIN MODULE", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCanvasInitialization()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.canvas = canvas", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCanvasContextInitialization()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.ctx = canvas.getContext('2d')", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGameStateVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.balls = []", result);
        Assert.Contains("this.cueBall = null", result);
        Assert.Contains("this.pockets = []", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPlayerArrayInitialization()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.players = [", result);
        Assert.Contains("'Player 1'", result);
        Assert.Contains("'Player 2'", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGoldenBallRule()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.goldenBall = false", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGoldenDuckRule()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.goldenDuck = false", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsMatchTypeVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.matchType = 'single'", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsGamePhaseVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.gamePhase = 'break'", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBaulkLineXVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.baulkLineX = this.width * 0.2", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsFrictionVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.friction = 0.987", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsCushionRestitutionVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.cushionRestitution = 0.78", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsSpinControlProperties()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.maxSpin = 1.5", result);
        Assert.Contains("this.spinEffect = 2.0", result);
        Assert.Contains("this.showSpinArrows = true", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsTrajectoryPredictionSettings()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.showTrajectoryPrediction = true", result);
        Assert.Contains("this.trajectoryLength = 200", result);
        Assert.Contains("this.trajectorySegments = 15", result);
        Assert.Contains("this.showCollisionPoints = true", result);
        Assert.Contains("this.showGhostBalls = true", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBreakPointsCalculation()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("let breakPoints = 0", result);
        Assert.Contains("REQUIRED_BREAK_POINTS = 3", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolInputSetupCalls()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("PoolInput.setupMouseControls", result);
        Assert.Contains("PoolInput.setupTouchControls", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolSpinControlSetup()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("PoolSpinControl.setupSpinControl", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolRenderingCalls()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("PoolRendering.drawTable", result);
        Assert.Contains("PoolRendering.drawPockets", result);
        Assert.Contains("PoolRendering.drawBall", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolPhysicsCalls()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("PoolPhysics.applyFriction", result);
        Assert.Contains("PoolPhysics.processCollisions", result);
        Assert.Contains("PoolPhysics.handleCushionBounce", result);
        Assert.Contains("PoolPhysics.handlePocketJawCollision", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolAudioCalls()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("PoolAudio", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolReplayCalls()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("PoolReplay", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPoolAICalls()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("PoolAI", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRequestAnimationFrameCall()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("requestAnimationFrame", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsConsoleLogStatements()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("console.log", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsRackPattern()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("rackPattern", result);
        Assert.Contains("15-ball rack pattern", result);
    }

    [Fact]
    public void GenerateJavaScript_ReturnsStringWithMinimumLength()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        // The JavaScript is quite large, should be at least 10000 characters
        Assert.True(result.Length > 10000, $"Expected length > 10000, but was {result.Length}");
    }

    [Fact]
    public void GenerateJavaScript_ContainsBallReturnTrayMethods()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("updateBallReturnTray(ball)", result);
        Assert.Contains("updateBallReturnStats()", result);
        Assert.Contains("clearBallReturnTray()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPocketRadiusVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.cornerPocketRadius", result);
        Assert.Contains("this.middlePocketRadius", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPocketOpeningVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.cornerPocketOpening", result);
        Assert.Contains("this.sidePocketOpening", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBallRadiusVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.standardBallRadius", result);
        Assert.Contains("this.cueBallRadius", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsPixelsPerInchVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.pixelsPerInch", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsDimensionVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.width = 1000", result);
        Assert.Contains("this.height = 500", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShotControlModeVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.shotControlMode = 'drag'", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsDeveloperSettingsProperties()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.captureThresholdPercent", result);
        Assert.Contains("this.showPocketZones", result);
        Assert.Contains("this.showCushionLines", result);
        Assert.Contains("this.showVelocities", result);
        Assert.Contains("this.showFps", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBallInHandTouchFoulVariable()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.ballInHandTouchFoul = true", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsFPSTrackingVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.fps = 0", result);
        Assert.Contains("this.frameCount = 0", result);
        Assert.Contains("this.lastFpsUpdate", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsBallsCrossedCenterSet()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.ballsCrossedCenter = new Set()", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsShotTrackingVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.shotInProgress = false", result);
        Assert.Contains("this.firstBallHit = null", result);
        Assert.Contains("this.ballsPottedThisShot = []", result);
        Assert.Contains("this.cueBallPotted = false", result);
    }

    [Fact]
    public void GenerateJavaScript_ContainsFoulTrackingVariables()
    {
        // Act
        var result = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Contains("this.foulCommitted = false", result);
        Assert.Contains("this.foulReason = ''", result);
        Assert.Contains("this.ballInHand = false", result);
        Assert.Contains("this.ballInHandBaulk = true", result);
    }

    [Fact]
    public void GenerateJavaScript_ReturnsConsistentResultOnMultipleCalls()
    {
        // Act
        var result1 = PoolGameModule.GenerateJavaScript();
        var result2 = PoolGameModule.GenerateJavaScript();

        // Assert
        Assert.Equal(result1, result2);
    }
}
