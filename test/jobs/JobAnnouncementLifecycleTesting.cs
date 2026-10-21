using System;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// JobAnnouncement's base-class mechanics - the same plumbing as Job (see JobLifecycleTesting), kept to
// one case per mechanism, plus Finish(). StubJobAnnouncement constructed directly, hand-built screen
[TestSuite]
[RequireGodotRuntime]
public class JobAnnouncementLifecycleTesting
{
    private EventLog _log = null!;
    private OfficeScreen _screen = null!;

    [BeforeTest]
    public void Setup()
    {
        _log = new EventLog();
        _screen = AutoFree(BuildScreen("S"))!;
        // Manual _Ready() call - no SceneTree to auto-trigger it (applies to every test below)
        _screen._Ready();
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    private Node2D Part(string label)
    {
        Node2D part = new() { Name = label };
        part.AddChild(new StubInteractable { Label = label, Journal = _log });
        return part;
    }

    private static StubJobAnnouncement AnnouncementWithParts(params Node2D[] parts)
    {
        StubJobAnnouncement announcement = AutoFree(new StubJobAnnouncement())!;
        foreach (Node2D part in parts) { announcement.AddChild(part); }
        return announcement;
    }

    // ---- Attach / Detach ----

    [TestCase]
    public void AttachMovesPartsToTheScreenAndDetachTakesThemBack()
    {
        Node2D part = AutoFree(Part("P1"))!;
        StubJobAnnouncement announcement = AnnouncementWithParts(part);

        announcement.Attach(_screen);
        AssertThat(announcement.Screen).IsSame(_screen);
        AssertThat(part.GetParent()).IsSame(_screen.GetFixture<Node2D>("Anchor"));

        announcement.Detach();
        AssertThat(announcement.Screen).IsNull();
        AssertThat(part.IsQueuedForDeletion()).IsTrue();
    }

    [TestCase]
    public void AttachingTwiceThrows()
    {
        StubJobAnnouncement announcement = AnnouncementWithParts();
        announcement.Attach(_screen);

        AssertThrown(() => announcement.Attach(_screen)).IsInstanceOf<InvalidOperationException>()
            .HasMessage("StubJobAnnouncement: already attached");
    }

    [TestCase]
    public void AFailingOnAttachedTakesBackWhatItDidAndRethrows()
    {
        Node2D part = AutoFree(Part("P1"))!;
        StubJobAnnouncement announcement = AnnouncementWithParts(part);
        announcement.ThrowAfterPlacing = true;
        announcement.AttachHook = a => a.AddUndo(() => _log.Add("undo"));

        AssertThrown(() => announcement.Attach(_screen)).IsInstanceOf<InvalidOperationException>();

        AssertThat(_log.Entries).ContainsExactly("P1:SetInteractable(true)", "undo", "P1:SetInteractable(false)");
        AssertThat(announcement.Screen).IsNull();
    }

    [TestCase]
    public void DetachRunsCleanupsOnceAndIsSafeWithoutAttach()
    {
        StubJobAnnouncement unattached = AnnouncementWithParts();
        unattached.Detach();
        StubJobAnnouncement announcement = AnnouncementWithParts();
        announcement.AttachHook = a => a.AddUndo(() => _log.Add("undo"));
        announcement.Attach(_screen);

        announcement.Detach();
        announcement.Detach();

        AssertThat(_log.Entries).ContainsExactly("undo");
    }

    [TestCase]
    public void PlaceAndAddCleanupOutsideAnAttachmentThrow()
    {
        StubJobAnnouncement announcement = AnnouncementWithParts();
        Node2D part = AutoFree(Part("P1"))!;

        AssertThrown(() => announcement.PlacePart(part, "Anchor")).IsInstanceOf<InvalidOperationException>()
            .HasMessage("StubJobAnnouncement: not attached to a screen yet (only valid from OnAttached onwards)");
        AssertThrown(() => announcement.AddUndo(() => { })).IsInstanceOf<InvalidOperationException>();
    }

    // ---- Finish ----

    [TestCase]
    public void FinishInvokesOnFinishedOnce()
    {
        StubJobAnnouncement announcement = AnnouncementWithParts();
        int finished = 0;
        announcement.OnFinished = () => finished++;

        announcement.Finish();
        announcement.Finish();

        AssertThat(finished).IsEqual(1);
    }

    [TestCase]
    public void FinishIsSafeWithoutASubscriber()
    {
        StubJobAnnouncement announcement = AnnouncementWithParts();
        AssertThat(announcement.OnFinished).IsNull();

        announcement.Finish();
    }

    // ---- _ExitTree safety net ----

    [TestCase]
    public async Task FreeingAnAttachedAnnouncementGivesBackItsContent()
    {
        Node2D root = new();
        OfficeScreen screen = BuildScreen("Live");
        root.AddChild(screen);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        Node2D part = Part("P1");
        StubJobAnnouncement announcement = new();
        announcement.AddChild(part);
        announcement.Attach(screen);
        root.AddChild(announcement);

        announcement.QueueFree();
        await runner.AwaitIdleFrame();

        AssertThat(GodotObject.IsInstanceValid(part)).IsFalse();
    }
}
