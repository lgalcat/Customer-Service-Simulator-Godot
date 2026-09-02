using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// TestSuite for testing of the Platformer class
[TestSuite]
[RequireGodotRuntime]
public class PlatformerDistractionTesting : DistractionTesting
{
    protected override Distraction CreateDistraction()
    {
        // Barebones stand-in tree matching only the node names Setup() looks up
        // (Window/SubViewport/Stage/Goal) - GetNode walks by name, intermediate node types
        // don't need to match the real SubViewportContainer/SubViewport, only the Goal leaf does
        var platformer = new Platformer();
        var window = new Node2D { Name = "Window" };
        platformer.AddChild(window);
        var subViewport = new Node2D { Name = "SubViewport" };
        window.AddChild(subViewport);
        var stage = new Node2D { Name = "Stage" };
        subViewport.AddChild(stage);
        stage.AddChild(new Goal { Name = "Goal" });
        return platformer;
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

    [TestCase]
    public override void VictoryInvoked() { base.VictoryInvoked(); }

    [TestCase]
    public override void NonZeroExpectedViewport() { base.NonZeroExpectedViewport(); }

    [TestCase]
    public override void SetupAssignsDifficulty() { base.SetupAssignsDifficulty(); }

    [TestCase]
    public override void VictoryDoesNotThrowWhenOnVictoryUnset() { base.VictoryDoesNotThrowWhenOnVictoryUnset(); }
}
