using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Procedural deal source: builds a shuffled 52-card deck and distributes it into the tableau/stock per traditional Klondike rules.
/// Intended as a development/testing deal source - a separate, data-driven primary source is planned but not yet built.
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

    private static List<Card> BuildShuffledDeck(PackedScene cardScene, Node2D dragLayer)
    {
        List<Card> deck = new List<Card>();
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            for (int rank = 1; rank <= 13; rank++)
            {
                Card card = cardScene.Instantiate<Card>();
                card.Configure(suit, rank, false, dragLayer);
                dragLayer.AddChild(card);
                deck.Add(card);
            }
        }

        // Fisher-Yates shuffle using Godot's own RNG
        for (int i = deck.Count - 1; i > 0; i--)
        {
            int j = GD.RandRange(0, i);
            (deck[i], deck[j]) = (deck[j], deck[i]);
        }

        return deck;
    }
}