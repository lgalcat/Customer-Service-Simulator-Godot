using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// The real job_manager.tscn composed with the real office.tscn, as the level will compose them: stub
// jobs injected through InitialFactory, screens resolved through OfficeManager.TryGetScreen. The real
// screens author no anchors yet, so each gets a TestAnchor before tree entry. What is checked here is
// only what composing adds - the mechanisms themselves are the component suites' business
[TestSuite]
[RequireGodotRuntime]
public class JobSystemIntegrationTesting
{
    private ISceneRunner? _runner;
    private EventLog _log = null!;
    private OfficeManager _office = null!;
    private JobManager _jobs = null!;

    // Spins frames and waits on real time (the scheduler test)
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(JobSystemIntegrationTesting));

    [BeforeTest]
    public void Setup()
    {
        _log = new EventLog();
        StubJob.Created.Clear();
    }

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    // root > OfficeManager, JobManager (the real scenes). The JobManager's Scheduler is freed before tree
    // entry unless the test is about it ('schedulerWait' then shrinks its wait to that many seconds and
    // turns it on); every other test thereby shows nothing depends on one
    private void Load(IReadOnlyDictionary<JobType, PackedScene> jobs, float? schedulerWait = null)
    {
        Node root = new();
        _office = InstantiateOfficeWithTestAnchors();
        _jobs = GD.Load<PackedScene>(JobManagerPath).Instantiate<JobManager>();
        _jobs.InitialFactory = new JobFactory(jobs);
        _jobs.ScreenResolver = _office.TryGetScreen;
        Node scheduler = _jobs.GetNode("Scheduler");
        if (schedulerWait is float wait)
        {
            scheduler.Set("_minWait", wait);
            scheduler.Set("_maxWait", wait);
            scheduler.Set("_autoStart", true);
        }
        else
        {
            scheduler.Free();
        }

        root.AddChild(_office);
        root.AddChild(_jobs);
        _runner = ISceneRunner.Load(root, true, true);
        _log.WatchJobs(_jobs);
    }

    private void Load(PackedScene job, float? schedulerWait = null) =>
        Load(new Dictionary<JobType, PackedScene> { { StubTypeA, job } }, schedulerWait);

    private OfficeScreen Screen(string name) => _office.TryGetScreen(name)!;

    private static PackedScene ComputerJob(Action<StubJob>? configure = null) => StubJob.Pack(j =>
    {
        j.Target = "ComputerScreen";
        j.PartAnchor = TestAnchor;
        configure?.Invoke(j);
    }, InteractablePart());

    private Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000) =>
        OfficeTestKit.WaitUntil(_runner!, condition, timeoutMs);

    // ---- Composition ----

    [TestCase]
    public void TheJobManagerSceneIsComposedAsDesigned()
    {
        JobManager manager = AutoFree(GD.Load<PackedScene>(JobManagerPath).Instantiate<JobManager>())!;

        IntervalJobScheduler scheduler = manager.GetNode<IntervalJobScheduler>("Scheduler");
        AssertThat(scheduler.Get("_autoStart").AsBool()).IsFalse();
        AssertThat(scheduler.Get("_managerPath").AsNodePath().ToString()).IsEqual("..");
    }

    // ---- A full cycle across the real screens ----

    // Announcement on MainScreen, job on ComputerScreen, and both screens exactly as they were afterwards
    [TestCase]
    public async Task AnAnnouncedJobRunsAcrossTheRealScreensAndLeavesThemAsFound()
    {
        PackedScene announcement = StubJobAnnouncement.Pack(a =>
        {
            a.Target = "MainScreen";
            a.PartAnchor = TestAnchor;
        }, InteractablePart("AnnouncementPart"));
        Load(ComputerJob(j => j.AnnouncementScene = announcement));
        int mainBaseline = CountDescendants(Screen("MainScreen"));
        int computerBaseline = CountDescendants(Screen("ComputerScreen"));

        _jobs.RequestJob();
        StubJobAnnouncement playing = _jobs.GetChildren().OfType<StubJobAnnouncement>().Single();
        AssertThat(playing.Parts.Single().GetParent()).IsSame(Screen("MainScreen").GetFixture<Node2D>(TestAnchor));

        playing.Finish();
        StubJob job = StubJob.Created.Single();
        AssertThat(job.Parts.Single().GetParent()).IsSame(Screen("ComputerScreen").GetFixture<Node2D>(TestAnchor));

        job.Complete();
        await _runner!.AwaitIdleFrame();

        AssertThat(_log.Entries).ContainsExactly("Announcing@Announcing", "Started@Active", "Completed@Idle");
        AssertThat(CountDescendants(Screen("MainScreen"))).IsEqual(mainBaseline);
        AssertThat(CountDescendants(Screen("ComputerScreen"))).IsEqual(computerBaseline);
    }

    // A placed part is part of its screen for Office's purposes: hidden with it, shown with it, locked
    // with it (the lock MainLevel applies while a Distraction is up)
    [TestCase]
    public void APlacedPartFollowsRealNavigationAndTheInputLock()
    {
        Load(ComputerJob());
        _jobs.RequestJob();
        var part = (SpriteInteractable)StubJob.Created.Single().Parts.Single();

        // Placed on ComputerScreen while MainScreen is current
        AssertThat(part.InputPickable).IsFalse();

        _office.GoToScreen("ComputerScreen");
        AssertThat(part.InputPickable).IsTrue();

        _office.InputLocked = true;
        AssertThat(part.InputPickable).IsFalse();

        _office.InputLocked = false;
        AssertThat(part.InputPickable).IsTrue();

        _office.GoToScreen("MainScreen");
        AssertThat(part.InputPickable).IsFalse();
    }

    // ---- The scene's own scheduler ----

    [TestCase]
    public async Task TheScenesSchedulerChainsJobsAfterEachCompletion()
    {
        Load(ComputerJob(), schedulerWait: 0.05f);

        AssertThat(await WaitUntil(() => _jobs.Phase == JobPhase.Active)).IsTrue();
        _jobs.ActiveJob!.Complete();
        AssertThat(await WaitUntil(() => _jobs.Phase == JobPhase.Active)).IsTrue();

        AssertThat(StubJob.Created.Count).IsEqual(2);
    }

    // ---- Authoring errors ----

    // A job naming an anchor the real screen doesn't expose fails at start, takes back what it placed and
    // leaves the office as it was; the manager stays usable
    [TestCase]
    public async Task AMisAuthoredJobLeavesTheRealOfficeUntouched()
    {
        Load(new Dictionary<JobType, PackedScene>
        {
            { StubTypeA, ComputerJob(j => j.PartAnchor = "Monitor") },
            { StubTypeB, ComputerJob() },
        });
        OfficeScreen[] screens = _office.GetNode("ScreenLayer").GetChildren().OfType<OfficeScreen>().ToArray();
        int[] baselines = screens.Select(CountDescendants).ToArray();

        AssertThrown(() => _jobs.RequestJob(StubTypeA)).IsInstanceOf<ArgumentException>();
        await _runner!.AwaitIdleFrame();

        AssertThat(screens.Select(CountDescendants).ToArray()).IsEqual(baselines);
        AssertThat(_log.Entries).IsEmpty();
        AssertThat(_jobs.RequestJob(StubTypeB)).IsTrue();
    }
}
