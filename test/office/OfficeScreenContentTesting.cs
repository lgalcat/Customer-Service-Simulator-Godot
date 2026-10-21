using System;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// The surface OfficeScreen offers outside systems (today: Jobs): the declared Fixtures list, content
// placed at anchors, and interactables registered after _Ready(). Split from OfficeScreenTesting, which
// keeps the screen's own lifecycle. Hand-built screens over StubInteractables; no SceneTree except where
// a deferred free has to actually happen.
[TestSuite]
[RequireGodotRuntime]
public class OfficeScreenContentTesting
{
    private EventLog _log = null!;

    [BeforeTest]
    public void Setup()
    {
        _log = new EventLog();
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    private StubInteractable Stub(string label) => new() { Label = label, Journal = _log };

    // Screen 'S' > Back, Anchor, Front, with Anchor as its one fixture: the layering an anchor is
    // authored for (content placed there draws between the two). Not ready
    private static (OfficeScreen Screen, Node2D Anchor) AnchoredScreen()
    {
        Node2D anchor = new() { Name = "Anchor" };
        OfficeScreen screen = BuildScreen(new Node2D { Name = "Back" }, anchor, new Node2D { Name = "Front" });
        screen.Name = "S";
        screen.Fixtures.Add(anchor);
        return (screen, anchor);
    }

    private static Node2D Content(params Node[] children)
    {
        Node2D content = new() { Name = "Content" };
        foreach (Node child in children) { content.AddChild(child); }
        return content;
    }

    // ---- Fixtures: validation in _Ready() ----
    // Each mis-authoring fails fast at load rather than as a null or a shadowed fixture far from the cause

    [TestCase]
    public void ReadyThrowsOnAnUnsetFixturesEntry()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen.Name = "S";
        screen.Fixtures.Add(null!);

        // Manual _Ready() call - no SceneTree to auto-trigger it (applies to every test below)
        AssertThrown(() => screen._Ready()).IsInstanceOf<InvalidOperationException>().HasMessage("OfficeScreen 'S': Fixtures entry 0 is unset");
    }

    [TestCase]
    public void ReadyThrowsOnAFixtureOutsideTheScreen()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen.Name = "S";
        Node2D outsider = AutoFree(new Node2D { Name = "Outsider" })!;
        screen.Fixtures.Add(outsider);

