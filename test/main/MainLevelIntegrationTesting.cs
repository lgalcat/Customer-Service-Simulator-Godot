using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// The real composed main_level.tscn, driven without a mouse: that the pieces are composed as designed
// and that the lock follows the real distraction lifecycle through the real screens. Office's reaction
// to the lock and the Overlay's input blocking are the owning suites' business, not re-tested here.
[TestSuite]
[RequireGodotRuntime]
public class MainLevelIntegrationTesting
{
    private const string LevelPath = "res://assets/scenes/main/main_level.tscn";

    private ISceneRunner _runner = null!;
    private OfficeManager _office = null!;
    private DistractionManager _distractions = null!;

    // Spins frames and waits on real time (the scheduler test)
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(MainLevelIntegrationTesting));

    [AfterTest]
    public void Teardown()
    {
        _runner.Dispose();
    }

    // The Scheduler is freed before tree entry unless the test is about it: no stray timed spawns, and
    // every other test thereby shows the level doesn't depend on one. 'schedulerWait' shrinks its wait to
    // that many seconds (private [Export]s, set through Set() before _Ready())
    private void LoadLevel(float? schedulerWait = null)
    {
        Node level = GD.Load<PackedScene>(LevelPath).Instantiate();
        Node scheduler = level.GetNode("DistractionManager/Scheduler");
        if (schedulerWait is float wait)
        {
            scheduler.Set("_minWait", wait);
            scheduler.Set("_maxWait", wait);
        }
        else
        {
            scheduler.Free();
        }

        _runner = ISceneRunner.Load(level, true, true);
        _office = level.GetNode<OfficeManager>("OfficeManager");
        _distractions = level.GetNode<DistractionManager>("DistractionManager");
    }

    private static Distraction FindHostedDistraction(Control overlay)
    {
        Node host = overlay.GetChild(0);
        SubViewport viewport = host.GetChildren().OfType<SubViewport>().First();
        return viewport.GetChildren().OfType<Distraction>().First();
    }

    private Distraction HostedDistraction() => FindHostedDistraction(_distractions.GetNode<Control>("Overlay"));

    // ---- Composition ----

    [TestCase]
    public void LevelIsComposedAsDesigned()
    {
        LoadLevel();

        AssertThat(_runner.Scene() is MainLevel).IsTrue();
        // The draw-order requirement is structural: a higher layer always draws (and gets input) above
        AssertThat(_distractions.Layer).IsGreater(_office.GetNode<CanvasLayer>("ScreenLayer").Layer);
        AssertThat(_distractions.GetNode<Control>("Overlay").MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
        AssertThat(_office.InputLocked).IsFalse();
        AssertThat(_office.CurrentScreen!.Name.ToString()).IsEqual("MainScreen");
        AssertThat(MainScenePath()).IsEqual(LevelPath);
    }

    // The setting may be stored as a res:// path or as a uid:// reference, depending on who last saved it
    private static string MainScenePath()
    {
        string setting = ProjectSettings.GetSetting("application/run/main_scene").AsString();
        return setting.StartsWith("uid://") ? ResourceUid.UidToPath(setting) : setting;
    }

    // ---- Lock follows the real lifecycle ----

    // The lock wiring is minigame-agnostic by design, hence one case per real implementation
    [TestCase(DistractionType.ThrowPaperBall)]
    [TestCase(DistractionType.FlySwatter)]
    [TestCase(DistractionType.Solitaire)]
    [TestCase(DistractionType.Platformer)]
    public void LockFollowsTheRealDistractionLifecycleForEveryMinigame(DistractionType type)
    {
        LoadLevel();
        var hotspots = InteractablesUnder(_office.CurrentScreen!).ToList();
        AssertThat(hotspots.Count).IsGreater(0);

        bool requested = _distractions.RequestDistraction(type, 1);

        AssertThat(requested).IsTrue();
        AssertThat(_office.InputLocked).IsTrue();
        AssertThat(hotspots.Any(hotspot => hotspot.InputPickable)).IsFalse();

        HostedDistraction().Victory();

        AssertThat(_office.InputLocked).IsFalse();
        AssertThat(hotspots.All(hotspot => hotspot.InputPickable)).IsTrue();
    }

    // A hover in progress when the distraction starts must end exactly once (cursor hint and outline
    // must not outlive the lock), whichever screen is active
    [TestCase("MainScreen")]
    [TestCase("ComputerScreen")]
    [TestCase("PrinterScreen")]
    public void LockCoversEveryScreenAndEndsAHoverInProgress(string screen)
    {
        LoadLevel();
        _office.GoToScreen(new StringName(screen));
        var hotspots = InteractablesUnder(_office.CurrentScreen!).ToList();
        AssertThat(hotspots.Count).IsGreater(0);
        EventLog log = new();
        log.Watch(hotspots[0]);
        PointerEnter(hotspots[0]);
        AssertThat(log.Entries).Contains("HoverStarted");

        _distractions.RequestDistraction(DistractionType.ThrowPaperBall, 1);

        AssertThat(log.Entries.Count(entry => entry == "HoverEnded")).IsEqual(1);
        AssertThat(hotspots.Any(hotspot => hotspot.InputPickable)).IsFalse();

        HostedDistraction().Victory();

        AssertThat(hotspots.All(hotspot => hotspot.InputPickable)).IsTrue();
    }

    // ---- The placeholder scheduler, end to end ----

    // _autoStart is deliberately NOT set here: it comes from main_level.tscn's override, so a dropped
    // override fails this test (nothing would ever spawn)
    [TestCase]
    public async Task SchedulerDrivenSpawnLocksOfficeUntilItCompletes()
    {
        LoadLevel(schedulerWait: 0.05f);

        bool spawned = await WaitUntil(_runner, () => _distractions.IsDistractionActive, 3000);

        AssertThat(spawned).IsTrue();
        AssertThat(_office.InputLocked).IsTrue();

        HostedDistraction().Victory();

        AssertThat(_office.InputLocked).IsFalse();
    }
}
