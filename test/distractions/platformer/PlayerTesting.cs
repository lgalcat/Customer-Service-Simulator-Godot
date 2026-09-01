using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Threading.Tasks;

// TestSuite for testing of the Player class
// Every test needs a live SceneTree: movement is entirely physics-driven (MoveAndSlide/IsOnFloor),
// mirrors BallTesting's "_IntegrateForces needs a real physics step" gotcha
[TestSuite]
[RequireGodotRuntime]
public class PlayerTesting
{
    private const float FloorHalfHeight = 20f;
    private const float FloorTopY = -FloorHalfHeight; // floor StaticBody2D sits at wrapper-local (0,0)
    private const float PlayerBottomOffset = 16f; // PlayerCollision's local offset(4) + half-height(12), matches the real 10x24 shape at position (0,4)
    private const float GroundedStartY = FloorTopY - PlayerBottomOffset - 4f; // small air gap so a short settle simulates real floor contact instead of assuming it
    private const float FallLandingStartY = -150f; // high enough to actually fall and land within a modest frame budget
    private const float HighAltitudeStartY = -3000f; // never reaches the floor within this suite's frame budgets - isolates the fall-speed clamp from landing collision

    // No shared fixture: every test builds its own floor+Player wrapper and ISceneRunner, since
    // physics state (position/velocity) must not leak between tests - mirrors CardTesting's
    // no-shared-fixture shape
    [BeforeTest]
    public void Setup()
    {
    }

    // SimulateActionPress/Release act on the real global Input singleton, not anything scoped to
    // a test's own ISceneRunner - several tests below press keys and don't release them before the
    // test ends (e.g. MeasureJumpApex's held-jump case, by design). Force-releasing here keeps
    // this suite self-contained regardless of what a given test leaves behind.
    [AfterTest]
    public void Teardown()
    {
        Input.ActionRelease("MoveLeftKey");
        Input.ActionRelease("MoveRightKey");
        Input.ActionRelease("JumpKey");
    }

    // Helper building a minimal grounded-capable Player fixture (testing independent from any
    // scene): a flat static floor plus a Player with only the child nodes Player.cs's own script
    // logic actually touches. Synthetic zero-frame SpriteFrames (mirrors
    // FlySpawnerTesting/FlyTesting's BuildFly()) avoids depending on the real
    // jumpman_frames_wip.tres asset
    private static Node2D BuildFixture(out Player player, out AnimatedSprite2D sprite, float startY)
    {
        var wrapper = new Node2D();

        var floor = new StaticBody2D();
        floor.AddChild(new CollisionShape2D { Shape = new RectangleShape2D { Size = new Vector2(2000f, FloorHalfHeight * 2f) } });
        wrapper.AddChild(floor);

        player = new Player { Position = new Vector2(0, startY) };
        player.AddChild(new CollisionShape2D { Position = new Vector2(0, 4), Shape = new RectangleShape2D { Size = new Vector2(10, 24) } });
        // AnimatedSprite2D.Play() silently no-ops on an animation with zero frames (unlike
        // FlyTesting/FlySpawnerTesting's fixtures, which only ever assign .Animation directly) -
        // each animation needs at least one real (if minimal) frame
        var frames = new SpriteFrames();
        var placeholderFrame = new PlaceholderTexture2D();
        foreach (string animation in new[] { "idle", "walking", "jumping", "falling" })
        {
            frames.AddAnimation(animation);
            frames.AddFrame(animation, placeholderFrame);
        }
        sprite = new AnimatedSprite2D { Name = "AnimatedSprite2D", SpriteFrames = frames };
        player.AddChild(sprite);
        wrapper.AddChild(player);

        return wrapper;
    }

    // Helper counting physics ticks until Velocity.X first reaches a target (descending: <=
    // target; ascending: >= target), up to a bounded budget. Verifies a rate via elapsed
    // ticks-to-threshold rather than an exact-tick velocity snapshot - coarser, but reliable under
    // full-suite load; a rate differing by ~2x still gives clearly separated tick counts
    private static async Task<int> CountTicksUntilVelocityXCrosses(ISceneRunner runner, Player player, float target, bool descending, int maxTicks)
    {
        for (int ticks = 0; ticks < maxTicks; ticks++)
        {
            if (descending ? player.Velocity.X <= target : player.Velocity.X >= target) { return ticks; }
            await runner.SimulateFrames(1);
        }
        return maxTicks;
    }

    // Helper measuring a jump's peak height (minimum Position.Y, since Y grows downward),
    // optionally releasing JumpKey right after launch to simulate a "tap" instead of a hold
    private static async Task<float> MeasureJumpApex(bool holdJumpKey)
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        float apexY = player.Position.Y;

        runner.SimulateActionPress("JumpKey");
        await runner.SimulateFrames(1);
        if (!holdJumpKey) { runner.SimulateActionRelease("JumpKey"); }

