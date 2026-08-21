using Godot;
using System.Collections.Generic;

/// <summary>
/// Base class for all Solitaire card containers (tableau, foundation, stock, waste).
/// </summary>
public abstract partial class Pile : Node2D
{
    protected List<Card> _cards = new();

    /// <summary>
    /// Cards currently held by this pile, ordered bottom (index 0) to top/exposed (last index).
    /// </summary>
    public IReadOnlyList<Card> Cards { get => _cards; }

    /// <summary>
    /// The topmost (most recently added) card, or null if the pile is empty.
    /// </summary>
    public Card? TopCard { get => _cards.Count > 0 ? _cards[^1] : null; }

    // Joins the "piles" group used by Card to discover drop targets - subclasses overriding _Ready must call base._Ready()
    public override void _Ready()
    {
        AddToGroup("piles");
    }

    /// <summary>
    /// Whether this pile would accept the given run (bottom-to-top ordered) if dropped on it right now.
    /// </summary>
    public abstract bool CanAccept(IReadOnlyList<Card> run);

    /// <summary>
    /// Returns the contiguous, currently-movable run starting at (and including) <paramref name="start"/>, or an empty list if it can't be picked up.
    /// </summary>
    public abstract IReadOnlyList<Card> GetMovableRun(Card start);

    /// <summary>
    /// Global-space rectangle used to detect a run dropped onto this pile.
    /// </summary>
    public abstract Rect2 GetDropZoneGlobalRect();

    /// <summary>
    /// Local position this pile lays a card at, given its index within <see cref="Cards"/>.
    /// </summary>
    protected abstract Vector2 GetLocalOffset(int index);

    /// <summary>
    /// Adds a run (bottom-to-top ordered) to the top of this pile, reparenting and repositioning it.
    /// </summary>
    public virtual void AddCards(IReadOnlyList<Card> run)
    {
        foreach (Card card in run)
        {
            card.Reparent(this, false);
            card.CurrentPile = this;
            _cards.Add(card);
        }
        RepositionAll();
    }

    /// <summary>
    /// Removes a run (bottom-to-top ordered) from this pile.
    /// </summary>
    // Deliberately does not reparent - a card only ever leaves a pile via a drag (already reparented under
    // the drag layer by Card.TryPickUp) or a programmatic move immediately followed by a paired AddCards call
    // on the destination, which is what actually performs the Reparent(). Never call this without a paired
    // AddCards right after, or the card is left orphaned under whatever parent it had when this ran.
    public virtual void RemoveCards(IReadOnlyList<Card> run)
    {
        foreach (Card card in run)
        {
            _cards.Remove(card);
            card.CurrentPile = null;
        }
        RepositionAll();
    }

    /// <summary>
    /// Repositions every currently-held card according to <see cref="GetLocalOffset"/>.
    /// </summary>
    public void RepositionAll()
    {
        for (int i = 0; i < _cards.Count; i++)
        {
            _cards[i].Position = GetLocalOffset(i);
        }
    }
}