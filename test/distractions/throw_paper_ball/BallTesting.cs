using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;
using System.Threading.Tasks;

// Dedicated component suite for Ball, independent of the Distraction hierarchy.
// Covers Ball's own state machine (idle/thrown/overdue) and timer-driven reset,
// not whether ThrowPaperBall/TrashCan wire it correctly (see ThrowPaperBallBehaviourTesting)
[TestSuite]
[RequireGodotRuntime]
public class BallTesting
{
    private Ball _ball = null!;

    [BeforeTest]
    public void Setup()
    {
        // Barebones setup for a Ball object (testing independent from any scene)
        _ball = AutoFree(new Ball())!;
        // Implement any additional pre test logic here
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
        // Implement any additional post test logic here
    }

    // _lifeTime has no public accessor, so this can only check the Timer picked up some
    // positive WaitTime during _Ready() - not that it equals _lifeTime's actual value
    [TestCase]
    public void ReadySetsIdleStateFreezesAndCreatesTimer()
    {
        // Manual call for setup, no SceneTree to call _Ready()
        _ball._Ready();
        Timer timer = AutoFree(_ball.GetChildren().OfType<Timer>().First())!;

        AssertThat(_ball.IsIdle).IsTrue();
        AssertThat(_ball.Freeze).IsTrue();
        AssertThat(timer.OneShot).IsTrue();
        AssertThat(timer.WaitTime).IsGreater(0d);
    }

    // Verify state machine Idle -> Thrown update path via affected attributes
    [TestCase]
    public void ThrowFromIdleUnfreezesAndSetsThrownState()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _ball._Ready();
        AutoFree(_ball.GetChildren().OfType<Timer>().First());

        _ball.Throw(Vector2.Right * 100);

        AssertThat(_ball.Freeze).IsFalse();
        AssertThat(_ball.IsIdle).IsFalse();
    }

    // Defensive test to verify state machine safeguards preventing illegal state updates
    [TestCase]
    public void ThrowIsIgnoredWhenNotIdle()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _ball._Ready();
        AutoFree(_ball.GetChildren().OfType<Timer>().First());
        _ball.Throw(Vector2.Right * 100);
        // Detectable probe: wrongly-accepted second Throw() would flip this back to false
        _ball.Freeze = true;

        _ball.Throw(Vector2.Right * 100);

        AssertThat(_ball.Freeze).IsTrue();
    }

    // Verify state machine Thrown -> Overdue update path via affected attributes
    [TestCase]
    public void TimerTimeoutTriggersResetToOverdueAndRefreezes()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _ball._Ready();
        Timer timer = AutoFree(_ball.GetChildren().OfType<Timer>().First())!;
        _ball.Throw(Vector2.Right * 100);

        timer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(_ball.Freeze).IsTrue();
        // Freeze alone doesn't distinguish "overdue" from "idle" (both freeze) - IsIdle does
        AssertThat(_ball.IsIdle).IsFalse();
    }

    // Verify Reset's Timer attribute reset (safeguard feature intended for edge case error prevention)
    // [11/08/2026] This case makes me think a more black-box approach to test specification 
    // [11/08/2026] ...focused on edge values/timings could be a better fit, need time to meditate it
    [TestCase]
    public void TimeoutClearsAnExistingPause()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _ball._Ready();
        Timer timer = AutoFree(_ball.GetChildren().OfType<Timer>().First())!;
        _ball.Throw(Vector2.Right * 100);
        _ball.PauseTime();
        AssertThat(timer.Paused).IsTrue();

        timer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(timer.Paused).IsFalse();
    }

    // Defensive test to verify state machine safeguards for invalid external PauseTime calls
    [TestCase]
    public void PauseTimeHasNoEffectWhenNotThrown()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _ball._Ready();
        Timer timer = AutoFree(_ball.GetChildren().OfType<Timer>().First())!;

        _ball.PauseTime();

        AssertThat(timer.Paused).IsFalse();
    }

    // Verify PauseTime's and ResumeTime's (publicly accesible methods) internal component updates
    [TestCase]
    public void PauseTimeAndResumeTimeToggleTimerPause()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _ball._Ready();
        Timer timer = AutoFree(_ball.GetChildren().OfType<Timer>().First())!;
        _ball.Throw(Vector2.Right * 100);

        _ball.PauseTime();
        AssertThat(timer.Paused).IsTrue();

        _ball.ResumeTime();
        AssertThat(timer.Paused).IsFalse();
    }

    // Verify state machine Overdue -> Idle update path and related position reset updates
    // This case requires a working SceneTree
    // [11/08/2026] Consider splitting position, state and action updates into separate tests
    // [11/08/2026] ...(although both run sequentially during same engine method call)
    [TestCase]
    public async Task OverdueBallResetsPositionAndBecomesIdleAfterPhysicsStep()
    {
        // Setup "live" object inside a SceneTree
        var parent = new Node2D();
        var ball = new Ball { Position = new Vector2(200, 150) };
        parent.AddChild(ball);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Timer timer = ball.GetChildren().OfType<Timer>().First();
        Vector2 spawn = ball.Position;
        // Assert specific setup
        int resetCount = 0;
        ball.BallReset = () => { resetCount++; };

        ball.Throw(Vector2.Right * 100);
        await runner.SimulateFrames(2);
        timer.EmitSignal(Timer.SignalName.Timeout);
        await runner.SimulateFrames(10, 20);

        AssertThat(ball.IsIdle).IsTrue();
        AssertThat(resetCount).IsEqual(1);
        AssertThat(ball.Position.DistanceTo(spawn)).IsLess(1f);
        AssertThat(ball.LinearVelocity).IsEqual(Vector2.Zero);
        AssertThat(ball.AngularVelocity).IsEqual(0f);
    }
}