using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

// Dedicated component suite for DistractionManager, independent of the Distraction hierarchy -
// exercises the single-active lifecycle via the InitialFactory injection seam, no real minigame
// scenes involved (see DistractionSystemIntegrationTesting for the real end-to-end path)
[TestSuite]
[RequireGodotRuntime]
public class DistractionManagerTesting
{
    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(DistractionManagerTesting));

    [TestCase]
    public void ReadyThrowsWhenOverlayMissing()
    {
        DistractionManager manager = AutoFree(new DistractionManager())!;
        manager.InitialFactory = new DistractionFactory(SingleTypeMap());

        AssertThrown(() => manager._Ready()).IsInstanceOf<NullReferenceException>();
    }

    // GetNode<Control>() throws its own InvalidCastException for a present-but-wrong-type child,
    // never returning null - so the manual "if (_overlay == null) throw" guard is unreachable here
    [TestCase]
    public void ReadyThrowsWhenOverlayIsWrongType()
    {
        DistractionManager manager = AutoFree(new DistractionManager())!;
        manager.InitialFactory = new DistractionFactory(SingleTypeMap());
        manager.AddChild(new Node2D { Name = "Overlay" });

        AssertThrown(() => manager._Ready()).IsInstanceOf<InvalidCastException>();
    }

    [TestCase]
    public void RequestDistractionHostsMinigameUnderOverlay()
    {
        (DistractionManager manager, Control overlay) = BuildManager();

        bool requested = manager.RequestDistraction();
        AutoFreeChildren(overlay);

        AssertThat(requested).IsTrue();
        AssertThat(overlay.GetChildCount()).IsEqual(1);
        AssertThat(manager.IsDistractionActive).IsTrue();
    }

    // Regression pin for the KeepSize fix - the default Minsize preset previously collapsed the
    // host, since a Stretch=true SubViewportContainer reports zero minimum size
    [TestCase]
    public void RequestDistractionKeepsHostSizeAfterCentering()
    {
        (DistractionManager manager, Control overlay) = BuildManager();

        manager.RequestDistraction();
        AutoFreeChildren(overlay);
        Control host = (Control)overlay.GetChild(0);
        Distraction distraction = FindHostedDistraction(overlay);

        AssertThat(host.Size).IsEqual(distraction.ViewportSize);
    }

    [TestCase]
    public void RequestDistractionWiresVictoryToComplete()
    {
        (DistractionManager manager, Control overlay) = BuildManager();
        manager.RequestDistraction();
        AutoFreeChildren(overlay);
        Distraction distraction = FindHostedDistraction(overlay);
        int completedCount = 0;
        manager.DistractionCompleted += () => { completedCount++; };

        distraction.Victory();

        AssertThat(completedCount).IsEqual(1);
        AssertThat(manager.IsDistractionActive).IsFalse();
    }

    // Requires a live tree for QueueFree() to actually process
    [TestCase]
    public async Task CompleteFreesHostAfterVictory()
    {
        var manager = new DistractionManager();
        var overlay = new Control { Name = "Overlay" };
        manager.AddChild(overlay);
        manager.InitialFactory = new DistractionFactory(SingleTypeMap());
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);

        manager.RequestDistraction();
        FindHostedDistraction(overlay).Victory();
        await runner.SimulateFrames(1);

        AssertThat(overlay.GetChildCount()).IsEqual(0);
    }

    [TestCase]
    public void RequestDistractionWhileActiveIsRejectedWithoutSideEffects()
    {
        (DistractionManager manager, Control overlay) = BuildManager();
        manager.RequestDistraction();
        AutoFreeChildren(overlay);
        Distraction firstDistraction = FindHostedDistraction(overlay);

        bool secondRequest = manager.RequestDistraction();

        AssertThat(secondRequest).IsFalse();
        AssertThat(overlay.GetChildCount()).IsEqual(1);

        int completedCount = 0;
        manager.DistractionCompleted += () => { completedCount++; };

        firstDistraction.Victory();

        AssertThat(completedCount).IsEqual(1);
    }

    // OnVictory is nulled by Complete() - a further Victory() call on the same (already-completed)
    // Distraction must not refire DistractionCompleted
    [TestCase]
    public void VictoryAfterCompleteDoesNotRefireCompleted()
    {
        (DistractionManager manager, Control overlay) = BuildManager();
        manager.RequestDistraction();
        AutoFreeChildren(overlay);
        Distraction distraction = FindHostedDistraction(overlay);
        int completedCount = 0;
        manager.DistractionCompleted += () => { completedCount++; };
        distraction.Victory();

        distraction.Victory();

        AssertThat(completedCount).IsEqual(1);
    }

    [TestCase]
    public void RequestDistractionDefaultsToBaseDifficultyAndFactoryRandomType()
    {
        (DistractionManager manager, Control overlay) = BuildManager();

        manager.RequestDistraction();
        AutoFreeChildren(overlay);

        int baseDifficulty = manager.Get("_baseDifficulty").AsInt32();
        Distraction distraction = FindHostedDistraction(overlay);
        AssertThat(distraction.Difficulty).IsEqual(baseDifficulty);
    }

    // Requires a live tree for QueueFree() to actually process before the second request
    [TestCase]
    public async Task RequestDistractionSucceedsAgainAfterCompletion()
    {
        var manager = new DistractionManager();
        var overlay = new Control { Name = "Overlay" };
        manager.AddChild(overlay);
        manager.InitialFactory = new DistractionFactory(SingleTypeMap());
        using ISceneRunner runner = ISceneRunner.Load(manager, true, true);
        manager.RequestDistraction();
        FindHostedDistraction(overlay).Victory();
        await runner.SimulateFrames(1);

        bool secondRequest = manager.RequestDistraction();

        AssertThat(secondRequest).IsTrue();
    }

    // Hand-built DistractionManager + Overlay child, wired with a stub-backed factory via the
    // InitialFactory seam - manual _Ready() call, no SceneTree needed unless a test needs real
    // QueueFree() processing (see the live-tree tests above, built independently of this helper)
    private static (DistractionManager manager, Control overlay) BuildManager()
    {
        DistractionManager manager = AutoFree(new DistractionManager())!;
        var overlay = new Control { Name = "Overlay" };
        manager.AddChild(overlay);
        manager.InitialFactory = new DistractionFactory(SingleTypeMap());
        manager._Ready();
        return (manager, overlay);
    }

    // Host -> SubViewport -> Distraction is BuildHost's exact nesting
    private static Distraction FindHostedDistraction(Control overlay)
    {
        Node host = overlay.GetChild(0);
        SubViewport viewport = host.GetChildren().OfType<SubViewport>().First();
        return viewport.GetChildren().OfType<Distraction>().First();
    }

    private static Dictionary<DistractionType, PackedScene> SingleTypeMap()
    {
        return new Dictionary<DistractionType, PackedScene>
        {
            { DistractionType.ThrowPaperBall, StubDistraction.Pack() },
        };
    }

    // AutoFrees every child of `parent` - RequestDistraction() adds a real host tree mid-test,
    // after `manager`/`overlay` may already be AutoFree-wrapped, so it needs its own registration
    // or gdUnit4 reports a transient orphan (mirrors FlySpawnerTesting.AutoFreeChildren)
    private static void AutoFreeChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren()) { AutoFree(child); }
    }
}