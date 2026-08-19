using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;

// Sibling to FlyTesting: loads the real fly.tscn to catch script/scene drift the hand-built
// suite can't see by construction (e.g. a child node renamed/removed in the .tscn silently
// breaking a GetNode() call). Deliberately lean - logic coverage lives in FlyTesting
[TestSuite]
[RequireGodotRuntime]
public class FlySceneTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/fly_swatter/fly.tscn";

    private Fly _fly = null!;

    [BeforeTest]
    public void Setup()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        _fly = AutoFree(GD.Load<PackedScene>(ScenePath).Instantiate<Fly>())!;
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // Proves _Ready()'s GetNode() lookups still resolve against the real scene
    [TestCase]
    public void ReadySucceedsOnRealScene()
    {
        _fly._Ready();
        AutoFree(_fly.GetChildren().OfType<Timer>().First());

        AssertThat(_fly.IsAlive).IsTrue();
    }

    // Type checks only, not exact tuning values - those are designer-owned and expected to drift
    [TestCase]
    public void RealSceneHasExpectedChildTypes()
    {
        AnimatedSprite2D sprite = _fly.GetNode<AnimatedSprite2D>("FlySprite");
        CollisionShape2D collisionShape = _fly.GetNode<CollisionShape2D>("CollisionShape2D");

        AssertThat(sprite.SpriteFrames).IsNotNull();
        AssertThat(collisionShape.Shape).IsInstanceOf<CircleShape2D>();
    }
}
