using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Collections.Generic;

// TestSuite for WastePile-specific behaviour
// (see PileTesting for the generic Pile contract checks)
[TestSuite]
[RequireGodotRuntime]
public class WastePileTesting : PileTesting
{
    protected override Pile CreatePile()
    {
        // Barebones stand-in (no children needed - WastePile has no _Ready() override)
        return new WastePile();
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


    // Block containing base calls to inherited TestCases, no class specific logic should be present further down
    [TestCase]
    public override void AddCardsAppendsReparentsAndSetsCurrentPile() { base.AddCardsAppendsReparentsAndSetsCurrentPile(); }

    [TestCase]
    public override void RemoveCardsRemovesClearsCurrentPileWithoutReparenting() { base.RemoveCardsRemovesClearsCurrentPileWithoutReparenting(); }

    [TestCase]
    public override void TopCardReflectsMostRecentlyAddedCardAndNullWhenEmpty() { base.TopCardReflectsMostRecentlyAddedCardAndNullWhenEmpty(); }

    [TestCase]
    public override void ReadyAddsPileToPilesGroup() { base.ReadyAddsPileToPilesGroup(); }


    // Rule: the waste only ever receives cards via StockPile's draw action, never a manual drop
    [TestCase]
    public void CanAcceptAlwaysRejects()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 1, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { card })).IsFalse();
    }

    // Rule: only the exposed top (most recently drawn) card can ever be dragged back off
    [TestCase]
    public void GetMovableRunOnlyReturnsTopCard()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card bottom = AutoFree(BuildCard(Suit.Hearts, 1, true, dragLayer))!;
        Card top = AutoFree(BuildCard(Suit.Hearts, 2, true, dragLayer))!;
        scratch.AddChild(bottom);
        scratch.AddChild(top);
        pile.AddCards(new List<Card> { bottom, top });

        IReadOnlyList<Card> fromTop = pile.GetMovableRun(top);
        AssertThat(fromTop.Count).IsEqual(1);
        AssertThat(fromTop[0]).IsEqual(top);
        AssertThat(pile.GetMovableRun(bottom).Count).IsEqual(0);
    }

    [TestCase]
    public void GetDropZoneGlobalRectIsZeroSize()
    {
        Rect2 dropZone = pile.GetDropZoneGlobalRect();

        AssertThat(dropZone.Size).IsEqual(Vector2.Zero);
    }

    // Only the top card is ever meant to be visible - older cards sit exactly underneath, hidden.
    // Verified indirectly via Position, since GetLocalOffset is protected
    [TestCase]
    public void AllCardsStackAtTheSameLocalPosition()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card first = AutoFree(BuildCard(Suit.Hearts, 1, true, dragLayer))!;
        Card second = AutoFree(BuildCard(Suit.Hearts, 2, true, dragLayer))!;
        scratch.AddChild(first);
        scratch.AddChild(second);

        pile.AddCards(new List<Card> { first, second });

        AssertThat(first.Position).IsEqual(Vector2.Zero);
        AssertThat(second.Position).IsEqual(Vector2.Zero);
    }
}
