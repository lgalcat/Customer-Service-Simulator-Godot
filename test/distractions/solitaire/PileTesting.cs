using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Collections.Generic;

// Abstract boilerplate testcases for all implementations of the Pile abstract class
// Limitations on the GdUnit test discovery pipeline prevent inheritted methods to register as testcases
// All implementations of this class should explicitly declare an override + base for all testcases below
public abstract class PileTesting
{
    protected Pile pile = null!;

    /// <summary>
    /// Instantiation method to inject with individual initialization steps and artifacts
    /// </summary>
    protected abstract Pile CreatePile();

    /// <summary>
    /// Common setup steps for all tests
    /// </summary>
    public virtual void Setup()
    {
        pile = AutoFree(CreatePile());
    }

    /// <summary>
    /// Common teardown for all tests
    /// </summary>
    public virtual void Teardown()
    {
        // Cleanup is handled by AutoFree(...) in Setup(), no explicit freeing needed here
    }

    // Helper building a minimal Card (testing independent from any scene): only the child node
    // Configure() actually touches. Hframes/Vframes must match the real atlas grid (14x4) -
    // FrameCoords is silently clamped to (0,0) against a default 1x1 grid otherwise
    protected static Card BuildCard(Suit suit, int rank, bool faceUp, Node2D dragLayer)
    {
        var card = new Card();
        card.AddChild(new Sprite2D { Name = "CardSprite", Hframes = 14, Vframes = 4 });
        card.Configure(suit, rank, faceUp, dragLayer);
        return card;
    }

    /// <summary>
    /// Tests that AddCards appends to Cards, reparents under the pile, and assigns CurrentPile
    /// </summary>
    public virtual void AddCardsAppendsReparentsAndSetsCurrentPile()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 5, true, dragLayer))!;
        scratch.AddChild(card);

        pile.AddCards(new List<Card> { card });

        AssertThat(pile.Cards.Count).IsEqual(1);
        AssertThat(pile.Cards[0]).IsEqual(card);
        AssertThat(card.CurrentPile).IsEqual(pile);
        AssertThat(card.GetParent()).IsEqual(pile);
    }

    /// <summary>
    /// Tests that RemoveCards removes from Cards and clears CurrentPile, but deliberately does not reparent
    /// </summary>
    public virtual void RemoveCardsRemovesClearsCurrentPileWithoutReparenting()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card card = AutoFree(BuildCard(Suit.Hearts, 5, true, dragLayer))!;
        scratch.AddChild(card);
        pile.AddCards(new List<Card> { card });

        pile.RemoveCards(new List<Card> { card });

        AssertThat(pile.Cards.Count).IsEqual(0);
        AssertThat(card.CurrentPile).IsNull();
        AssertThat(card.GetParent()).IsEqual(pile);
    }

    /// <summary>
    /// Tests that TopCard tracks the most recently added card and reverts correctly on removal, null when empty
    /// </summary>
    public virtual void TopCardReflectsMostRecentlyAddedCardAndNullWhenEmpty()
    {
        var dragLayer = AutoFree(new Node2D())!;
        var scratch = AutoFree(new Node2D())!;
        Card cardA = AutoFree(BuildCard(Suit.Hearts, 5, true, dragLayer))!;
        Card cardB = AutoFree(BuildCard(Suit.Clubs, 4, true, dragLayer))!;
        scratch.AddChild(cardA);
        scratch.AddChild(cardB);
        AssertThat(pile.TopCard).IsNull();

        pile.AddCards(new List<Card> { cardA });
        AssertThat(pile.TopCard).IsEqual(cardA);

        pile.AddCards(new List<Card> { cardB });
        AssertThat(pile.TopCard).IsEqual(cardB);

        pile.RemoveCards(new List<Card> { cardB });
        AssertThat(pile.TopCard).IsEqual(cardA);

        pile.RemoveCards(new List<Card> { cardA });
        AssertThat(pile.TopCard).IsNull();
    }

    /// <summary>
    /// Tests that _Ready() registers this pile in the "piles" group Card discovers drop targets through
    /// </summary>
    public virtual void ReadyAddsPileToPilesGroup()
    {
        // Manual call for setup, no SceneTree to auto call _Ready()
        pile._Ready();

        AssertThat(pile.IsInGroup("piles")).IsTrue();
    }
}
