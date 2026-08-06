using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// TestSuite for testing of the ThrowPaperBall class
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallDistractionTesting : DistractionTesting
{
    protected override Distraction CreateDistraction()
    {
        // Barebones stand-in tree matching only the node names/types Setup() looks up
        // (Stage/TrashCan, Stage/Ball, Stage/Projection) - not the full production scene
        var throwPaperBall = new ThrowPaperBall();
        var stage = new Node2D { Name = "Stage" };
        throwPaperBall.AddChild(stage);
        stage.AddChild(new TrashCan { Name = "TrashCan" });
        stage.AddChild(new Ball { Name = "Ball" });
        stage.AddChild(new Projection { Name = "Projection" });
        return throwPaperBall;
    }

    // Setup before each test
    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
        // Insert class specific logic here
    }

    // Teardown after each test
    [AfterTest]
    public override void Teardown()
    {
        base.Teardown();
        // Insert class specific logic here
    }


    // Block containing base calls to inheritted TestCases, no class specific logic should be present further down
    [TestCase]
    public override void VictoryInvoked() { base.VictoryInvoked(); }

    [TestCase]
    public override void NonZeroExpectedViewport() { base.NonZeroExpectedViewport(); }

    [TestCase]
    public override void SetupAssignsDifficulty() { base.SetupAssignsDifficulty(); }

    [TestCase]
    public override void VictoryDoesNotThrowWhenOnVictoryUnset() { base.VictoryDoesNotThrowWhenOnVictoryUnset(); }
}