using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;
using System.Threading.Tasks;

// Integration suite: the real distraction_manager.tscn against all four real minigame scenes via
// DistractionFactory.CreateDefault() (InitialFactory left unset) - the end-to-end path nothing
// else in the suite drives, which is exactly what let the now-removed OwnHostControl self-hosting
// seam ship a real runtime crash past a full test-green run
[TestSuite]
[RequireGodotRuntime]
public class DistractionSystemIntegrationTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/distraction_manager.tscn";

    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(DistractionSystemIntegrationTesting));

    [TestCase(DistractionType.ThrowPaperBall)]
    [TestCase(DistractionType.FlySwatter)]
    [TestCase(DistractionType.Solitaire)]
    [TestCase(DistractionType.Platformer)]
    public async Task RequestDistractionHostsAndTearsDownRealMinigame(DistractionType type)
    {
        await RunFullCycle(type);
    }

    // Most common call a scheduled/default spawn makes in production (no explicit type)
    // Relies on RandomType()'s own distribution guarantee (DistractionFactoryTesting)
    // This test aims to validate the null-coalescing wiring to the real CreateDefault() pool doesn't break
    [TestCase]
    public async Task RequestDistractionWithNoTypeHostsAndTearsDownARealMinigame()
    {
        await RunFullCycle(null);
    }

    private static async Task RunFullCycle(DistractionType? type)
    {
        using ISceneRunner runner = ISceneRunner.Load(ScenePath, true, true);
        var manager = (DistractionManager)runner.Scene()!;
        Control overlay = manager.GetNode<Control>("Overlay");
        int completedCount = 0;
        manager.DistractionCompleted += () => { completedCount++; };

        bool requested = manager.RequestDistraction(type, 1);
        AssertThat(requested).IsTrue();
        AssertThat(overlay.GetChildCount()).IsEqual(1);
        AssertThat(manager.IsDistractionActive).IsTrue();

        Distraction distraction = FindHostedDistraction(overlay);
        distraction.Victory();

        AssertThat(completedCount).IsEqual(1);
        AssertThat(manager.IsDistractionActive).IsFalse();

        await runner.SimulateFrames(1);

        AssertThat(overlay.GetChildCount()).IsEqual(0);
    }

    // Host -> SubViewport -> Distraction is BuildHost's exact nesting
    private static Distraction FindHostedDistraction(Control overlay)
    {
        Node host = overlay.GetChild(0);
        SubViewport viewport = host.GetChildren().OfType<SubViewport>().First();
        return viewport.GetChildren().OfType<Distraction>().First();
    }

    // ---- Overlay modality under real input ----
    //
    // The Overlay's MouseFilter value is unit-tested in DistractionManagerTesting; whether the engine then
    // withholds physics-picking presses (including ones outside the minigame's window) from an Area2D
    // below is only visible with real input. The backdrop is a synthetic full-viewport Area2D, so no
    // Office geometry is involved.

    private const string ManagerPath = "res://assets/scenes/distractions/distraction_manager.tscn";
    // A corner of the 640x360 viewport: far outside any centred minigame window
    private static readonly Vector2 CornerPoint = new(6, 6);
    private static readonly Vector2 NearbyPoint = new(12, 12);

    private sealed class PointerProbe
    {
        public int Presses;
        public int Enters;
    }

    // Backdrop Area2D (counting into the probe) + the real distraction_manager.tscn, not yet in a tree
    private static (Node2D root, DistractionManager manager, PointerProbe probe) BuildBackdrop()
    {
        PointerProbe probe = new();
        Area2D area = new();
        area.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(640, 360) }, Position = new Vector2(320, 180) });
        area.InputEvent += (_, @event, _) =>
        {
            if (@event is InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) { probe.Presses++; }
        };
        area.MouseEntered += () => probe.Enters++;

        CanvasLayer world = new() { Layer = 0 };
        world.AddChild(area);
        DistractionManager manager = GD.Load<PackedScene>(ManagerPath).Instantiate<DistractionManager>();
        Node2D root = new();
        root.AddChild(world);
        root.AddChild(manager);
        return (root, manager, probe);
    }

    // Simulated pointer state lives in the process-wide Input singleton and outlives a test: park it
    // off-screen so the backdrop only ever sees what this test causes
    private static async Task ParkPointer(ISceneRunner runner, PointerProbe probe)
    {
        runner.SimulateMouseMove(new Vector2(-100, -100));
        await runner.AwaitMillis(150);
        probe.Presses = 0;
        probe.Enters = 0;
    }

    private static async Task PressAt(ISceneRunner runner, Vector2 position)
    {
        runner.SimulateMouseMove(position);
        await runner.AwaitMillis(100);
        runner.SimulateMouseButtonPressed(MouseButton.Left);
        await runner.AwaitMillis(100);
        runner.SimulateMouseButtonRelease(MouseButton.Left);
        await runner.AwaitMillis(50);
    }

    // The control: proves the harness itself delivers presses and hovers, so the blocked case below
    // can't pass merely because the backdrop never receives anything
    [TestCase]
    public async Task PointerReachesAnAreaBehindAnIdleOverlay()
    {
        (Node2D root, _, PointerProbe probe) = BuildBackdrop();
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        await ParkPointer(runner, probe);

        await PressAt(runner, CornerPoint);

        AssertThat(probe.Enters).IsEqual(1);
        AssertThat(probe.Presses).IsEqual(1);
    }

    // Hover (MouseEntered) is deliberately not asserted: it still reaches the Area2D under a Stop Control.
    // Suppressing it is OfficeManager.InputLocked's job (InputPickable = false), not the Overlay's.
    [TestCase]
    public async Task OverlayBlocksPressFromAnAreaBehindItWhileActive()
    {
        (Node2D root, DistractionManager manager, PointerProbe probe) = BuildBackdrop();
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        Control overlay = manager.GetNode<Control>("Overlay");
        manager.RequestDistraction(DistractionType.ThrowPaperBall, 1);
        await ParkPointer(runner, probe);
        // Read after frames have passed, so the centring has laid the window out
        Control window = (Control)overlay.GetChild(0);
        AssertThat(window.GetGlobalRect().HasPoint(CornerPoint)).IsFalse();
        AssertThat(window.GetGlobalRect().HasPoint(NearbyPoint)).IsFalse();

        await PressAt(runner, CornerPoint);

        AssertThat(probe.Presses).IsEqual(0);

        // Once the minigame is gone the same pointer reaches the backdrop again
        FindHostedDistraction(overlay).Victory();
        await PressAt(runner, NearbyPoint);

        AssertThat(probe.Presses).IsEqual(1);
    }
}
