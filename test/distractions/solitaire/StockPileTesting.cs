using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Collections.Generic;

// TestSuite for StockPile-specific behaviour
// (see PileTesting for the generic Pile contract checks)
[TestSuite]
[RequireGodotRuntime]
public class StockPileTesting : PileTesting
{
    protected override Pile CreatePile()
    {
        // ClickZone is required for _Ready() to not throw (including the inherited
        // ReadyAddsPileToPilesGroup redeclaration below) - harmless for tests that never call it
        var stock = new StockPile();
        stock.AddChild(new Area2D { Name = "ClickZone" });
        return stock;
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

    // Fires the private OnClickZoneInputEvent handler directly (wired to ClickZone.InputEvent in
    // _Ready()) without needing real physics-based picking - mirrors CardTesting's established technique
    private static void EmitClick(Area2D clickZone)
    {
        clickZone.EmitSignal(Area2D.SignalName.InputEvent, (Node)null!, new InputEventMouseButton { ButtonIndex = MouseButton.Left, Pressed = true }, 0L);
    }


    // Block containing base calls to inherited TestCases, no class specific logic should be present further down
    [TestCase]
    public override void AddCardsAppendsReparentsAndSetsCurrentPile() { base.AddCardsAppendsReparentsAndSetsCurrentPile(); }

    [TestCase]
    public override void RemoveCardsRemovesClearsCurrentPileWithoutReparenting() { base.RemoveCardsRemovesClearsCurrentPileWithoutReparenting(); }

    [TestCase]
    public override void TopCardReflectsMostRecentlyAddedCardAndNullWhenEmpty() { base.TopCardReflectsMostRecentlyAddedCardAndNullWhenEmpty(); }

    [TestCase]
    public override void ReadyAddsPileToPilesGroup() { base.ReadyAddsPileToPilesGroup(); }


    // Rule: cards only ever leave the stock via a draw, never a manual drop
    [TestCase]
    public void CanAcceptAlwaysRejects()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 1, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { card })).IsFalse();
    }

    // Resident cards keep InputPickable off - nothing in the stock is independently drag-pickable,
    // not even the actual top card
    [TestCase]
    public void GetMovableRunAlwaysReturnsEmpty()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 1, false, dragLayer))!;
        scratch.AddChild(card);
        pile.AddCards(new List<Card> { card });

        AssertThat(pile.GetMovableRun(card).Count).IsEqual(0);
    }

    [TestCase]
    public void GetDropZoneGlobalRectIsZeroSize()
    {
        Rect2 dropZone = pile.GetDropZoneGlobalRect();

        AssertThat(dropZone.Size).IsEqual(Vector2.Zero);
    }

    // Verified indirectly via Position, since GetLocalOffset is protected
    [TestCase]
    public void AllCardsStackAtTheSameLocalPosition()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card first = AutoFree(BuildCard(Suit.Hearts, 1, false, dragLayer))!;
        Card second = AutoFree(BuildCard(Suit.Hearts, 2, false, dragLayer))!;
        scratch.AddChild(first);
        scratch.AddChild(second);

        pile.AddCards(new List<Card> { first, second });

        AssertThat(first.Position).IsEqual(Vector2.Zero);
        AssertThat(second.Position).IsEqual(Vector2.Zero);
    }

    // The actual fix for the documented click-through bug: stock-resident cards must not compete
    // with ClickZone for input picking
    [TestCase]
    public void AddCardsDisablesInputPickableForResidentCards()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 5, false, dragLayer))!;
        scratch.AddChild(card);
        AssertThat(card.InputPickable).IsTrue();

        pile.AddCards(new List<Card> { card });

        AssertThat(card.InputPickable).IsFalse();
    }

    [TestCase]
    public void RemoveCardsRestoresInputPickableOnRemoval()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 5, false, dragLayer))!;
        scratch.AddChild(card);
        pile.AddCards(new List<Card> { card });
        AssertThat(card.InputPickable).IsFalse();

        pile.RemoveCards(new List<Card> { card });

        AssertThat(card.InputPickable).IsTrue();
    }

    // Rule: clicking the stock deals its top card face-up into the waste
    [TestCase]
    public void ClickDealsTopCardFaceUpToWaste()
    {
        var stock = (StockPile)pile;
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        var waste = AutoFree(new WastePile())!;
        stock.Waste = waste;
        Card card = AutoFree(BuildCard(Suit.Clubs, 9, false, dragLayer))!;
        scratch.AddChild(card);
        stock.AddCards(new List<Card> { card });
        // Manual call for setup, no SceneTree to auto call _Ready()
        stock._Ready();

        EmitClick(stock.GetNode<Area2D>("ClickZone"));

        AssertThat(stock.Cards.Count).IsEqual(0);
        AssertThat(waste.Cards.Count).IsEqual(1);
        AssertThat(waste.TopCard).IsEqual(card);
        AssertThat(card.IsFaceUp).IsTrue();
    }

    // Rule: clicking with both piles exhausted is a safe no-op
    [TestCase]
    public void ClickNoOpsWhenBothStockAndWasteAreEmpty()
    {
        var stock = (StockPile)pile;
        var waste = AutoFree(new WastePile())!;
        stock.Waste = waste;
        stock._Ready();

        EmitClick(stock.GetNode<Area2D>("ClickZone"));

        AssertThat(stock.Cards.Count).IsEqual(0);
        AssertThat(waste.Cards.Count).IsEqual(0);
    }

    // Rule: draw-1, unlimited redeals - recycling the waste back into the stock must reproduce the
    // exact same draw order on the next pass through. Proves the documented reversal-correctness
    // gotcha actually holds, not just that "some" reversal happens
    [TestCase]
    public void ClickRecyclesWasteBackIntoStockReproducingOriginalDrawOrderFaceDown()
    {
        var stock = (StockPile)pile;
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        var waste = AutoFree(new WastePile())!;
        stock.Waste = waste;
        Card a = AutoFree(BuildCard(Suit.Hearts, 1, false, dragLayer))!;
        Card b = AutoFree(BuildCard(Suit.Hearts, 2, false, dragLayer))!;
        Card c = AutoFree(BuildCard(Suit.Hearts, 3, false, dragLayer))!;
        scratch.AddChild(a);
        scratch.AddChild(b);
        scratch.AddChild(c);
        stock.AddCards(new List<Card> { a, b, c });
        stock._Ready();
        Area2D clickZone = stock.GetNode<Area2D>("ClickZone");

        var firstPassOrder = new List<Card>();
        EmitClick(clickZone);
        firstPassOrder.Add(waste.TopCard!);
        EmitClick(clickZone);
        firstPassOrder.Add(waste.TopCard!);
        EmitClick(clickZone);
        firstPassOrder.Add(waste.TopCard!);
        AssertThat(stock.Cards.Count).IsEqual(0);

        // Stock now empty, waste non-empty - this click recycles instead of drawing
        EmitClick(clickZone);

        AssertThat(waste.Cards.Count).IsEqual(0);
        AssertThat(stock.Cards.Count).IsEqual(3);
        foreach (Card card in stock.Cards) { AssertThat(card.IsFaceUp).IsFalse(); }

        var secondPassOrder = new List<Card>();
        EmitClick(clickZone);
        secondPassOrder.Add(waste.TopCard!);
        EmitClick(clickZone);
        secondPassOrder.Add(waste.TopCard!);
        EmitClick(clickZone);
        secondPassOrder.Add(waste.TopCard!);

        AssertThat(secondPassOrder[0]).IsEqual(firstPassOrder[0]);
        AssertThat(secondPassOrder[1]).IsEqual(firstPassOrder[1]);
        AssertThat(secondPassOrder[2]).IsEqual(firstPassOrder[2]);
    }
}
