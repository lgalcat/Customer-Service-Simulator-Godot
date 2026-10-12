using System;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// Hand-built OfficeScreens over StubInteractable children, so the sweep (who is toggled, in what
// order, with what value) is observable without any pointer behaviour. No SceneTree needed:
// Activate/Deactivate only flip Visible and loop the list _Ready() cached.
[TestSuite]
[RequireGodotRuntime]
public class OfficeScreenTesting
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

    // ---- _Ready(): collection ----

    // Pre-order, depth-first, child order - including through a non-interactable intermediate node
    // and a hand-built (Owner-less) tree, which Node.FindChildren would silently skip
    [TestCase]
    public void ReadyCollectsNestedInteractablesInTreeOrder()
    {
        StubInteractable a = Stub("A"), b = Stub("B"), c = Stub("C"), d = Stub("D");
        Node2D group = new();
        group.AddChild(b);
        group.AddChild(c);
        OfficeScreen screen = AutoFree(BuildScreen(a, group, d))!;

        // Manual _Ready() call - no SceneTree to auto-trigger it (applies to every test below)
        screen._Ready();
        screen.Activate();

        AssertThat(_log.Entries).ContainsExactly(
            "A:SetInteractable(true)", "B:SetInteractable(true)", "C:SetInteractable(true)", "D:SetInteractable(true)");
    }

    [TestCase]
    public void ReadyIgnoresNodesThatAreNotInteractable()
    {
        StubInteractable a = Stub("A");
        OfficeScreen screen = AutoFree(BuildScreen(a, new Sprite2D(), new Node()))!;

        screen._Ready();
        screen.Activate();

        AssertThat(_log.Entries).ContainsExactly("A:SetInteractable(true)");
    }

    // OfficeManager owns the starting baseline; a screen run on its own must stay visible and live
    [TestCase]
    public void ReadyDoesNotDeactivateTheScreen()
    {
        StubInteractable a = Stub("A");
        OfficeScreen screen = AutoFree(BuildScreen(a))!;

        screen._Ready();

        AssertThat(screen.Visible).IsTrue();
        AssertThat(a.CallCount).IsEqual(0);
    }

    // ---- Activate / Deactivate ----

    [TestCase]
    public void ActivateShowsTheScreenAndEnablesEveryInteractable()
    {
        StubInteractable a = Stub("A"), b = Stub("B");
        OfficeScreen screen = AutoFree(BuildScreen(a, b))!;
        screen._Ready();
        screen.Visible = false;

        screen.Activate();

        AssertThat(screen.Visible).IsTrue();
        AssertThat(a.LastValue).IsEqual(true);
        AssertThat(b.LastValue).IsEqual(true);
    }

    [TestCase]
    public void DeactivateHidesTheScreenAndDisablesEveryInteractable()
    {
        StubInteractable a = Stub("A"), b = Stub("B");
        OfficeScreen screen = AutoFree(BuildScreen(a, b))!;
        screen._Ready();

        screen.Deactivate();

        AssertThat(screen.Visible).IsFalse();
        AssertThat(a.LastValue).IsEqual(false);
        AssertThat(b.LastValue).IsEqual(false);
    }

    // Safe to repeat, and each call re-notifies (no dedup guard) - pinned, not endorsed
    [TestCase]
    public void RepeatedActivateAndDeactivateRenotifyEveryInteractable()
    {
        StubInteractable a = Stub("A");
        OfficeScreen screen = AutoFree(BuildScreen(a))!;
        screen._Ready();

        screen.Activate();
        screen.Activate();
        screen.Deactivate();
        screen.Deactivate();

        AssertThat(a.CallCount).IsEqual(4);
        AssertThat(screen.Visible).IsFalse();
    }

    [TestCase]
    public void ScreenWithNoInteractablesActivatesAndDeactivatesCleanly()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        screen._Ready();

        screen.Deactivate();
        AssertThat(screen.Visible).IsFalse();

        screen.Activate();
        AssertThat(screen.Visible).IsTrue();
    }

    // ---- Hooks ----

    // Template-method guarantee: the sweep is done before a subclass hook sees the screen
    [TestCase]
    public void HooksRunAfterTheSweep()
    {
        HookProbeScreen screen = AutoFree(new HookProbeScreen { Journal = _log })!;
        screen.AddChild(Stub("A"));
        screen._Ready();

        screen.Activate();
        screen.Deactivate();

        AssertThat(_log.Entries).ContainsExactly(
            "A:SetInteractable(true)", "OnActivated", "A:SetInteractable(false)", "OnDeactivated");
    }

    // ---- RequestScreenChange ----

    [TestCase]
    public void RequestScreenChangeRelaysTheTargetName()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        _log.Watch(screen);

        screen.RequestScreenChange("ComputerScreen");

        AssertThat(_log.Entries).ContainsExactly("ScreenChange:ComputerScreen");
    }

    [TestCase]
    public void RequestScreenChangeWithoutASubscriberDoesNotThrow()
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;

        screen.RequestScreenChange("ComputerScreen");

        AssertThat(screen.ScreenChangeRequested).IsNull();
    }

    [TestCase("")]
    [TestCase(null)]
    public void RequestScreenChangeRejectsAnEmptyTarget(string? target)
    {
        OfficeScreen screen = AutoFree(BuildScreen())!;
        _log.Watch(screen);

        AssertThrown(() => screen.RequestScreenChange(target!)).IsInstanceOf<ArgumentException>();
        AssertThat(_log.Entries).IsEmpty();
    }
}
