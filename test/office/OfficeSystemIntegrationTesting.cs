using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// The real composed office.tscn (manager + every real screen), driven end to end by clicking
// through each hotspot's InputEvent signal. Closes the gap the per-screen suites can't: bugs that
// only appear once independently-correct screens are hosted and navigated together (baseline,
// transitions, hidden screens going deaf, navigation targets that name no screen).
// The "navigation sweep" tests discover every screen's links by clicking every hotspot, so a screen
// or hotspot added later is covered without editing this file.
[TestSuite]
[RequireGodotRuntime]
public class OfficeSystemIntegrationTesting
{
    private const string OfficePath = "res://assets/scenes/office/office.tscn";

    private ISceneRunner _runner = null!;
    private OfficeManager _manager = null!;
    private CanvasLayer _layer = null!;

    [BeforeTest]
    public void Setup()
    {
        _runner = ISceneRunner.Load(OfficePath, true, true);
        _manager = (OfficeManager)_runner.Scene()!;
        _layer = _manager.GetNode<CanvasLayer>("ScreenLayer");
    }

    [AfterTest]
    public void Teardown()
    {
        _runner.Dispose();
    }

    private OfficeScreen Screen(string name) => _layer.GetNode<OfficeScreen>(name);

    private SpriteInteractable Element(string screen, string element) => _layer.GetNode<SpriteInteractable>(screen + "/" + element);

    private List<OfficeScreen> Screens() => _layer.GetChildren().Cast<OfficeScreen>().ToList();

    // ---- Baseline ----

    [TestCase]
    public void StartsOnMainScreenWithEveryOtherScreenHidden()
    {
        AssertThat(_manager.CurrentScreen).IsSame(Screen("MainScreen"));
        foreach (OfficeScreen screen in Screens())
        {
            AssertThat(screen.Visible).IsEqual(screen == _manager.CurrentScreen);
        }
    }

    // The baseline must reach the elements too: only the active screen's hotspots can be picked
    [TestCase]
    public void OnlyTheActiveScreensHotspotsArePickable()
    {
        foreach (OfficeScreen screen in Screens())
        {
            bool active = screen == _manager.CurrentScreen;
            foreach (SpriteInteractable interactable in InteractablesUnder(screen))
            {
                AssertThat(interactable.InputPickable).IsEqual(active);
            }
        }
    }

    // The instance name in office.tscn IS the navigation identifier; it must match the scene's own root
    [TestCase]
    public void ScreenInstanceNamesMatchTheirSceneRootNames()
    {
        foreach (OfficeScreen screen in Screens())
        {
            PackedScene scene = GD.Load<PackedScene>(screen.SceneFilePath);

            AssertThat(scene.GetState().GetNodeName(0).ToString()).IsEqual(screen.Name.ToString());
        }
    }

    // ---- Navigation round trips ----

    [TestCase("Computer", "ComputerScreen")]
    [TestCase("Printer", "PrinterScreen")]
    public void HotspotThereAndBackRoundTrips(string element, string destination)
    {
        Click(Element("MainScreen", element));
        AssertThat(_manager.CurrentScreen).IsSame(Screen(destination));

        Click(Element(destination, "Back"));
        AssertThat(_manager.CurrentScreen).IsSame(Screen("MainScreen"));
    }

    [TestCase]
    public void NavigatingSwapsWhichScreenIsVisibleAndPickable()
    {
        Click(Element("MainScreen", "Computer"));

        AssertThat(Screen("MainScreen").Visible).IsFalse();
        AssertThat(Screen("ComputerScreen").Visible).IsTrue();
        AssertThat(InteractablesUnder(Screen("MainScreen")).Any(i => i.InputPickable)).IsFalse();
        AssertThat(InteractablesUnder(Screen("ComputerScreen")).All(i => i.InputPickable)).IsTrue();
    }

    // A hidden screen's hotspot ignores clicks even though its node still exists and its scene is wired
    [TestCase]
    public void ClickingAHiddenScreensHotspotDoesNothing()
    {
        Click(Element("ComputerScreen", "Back"));

        AssertThat(_manager.CurrentScreen).IsSame(Screen("MainScreen"));
    }

