using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Threading.Tasks;

// TestSuite for ThrowPaperBall-specific behaviour
// (see DistractionTesting/ThrowPaperBallDistractionTesting for the contract-level checks)
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallBehaviourTesting : DistractionTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/throw_paper_ball/throw_paper_ball.tscn";

    protected override Distraction CreateDistraction()
    {
        // Full scene instancing (as oposed to barebones object instancing)
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
}