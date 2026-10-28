using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// Abstract boilerplate test cases for every concrete Job, checked against the office it will really be
// played in. Limitations on the GdUnit test discovery pipeline prevent inherited methods to register as
// testcases: every concrete suite redeclares each case below as an override calling base (async ones as
// `async ... => await base...`: gdUnit4 reads async-ness off the override itself), carries
// [GodotExceptionMonitor] at class level (EntersTheTreeAfterAttachWithoutErrors depends on it), and
// redeclares Setup/Teardown with [BeforeTest]/[AfterTest].
// Only what an implementation can get wrong lives here; the mechanics every job inherits unchanged
// (Complete, the scope guards, rollback) are JobLifecycleTesting's
public abstract class JobTesting
{
    protected ISceneRunner runner = null!;
    protected OfficeManager office = null!;

    /// <summary>
    /// A fresh instance of the job under test, not set up and not in the tree - normally its real
    /// scene, instanced.
    /// </summary>
    protected abstract Job CreateJob();

    /// <summary>
    /// The office the job is checked against, not yet in the tree. Defaults to the real
    /// <c>office.tscn</c>: the point of the fixture contract is that every fixture and anchor a job
    /// names exists, with the right type, on the screen it will actually be played on.
    /// </summary>
    protected virtual OfficeManager InstantiateOffice() => GD.Load<PackedScene>(OfficePath).Instantiate<OfficeManager>();

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
    /// A job for the current test, set up with <paramref name="difficulty"/>. Created inside each test,
    /// never in Setup: gdUnit4 reports nodes made in [BeforeTest] that move into the tree during the test
    /// (as attaching does with a job's parts) as orphans.
    /// </summary>
    protected Job NewJob(int difficulty = 1)
    {
        Job job = AutoFree(CreateJob())!;
        job.Setup(difficulty);
        return job;
    }

    /// <summary>
    /// The screen named by <paramref name="job"/>'s <see cref="Job.TargetScreen"/>, failing with the
    /// name if the office has no such screen.
    /// </summary>
    protected OfficeScreen TargetScreenOf(Job job)
    {
        OfficeScreen? screen = office.TryGetScreen(job.TargetScreen);
        AssertThat(screen).OverrideFailureMessage($"{job.GetType().Name} targets screen '{job.TargetScreen}', which the office does not have").IsNotNull();
        return screen!;
    }

    /// <summary>
    /// Tests that Setup assigns the given difficulty, outside the tree (its contract: called before
    /// tree entry, so no GetTree())
    /// </summary>
    public virtual void SetupAssignsDifficulty()
    {
        Job job = NewJob(3);

        AssertThat(job.Difficulty).IsEqual(3);
    }

    /// <summary>
    /// Tests that the screen the job targets exists in the office
    /// </summary>
    public virtual void TargetScreenExistsInTheOffice()
    {
        TargetScreenOf(NewJob());
    }

    /// <summary>
    /// The screen × job fixture contract: attaching runs the job's OnAttached against the real target
    /// screen, so every fixture and anchor it names must exist there with the expected type (a
    /// mismatch throws here, listing what the screen exposes)
    /// </summary>
    public virtual void AttachesToItsTargetScreen()
    {
        Job job = NewJob();
        OfficeScreen screen = TargetScreenOf(job);

        job.Attach(screen);

        AssertThat(job.Screen).IsSame(screen);
        job.Detach();
    }

    /// <summary>
    /// Tests that the job's tree-dependent initialisation (_Ready) works once its parts have been moved
    /// out to anchors - e.g. no GetNode path to a part that is no longer under the job. Exceptions
    /// there are only logged by the engine; [GodotExceptionMonitor] on the concrete suite surfaces them
    /// </summary>
    public virtual void EntersTheTreeAfterAttachWithoutErrors()
    {
        Job job = NewJob();
        job.Attach(TargetScreenOf(job));

        office.AddChild(job);

        AssertThat(job.IsInsideTree()).IsTrue();
    }

    /// <summary>
    /// What attaching <paramref name="job"/> adds under <paramref name="screen"/>: every node that was
    /// not there before.
    /// </summary>
    protected static Node[] AttachAndCollectAdded(Job job, OfficeScreen screen)
    {
        HashSet<Node> resident = DescendantsOf(screen).ToHashSet();
        job.Attach(screen);
        return DescendantsOf(screen).Where(node => !resident.Contains(node)).ToArray();
    }

    private static string Names(IEnumerable<Node> nodes) => string.Join(", ", nodes.Select(node => node.Name.ToString()));

