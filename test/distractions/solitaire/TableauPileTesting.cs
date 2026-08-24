using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Collections.Generic;

// TestSuite for TableauPile-specific behaviour
// (see PileTesting for the generic Pile contract checks)
[TestSuite]
[RequireGodotRuntime]
public class TableauPileTesting : PileTesting
{
    protected override Pile CreatePile()
    {
        // Barebones stand-in (no children needed - TableauPile has no _Ready() override)
        return new TableauPile();
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


    // Rule: an empty column may only receive a King
    [TestCase]
    public void CanAcceptOnEmptyColumnOnlyAllowsKing()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card king = AutoFree(BuildCard(Suit.Spades, 13, true, dragLayer))!;
        Card queen = AutoFree(BuildCard(Suit.Hearts, 12, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { king })).IsTrue();
        AssertThat(pile.CanAccept(new List<Card> { queen })).IsFalse();
    }

    // Rule: a non-empty column accepts a card one rank below its top card, of the opposite color
    [TestCase]
    public void CanAcceptOnNonEmptyColumnRequiresDescendingAlternatingColor()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card resident = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;
        scratch.AddChild(resident);
        pile.AddCards(new List<Card> { resident });
        Card candidate = AutoFree(BuildCard(Suit.Clubs, 6, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { candidate })).IsTrue();
    }

    [TestCase]
    public void CanAcceptRejectsSameColorOnNonEmptyColumn()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card resident = AutoFree(BuildCard(Suit.Clubs, 7, true, dragLayer))!;
        scratch.AddChild(resident);
        pile.AddCards(new List<Card> { resident });
        Card candidate = AutoFree(BuildCard(Suit.Spades, 6, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { candidate })).IsFalse();
    }

    [TestCase]
    public void CanAcceptRejectsNonSequentialRankOnNonEmptyColumn()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card resident = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;
        scratch.AddChild(resident);
        pile.AddCards(new List<Card> { resident });
        Card candidate = AutoFree(BuildCard(Suit.Clubs, 5, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { candidate })).IsFalse();
    }

    // Regression guard for the documented "compares against run[0], not run[^1]" rule: the second
    // member is deliberately a card that would fail the check on its own, to prove it's never consulted
    [TestCase]
    public void CanAcceptJudgesMultiCardRunByLeadCardOnly()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card resident = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;
        scratch.AddChild(resident);
        pile.AddCards(new List<Card> { resident });
        Card lead = AutoFree(BuildCard(Suit.Clubs, 6, true, dragLayer))!;
        Card second = AutoFree(BuildCard(Suit.Spades, 13, true, dragLayer))!;

        AssertThat(pile.CanAccept(new List<Card> { lead, second })).IsTrue();
    }

    // Rule: a covered (face-down) card can never be picked up
    [TestCase]
    public void GetMovableRunReturnsEmptyForFaceDownCard()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 7, false, dragLayer))!;
        scratch.AddChild(card);
        pile.AddCards(new List<Card> { card });

        AssertThat(pile.GetMovableRun(card).Count).IsEqual(0);
    }

    // Rule: a card can't be picked up if what's stacked on it isn't itself a legal run - built via
    // direct AddCards (bypasses CanAccept) to manufacture an otherwise-illegal same-color stack
    [TestCase]
    public void GetMovableRunReturnsEmptyWhenFollowedByInvalidSequence()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card bottomCard = AutoFree(BuildCard(Suit.Clubs, 6, true, dragLayer))!;
        Card topCard = AutoFree(BuildCard(Suit.Spades, 5, true, dragLayer))!;
        scratch.AddChild(bottomCard);
        scratch.AddChild(topCard);
        pile.AddCards(new List<Card> { bottomCard, topCard });

        AssertThat(pile.GetMovableRun(bottomCard).Count).IsEqual(0);
    }

    // Rule: a valid descending, alternating-color sequence is fully movable from any of its cards
    [TestCase]
    public void GetMovableRunReturnsFullValidSequenceFromGivenCard()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card bottom = AutoFree(BuildCard(Suit.Clubs, 8, true, dragLayer))!;
        Card middle = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;
        Card top = AutoFree(BuildCard(Suit.Spades, 6, true, dragLayer))!;
        scratch.AddChild(bottom);
        scratch.AddChild(middle);
        scratch.AddChild(top);
        pile.AddCards(new List<Card> { bottom, middle, top });

        IReadOnlyList<Card> fromBottom = pile.GetMovableRun(bottom);
        AssertThat(fromBottom.Count).IsEqual(3);
        AssertThat(fromBottom[0]).IsEqual(bottom);
        AssertThat(fromBottom[2]).IsEqual(top);

        IReadOnlyList<Card> fromMiddle = pile.GetMovableRun(middle);
        AssertThat(fromMiddle.Count).IsEqual(2);
        AssertThat(fromMiddle[0]).IsEqual(middle);
        AssertThat(fromMiddle[1]).IsEqual(top);
    }

    [TestCase]
    public void GetMovableRunReturnsEmptyForCardNotInThisPile()
    {
        var dragLayer = AutoFree(new Node2D())!;
        Card outsider = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;

        AssertThat(pile.GetMovableRun(outsider).Count).IsEqual(0);
    }

    // A card's own face state determines how much it contributes to whatever is stacked on it -
    // verified indirectly via the resulting Position, since GetLocalOffset is protected
    [TestCase]
    public void RepositionAllAccumulatesFaceUpAndFaceDownOffsetsSeparately()
    {
        float faceDownOffsetY = pile.Get("_faceDownOffsetY").AsSingle();
        float faceUpOffsetY = pile.Get("_faceUpOffsetY").AsSingle();
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card faceDownCard = AutoFree(BuildCard(Suit.Clubs, 9, false, dragLayer))!;
        Card faceUpCard = AutoFree(BuildCard(Suit.Hearts, 8, true, dragLayer))!;
        Card topCard = AutoFree(BuildCard(Suit.Clubs, 7, true, dragLayer))!;
        scratch.AddChild(faceDownCard);
        scratch.AddChild(faceUpCard);
        scratch.AddChild(topCard);

        pile.AddCards(new List<Card> { faceDownCard, faceUpCard, topCard });

        AssertThat(topCard.Position.Y).IsEqual(faceDownOffsetY + faceUpOffsetY);
    }

    [TestCase]
    public void GetDropZoneGlobalRectIsAFixedWidthColumnExtendingFarDownward()
    {
        float dropZoneWidth = pile.Get("_dropZoneWidth").AsSingle();
        float dropZoneVerticalOffset = pile.Get("_dropZoneVerticalOffset").AsSingle();

        Rect2 dropZone = pile.GetDropZoneGlobalRect();

        AssertThat(dropZone.Size.X).IsEqual(dropZoneWidth);
        AssertThat(dropZone.Position.X).IsEqual(pile.GlobalPosition.X - dropZoneWidth / 2f);
        AssertThat(dropZone.Position.Y).IsEqual(pile.GlobalPosition.Y - dropZoneVerticalOffset);
        AssertThat(dropZone.HasPoint(pile.GlobalPosition + new Vector2(0, 900))).IsTrue();
    }

    // Rule: removing the exposed card reveals whatever was covered beneath it
    [TestCase]
    public void RemoveCardsRevealsNewTopCardWhenFaceDown()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card bottomFaceDown = AutoFree(BuildCard(Suit.Clubs, 9, false, dragLayer))!;
        Card topFaceUp = AutoFree(BuildCard(Suit.Hearts, 8, true, dragLayer))!;
        scratch.AddChild(bottomFaceDown);
        scratch.AddChild(topFaceUp);
        pile.AddCards(new List<Card> { bottomFaceDown, topFaceUp });

        pile.RemoveCards(new List<Card> { topFaceUp });

        AssertThat(bottomFaceDown.IsFaceUp).IsTrue();
    }

    [TestCase]
    public void RemoveCardsOnEmptyingPileDoesNotThrow()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card onlyCard = AutoFree(BuildCard(Suit.Hearts, 7, true, dragLayer))!;
        scratch.AddChild(onlyCard);
        pile.AddCards(new List<Card> { onlyCard });

        pile.RemoveCards(new List<Card> { onlyCard });

        AssertThat(pile.Cards.Count).IsEqual(0);
        AssertThat(pile.TopCard).IsNull();
    }
}
