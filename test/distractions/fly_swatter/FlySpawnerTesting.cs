using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;

// Dedicated component suite for FlySpawner, independent of the Distraction hierarchy.
// Covers FlySpawner's own wave pacing, spawn placement, early refill, and death-forwarding wiring
// For per-instance wiring (FlySwatter integration) see FlySwatterBehaviourTesting
// Not observable, so not tested: the refill's "_refillThreshold > 0" check (an alive count is never below 0, so a
// zero threshold never refills either way). At the cap, the refill's outer "!_spawningStopped" is backed up by the
// inner one: NoRefillAfterStopSpawning is the case that catches the outer check
[TestSuite]
[RequireGodotRuntime]
public class FlySpawnerTesting
{
    private FlySpawner _flySpawner = null!;

    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(FlySpawnerTesting));

    [BeforeTest]
    public void Setup()
    {
        // Barebones setup for a FlySpawner object (testing independent from any scene)
        _flySpawner = AutoFree(new FlySpawner())!;
        _flySpawner.Set("_flyScene", BuildFlyScene());
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // Defensive test to check improper object instancing or scene layout isn't tolerated
    [TestCase]
    public void ReadyThrowsWhenFlySceneMissing()
    {
        FlySpawner orphanSpawner = AutoFree(new FlySpawner())!;

        AssertThrown(() => orphanSpawner._Ready())
            .IsInstanceOf<NullReferenceException>()
            .HasMessage("FlySpawner: _flyScene is not assigned (expected fly.tscn in the Inspector)");
    }

    // This test only checks Timer instancing and non default WaitTime values
    // an exact value of WaitTime is not expected or validated
    [TestCase]
    public void ReadySpawnsConfiguredFirstWaveAndCreatesWaveTimer()
    {
        _flySpawner.Set("_firstWaveSize", 4);

        // Manual _Ready() call - no SceneTree to auto-trigger it (applies to every test below)
        _flySpawner._Ready();
        AutoFreeChildren(_flySpawner);
        Timer waveTimer = _flySpawner.GetChildren().OfType<Timer>().First();

        AssertThat(_flySpawner.GetChildren().OfType<Fly>().Count()).IsEqual(4);
        AssertThat(waveTimer.OneShot).IsFalse();
        AssertThat(waveTimer.WaitTime).IsGreater(0d);
    }

    // Tests the SpawnFly() -> OnFlyDied() -> FlyDied sequence bound per spawned fly,
    // by triggering one spawned fly's own Died action directly
    [TestCase]
    public void SpawnedFlyDeathForwardsFlyDiedUpstream()
    {
        _flySpawner.Set("_firstWaveSize", 1);
        _flySpawner._Ready();
        AutoFreeChildren(_flySpawner);
        Fly fly = _flySpawner.GetChildren().OfType<Fly>().First();
        bool flyDied = false;
        _flySpawner.FlyDied = () => { flyDied = true; };

        fly.Died?.Invoke();

        AssertThat(flyDied).IsTrue();
    }

    // Emitting Timeout directly (rather than waiting out a real Timer) is deliberate - only
    // testing the wave Timer's wiring, not whether Start()/real elapsed time behave correctly
    [TestCase]
    public void WaveTimerTimeoutSpawnsAdditionalWave()
    {
        _flySpawner.Set("_firstWaveSize", 2);
        _flySpawner.Set("_minFliesPerWave", 3);
        _flySpawner.Set("_maxFliesPerWave", 3);
        _flySpawner.Set("_maxTotalFlies", 100);
        _flySpawner._Ready();
        Timer waveTimer = _flySpawner.GetChildren().OfType<Timer>().First();
        int countAfterFirstWave = _flySpawner.GetChildren().OfType<Fly>().Count();

        waveTimer.EmitSignal(Timer.SignalName.Timeout);
        AutoFreeChildren(_flySpawner);

        AssertThat(_flySpawner.GetChildren().OfType<Fly>().Count()).IsEqual(countAfterFirstWave + 3);
    }

    // Verify the total-spawned cap is respected even when a single wave requests more than
    // the cap allows
    [TestCase]
    public void SpawnFliesClampsToTotalCapWithinASingleWave()
    {
        _flySpawner.Set("_maxTotalFlies", 5);
        _flySpawner.Set("_firstWaveSize", 20);
        _flySpawner._Ready();
        AutoFreeChildren(_flySpawner);

        AssertThat(_flySpawner.GetChildren().OfType<Fly>().Count()).IsEqual(5);
    }

    // Design-invariant test: every spawn must land within the stage AND within corridorWidth of
    // one of its edges (a "corridor" spawn, not anywhere on the stage) - holds for every draw,
    // not just typically, so a large sample size just increases confidence, not precision
    [TestCase]
    public void RollSpawnPositionKeepsSpawnsWithinCorridorBand()
    {
        var stageBounds = new Rect2(-50, -50, 100, 100);
        const float corridorWidth = 10f;
        _flySpawner.Set("_stageBounds", stageBounds);
        _flySpawner.Set("_corridorWidth", corridorWidth);
        _flySpawner.Set("_maxTotalFlies", 100);
        _flySpawner.Set("_firstWaveSize", 100);
        _flySpawner._Ready();
        AutoFreeChildren(_flySpawner);

        Vector2 min = stageBounds.Position;
        Vector2 max = stageBounds.End;
        foreach (Fly fly in _flySpawner.GetChildren().OfType<Fly>())
        {
            AssertThat(fly.Position.X).IsGreaterEqual(min.X);
            AssertThat(fly.Position.X).IsLessEqual(max.X);
            AssertThat(fly.Position.Y).IsGreaterEqual(min.Y);
            AssertThat(fly.Position.Y).IsLessEqual(max.Y);

            float distanceToNearestEdge = Mathf.Min(
                Mathf.Min(fly.Position.X - min.X, max.X - fly.Position.X),
                Mathf.Min(fly.Position.Y - min.Y, max.Y - fly.Position.Y)
            );
            AssertThat(distanceToNearestEdge).IsLessEqual(corridorWidth);
        }
    }

    // Verify the publicly accessible StopSpawning() method's internal component update
    // This test case requires a working SceneTree
    [TestCase]
    public void StopSpawningStopsTheWaveTimer()
    {
        // Create "live" object inside a SceneTree
        var parent = new Node2D();
        var flySpawner = new FlySpawner();
        flySpawner.Set("_flyScene", BuildFlyScene());
        parent.AddChild(flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Timer waveTimer = flySpawner.GetChildren().OfType<Timer>().First();

        flySpawner.StopSpawning();

        AssertThat(waveTimer.IsStopped()).IsTrue();
    }

    // Verify reaching the total cap via a later wave (not just the first) also stops the timer
    // This test case requires a working SceneTree
    [TestCase]
    public void ReachingTotalCapAcrossWavesStopsTheWaveTimer()
    {
        // Create "live" object inside a SceneTree
        var parent = new Node2D();
        var flySpawner = new FlySpawner();
        flySpawner.Set("_flyScene", BuildFlyScene());
        flySpawner.Set("_maxTotalFlies", 5);
        flySpawner.Set("_firstWaveSize", 3);
        flySpawner.Set("_minFliesPerWave", 3);
        flySpawner.Set("_maxFliesPerWave", 3);
        parent.AddChild(flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Timer waveTimer = flySpawner.GetChildren().OfType<Timer>().First();
        AssertThat(waveTimer.IsStopped()).IsFalse();

        waveTimer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(waveTimer.IsStopped()).IsTrue();
    }

    // ConfigureWaves() before _Ready(), as its contract requires: the round's first wave, interval and total
    [TestCase]
    public void ConfigureWavesSetsTheRoundsPacing()
    {
        _flySpawner.ConfigureWaves(Plan(total: 9, firstWave: 4, minWave: 2, maxWave: 2, refill: 0, interval: 3f));

        _flySpawner._Ready();
        AutoFreeChildren(_flySpawner);
        Timer waveTimer = _flySpawner.GetChildren().OfType<Timer>().First();

        AssertThat(_flySpawner.GetChildren().OfType<Fly>().Count()).IsEqual(4);
        AssertThat(waveTimer.WaitTime).IsEqual(3d);
        AssertThat(_flySpawner.MaxTotalFlies).IsEqual(9);
    }

    // Each periodic wave rolls its own size within the range: every end turns up across the waves, which a size
    // rolled once per round wouldn't do (missing one of 3 values over 60 waves: ~1e-10)
    [TestCase]
    public void PeriodicWaveSizesStayWithinTheRangeAndVary()
    {
        _flySpawner.ConfigureWaves(Plan(total: 1000, firstWave: 1, minWave: 1, maxWave: 3, refill: 0));
        _flySpawner._Ready();
        Timer waveTimer = _flySpawner.GetChildren().OfType<Timer>().First();
        var sizes = new HashSet<int>();

        for (int i = 0; i < 60; i++)
        {
            int before = _flySpawner.GetChildren().OfType<Fly>().Count();
            waveTimer.EmitSignal(Timer.SignalName.Timeout);
            int size = _flySpawner.GetChildren().OfType<Fly>().Count() - before;
            AssertThat(size).IsBetween(1, 3);
            sizes.Add(size);
        }
        AutoFreeChildren(_flySpawner);

        AssertThat(sizes).ContainsExactlyInAnyOrder(1, 2, 3);
    }

    // A first wave that already uses up the total leaves no wave to wait for
    // This test case requires a working SceneTree (and so does every refill case below)
    [TestCase]
    public void FirstWaveReachingTheCapLeavesTheTimerStopped()
    {
        Node2D parent = BuildSpawnerInParent(Plan(total: 3, firstWave: 5, minWave: 1, maxWave: 1, refill: 0), out FlySpawner flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);

        AssertThat(WaveTimer(flySpawner).IsStopped()).IsTrue();
    }

    // Threshold 2: the first kill leaves 2 alive (not below it), the second leaves 1 and brings a wave at once
    [TestCase]
    public void RefillComesOnlyOnceAliveFliesDropBelowTheThreshold()
    {
        Node2D parent = BuildSpawnerInParent(Plan(total: 100, firstWave: 3, minWave: 2, maxWave: 2, refill: 2), out FlySpawner flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        List<Fly> firstWave = Flies(flySpawner);

        firstWave[0].Swat();
        AssertThat(Flies(flySpawner).Count).IsEqual(3);

        firstWave[1].Swat();
        AssertThat(Flies(flySpawner).Count).IsEqual(5);
    }

    // The timer is stopped by hand first (not through StopSpawning), so only the refill's restart can start it again
    // This test pins current behaviour as of 17-11-2026, changes in design should expect this to fail.
    [TestCase]
    public void RefillRestartsTheWaveTimer()
    {
        Node2D parent = BuildSpawnerInParent(Plan(total: 100, firstWave: 2, minWave: 1, maxWave: 1, refill: 2), out FlySpawner flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Timer waveTimer = WaveTimer(flySpawner);
        waveTimer.Stop();

        Flies(flySpawner)[0].Swat();

        AssertThat(Flies(flySpawner).Count).IsEqual(3);
        AssertThat(waveTimer.IsStopped()).IsFalse();
    }

    [TestCase]
    public void NoRefillAfterStopSpawning()
    {
        Node2D parent = BuildSpawnerInParent(Plan(total: 100, firstWave: 2, minWave: 1, maxWave: 1, refill: 2), out FlySpawner flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        flySpawner.StopSpawning();

        Flies(flySpawner)[0].Swat();

        AssertThat(Flies(flySpawner).Count).IsEqual(2);
        AssertThat(WaveTimer(flySpawner).IsStopped()).IsTrue();
    }

    [TestCase]
    public void NoRefillOnceTheCapIsReached()
    {
        Node2D parent = BuildSpawnerInParent(Plan(total: 2, firstWave: 2, minWave: 1, maxWave: 1, refill: 2), out FlySpawner flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);

        Flies(flySpawner)[0].Swat();

        AssertThat(Flies(flySpawner).Count).IsEqual(2);
        AssertThat(WaveTimer(flySpawner).IsStopped()).IsTrue();
    }

    // The refill wave itself uses up the total, so the interval must not restart after it
    [TestCase]
    public void RefillThatReachesTheCapLeavesTheTimerStopped()
    {
        Node2D parent = BuildSpawnerInParent(Plan(total: 3, firstWave: 2, minWave: 1, maxWave: 1, refill: 2), out FlySpawner flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        AssertThat(WaveTimer(flySpawner).IsStopped()).IsFalse();

        Flies(flySpawner)[0].Swat();

        AssertThat(Flies(flySpawner).Count).IsEqual(3);
        AssertThat(WaveTimer(flySpawner).IsStopped()).IsTrue();
    }

    private static FlyWavePlan Plan(int total, int firstWave, int minWave, int maxWave, int refill, float interval = 4f)
        => new()
        {
            TotalFlies = total,
            FirstWaveSize = firstWave,
            MinFliesPerWave = minWave,
            MaxFliesPerWave = maxWave,
            WaveInterval = interval,
            RefillThreshold = refill
        };

    // Spawner configured with "plan" under a parent root, for tests that need a live SceneTree: its _Ready() runs
    // when the runner loads the parent. Real Swat()s then go through the spawner's own Died binding
    private static Node2D BuildSpawnerInParent(FlyWavePlan plan, out FlySpawner flySpawner)
    {
        var parent = new Node2D();
        flySpawner = new FlySpawner();
        flySpawner.Set("_flyScene", BuildFlyScene());
        flySpawner.ConfigureWaves(plan);
        parent.AddChild(flySpawner);
        return parent;
    }

    // Every fly spawned so far; a swatted fly stays a child until its death timer runs out, and no frames run here
    private static List<Fly> Flies(FlySpawner flySpawner) => flySpawner.GetChildren().OfType<Fly>().ToList();

    private static Timer WaveTimer(FlySpawner flySpawner) => flySpawner.GetChildren().OfType<Timer>().First();

    // Helper building a minimal Fly (testing independent from any scene): only the child nodes
    // Fly.cs's own script logic actually touches. Mirrors FlyTesting/SwatterTesting's BuildFly()
    private static Fly BuildFly()
    {
        var fly = new Fly();

        var aliveDeadFrames = new SpriteFrames();
        aliveDeadFrames.AddAnimation("alive");
        aliveDeadFrames.AddAnimation("dead");
        fly.AddChild(new AnimatedSprite2D { Name = "FlySprite", SpriteFrames = aliveDeadFrames });

        fly.AddChild(new CollisionShape2D { Name = "CollisionShape2D", Shape = new CircleShape2D { Radius = 3f } });

        return fly;
    }

    // Helper packing a minimal hand-built Fly into a fresh, in-memory PackedScene, so FlySpawner's
    // required _flyScene dependency can be satisfied without loading the real fly.tscn
    // Pack() only keeps the nodes the root owns, so the children are given an Owner first; without it the
    // instances would lack FlySprite and fail their _Ready() once they enter a live tree
    private static PackedScene BuildFlyScene()
    {
        Fly sourceFly = BuildFly();
        foreach (Node child in sourceFly.GetChildren()) { child.Owner = sourceFly; }
        var packedScene = new PackedScene();
        packedScene.Pack(sourceFly);
        // Pack() copies the node's state into the resource, it doesn't take ownership
        sourceFly.Free();
        return packedScene;
    }

    // AutoFrees every child of `parent`. The manual _Ready() call above creates the wave Timer and
    // a wave of real Fly children mid-test-body, after `_flySpawner` was already AutoFree-wrapped -
    // so they each need their own cleanup registration or gdUnit4 reports transient orphan nodes
    private static void AutoFreeChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren()) { AutoFree(child); }
    }
}
