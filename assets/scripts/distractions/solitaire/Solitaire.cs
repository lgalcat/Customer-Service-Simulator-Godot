using Godot;
using System.Collections.Generic;

/// <summary>
/// Main script of the Solitaire (Klondike) minigame
/// </summary>
public partial class Solitaire : Distraction
{
    // Matches the current placeholder_background_410x240 background - keep in sync if that asset changes again
    private readonly float _viewportX = 410;
    /// <summary>
    /// Expected viewport width by the minigame.
    /// </summary>
    public override float ViewportX { get => _viewportX; }
    private readonly float _viewportY = 240;
    /// <summary>
    /// Expected viewport height by the minigame.
    /// </summary>
    public override float ViewportY { get => _viewportY; }

    /// <summary>
    /// Scene instanced once per card during dealing.
    /// </summary>
    [Export] private PackedScene _cardScene = null!;

    // How many foundations must simultaneously hold a King to win - a difficulty knob, fixed for now
    // [22/08/2026] TODO: should eventually come from Difficulty or per-deal data, once the alternate data-driven deal source exists
    [Export] private int _foundationsRequiredToWin = 4;

    private List<TableauPile> _tableaus = new();
    private List<FoundationPile> _foundations = new();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Must run after tree entry (GetViewport() needs it) - can't live in Setup(), which a future factory may call before AddChild()
        GetViewport().PhysicsObjectPickingSort = true;
        GetViewport().PhysicsObjectPickingFirstOnly = true;

        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);
    }

    /// <summary>
    /// Sets up the minigame instance before it enters the scene tree.
    /// </summary>
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;
        // Add any difficulty dependent location and instancing here

        Node2D dragLayer = GetNode<Node2D>("Stage/DragLayer");

        _tableaus.Clear();
        for (int i = 0; i < 7; i++)
        {
            _tableaus.Add(GetNode<TableauPile>($"Stage/TableauPiles/TableauPile{i}"));
        }

        _foundations.Clear();
        for (int i = 0; i < 4; i++)
        {
            FoundationPile foundation = GetNode<FoundationPile>($"Stage/FoundationPiles/Foundation{(Suit)i}");
            foundation.Filled += OnFoundationFilled;
            _foundations.Add(foundation);
        }

        StockPile stock = GetNode<StockPile>("Stage/StockPile");
        WastePile waste = GetNode<WastePile>("Stage/WastePile");
        stock.Waste = waste;

        // [22/08/2026] TODO: alternate data-driven deal source goes here once implemented, in place of/alongside DealBuilder
        DealBuilder.Deal(_cardScene, dragLayer, _tableaus, stock);
    }

    /// <summary>
    /// Invoked when the win condition has been met, notifies relevant systems upstream.
    /// </summary>
    public override void Victory()
    {
        foreach (FoundationPile foundation in _foundations)
        {
            foundation.Filled -= OnFoundationFilled;
        }

        GD.Print("Solitaire Completed!!!");
        OnVictory?.Invoke();
    }

    // Re-checks live foundation state on every fill rather than accumulating a counter, since foundation drag-off makes fills reversible
    private void OnFoundationFilled()
    {
        int filledCount = 0;
        foreach (FoundationPile foundation in _foundations)
        {
            if (foundation.TopCard != null && foundation.TopCard.Rank == 13) { filledCount++; }
        }
        if (filledCount >= _foundationsRequiredToWin) { Victory(); }
    }
}
