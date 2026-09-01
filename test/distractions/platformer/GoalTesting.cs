using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Threading.Tasks;

// TestSuite for testing of the Goal class
[TestSuite]
[RequireGodotRuntime]
public class GoalTesting
{
    // No shared fixture: tests split between tree-less (manual _Ready(), fires BodyEntered
    // directly) and live-tree (ISceneRunner, to prove the deferred Monitoring change actually
    // takes effect) shapes - mirrors CardTesting/PlayerTesting's no-shared-fixture pattern
    [BeforeTest]
    public void Setup()
    {
    }

    [AfterTest]
    public void Teardown()
    {
    }

    // Goal has no child nodes of its own to look up - _Ready() just wires BodyEntered
    [TestCase]
    public void ReadyWiresBodyEnteredToHandler()
    {
        var goal = AutoFree(new Goal())!;
        goal._Ready();
        var playerBody = AutoFree(new Node2D())!;
        playerBody.AddToGroup("player");
        int reachedCount = 0;
        goal.Reached += () => { reachedCount++; };

        goal.EmitSignal(Area2D.SignalName.BodyEntered, playerBody);

        AssertThat(reachedCount).IsEqual(1);
    }

    // Detection is entirely group-membership-based (see Player.cs's AddToGroup("player")), not a
    // concrete type check - a body outside the "player" group must be ignored
    [TestCase]
    public void NonPlayerBodyDoesNotTriggerReached()
    {
        var goal = AutoFree(new Goal())!;
        goal._Ready();
        var otherBody = AutoFree(new Node2D())!;
        bool reached = false;
        goal.Reached += () => { reached = true; };

        goal.EmitSignal(Area2D.SignalName.BodyEntered, otherBody);

        AssertThat(reached).IsFalse();
    }

    // Proves the SetDeferred(Monitoring, false) fix actually takes effect once processed, not
    // just that it compiles - mirrors the project's "deferred call needs a live tree" gotcha class
    [TestCase]
    public async Task BodyEnteredDisablesMonitoringAfterOneIdleFrame()
    {
        var goal = new Goal();
        using ISceneRunner runner = ISceneRunner.Load(goal, true, true);
        var playerBody = AutoFree(new Node2D())!;
        playerBody.AddToGroup("player");

        goal.EmitSignal(Area2D.SignalName.BodyEntered, playerBody);
        AssertThat(goal.Monitoring).IsTrue();

        await runner.SimulateFrames(1);

        AssertThat(goal.Monitoring).IsFalse();
    }
}
