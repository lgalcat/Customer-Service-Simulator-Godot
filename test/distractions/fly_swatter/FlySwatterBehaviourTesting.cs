using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;

// TestSuite for FlySwatter-specific behaviour
// (see DistractionTesting/FlySwatterDistractionTesting for the contract-level checks)
[TestSuite]
[RequireGodotRuntime]
public class FlySwatterBehaviourTesting : DistractionTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/fly_swatter/fly_swatter.tscn";
    private const string FlyScenePath = "res://assets/scenes/distractions/fly_swatter/fly.tscn";

    protected override Distraction CreateDistraction()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        return GD.Load<PackedScene>(ScenePath).Instantiate<FlySwatter>();
    }

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
    // Bounded by the spawner's own public _maxTotalFlies cap
    // this verifies the win score is actually reachable within the spawner's total-fly cap
    [TestCase]
    public void FlyDeathsWithinSpawnCapEventuallyTriggerVictory()
    {
        distraction.Setup(1);
        FlySpawner flySpawner = distraction.GetNode<FlySpawner>("Stage/FlySpawner");
        // FlySpawner never entered a live SceneTree here, but Victory() (via UpdateScore) calls
        // FlySpawner.StopSpawning(), which needs the wave Timer _Ready() creates - which in turn
        // also spawns the first wave of real Fly children. All created mid-test, after distraction
        // was already wrapped, so they need explicit AutoFree cleanup registration
        flySpawner._Ready();
        foreach (Node child in flySpawner.GetChildren()) { AutoFree(child); }
        bool victoryCalled = false;
        distraction.OnVictory = () => { victoryCalled = true; };

        for (int i = 0; i < flySpawner._maxTotalFlies; i++)
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
    // Also guarantees no undesired repeat emmisions of "OnVictory" action
    [TestCase]
    public void FurtherFlyDeathsAfterVictoryDoNotRetrigger()
    {
        distraction.Setup(1);
        FlySpawner flySpawner = distraction.GetNode<FlySpawner>("Stage/FlySpawner");
        // FlySpawner never entered a live SceneTree here, but Victory() (via UpdateScore) calls
        // FlySpawner.StopSpawning(), which needs the wave Timer _Ready() creates - which in turn
        // also spawns the first wave of real Fly children. All created mid-test, after distraction
        // was already wrapped, so they need explicit AutoFree cleanup registration
        flySpawner._Ready();
        foreach (Node child in flySpawner.GetChildren()) { AutoFree(child); }
        int victoryCount = 0;
        distraction.OnVictory = () => { victoryCount++; };
        for (int i = 0; i < flySpawner._maxTotalFlies; i++)
        {
            flySpawner.FlyDied?.Invoke();
        }
        AssertThat(victoryCount).IsEqual(1);

        flySpawner.FlyDied?.Invoke();

        AssertThat(victoryCount).IsEqual(1);
    }

    // Tests that Victory()'s _flySpawner.StopSpawning() call actually stops the wave timer, not
    // just that scoring stops (see FurtherFlyDeathsAfterVictoryDoNotRetrigger for that half).
    // Needs a real SceneTree for Timer.IsStopped() to reflect Stop()'s effect - loads the real
    // scene directly (mirrors ThrowPaperBallBehaviourTesting.ChargingAndReleasingThrowsTheBall),
    // whose own _Ready() already calls Setup(1), so Setup() isn't called again here
    [TestCase]
    public void VictoryStopsTheSpawnerWaveTimer()
    {
        using ISceneRunner runner = ISceneRunner.Load(ScenePath, true, true);
        FlySwatter flySwatter = (FlySwatter)runner.Scene()!;
        FlySpawner flySpawner = flySwatter.GetNode<FlySpawner>("Stage/FlySpawner");
        Timer waveTimer = flySpawner.GetChildren().OfType<Timer>().First();
        AssertThat(waveTimer.IsStopped()).IsFalse();

        for (int i = 0; i < flySpawner._maxTotalFlies; i++)
        {
            flySpawner.FlyDied?.Invoke();
        }

        AssertThat(waveTimer.IsStopped()).IsTrue();
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

    // Helper to find a Fly's hittable width (assumes shape is a CircleShape2D)
    private static float FlyHitboxDiameter(Fly fly)
    {
        var flyShape = fly.GetNode<CollisionShape2D>("CollisionShape2D");
        return ((CircleShape2D)flyShape.Shape).Radius * 2;
    }
}