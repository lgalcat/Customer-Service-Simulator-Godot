using System;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// The concrete mechanics every Job inherits from the base class (attach/detach, the scope guards,
// rollback, Complete, the _ExitTree safety net), over a directly constructed StubJob and a hand-built
// screen. None of it varies per job, so it is tested once here - JobTesting holds what a concrete job
// can get wrong. No SceneTree except for the _ExitTree case
[TestSuite]
[RequireGodotRuntime]
public class JobLifecycleTesting
{
    private EventLog _log = null!;
    private OfficeScreen _screen = null!;
    private Node2D _anchor = null!;

    [BeforeTest]
    public void Setup()
    {
        _log = new EventLog();
        _screen = AutoFree(BuildScreen("S"))!;
        // Manual _Ready() call - no SceneTree to auto-trigger it (applies to every test below)
        _screen._Ready();
        _anchor = _screen.GetFixture<Node2D>("Anchor");
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // An authored part, carrying a StubInteractable so its removal is journalled
    private Node2D Part(string label)
    {
        Node2D part = new() { Name = label };
        part.AddChild(new StubInteractable { Label = label, Journal = _log });
        return part;
    }

    private static StubJob JobWithParts(params Node2D[] parts)
    {
        StubJob job = AutoFree(new StubJob())!;
        foreach (Node2D part in parts) { job.AddChild(part); }
        return job;
    }

    // ---- Attach / Detach ----

    [TestCase]
    public void ScreenIsSetOnlyWhileAttached()
    {
        StubJob job = JobWithParts();
        AssertThat(job.Screen).IsNull();

        job.Attach(_screen);
        AssertThat(job.Screen).IsSame(_screen);

        job.Detach();
        AssertThat(job.Screen).IsNull();
    }

    [TestCase]
    public void AttachMovesAuthoredPartsToTheScreensAnchor()
    {
        Node2D part = AutoFree(Part("P1"))!;
        StubJob job = JobWithParts(part);

        job.Attach(_screen);

        AssertThat(part.GetParent()).IsSame(_anchor);
        AssertThat(job.GetChildCount()).IsEqual(0);
    }

    [TestCase]
    public void AttachingTwiceThrowsAndKeepsTheFirstAttachment()
    {
        StubJob job = JobWithParts();
        job.Attach(_screen);
        OfficeScreen other = AutoFree(BuildScreen("Other"))!;
        other._Ready();

        AssertThrown(() => job.Attach(other)).IsInstanceOf<InvalidOperationException>().HasMessage("StubJob: already attached");
        AssertThat(job.Screen).IsSame(_screen);
    }

    // Everything OnAttached did before failing is undone - cleanups and placements alike - and the job
    // is left as if never attached
    [TestCase]
    public void AFailingOnAttachedTakesBackWhatItDidAndRethrows()
    {
        Node2D part = AutoFree(Part("P1"))!;
        StubJob job = JobWithParts(part);
        job.ThrowAfterPlacing = true;
        job.AttachHook = j => j.AddUndo(() => _log.Add("undo"));

        AssertThrown(() => job.Attach(_screen)).IsInstanceOf<InvalidOperationException>().HasMessage("StubJob: ThrowAfterPlacing");

        AssertThat(_log.Entries).ContainsExactly("P1:SetInteractable(true)", "undo", "P1:SetInteractable(false)");
        AssertThat(part.IsQueuedForDeletion()).IsTrue();
        AssertThat(job.Screen).IsNull();
    }

    [TestCase]
    public void AJobThatFailedToAttachCanBeAttachedAgain()
    {
        StubJob job = JobWithParts();
        job.ThrowAfterPlacing = true;
        AssertThrown(() => job.Attach(_screen)).IsInstanceOf<InvalidOperationException>();
        job.ThrowAfterPlacing = false;

        job.Attach(_screen);

        AssertThat(job.Screen).IsSame(_screen);
    }

    [TestCase]
    public void DetachRunsCleanupsAndRemovesPartsOnce()
    {
        StubJob job = JobWithParts(AutoFree(Part("P1"))!);
        job.AttachHook = j => j.AddUndo(() => _log.Add("undo"));
        job.Attach(_screen);
        _log.Clear();

        job.Detach();
        job.Detach();

        AssertThat(_log.Entries).ContainsExactly("undo", "P1:SetInteractable(false)");
    }

    [TestCase]
    public void DetachWithoutAttachIsSafe()
    {
        StubJob job = JobWithParts();

        job.Detach();

        AssertThat(job.Screen).IsNull();
    }

    // ---- Scope guards ----

    [TestCase]
    public void PlaceAndAddCleanupBeforeAttachThrow()
    {
        StubJob job = JobWithParts();
        Node2D part = AutoFree(Part("P1"))!;

        AssertThrown(() => job.PlacePart(part, "Anchor")).IsInstanceOf<InvalidOperationException>()
            .HasMessage("StubJob: not attached to a screen yet (only valid from OnAttached onwards)");
        AssertThrown(() => job.AddUndo(() => { })).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public void PlaceAndAddCleanupAfterDetachThrow()
    {
        StubJob job = JobWithParts();
        job.Attach(_screen);
        job.Detach();
        Node2D part = AutoFree(Part("P1"))!;

        AssertThrown(() => job.PlacePart(part, "Anchor")).IsInstanceOf<InvalidOperationException>();
        AssertThrown(() => job.AddUndo(() => { })).IsInstanceOf<InvalidOperationException>();
    }

    // The reason AddCleanup exists: a C# += on a resident emitter's custom signal outlives the
    // (transient) job unless it is undone
    [TestCase]
    public void ACleanupReleasesASubscriptionToAResidentFixture()
    {
        int clicks = 0;
        SpriteInteractable device = _screen.GetFixture<SpriteInteractable>("Device");
        StubJob job = JobWithParts();
        job.AttachHook = j =>
        {
            SpriteInteractable.ClickedEventHandler onClicked = () => clicks++;
            device.Clicked += onClicked;
            j.AddUndo(() => device.Clicked -= onClicked);
        };
        job.Attach(_screen);
        device.EmitSignal(SpriteInteractable.SignalName.Clicked);

        job.Detach();
        device.EmitSignal(SpriteInteractable.SignalName.Clicked);

        AssertThat(clicks).IsEqual(1);
    }

    // ---- Complete ----

    [TestCase]
    public void CompleteInvokesOnCompletedOnce()
    {
        StubJob job = JobWithParts();
        int completed = 0;
        job.OnCompleted = () => completed++;

        job.Complete();
        job.Complete();

        AssertThat(completed).IsEqual(1);
        AssertThat(job.IsCompleted).IsTrue();
    }

    // Mirrors the Distraction contract: completion must not depend on Setup()/Attach() having run, or on
    // anyone listening
    [TestCase]
    public void CompleteIsSafeWithoutSetupAttachOrSubscriber()
    {
        StubJob job = JobWithParts();
        AssertThat(job.OnCompleted).IsNull();

        job.Complete();

        AssertThat(job.IsCompleted).IsTrue();
    }

    // ---- _ExitTree safety net ----

    // A job freed without going through the manager's teardown still gives back what it placed.
    // Attached before entering the tree, as JobManager does
    [TestCase]
    public async Task FreeingAnAttachedJobGivesBackItsContent()
    {
        Node2D root = new();
        OfficeScreen screen = BuildScreen("Live");
        root.AddChild(screen);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        Node2D part = Part("P1");
        StubJob job = new();
        job.AddChild(part);
        job.Attach(screen);
        root.AddChild(job);

        job.QueueFree();
        await runner.AwaitIdleFrame();

        AssertThat(GodotObject.IsInstanceValid(part)).IsFalse();
        AssertThat(screen.GetFixture<Node2D>("Anchor").GetChildCount()).IsEqual(0);
    }
}
