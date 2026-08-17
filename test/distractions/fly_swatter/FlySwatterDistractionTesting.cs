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
        // Setup() doesn't look up any child nodes yet (Fly/Swatter/spawner wiring is pending) -
        // a bare instance satisfies today's contract tests. Once Setup() wiring lands this will
        // need a Stage/... stand-in tree, mirroring ThrowPaperBallDistractionTesting.CreateDistraction()
        return new FlySwatter();
    }

    // Setup before each test
    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
    }

    // Teardown after each test
    [AfterTest]
    public override void Teardown()
    {
        base.Teardown();
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