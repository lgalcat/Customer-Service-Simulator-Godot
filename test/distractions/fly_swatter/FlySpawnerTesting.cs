using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Linq;

// Dedicated component suite for FlySpawner, independent of the Distraction hierarchy.
// Covers FlySpawner's own wave pacing, spawn placement, and death-forwarding wiring
// For per-instance wiring (FlySwatter integration) see FlySwatterBehaviourTesting
[TestSuite]
[RequireGodotRuntime]
public class FlySpawnerTesting
{
    private FlySpawner _flySpawner = null!;

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

        AssertThrown(() => orphanSpawner._Ready()).IsInstanceOf<NullReferenceException>();
    }

    // This test only checks Timer instancing and non default WaitTime values
    // an exact value of WaitTime is not expected or validated
    [TestCase]
    public void ReadySpawnsConfiguredFirstWaveAndCreatesWaveTimer()
    {
        _flySpawner.Set("_firstWaveSize", 4);

        // Manual call for setup, no SceneTree to auto call _Ready()
        _flySpawner._Ready();
        // The wave Timer and the whole first wave of flies are created mid-test-body, after
        // _flySpawner was already wrapped, so they need explicit AutoFree cleanup registration
        foreach (Node child in _flySpawner.GetChildren()) { AutoFree(child); }
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
        // Manual call for setup, no SceneTree to auto call _Ready()
        _flySpawner._Ready();
        // Created mid-test-body, after _flySpawner was already wrapped, so they need explicit
        // AutoFree cleanup registration
        foreach (Node child in _flySpawner.GetChildren()) { AutoFree(child); }
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
        _flySpawner.Set("_fliesPerWave", 3);
        _flySpawner.Set("_maxTotalFlies", 100);
        // Manual call for setup, no SceneTree to auto call _Ready()
        _flySpawner._Ready();
        Timer waveTimer = _flySpawner.GetChildren().OfType<Timer>().First();
        int countAfterFirstWave = _flySpawner.GetChildren().OfType<Fly>().Count();

        waveTimer.EmitSignal(Timer.SignalName.Timeout);
        // The wave Timer and every spawned fly (both waves) are created mid-test-body, after
        // _flySpawner was already wrapped, so they need explicit AutoFree cleanup registration
        foreach (Node child in _flySpawner.GetChildren()) { AutoFree(child); }

        AssertThat(_flySpawner.GetChildren().OfType<Fly>().Count()).IsEqual(countAfterFirstWave + 3);
    }

    // Verify the total-spawned cap is respected even when a single wave requests more than
    // the cap allows
    [TestCase]
    public void SpawnFliesClampsToTotalCapWithinASingleWave()
    {
        _flySpawner.Set("_maxTotalFlies", 5);
        _flySpawner.Set("_firstWaveSize", 20);
        // Manual call for setup, no SceneTree to auto call _Ready()
        _flySpawner._Ready();
        // The wave Timer and every spawned fly are created mid-test-body, after _flySpawner was
        // already wrapped, so they need explicit AutoFree cleanup registration
        foreach (Node child in _flySpawner.GetChildren()) { AutoFree(child); }

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
        // Manual call for setup, no SceneTree to auto call _Ready()
        _flySpawner._Ready();
        // The wave Timer and every spawned fly are created mid-test-body, after _flySpawner was
        // already wrapped, so they need explicit AutoFree cleanup registration
        foreach (Node child in _flySpawner.GetChildren()) { AutoFree(child); }

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

    // Verify the publicly accesible StopSpawning() method's internal component update
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
        flySpawner.Set("_fliesPerWave", 3);
        parent.AddChild(flySpawner);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Timer waveTimer = flySpawner.GetChildren().OfType<Timer>().First();
        AssertThat(waveTimer.IsStopped()).IsFalse();

        waveTimer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(waveTimer.IsStopped()).IsTrue();
    }

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
    private static PackedScene BuildFlyScene()
    {
        Fly sourceFly = BuildFly();
        var packedScene = new PackedScene();
        packedScene.Pack(sourceFly);
        // Pack() copies the node's state into the resource, it doesn't take ownership
        sourceFly.Free();
        return packedScene;
    }
}
