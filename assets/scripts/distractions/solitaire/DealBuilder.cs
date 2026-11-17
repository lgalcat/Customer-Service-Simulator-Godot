using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Deal sources: instances the cards and distributes them into the piles. <see cref="Deal"/> is the procedural
/// source (a shuffled traditional Klondike deal); <see cref="DealBoard"/> lays out an authored
/// <see cref="SolitaireBoard"/> (an interim exploration source, the final sourcing design is still open).
/// </summary>
public static class DealBuilder
{
    /// <summary>
    /// Instances, shuffles, and deals a full deck: tableau pile <c>i</c> gets <c>i+1</c> cards (only the last face-up), the remainder goes to stock face-down.
    /// Foundations/waste are left untouched, matching traditional Klondike setup.
    /// </summary>
    public static void Deal(PackedScene cardScene, Node2D dragLayer, IReadOnlyList<TableauPile> tableaus, StockPile stock)
    {
        List<Card> deck = BuildShuffledDeck(cardScene, dragLayer);

        int deckIndex = 0;
        for (int pileIndex = 0; pileIndex < tableaus.Count; pileIndex++)
        {
            int cardCount = pileIndex + 1;
            List<Card> pileCards = deck.GetRange(deckIndex, cardCount);
            deckIndex += cardCount;

            pileCards[^1].SetFaceUp(true);
            tableaus[pileIndex].AddCards(pileCards);
        }

        List<Card> remainder = deck.GetRange(deckIndex, deck.Count - deckIndex);
        stock.AddCards(remainder);
    }

    /// <summary>
    /// Instances and lays out a parsed authored board: each tableau as listed, each foundation Ace up to its
    /// height (matched by <see cref="FoundationPile.Suit"/>), the stock as the shuffled
    /// <see cref="SolitaireBoardPlan.StockRemainder"/> with the listed stock cards on top, and the waste as listed.
    /// </summary>
    public static void DealBoard(SolitaireBoardPlan plan, PackedScene cardScene, Node2D dragLayer, IReadOnlyList<TableauPile> tableaus,
        IReadOnlyList<FoundationPile> foundations, StockPile stock, WastePile waste)
    {
        // Face state is set at creation, before AddCards, so the tableau cascade offsets see it
        for (int i = 0; i < tableaus.Count; i++)
        {
            tableaus[i].AddCards(CreateCards(cardScene, dragLayer, plan.Tableaus[i]));
        }

        foreach (FoundationPile foundation in foundations)
        {
            List<Card> cards = new List<Card>();
            for (int rank = 1; rank <= plan.FoundationHeights[foundation.Suit]; rank++)
            {
                cards.Add(CreateCard(cardScene, dragLayer, foundation.Suit, rank, true));
            }
            foundation.AddCards(cards);
        }

        List<Card> stockCards = CreateCards(cardScene, dragLayer, plan.StockRemainder);
        Shuffle(stockCards);
        stockCards.AddRange(CreateCards(cardScene, dragLayer, plan.Stock));
        stock.AddCards(stockCards);

        waste.AddCards(CreateCards(cardScene, dragLayer, plan.Waste));
    }

    private static List<Card> BuildShuffledDeck(PackedScene cardScene, Node2D dragLayer)
    {
        List<Card> deck = new List<Card>();
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                deck.Add(CreateCard(cardScene, dragLayer, suit, rank, false));
            }
        }

        Shuffle(deck);
        return deck;
    }

    private static List<Card> CreateCards(PackedScene cardScene, Node2D dragLayer, IReadOnlyList<CardSpec> specs)
    {
        List<Card> cards = new List<Card>();
        foreach (CardSpec spec in specs)
        {
            cards.Add(CreateCard(cardScene, dragLayer, spec.Suit, spec.Rank, spec.FaceUp));
        }
        return cards;
    }

    // dragLayer doubles as the staging parent: Pile.AddCards' Reparent() needs the card to already have one
    private static Card CreateCard(PackedScene cardScene, Node2D dragLayer, Suit suit, int rank, bool faceUp)
    {
        Card card = cardScene.Instantiate<Card>();
        card.Configure(suit, rank, faceUp, dragLayer);
        dragLayer.AddChild(card);
        return card;
    }

    // Fisher-Yates shuffle using Godot's own RNG
    private static void Shuffle(List<Card> cards)
    {
        for (int i = cards.Count - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            (cards[i], cards[j]) = (cards[j], cards[i]);
        }
    }
}
