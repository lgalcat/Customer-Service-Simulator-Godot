using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;
using System.Threading.Tasks;

// Dedicated component suite for Swatter, independent of the Distraction hierarchy.
// Covers Swatter's own swing/cooldown state machine, hit-detection, and mouse-follow/
// containment - not whether FlySwatter wires it correctly (it doesn't - see FlySwatterBehaviourTesting)
[TestSuite]
[RequireGodotRuntime]
public class SwatterTesting
{
    private Swatter _swatter = null!;

    [BeforeTest]
    public void Setup()
    {
        // Barebones setup for a Swatter object (testing independent from any scene)
        _swatter = AutoFree(BuildSwatter())!;
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // This test only checks Timer instancing and non default WaitTime values
    // an exact value of WaitTime is not expected or validated
    [TestCase]
    public void ReadySetsIdleStateAndCreatesCooldownTimer()
    {
        // Manual call for setup, no SceneTree to auto call _Ready()
        _swatter._Ready();
        Timer cooldownTimer = AutoFree(_swatter.GetChildren().OfType<Timer>().First())!;

        AssertThat(_swatter.CanSwat).IsTrue();
        AssertThat(cooldownTimer.OneShot).IsTrue();
        AssertThat(cooldownTimer.WaitTime).IsGreater(0d);
    }

    // Verify the smack FX starts hidden regardless of whatever the scene/editor left it as
    [TestCase]
    public void ReadyHidesSmackFxInitially()
    {
        AnimatedSprite2D smackFx = _swatter.GetNode<AnimatedSprite2D>("SmackFx");

        // Manual call for setup, no SceneTree to auto call _Ready()
        _swatter._Ready();
        AutoFree(_swatter.GetChildren().OfType<Timer>().First());

        AssertThat(smackFx.Visible).IsFalse();
    }

    // Emitting AnimationFinished directly (rather than waiting out a real animation) is deliberate,
    // mirrors the same technique already used for Area2D/Timer signal-driven wiring elsewhere
    [TestCase]
    public void SmackFxAutoHidesAfterAnimationFinishes()
    {
        // Manual call for setup, no SceneTree to auto call _Ready()
        _swatter._Ready();
        AutoFree(_swatter.GetChildren().OfType<Timer>().First());
        AnimatedSprite2D smackFx = _swatter.GetNode<AnimatedSprite2D>("SmackFx");
        // Simulate a smack animation still in progress
        smackFx.Visible = true;

        smackFx.EmitSignal(AnimatedSprite2D.SignalName.AnimationFinished);

        AssertThat(smackFx.Visible).IsFalse();
    }

    // Verify the mouse-follow lerp in _Process actually converges toward the simulated cursor
    // This test case requires a working SceneTree
    [TestCase]
    public async Task MouseFollowMovesSwatterTowardCursor()
    {
        // Create "live" object inside a SceneTree
        Node2D parent = BuildSwatterInParent(out Swatter swatter);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Vector2 target = new Vector2(50, 30);

        runner.SimulateMouseMove(target);
        await runner.SimulateFrames(300);

        AssertThat(swatter.Position.DistanceTo(target)).IsLess(1f);
    }

    // Verify the anchor never leaves the configured playspace, even chasing a far-off cursor
    // Uses a test-controlled _movementBounds (set like the .tscn itself would) rather than the
    // production default, to avoid tailoring this test to today's magic numbers
    // This test case requires a working SceneTree
    [TestCase]
    public async Task PositionIsClampedToMovementBounds()
    {
        // Create "live" object inside a SceneTree
        Node2D parent = BuildSwatterInParent(out Swatter swatter);
        var bounds = new Rect2(-20, -20, 40, 40);
        swatter.Set("_movementBounds", bounds);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);

        runner.SimulateMouseMove(new Vector2(10000, 10000));
        await runner.SimulateFrames(60);
        AssertThat(swatter.Position.X).IsLessEqual(bounds.End.X);
        AssertThat(swatter.Position.Y).IsLessEqual(bounds.End.Y);

        runner.SimulateMouseMove(new Vector2(-10000, -10000));
        await runner.SimulateFrames(60);
        AssertThat(swatter.Position.X).IsGreaterEqual(bounds.Position.X);
        AssertThat(swatter.Position.Y).IsGreaterEqual(bounds.Position.Y);
    }

    // Defensive test to verify state machine safeguards preventing illegal state updates
    // This test case requires a working SceneTree (Swing()'s hit-scan needs a real physics step)
    [TestCase]
    public async Task SwingIsIgnoredWhenNotIdle()
    {
        // Create "live" object inside a SceneTree
        Node2D parent = BuildSwatterInParent(out Swatter swatter);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Sprite2D racketSprite = swatter.GetNode<Sprite2D>("SwatterSprite");
        runner.SimulateMouseButtonPressed(MouseButton.Left);
        await runner.SimulateFrames(2);
        AssertThat(swatter.CanSwat).IsFalse();
        // Detectable probe: a wrongly-accepted second swing would flip this back to the cooldown tint
        racketSprite.Modulate = Colors.White;

        runner.SimulateMouseButtonPressed(MouseButton.Left);
        await runner.SimulateFrames(2);

        AssertThat(racketSprite.Modulate).IsEqual(Colors.White);
    }

