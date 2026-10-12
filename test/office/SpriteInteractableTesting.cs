using System.Threading.Tasks;
using Godot;
using GdUnit4;
using GdUnit4.Asserts;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// Pointer-state guards and signal emission of SpriteInteractable. Driven by emitting Godot's own
// InputEvent/MouseEntered/MouseExited signals (no real input or physics); real picking/hitbox
// accuracy is covered separately by OfficeInputTesting. Every test loads the node into a live tree
// via ISceneRunner because the handlers check IsVisibleInTree() (which also runs _Ready() for us).
// Observation: an EventLog (synchronous, ordered) by default; gdUnit4's AssertSignal only where the
// engine signal path itself is the subject (see the last two tests)
[TestSuite]
[RequireGodotRuntime]
public class SpriteInteractableTesting
{
    private Node2D _root = null!;
    private SpriteInteractable _interactable = null!;
    private EventLog _log = null!;
    private ISceneRunner _runner = null!;

    [BeforeTest]
    public void Setup()
    {
        (_root, _interactable) = BuildInteractable();
        _log = new EventLog();
        _log.Watch(_interactable);
        // autoFree: the runner frees the loaded root (and so the interactable) itself
        _runner = ISceneRunner.Load(_root, true, true);
    }

    [AfterTest]
    public void Teardown()
    {
        _runner.Dispose();
    }

    [TestCase]
    public void ReadyDisablesPhysicsOverlapDetection()
    {
        AssertThat(_interactable.Monitoring).IsFalse();
        AssertThat(_interactable.Monitorable).IsFalse();
    }

    [TestCase]
    public void DeclaresAllFiveSignals()
    {
        // Rename guard: scene [connection] lines and OfficeScreen consumers reference these by name
        AssertSignal(_interactable).IsSignalExists(SpriteInteractable.SignalName.Clicked);
        AssertSignal(_interactable).IsSignalExists(SpriteInteractable.SignalName.HoverStarted);
        AssertSignal(_interactable).IsSignalExists(SpriteInteractable.SignalName.HoverEnded);
        AssertSignal(_interactable).IsSignalExists(SpriteInteractable.SignalName.Activated);
        AssertSignal(_interactable).IsSignalExists(SpriteInteractable.SignalName.Deactivated);
    }

    // ---- Click ----

    // Interactable from the start: a screen run on its own never calls SetInteractable
    [TestCase]
    public void LeftPressClicksWithoutAnyPriorSetInteractable()
    {
        Click(_interactable);

        AssertThat(_log.Entries).ContainsExactly("Clicked");
    }

    [TestCase]
    public void EachLeftPressClicksOnce()
    {
        Click(_interactable);
        Click(_interactable);

        AssertThat(_log.Entries).ContainsExactly("Clicked", "Clicked");
    }

    // Press-only clicking: the matching release must not click through onto a screen navigated to
    [TestCase("LeftRelease")]
    [TestCase("RightPress")]
    [TestCase("Motion")]
    public void OnlyALeftPressClicks(string kind)
    {
        InputEvent @event = kind switch
        {
            "LeftRelease" => new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = false },
            "RightPress" => new InputEventMouseButton { ButtonIndex = MouseButton.Right, Pressed = true },
            _ => new InputEventMouseMotion(),
        };

        Press(_interactable, @event);

        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void ClickIgnoredWhileInactive()
    {
        _interactable.SetInteractable(false);
        _log.Clear();

        Click(_interactable);

        AssertThat(_log.Entries).IsEmpty();
    }

