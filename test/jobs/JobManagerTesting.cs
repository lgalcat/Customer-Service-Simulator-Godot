using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// JobManager over hand-built screens ('Screen' for jobs, 'Announcer' for announcements) reached through a
// resolver lambda - no OfficeManager, which is the point: the manager depends on screens only. Packed
// StubJobs/StubJobAnnouncements configured through their [Export] knobs; StubJob.Created is the handle
// on jobs the manager holds privately. Everything runs in a live tree so jobs really enter it and
// deferred frees really happen (one idle frame, nothing timing-sensitive). The real composed scenes are
// covered by JobSystemIntegrationTesting
[TestSuite]
[RequireGodotRuntime]
public class JobManagerTesting
{
    private ISceneRunner? _runner;
    private EventLog _log = null!;
    private JobManager _manager = null!;
    private OfficeScreen _screen = null!;
    private OfficeScreen _announcer = null!;

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

    // root > Screen, Announcer, JobManager, loaded live; manager events go to _log with their phase
    private void Load(params (JobType Type, PackedScene Scene)[] entries)
    {
        Node root = new();
        _screen = BuildScreen("Screen");
        _announcer = BuildScreen("Announcer");
        _manager = new JobManager
        {
            Name = "JobManager",
            InitialFactory = Factory(entries),
            ScreenResolver = Resolver(_screen, _announcer),
        };
        root.AddChild(_screen);
        root.AddChild(_announcer);
        root.AddChild(_manager);
        _runner = ISceneRunner.Load(root, true, true);
        _log.WatchJobs(_manager);
    }

    private void Load(PackedScene job) => Load((StubTypeA, job));

    // A stub job with one clickable part, targeting 'Screen' unless configured otherwise
    private static PackedScene Job(Action<StubJob>? configure = null) => StubJob.Pack(configure, InteractablePart());

    // A stub job whose scene declares an announcement (one part, targeting 'Announcer')
    private static PackedScene AnnouncedJob(Action<StubJobAnnouncement>? announcement = null, Action<StubJob>? job = null)
    {
        PackedScene announcementScene = StubJobAnnouncement.Pack(announcement, InteractablePart());
        return Job(j =>
        {
            j.AnnouncementScene = announcementScene;
            job?.Invoke(j);
        });
    }

    private static Node2D Anchor(OfficeScreen screen) => screen.GetFixture<Node2D>("Anchor");

    private static StubJob OnlyJob => StubJob.Created.Single();

    private StubJobAnnouncement HostedAnnouncement() => _manager.GetChildren().OfType<StubJobAnnouncement>().Single();

    // For tests ending mid-announcement. The pending job is held outside the tree until the manager frees
    // it at teardown, after gdUnit4's orphan check for the test; registering it frees it within the
    // test's own stage instead (the manager's _ExitTree skips a job that is already gone)
    private static void AutoFreePendingJob() => AutoFree(OnlyJob);

    // ---- Request guards ----

    [TestCase]
    public void RequestWithoutAScreenResolverThrowsBeforeCreatingAnything()
    {
        Load(Job());
        _manager.ScreenResolver = null;

        AssertThrown(() => _manager.RequestJob()).IsInstanceOf<InvalidOperationException>()
            .HasMessage("JobManager: ScreenResolver must be set before requesting a job");
        AssertThat(StubJob.Created).IsEmpty();
        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
    }

