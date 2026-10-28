using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// Loads the real computer_screen.tscn: a renamed node or a dropped/retargeted [connection] would
// otherwise only show up in the running game. Deliberately lean; the logic is covered by the
// hand-built suites.
[TestSuite]
[RequireGodotRuntime]
public class ComputerScreenSceneTesting
{
    private const string ScenePath = "res://assets/scenes/office/computer/computer_screen.tscn";

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
        AssertThat(_screen.Name.ToString()).IsEqual("ComputerScreen");
    }

    [TestCase]
    public void BackIsASpriteInteractableWithAUsableHitbox()
    {
        AssertThat(HasUsableHitbox(_screen.GetNode<SpriteInteractable>("Back"))).IsTrue();
    }

    [TestCase]
    public void ClickingBackRequestsMainScreen()
    {
        Click(_screen.GetNode<SpriteInteractable>("Back"));

        AssertThat(_log.Entries).ContainsExactly("ScreenChange:MainScreen");
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
    public void ExposesTheJobAnchors()
    {
        AssertThat(_screen.Fixtures.Select(fixture => fixture.Name.ToString())).ContainsExactlyInAnyOrder("MonitorAnchor", "DocumentAnchor");
        AssertThat(_screen.Fixtures.Count).IsEqual(2);
        AssertThat(_screen.TryGetFixture<Node2D>("MonitorAnchor", out _)).IsTrue();
        AssertThat(_screen.TryGetFixture<Node2D>("DocumentAnchor", out _)).IsTrue();
    }

    // Authoring rule nothing enforces: placed content keeps full-canvas coordinates only if its anchor
    // adds no transform of its own
    [TestCase]
    public void AnchorsSitAtIdentityTransform()
    {
        foreach (string name in new[] { "MonitorAnchor", "DocumentAnchor" })
        {
            Node2D anchor = _screen.GetFixture<Node2D>(name);
            AssertThat(_screen.GlobalTransform.AffineInverse() * anchor.GlobalTransform)
                .OverrideFailureMessage($"{name} is not at identity relative to the screen").IsEqual(Transform2D.Identity);
        }
    }

    // The monitor's display is a transparent hole in Front, so the record shows through the bezel; the
    // document lies on the desk, over Front
    [TestCase]
    public void MonitorAnchorDrawsBehindFrontAndDocumentAnchorInFront()
    {
        int front = _screen.GetNode("Front").GetIndex();

        AssertThat(_screen.GetFixture<Node2D>("MonitorAnchor").GetIndex()).IsLess(front);
        AssertThat(_screen.GetFixture<Node2D>("DocumentAnchor").GetIndex()).IsGreater(front);
    }
}
