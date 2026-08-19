using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;
using System.Threading.Tasks;

// Dedicated component suite for Fly, independent of the Distraction hierarchy.
// Covers Fly's own state machine, hit/death handling, and alive/dead-state movement math
// For real-scene drift coverage see FlySceneTesting
// For per-instance wiring (spawner/swatter integration) see FlySwatterBehaviourTesting/SwatterTesting
[TestSuite]
[RequireGodotRuntime]
public class FlyTesting
{
    private Fly _fly = null!;

    [BeforeTest]
    public void Setup()
    {
        // Barebones setup for a Fly object (testing independent from any scene)
        _fly = AutoFree(BuildFly())!;
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // This test only checks Timer instancing and non default WaitTime values
    // an exact value of WaitTime is not expected or validated
    [TestCase]
    public void ReadySetsAliveStateAndCreatesDeathTimer()
    {
        // Manual call for setup, no SceneTree to auto call _Ready()
        _fly._Ready();
        Timer deathTimer = AutoFree(_fly.GetChildren().OfType<Timer>().First())!;

        AssertThat(_fly.IsAlive).IsTrue();
        AssertThat(deathTimer.OneShot).IsTrue();
        AssertThat(deathTimer.WaitTime).IsGreater(0d);
    }

    // Verify state machine Alive -> Dead update path, and the guard preventing a second Swat()
    [TestCase]
    public void SwatFromAliveTransitionsToDeadAndFiresDiedOnce()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _fly._Ready();
        AutoFree(_fly.GetChildren().OfType<Timer>().First());
        int diedCount = 0;
        _fly.Died = () => { diedCount++; };

        _fly.Swat();
        _fly.Swat();

        AssertThat(_fly.IsAlive).IsFalse();
        AssertThat(diedCount).IsEqual(1);
    }

    // Emitting Timeout directly (rather than waiting out a real Timer) is deliberate - only
    // testing the death Timer's wiring, not whether Start()/real elapsed time behave correctly.
    // Uses its own live-tree fixture (not the shared _fly) since QueueFree()'s deferred deletion
    // needs a real SceneTree to actually process - mixing that with the shared fixture's own
    // AutoFree cleanup left unfreed orphans behind
    // This test case requires a working SceneTree
    [TestCase]
    public async Task TimerTimeoutQueuesFlyForDeletion()
    {
        // Create "live" object inside a SceneTree; entering the tree auto-calls _Ready().
        // The fly is a child of a separate parent root (not the loaded root itself) - the
        // SceneRunner's own Dispose() can't handle its loaded root freeing itself mid-test
        var parent = new Node2D();
        Fly fly = BuildFly();
        parent.AddChild(fly);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Timer deathTimer = fly.GetChildren().OfType<Timer>().First();

        deathTimer.EmitSignal(Timer.SignalName.Timeout);
        await runner.SimulateFrames(1);

        AssertThat(GodotObject.IsInstanceValid(fly)).IsFalse();
    }

    // Verify the AnimationChanged -> modulate wiring bound in _Ready(), for both animations it
    // reacts to. Driven directly via EmitSignal rather than Play(), since Play()'s own signal
    // emission outside a live SceneTree isn't something this suite wants to depend on
    [TestCase]
    public void AnimationChangedTogglesModulateForAliveAndDead()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _fly._Ready();
        AutoFree(_fly.GetChildren().OfType<Timer>().First());
        AnimatedSprite2D sprite = _fly.GetNode<AnimatedSprite2D>("FlySprite");

        sprite.Animation = "dead";
        sprite.EmitSignal(AnimatedSprite2D.SignalName.AnimationChanged);
        AssertThat(sprite.Modulate.A).IsLess(1f);

