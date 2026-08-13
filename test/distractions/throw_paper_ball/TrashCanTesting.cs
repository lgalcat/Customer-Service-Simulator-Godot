using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Linq;

// Dedicated component suite for TrashCan, independent of the Distraction hierarchy.
// Covers TrashCan's own state (dwell-timer, body-type filtering), not whether
// ThrowPaperBall wires it correctly (see ThrowPaperBallBehaviourTesting for that)
[TestSuite]
[RequireGodotRuntime]
public class TrashCanTesting
{
    private TrashCan _trashCan = null!;
    private Area2D _winArea = null!;

    [BeforeTest]
    public void Setup()
    {
        // Barebones setup for a TrashCan object (testing independent from any scene)
        _trashCan = AutoFree(new TrashCan())!;
        _winArea = new Area2D { Name = "TrashCanInside" };
        _trashCan.AddChild(_winArea);
        // Implement any additional pre-test logic here
    }

    [AfterTest]
    public void Teardown()
    {
        // Node cleanup is handled by AutoFree()
        // Implement any additional post-testing logic here
    }

    // This test only checks Timer instancing and non default WaitTime values
    // an exact value of WaitTime is not expected or validated
    [TestCase]
    public void ReadySpawnsWinCounterWithConfiguredWaitTime()
    {
        // Manual call for setup, no SceneTree to auto call _Ready()
        _trashCan._Ready();
        Timer winCounter = AutoFree(_trashCan.GetChildren().OfType<Timer>().First())!;

        AssertThat(winCounter.OneShot).IsTrue();
        AssertThat(winCounter.WaitTime).IsGreater(0d);
    }

    // Emitting BodyEntered/BodyExited directly (rather than a real physics overlap) is deliberate
    // Correct physics engine process testing is out of scope (treated as black box)
    // For testing of correct treatment of events and invariants see ThrowPaperBallBehaviourTesting
    // This test case requires a working SceneTree
    [TestCase]
    public void BallEnteringThenExitingStartsThenStopsTimer()
    {
        // Create "live" object inside a SceneTree
        var trashCan = new TrashCan();
        trashCan.AddChild(new Area2D { Name = "TrashCanInside" });
        using ISceneRunner runner = ISceneRunner.Load(trashCan, true, true);
        Area2D winArea = trashCan.GetNode<Area2D>("TrashCanInside");
        Timer winCounter = trashCan.GetChildren().OfType<Timer>().First();
        Ball ball = AutoFree(new Ball())!;
        // Assert specific setup
        bool ballEntered = false;
        bool ballExited = false;
        trashCan.BallEntered = () => { ballEntered = true; };
        trashCan.BallExited = () => { ballExited = true; };

        winArea.EmitSignal(Area2D.SignalName.BodyEntered, ball);
        AssertThat(ballEntered).IsTrue();
        AssertThat(ballExited).IsFalse();
        AssertThat(winCounter.IsStopped()).IsFalse();

        winArea.EmitSignal(Area2D.SignalName.BodyExited, ball);
        AssertThat(ballExited).IsTrue();
        AssertThat(ballEntered).IsTrue();
        AssertThat(winCounter.IsStopped()).IsTrue();
    }

    // Defensive test to verify irrelevant objects don't trigger false positives
    // Only desired "Ball" objects should affect win condition logic
    [TestCase]
    public void NonBallBodyIsIgnoredOnEnterAndExit()
    {
        // Manual call for setup, no SceneTree calling _Ready() 
        _trashCan._Ready();
        Timer winCounter = AutoFree(_trashCan.GetChildren().OfType<Timer>().First())!;
        Node2D notABall = AutoFree(new Node2D())!;
        // Assert specific setup
        bool ballEntered = false;
        bool ballExited = false;
        _trashCan.BallEntered = () => { ballEntered = true; };
        _trashCan.BallExited = () => { ballExited = true; };

        _winArea.EmitSignal(Area2D.SignalName.BodyEntered, notABall);
        AssertThat(ballEntered).IsFalse();
        AssertThat(winCounter.IsStopped()).IsTrue();

        _winArea.EmitSignal(Area2D.SignalName.BodyExited, notABall);
        AssertThat(ballExited).IsFalse();
    }

    // Emits Timeout directly, rather than waiting for natural timeout, intentionally
    // only testing TrashCan's Timeout -> MinigameCompleted wiring, no pause/engine systems
    // For testing of proper event treatment see ThrowPaperBallBehaviourTesting
    [TestCase]
    public void WinCounterTimeoutTriggersMinigameCompleted()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _trashCan._Ready();
        Timer winCounter = AutoFree(_trashCan.GetChildren().OfType<Timer>().First())!;
        // Assert specific setup
        bool minigameCompleted = false;
        _trashCan.MinigameCompleted = () => { minigameCompleted = true; };

        winCounter.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(minigameCompleted).IsTrue();
    }

    // Defensive test to check improper object instancing or scene layout isn't tolerated
    // only verifies script level safeguards 
    // if validated, should guarantee exception throwing in any incomplete scenes
    [TestCase]
    public void ReadyThrowsWhenWinAreaMissing()
    {
        TrashCan orphanTrashCan = AutoFree(new TrashCan())!;

        AssertThrown(() => orphanTrashCan._Ready()).IsInstanceOf<NullReferenceException>();
    }
}