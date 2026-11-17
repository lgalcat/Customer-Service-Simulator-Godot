using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;
using System.Threading.Tasks;

// TestSuite for ThrowPaperBall-specific behaviour
// (see DistractionTesting/ThrowPaperBallDistractionTesting for the contract-level checks, ThrowPaperBallSetupTesting
// for choosing the layout, ThrowPaperBallLayoutContentTesting for per-layout invariants)
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallBehaviourTesting : DistractionTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/throw_paper_ball/throw_paper_ball.tscn";
    private const float Tolerance = 0.5f;

    protected override Distraction CreateDistraction()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        return InstantiateShell();
    }

    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(ThrowPaperBallBehaviourTesting));

    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
    }

    [AfterTest]
    public override void Teardown()
    {
        // A press left in the global Input singleton would leak into the next test
        Input.ActionRelease("JumpKey");
        base.Teardown();
    }

    // Tests that Setup copies the ball's actual physics parameters into the projection,
    // not just default/zero values - requires the real scene's authored Ball properties
    [TestCase]
    public void SetupWiresProjectionParametersFromBall()
    {
        distraction.Setup(1);
        Ball ball = distraction.GetNode<Ball>("Stage/Ball");
        Projection projection = distraction.GetNode<Projection>("Stage/Projection");

        AssertThat(projection.Damp).IsEqual(ball.LinearDamp);
        AssertThat(projection.GravityScale).IsEqual(ball.GravityScale);
    }

    // Tests the Ball.BallReset -> ThrowPaperBall.ResetState -> Projection.DrawOneStep sequence
    // bound in Setup, by triggering the ball's reset event
    // Observes projection's sprite visibility (the only externally-observable effect of ResetState)
    [TestCase]
    public void BallResetTriggersProjectionSingleStepReset()
    {
        distraction.Setup(1);
        Ball ball = distraction.GetNode<Ball>("Stage/Ball");
        Projection projection = distraction.GetNode<Projection>("Stage/Projection");
        // Manually populate the projection's sprite steps (no SceneTree to call _Ready() )
        projection._Ready();

        ball.BallReset?.Invoke();

        var steps = projection.GetChildren();
        AssertThat(((Sprite2D)steps[0]).Visible).IsTrue();
        for (int i = 1; i < steps.Count; i++)
        {
            AssertThat(((Sprite2D)steps[i]).Visible).IsFalse();
        }
    }

    // Tests the TrashCan.MinigameCompleted -> Victory sequence bound in Setup,
    // by triggering the trash can's completion event directly
    [TestCase]
    public void MinigameCompletedTriggersVictory()
    {
        distraction.Setup(1);
        TrashCan trashCan = distraction.GetNode<TrashCan>("Stage/TrashCan");
        bool victoryCalled = false;
        distraction.OnVictory = () => { victoryCalled = true; };

        trashCan.MinigameCompleted?.Invoke();

        AssertThat(victoryCalled).IsTrue();
    }

    // Tests the full aiming -> charging -> throw state machine driven by _Process, via
    // simulated input on a live scene (the only way to get real IsActionJustPressed/
    // IsActionJustReleased edges to fire)
    [TestCase]
    public async Task ChargingAndReleasingThrowsTheBall()
    {
        // Setup() is the hosting factory's job at runtime; call it before tree entry (the runner
        // enters the tree on Load), since _Ready() no longer does - it only runs ResetState(),
        // which needs Setup() to have assigned _projection first
        ThrowPaperBall throwPaperBall = InstantiateShell();
        throwPaperBall.Setup(1);
        using ISceneRunner runner = ISceneRunner.Load(throwPaperBall, true, true);
        Ball ball = throwPaperBall.GetNode<Ball>("Stage/Ball");

        AssertThat(ball.IsIdle).IsTrue();

        runner.SimulateActionPress("JumpKey");
        await runner.SimulateFrames(2);
        runner.SimulateActionRelease("JumpKey");
        await runner.SimulateFrames(2);

        AssertThat(ball.IsIdle).IsFalse();
    }

    // Aiming starts at the layout's highest angle and charging at the middle of its strength range: a throw released
    // with no time passing carries exactly those (the midpoint differs from the (max - min) / 2 and min + max / 2 slips)
    [TestCase]
    public void ImmediateThrowUsesMaxAngleAndMidStrength()
    {
        ThrowPaperBall throwPaperBall = SetUpTunedShell();
        using ISceneRunner runner = ISceneRunner.Load(throwPaperBall, true, true);

        Vector2 impulse = Throw(throwPaperBall, aimingTime: 0);

        AssertThat(impulse.DistanceTo(Vector2.FromAngle(Mathf.DegToRad(TunedMaxAngle)) * 400)).IsLess(Tolerance);
    }

    // One ThrowCycle of aiming sweeps the whole angle range, from the highest angle down to the lowest
    [TestCase]
    public void AimingSweepsToMinAngleInOneThrowCycle()
    {
        ThrowPaperBall throwPaperBall = SetUpTunedShell();
        using ISceneRunner runner = ISceneRunner.Load(throwPaperBall, true, true);

        Vector2 impulse = Throw(throwPaperBall, aimingTime: TunedCycle);

        AssertThat(Mathf.Abs(Mathf.RadToDeg(impulse.Angle()) - TunedMinAngle)).IsLess(0.1f);
    }

    // Tests the TrashCan.BallEntered/BallExited -> Ball.PauseTime/ResumeTime wiring bound in
    // Setup, by triggering both trash can events in sequence and observing the ball's actual
    // reset Timer (built via _Ready) pause state - the only externally-observable effect
    [TestCase]
    public void BallEnteredAndExitedPauseAndResumeBallTimer()
    {
        distraction.Setup(1);
        Ball ball = distraction.GetNode<Ball>("Stage/Ball");
        TrashCan trashCan = distraction.GetNode<TrashCan>("Stage/TrashCan");
        ball._Ready();
        Timer timer = AutoFree(ball.GetChildren().OfType<Timer>().First())!;
        // PauseTime only takes effect while the ball is in the "thrown" state
        ball.Throw(Vector2.Right * 100);

        trashCan.BallEntered?.Invoke();
        AssertThat(timer.Paused).IsTrue();

        trashCan.BallExited?.Invoke();
        AssertThat(timer.Paused).IsFalse();
    }

    // Tests that the trash can's collision body follows the TrashCan node in the physics server when the
    // composite is moved (TrashCan movement moves the parent, never the body itself)
    // Guards TrashCanBody's non-default sync_to_physics = false: with it on, the AnimatableBody2D only
    // forwards changes to its own local transform, so a moving parent leaves the solid walls behind while
    // the sprite and win area move. Reads the physics server's transform, not the node's: with sync on, the
    // node is snapped back to the physics state, so the two would agree on the wrong position
    // TrashCan's own path movement is switched off and the move done by hand, so the case doesn't depend
    // on whether the layout's path is wired
    [TestCase]
    public async Task TrashCanBodyFollowsMovedTrashCanInPhysics()
    {
        ThrowPaperBall throwPaperBall = InstantiateShell();
        throwPaperBall.Setup(1);
        using ISceneRunner runner = ISceneRunner.Load(throwPaperBall, true, true);
        TrashCan trashCan = throwPaperBall.GetNode<TrashCan>("Stage/TrashCan");
        AnimatableBody2D body = trashCan.GetNode<AnimatableBody2D>("TrashCanBody");
        trashCan.SetPhysicsProcess(false);
        Vector2 bodyLocalPosition = body.Position;

        trashCan.Position += new Vector2(-40, 0);
        await runner.SimulateFrames(10, 20);

        var physicsTransform = (Transform2D)PhysicsServer2D.BodyGetState(body.GetRid(), PhysicsServer2D.BodyState.Transform);
        Vector2 expected = trashCan.GlobalTransform * bodyLocalPosition;
        AssertThat(physicsTransform.Origin.DistanceTo(expected)).IsLess(0.5f);
    }

    // The real shell, pinned to its authored Stage: without this, Setup() would roll a random catalog layout each run,
    // or play whatever LayoutOverride is saved in the scene
    private static ThrowPaperBall InstantiateShell()
    {
        ThrowPaperBall throwPaperBall = GD.Load<PackedScene>(ScenePath).Instantiate<ThrowPaperBall>();
        throwPaperBall.LayoutOverride = null;
        throwPaperBall.Set("_layoutCatalog", new Variant());
        return throwPaperBall;
    }

    // Throw tuning no layout uses, so the values seen at the throw can only come from the Stage
    private const float TunedMinAngle = -100;
    private const float TunedMaxAngle = -30;
    private const float TunedCycle = 2;

    private static ThrowPaperBall SetUpTunedShell()
    {
        ThrowPaperBall throwPaperBall = InstantiateShell();
        ThrowPaperBallLayout stage = throwPaperBall.GetNode<ThrowPaperBallLayout>("Stage");
        stage.MinThrowAngle = TunedMinAngle;
        stage.MaxThrowAngle = TunedMaxAngle;
        stage.MinThrowStrength = 200;
        stage.MaxThrowStrength = 600;
        stage.ThrowCycle = TunedCycle;
        throwPaperBall.Setup(1);
        return throwPaperBall;
    }

    // Drives the state machine by hand: one aiming step of "aimingTime" seconds ending in a press, then a release with
    // no time passing, and returns the impulse the ball was thrown with
    // Input.ActionPress/ActionRelease count as "just" pressed/released for the rest of the current frame, and no frame
    // runs between these synchronous calls, so the edges are exact (unlike SimulateAction*, which lands a variable
    // number of ticks later). The physics server applies an impulse to the velocity at once
    private static Vector2 Throw(ThrowPaperBall throwPaperBall, double aimingTime)
    {
        Input.ActionPress("JumpKey");
        throwPaperBall._Process(aimingTime);
        Input.ActionRelease("JumpKey");
        throwPaperBall._Process(0);

        Ball ball = throwPaperBall.GetNode<Ball>("Stage/Ball");
        AssertThat(ball.IsIdle).IsFalse();
        return (Vector2)PhysicsServer2D.BodyGetState(ball.GetRid(), PhysicsServer2D.BodyState.LinearVelocity) * ball.Mass;
    }
}
