using Godot;
using System.Collections.Generic;

/// <summary>
/// The stock pile: face-down draw pile. Deals its top card face-up into the waste pile on click,
/// or recycles the waste pile back into itself (face-down, reversed order) once both are otherwise exhausted.
/// </summary>
public partial class StockPile : Pile
{
    /// <summary>
    /// The waste pile this stock deals into. Must be assigned (by <see cref="Solitaire.Setup"/>) before the stock can be clicked.
    /// </summary>
    public WastePile Waste = null!;

    public override void _Ready()
    {
        base._Ready();
        GetNode<Area2D>("ClickZone").InputEvent += OnClickZoneInputEvent;
    }

    public override bool CanAccept(IReadOnlyList<Card> run)
    {
        // Never a valid drop target - cards only ever leave the stock via Draw()
        return false;
    }

    public override IReadOnlyList<Card> GetMovableRun(Card start)
    {
        // Nothing in the stock is ever independently pickable by drag - resident cards keep InputPickable off
        return new List<Card>();
    }

    public override Rect2 GetDropZoneGlobalRect()
    {
        // Never a valid drop target - a zero-size rect can never contain a point
        return new Rect2();
    }

    protected override Vector2 GetLocalOffset(int index)
    {
        return Vector2.Zero;
    }

    private void OnClickZoneInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left || !mouseButton.Pressed) { return; }

        DrawCard();
    }

    // Deals the top card to waste, or recycles waste back into the stock if the stock is empty; no-ops if both are empty
    private void DrawCard()
    {
        if (TopCard != null)
        {
            Card card = TopCard;
            List<Card> single = new List<Card> { card };
            RemoveCards(single);
            card.SetFaceUp(true);
            Waste.AddCards(single);
        }
        else if (Waste.TopCard != null)
        {
            Recycle();
        }
    }

    private void Recycle()
    {
        // Waste.Cards is a live view over its own mutable list - copy before RemoveCards mutates it mid-enumeration
        List<Card> reclaimed = new List<Card>(Waste.Cards);
        Waste.RemoveCards(reclaimed);

        reclaimed.Reverse();
        foreach (Card card in reclaimed) { card.SetFaceUp(false); }
        AddCards(reclaimed);
    }
}