using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// Loads the real printer_screen.tscn: a renamed node or a dropped/retargeted [connection] would
// otherwise only show up in the running game. Deliberately lean; the logic is covered by the
// hand-built suites.
[TestSuite]
[RequireGodotRuntime]
public class PrinterScreenSceneTesting
{
    private const string ScenePath = "res://assets/scenes/office/printer/printer_screen.tscn";

    private ISceneRunner _runner = null!;
    private OfficeScreen _screen = null!;
    private EventLog _log = null!;

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

    // The instance name in office.tscn is the navigation identifier and must equal this root name
    [TestCase]
    public void RootIsAnOfficeScreenNamedAfterItsNavigationIdentifier()
    {
        AssertThat(_screen.Name.ToString()).IsEqual("PrinterScreen");
    }

    [TestCase("Back")]
    [TestCase("ButtonA")]
    [TestCase("ButtonB")]
    [TestCase("ButtonC")]
    [TestCase("ButtonD")]
    public void HotspotIsASpriteInteractableWithAUsableHitbox(string element)
    {
        AssertThat(HasUsableHitbox(_screen.GetNode<SpriteInteractable>(element))).IsTrue();
    }

    // Empty expected target = the click must not navigate. The printer's buttons report to the panel
    // (PanelReportsEachButtonsIndex) and never change screens
    [TestCase("Back", "MainScreen")]
    [TestCase("ButtonA", "")]
    [TestCase("ButtonB", "")]
    [TestCase("ButtonC", "")]
    [TestCase("ButtonD", "")]
    public void ClickRequestsTheExpectedScreen(string element, string expectedTarget)
    {
        Click(_screen.GetNode<SpriteInteractable>(element));

        if (expectedTarget == "") { AssertThat(_log.Entries).IsEmpty(); }
        else { AssertThat(_log.Entries).ContainsExactly("ScreenChange:" + expectedTarget); }
    }

    // The [connection] lines Back -> CursorHint; their target methods must also still exist
    [TestCase]
    public void BackHoverIsWiredToTheCursorHint()
    {
        SpriteInteractable back = _screen.GetNode<SpriteInteractable>("Back");
        CursorHint cursorHint = _screen.GetNode<CursorHint>("CursorHint");

        AssertThat(IsConnected(back, SpriteInteractable.SignalName.HoverStarted, cursorHint, nameof(CursorHint.OnHoverStarted))).IsTrue();
        AssertThat(IsConnected(back, SpriteInteractable.SignalName.HoverEnded, cursorHint, nameof(CursorHint.OnHoverEnded))).IsTrue();
        AssertThat(cursorHint.HasMethod(nameof(CursorHint.OnHoverStarted))).IsTrue();
        AssertThat(cursorHint.HasMethod(nameof(CursorHint.OnHoverEnded))).IsTrue();
    }

    [TestCase]
    public void CursorHintHasTheSharedExitStyle()
    {
        CursorHint cursorHint = _screen.GetNode<CursorHint>("CursorHint");

        AssertThat(cursorHint.Get("_style").AsGodotObject()).IsInstanceOf<CursorHintStyle>();
    }

    // ---- Fixtures (what jobs reach; the names are string literals in job code) ----

    [TestCase]
    public void ExposesThePanelAndTheJobAnchors()
    {
        AssertThat(_screen.Fixtures.Select(fixture => fixture.Name.ToString())).ContainsExactlyInAnyOrder("PrinterPanel", "OutputAnchor", "TicketAnchor");
        AssertThat(_screen.Fixtures.Count).IsEqual(3);
        AssertThat(_screen.TryGetFixture<PrinterPanel>("PrinterPanel", out _)).IsTrue();
        AssertThat(_screen.TryGetFixture<Node2D>("OutputAnchor", out _)).IsTrue();
        AssertThat(_screen.TryGetFixture<Node2D>("TicketAnchor", out _)).IsTrue();
    }

    // Authoring rule nothing enforces: placed content keeps full-canvas coordinates only if its anchor
    // adds no transform of its own
    [TestCase]
    public void AnchorsSitAtIdentityTransform()
    {
        foreach (string name in new[] { "OutputAnchor", "TicketAnchor" })
        {
            Node2D anchor = _screen.GetFixture<Node2D>(name);
            AssertThat(_screen.GlobalTransform.AffineInverse() * anchor.GlobalTransform)
                .OverrideFailureMessage($"{name} is not at identity relative to the screen").IsEqual(Transform2D.Identity);
        }
    }

