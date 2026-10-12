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

    // Empty expected target = the click must not navigate. The buttons do nothing yet; this pins that
    // until they gain behaviour
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
}
