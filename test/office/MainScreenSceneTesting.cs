using System;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// Loads the real main_screen.tscn to catch what hand-built suites can't see by construction: a
// renamed node, a dropped or retargeted [connection], a style resource left unassigned. Logic lives
// in SpriteInteractableTesting/InteractableFeedbackTesting; this stays a drift tripwire. Clicks are
// driven through the interactable's InputEvent signal, so the whole chain from the click to the
// screen's navigation request (signal, scene connection, bound argument) is exercised.
[TestSuite]
[RequireGodotRuntime]
public class MainScreenSceneTesting
{
    private const string ScenePath = "res://assets/scenes/office/main_screen/main_screen.tscn";

    private ISceneRunner _runner = null!;
    private OfficeScreen _screen = null!;
    private EventLog _log = null!;

    // Hover/idle checks wait on real tweens, so this suite is CPU-load sensitive
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(MainScreenSceneTesting));

    [BeforeTest]
    public void Setup()
    {
        _runner = ISceneRunner.Load(ScenePath, true, true);
        _screen = (OfficeScreen)_runner.Scene()!;
        _log = new EventLog();
        _log.Watch(_screen);
    }

    [AfterTest]
    public void Teardown()
    {
        _runner.Dispose();
    }

    private SpriteInteractable Element(string name) => _screen.GetNode<SpriteInteractable>(name);

    private Sprite2D SpriteOf(string element) => _screen.GetNode<Sprite2D>(element + "/Sprite");

    private Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 3000) =>
        OfficeTestKit.WaitUntil(_runner, condition, timeoutMs);

    // ---- Structure ----

    // The instance name in office.tscn is the navigation identifier and must equal this root name
    [TestCase]
    public void RootIsAnOfficeScreenNamedAfterItsNavigationIdentifier()
    {
        AssertThat(_screen.Name.ToString()).IsEqual("MainScreen");
    }

    [TestCase("Door")]
    [TestCase("Computer")]
    [TestCase("Phone")]
    [TestCase("Printer")]
    public void ElementIsASpriteInteractableWithAUsableHitbox(string element)
    {
        AssertThat(HasUsableHitbox(Element(element))).IsTrue();
    }

    // ---- Navigation wiring (empty expected target = the click must not navigate) ----

    [TestCase("Computer", "ComputerScreen")]
    [TestCase("Printer", "PrinterScreen")]
    [TestCase("Door", "")]
    [TestCase("Phone", "")]
    public void ClickRequestsTheExpectedScreen(string element, string expectedTarget)
    {
        Click(Element(element));

        if (expectedTarget == "") { AssertThat(_log.Entries).IsEmpty(); }
        else { AssertThat(_log.Entries).ContainsExactly("ScreenChange:" + expectedTarget); }
    }

    // ---- Feedback configuration (which channels each element was authored with) ----

    [TestCase("Computer", true, true)]
    [TestCase("Printer", true, true)]
    [TestCase("Door", false, true)]
    [TestCase("Phone", false, true)]
    public void ElementHasTheExpectedFeedbackStyles(string element, bool hasIdle, bool hasHover)
    {
        Sprite2D sprite = SpriteOf(element);

        AssertThat(sprite is InteractableFeedback).IsTrue();
        AssertThat(sprite.Get("_idle").AsGodotObject() != null).IsEqual(hasIdle);
        AssertThat(sprite.Get("_hover").AsGodotObject() != null).IsEqual(hasHover);
    }

    // The shipped style + shader actually drive the real sprite: hovering fades the outline uniform in
    [TestCase("Door")]
    [TestCase("Computer")]
    [TestCase("Phone")]
    [TestCase("Printer")]
    public async Task HoveringAnElementFadesItsOutlineIn(string element)
    {
        PointerEnter(Element(element));

        bool reached = await WaitUntil(() =>
        {
            Variant hover = SpriteOf(element).GetInstanceShaderParameter("hover");
            return hover.VariantType != Variant.Type.Nil && hover.AsSingle() >= 0.99f;
        });

        AssertThat(reached).IsTrue();
    }

    // Idle pulse is visible on the elements that were given one. Wait and threshold come from the
    // element's own authored delay and style, so retuning them (designer-owned) cannot break this
    [TestCase("Computer")]
    [TestCase("Printer")]
    public async Task IdleElementPulsesOnItsOwn(string element)
    {
        Sprite2D sprite = SpriteOf(element);
        IdlePulseStyle style = (IdlePulseStyle)sprite.Get("_idle").AsGodotObject();
        float delay = sprite.Get("_idleDelay").AsSingle();
        float halfwayToPeak = 1f + (style.PulseColor.R - 1f) / 2f;
        int timeoutMs = (int)((delay + 2f * (style.HalfPeriod + style.RestPeriod) + 1f) * 1000f);

        bool pulsed = await WaitUntil(() => sprite.SelfModulate.R > halfwayToPeak, timeoutMs);

        AssertThat(pulsed).IsTrue();
    }
}
