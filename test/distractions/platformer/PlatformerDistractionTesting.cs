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
        // Barebones stand-in tree matching only the nodes Setup() and the layout's Validate() look up
        // (Stage/Player/MainCamera, Stage/Goal); the camera's default limits enclose both at the origin
        var platformer = new Platformer();
        var stage = new PlatformerLayout { Name = "Stage" };
        platformer.AddChild(stage);
        var player = new Node2D { Name = "Player" };
        stage.AddChild(player);
        player.AddChild(new Camera2D { Name = "MainCamera" });
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
