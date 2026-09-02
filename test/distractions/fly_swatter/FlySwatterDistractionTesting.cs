using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// TestSuite for testing of the FlySwatter class
[TestSuite]
[RequireGodotRuntime]
public class FlySwatterDistractionTesting : DistractionTesting
{
    protected override Distraction CreateDistraction()
    {
        // Barebones stand-in tree matching only the node name/type Setup() looks up
        // (Stage/FlySpawner) - not the full production scene
        var flySwatter = new FlySwatter();
        var stage = new Node2D { Name = "Stage" };
        flySwatter.AddChild(stage);
        stage.AddChild(new FlySpawner { Name = "FlySpawner" });
        return flySwatter;
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