    // Only one job may ever be in flight
    [TestCase]
    public void RequestWhileActiveIsRejectedWithoutSideEffects()
    {
        Load(Job());
        _manager.RequestJob();
        _log.Clear();

        bool second = _manager.RequestJob();

        AssertThat(second).IsFalse();
        AssertThat(StubJob.Created.Count).IsEqual(1);
        AssertThat(_manager.ActiveJob).IsSame(OnlyJob);
        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void RequestWhileAnnouncingIsRejectedWithoutSideEffects()
    {
        Load(AnnouncedJob());
        _manager.RequestJob();
        AutoFreePendingJob();
        _log.Clear();

        bool second = _manager.RequestJob();

        AssertThat(second).IsFalse();
        AssertThat(StubJob.Created.Count).IsEqual(1);
        AssertThat(_manager.GetChildren().OfType<StubJobAnnouncement>().Count()).IsEqual(1);
        AssertThat(_manager.Phase).IsEqual(JobPhase.Announcing);
        AssertThat(_log.Entries).IsEmpty();
    }

    // ---- Direct start (no announcement) ----

    [TestCase]
    public void AJobWithoutAnAnnouncementStartsStraightAway()
    {
        Load(Job());

        bool requested = _manager.RequestJob();

        AssertThat(requested).IsTrue();
        AssertThat(_manager.Phase).IsEqual(JobPhase.Active);
        AssertThat(_manager.ActiveJob).IsSame(OnlyJob);
        AssertThat(OnlyJob.GetParent()).IsSame(_manager);
        AssertThat(OnlyJob.Screen).IsSame(_screen);
        AssertThat(OnlyJob.Parts.Single().GetParent()).IsSame(Anchor(_screen));
        AssertThat(_log.Entries).ContainsExactly("Started@Active");
    }

    // Upstream may act on the job from JobStarted: by then it is attached, in the tree and ready
    [TestCase]
    public void JobStartedSeesTheJobAttachedInTheTreeAndReady()
    {
        Load(Job());
        bool? attached = null, inTree = null;
        int readyCalls = -1;
        _manager.JobStarted += () =>
        {
            attached = _manager.ActiveJob?.Screen != null;
            inTree = _manager.ActiveJob?.IsInsideTree();
            readyCalls = OnlyJob.ReadyCalls;
        };

        _manager.RequestJob();

        AssertThat(attached).IsEqual(true);
        AssertThat(inTree).IsEqual(true);
        AssertThat(readyCalls).IsEqual(1);
    }

    // Attached while still outside the tree: a part entering it under the job and then being moved would
    // fire exit/enter, and components that subscribe in _Ready and unsubscribe in _ExitTree
    // (InteractableFeedback) would lose their subscriptions
    [TestCase]
    public void PartsEnterTheTreeOnlyOnceAtTheirAnchor()
    {
        Load(StubJob.Pack(null, new TreeEntryProbe { Name = "Probe" }));

        _manager.RequestJob();

        AssertThat(((TreeEntryProbe)OnlyJob.Parts.Single()).EnteredUnder).ContainsExactly("Anchor");
    }

    [TestCase]
    public void RequestDefaultsToTheBaseDifficulty()
    {
        Load(Job());

        _manager.RequestJob();

        AssertThat(OnlyJob.Difficulty).IsEqual(_manager.Get("_baseDifficulty").AsInt32());
    }

    [TestCase]
    public void RequestPassesAnExplicitTypeAndDifficultyThrough()
    {
        Load((StubTypeA, Job()), (StubTypeB, Job(j => j.Target = "Announcer")));

        _manager.RequestJob(StubTypeB, 4);

        AssertThat(OnlyJob.Screen).IsSame(_announcer);
        AssertThat(OnlyJob.Difficulty).IsEqual(4);
    }

    // ---- Completion ----

    [TestCase]
    public void CompletingTheActiveJobReturnsTheManagerToIdle()
    {
        Load(Job());
        _manager.RequestJob();

        OnlyJob.Complete();

        AssertThat(_log.Entries).ContainsExactly("Started@Active", "Completed@Idle");
        AssertThat(_manager.ActiveJob).IsNull();
    }

    [TestCase]
    public async Task CompletionTakesTheJobsContentOutAndFreesIt()
    {
        Load(Job());
        _manager.RequestJob();
        StubJob job = OnlyJob;
        Node2D part = job.Parts.Single();

        job.Complete();
        await _runner!.AwaitIdleFrame();

        AssertThat(GodotObject.IsInstanceValid(job)).IsFalse();
        AssertThat(GodotObject.IsInstanceValid(part)).IsFalse();
        AssertThat(Anchor(_screen).GetChildCount()).IsEqual(0);
    }

    // The manager consumes the job's callback; one kept from before completion must not be able to
    // complete whatever job is active by the time it fires
    [TestCase]
    public void AStaleCompletionCallbackIsIgnored()
    {
        Load(Job());
        _manager.RequestJob();
        Action stale = OnlyJob.OnCompleted!;
        OnlyJob.Complete();
        _manager.RequestJob();
        _log.Clear();

        stale();

        AssertThat(_log.Entries).IsEmpty();
        AssertThat(_manager.ActiveJob).IsSame(StubJob.Created[1]);
    }

    // A level policy may chain the next job immediately: the manager is fully Idle inside JobCompleted
    [TestCase]
    public void ARequestFromInsideJobCompletedStartsTheNextJob()
    {
        Load(Job());
        bool? chained = null;
        _manager.JobCompleted += () => chained = _manager.RequestJob();
        _manager.RequestJob();

        OnlyJob.Complete();

        AssertThat(chained).IsEqual(true);
        AssertThat(_log.Entries).ContainsExactly("Started@Active", "Completed@Idle", "Started@Active");
        AssertThat(_manager.ActiveJob).IsSame(StubJob.Created[1]);
    }

    [TestCase]
    public async Task RequestSucceedsAgainOnceTheCompletedJobIsGone()
    {
        Load(Job());
        _manager.RequestJob();
        OnlyJob.Complete();
        await _runner!.AwaitIdleFrame();

        bool again = _manager.RequestJob();

        AssertThat(again).IsTrue();
        AssertThat(_manager.Phase).IsEqual(JobPhase.Active);
    }

    // ---- Announcement flow ----

    // The job is created (and set up) but held back: not attached, not in the tree, not active
    [TestCase]
    public void AJobWithAnAnnouncementPlaysTheAnnouncementFirst()
    {
        Load(AnnouncedJob());

        bool requested = _manager.RequestJob();
        AutoFreePendingJob();

        AssertThat(requested).IsTrue();
        AssertThat(_manager.Phase).IsEqual(JobPhase.Announcing);
        AssertThat(_log.Entries).ContainsExactly("Announcing@Announcing");
        StubJobAnnouncement announcement = HostedAnnouncement();
        AssertThat(announcement.Screen).IsSame(_announcer);
        AssertThat(announcement.PlayCalls).IsEqual(1);
        AssertThat(announcement.Parts.Single().GetParent()).IsSame(Anchor(_announcer));
        AssertThat(OnlyJob.Screen).IsNull();
        AssertThat(OnlyJob.GetParent()).IsNull();
        AssertThat(_manager.ActiveJob).IsNull();
    }

    // "About to play": attached and hosted, not yet playing
    [TestCase]
    public void JobAnnouncingFiresBeforeTheAnnouncementPlays()
    {
        Load(AnnouncedJob());
        int playCallsAtAnnouncing = -1;
        bool? attachedAtAnnouncing = null;
        _manager.JobAnnouncing += () =>
        {
            playCallsAtAnnouncing = HostedAnnouncement().PlayCalls;
            attachedAtAnnouncing = HostedAnnouncement().Screen != null;
        };

        _manager.RequestJob();
        AutoFreePendingJob();

        AssertThat(playCallsAtAnnouncing).IsEqual(0);
        AssertThat(attachedAtAnnouncing).IsEqual(true);
    }

    [TestCase]
    public async Task FinishingTheAnnouncementRemovesItAndStartsTheJob()
    {
        Load(AnnouncedJob());
        _manager.RequestJob();
        StubJobAnnouncement announcement = HostedAnnouncement();
        Node2D announcementPart = announcement.Parts.Single();

        announcement.Finish();

        AssertThat(_log.Entries).ContainsExactly("Announcing@Announcing", "Started@Active");
        AssertThat(announcement.Screen).IsNull();
        AssertThat(_manager.ActiveJob).IsSame(OnlyJob);
        AssertThat(OnlyJob.Screen).IsSame(_screen);
        AssertThat(OnlyJob.IsInsideTree()).IsTrue();
        await _runner!.AwaitIdleFrame();
        AssertThat(GodotObject.IsInstanceValid(announcement)).IsFalse();
        AssertThat(GodotObject.IsInstanceValid(announcementPart)).IsFalse();
    }

    // OnFinished is assigned before Play(), so an announcement that has nothing to wait for still works
    [TestCase]
    public void AnAnnouncementFinishingInsidePlayStartsTheJobStraightAway()
    {
        Load(AnnouncedJob(a => a.FinishOnPlay = true));

        _manager.RequestJob();

        AssertThat(_log.Entries).ContainsExactly("Announcing@Announcing", "Started@Active");
        AssertThat(_manager.ActiveJob).IsSame(OnlyJob);
    }

    [TestCase]
    public void AStaleFinishCallbackIsIgnored()
    {
        Load(AnnouncedJob());
        _manager.RequestJob();
        StubJobAnnouncement announcement = HostedAnnouncement();
        Action stale = announcement.OnFinished!;
        announcement.Finish();
        _log.Clear();

        stale();

        AssertThat(_log.Entries).IsEmpty();
        AssertThat(StubJob.Created.Count).IsEqual(1);
    }

    // ---- Failures: authoring errors ----
    // Each one propagates, leaves the manager Idle with no event fired, and takes back whatever was
    // partly set up

    [TestCase]
    public async Task AJobTargetingAnUnknownScreenFailsCleanly()
    {
        Load(Job(j => j.Target = "Nowhere"));

        AssertThrown(() => _manager.RequestJob()).IsInstanceOf<InvalidOperationException>()
            .HasMessage("JobManager: StubJob targets screen 'Nowhere', which does not exist");

        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
        AssertThat(_log.Entries).IsEmpty();
        await _runner!.AwaitIdleFrame();
        AssertThat(GodotObject.IsInstanceValid(OnlyJob)).IsFalse();
    }

    [TestCase]
    public async Task AJobFailingInOnAttachedFailsCleanly()
    {
        Load(Job(j => j.ThrowAfterPlacing = true));

        AssertThrown(() => _manager.RequestJob()).IsInstanceOf<InvalidOperationException>().HasMessage("StubJob: ThrowAfterPlacing");

        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
        AssertThat(_log.Entries).IsEmpty();
        await _runner!.AwaitIdleFrame();
        AssertThat(GodotObject.IsInstanceValid(OnlyJob)).IsFalse();
        AssertThat(Anchor(_screen).GetChildCount()).IsEqual(0);
    }

    [TestCase]
    public async Task AJobUsingAnUnknownAnchorFailsCleanly()
    {
        Load(Job(j => j.PartAnchor = "Nope"));

        AssertThrown(() => _manager.RequestJob()).IsInstanceOf<ArgumentException>();

        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
        AssertThat(_log.Entries).IsEmpty();
        await _runner!.AwaitIdleFrame();
        AssertThat(GodotObject.IsInstanceValid(OnlyJob)).IsFalse();
    }

    [TestCase]
    public async Task AnAnnouncementTargetingAnUnknownScreenFailsCleanly()
    {
        Load(AnnouncedJob(a => a.Target = "Nowhere"));

        AssertThrown(() => _manager.RequestJob()).IsInstanceOf<InvalidOperationException>()
            .HasMessage("JobManager: StubJobAnnouncement targets screen 'Nowhere', which does not exist");

        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
        AssertThat(_log.Entries).IsEmpty();
        await _runner!.AwaitIdleFrame();
        AssertThat(GodotObject.IsInstanceValid(OnlyJob)).IsFalse();
        AssertThat(_manager.GetChildCount()).IsEqual(0);
    }

    [TestCase]
    public async Task AnAnnouncementFailingInOnAttachedFailsCleanly()
    {
        Load(AnnouncedJob(a => a.ThrowAfterPlacing = true));

        AssertThrown(() => _manager.RequestJob()).IsInstanceOf<InvalidOperationException>().HasMessage("StubJobAnnouncement: ThrowAfterPlacing");

        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
        AssertThat(_log.Entries).IsEmpty();
        await _runner!.AwaitIdleFrame();
        AssertThat(GodotObject.IsInstanceValid(OnlyJob)).IsFalse();
        AssertThat(_manager.GetChildCount()).IsEqual(0);
        AssertThat(Anchor(_announcer).GetChildCount()).IsEqual(0);
    }

    // The failure surfaces from Finish(), i.e. inside whatever ended the announcement. Pinned as it
    // stands: JobAnnouncing was already fired and no event marks the abort, so upstream policy that
    // reacted to JobAnnouncing is never told it is over
    [TestCase]
    public async Task AJobFailingToStartAfterItsAnnouncementSurfacesFromFinish()
    {
        Load(AnnouncedJob(job: j => j.Target = "Nowhere"));
        _manager.RequestJob();
        StubJobAnnouncement announcement = HostedAnnouncement();

        AssertThrown(() => announcement.Finish()).IsInstanceOf<InvalidOperationException>();

        AssertThat(_manager.Phase).IsEqual(JobPhase.Idle);
        AssertThat(_log.Entries).ContainsExactly("Announcing@Announcing");
        await _runner!.AwaitIdleFrame();
        AssertThat(GodotObject.IsInstanceValid(OnlyJob)).IsFalse();
        AssertThat(GodotObject.IsInstanceValid(announcement)).IsFalse();
    }

    [TestCase]
    public async Task TheManagerAcceptsANewRequestAfterAFailure()
    {
        Load((StubTypeA, Job(j => j.Target = "Nowhere")), (StubTypeB, Job()));
        AssertThrown(() => _manager.RequestJob(StubTypeA)).IsInstanceOf<InvalidOperationException>();
        await _runner!.AwaitIdleFrame();

        bool requested = _manager.RequestJob(StubTypeB);

        AssertThat(requested).IsTrue();
        AssertThat(_log.Entries).ContainsExactly("Started@Active");
        AssertThat(GodotObject.IsInstanceValid(StubJob.Created[0])).IsFalse();
    }

    // ---- Completing before being started ----
    // A job that finishes during attach or _Ready had nobody to tell yet; the manager still reports a
    // complete start/complete pair

    [TestCase]
    public void AJobCompletingDuringAttachIsReportedStartedThenCompleted()
    {
        Load(Job(j => j.CompleteOnAttach = true));

        _manager.RequestJob();

        AssertThat(_log.Entries).ContainsExactly("Started@Active", "Completed@Idle");
        AssertThat(_manager.ActiveJob).IsNull();
    }

    [TestCase]
    public void AJobCompletingInReadyIsReportedStartedThenCompleted()
    {
        Load(Job(j => j.CompleteOnReady = true));

        _manager.RequestJob();

        AssertThat(_log.Entries).ContainsExactly("Started@Active", "Completed@Idle");
        AssertThat(_manager.ActiveJob).IsNull();
    }

    // Pinned as it stands: the rollback covers OnAttached only. An exception in the job's _Ready is
    // raised inside AddChild, where the engine logs it instead of propagating it, so the job is started
    // anyway, half-initialised. [GodotExceptionMonitor] surfaces that logged exception to the test
    [TestCase]
    [GodotExceptionMonitor]
    [ThrowsException(typeof(InvalidOperationException), "StubJob: ThrowOnReady")]
    public void AnExceptionInTheJobsReadyDoesNotPreventTheStart()
    {
        Load(Job(j => j.ThrowOnReady = true));

        bool requested = _manager.RequestJob();

        AssertThat(requested).IsTrue();
        AssertThat(_manager.Phase).IsEqual(JobPhase.Active);
        AssertThat(_log.Entries).ContainsExactly("Started@Active");
    }

    // ---- Manager teardown ----

    // The pending job is the one thing the manager owns that is not its child
    [TestCase]
    public async Task FreeingTheManagerWhileAnnouncingFreesThePendingJob()
    {
        Load(AnnouncedJob());
        _manager.RequestJob();
        StubJob pending = OnlyJob;

        _manager.QueueFree();
        await _runner!.AwaitIdleFrame();

        AssertThat(GodotObject.IsInstanceValid(pending)).IsFalse();
        AssertThat(Anchor(_announcer).GetChildCount()).IsEqual(0);
    }

    [TestCase]
    public async Task FreeingTheManagerWhileActiveTakesTheJobsContentOut()
    {
        Load(Job());
        _manager.RequestJob();
        Node2D part = OnlyJob.Parts.Single();

        _manager.QueueFree();
        await _runner!.AwaitIdleFrame();

        AssertThat(GodotObject.IsInstanceValid(part)).IsFalse();
        AssertThat(Anchor(_screen).GetChildCount()).IsEqual(0);
    }
}
