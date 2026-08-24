using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Collections.Generic;

// TestSuite for FoundationPile-specific behaviour
// (see PileTesting for the generic Pile contract checks)
[TestSuite]
[RequireGodotRuntime]
public class FoundationPileTesting : PileTesting
{
    protected override Pile CreatePile()
    {
        // Barebones stand-in (no children needed - FoundationPile has no _Ready() override)
        return new FoundationPile { Suit = Suit.Hearts };
    }

    // Setup before each test
    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
    }

    // Teardown after each test
    [AfterTest]
    public override void Teardown()
    {
        base.Teardown();
    }


    // Block containing base calls to inheritted TestCases, no class specific logic should be present further down
    [TestCase]
    public override void AddCardsAppendsReparentsAndSetsCurrentPile() { base.AddCardsAppendsReparentsAndSetsCurrentPile(); }

    [TestCase]
    public override void RemoveCardsRemovesClearsCurrentPileWithoutReparenting() { base.RemoveCardsRemovesClearsCurrentPileWithoutReparenting(); }

    [TestCase]
    public override void TopCardReflectsMostRecentlyAddedCardAndNullWhenEmpty() { base.TopCardReflectsMostRecentlyAddedCardAndNullWhenEmpty(); }

    [TestCase]
    public override void ReadyAddsPileToPilesGroup() { base.ReadyAddsPileToPilesGroup(); }


    // Rule: a foundation only ever accepts a single card at a time, even if the lead would otherwise be valid
    [TestCase]
    public void CanAcceptRejectsMultiCardRuns()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card lead = AutoFree(BuildCard(Suit.Hearts, 1, true, dragLayer))!;
        Card second = AutoFree(BuildCard(Suit.Hearts, 2, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { lead, second })).IsFalse();
    }

    // Rule: an empty foundation only accepts an Ace of its own suit
    [TestCase]
    public void CanAcceptOnEmptyFoundationOnlyAllowsMatchingSuitAce()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card matchingAce = AutoFree(BuildCard(Suit.Hearts, 1, true, dragLayer))!;
        Card wrongSuitAce = AutoFree(BuildCard(Suit.Clubs, 1, true, dragLayer))!;
        Card matchingNonAce = AutoFree(BuildCard(Suit.Hearts, 2, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { matchingAce })).IsTrue();
        AssertThat(pile.CanAccept(new List<Card> { wrongSuitAce })).IsFalse();
        AssertThat(pile.CanAccept(new List<Card> { matchingNonAce })).IsFalse();
    }

    // Rule: a non-empty foundation only accepts the next rank up, of its own suit
    [TestCase]
    public void CanAcceptOnNonEmptyFoundationRequiresNextRankSameSuit()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card resident = AutoFree(BuildCard(Suit.Hearts, 5, true, dragLayer))!;
        scratch.AddChild(resident);
        pile.AddCards(new List<Card> { resident });
        Card nextRankMatchingSuit = AutoFree(BuildCard(Suit.Hearts, 6, true, dragLayer))!;
        Card skipRankMatchingSuit = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;
        Card nextRankWrongSuit = AutoFree(BuildCard(Suit.Clubs, 6, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { nextRankMatchingSuit })).IsTrue();
        AssertThat(pile.CanAccept(new List<Card> { skipRankMatchingSuit })).IsFalse();
        AssertThat(pile.CanAccept(new List<Card> { nextRankWrongSuit })).IsFalse();
    }

    // Rule: no sequence concept on a foundation - only the exposed top card can ever be dragged
    // back off (the "foundation drag-off" rule)
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

    // Exact stacking, no cascade - verified indirectly via Position, since GetLocalOffset is protected
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

    [TestCase]
    public void GetDropZoneGlobalRectIsAFixedSizeRectCenteredOnPosition()
    {
        Vector2 dropZoneSize = pile.Get("_dropZoneSize").AsVector2();

        Rect2 dropZone = pile.GetDropZoneGlobalRect();

        AssertThat(dropZone.Size).IsEqual(dropZoneSize);
        AssertThat(dropZone.Position).IsEqual(pile.GlobalPosition - dropZoneSize / 2f);
    }

    // Rule: the foundation is "filled" exactly when it reaches King, not before
    [TestCase]
    public void AddCardsFiresFilledOnlyWhenReachingKing()
    {
        var foundation = (FoundationPile)pile;
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        int filledCount = 0;
        foundation.Filled += () => { filledCount++; };

        for (int rank = 1; rank <= 12; rank++)
        {
            Card card = AutoFree(BuildCard(Suit.Hearts, rank, true, dragLayer))!;
            scratch.AddChild(card);
            foundation.AddCards(new List<Card> { card });
        }
        AssertThat(filledCount).IsEqual(0);

        Card king = AutoFree(BuildCard(Suit.Hearts, 13, true, dragLayer))!;
        scratch.AddChild(king);
        foundation.AddCards(new List<Card> { king });

        AssertThat(filledCount).IsEqual(1);
    }

    // AddCards fires Filled unconditionally on every King-topping add, with no memory of previous
    // fires - the prerequisite that lets Solitaire's own live-recheck OnFoundationFilled correctly
    // handle a King dragged off and back on (regression-tested at that layer already)
    [TestCase]
    public void AddCardsFiresFilledAgainOnFoundationDragOffAndBackOn()
    {
        var foundation = (FoundationPile)pile;
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card king = AutoFree(BuildCard(Suit.Hearts, 13, true, dragLayer))!;
        scratch.AddChild(king);
        int filledCount = 0;
        foundation.Filled += () => { filledCount++; };
        foundation.AddCards(new List<Card> { king });
        AssertThat(filledCount).IsEqual(1);

        foundation.RemoveCards(new List<Card> { king });
        foundation.AddCards(new List<Card> { king });

        AssertThat(filledCount).IsEqual(2);
    }
}