using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// Sibling to CardTesting: loads the real card.tscn to catch script/scene drift the hand-built
// suite can't see by construction (e.g. a child node renamed/removed in the .tscn silently
// breaking a GetNode() call). Deliberately lean - logic coverage lives in CardTesting
[TestSuite]
[RequireGodotRuntime]
public class CardSceneTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/solitaire/card.tscn";

    private Card _card = null!;

    [BeforeTest]
    public void Setup()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        _card = AutoFree(GD.Load<PackedScene>(ScenePath).Instantiate<Card>())!;
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // Proves _Ready()'s InputEvent wiring still resolves against the real scene, and that a card
    // with no assigned pile safely no-ops on a press rather than throwing
    [TestCase]
    public void ReadySucceedsOnRealScene()
    {
        _card._Ready();

        _card.EmitSignal(Area2D.SignalName.InputEvent, (Node)null!, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }, 0L);

        AssertThat(_card.CurrentPile).IsNull();
    }

    // Type checks only, not exact tuning values - those are designer-owned and expected to drift
    [TestCase]
    public void RealSceneHasExpectedChildTypes()
    {
        CollisionShape2D collisionShape = _card.GetNode<CollisionShape2D>("CollisionShape2D");
        Sprite2D sprite = _card.GetNode<Sprite2D>("CardSprite");

        AssertThat(collisionShape.Shape).IsInstanceOf<RectangleShape2D>();
        AssertThat(sprite.Texture).IsNotNull();
    }
}
