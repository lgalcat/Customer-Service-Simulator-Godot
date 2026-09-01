using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Threading.Tasks;

// TestSuite for Platformer-specific behaviour
// (see DistractionTesting/PlatformerDistractionTesting for the contract-level checks)
[TestSuite]
[RequireGodotRuntime]
public class PlatformerBehaviourTesting : DistractionTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/platformer/platformer.tscn";
    private const string PlayerPath = "Window/SubViewport/Stage/Player";
    private const string GoalPath = "Window/SubViewport/Stage/Goal";
    // How far above the goal the "find the platform below it" probe starts - more than a max
    // jump height, but small enough to stay inside the level's encased boundary
    private const float PlatformSearchHeight = 150f;

    protected override Distraction CreateDistraction()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        return GD.Load<PackedScene>(ScenePath).Instantiate<Platformer>();
    }

    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
    }

    // GoalIsWithinMaxJumpHeightOfPlatformBelowIt presses JumpKey and never releases it -
    // SimulateActionPress affects the real global Input singleton, so force-release here to avoid
    // leaking into whichever test runs next (see PlayerTesting's Teardown)
    [AfterTest]
    public override void Teardown()
    {
        base.Teardown();
        Input.ActionRelease("JumpKey");
    }

    // Tests the Goal.Reached -> Victory sequence bound in Setup, by triggering the goal's
    // reached event directly
    [TestCase]
    public void SetupWiresGoalReachedToVictory()
    {
        distraction.Setup(1);
        Goal goal = distraction.GetNode<Goal>(GoalPath);
        bool victoryCalled = false;
        distraction.OnVictory = () => { victoryCalled = true; };

        goal.Reached?.Invoke();

        AssertThat(victoryCalled).IsTrue();
    }

    // Tests that Victory()'s explicit "_goal.Reached -= Victory" unsubscribe actually takes
    // effect: a further Reached invocation after winning must not re-trigger Victory
    [TestCase]
    public void VictoryUnsubscribesFromGoalReached()
    {
        distraction.Setup(1);
        Goal goal = distraction.GetNode<Goal>(GoalPath);
        int victoryCount = 0;
        distraction.OnVictory = () => { victoryCount++; };

        goal.Reached?.Invoke();
        AssertThat(victoryCount).IsEqual(1);

        goal.Reached?.Invoke();

        AssertThat(victoryCount).IsEqual(1);
    }


    // Level-content invariants below - scoped to the single current level, kept here rather than
    // a dedicated suite until multiple level layouts exist. Black-box (real physics simulation)
    // rather than decoding tile data by hand.

    // Soft-lock guard: the player must actually be standing on solid ground at spawn, not falling
    // or embedded in geometry
    [TestCase]
    public async Task PlayerSpawnsAboveSolidGround()
    {
        using ISceneRunner runner = ISceneRunner.Load(ScenePath, true, true);
        var platformer = (Platformer)runner.Scene()!;
        Player player = platformer.GetNode<Player>(PlayerPath);

        await runner.SimulateFrames(30);

        AssertThat(player.IsOnFloor()).IsTrue();
    }

    // Reachability guard: the goal must be within a single held jump of the platform sitting
    // beneath it. Only valid for a goal placed directly above its own supporting platform (today's
    // design) - a goal placed across a horizontal gap would need a different check entirely, since
    // this only verifies vertical reach
    [TestCase]
    public async Task GoalIsWithinMaxJumpHeightOfPlatformBelowIt()
    {
        using ISceneRunner runner = ISceneRunner.Load(ScenePath, true, true);
        var platformer = (Platformer)runner.Scene()!;
        Player player = platformer.GetNode<Player>(PlayerPath);
        Goal goal = platformer.GetNode<Goal>(GoalPath);

        // Drop the level's own player from just above the goal to find the platform actually
        // sitting beneath it, rather than hand-decoding tile data
        player.Position = new Vector2(goal.Position.X, goal.Position.Y - PlatformSearchHeight);
        player.Velocity = Vector2.Zero;
        for (int i = 0; i < 90 && !player.IsOnFloor(); i++) { await runner.SimulateFrames(1); }
        // Sanity check: a platform was actually found within the search range
        AssertThat(player.IsOnFloor()).IsTrue();

        // From that landing spot, a full held jump must reach the goal's height
        float apexY = player.Position.Y;
        runner.SimulateActionPress("JumpKey");
        for (int i = 0; i < 120 && player.Velocity.Y <= 0; i++)
        {
            await runner.SimulateFrames(1);
            apexY = Mathf.Min(apexY, player.Position.Y);
        }

        // Lower Position.Y means higher in the air (Y grows downward)
        AssertThat(apexY).IsLessEqual(goal.Position.Y + 1f);
    }

    // Sanity guard: neither the player's spawn nor the goal should sit outside the camera's own
    // configured pan range - otherwise the intended MainCamera would never actually reveal them
    // through the SubViewport. Pure static-data check, no simulation needed
    [TestCase]
    public void CameraBoundsContainPlayerSpawnAndGoal()
    {
        using ISceneRunner runner = ISceneRunner.Load(ScenePath, true, true);
        var platformer = (Platformer)runner.Scene()!;
        Player player = platformer.GetNode<Player>(PlayerPath);
        Goal goal = platformer.GetNode<Goal>(GoalPath);
        Camera2D camera = player.GetNode<Camera2D>("MainCamera");

        AssertThat(player.Position.X).IsGreaterEqual((float)camera.LimitLeft);
        AssertThat(player.Position.X).IsLessEqual((float)camera.LimitRight);
        AssertThat(player.Position.Y).IsGreaterEqual((float)camera.LimitTop);
        AssertThat(player.Position.Y).IsLessEqual((float)camera.LimitBottom);

        AssertThat(goal.Position.X).IsGreaterEqual((float)camera.LimitLeft);
        AssertThat(goal.Position.X).IsLessEqual((float)camera.LimitRight);
        AssertThat(goal.Position.Y).IsGreaterEqual((float)camera.LimitTop);
        AssertThat(goal.Position.Y).IsLessEqual((float)camera.LimitBottom);
    }
}