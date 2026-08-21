using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// A foundation pile: builds a single suit strictly ascending from Ace to King.
/// </summary>
public partial class FoundationPile : Pile
{
    /// <summary>
    /// The suit this foundation accepts.
    /// </summary>
    [Export] public Suit Suit { get; set; }

    /// <summary>
    /// Size of this pile's fixed drop zone, centered on its position.
    /// </summary>
    [Export] private Vector2 _dropZoneSize = new Vector2(50f, 70f);

    /// <summary>
    /// Invoked once this foundation reaches King (rank 13).
    /// </summary>
    public Action? Filled;

    public override bool CanAccept(IReadOnlyList<Card> run)
    {
        if (run.Count != 1) { return false; }

        Card lead = run[0];
        if (lead.CardSuit != Suit) { return false; }
        return TopCard == null ? lead.Rank == 1 : lead.Rank == TopCard.Rank + 1;
    }

    public override IReadOnlyList<Card> GetMovableRun(Card start)
    {
        return start == TopCard ? new List<Card> { start } : new List<Card>();
    }

    public override Rect2 GetDropZoneGlobalRect()
    {
        return new Rect2(GlobalPosition - _dropZoneSize / 2f, _dropZoneSize);
    }

    protected override Vector2 GetLocalOffset(int index)
    {
        return Vector2.Zero;
    }

    public override void AddCards(IReadOnlyList<Card> run)
    {
        base.AddCards(run);
        if (TopCard != null && TopCard.Rank == 13) { Filled?.Invoke(); }
    }
}