    // What comes out of the printer is hidden by its body (Front) until it passes the body's lower edge,
    // over the tray painted in Base; notes lie over the body
    [TestCase]
    public void OutputAnchorDrawsBetweenBaseAndFrontAndTicketAnchorInFront()
    {
        int front = _screen.GetNode("Front").GetIndex();
        int output = _screen.GetFixture<Node2D>("OutputAnchor").GetIndex();

        AssertThat(output).IsGreater(_screen.GetNode("Base").GetIndex());
        AssertThat(output).IsLess(front);
        AssertThat(_screen.GetFixture<Node2D>("TicketAnchor").GetIndex()).IsGreater(front);
    }

    // ---- The panel (the device Print Report binds to) ----

    // ButtonA is the print button; the rest are function buttons, each its own index
    [TestCase]
    public void PanelReportsEachButtonsIndex()
    {
        PrinterPanel panel = _screen.GetFixture<PrinterPanel>("PrinterPanel");
        var pressed = new List<int>();
        panel.ButtonPressed += pressed.Add;

        foreach (string button in new[] { "ButtonA", "ButtonB", "ButtonC", "ButtonD" }) { Click(_screen.GetNode<SpriteInteractable>(button)); }

        AssertThat(pressed.Count).IsEqual(4);
        AssertThat(pressed[0]).IsEqual(PrinterPanel.PrintButton);
        AssertThat(pressed.Skip(1).Distinct().Count()).IsEqual(3);
        AssertThat(pressed.Skip(1).All(index => index != PrinterPanel.PrintButton && index < panel.ButtonCount)).IsTrue();
    }

    // A printer hotspot left off the panel would be dead to every job
    [TestCase]
    public void EveryPrinterHotspotButBackIsOnThePanel()
    {
        PrinterPanel panel = _screen.GetFixture<PrinterPanel>("PrinterPanel");
        IEnumerable<Node> onPanel = panel.Get("_buttons").AsGodotArray<Node>();
        Node[] hotspots = InteractablesUnder(_screen).Where(hotspot => hotspot.Name != "Back").ToArray<Node>();

        // The count too: ContainsExactlyInAnyOrder ignores repeats
        AssertThat(onPanel).ContainsExactlyInAnyOrder(hotspots);
        AssertThat(panel.ButtonCount).IsEqual(hotspots.Length);
    }

    // The buttons look alike: each one's light glows at rest (dimmer than lit, over the button's own art) in
    // its own colour, which is how the player tells them apart. Relations only; the values are tuning
    [TestCase]
    public void EveryButtonGlowsAtRestInItsOwnColour()
    {
        PrinterPanel panel = _screen.GetFixture<PrinterPanel>("PrinterPanel");
        IndicatorLight[] lights = Enumerable.Range(0, panel.ButtonCount).Select(panel.GetLight).ToArray();

        foreach (IndicatorLight light in lights)
        {
            float rest = light.Get("_restLevel").AsSingle();
            AssertThat(rest > 0f && rest < light.Get("_peakLevel").AsSingle())
                .OverrideFailureMessage($"{light.GetParent().Name}'s light does not glow dimmer than lit at rest").IsTrue();
            AssertThat(light.GetIndex()).OverrideFailureMessage($"{light.GetParent().Name}'s light draws under its art")
                .IsGreater(light.GetParent().GetNode("Sprite").GetIndex());
        }
        AssertThat(lights.Select(light => (light.LightColor.R, light.LightColor.G, light.LightColor.B)).Distinct().Count()).IsEqual(lights.Length);
    }

    // Hover outline only: the light is their identity, and an idle pulse would compete with it
    [TestCase("ButtonA")]
    [TestCase("ButtonB")]
    [TestCase("ButtonC")]
    [TestCase("ButtonD")]
    public void ButtonHasAHoverOutlineButNoIdlePulse(string button)
    {
        Sprite2D sprite = _screen.GetNode<Sprite2D>($"{button}/Sprite");

        AssertThat(sprite is InteractableFeedback).IsTrue();
        AssertThat(sprite.Get("_idle").AsGodotObject()).IsNull();
        AssertThat(sprite.Get("_hover").AsGodotObject()).IsNotNull();
    }
}
