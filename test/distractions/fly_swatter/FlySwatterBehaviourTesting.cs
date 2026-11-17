using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;

// TestSuite for FlySwatter-specific behaviour
// (see DistractionTesting/FlySwatterDistractionTesting for the contract-level checks)
// The real scene runs on its authored fallback values (catalog and override cleared), so these cases don't roll a
// WIP level per run; level choice is FlySwatterSetupTesting's, every playable level FlySwatterLevelContentTesting's
[TestSuite]
[RequireGodotRuntime]
public class FlySwatterBehaviourTesting : DistractionTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/fly_swatter/fly_swatter.tscn";
    private const string FlyScenePath = "res://assets/scenes/distractions/fly_swatter/fly.tscn";

    protected override Distraction CreateDistraction()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        return InstantiateOnFallback();
    }

    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(FlySwatterBehaviourTesting));

    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
    }

    [AfterTest]
    public override void Teardown()
    {
        base.Teardown();
    }

    // Tests the FlySpawner.FlyDied -> UpdateScore -> Victory sequence bound in Setup
    // Bounded by the spawner's own public MaxTotalFlies cap
    // this verifies the win score is actually reachable within the spawner's total-fly cap
    [TestCase]
    public void FlyDeathsWithinSpawnCapEventuallyTriggerVictory()
    {
        distraction.Setup(1);
        FlySpawner flySpawner = distraction.GetNode<FlySpawner>("Stage/FlySpawner");
        flySpawner._Ready();
        AutoFreeChildren(flySpawner);
        bool victoryCalled = false;
        distraction.OnVictory = () => { victoryCalled = true; };

        for (int i = 0; i < flySpawner.MaxTotalFlies; i++)
        {
            flySpawner.FlyDied?.Invoke();
        }

        AssertThat(victoryCalled).IsTrue();
    }

    // Defensive test: a single fly death should not be enough to win on its own
    [TestCase]
    public void SingleFlyDeathDoesNotTriggerVictory()
    {
        distraction.Setup(1);
        FlySpawner flySpawner = distraction.GetNode<FlySpawner>("Stage/FlySpawner");
        bool victoryCalled = false;
        distraction.OnVictory = () => { victoryCalled = true; };

        flySpawner.FlyDied?.Invoke();

        AssertThat(victoryCalled).IsFalse();
    }

    // Tests that Victory's explicit "FlyDied -= UpdateScore" unsubscribe actually takes effect:
    // further deaths after winning must not re-trigger Victory
    // Also guarantees no undesired repeat emissions of "OnVictory" action
    [TestCase]
    public void FurtherFlyDeathsAfterVictoryDoNotRetrigger()
    {
        distraction.Setup(1);
        FlySpawner flySpawner = distraction.GetNode<FlySpawner>("Stage/FlySpawner");
        flySpawner._Ready();
        AutoFreeChildren(flySpawner);
        int victoryCount = 0;
        distraction.OnVictory = () => { victoryCount++; };
        for (int i = 0; i < flySpawner.MaxTotalFlies; i++)
        {
            flySpawner.FlyDied?.Invoke();
        }
        AssertThat(victoryCount).IsEqual(1);

        flySpawner.FlyDied?.Invoke();

        AssertThat(victoryCount).IsEqual(1);
    }

    // Tests that Victory()'s _flySpawner.StopSpawning() call actually stops the wave timer, not
    // just that scoring stops (see FurtherFlyDeathsAfterVictoryDoNotRetrigger for that half).
    // Needs a real SceneTree for Timer.IsStopped() to reflect Stop()'s effect. Setup() is the
    // hosting factory's job at runtime; call it before tree entry (the runner enters on Load),
    // since _Ready() no longer does (mirrors ThrowPaperBallBehaviourTesting.ChargingAndReleasingThrowsTheBall)
    [TestCase]
    public void VictoryStopsTheSpawnerWaveTimer()
    {
        FlySwatter flySwatter = InstantiateOnFallback();
        flySwatter.Setup(1);
        using ISceneRunner runner = ISceneRunner.Load(flySwatter, true, true);
        FlySpawner flySpawner = flySwatter.GetNode<FlySpawner>("Stage/FlySpawner");
        Timer waveTimer = flySpawner.GetChildren().OfType<Timer>().First();
        AssertThat(waveTimer.IsStopped()).IsFalse();

        for (int i = 0; i < flySpawner.MaxTotalFlies; i++)
        {
            flySpawner.FlyDied?.Invoke();
        }

        AssertThat(waveTimer.IsStopped()).IsTrue();
    }

    // The winning kill's Victory() stops the spawner before the spawner's own refill check runs, so a stage left
    // low by that kill gets no last wave. In-memory level: win 2, refill below 3 alive, first wave 3, waves of 1
    // This test case requires a working SceneTree
    [TestCase]
    public void WinningKillDoesNotBringARefill()
    {
        FlySwatter flySwatter = GD.Load<PackedScene>(ScenePath).Instantiate<FlySwatter>();
        flySwatter.LevelOverride = new FlySwatterLevel
        {
            MinWinScore = 2, MaxWinScore = 2,
            MinSpareFlies = 10, MaxSpareFlies = 10,
            MinFirstWaveSize = 3, MaxFirstWaveSize = 3,
            MinFliesPerWave = 1, MaxFliesPerWave = 1,
            MinWaveInterval = 4, MaxWaveInterval = 4,
            RefillThreshold = 3
        };
        flySwatter.Setup(1);
        using ISceneRunner runner = ISceneRunner.Load(flySwatter, true, true);
        FlySpawner flySpawner = flySwatter.GetNode<FlySpawner>("Stage/FlySpawner");
        Timer waveTimer = flySpawner.GetChildren().OfType<Timer>().First();
        var firstWave = flySpawner.GetChildren().OfType<Fly>().ToList();
        bool victoryCalled = false;
        flySwatter.OnVictory = () => { victoryCalled = true; };
        // A non-winning kill leaves 2 alive and refills, so refills are live in this round
        firstWave[0].Swat();
        AssertThat(flySpawner.GetChildren().OfType<Fly>().Count()).IsEqual(4);

        firstWave[1].Swat();

        AssertThat(victoryCalled).IsTrue();
        AssertThat(flySpawner.GetChildren().OfType<Fly>().Count()).IsEqual(4);
        AssertThat(waveTimer.IsStopped()).IsTrue();
    }

    // Tests that the ScoreTracker label counts down in step with real FlyDied events and lands
    // on exactly "0" when Victory fires. Bounded by the tracker's own starting reading rather
    // than the private _winScore, to avoid tailoring the test to today's magic number
    [TestCase]
    public void ScoreTrackerCountsDownToZeroAtVictory()
    {
        distraction.Setup(1);
        FlySpawner flySpawner = distraction.GetNode<FlySpawner>("Stage/FlySpawner");
        Label scoreTracker = distraction.GetNode<Label>("Stage/ScoreTracker");
        flySpawner._Ready();
        AutoFreeChildren(flySpawner);
        bool victoryCalled = false;
        distraction.OnVictory = () => { victoryCalled = true; };
        int startingCount = int.Parse(scoreTracker.Text);

        for (int i = 0; i < startingCount; i++)
        {
            flySpawner.FlyDied?.Invoke();
            AssertThat(int.Parse(scoreTracker.Text)).IsEqual(startingCount - (i + 1));
        }

        AssertThat(scoreTracker.Text).IsEqual("0");
        AssertThat(victoryCalled).IsTrue();
    }

    // Tests that a missing ScoreTracker node doesn't break Setup()/scoring/Victory() - the
    // tracker is explicitly optional (nullable field, no throw on a failed lookup), unlike
    // FlySpawner's own mandatory lookup
    [TestCase]
    public void SetupToleratesMissingScoreTracker()
    {
        // Barebones stand-in tree without a ScoreTracker node, mirroring
        // FlySwatterDistractionTesting.CreateDistraction()'s stand-in shape
        var flySwatter = AutoFree(new FlySwatter())!;
        var stage = new Node2D { Name = "Stage" };
        flySwatter.AddChild(stage);
        var flySpawner = new FlySpawner { Name = "FlySpawner" };
        // _Ready() requires a real _flyScene; reuse the real fly.tscn rather than duplicating
        // FlySpawnerTesting's PackedScene.Pack() helper into a fourth file for this one test
        flySpawner.Set("_flyScene", GD.Load<PackedScene>(FlyScenePath));
        stage.AddChild(flySpawner);

        flySwatter.Setup(1);
        flySpawner._Ready();
        AutoFreeChildren(flySpawner);
        bool victoryCalled = false;
        flySwatter.OnVictory = () => { victoryCalled = true; };

        for (int i = 0; i < flySpawner.MaxTotalFlies; i++)
        {
            flySpawner.FlyDied?.Invoke();
        }

        AssertThat(victoryCalled).IsTrue();
    }

    // Tests that the swatter's hit-scan area is big enough to actually catch a fly, not just
    // theoretically overlap it - checked against a freshly instanced fly.tscn, not a hardcoded size
    [TestCase]
    public void HitScanCoversAFlyHitbox()
    {
        Swatter swatter = distraction.GetNode<Swatter>("Stage/Swatter");
        var hitScanShape = (RectangleShape2D)swatter.GetNode<ShapeCast2D>("HitScan").Shape;
        Fly fly = AutoFree(GD.Load<PackedScene>(FlyScenePath).Instantiate<Fly>())!;

        float flyHitboxDiameter = FlyHitboxDiameter(fly);
        AssertThat(hitScanShape.Size.X).IsGreater(flyHitboxDiameter);
        AssertThat(hitScanShape.Size.Y).IsGreater(flyHitboxDiameter);
    }

    // The real scene with its level catalog and override cleared, so Setup() keeps the authored fallback values
    private static FlySwatter InstantiateOnFallback()
    {
        FlySwatter flySwatter = GD.Load<PackedScene>(ScenePath).Instantiate<FlySwatter>();
        flySwatter.Set("_levelCatalog", new Variant());
        flySwatter.LevelOverride = null;
        return flySwatter;
    }

    // Helper to find a Fly's hittable width (assumes shape is a CircleShape2D)
    private static float FlyHitboxDiameter(Fly fly)
    {
        var flyShape = fly.GetNode<CollisionShape2D>("CollisionShape2D");
        return ((CircleShape2D)flyShape.Shape).Radius * 2;
    }

    // AutoFrees every child of `parent`. FlySpawner._Ready() (called manually here, since the
    // fixture never enters a live SceneTree) creates its wave Timer and a first wave of real Fly
    // children mid-test-body, after `distraction` was already AutoFree-wrapped - so they each need
    // their own cleanup registration or gdUnit4 reports transient orphan nodes
    private static void AutoFreeChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren()) { AutoFree(child); }
    }
}