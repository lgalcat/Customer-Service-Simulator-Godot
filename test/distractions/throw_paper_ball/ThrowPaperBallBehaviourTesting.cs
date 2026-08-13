using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;
using System.Threading.Tasks;

// TestSuite for ThrowPaperBall-specific behaviour
// (see DistractionTesting/ThrowPaperBallDistractionTesting for the contract-level checks)
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallBehaviourTesting : DistractionTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/throw_paper_ball/throw_paper_ball.tscn";
    // Design-invariant thresholds - not derived from code
    // Keep margins comfortably passable/winnable as scene/tuning evolve
    private const float MinimumFitRatio = 1.5f; // win area / body opening must be >= this times the ball's width
    private const float MinimumTimerMargin = 0.5f; // seconds the ball must stay alive past the win timer

    protected override Distraction CreateDistraction()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        return GD.Load<PackedScene>(ScenePath).Instantiate<ThrowPaperBall>();
    }

    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
    }

    [AfterTest]
    public override void Teardown()
    {
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
        using ISceneRunner runner = ISceneRunner.Load(ScenePath, true, true);
        Ball ball = ((ThrowPaperBall)runner.Scene()!).GetNode<Ball>("Stage/Ball");

        AssertThat(ball.IsIdle).IsTrue();

        runner.SimulateActionPress("JumpKey");
        await runner.SimulateFrames(2);
        runner.SimulateActionRelease("JumpKey");
        await runner.SimulateFrames(2);

        AssertThat(ball.IsIdle).IsFalse();
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

    // Tests that the ball's lifetime leaves a comfortable margin past the win timer
    // Reads both values off the components' actual Timer children 
    // test tracks whatever value ends up driving real timers at startup
    [TestCase]
    public void BallLifetimeExceedsWinTimeWithMargin()
    {
        Ball ball = distraction.GetNode<Ball>("Stage/Ball");
        TrashCan trashCan = distraction.GetNode<TrashCan>("Stage/TrashCan");
        // Neither node ever entered a live SceneTree -> no auto _Ready() call
        // Each component builds its internal Timer during _Ready()
        ball._Ready();
        trashCan._Ready();

        // Find procedurally-created children without concrete names
        // AutoFree them explicitly: they're created mid-test, after distraction was
        // already wrapped, so they need explicit cleanup registration
        double lifeTime = AutoFree(ball.GetChildren().OfType<Timer>().First())!.WaitTime;
        double winTime = AutoFree(trashCan.GetChildren().OfType<Timer>().First())!.WaitTime;

        AssertThat(lifeTime - winTime).IsGreaterEqual(MinimumTimerMargin);
    }

    // Tests that the win area is wide enough for the ball to actually fit inside
    // not just visual overlap (verify win condition triggerability)
    [TestCase]
    public void WinAreaFitsBallWithMargin()
    {
        TrashCan trashCan = distraction.GetNode<TrashCan>("Stage/TrashCan");
        var winAreaPolygon = trashCan.GetNode<CollisionPolygon2D>("TrashCanInside/TrashInsideArea").Polygon;

        float winAreaWidth = NarrowestHorizontalSpan(winAreaPolygon);

        AssertThat(winAreaWidth).IsGreaterEqual(BallDiameter() * MinimumFitRatio);
    }

    // Tests that the trash can's body leaves a wide enough opening for the ball to pass through
    // Checks for designer placed Markers flagging the edges of the intended entry point
    [TestCase]
    public void BodyOpeningFitsBallWithMargin()
    {
        TrashCan trashCan = distraction.GetNode<TrashCan>("Stage/TrashCan");
        Marker2D edgeA = trashCan.GetNode<Marker2D>("TrashCanBody/EdgeA");
        Marker2D edgeB = trashCan.GetNode<Marker2D>("TrashCanBody/EdgeB");

        float openingWidth = edgeA.Position.DistanceTo(edgeB.Position);

        AssertThat(openingWidth).IsGreaterEqual(BallDiameter() * MinimumFitRatio);
    }

    // Helper to find a Ball's max width (assumes shape is a CircleShape2D)
    private float BallDiameter()
    {
        var ballShape = distraction.GetNode<Ball>("Stage/Ball").GetNode<CollisionShape2D>("BallShape");
        return ((CircleShape2D)ballShape.Shape).Radius * 2;
    }

    // Helper that finds narrowest horizontal span of a polygon across its y axis
    // Assumes convex polygon whose width is monotonic between vertex y-levels,
    // (true for simple shapes like trapezoids) a concave shape would not be correctly analyzed by this
    private static float NarrowestHorizontalSpan(Vector2[] polygon)
    {
        return polygon
            .GroupBy(point => point.Y)
            .Select(levelPoints => levelPoints.Max(point => point.X) - levelPoints.Min(point => point.X))
            .Min();
    }
}