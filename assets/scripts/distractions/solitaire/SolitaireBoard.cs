using Godot;
using System;
using System.Collections.Generic;
using System.Globalization;

/// <summary>
/// One card of an authored board: its identity and face state
/// </summary>
public readonly record struct CardSpec(Suit Suit, int Rank, bool FaceUp);

/// <summary>
/// A parsed and validated <see cref="SolitaireBoard"/>, ready for <see cref="DealBuilder.DealBoard"/>. Every card
/// list is ordered bottom to top
/// </summary>
/// <param name="Tableaus">The seven tableau columns</param>
/// <param name="FoundationHeights">How many cards (Ace upwards) each suit's foundation holds</param>
/// <param name="Stock">The listed stock cards, all face-down; the last one is drawn first</param>
/// <param name="Waste">The waste cards, all face-up</param>
/// <param name="StockRemainder">Unlisted cards to shuffle under the listed stock, all face-down; empty when the
/// board leaves them out of play</param>
public record SolitaireBoardPlan(
    IReadOnlyList<IReadOnlyList<CardSpec>> Tableaus,
    IReadOnlyDictionary<Suit, int> FoundationHeights,
    IReadOnlyList<CardSpec> Stock,
    IReadOnlyList<CardSpec> Waste,
    IReadOnlyList<CardSpec> StockRemainder);

/// <summary>
/// An authored Solitaire board state, loaded through <see cref="Solitaire.BoardOverride"/>. An interim tool to
/// explore board sourcing (e.g. "short" boards) in-game, not a final design
/// <para>Cards are written as rank (<c>A</c>, <c>2</c>-<c>10</c>, <c>J</c>, <c>Q</c>, <c>K</c>) plus suit
/// (<c>H</c>, <c>D</c>, <c>C</c>, <c>S</c>), case-insensitive, separated by spaces, bottom to top. In a tableau,
/// cards left of an optional <c>|</c> are face-down and the rest face-up (<c>"5H 9C | KS QH"</c>); without one
/// every card is face-up. A foundation always holds Ace..N of its suit, so it's written as its height N</para>
/// </summary>
[GlobalClass]
public partial class SolitaireBoard : Resource
{
    /// <summary>
    /// Tableau column 0 (leftmost), bottom to top
    /// </summary>
    [ExportGroup("Tableau")]
    [Export] public string Tableau0 { get; set; } = "";

    /// <summary>
    /// Tableau column 1, bottom to top
    /// </summary>
    [Export] public string Tableau1 { get; set; } = "";

    /// <summary>
    /// Tableau column 2, bottom to top
    /// </summary>
    [Export] public string Tableau2 { get; set; } = "";

    /// <summary>
    /// Tableau column 3, bottom to top
    /// </summary>
    [Export] public string Tableau3 { get; set; } = "";

    /// <summary>
    /// Tableau column 4, bottom to top
    /// </summary>
    [Export] public string Tableau4 { get; set; } = "";

    /// <summary>
    /// Tableau column 5, bottom to top
    /// </summary>
    [Export] public string Tableau5 { get; set; } = "";

    /// <summary>
    /// Tableau column 6 (rightmost), bottom to top
    /// </summary>
    [Export] public string Tableau6 { get; set; } = "";

    /// <summary>
    /// Cards on the Hearts foundation: Ace up to this rank (0 = empty)
    /// </summary>
    [ExportGroup("Foundations")]
    [Export(PropertyHint.Range, "0,13")] public int HeartsHeight { get; set; }

    /// <summary>
    /// Cards on the Diamonds foundation: Ace up to this rank (0 = empty)
    /// </summary>
    [Export(PropertyHint.Range, "0,13")] public int DiamondsHeight { get; set; }

    /// <summary>
    /// Cards on the Clubs foundation: Ace up to this rank (0 = empty)
    /// </summary>
    [Export(PropertyHint.Range, "0,13")] public int ClubsHeight { get; set; }

    /// <summary>
    /// Cards on the Spades foundation: Ace up to this rank (0 = empty)
    /// </summary>
    [Export(PropertyHint.Range, "0,13")] public int SpadesHeight { get; set; }

    /// <summary>
    /// Listed stock cards, bottom to top (the last one is drawn first), always face-down
    /// </summary>
    [ExportGroup("Stock and waste")]
    [Export] public string Stock { get; set; } = "";

    /// <summary>
    /// Waste cards, bottom to top, always face-up
    /// </summary>
    [Export] public string Waste { get; set; } = "";

    /// <summary>
    /// Whether cards listed nowhere on the board are shuffled into the stock, under the listed stock cards.
    /// Off leaves them out of play (winnability is then up to the author)
    /// </summary>
    [Export] public bool ShuffleUnlistedIntoStock { get; set; } = true;

