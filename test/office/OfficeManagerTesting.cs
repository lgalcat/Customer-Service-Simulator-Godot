using System;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// OfficeManager over hand-built Manager > ScreenLayer > HookProbeScreen trees (see OfficeTestKit).
// Screens journal their activate/deactivate hooks into a shared EventLog, which is how switch ORDER
// is observed. The real office.tscn is covered by OfficeSystemIntegrationTesting.
[TestSuite]
[RequireGodotRuntime]
public class OfficeManagerTesting
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

    private static HookProbeScreen Screen(OfficeManager manager, string name) =>
        manager.GetNode<HookProbeScreen>("ScreenLayer/" + name);

    // ---- _Ready(): baseline ----

    [TestCase]
    public void ReadyActivatesOnlyTheInitialScreen()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "B", "A", "B", "C"))!;

        // Hand-built tree is not in a SceneTree, so _Ready() runs by hand, screens first (applies to every test below)
        ReadyBottomUp(manager);

        AssertThat(Screen(manager, "A").Visible).IsFalse();
        AssertThat(Screen(manager, "B").Visible).IsTrue();
        AssertThat(Screen(manager, "C").Visible).IsFalse();
        AssertThat(manager.CurrentScreen).IsSame(Screen(manager, "B"));
    }

    // The initial screen is only ever activated: never deactivated first and then reactivated
    [TestCase]
    public void ReadyDeactivatesTheOthersThenActivatesTheInitialOnce()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "B", "A", "B", "C"))!;

        ReadyBottomUp(manager);

        AssertThat(_log.Entries).ContainsExactly("A.OnDeactivated", "C.OnDeactivated", "B.OnActivated");
    }

    // Tolerated by design: nothing starts active, so CurrentScreen stays null
    [TestCase]
    public void ReadyWithoutAnInitialScreenDeactivatesEveryScreen()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, null, "A", "B"))!;

        ReadyBottomUp(manager);

        AssertThat(Screen(manager, "A").Visible).IsFalse();
        AssertThat(Screen(manager, "B").Visible).IsFalse();
        AssertThat(manager.CurrentScreen).IsNull();
    }

    [TestCase]
    public void ReadySubscribesEveryScreenToItsNavigationRequests()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A", "B", "C"))!;
        ReadyBottomUp(manager);

        // C is a non-initial screen: its subscription must exist even though it started deactivated
        Screen(manager, "C").RequestScreenChange("B");

        AssertThat(manager.CurrentScreen).IsSame(Screen(manager, "B"));
    }

    // ---- _Ready(): authoring errors ----

    [TestCase]
    public void ReadyThrowsWhenScreenLayerIsMissing()
    {
        OfficeManager manager = AutoFree(new OfficeManager())!;

        // The message tells the guard apart from an incidental NullReferenceException further down
        AssertThrown(() => manager._Ready()).IsInstanceOf<NullReferenceException>().HasMessage("OfficeManager: missing required child 'ScreenLayer'");
    }

    // GetNode<T>() throws its own InvalidCastException before the manual null guard can fire
    [TestCase]
    public void ReadyThrowsWhenScreenLayerIsTheWrongType()
    {
        OfficeManager manager = AutoFree(new OfficeManager())!;
        manager.AddChild(new Node2D { Name = "ScreenLayer" });

        AssertThrown(() => manager._Ready()).IsInstanceOf<InvalidCastException>();
    }

    // A script-less screen root would otherwise sit visible and interactive over the active one
    [TestCase]
    public void ReadyThrowsOnAScreenLayerChildThatIsNotAnOfficeScreen()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, null, "A"))!;
        manager.GetNode("ScreenLayer").AddChild(new Node2D { Name = "NotAScreen" });

        AssertThrown(() => ReadyBottomUp(manager)).IsInstanceOf<InvalidOperationException>();
    }

    // ---- GoToScreen ----

    [TestCase]
    public void GoToScreenSwitchesVisibilityAndCurrentScreen()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A", "B"))!;
        ReadyBottomUp(manager);

        manager.GoToScreen(Screen(manager, "B"));

        AssertThat(Screen(manager, "A").Visible).IsFalse();
        AssertThat(Screen(manager, "B").Visible).IsTrue();
        AssertThat(manager.CurrentScreen).IsSame(Screen(manager, "B"));
    }

    // Old one first, so the two are never both active at once
    [TestCase]
    public void GoToScreenDeactivatesTheOldScreenBeforeActivatingTheNew()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A", "B"))!;
        ReadyBottomUp(manager);
        _log.Clear();

        manager.GoToScreen(Screen(manager, "B"));

        AssertThat(_log.Entries).ContainsExactly("A.OnDeactivated", "B.OnActivated");
    }

    [TestCase]
    public void GoToScreenByNameResolvesAgainstTheScreenLayer()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A", "B"))!;
        ReadyBottomUp(manager);

        manager.GoToScreen(new StringName("B"));

        AssertThat(manager.CurrentScreen).IsSame(Screen(manager, "B"));
    }

    [TestCase]
    public void GoToTheCurrentScreenIsANoOp()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A", "B"))!;
        ReadyBottomUp(manager);
        _log.Clear();

        manager.GoToScreen(Screen(manager, "A"));

        AssertThat(_log.Entries).IsEmpty();
        AssertThat(manager.CurrentScreen).IsSame(Screen(manager, "A"));
    }

    [TestCase]
    public void GoToAnUnknownScreenNameThrowsAndLeavesStateUntouched()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A", "B"))!;
        ReadyBottomUp(manager);
        _log.Clear();

        AssertThrown(() => manager.GoToScreen(new StringName("Nope"))).IsInstanceOf<ArgumentException>();

        AssertThat(_log.Entries).IsEmpty();
        AssertThat(manager.CurrentScreen).IsSame(Screen(manager, "A"));
    }

    // Same exception for a name that resolves to something that isn't a screen (no InvalidCastException)
    [TestCase]
    public void GoToANameThatIsNotAnOfficeScreenThrowsArgumentException()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A"))!;
        ReadyBottomUp(manager);
        manager.GetNode("ScreenLayer").AddChild(new Node2D { Name = "Decor" });

        AssertThrown(() => manager.GoToScreen(new StringName("Decor"))).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public void GoToAScreenOutsideTheScreenLayerThrowsAndLeavesStateUntouched()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A"))!;
        ReadyBottomUp(manager);
        OfficeScreen stray = new() { Name = "Stray" };
        manager.AddChild(stray);
        _log.Clear();

        AssertThrown(() => manager.GoToScreen(stray)).IsInstanceOf<ArgumentException>();

        AssertThat(_log.Entries).IsEmpty();
        AssertThat(manager.CurrentScreen).IsSame(Screen(manager, "A"));
    }

    // End to end through the subscription: a bad navigation target surfaces at the requester
    [TestCase]
    public void RequestingAnUnknownScreenFromInsideAScreenThrows()
    {
        OfficeManager manager = AutoFree(BuildManager(_log, "A", "A"))!;
        ReadyBottomUp(manager);

        AssertThrown(() => Screen(manager, "A").RequestScreenChange("Nope")).IsInstanceOf<ArgumentException>();
    }
}
