using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

// Shared fixtures and drivers for the Jobs suites: stub type keys, factories, hand-built screens and
// managers, the real office with test anchors, and an event recorder. Builds on OfficeTestKit/EventLog
// (Jobs depends on Office, so its tests may too - never the other way round)
public static class JobTestKit
{
    // Stub scenes are keyed by these cast values (an enum accepts any), so stub suites never depend on
    // the real JobType members. Kept high so they never collide with one
    public const JobType StubTypeA = (JobType)1000;
    public const JobType StubTypeB = (JobType)1001;

    public const string OfficePath = "res://assets/scenes/office/office.tscn";
    public const string JobManagerPath = "res://assets/scenes/jobs/job_manager.tscn";
    // Name of the anchor InstantiateOfficeWithTestAnchors adds to every real screen
    public const string TestAnchor = "TestAnchor";

    public static JobFactory Factory(params (JobType Type, PackedScene Scene)[] entries) =>
        new(entries.ToDictionary(entry => entry.Type, entry => entry.Scene));

    // Single-entry factory under StubTypeA
    public static JobFactory Factory(PackedScene scene) => Factory((StubTypeA, scene));

    // Screen 'name' > Back, Anchor, Front, Device, exposing Anchor (content placed there draws between
    // Back and Front) and Device (a resident SpriteInteractable: the custom-signal emitter a job binds
    // to). Not ready: load it into a tree or call _Ready() by hand
    public static OfficeScreen BuildScreen(string name)
    {
        OfficeScreen screen = new() { Name = name };
        Node2D anchor = new() { Name = "Anchor" };
        SpriteInteractable device = new() { Name = "Device" };
        screen.AddChild(new Node2D { Name = "Back" });
        screen.AddChild(anchor);
        screen.AddChild(new Node2D { Name = "Front" });
        screen.AddChild(device);
        screen.Fixtures.Add(anchor);
        screen.Fixtures.Add(device);
        return screen;
    }

    // ScreenResolver over hand-built screens, by node name - what OfficeManager.TryGetScreen does for
    // the real office
    public static Func<StringName, OfficeScreen?> Resolver(params OfficeScreen[] screens) =>
        name => screens.FirstOrDefault(screen => screen.Name == name);

    // A clickable part for a stub job or announcement to place: anything the screen must sweep
    public static SpriteInteractable InteractablePart(string name = "Part")
    {
        SpriteInteractable part = new() { Name = name };
        part.AddChild(new CollisionShape2D { Name = "Hitbox", Shape = new RectangleShape2D() });
        return part;
    }

    // The real office.tscn, not yet in a tree, with an empty TestAnchor Node2D added to every screen and
    // appended to its Fixtures - somewhere for stubs to go on any screen, whatever anchors the real
    // screens author. Must happen before tree entry: Fixtures is read once, in _Ready()
    public static OfficeManager InstantiateOfficeWithTestAnchors()
    {
        OfficeManager office = GD.Load<PackedScene>(OfficePath).Instantiate<OfficeManager>();
        foreach (OfficeScreen screen in office.GetNode("ScreenLayer").GetChildren().OfType<OfficeScreen>())
        {
            Node2D anchor = new() { Name = TestAnchor };
            screen.AddChild(anchor);
            screen.Fixtures.Add(anchor);
        }
        return office;
    }

    // A real job (the scene at 'scenePath') set up ('configure' runs first, for balance knobs) and started on
    // its screen in 'office' the way the manager starts it - attached, then added to the tree - with that
    // screen made current first, so its parts take input. Freed with the test
    public static T StartJob<T>(OfficeManager office, string scenePath, int difficulty = 1, Action<Job>? configure = null) where T : Job
    {
        T job = GdUnit4.Assertions.AutoFree(GD.Load<PackedScene>(scenePath).Instantiate<T>())!;
        configure?.Invoke(job);
        job.Setup(difficulty);
        OfficeScreen? screen = office.TryGetScreen(job.TargetScreen);
        GdUnit4.Assertions.AssertThat(screen).OverrideFailureMessage($"the office has no screen '{job.TargetScreen}'").IsNotNull();
        office.GoToScreen(screen!);
        job.Attach(screen!);
        office.AddChild(job);
        return job;
    }

    // How much is in a screen: "everything placed was taken back out" is checked as "back to the
    // baseline count" once the deferred frees have run
    public static int CountDescendants(Node node) =>
        node.GetChildCount() + node.GetChildren().Sum(CountDescendants);

    public static IEnumerable<Node> DescendantsOf(Node node) =>
        node.GetChildren().SelectMany(child => DescendantsOf(child).Prepend(child));

    // Records each manager event together with the phase it fired in: "Announcing@Announcing",
    // "Started@Active", "Completed@Idle"
    public static void WatchJobs(this EventLog log, JobManager manager)
    {
        manager.JobAnnouncing += () => log.Add("Announcing@" + manager.Phase);
        manager.JobStarted += () => log.Add("Started@" + manager.Phase);
        manager.JobCompleted += () => log.Add("Completed@" + manager.Phase);
    }
}
