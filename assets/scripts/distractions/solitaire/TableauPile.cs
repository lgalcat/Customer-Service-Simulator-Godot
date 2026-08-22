using Godot;
using System.Collections.Generic;

/// <summary>
/// A tableau column: builds descending, alternating-color runs; accepts only a King onto an empty column.
/// </summary>
public partial class TableauPile : Pile
{
    // Downward extent of the drop zone, comfortably past the stage regardless of final art sizing
    private const float DropZoneOverflow = 1000f;

    /// <summary>
    /// Vertical cascade step contributed by a face-down card to whatever is stacked on top of it.
    /// </summary>
    [Export] private float _faceDownOffsetY = 6f;

    /// <summary>
    /// Vertical cascade step contributed by a face-up card to whatever is stacked on top of it.
    /// </summary>
    [Export] private float _faceUpOffsetY = 11f;

    /// <summary>
    /// Width of this column's drop zone, independent from the card's own collision size.
    /// </summary>
    [Export] private float _dropZoneWidth = 50f;

    public override bool CanAccept(IReadOnlyList<Card> run)
    {
        Card lead = run[0];
        if (TopCard == null) { return lead.Rank == 13; }
        return lead.Rank == TopCard.Rank - 1 && Card.IsRed(lead.CardSuit) != Card.IsRed(TopCard.CardSuit);
    }

    public override IReadOnlyList<Card> GetMovableRun(Card start)
    {
        int startIndex = _cards.IndexOf(start);
        if (startIndex < 0 || !start.IsFaceUp) { return new List<Card>(); }

        for (int i = startIndex; i < _cards.Count - 1; i++)
        {
            Card current = _cards[i];
            Card next = _cards[i + 1];
            bool isValidStep = next.IsFaceUp && next.Rank == current.Rank - 1 && Card.IsRed(next.CardSuit) != Card.IsRed(current.CardSuit);
            if (!isValidStep) { return new List<Card>(); }
        }

        return _cards.GetRange(startIndex, _cards.Count - startIndex);
    }

    public override Rect2 GetDropZoneGlobalRect()
    {
        Vector2 origin = GlobalPosition;
        return new Rect2(origin.X - _dropZoneWidth / 2f, origin.Y, _dropZoneWidth, DropZoneOverflow);
    }

    protected override Vector2 GetLocalOffset(int index)
    {
        float y = 0f;
        for (int i = 0; i < index; i++)
        {
            y += _cards[i].IsFaceUp ? _faceUpOffsetY : _faceDownOffsetY;
        }
        return new Vector2(0f, y);
    }

    public override void RemoveCards(IReadOnlyList<Card> run)
    {
        base.RemoveCards(run);
        if (TopCard != null && !TopCard.IsFaceUp) { TopCard.SetFaceUp(true); }
    }
}