    // Verify Timeout's Cooldown -> Idle update path and related visual reset
    // This test case requires a working SceneTree
    [TestCase]
    public async Task CooldownTimeoutRevertsModulateAndReallowsSwinging()
    {
        // Create "live" object inside a SceneTree
        Node2D parent = BuildSwatterInParent(out Swatter swatter);
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        Sprite2D racketSprite = swatter.GetNode<Sprite2D>("SwatterSprite");
        Timer cooldownTimer = swatter.GetChildren().OfType<Timer>().First();
        runner.SimulateMouseButtonPressed(MouseButton.Left);
        await runner.SimulateFrames(2);
        AssertThat(swatter.CanSwat).IsFalse();
        AssertThat(racketSprite.Modulate).IsNotEqual(Colors.White);

        cooldownTimer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(swatter.CanSwat).IsTrue();
        AssertThat(racketSprite.Modulate).IsEqual(Colors.White);
    }

    // Verify Swing() actually swats every overlapping alive fly in one pass (not just a single
    // one), while correctly leaving a non-overlapping fly untouched - HitScan is a 20x20 rect
    // centered on the swatter, so +-5 offsets stay inside it and +100 lands well outside
    // This test case requires a working SceneTree (hit-scan needs a real physics step)
    [TestCase]
    public async Task SwingSwatsAllOverlappingAliveFliesAndSkipsOutOfRangeOnes()
    {
        // Create "live" object inside a SceneTree
        Node2D parent = BuildSwatterInParent(out Swatter swatter);
        Vector2 swingSpot = new Vector2(40, 40);
        Fly flyInRangeA = BuildFly();
        flyInRangeA.Position = swingSpot;
        parent.AddChild(flyInRangeA);
        Fly flyInRangeB = BuildFly();
        flyInRangeB.Position = swingSpot + new Vector2(5, 5);
        parent.AddChild(flyInRangeB);
        Fly flyOutOfRange = BuildFly();
        flyOutOfRange.Position = swingSpot + new Vector2(100, 100);
        parent.AddChild(flyOutOfRange);
        // Parked directly at the flies' spot rather than relying on the mouse-follow lerp to get
        // there - this test is about hit-detection, not about how fast the follow converges
        // (see MouseFollowMovesSwatterTowardCursor for that)
        swatter.Position = swingSpot;
        using ISceneRunner runner = ISceneRunner.Load(parent, true, true);
        AnimatedSprite2D smackFx = swatter.GetNode<AnimatedSprite2D>("SmackFx");

        runner.SimulateMouseMove(swingSpot);
        // A couple of physics steps so the flies' collision shapes are registered before the shapecast
        await runner.SimulateFrames(2);
        runner.SimulateMouseButtonPressed(MouseButton.Left);
        await runner.SimulateFrames(2);

        AssertThat(flyInRangeA.IsAlive).IsFalse();
        AssertThat(flyInRangeB.IsAlive).IsFalse();
        AssertThat(flyOutOfRange.IsAlive).IsTrue();
        AssertThat(swatter.CanSwat).IsFalse();
        AssertThat(smackFx.Visible).IsTrue();
    }

    // Helper building a minimal Swatter (testing independent from any scene): only the child
    // nodes Swatter.cs's own script logic actually touches
    private static Swatter BuildSwatter()
    {
        var swatter = new Swatter();

        swatter.AddChild(new ShapeCast2D
        {
            Name = "HitScan",
            Shape = new RectangleShape2D { Size = new Vector2(20, 20) },
            CollideWithAreas = true,
            CollideWithBodies = false
        });

        var smackFrames = new SpriteFrames();
        smackFrames.AddAnimation("smack");
        swatter.AddChild(new AnimatedSprite2D { Name = "SmackFx", SpriteFrames = smackFrames, Visible = true });

        swatter.AddChild(new Sprite2D { Name = "SwatterSprite" });

        return swatter;
    }

    // Helper wrapping BuildSwatter() in a parent Node2D, for tests that need a live SceneTree
    private static Node2D BuildSwatterInParent(out Swatter swatter)
    {
        var parent = new Node2D();
        swatter = BuildSwatter();
        parent.AddChild(swatter);
        return parent;
    }

    // Helper building a minimal Fly (testing independent from any scene), for the one test that
    // needs a real Fly collider - only the child nodes Fly.cs's own script logic actually touches
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
}
