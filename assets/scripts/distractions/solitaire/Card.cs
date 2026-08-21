using Godot;
using System.Collections.Generic;

/// <summary>
/// Suit of a French-deck playing card. Declaration order matches the card atlas' row order.
/// </summary>
public enum Suit { Hearts, Diamonds, Clubs, Spades }

/// <summary>
/// A single physical playing card: its identity, rendering, and drag/drop interaction.
/// </summary>
public partial class Card : Area2D
{
    private enum CardState { err, idle, dragging }

    /// <summary>
    /// Suit of this card.
    /// </summary>
    public Suit CardSuit { get; private set; }

    /// <summary>
    /// Rank of this card, 1 (Ace) through 13 (King).
    /// </summary>
    public int Rank { get; private set; }

    /// <summary>
    /// Whether this card is currently showing its face (vs. its back).
    /// </summary>
    public bool IsFaceUp { get; private set; }

    /// <summary>
    /// The pile this card logically belongs to. Distinct from its scene-tree parent while mid-drag
    /// (it's temporarily reparented under the drag layer), only ever changed by <see cref="Pile.AddCards"/>/<see cref="Pile.RemoveCards"/>.
    /// </summary>
    public Pile? CurrentPile;

    /// <summary>
    /// Whether the given suit is red (Hearts/Diamonds) as opposed to black (Clubs/Spades).
    /// </summary>
    public static bool IsRed(Suit suit) => suit is Suit.Hearts or Suit.Diamonds;

    // Column/row into the card atlas' back design - fixed regardless of this card's actual suit/rank
    private static readonly Vector2I _faceDownFrame = new Vector2I(13, 1);

    private CardState _state = CardState.err;
    private Node2D _dragLayer = null!;
    private Sprite2D _sprite = null!;

    // Captured at pickup: how far the cursor was from this card, and how far each run member was from this card
    private Vector2 _grabOffset;
    private List<Card> _dragRun = new();
    private List<Vector2> _followerOffsets = new();

    public override void _Ready()
    {
        InputEvent += OnInputEvent;
    }

    public override void _Process(double delta)
    {
        if (_state != CardState.dragging) { return; }

        GlobalPosition = GetGlobalMousePosition() - _grabOffset;
        for (int i = 1; i < _dragRun.Count; i++)
        {
            _dragRun[i].GlobalPosition = GlobalPosition + _followerOffsets[i];
        }
    }

    /// <summary>
    /// Sets up this card's identity, rendering and drag dependencies. Must be called before <c>AddChild()</c>.
    /// </summary>
    public void Configure(Suit suit, int rank, bool faceUp, Node2D dragLayer)
    {
        CardSuit = suit;
        Rank = rank;
        IsFaceUp = faceUp;
        _dragLayer = dragLayer;

        _sprite = GetNode<Sprite2D>("CardSprite");
        UpdateSpriteFrame();

        // Only pointer-picking is used on this Area2D, not physics overlap detection
        Monitoring = false;
        Monitorable = false;

        _state = CardState.idle;
    }

    /// <summary>
    /// Updates whether this card is showing its face, and its rendered sprite frame accordingly.
    /// </summary>
    public void SetFaceUp(bool faceUp)
    {
        IsFaceUp = faceUp;
        UpdateSpriteFrame();
    }

    // [21/08/2026] Simple sprite change, must be replaced with animation logic if desired in future passes
    private void UpdateSpriteFrame()
    {
        _sprite.FrameCoords = IsFaceUp ? new Vector2I(Rank - 1, (int)CardSuit) : _faceDownFrame;
    }

    // Logic to detect pickup attempts via built-in events
    private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        if (@event is not InputEventMouseButton mouseButton || mouseButton.ButtonIndex != MouseButton.Left) { return; }

        if (mouseButton.Pressed) { TryPickUp(); }
        else { Release(); }
    }

    // Check all conditions to validate an attempt to move this card
    private void TryPickUp()
    {
        if (_state != CardState.idle || !IsFaceUp || CurrentPile == null) { return; }

        IReadOnlyList<Card> run = CurrentPile.GetMovableRun(this);
        if (run.Count == 0) { return; }

        _dragRun = new List<Card>(run);
        _followerOffsets = new List<Vector2>();
        foreach (Card member in _dragRun)
        {
            _followerOffsets.Add(member == this ? Vector2.Zero : member.GlobalPosition - GlobalPosition);
        }
        _grabOffset = GetGlobalMousePosition() - GlobalPosition;

        // Reparent in run order so relative draw order (and thus click picking) is preserved
        foreach (Card member in _dragRun)
        {
            member.Reparent(_dragLayer, true);
        }

        _state = CardState.dragging;
    }

    // Handle where the card(s) should move after being dropped by the player
    private void Release()
    {
        if (_state != CardState.dragging) { return; }

        Pile source = CurrentPile!;
        Pile? target = ResolveDropTarget();

        if (target != null && target.CanAccept(_dragRun))
        {
            source.RemoveCards(_dragRun);
            target.AddCards(_dragRun);
        }
        else
        {
            foreach (Card member in _dragRun)
            {
                member.Reparent(source, false);
            }
            source.RepositionAll();
        }

        _dragRun.Clear();
        _followerOffsets.Clear();
        _state = CardState.idle;
    }

    // Locator method to find possible destination piles during "Release()"
    private Pile? ResolveDropTarget()
    {
        Vector2 dropPoint = GlobalPosition;
        List<Pile> candidates = new List<Pile>();
        foreach (Node node in GetTree().GetNodesInGroup("piles"))
        {
            if (node is not Pile pile || pile == CurrentPile) { continue; }
            if (pile.GetDropZoneGlobalRect().HasPoint(dropPoint)) { candidates.Add(pile); }
        }

        if (candidates.Count == 0) { return null; }
        if (candidates.Count == 1) { return candidates[0]; }

        // Multiple overlapping drop zones - favour whichever pile's origin is nearest this (the lead card's) position
        Pile nearest = candidates[0];
        float nearestDistance = (nearest.GlobalPosition - dropPoint).LengthSquared();
        for (int i = 1; i < candidates.Count; i++)
        {
            float distance = (candidates[i].GlobalPosition - dropPoint).LengthSquared();
            if (distance < nearestDistance)
            {
                nearest = candidates[i];
                nearestDistance = distance;
            }
        }
        return nearest;
    }
}