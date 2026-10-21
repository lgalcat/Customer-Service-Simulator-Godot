using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// IntervalJobScheduler as a child of a real JobManager (job_manager.tscn's topology), over hand-built
// screens and packed stubs. Live tree, for Timer.Start()'s effect on IsStopped(); Timeout is emitted
// directly rather than waited out - this is about the wiring, not real elapsed time (mirrors
// IntervalDistractionSchedulerTesting)
[TestSuite]
[RequireGodotRuntime]
public class IntervalJobSchedulerTesting
{
    // Deliberately outside the [Export] defaults (5-12 s): a WaitTime in this range shows the exports
    // are what feeds the random draw
    private const float CustomMinWait = 1f;
    private const float CustomMaxWait = 2f;

    private ISceneRunner? _runner;
    private JobManager _manager = null!;
    private Timer _timer = null!;

    [BeforeTest]
    public void Setup()
    {
        StubJob.Created.Clear();
    }

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    // root > Screen, Announcer, JobManager > Scheduler, loaded live
    private void Load(PackedScene job, bool autoStart = false)
    {
        Node root = new();
        OfficeScreen screen = BuildScreen("Screen");
        OfficeScreen announcer = BuildScreen("Announcer");
        _manager = new JobManager { InitialFactory = Factory(job), ScreenResolver = Resolver(screen, announcer) };
        IntervalJobScheduler scheduler = new();
        scheduler.Set("_minWait", CustomMinWait);
        scheduler.Set("_maxWait", CustomMaxWait);
        if (autoStart) { scheduler.Set("_autoStart", true); }
        _manager.AddChild(scheduler);
        root.AddChild(screen);
        root.AddChild(announcer);
        root.AddChild(_manager);
        _runner = ISceneRunner.Load(root, true, true);
        // The scheduler builds its Timer in _Ready()
        _timer = scheduler.GetChildren().OfType<Timer>().Single();
    }

    private void AssertTimerArmedWithinBounds()
    {
        AssertThat(_timer.IsStopped()).IsFalse();
        AssertThat(_timer.WaitTime).IsGreaterEqual((double)CustomMinWait);
        AssertThat(_timer.WaitTime).IsLessEqual((double)CustomMaxWait);
    }

    [TestCase]
    public void ReadyThrowsWhenTheManagerIsMissing()
    {
        IntervalJobScheduler scheduler = AutoFree(new IntervalJobScheduler())!;

        AssertThrown(() => scheduler._Ready()).IsInstanceOf<NullReferenceException>()
            .HasMessage("IntervalJobScheduler: missing required node '..'");
    }

    [TestCase]
    public void TheTimerStaysStoppedByDefault()
    {
        Load(StubJob.Pack());

        AssertThat(_timer.IsStopped()).IsTrue();
    }

    [TestCase]
    public void AutoStartArmsTheTimerWithinBounds()
    {
        Load(StubJob.Pack(), autoStart: true);

        AssertTimerArmedWithinBounds();
    }

    [TestCase]
    public void TimeoutRequestsAJob()
    {
        Load(StubJob.Pack());

        _timer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(_manager.Phase).IsEqual(JobPhase.Active);
    }

    // The wait means "rest after a job": it is measured from completion, not from the request
    [TestCase]
    public void JobCompletedReschedulesWithinBounds()
    {
        Load(StubJob.Pack());
        _timer.EmitSignal(Timer.SignalName.Timeout);
        AssertThat(_timer.IsStopped()).IsTrue();

        _manager.ActiveJob!.Complete();

        AssertTimerArmedWithinBounds();
    }

    // An announcement and its job may take as long as they need: neither stage re-arms the timer
    [TestCase]
    public void AnnouncingAndStartingAJobDoNotArmTheTimer()
    {
        PackedScene announcement = StubJobAnnouncement.Pack();
        Load(StubJob.Pack(j => j.AnnouncementScene = announcement));

        _timer.EmitSignal(Timer.SignalName.Timeout);
        AssertThat(_manager.Phase).IsEqual(JobPhase.Announcing);
        AssertThat(_timer.IsStopped()).IsTrue();

        _manager.GetChildren().OfType<StubJobAnnouncement>().Single().Finish();
        AssertThat(_manager.Phase).IsEqual(JobPhase.Active);
        AssertThat(_timer.IsStopped()).IsTrue();
    }

    // A Timeout that finds a job already running (requested by someone else) is simply denied, and the
    // scheduler does not reschedule from it...
    [TestCase]
    public void ATimeoutWhileAJobIsRunningDoesNotReschedule()
    {
        Load(StubJob.Pack());
        _manager.RequestJob();

        _timer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(_timer.IsStopped()).IsTrue();
        AssertThat(StubJob.Created.Count).IsEqual(1);
    }

    // ...because that job's completion reschedules anyway
    [TestCase]
    public void AnExternallyRequestedJobReschedulesOnCompletion()
    {
        Load(StubJob.Pack());
        _manager.RequestJob();
        _timer.EmitSignal(Timer.SignalName.Timeout);
        AssertThat(_timer.IsStopped()).IsTrue();

        _manager.ActiveJob!.Complete();

        AssertTimerArmedWithinBounds();
    }

    // A mis-authored job throws out of RequestJob; inside the Timeout handler the engine only logs it,
    // and nothing reschedules - the chain stops loudly instead of retrying a broken job
    [TestCase]
    public async Task AnAuthoringErrorOnTimeoutStopsTheChain()
    {
        Load(StubJob.Pack(j => j.Target = "Nowhere"));

        _timer.EmitSignal(Timer.SignalName.Timeout);
        await _runner!.AwaitIdleFrame();

        AssertThat(StubJob.Created.Count).IsEqual(1);
        AssertThat(GodotObject.IsInstanceValid(StubJob.Created[0])).IsFalse();
        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
        AssertThat(_timer.IsStopped()).IsTrue();
    }
}
