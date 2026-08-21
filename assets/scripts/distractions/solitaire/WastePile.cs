using Godot;
using System.Collections.Generic;

/// <summary>
/// The waste pile: holds cards drawn from the stock. Only the top (most recently drawn) card is ever accessible.
/// </summary>
public partial class WastePile : Pile
{
    public override bool CanAccept(IReadOnlyList<Card> run)
    {
        // Never a valid drop target - the waste only ever receives cards via StockPile's draw action
        return false;
    }

    public override IReadOnlyList<Card> GetMovableRun(Card start)
    {
        return start == TopCard ? new List<Card> { start } : new List<Card>();
    }

    public override Rect2 GetDropZoneGlobalRect()
    {
        // Never a valid drop target - a zero-size rect can never contain a point
        return new Rect2();
    }

    protected override Vector2 GetLocalOffset(int index)
    {
        // Only the top card is ever meant to be visible - older cards sit exactly underneath, hidden
        return Vector2.Zero;
    }
}