using Godot;
using GdUnit4;
using System.Threading.Tasks;
using static GdUnit4.Assertions;

// TestSuite for testing of the ThrowPaperBall class
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallTesting : DistractionTesting
{
    protected override Distraction CreateDistraction()
    {
        // [16/6/2026] Further logic pending complete implementation of the ThrowPaperBall class
        return new ThrowPaperBall();
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
}