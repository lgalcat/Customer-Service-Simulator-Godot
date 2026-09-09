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
}