        for (int i = 0; i < 120 && player.Velocity.Y < 0; i++)
        {
            await runner.SimulateFrames(1);
            apexY = Mathf.Min(apexY, player.Position.Y);
        }

        return apexY;
    }


    // AddToGroup("player") is Goal's entire detection mechanism (see Goal.OnBodyEntered) - not
    // testable from Goal's own side without this
    [TestCase]
    public void ReadyAddsPlayerToPlayerGroup()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);

        AssertThat(player.IsInGroup("player")).IsTrue();
    }

    [TestCase]
    public async Task IdleOnFloorWithNoInputStaysIdle()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);

        await runner.SimulateFrames(30);

        AssertThat(player.IsOnFloor()).IsTrue();
        AssertThat(Mathf.Abs(player.Velocity.X)).IsLess(0.5f);
        AssertThat(sprite.Animation.ToString()).IsEqual("idle");
    }

    [TestCase]
    public async Task HorizontalInputAcceleratesTowardMoveSpeedAndPlaysWalking()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        float moveSpeed = player.Get("_moveSpeed").AsSingle();

        runner.SimulateActionPress("MoveRightKey");
        await runner.SimulateFrames(90);

        AssertThat(Mathf.Abs(player.Velocity.X - moveSpeed)).IsLess(0.5f);
        AssertThat(sprite.Animation.ToString()).IsEqual("walking");
        AssertThat(sprite.FlipH).IsFalse();
    }

    [TestCase]
    public async Task MovingLeftFlipsSpriteAndReachesNegativeMoveSpeed()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        float moveSpeed = player.Get("_moveSpeed").AsSingle();

        runner.SimulateActionPress("MoveLeftKey");
        await runner.SimulateFrames(90);

        AssertThat(Mathf.Abs(player.Velocity.X - (-moveSpeed))).IsLess(0.5f);
        AssertThat(sprite.Animation.ToString()).IsEqual("walking");
        AssertThat(sprite.FlipH).IsTrue();
    }

    [TestCase]
    public async Task ReleasingHorizontalInputDeceleratesBackToIdle()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        runner.SimulateActionPress("MoveRightKey");
        await runner.SimulateFrames(90);

        runner.SimulateActionRelease("MoveRightKey");
        await runner.SimulateFrames(90);

        AssertThat(Mathf.Abs(player.Velocity.X)).IsLess(0.5f);
        AssertThat(sprite.Animation.ToString()).IsEqual("idle");
    }

    // Rule: on the ground with fresh input, velocity eases toward the target at exactly the
    // _groundAcceleration rate - verified via CountTicksUntilVelocityXCrosses, not just eventual
    // convergence to full speed
    [TestCase]
    public async Task GroundAccelerationMatchesTheExportedRate()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        float moveSpeed = player.Get("_moveSpeed").AsSingle();
        float groundAcceleration = player.Get("_groundAcceleration").AsSingle();
        float physicsDelta = 1f / Engine.PhysicsTicksPerSecond;
        float target = moveSpeed * 0.5f;
        float expectedTicks = target / (groundAcceleration * physicsDelta);

        runner.SimulateActionPress("MoveRightKey");
        int actualTicks = await CountTicksUntilVelocityXCrosses(runner, player, target, descending: false, maxTicks: 200);

        // Generous absolute tick tolerance - proving the right rate was used, not pinning an
        // exact tick
        AssertThat((float)actualTicks).IsGreater(expectedTicks - 6f);
        AssertThat((float)actualTicks).IsLess(expectedTicks + 6f);
    }

    // Rule: on the ground with no input, horizontal velocity eases toward zero at exactly the
    // _groundDeceleration rate - verified via elapsed ticks to reach a fixed intermediate
    // threshold (see GroundAccelerationMatchesTheExportedRate)
    [TestCase]
    public async Task GroundDecelerationMatchesTheExportedRate()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        float moveSpeed = player.Get("_moveSpeed").AsSingle();
        float groundDeceleration = player.Get("_groundDeceleration").AsSingle();
        float physicsDelta = 1f / Engine.PhysicsTicksPerSecond;
        runner.SimulateActionPress("MoveRightKey");
        await runner.SimulateFrames(90);
        AssertThat(Mathf.Abs(player.Velocity.X - moveSpeed)).IsLess(0.5f);
        float target = moveSpeed * 0.5f;
        float expectedTicks = (moveSpeed - target) / (groundDeceleration * physicsDelta);

        runner.SimulateActionRelease("MoveRightKey");
        int actualTicks = await CountTicksUntilVelocityXCrosses(runner, player, target, descending: true, maxTicks: 200);

        AssertThat((float)actualTicks).IsGreater(expectedTicks - 6f);
        AssertThat((float)actualTicks).IsLess(expectedTicks + 6f);
    }

    // No separate left/right-facing frames exist - facing must hold the last moved direction
    // rather than resetting once idle (see Player.UpdateFacing)
    [TestCase]
    public async Task FacingHoldsLastDirectionWhileIdle()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        runner.SimulateActionPress("MoveLeftKey");
        await runner.SimulateFrames(10);

        runner.SimulateActionRelease("MoveLeftKey");
        await runner.SimulateFrames(90);

        AssertThat(sprite.Animation.ToString()).IsEqual("idle");
        AssertThat(sprite.FlipH).IsTrue();
    }

    // Rule: above _skidTurnaroundSpeed, input opposing current motion uses the dedicated
    // (faster) _skidDeceleration rate toward the new target, not the plain _groundAcceleration
    // rate - verified against the real exported tunables rather than hardcoded magnitudes
    [TestCase]
    public async Task SkiddingAboveTurnaroundSpeedUsesTheSkidDecelerationRate()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        float moveSpeed = player.Get("_moveSpeed").AsSingle();
        float skidDeceleration = player.Get("_skidDeceleration").AsSingle();
        float groundAcceleration = player.Get("_groundAcceleration").AsSingle();
        runner.SimulateActionPress("MoveRightKey");
        await runner.SimulateFrames(90);
        AssertThat(Mathf.Abs(player.Velocity.X - moveSpeed)).IsLess(0.5f);

        // Target stays above _skidTurnaroundSpeed (34) so the branch can't swap to plain
        // ground-acceleration mid-measurement
        float physicsDelta = 1f / Engine.PhysicsTicksPerSecond;
        float target = 45f;
        float expectedSkidTicks = (moveSpeed - target) / (skidDeceleration * physicsDelta);
        float expectedGroundTicks = (moveSpeed - target) / (groundAcceleration * physicsDelta);

        runner.SimulateActionRelease("MoveRightKey");
        runner.SimulateActionPress("MoveLeftKey");
        int actualTicks = await CountTicksUntilVelocityXCrosses(runner, player, target, descending: true, maxTicks: 200);

        // Generous absolute tick tolerance - proving the right rate was used, not pinning an
        // exact tick
        AssertThat((float)actualTicks).IsGreater(expectedSkidTicks - 4f);
        AssertThat((float)actualTicks).IsLess(expectedSkidTicks + 4f);
        // Distinguishes the skid branch from the (slower) plain-acceleration branch - proves the
        // faster rate was actually used, not just that velocity moved toward the target at all
        AssertThat((float)actualTicks).IsLess(expectedGroundTicks);
    }

    // Rule: below _skidTurnaroundSpeed, input opposing (slow) current motion just re-accelerates
    // via the plain _groundAcceleration rate instead of skidding first
    [TestCase]
    public async Task BelowSkidTurnaroundSpeedOpposingInputUsesPlainGroundAcceleration()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        AssertThat(player.IsOnFloor()).IsTrue();
        float groundAcceleration = player.Get("_groundAcceleration").AsSingle();
        float skidTurnaroundSpeed = player.Get("_skidTurnaroundSpeed").AsSingle();
        float belowThresholdSpeed = skidTurnaroundSpeed * 0.5f;
        player.Velocity = new Vector2(belowThresholdSpeed, player.Velocity.Y);
        float physicsDelta = 1f / Engine.PhysicsTicksPerSecond;
        float target = -40f;
        float expectedTicks = (belowThresholdSpeed - target) / (groundAcceleration * physicsDelta);

        runner.SimulateActionPress("MoveLeftKey");
        int actualTicks = await CountTicksUntilVelocityXCrosses(runner, player, target, descending: true, maxTicks: 200);

        AssertThat((float)actualTicks).IsGreater(expectedTicks - 6f);
        AssertThat((float)actualTicks).IsLess(expectedTicks + 6f);
    }

    // Rule: airborne horizontal input still eases toward the target speed, but at the dedicated
    // (slower) _airAcceleration rate rather than any ground-only rate
    [TestCase]
    public async Task AirborneHorizontalInputUsesAirAcceleration()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        float airAcceleration = player.Get("_airAcceleration").AsSingle();
        float groundAcceleration = player.Get("_groundAcceleration").AsSingle();

        float physicsDelta = 1f / Engine.PhysicsTicksPerSecond;
        float target = 50f;
        float expectedAirTicks = target / (airAcceleration * physicsDelta);
        float expectedGroundTicks = target / (groundAcceleration * physicsDelta);

        // Holds JumpKey (unlike other jump tests) purely to stay airborne long enough for the
        // measurement window to finish before landing
        runner.SimulateActionPress("JumpKey");
        for (int i = 0; i < 10 && player.IsOnFloor(); i++) { await runner.SimulateFrames(1); }
        AssertThat(player.IsOnFloor()).IsFalse();

        runner.SimulateActionPress("MoveRightKey");
        int actualTicks = await CountTicksUntilVelocityXCrosses(runner, player, target, descending: false, maxTicks: 200);

        // Generous absolute tick tolerance - proving the right rate was used, not pinning an
        // exact tick
        AssertThat((float)actualTicks).IsGreater(expectedAirTicks - 4f);
        AssertThat((float)actualTicks).IsLess(expectedAirTicks + 4f);
        // Distinguishes the air-acceleration branch from the (faster) ground-acceleration one -
        // proves the airborne rate was actually used, not just that velocity moved toward the
        // target at all
        AssertThat((float)actualTicks).IsGreater(expectedGroundTicks);
    }

    // Core momentum-conservation rule (classic NES SMB air control): releasing all input mid-air must not
    // change horizontal speed. Regression test for a bug where ApplyHorizontalMovement still
    // eased Velocity.X toward 0 in the air with no input
    [TestCase]
    public async Task AirborneMomentumHoldsWithNoHorizontalInput()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        runner.SimulateActionPress("MoveRightKey");
        await runner.SimulateFrames(90);
        runner.SimulateActionRelease("MoveRightKey");

        runner.SimulateActionPress("JumpKey");
        for (int i = 0; i < 10 && player.IsOnFloor(); i++) { await runner.SimulateFrames(1); }
        AssertThat(player.IsOnFloor()).IsFalse();
        runner.SimulateActionRelease("JumpKey");
        float speedAtTakeoff = player.Velocity.X;

        await runner.SimulateFrames(10);

        AssertThat(Mathf.Abs(player.Velocity.X - speedAtTakeoff)).IsLess(0.01f);
    }

    [TestCase]
    public async Task JumpFromFloorLaunchesUpwardAndEntersJumpingState()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        AssertThat(player.IsOnFloor()).IsTrue();
        float jumpVelocity = player.Get("_jumpVelocity").AsSingle();

        runner.SimulateActionPress("JumpKey");
        await runner.SimulateFrames(1);

        // Wide tolerance: the press can register a tick late, adding a tick of reduced gravity
        AssertThat(Mathf.Abs(player.Velocity.Y - (-jumpVelocity))).IsLess(10f);
        AssertThat(player.IsOnFloor()).IsFalse();
        AssertThat(sprite.Animation.ToString()).IsEqual("jumping");
    }

    // Core variable-jump-height game rule (loosely modelled after NES Super Mario Bros): holding
    // JumpKey through the whole ascent must reach a higher peak than tapping it - checked
    // black-box via observed Position.Y, not via any internal state
    [TestCase]
    public async Task HoldingJumpKeyProducesAHigherApexThanTappingIt()
    {
        float heldApexY = await MeasureJumpApex(holdJumpKey: true);
        float tappedApexY = await MeasureJumpApex(holdJumpKey: false);

        // Lower Position.Y means higher in the air (Y grows downward)
        AssertThat(heldApexY).IsLess(tappedApexY);
    }

    [TestCase]
    public async Task ApexTransitionsFromJumpingToFalling()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, GroundedStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        await runner.SimulateFrames(30);
        runner.SimulateActionPress("JumpKey");
        await runner.SimulateFrames(1);
        runner.SimulateActionRelease("JumpKey");
        AssertThat(sprite.Animation.ToString()).IsEqual("jumping");

        for (int i = 0; i < 120 && player.Velocity.Y < 0; i++) { await runner.SimulateFrames(1); }

        AssertThat(player.Velocity.Y).IsGreaterEqual(0f);
        AssertThat(sprite.Animation.ToString()).IsEqual("falling");
    }

    [TestCase]
    public async Task FallSpeedIsClampedToMaxFallSpeed()
    {
        Node2D wrapper = BuildFixture(out Player player, out _, HighAltitudeStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);
        float maxFallSpeed = player.Get("_maxFallSpeed").AsSingle();

        for (int i = 0; i < 120; i++)
        {
            await runner.SimulateFrames(1);
            AssertThat(player.Velocity.Y).IsLessEqual(maxFallSpeed + 0.01f);
        }

        AssertThat(Mathf.Abs(player.Velocity.Y - maxFallSpeed)).IsLess(0.5f);
    }

    [TestCase]
    public async Task LandingAfterFallReturnsToGroundState()
    {
        Node2D wrapper = BuildFixture(out Player player, out AnimatedSprite2D sprite, FallLandingStartY);
        using ISceneRunner runner = ISceneRunner.Load(wrapper, true, true);

        await runner.SimulateFrames(90);

        AssertThat(player.IsOnFloor()).IsTrue();
        AssertThat(sprite.Animation.ToString()).IsEqual("idle");
    }
}
