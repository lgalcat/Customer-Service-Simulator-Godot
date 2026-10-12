using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// The one Office suite that uses real simulated mouse input through the engine's physics picking:
// what emitting an InputEvent signal can't prove - that each hand-fitted polygon hitbox is hit
// where it should be, nothing else is, and a hidden screen stays deaf. Targets are derived from
// each hotspot's own polygon (its deepest interior point), not hard-coded pixels, so retuned
// hitboxes keep working; waits poll for outcomes instead of counting frames.
[TestSuite]
[RequireGodotRuntime]
public class OfficeInputTesting
{
    private const string MainScreenPath = "res://assets/scenes/office/main_screen/main_screen.tscn";
    private const string PrinterScreenPath = "res://assets/scenes/office/printer/printer_screen.tscn";
    private const string OfficePath = "res://assets/scenes/office/office.tscn";

    private ISceneRunner _runner = null!;
    private EventLog _log = null!;

    // Simulated mouse input goes through the process-wide Input singleton
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(OfficeInputTesting));

    [BeforeTest]
    public void Setup()
    {
        _log = new EventLog();
    }

    [AfterTest]
    public void Teardown()
    {
        _runner.SimulateMouseButtonRelease(MouseButton.Left);
        _runner.Dispose();
    }

    private Node Load(string path)
    {
        _runner = ISceneRunner.Load(path, true, true);
        return _runner.Scene()!;
    }

    // Records every hotspot's signals under 'root', labelled with the hotspot's node name
    private void WatchAll(Node root)
    {
        foreach (SpriteInteractable interactable in InteractablesUnder(root)) { _log.Watch(interactable, interactable.Name); }
    }

    private IEnumerable<string> Clicks() => _log.Entries.Where(entry => entry.EndsWith(".Clicked"));

    private Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 1500) =>
        OfficeTestKit.WaitUntil(_runner, condition, timeoutMs);

    private async Task Settle() => await _runner.AwaitMillis(200);

    // The simulated pointer outlives a test (it lives in the Input singleton): a freshly loaded scene
    // is greeted by hover events for wherever the previous test left it. Park it off-screen, let those
    // settle, and forget them, so a test only sees what it causes itself
    private async Task ParkPointer()
    {
        _runner.SimulateMouseMove(new Vector2(-100, -100));
        await _runner.AwaitMillis(150);
        _log.Clear();
    }

    // Picking runs on physics frames: move first so the pointer is over the hotspot, let it settle,
    // then press
    private async Task ClickAt(Vector2 position)
    {
        _runner.SimulateMouseMove(position);
        await _runner.AwaitMillis(100);
        _runner.SimulateMouseButtonPressed(MouseButton.Left);
        await _runner.AwaitMillis(100);
    }

    // ---- Targets derived from the authored polygons ----

    private static CollisionPolygon2D PolygonOf(Area2D hotspot) => hotspot.GetChildren().OfType<CollisionPolygon2D>().First();

    private static Vector2[] GlobalPolygon(CollisionPolygon2D polygon) => polygon.Polygon.Select(polygon.ToGlobal).ToArray();

    // The grid point inside 'polygon' farthest from all of its edges: a robust click target even for a
    // concave or ring-shaped hitbox, where the bounding-box centre may lie outside. Ties (a band has a
    // whole line of equally deep points) go to the one nearest the bounding-box centre, which keeps
    // the target out of corners, where a concave hitbox's convex pieces overlap and a click is
    // delivered once per piece
    private static Vector2 DeepestPoint(CollisionPolygon2D hotspotPolygon)
    {
        Vector2[] outline = GlobalPolygon(hotspotPolygon);
        Vector2 center = new((outline.Min(p => p.X) + outline.Max(p => p.X)) / 2f, (outline.Min(p => p.Y) + outline.Max(p => p.Y)) / 2f);
        Vector2? best = null;
        float bestDepth = -1f;
        for (float y = outline.Min(p => p.Y); y <= outline.Max(p => p.Y); y += 4f)
        {
            for (float x = outline.Min(p => p.X); x <= outline.Max(p => p.X); x += 4f)
            {
                Vector2 candidate = new(x, y);
                if (!Geometry2D.IsPointInPolygon(candidate, outline)) { continue; }
                float depth = DistanceToEdges(candidate, outline);
                bool deeper = depth > bestDepth + 0.01f;
                bool tiedButCloserToCenter = Math.Abs(depth - bestDepth) <= 0.01f && candidate.DistanceTo(center) < best!.Value.DistanceTo(center);
                if (deeper || tiedButCloserToCenter) { best = candidate; bestDepth = depth; }
            }
        }
        return best ?? throw new InvalidOperationException("No interior point found for " + hotspotPolygon.GetParent().Name);
    }

    // The point inside the 640x360 screen farthest from every hotspot outline and outside all of them
    private static Vector2 DeepestBlankPoint(IEnumerable<CollisionPolygon2D> hotspotPolygons)
    {
        List<Vector2[]> outlines = hotspotPolygons.Select(GlobalPolygon).ToList();
        Vector2? best = null;
        float bestDepth = -1f;
        for (float y = 4f; y < 360f; y += 4f)
        {
            for (float x = 4f; x < 640f; x += 4f)
            {
                Vector2 candidate = new(x, y);
                if (outlines.Any(outline => Geometry2D.IsPointInPolygon(candidate, outline))) { continue; }
                float depth = outlines.Min(outline => DistanceToEdges(candidate, outline));
                if (depth > bestDepth) { best = candidate; bestDepth = depth; }
            }
        }
        return best ?? throw new InvalidOperationException("No blank point found");
    }

    private static float DistanceToEdges(Vector2 point, Vector2[] outline)
    {
        float nearest = float.MaxValue;
        for (int i = 0; i < outline.Length; i++)
        {
            Vector2 onEdge = Geometry2D.GetClosestPointToSegment(point, outline[i], outline[(i + 1) % outline.Length]);
            nearest = Math.Min(nearest, point.DistanceTo(onEdge));
        }
        return nearest;
    }

    private static Vector2 DeepestPointOf(Node screen, string hotspot) => DeepestPoint(PolygonOf(screen.GetNode<Area2D>(hotspot)));

    // ---- Hit accuracy on the main screen ----

    [TestCase("Door")]
    [TestCase("Computer")]
    [TestCase("Phone")]
    [TestCase("Printer")]
    public async Task ClickAtAHotspotsDeepestPointReachesOnlyThatHotspot(string element)
    {
        Node screen = Load(MainScreenPath);
        WatchAll(screen);
        await ParkPointer();

        await ClickAt(DeepestPointOf(screen, element));

        AssertThat(await WaitUntil(() => Clicks().Any())).IsTrue();
        AssertThat(Clicks()).ContainsExactly(element + ".Clicked");
    }

    [TestCase("Door")]
    [TestCase("Computer")]
    [TestCase("Phone")]
    [TestCase("Printer")]
    public async Task HoveringAHotspotsDeepestPointStartsAndEndsItsHoverOnly(string element)
    {
        Node screen = Load(MainScreenPath);
        WatchAll(screen);
        Vector2 blank = DeepestBlankPoint(InteractablesUnder(screen).Select(PolygonOf));
        await ParkPointer();

        _runner.SimulateMouseMove(DeepestPointOf(screen, element));
        AssertThat(await WaitUntil(() => _log.Entries.Contains(element + ".HoverStarted"))).IsTrue();

        _runner.SimulateMouseMove(blank);
        AssertThat(await WaitUntil(() => _log.Entries.Contains(element + ".HoverEnded"))).IsTrue();
        AssertThat(_log.Entries.Where(entry => entry.EndsWith(".HoverStarted"))).ContainsExactly(element + ".HoverStarted");
    }

    [TestCase]
    public async Task ClickingBlankBackgroundReachesNoHotspot()
    {
        Node screen = Load(MainScreenPath);
        WatchAll(screen);
        await ParkPointer();

        await ClickAt(DeepestBlankPoint(InteractablesUnder(screen).Select(PolygonOf)));
        await Settle();

        AssertThat(_log.Entries).IsEmpty();
    }

    // ---- Back hotspots (edge band) ----

    [TestCase("res://assets/scenes/office/computer/computer_screen.tscn")]
    [TestCase("res://assets/scenes/office/printer/printer_screen.tscn")]
    public async Task BackIsClickableInItsBandButNotAtTheScreenCentre(string scenePath)
    {
        OfficeScreen screen = (OfficeScreen)Load(scenePath);
        _log.Watch(screen);

        await ClickAt(new Vector2(320, 180));
        await Settle();
        AssertThat(_log.Entries).IsEmpty();

        await ClickAt(DeepestPointOf(screen, "Back"));
        AssertThat(await WaitUntil(() => _log.Entries.Count > 0)).IsTrue();
        AssertThat(_log.Entries).ContainsExactly("ScreenChange:MainScreen");
    }

    // ---- Printer screen buttons ----

    [TestCase("ButtonA")]
    [TestCase("ButtonB")]
    [TestCase("ButtonC")]
    [TestCase("ButtonD")]
    public async Task PrinterButtonIsClickableAtItsDeepestPointAndNothingElseIsHit(string button)
    {
        Node screen = Load(PrinterScreenPath);
        WatchAll(screen);
        await ParkPointer();

        await ClickAt(DeepestPointOf(screen, button));

        AssertThat(await WaitUntil(() => Clicks().Any())).IsTrue();
        AssertThat(Clicks()).ContainsExactly(button + ".Clicked");
    }

    // ---- Real clicks through the composed office ----

    [TestCase("Computer", "ComputerScreen")]
    [TestCase("Printer", "PrinterScreen")]
    public async Task RealClicksRoundTripThroughTheComposedOffice(string element, string destination)
    {
        OfficeManager office = (OfficeManager)Load(OfficePath);
        Node layer = office.GetNode("ScreenLayer");
        Vector2 there = DeepestPointOf(layer.GetNode("MainScreen"), element);
        Vector2 back = DeepestPointOf(layer.GetNode(destination), "Back");

        await ClickAt(there);
        AssertThat(await WaitUntil(() => office.CurrentScreen?.Name == destination)).IsTrue();

        await ClickAt(back);
        AssertThat(await WaitUntil(() => office.CurrentScreen?.Name == "MainScreen")).IsTrue();
    }

    // A corner click on the Back band is delivered twice (once per convex piece of the hitbox). The
    // first delivery navigates and deactivates the screen synchronously, so SpriteInteractable's
    // state guard makes the second a no-op. A standalone screen with no manager would see two requests
    [TestCase]
    public async Task CornerClickOnBackNavigatesExactlyOnceInTheComposedOffice()
    {
        OfficeManager office = (OfficeManager)Load(OfficePath);
        Node layer = office.GetNode("ScreenLayer");
        await ClickAt(DeepestPointOf(layer.GetNode("MainScreen"), "Computer"));
        AssertThat(await WaitUntil(() => office.CurrentScreen?.Name == "ComputerScreen")).IsTrue();
        // Recorded before the manager's own handler so every request is seen, whatever happens next
        _log.WatchFirst(layer.GetNode<OfficeScreen>("ComputerScreen"));

        await ClickAt(new Vector2(12, 12));
        AssertThat(await WaitUntil(() => office.CurrentScreen?.Name == "MainScreen")).IsTrue();
        await Settle();

        AssertThat(_log.Entries).ContainsExactly("ScreenChange:MainScreen");
    }

    // The hidden MainScreen's Computer still sits at the same position under the active screen
    [TestCase]
    public async Task HiddenScreensHotspotStaysDeafAtTheSamePosition()
    {
        OfficeManager office = (OfficeManager)Load(OfficePath);
        Node layer = office.GetNode("ScreenLayer");
        SpriteInteractable mainComputer = layer.GetNode<SpriteInteractable>("MainScreen/Computer");
        Vector2 computerPoint = DeepestPointOf(layer.GetNode("MainScreen"), "Computer");

        await ClickAt(computerPoint);
        AssertThat(await WaitUntil(() => office.CurrentScreen?.Name == "ComputerScreen")).IsTrue();
        _log.Watch(mainComputer, "Computer");

        await ClickAt(computerPoint);
        await Settle();

        AssertThat(Clicks()).IsEmpty();
    }
}
