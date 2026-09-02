using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Collections.Generic;
using System.Threading.Tasks;

// Dedicated component suite for Card, independent of the Distraction hierarchy.
// Covers Card's own identity/rendering, pickup/drag/release state machine, and drop-target
// resolution. For real-scene drift coverage see CardSceneTesting
[TestSuite]
[RequireGodotRuntime]
public class CardTesting
{
    // No shared fixture: tests split between tree-less (manual _Ready()) and live-tree (ISceneRunner)
    // shapes that don't share a common base instance, unlike SwatterTesting/FlyTesting's single _fly/_swatter field
    [BeforeTest]
    public void Setup()
    {
    }

    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(CardTesting));

    [AfterTest]
    public void Teardown()
    {
    }

    // Helper building a minimal Card (testing independent from any scene): only the child node
    // Configure() actually touches. Hframes/Vframes must match the real atlas grid (14x4) -
    // FrameCoords is silently clamped to (0,0) against a default 1x1 grid otherwise
    private static Card BuildCard(Suit suit, int rank, bool faceUp, Node2D dragLayer)
    {
        var card = new Card();
        card.AddChild(new Sprite2D { Name = "CardSprite", Hframes = 14, Vframes = 4 });
        card.Configure(suit, rank, faceUp, dragLayer);
        return card;
    }

    // Fires Card's private OnInputEvent handler directly (wired to InputEvent in _Ready()) without
    // needing real physics-based picking - mirrors the established "trigger wiring without real
    // physics" technique already used for Area2D.BodyEntered/Timer.Timeout elsewhere in this project
    private static void EmitClick(Card card, bool pressed)
    {
        card.EmitSignal(Area2D.SignalName.InputEvent, (Node)null!, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = pressed }, 0L);
    }

    // Helper wrapping a DragLayer + single TableauPile under a wrapper root, for tests needing a
    // live SceneTree (successful pickup/drag/release all touch GetGlobalMousePosition()/GetTree())
    private static Node2D BuildFixtureInParent(out Node2D dragLayer, out TableauPile pile)
    {
        var root = new Node2D();
        dragLayer = new Node2D { Name = "DragLayer" };
        root.AddChild(dragLayer);
        pile = new TableauPile();
        root.AddChild(pile);
        return root;
    }

    [TestCase]
    public void ConfigureSetsIdentityAndFaceUpSpriteFrame()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 5, true, dragLayer))!;

        AssertThat(card.CardSuit).IsEqual(Suit.Hearts);
        AssertThat(card.Rank).IsEqual(5);
        AssertThat(card.IsFaceUp).IsTrue();
        AssertThat(card.GetNode<Sprite2D>("CardSprite").FrameCoords).IsEqual(new Vector2I(4, 0));
    }

    // The back frame is fixed regardless of the card's actual suit/rank - a face-down card must
    // never leak its identity through the rendered sprite
    [TestCase]
    public void ConfigureFaceDownShowsBackFrameRegardlessOfIdentity()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Spades, 10, false, dragLayer))!;

        AssertThat(card.GetNode<Sprite2D>("CardSprite").FrameCoords).IsEqual(new Vector2I(13, 1));
    }

    [TestCase]
    public void SetFaceUpTogglesSpriteFrame()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Clubs, 1, false, dragLayer))!;
        Sprite2D sprite = card.GetNode<Sprite2D>("CardSprite");
        AssertThat(sprite.FrameCoords).IsEqual(new Vector2I(13, 1));

        card.SetFaceUp(true);
        AssertThat(card.IsFaceUp).IsTrue();
        AssertThat(sprite.FrameCoords).IsEqual(new Vector2I(0, 2));

        card.SetFaceUp(false);
        AssertThat(card.IsFaceUp).IsFalse();
        AssertThat(sprite.FrameCoords).IsEqual(new Vector2I(13, 1));
    }

    // Rule: card color (red/black) drives the tableau's alternating-color stacking requirement
    [TestCase]
    public void IsRedClassifiesSuitsCorrectly()
    {
        AssertThat(Card.IsRed(Suit.Hearts)).IsTrue();
        AssertThat(Card.IsRed(Suit.Diamonds)).IsTrue();
        AssertThat(Card.IsRed(Suit.Clubs)).IsFalse();
        AssertThat(Card.IsRed(Suit.Spades)).IsFalse();
    }

    // Rejection guards short-circuit before the one GetGlobalMousePosition() call, so this can run
    // without a live SceneTree - manual _Ready() call needed to wire OnInputEvent (no tree to auto-call it)
    [TestCase]
    public void PickUpIsRejectedWhenNotAssignedToAPile()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;
        card._Ready();

        EmitClick(card, true);

        AssertThat(card.GetParent()).IsNull();
    }

    // Rule: a covered (face-down) card can never be picked up
    [TestCase]
    public void PickUpIsRejectedWhenCardIsFaceDown()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        var pile = AutoFree(new TableauPile())!;
        Card card = BuildCard(Suit.Hearts, 7, false, dragLayer);
        scratch.AddChild(card);
        pile.AddCards(new List<Card> { card });
        card._Ready();

        EmitClick(card, true);

        AssertThat(card.GetParent()).IsEqual(pile);
    }

    // Rule: a card can't be picked up if what's stacked on it isn't itself a legal run - built via
    // direct AddCards (bypasses CanAccept) to manufacture an otherwise-illegal same-color stack
    [TestCase]
    public void PickUpIsRejectedWhenPileReturnsEmptyMovableRun()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        var pile = AutoFree(new TableauPile())!;
        Card bottomCard = BuildCard(Suit.Clubs, 6, true, dragLayer);
        Card topCard = BuildCard(Suit.Spades, 5, true, dragLayer);
        scratch.AddChild(bottomCard);
        scratch.AddChild(topCard);
        pile.AddCards(new List<Card> { bottomCard, topCard });
        bottomCard._Ready();

        EmitClick(bottomCard, true);

        AssertThat(bottomCard.GetParent()).IsEqual(pile);
    }

    [TestCase]
    public void SuccessfulPickupReparentsCardUnderDragLayer()
    {
        Node2D root = BuildFixtureInParent(out Node2D dragLayer, out TableauPile pile);
        Card card = BuildCard(Suit.Hearts, 13, true, dragLayer);
        dragLayer.AddChild(card);
        pile.AddCards(new List<Card> { card });
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);

        EmitClick(card, true);

        AssertThat(card.GetParent()).IsEqual(dragLayer);
    }

    // Rule: dragging a card with a valid run stacked on it moves the whole run together, preserving
    // each member's relative offset - proven via an actual mouse move + frame tick, not just the
    // offset captured at the pickup instant
    [TestCase]
    public async Task PickupOfARunCarriesFollowersAtCapturedOffsets()
    {
        Node2D root = BuildFixtureInParent(out Node2D dragLayer, out TableauPile pile);
        Card lead = BuildCard(Suit.Clubs, 6, true, dragLayer);
        Card follower = BuildCard(Suit.Hearts, 5, true, dragLayer);
        dragLayer.AddChild(lead);
        dragLayer.AddChild(follower);
        pile.AddCards(new List<Card> { lead, follower });
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        Vector2 offsetBeforePickup = follower.GlobalPosition - lead.GlobalPosition;
        runner.SimulateMouseMove(lead.GlobalPosition);

        EmitClick(lead, true);
        Vector2 target = lead.GlobalPosition + new Vector2(40, -20);
        runner.SimulateMouseMove(target);
        await runner.SimulateFrames(1);

        AssertThat(lead.GetParent()).IsEqual(dragLayer);
        AssertThat(follower.GetParent()).IsEqual(dragLayer);
        AssertThat((follower.GlobalPosition - lead.GlobalPosition).DistanceTo(offsetBeforePickup)).IsLess(0.01f);
    }

    // Card._Process assigns position directly (no smoothing, unlike Swatter's follow), so a single
    // simulated frame is enough to observe convergence
    [TestCase]
    public async Task DraggingFollowsTheMouse()
    {
        Node2D root = BuildFixtureInParent(out Node2D dragLayer, out TableauPile pile);
        Card card = BuildCard(Suit.Diamonds, 8, true, dragLayer);
        dragLayer.AddChild(card);
        pile.AddCards(new List<Card> { card });
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        runner.SimulateMouseMove(card.GlobalPosition);
        EmitClick(card, true);
        Vector2 target = card.GlobalPosition + new Vector2(60, 25);

        runner.SimulateMouseMove(target);
        await runner.SimulateFrames(1);

        AssertThat(card.GlobalPosition.DistanceTo(target)).IsLess(0.01f);
    }

    [TestCase]
    public async Task ReleaseSnapsBackWhenNoValidDropTarget()
    {
        Node2D root = BuildFixtureInParent(out Node2D dragLayer, out TableauPile pile);
        Card card = BuildCard(Suit.Diamonds, 7, true, dragLayer);
        dragLayer.AddChild(card);
        pile.AddCards(new List<Card> { card });
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        runner.SimulateMouseMove(card.GlobalPosition);
        EmitClick(card, true);
        // Far outside every pile's drop zone
        runner.SimulateMouseMove(card.GlobalPosition + new Vector2(5000, 5000));
        await runner.SimulateFrames(1);

        EmitClick(card, false);

        AssertThat(card.CurrentPile).IsEqual(pile);
        AssertThat(card.GetParent()).IsEqual(pile);
    }

    // Rule: a King may only be dropped onto an empty tableau column
    [TestCase]
    public async Task ReleaseMovesRunToValidDropTargetPile()
    {
        var root = new Node2D();
        var dragLayer = new Node2D { Name = "DragLayer" };
        root.AddChild(dragLayer);
        var source = new TableauPile();
        root.AddChild(source);
        var target = new TableauPile { Position = new Vector2(300, 0) };
        root.AddChild(target);
        Card king = BuildCard(Suit.Spades, 13, true, dragLayer);
        dragLayer.AddChild(king);
        source.AddCards(new List<Card> { king });
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        runner.SimulateMouseMove(king.GlobalPosition);
        EmitClick(king, true);
        runner.SimulateMouseMove(target.GlobalPosition);
        await runner.SimulateFrames(1);

        EmitClick(king, false);

        AssertThat(king.CurrentPile).IsEqual(target);
        AssertThat(source.Cards.Count).IsEqual(0);
        AssertThat(target.Cards.Count).IsEqual(1);
    }

    // Rule: a King may NOT be dropped onto an already-occupied tableau column
    [TestCase]
    public async Task ReleaseRejectsInvalidTargetAndSnapsBack()
    {
        var root = new Node2D();
        var dragLayer = new Node2D { Name = "DragLayer" };
        root.AddChild(dragLayer);
        var source = new TableauPile();
        root.AddChild(source);
        var target = new TableauPile { Position = new Vector2(300, 0) };
        root.AddChild(target);
        Card targetResident = BuildCard(Suit.Hearts, 2, true, dragLayer);
        dragLayer.AddChild(targetResident);
        target.AddCards(new List<Card> { targetResident });
        Card king = BuildCard(Suit.Spades, 13, true, dragLayer);
        dragLayer.AddChild(king);
        source.AddCards(new List<Card> { king });
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        runner.SimulateMouseMove(king.GlobalPosition);
        EmitClick(king, true);
        runner.SimulateMouseMove(target.GlobalPosition);
        await runner.SimulateFrames(1);

        EmitClick(king, false);

        AssertThat(king.CurrentPile).IsEqual(source);
        AssertThat(target.Cards.Count).IsEqual(1);
    }

    // Documented tie-break rule (see project memory): when a drop point lands inside multiple
    // piles' drop zones at once, the pile whose own position is nearest the drop point wins
    [TestCase]
    public async Task ReleaseWithMultipleOverlappingCandidatesPicksNearest()
    {
        var root = new Node2D();
        var dragLayer = new Node2D { Name = "DragLayer" };
        root.AddChild(dragLayer);
        var source = new TableauPile { Position = new Vector2(-300, 0) };
        root.AddChild(source);
        var pileA = new TableauPile { Position = new Vector2(0, 0) };
        root.AddChild(pileA);
        var pileB = new TableauPile { Position = new Vector2(20, 0) };
        root.AddChild(pileB);
        Card king = BuildCard(Suit.Diamonds, 13, true, dragLayer);
        dragLayer.AddChild(king);
        source.AddCards(new List<Card> { king });
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        runner.SimulateMouseMove(king.GlobalPosition);
        EmitClick(king, true);
        // Inside both drop zones (pileA: [-25,25], pileB: [-5,45]), nearer to pileB
        runner.SimulateMouseMove(new Vector2(15, 0));
        await runner.SimulateFrames(1);

        EmitClick(king, false);

        AssertThat(king.CurrentPile).IsEqual(pileB);
        AssertThat(pileA.Cards.Count).IsEqual(0);
    }
}