    // Hovering a hotspot and then clicking it to leave: SetInteractable(false) must end the hover, or the
    // exit cursor (CursorHint) would stay on screen over the destination
    [TestCase]
    public void NavigatingWhileHoveredEndsTheHover()
    {
        Click(Element("MainScreen", "Computer"));
        EventLog log = new();
        SpriteInteractable back = Element("ComputerScreen", "Back");
        log.Watch(back);
        PointerEnter(back);

        Click(back);

        // Where "Clicked" lands relative to the navigation side effects depends on connection order,
        // which is not the point here
        AssertThat(log.Entries.Count(entry => entry == "HoverEnded")).IsEqual(1);
        AssertThat(log.Entries.ToList().IndexOf("HoverStarted")).IsLess(log.Entries.ToList().IndexOf("HoverEnded"));
    }

    // ---- Navigation sweep ----

    // One probe per hotspot: which screen it asked for (null = nothing) and where the manager landed
    private record NavProbe(string From, string Element, string? Requested, string Landed);

    // Clicks every hotspot of every screen from a freshly activated screen, recording what it asked for
    private List<NavProbe> SweepEveryHotspot()
    {
        EventLog requests = new();
        List<OfficeScreen> screens = Screens();
        // Before the manager's own subscription, or a bad target would abort the chain unrecorded
        foreach (OfficeScreen screen in screens) { requests.WatchFirst(screen); }

        List<NavProbe> probes = new();
        foreach (OfficeScreen screen in screens)
        {
            foreach (SpriteInteractable interactable in InteractablesUnder(screen).ToList())
            {
                _manager.GoToScreen(screen);
                requests.Clear();

                Click(interactable);

                string? requested = requests.Entries.Count == 0 ? null : requests.Entries.Single()["ScreenChange:".Length..];
                probes.Add(new NavProbe(screen.Name, screen.GetPathTo(interactable).ToString(), requested, _manager.CurrentScreen!.Name));
            }
        }
        return probes;
    }

    // Guards the sweep itself against passing vacuously
    [TestCase]
    public void SweepFindsTheKnownNavigationLinks()
    {
        var links = SweepEveryHotspot().Where(p => p.Requested != null).Select(p => (p.From, p.Element, p.Requested)).ToList();

        AssertThat(links).Contains(("MainScreen", "Computer", "ComputerScreen"));
        AssertThat(links).Contains(("MainScreen", "Printer", "PrinterScreen"));
        AssertThat(links).Contains(("ComputerScreen", "Back", "MainScreen"));
        AssertThat(links).Contains(("PrinterScreen", "Back", "MainScreen"));
    }

    // A navigation target is a plain string. A typo'd or renamed one only logs an engine error inside
    // the signal bridge, so the observable symptom is the manager not landing where it was asked
    [TestCase]
    public void EveryRequestedScreenIsHostedAndReached()
    {
        foreach (NavProbe probe in SweepEveryHotspot().Where(p => p.Requested != null))
        {
            AssertThat(_layer.HasNode(probe.Requested!)).IsTrue();
            AssertThat(probe.Landed).IsEqual(probe.Requested!);
        }
    }

    [TestCase]
    public void EveryScreenCanBeReachedFromMainScreenAndLeadBackToIt()
    {
        List<NavProbe> links = SweepEveryHotspot().Where(p => p.Requested != null).ToList();
        string[] everyScreen = Screens().Select(s => s.Name.ToString()).ToArray();

        foreach (string screen in everyScreen)
        {
            AssertThat(Reachable(links, "MainScreen")).Contains(screen);
            AssertThat(Reachable(links, screen)).Contains("MainScreen");
        }
    }

    // Screens reachable from 'start' following the discovered links (including 'start')
    private static HashSet<string> Reachable(List<NavProbe> links, string start)
    {
        HashSet<string> seen = new() { start };
        Queue<string> queue = new();
        queue.Enqueue(start);
        while (queue.Count > 0)
        {
            string current = queue.Dequeue();
            foreach (NavProbe link in links.Where(l => l.From == current))
            {
                if (seen.Add(link.Requested!)) { queue.Enqueue(link.Requested!); }
            }
        }
        return seen;
    }
}
