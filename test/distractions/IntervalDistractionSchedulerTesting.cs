using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;

// Dedicated component suite for IntervalDistractionScheduler, independent of the Distraction
// hierarchy. No injection seam of its own exists yet - _manager is typed as the concrete
// DistractionManager, so every fixture here builds a real one (fast, via its own InitialFactory
// seam) as a sibling, matching distraction_manager.tscn's actual topology
[TestSuite]
[RequireGodotRuntime]
public class IntervalDistractionSchedulerTesting
{
    // Matches the [Export] defaults - used wherever a test doesn't care about the exact range
    private const float DefaultMinWait = 8f;
    private const float DefaultMaxWait = 20f;
    // Deliberately outside [DefaultMinWait, DefaultMaxWait] - a WaitTime that fell in the default
    // range with these set would mean _minWait/_maxWait aren't actually wired to GD.RandRange
    private const float CustomMinWait = 1f;
    private const float CustomMaxWait = 2f;

    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(IntervalDistractionSchedulerTesting));

    [TestCase]
    public void ReadyThrowsWhenManagerMissing()
    {
        IntervalDistractionScheduler scheduler = AutoFree(new IntervalDistractionScheduler())!;

        AssertThrown(() => scheduler._Ready()).IsInstanceOf<NullReferenceException>();
    }

    // Live tree required for Timer.Start()'s effect on IsStopped() to actually resolve
    [TestCase]
    public void ReadyBuildsStoppedTimerByDefault()
    {
        (DistractionManager manager, IntervalDistractionScheduler scheduler) = BuildTree();
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);
        Timer timer = scheduler.GetChildren().OfType<Timer>().First();

        AssertThat(timer.IsStopped()).IsTrue();
    }

    [TestCase]
    public void AutoStartArmsTimerOnReady()
    {
        (DistractionManager manager, IntervalDistractionScheduler scheduler) = BuildTree(autoStart: true, minWait: CustomMinWait, maxWait: CustomMaxWait);
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);
        Timer timer = scheduler.GetChildren().OfType<Timer>().First();

        AssertThat(timer.IsStopped()).IsFalse();
        AssertThat(timer.WaitTime).IsGreaterEqual((double)CustomMinWait);
        AssertThat(timer.WaitTime).IsLessEqual((double)CustomMaxWait);
    }

    // Emitting Timeout directly (rather than waiting out a real Timer) is deliberate - only
    // testing the wiring, not whether Start()/real elapsed time behave correctly
    [TestCase]
    public void TimerTimeoutRequestsADistraction()
    {
        (DistractionManager manager, IntervalDistractionScheduler scheduler) = BuildTree();
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);
        Timer timer = scheduler.GetChildren().OfType<Timer>().First();

        timer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(manager.IsDistractionActive).IsTrue();
    }

    [TestCase]
    public void DistractionCompletedReschedulesTimerWithinBounds()
    {
        (DistractionManager manager, IntervalDistractionScheduler scheduler) = BuildTree(minWait: CustomMinWait, maxWait: CustomMaxWait);
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);
        Timer timer = scheduler.GetChildren().OfType<Timer>().First();
        manager.RequestDistraction();
        Distraction distraction = FindHostedDistraction(manager);

        distraction.Victory();

        AssertThat(timer.IsStopped()).IsFalse();
        AssertThat(timer.WaitTime).IsGreaterEqual((double)CustomMinWait);
        AssertThat(timer.WaitTime).IsLessEqual((double)CustomMaxWait);
    }

    // Pins the documented stall corner case: a Timeout that finds the manager already active
    // (here, via an outside RequestDistraction() call the scheduler had no part in) is silently
    // ignored - the scheduler does not reschedule itself from this path
    [TestCase]
    public void TimeoutWhileManagerAlreadyActiveDoesNotSelfReschedule()
    {
        (DistractionManager manager, IntervalDistractionScheduler scheduler) = BuildTree();
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);
        Timer timer = scheduler.GetChildren().OfType<Timer>().First();
        manager.RequestDistraction();

        timer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(timer.IsStopped()).IsTrue();
    }

    // Continuing from the stall above: once the externally-triggered minigame completes, the
    // scheduler self-heals via DistractionCompleted, not from its own (already-fired) Timeout
    [TestCase]
    public void SchedulerSelfHealsOnceTheExternalDistractionCompletes()
    {
        (DistractionManager manager, IntervalDistractionScheduler scheduler) = BuildTree();
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);
        Timer timer = scheduler.GetChildren().OfType<Timer>().First();
        manager.RequestDistraction();
        timer.EmitSignal(Timer.SignalName.Timeout);
        Distraction distraction = FindHostedDistraction(manager);

        distraction.Victory();

        AssertThat(timer.IsStopped()).IsFalse();
    }

    // Builds the real distraction_manager.tscn topology by hand (DistractionManager -> Overlay +
    // Scheduler), not yet added to any tree - the caller loads it live via ISceneRunner
    private static (DistractionManager manager, IntervalDistractionScheduler scheduler) BuildTree(bool autoStart = false, float minWait = DefaultMinWait, float maxWait = DefaultMaxWait)
    {
        var manager = new DistractionManager();
        var overlay = new Control { Name = "Overlay" };
        var scheduler = new IntervalDistractionScheduler();
        manager.AddChild(overlay);
        manager.AddChild(scheduler);
        manager.InitialFactory = new DistractionFactory(SingleTypeMap());
        scheduler.Set("_minWait", minWait);
        scheduler.Set("_maxWait", maxWait);
        if (autoStart) { scheduler.Set("_autoStart", true); }

        return (manager, scheduler);
    }

    // Host -> SubViewport -> Distraction is BuildHost's exact nesting
    private static Distraction FindHostedDistraction(DistractionManager manager)
    {
        Control overlay = manager.GetNode<Control>("Overlay");
        Node host = overlay.GetChild(0);
        SubViewport viewport = host.GetChildren().OfType<SubViewport>().First();
        return viewport.GetChildren().OfType<Distraction>().First();
    }

    private static Dictionary<DistractionType, PackedScene> SingleTypeMap()
    {
        return new Dictionary<DistractionType, PackedScene>
        {
            { DistractionType.ThrowPaperBall, StubDistraction.Pack() },
        };
    }
}