    // Visibility alone (without SetInteractable) also suppresses: the node itself or any ancestor
    [TestCase]
    public void ClickIgnoredWhileItselfHidden()
    {
        _interactable.Visible = false;

        Click(_interactable);

        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void ClickIgnoredWhileAncestorHidden()
    {
        _root.Visible = false;

        Click(_interactable);

        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void ClickWorksAgainAfterReactivation()
    {
        _interactable.SetInteractable(false);
        _interactable.SetInteractable(true);
        _log.Clear();

        Click(_interactable);

        AssertThat(_log.Entries).ContainsExactly("Clicked");
    }

    // ---- Hover ----

    [TestCase]
    public void PointerEnterAndExitEmitHoverStartedThenEnded()
    {
        PointerEnter(_interactable);
        PointerExit(_interactable);

        AssertThat(_log.Entries).ContainsExactly("HoverStarted", "HoverEnded");
    }

    // Godot can deliver repeated enter events; only the first starts a hover
    [TestCase]
    public void DuplicatePointerEnterEmitsOnce()
    {
        PointerEnter(_interactable);
        PointerEnter(_interactable);

        AssertThat(_log.Entries).ContainsExactly("HoverStarted");
    }

    [TestCase]
    public void PointerExitWithoutEnterIsIgnored()
    {
        PointerExit(_interactable);

        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void DuplicatePointerExitEmitsOnce()
    {
        PointerEnter(_interactable);
        PointerExit(_interactable);
        PointerExit(_interactable);

        AssertThat(_log.Entries).ContainsExactly("HoverStarted", "HoverEnded");
    }

    [TestCase]
    public void PointerEnterIgnoredWhileInactive()
    {
        _interactable.SetInteractable(false);
        _log.Clear();

        PointerEnter(_interactable);

        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void PointerEnterIgnoredWhileHidden()
    {
        _root.Visible = false;

        PointerEnter(_interactable);

        AssertThat(_log.Entries).IsEmpty();
    }

    // ---- SetInteractable ----

    [TestCase]
    public void SetInteractableTogglesInputPickable()
    {
        _interactable.SetInteractable(false);
        AssertThat(_interactable.InputPickable).IsFalse();

        _interactable.SetInteractable(true);
        AssertThat(_interactable.InputPickable).IsTrue();
    }

    // Pins the current no-dedup behaviour: every call announces, even a repeated value
    [TestCase]
    public void SetInteractableEmitsOnEveryCall()
    {
        _interactable.SetInteractable(true);
        _interactable.SetInteractable(true);
        _interactable.SetInteractable(false);
        _interactable.SetInteractable(false);

        AssertThat(_log.Entries).ContainsExactly("Activated", "Activated", "Deactivated", "Deactivated");
    }

    // Ordering is the point: a subscriber to Deactivated must already see the hover as ended
    [TestCase]
    public void DeactivatingWhileHoveredEndsHoverBeforeDeactivated()
    {
        PointerEnter(_interactable);
        _log.Clear();

        _interactable.SetInteractable(false);

        AssertThat(_log.Entries).ContainsExactly("HoverEnded", "Deactivated");
    }

    // The engine's own mouse_exited arrives a physics tick or more after SetInteractable(false)
    [TestCase]
    public void LateExitAfterHoverEndingDeactivationEmitsNothing()
    {
        PointerEnter(_interactable);
        _interactable.SetInteractable(false);
        _log.Clear();

        PointerExit(_interactable);

        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void DeactivatingWhileNotHoveredEmitsNoHoverEnded()
    {
        _interactable.SetInteractable(false);

        AssertThat(_log.Entries).ContainsExactly("Deactivated");
    }

    // Hover state was reset by the deactivation, so a reactivated element starts a fresh hover
    [TestCase]
    public void ReactivatedElementCanStartAFreshHover()
    {
        PointerEnter(_interactable);
        _interactable.SetInteractable(false);
        _interactable.SetInteractable(true);
        _log.Clear();

        PointerEnter(_interactable);

        AssertThat(_log.Entries).ContainsExactly("HoverStarted");
    }

    // ---- Engine signal path (gdUnit4 monitor) ----
    // AssertSignal needs StartMonitoring() BEFORE the action, IsCountEmitted(0, ...) is the instant
    // absence check, and IsNotEmitted must never be used without WithTimeout (it can hang the test host)

    [TestCase]
    public void GdUnitMonitorSeesClickedThroughTheEngineSignalPath()
    {
        var monitor = AssertSignal(_interactable).StartMonitoring();

        Click(_interactable);

        monitor.IsCountEmitted(1, SpriteInteractable.SignalName.Clicked);
        monitor.IsCountEmitted(0, SpriteInteractable.SignalName.HoverStarted);
    }

    [TestCase]
    public async Task GdUnitMonitorAwaitsHoverStartedEmittedAfterWaitBegins()
    {
        Task waiting = AssertSignal(_interactable).IsEmitted(SpriteInteractable.SignalName.HoverStarted).WithTimeout(1000);

        PointerEnter(_interactable);

        await waiting;
    }
}
