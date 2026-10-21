using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// Abstract boilerplate test cases for every concrete JobAnnouncement, checked against the real office -
// the JobTesting shape (same redeclaration rules: override + base for each case, async ones as
// `async ... => await base...`, [GodotExceptionMonitor] at class level, Setup/Teardown redeclared with
// [BeforeTest]/[AfterTest]). A concrete suite whose announcement takes real time to finish (animation,
// timer) is timing-sensitive and also carries the input-sensitivity banner.
// Base-class mechanics (Finish once, scope guards, rollback) are JobAnnouncementLifecycleTesting's
public abstract class JobAnnouncementTesting
{
    protected ISceneRunner runner = null!;
    protected OfficeManager office = null!;

    /// <summary>
    /// A fresh instance of the announcement under test, not in the tree - normally its real scene,
    /// instanced.
    /// </summary>
    protected abstract JobAnnouncement CreateAnnouncement();

    /// <summary>
    /// The office the announcement is checked against, not yet in the tree. Defaults to the real
    /// <c>office.tscn</c>.
    /// </summary>
    protected virtual OfficeManager InstantiateOffice() => GD.Load<PackedScene>(OfficePath).Instantiate<OfficeManager>();

    /// <summary>
    /// How long <see cref="DriveToFinish"/> waits by default for a playing announcement to finish.
    /// </summary>
    protected virtual int FinishTimeoutMs => 5000;

    /// <summary>
    /// Brings a playing announcement to its end. The default just waits (a passive announcement ends by
    /// itself); an interactive one overrides this to do what the player would (e.g. answer the phone).
    /// </summary>
    protected virtual async Task DriveToFinish(JobAnnouncement announcement, Func<bool> finished)
    {
        await OfficeTestKit.WaitUntil(runner, finished, FinishTimeoutMs);
    }

    /// <summary>
    /// Common setup: the office, loaded live (freed with the runner).
    /// </summary>
    public virtual void Setup()
    {
        office = InstantiateOffice();
        runner = ISceneRunner.Load(office, true, true);
    }

    /// <summary>
    /// Common teardown: the office, with anything that entered it, is freed with the runner.
    /// </summary>
    public virtual void Teardown()
    {
        runner.Dispose();
    }

    /// <summary>
    /// An announcement for the current test. Created inside each test, never in Setup (see
    /// <see cref="JobTesting.NewJob"/> for why).
    /// </summary>
    protected JobAnnouncement NewAnnouncement() => AutoFree(CreateAnnouncement())!;

    /// <summary>
    /// The screen named by the announcement's <see cref="JobAnnouncement.TargetScreen"/>, failing with
    /// the name if the office has no such screen.
    /// </summary>
    protected OfficeScreen TargetScreenOf(JobAnnouncement announcement)
    {
        OfficeScreen? screen = office.TryGetScreen(announcement.TargetScreen);
        AssertThat(screen).OverrideFailureMessage($"{announcement.GetType().Name} targets screen '{announcement.TargetScreen}', which the office does not have").IsNotNull();
        return screen!;
    }

    /// <summary>
    /// Tests that the screen the announcement plays on exists in the office
    /// </summary>
    public virtual void TargetScreenExistsInTheOffice()
    {
        TargetScreenOf(NewAnnouncement());
    }

    /// <summary>
    /// The screen × announcement fixture contract: every fixture and anchor the announcement names
    /// exists on its real target screen with the expected type
    /// </summary>
    public virtual void AttachesToItsTargetScreen()
    {
        JobAnnouncement announcement = NewAnnouncement();
        OfficeScreen screen = TargetScreenOf(announcement);

        announcement.Attach(screen);

        AssertThat(announcement.Screen).IsSame(screen);
        announcement.Detach();
    }

    /// <summary>
    /// Tests that the announcement's _Ready works once its parts have been moved out to anchors
    /// (exceptions surfaced by [GodotExceptionMonitor] on the concrete suite)
    /// </summary>
    public virtual void EntersTheTreeAfterAttachWithoutErrors()
    {
        JobAnnouncement announcement = NewAnnouncement();
        announcement.Attach(TargetScreenOf(announcement));

        office.AddChild(announcement);

        AssertThat(announcement.IsInsideTree()).IsTrue();
    }

    /// <summary>
    /// Tests that every clickable part the announcement adds is reached by the screen's input state.
    /// Trivially passes for an announcement that adds no SpriteInteractable
    /// </summary>
    public virtual void PlacedInteractablesFollowTheScreensInputState()
    {
        JobAnnouncement announcement = NewAnnouncement();
        OfficeScreen screen = TargetScreenOf(announcement);
        office.GoToScreen(screen);
        SpriteInteractable[] resident = OfficeTestKit.InteractablesUnder(screen).ToArray();

        announcement.Attach(screen);
        SpriteInteractable[] added = OfficeTestKit.InteractablesUnder(screen).Except(resident).ToArray();

        screen.SetInputEnabled(false);
        AssertThat(added.All(interactable => !interactable.InputPickable)).IsTrue();
        screen.SetInputEnabled(true);
        AssertThat(added.All(interactable => interactable.InputPickable)).IsTrue();
        announcement.Detach();
    }

    /// <summary>
    /// Tests that a playing announcement reaches Finish (the job behind it never starts otherwise),
    /// exactly once. Hosted the way JobManager hosts it: attached, in the tree, OnFinished assigned
    /// before Play
    /// </summary>
    public virtual async Task PlayEventuallyFinishes()
    {
        JobAnnouncement announcement = NewAnnouncement();
        announcement.Attach(TargetScreenOf(announcement));
        office.AddChild(announcement);
        int finished = 0;
        announcement.OnFinished = () => finished++;

        announcement.Play();
        await DriveToFinish(announcement, () => finished > 0);

        AssertThat(finished).OverrideFailureMessage($"{announcement.GetType().Name} did not finish within {FinishTimeoutMs} ms").IsEqual(1);
    }

    /// <summary>
    /// Tests that Detach leaves the target screen exactly as it found it
    /// </summary>
    public virtual async Task DetachGivesBackEverythingItPlaced()
    {
        JobAnnouncement announcement = NewAnnouncement();
        OfficeScreen screen = TargetScreenOf(announcement);
        int baseline = CountDescendants(screen);

        announcement.Attach(screen);
        announcement.Detach();
        await runner.AwaitIdleFrame();

        AssertThat(CountDescendants(screen)).IsEqual(baseline);
    }

    /// <summary>
    /// Tests the _ExitTree safety net through the concrete announcement (fails if an _ExitTree override
    /// forgets base._ExitTree())
    /// </summary>
    public virtual async Task FreeingTheAnnouncementGivesBackItsContent()
    {
        JobAnnouncement announcement = NewAnnouncement();
        OfficeScreen screen = TargetScreenOf(announcement);
        int baseline = CountDescendants(screen);
        announcement.Attach(screen);
        office.AddChild(announcement);

        announcement.QueueFree();
        await runner.AwaitIdleFrame();

        AssertThat(CountDescendants(screen)).IsEqual(baseline);
    }
}