    /// <summary>
    /// Parses and validates the board
    /// </summary>
    /// <exception cref="InvalidOperationException">The board is mis-authored: a malformed card, a misplaced
    /// <c>|</c>, a foundation height outside 0..13, a card placed twice, or a tableau whose top card is
    /// face-down (nothing would ever flip it).</exception>
    public SolitaireBoardPlan Parse()
    {
        string board = string.IsNullOrEmpty(ResourcePath) ? ResourceName : ResourcePath;
        // Where each card has been placed so far, to report duplicates against their first placement
        Dictionary<(Suit, int), string> placedIn = new();

        Dictionary<Suit, int> heights = new()
        {
            [Suit.Hearts] = HeartsHeight,
            [Suit.Diamonds] = DiamondsHeight,
            [Suit.Clubs] = ClubsHeight,
            [Suit.Spades] = SpadesHeight,
        };
        foreach ((Suit suit, int height) in heights)
        {
            if (height < 0 || height > 13) { throw Fail(board, $"{suit}Height {height} must be between 0 and 13"); }
            for (int rank = 1; rank <= height; rank++) { placedIn[(suit, rank)] = $"{suit}Height"; }
        }

        string[] columns = { Tableau0, Tableau1, Tableau2, Tableau3, Tableau4, Tableau5, Tableau6 };
        List<IReadOnlyList<CardSpec>> tableaus = new();
        for (int i = 0; i < columns.Length; i++)
        {
            string pile = $"Tableau{i}";
            string[] sections = columns[i].Split('|');
            if (sections.Length > 2) { throw Fail(board, $"{pile} has more than one '|'"); }

            List<CardSpec> cards = new();
            if (sections.Length == 2) { cards.AddRange(ParseCards(board, pile, sections[0], false, placedIn)); }
            cards.AddRange(ParseCards(board, pile, sections[^1], true, placedIn));
            if (cards.Count > 0 && !cards[^1].FaceUp)
            {
                throw Fail(board, $"{pile}'s top card is face-down and nothing would ever flip it; put at least one card after the '|'");
            }
            tableaus.Add(cards);
        }

        if (Stock.Contains('|')) { throw Fail(board, "Stock can't use '|' (stock cards are always face-down)"); }
        List<CardSpec> stock = ParseCards(board, nameof(Stock), Stock, false, placedIn);
        if (Waste.Contains('|')) { throw Fail(board, "Waste can't use '|' (waste cards are always face-up)"); }
        List<CardSpec> waste = ParseCards(board, nameof(Waste), Waste, true, placedIn);

        List<CardSpec> remainder = new();
        if (ShuffleUnlistedIntoStock)
        {
            foreach (Suit suit in Enum.GetValues<Suit>())
            {
                for (int rank = 1; rank <= 13; rank++)
                {
                    if (!placedIn.ContainsKey((suit, rank))) { remainder.Add(new CardSpec(suit, rank, false)); }
                }
            }
        }

        return new SolitaireBoardPlan(tableaus, heights, stock, waste, remainder);
    }

    // Parses one pile's space-separated cards, all with the given face state, recording each placement
    private static List<CardSpec> ParseCards(string board, string pile, string text, bool faceUp, Dictionary<(Suit, int), string> placedIn)
    {
        List<CardSpec> cards = new();
        foreach (string token in text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries))
        {
            if (!TryParseCard(token, out Suit suit, out int rank))
            {
                throw Fail(board, $"{pile}: '{token}' is not a card (rank A, 2-10, J, Q or K followed by suit H, D, C or S)");
            }
            if (placedIn.TryGetValue((suit, rank), out string? firstPile))
            {
                throw Fail(board, $"{pile}: '{token}' is already placed in {firstPile}");
            }
            placedIn[(suit, rank)] = pile;
            cards.Add(new CardSpec(suit, rank, faceUp));
        }
        return cards;
    }

    private static bool TryParseCard(string token, out Suit suit, out int rank)
    {
        suit = default;
        rank = 0;
        string upper = token.ToUpperInvariant();
        if (upper.Length < 2 || upper.Length > 3) { return false; }

        switch (upper[^1])
        {
            case 'H': suit = Suit.Hearts; break;
            case 'D': suit = Suit.Diamonds; break;
            case 'C': suit = Suit.Clubs; break;
            case 'S': suit = Suit.Spades; break;
            default: return false;
        }

        string rankText = upper[..^1];
        switch (rankText)
        {
            case "A": rank = 1; return true;
            case "J": rank = 11; return true;
            case "Q": rank = 12; return true;
            case "K": rank = 13; return true;
        }
        // Strict number: digits only, no leading zero, 2..10
        if (rankText[0] == '0' || !int.TryParse(rankText, NumberStyles.None, CultureInfo.InvariantCulture, out rank)) { return false; }
        return rank >= 2 && rank <= 10;
    }

    private static InvalidOperationException Fail(string board, string reason)
    {
        return new InvalidOperationException($"SolitaireBoard '{board}': {reason}");
    }
}