        AssertThrown(() => screen._Ready()).IsInstanceOf<InvalidOperationException>().HasMessage("OfficeScreen 'S': fixture 'Outsider' (entry 0) is not a descendant of the screen");
    }

    // A node is not its own descendant: the screen root cannot expose itself
    [TestCase]
    public void ReadyThrowsWhenTheScreenListsItself()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen.Name = "S";
        screen.Fixtures.Add(screen);

        AssertThrown(() => screen._Ready()).IsInstanceOf<InvalidOperationException>().HasMessage("OfficeScreen 'S': fixture 'S' (entry 0) is not a descendant of the screen");
    }

    // Names are only unique among siblings, so two fixtures under different parents can collide
    [TestCase]
    public void ReadyThrowsOnADuplicateFixtureName()
    {
        Node2D first = new() { Name = "Twin" };
        Node2D second = new() { Name = "Twin" };
        Node2D group = new() { Name = "Group" };
        group.AddChild(second);
        OfficeScreen screen = AutoFree(BuildScreen(first, group))!;
        screen.Name = "S";
        screen.Fixtures.Add(first);
        screen.Fixtures.Add(second);

        AssertThrown(() => screen._Ready()).IsInstanceOf<InvalidOperationException>().HasMessage("OfficeScreen 'S': duplicate fixture name 'Twin' (entry 1)");
    }

    // ---- Fixtures: lookup ----

    [TestCase]
    public void GetFixtureReturnsAListedNodeByNameAtAnyDepth()
    {
        Node2D device = new() { Name = "Device" };
        Node2D group = new() { Name = "Group" };
        group.AddChild(device);
        (OfficeScreen screen, Node2D anchor) = AnchoredScreen();
        AutoFree(screen);
        screen.AddChild(group);
        screen.Fixtures.Add(device);
        screen._Ready();

        AssertThat(screen.GetFixture<Node2D>("Anchor")).IsSame(anchor);
        AssertThat(screen.GetFixture<Node2D>("Device")).IsSame(device);
    }

    // Nothing is scanned: a child that isn't listed is not reachable, and the error names what is
    [TestCase]
    public void GetFixtureOfAnUnlistedNodeThrowsListingTheExposedOnes()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen.AddChild(new Node2D { Name = "Unlisted" });
        screen._Ready();

        AssertThrown(() => screen.GetFixture<Node2D>("Unlisted")).IsInstanceOf<ArgumentException>()
            .StartsWithMessage("OfficeScreen 'S': no fixture named 'Unlisted'. Exposed: 'Anchor' (Node2D)");
    }

    [TestCase]
    public void GetFixtureOfTheWrongTypeThrowsInvalidCast()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();

        AssertThrown(() => screen.GetFixture<Sprite2D>("Anchor")).IsInstanceOf<InvalidCastException>()
            .HasMessage("OfficeScreen 'S': fixture 'Anchor' is a Node2D, not a Sprite2D");
    }

    // The index is built in _Ready(): before it, nothing is exposed yet
    [TestCase]
    public void GetFixtureBeforeReadyFindsNothing()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);

        AssertThrown(() => screen.GetFixture<Node2D>("Anchor")).IsInstanceOf<ArgumentException>()
            .StartsWithMessage("OfficeScreen 'S': no fixture named 'Anchor'. Exposed: (none)");
    }

    [TestCase]
    public void TryGetFixtureIsFalseForAMissingOrMistypedFixture()
    {
        (OfficeScreen screen, Node2D anchor) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();

        AssertThat(screen.TryGetFixture("Nope", out Node2D? missing)).IsFalse();
        AssertThat(missing).IsNull();
        AssertThat(screen.TryGetFixture("Anchor", out Sprite2D? mistyped)).IsFalse();
        AssertThat(mistyped).IsNull();
        AssertThat(screen.TryGetFixture("Anchor", out Node2D? found)).IsTrue();
        AssertThat(found).IsSame(anchor);
    }

    // The design relies on an exported Array<Node> being saved with the scene (as node paths) and
    // rebound to the instance's own nodes on load
    [TestCase]
    public void FixturesSurviveAPackedSceneRoundTrip()
    {
        (OfficeScreen source, _) = AnchoredScreen();
        foreach (Node child in source.GetChildren()) { child.Owner = source; }
        PackedScene packed = new();
        packed.Pack(source);
        source.Free();

        OfficeScreen screen = AutoFree(packed.Instantiate<OfficeScreen>())!;
        screen._Ready();

        AssertThat(screen.GetFixture<Node2D>("Anchor").GetParent()).IsSame(screen);
    }

    // ---- Place ----

    [TestCase]
    public void PlaceAddsUnparentedContentUnderTheAnchor()
    {
        (OfficeScreen screen, Node2D anchor) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        Node2D content = Content();

        screen.Place(content, "Anchor");

        AssertThat(content.GetParent()).IsSame(anchor);
    }

    // Reparented without keeping the global transform: an anchor sits at identity, so content keeps
    // the full-canvas coordinates it was authored with, whatever its old parent's transform was
    [TestCase]
    public void PlaceReparentsContentAndKeepsItsOwnCoordinates()
    {
        (OfficeScreen screen, Node2D anchor) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        Node2D oldParent = AutoFree(new Node2D { Position = new Vector2(100, 100) })!;
        Node2D content = Content();
        content.Position = new Vector2(10, 20);
        oldParent.AddChild(content);

        screen.Place(content, "Anchor");

        AssertThat(content.GetParent()).IsSame(anchor);
        AssertThat(content.Position).IsEqual(new Vector2(10, 20));
    }

    [TestCase]
    public void PlaceAtAnUnknownAnchorThrowsAndLeavesTheContentAlone()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        Node2D content = AutoFree(Content())!;

        AssertThrown(() => screen.Place(content, "Nope")).IsInstanceOf<ArgumentException>();
        AssertThat(content.GetParent()).IsNull();
    }

    [TestCase]
    public void PlaceAtAFixtureThatIsNotANode2DThrows()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        Node device = new() { Name = "Device" };
        screen.AddChild(device);
        screen.Fixtures.Add(device);
        screen._Ready();
        Node2D content = AutoFree(Content())!;

        AssertThrown(() => screen.Place(content, "Device")).IsInstanceOf<InvalidCastException>();
    }

    // Registered content is swept like resident interactables, at any depth
    [TestCase]
    public void PlacedNestedInteractablesFollowTheScreensSweeps()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        screen.Activate();
        Node2D group = new();
        group.AddChild(Stub("B"));
        screen.Place(Content(Stub("A"), group), "Anchor");
        _log.Clear();

        screen.Deactivate();
        screen.Activate();
        screen.SetInputEnabled(false);

        AssertThat(_log.Entries).ContainsExactly(
            "A:SetInteractable(false)", "B:SetInteractable(false)",
            "A:SetInteractable(true)", "B:SetInteractable(true)",
            "A:SetInteractable(false)", "B:SetInteractable(false)");
    }

    // A part may be an interactable itself, not only contain some. Starts hidden: InputPickable defaults
    // to true, so only a false-then-true sequence shows the screen is driving it
    [TestCase]
    public void PlacedContentThatIsItselfInteractableFollowsTheScreensSweeps()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        screen.Deactivate();
        SpriteInteractable part = new();

        screen.Place(part, "Anchor");
        AssertThat(part.InputPickable).IsFalse();
        screen.Activate();
        AssertThat(part.InputPickable).IsTrue();
    }

    // Content arriving on a hidden or locked screen must not be interactable for a moment
    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    public void PlaceAppliesTheScreensCurrentState(bool active, bool inputEnabled, bool expected)
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        if (active) { screen.Activate(); } else { screen.Deactivate(); }
        screen.SetInputEnabled(inputEnabled);
        StubInteractable stub = Stub("A");

        screen.Place(Content(stub), "Anchor");

        AssertThat(stub.CallCount).IsEqual(1);
        AssertThat(stub.LastValue).IsEqual(expected);
    }

    // A part authored in another scene (a job's) is owned by that scene's root; once moved it becomes
    // the owner of its own subtree, as if it had always been a scene of its own. A nested instance's
    // internals keep their own owner
    [TestCase]
    public void PlaceMakesAPartFromAnotherSceneTheOwnerOfItsSubtree()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        Node jobRoot = AutoFree(new Node())!;
        Node2D part = new() { Name = "Part" };
        Node2D inner = new() { Name = "Inner" };
        Node2D instance = new() { Name = "Instance" };
        Node2D instanceInner = new() { Name = "InstanceInner" };
        jobRoot.AddChild(part);
        part.AddChild(inner);
        part.AddChild(instance);
        instance.AddChild(instanceInner);
        part.Owner = jobRoot;
        inner.Owner = jobRoot;
        instance.Owner = jobRoot;
        instanceInner.Owner = instance;

        screen.Place(part, "Anchor");

        AssertThat(part.Owner).IsNull();
        AssertThat(inner.Owner).IsSame(part);
        AssertThat(instance.Owner).IsSame(part);
        AssertThat(instanceInner.Owner).IsSame(instance);
    }

    // ---- Remove ----

    [TestCase]
    public void RemoveSwitchesInteractablesOffAndStopsSweepingThem()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        screen.Activate();
        Node2D content = Content(Stub("A"));
        screen.Place(content, "Anchor");
        _log.Clear();

        screen.Remove(content);
        screen.Activate();

        AssertThat(_log.Entries).ContainsExactly("A:SetInteractable(false)");
        AssertThat(content.IsQueuedForDeletion()).IsTrue();
    }

    // Live tree, so the deferred free actually runs
    [TestCase]
    public async Task RemoveFreesTheContent()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        using ISceneRunner runner = ISceneRunner.Load(screen, true, true);
        Node2D content = Content(Stub("A"));
        screen.Place(content, "Anchor");

        screen.Remove(content);
        await runner.AwaitIdleFrame();

        AssertThat(GodotObject.IsInstanceValid(content)).IsFalse();
    }

    [TestCase]
    public void RemoveIgnoresContentThatWasAlreadyFreed()
    {
        (OfficeScreen screen, _) = AnchoredScreen();
        AutoFree(screen);
        screen._Ready();
        Node2D content = Content(Stub("A"));
        screen.Place(content, "Anchor");
        content.Free();

        screen.Remove(content);
        screen.Deactivate();

        AssertThat(_log.Entries).ContainsExactly("A:SetInteractable(true)");
    }

    // ---- RegisterInteractable / UnregisterInteractable ----

    [TestCase(true, true, true)]
    [TestCase(true, false, false)]
    [TestCase(false, true, false)]
    public void RegisterAppliesTheScreensCurrentState(bool active, bool inputEnabled, bool expected)
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen._Ready();
        if (active) { screen.Activate(); } else { screen.Deactivate(); }
        screen.SetInputEnabled(inputEnabled);
        StubInteractable stub = AutoFree(Stub("A"))!;

        screen.RegisterInteractable(stub);

        AssertThat(stub.CallCount).IsEqual(1);
        AssertThat(stub.LastValue).IsEqual(expected);
    }

    [TestCase]
    public void RegisteringTwiceIsSweptOnce()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen._Ready();
        StubInteractable stub = AutoFree(Stub("A"))!;
        screen.RegisterInteractable(stub);
        screen.RegisterInteractable(stub);
        _log.Clear();

        screen.Deactivate();

        AssertThat(_log.Entries).ContainsExactly("A:SetInteractable(false)");
    }

    [TestCase]
    public void UnregisterSwitchesOffAndStopsSweeping()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen._Ready();
        screen.Activate();
        StubInteractable stub = AutoFree(Stub("A"))!;
        screen.RegisterInteractable(stub);
        _log.Clear();

        screen.UnregisterInteractable(stub);
        screen.Activate();

        AssertThat(_log.Entries).ContainsExactly("A:SetInteractable(false)");
    }

    [TestCase]
    public void UnregisteringAnInteractableThatWasNeverRegisteredDoesNothing()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen._Ready();
        StubInteractable stub = AutoFree(Stub("A"))!;

        screen.UnregisterInteractable(stub);

        AssertThat(stub.CallCount).IsEqual(0);
    }

    [TestCase]
    public void UnregisteringAFreedInteractableDoesNotCallIntoIt()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen._Ready();
        StubInteractable stub = Stub("A");
        screen.RegisterInteractable(stub);
        stub.Free();
        _log.Clear();

        screen.UnregisterInteractable(stub);

        AssertThat(_log.Entries).IsEmpty();
    }

    // Freed without unregistering (the caller forgot): dropped on the next sweep, never called into
    [TestCase]
    public void AFreedInteractableThatWasNeverUnregisteredIsDroppedFromSweeps()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen._Ready();
        StubInteractable a = Stub("A");
        StubInteractable b = AutoFree(Stub("B"))!;
        screen.RegisterInteractable(a);
        screen.RegisterInteractable(b);
        a.Free();
        _log.Clear();

        screen.Deactivate();
        screen.Activate();

        AssertThat(_log.Entries).ContainsExactly("B:SetInteractable(false)", "B:SetInteractable(true)");
    }

    // Switching one interactable can make a subscriber register another; the sweep runs over a snapshot,
    // so the newcomer gets the screen's state from RegisterInteractable and is not swept twice
    [TestCase]
    public void RegisteringAnotherInteractableMidSweepDoesNotBreakTheSweep()
    {
        StubInteractable a = Stub("A");
        OfficeScreen screen = AutoFree(BuildScreen(a))!;
        screen._Ready();
        StubInteractable b = AutoFree(Stub("B"))!;
        a.OnToggled = active => { if (!active) { screen.RegisterInteractable(b); } };

        screen.Deactivate();

        AssertThat(_log.Entries).ContainsExactly("A:SetInteractable(false)", "B:SetInteractable(false)");
    }

    [TestCase]
    public void UnregisteringAnotherInteractableMidSweepDoesNotBreakTheSweep()
    {
        StubInteractable a = Stub("A");
        OfficeScreen screen = AutoFree(BuildScreen(a))!;
        screen._Ready();
        StubInteractable b = AutoFree(Stub("B"))!;
        screen.RegisterInteractable(b);
        a.OnToggled = active => { if (!active) { screen.UnregisterInteractable(b); } };

        screen.Deactivate();
        _log.Clear();
        a.OnToggled = null;
        screen.Activate();

        AssertThat(_log.Entries).ContainsExactly("A:SetInteractable(true)");
    }
}