        sprite.Animation = "alive";
        sprite.EmitSignal(AnimatedSprite2D.SignalName.AnimationChanged);
        AssertThat(sprite.Modulate).IsEqual(Colors.White);
    }

    // Collapsing MinSpeed/MaxSpeed to a single value via Configure() makes the otherwise-random
    // per-step speed deterministic, without needing to seed the global RNG. Heading stays random,
    // but displacement magnitude over one step is fully determined by speed regardless of heading
    [TestCase]
    public void AliveStateMovesAtConfiguredSpeedMagnitude()
    {
        const float speed = 40f;
        const float delta = 0.1f;
        _fly.Configure(BuildProfile(minSpeed: speed, maxSpeed: speed), DefaultBounds());
        // Manual setup call, no SceneTree to call _Ready()
        _fly._Ready();
        AutoFree(_fly.GetChildren().OfType<Timer>().First());
        Vector2 start = _fly.Position;

        _fly._Process(delta);

        AssertThat(Mathf.Abs(_fly.Position.DistanceTo(start) - speed * delta)).IsLess(0.001f);
    }

    // Verify RollNewStep()'s speed reroll never leaves the configured [MinSpeed, MaxSpeed] range,
    // across many steps (short step durations force frequent rerolls within a handful of frames)
    [TestCase]
    public void ConfiguredSpeedRangeIsRespectedAcrossManySteps()
    {
        const float minSpeed = 20f;
        const float maxSpeed = 30f;
        const float delta = 0.05f;
        var profile = BuildProfile(minSpeed: minSpeed, maxSpeed: maxSpeed);
        profile.MinStepDuration = 0.01f;
        profile.MaxStepDuration = 0.02f;
        _fly.Configure(profile, DefaultBounds());
        // Manual setup call, no SceneTree to call _Ready()
        _fly._Ready();
        AutoFree(_fly.GetChildren().OfType<Timer>().First());

        for (int i = 0; i < 30; i++)
        {
            Vector2 before = _fly.Position;
            _fly._Process(delta);
            float impliedSpeed = _fly.Position.DistanceTo(before) / delta;

            AssertThat(impliedSpeed).IsGreaterEqual(minSpeed - 0.01f);
            AssertThat(impliedSpeed).IsLessEqual(maxSpeed + 0.01f);
        }
    }

    // Verify ContainWithinBounds() actually clamps Position into a test-controlled movementBounds
    // (not the production default) - a large fixed speed forces overshoot in a single step
    [TestCase]
    public void ContainWithinBoundsClampsPositionIntoConfiguredBounds()
    {
        var bounds = new Rect2(-5, -5, 10, 10);
        _fly.Configure(BuildProfile(minSpeed: 1000f, maxSpeed: 1000f), bounds);
        // Manual setup call, no SceneTree to call _Ready()
        _fly._Ready();
        AutoFree(_fly.GetChildren().OfType<Timer>().First());

        _fly._Process(0.1f);

        AssertThat(_fly.Position.X).IsGreaterEqual(bounds.Position.X);
        AssertThat(_fly.Position.X).IsLessEqual(bounds.End.X);
        AssertThat(_fly.Position.Y).IsGreaterEqual(bounds.Position.Y);
        AssertThat(_fly.Position.Y).IsLessEqual(bounds.End.Y);
    }

    // Dead-state drift is a plain constant-velocity move, not randomized - fully deterministic.
    // Uses a test-controlled _deadVelocity (via the [Export] property setter, same technique
    // SwatterTesting uses for _movementBounds) rather than hardcoding the production default
    [TestCase]
    public void DeadStateDriftsAtConfiguredDeadVelocity()
    {
        var deadVelocity = new Vector2(30, -10);
        _fly.Set("_deadVelocity", deadVelocity);
        // Manual setup call, no SceneTree to call _Ready()
        _fly._Ready();
        AutoFree(_fly.GetChildren().OfType<Timer>().First());
        _fly.Swat();
        Vector2 start = _fly.Position;

        _fly._Process(0.1f);

        AssertThat(_fly.Position.DistanceTo(start + deadVelocity * 0.1f)).IsLess(0.001f);
    }

    // Helper building a minimal Fly (testing independent from any scene): only the child nodes
    // Fly.cs's own script logic actually touches
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

    // Helper building a FlyDifficultyProfile with every non-speed field defaulted to a harmless,
    // fixed value - only the fields a given test actually cares about should be overridden
    private static FlyDifficultyProfile BuildProfile(float minSpeed, float maxSpeed)
    {
        return new FlyDifficultyProfile
        {
            MinSpeed = minSpeed,
            MaxSpeed = maxSpeed,
            MinStepDuration = 1f,
            MaxStepDuration = 1f,
            ArcChance = 0f,
            MinTurnRate = 0f,
            MaxTurnRate = 0f,
            MinHeadingDeviation = 0f,
            MaxHeadingDeviation = 0f,
            SpeedChangeChance = 1f
        };
    }

    // Design-independent default bounds for tests that don't care about clamping specifically
    private static Rect2 DefaultBounds()
    {
        return new Rect2(-1000, -1000, 2000, 2000);
    }
}