    /// <summary>
    /// Tests that every interactive part the job adds is reached by the screen's input state (the input
    /// lock a Distraction imposes, navigation): content added through Place is, content added behind the
    /// screen's back is not. A SpriteInteractable must stop being pickable; a GUI Control interactable must
    /// stop taking the mouse and focus (the Distraction overlay blocks the mouse, not keystrokes to a
    /// focused field; FocusMode None also makes the engine release focus held). Trivially passes for a
    /// job that adds neither
    /// </summary>
    public virtual void PlacedInteractablesFollowTheScreensInputState()
    {
        Job job = NewJob();
        OfficeScreen screen = TargetScreenOf(job);
        office.GoToScreen(screen);

        Node[] added = AttachAndCollectAdded(job, screen);
        SpriteInteractable[] sprites = added.OfType<SpriteInteractable>().ToArray();
        Control[] controls = added.OfType<Control>().Where(control => control is IOfficeInteractable).ToArray();

        screen.SetInputEnabled(false);
        AssertThat(Names(sprites.Where(sprite => sprite.InputPickable))).OverrideFailureMessage("still pickable while input is off").IsEmpty();
        AssertThat(Names(controls.Where(control => control.MouseFilter != Control.MouseFilterEnum.Ignore || control.FocusMode != Control.FocusModeEnum.None)))
            .OverrideFailureMessage("still taking the mouse or focus while input is off").IsEmpty();

        screen.SetInputEnabled(true);
        AssertThat(Names(sprites.Where(sprite => !sprite.InputPickable))).OverrideFailureMessage("not pickable again once input is on").IsEmpty();
        AssertThat(Names(controls.Where(control => control.MouseFilter == Control.MouseFilterEnum.Ignore)))
            .OverrideFailureMessage("not taking the mouse again once input is on").IsEmpty();
        job.Detach();
    }

    /// <summary>
    /// Tests that the GUI Controls the job adds which take no input of their own don't block the mouse: a
    /// Stop Control marks every click on it handled, so a hotspot beneath it could never be clicked (Pass
    /// and Ignore don't block). Interactive Controls are the previous case's. Trivially passes for a job
    /// that adds no Control
    /// </summary>
    public virtual void ControlsThatTakeNoInputLetClicksThrough()
    {
        Job job = NewJob();
        OfficeScreen screen = TargetScreenOf(job);

        Node[] added = AttachAndCollectAdded(job, screen);
        IEnumerable<Control> blocking = added.OfType<Control>()
            .Where(control => control is not IOfficeInteractable && control.MouseFilter == Control.MouseFilterEnum.Stop);

        AssertThat(Names(blocking)).OverrideFailureMessage("decorative Controls with mouse_filter Stop").IsEmpty();
        job.Detach();
    }

    /// <summary>
    /// Tests that Detach leaves the target screen exactly as it found it: everything placed is taken
    /// back out (counted once the deferred frees have run)
    /// </summary>
    public virtual async Task DetachGivesBackEverythingItPlaced()
    {
        Job job = NewJob();
        OfficeScreen screen = TargetScreenOf(job);
        int baseline = CountDescendants(screen);

        job.Attach(screen);
        job.Detach();
        await runner.AwaitIdleFrame();

        AssertThat(CountDescendants(screen)).IsEqual(baseline);
    }

    /// <summary>
    /// Tests the _ExitTree safety net through the concrete job: a job freed without going through the
    /// manager still gives back what it placed (fails if an _ExitTree override forgets base._ExitTree())
    /// </summary>
    public virtual async Task FreeingTheJobGivesBackItsContent()
    {
        Job job = NewJob();
        OfficeScreen screen = TargetScreenOf(job);
        int baseline = CountDescendants(screen);
        job.Attach(screen);
        office.AddChild(job);

        job.QueueFree();
        await runner.AwaitIdleFrame();

        AssertThat(CountDescendants(screen)).IsEqual(baseline);
    }

    /// <summary>
    /// Tests that the announcement the job's scene declares, if any, is a JobAnnouncement that can be
    /// played on its own target screen and gives that screen back afterwards. Trivially passes for a
    /// job without one; the announcement's own contract is JobAnnouncementTesting's
    /// </summary>
    public virtual async Task AnnouncementSceneIsPlayable()
    {
        Job job = NewJob();
        if (job.AnnouncementScene == null) { return; }

        Node instance = AutoFree(job.AnnouncementScene.Instantiate())!;
        AssertThat(instance).IsInstanceOf<JobAnnouncement>();
        var announcement = (JobAnnouncement)instance;
        OfficeScreen? screen = office.TryGetScreen(announcement.TargetScreen);
        AssertThat(screen).OverrideFailureMessage($"{announcement.GetType().Name} targets screen '{announcement.TargetScreen}', which the office does not have").IsNotNull();
        int baseline = CountDescendants(screen!);

        announcement.Attach(screen!);
        announcement.Detach();
        await runner.AwaitIdleFrame();

        AssertThat(CountDescendants(screen!)).IsEqual(baseline);
    }
}
