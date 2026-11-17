using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Main script of the Solitaire (Klondike) minigame
/// </summary>
public partial class Solitaire : Distraction
{
    // Matches the current placeholder_background_410x240 background - keep in sync if that asset changes again
    private readonly int _viewportX = 410;
    /// <summary>
    /// Expected viewport width by the minigame.
    /// </summary>
    public override int ViewportX { get => _viewportX; }
    private readonly int _viewportY = 240;
    /// <summary>
    /// Expected viewport height by the minigame.
    /// </summary>
    public override int ViewportY { get => _viewportY; }

    // Whether Setup() deals a randomized traditional deal when no BoardOverride is set
    // [18/11/2026] Authored boards (BoardOverride) are an interim exploration source; the final deal sourcing is
    // still undecided, so without either no cards are dealt
    [Export]
    private bool _randomizeDeal = false;

    /// <summary>
    /// Forces this authored board instead of the randomized deal; must be set before <see cref="Setup"/>
    /// <para>Meant for tests and playtesting: leave it empty in the saved scene, or the real game always plays it</para>
    /// </summary>
    [Export] public SolitaireBoard? BoardOverride { get; set; }

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
        // Viewport-scoped config for Card's per-object click picking. Must run post-tree-entry
        // (GetViewport() needs it) - can't live in Setup(), which the hosting factory calls before
        // AddChild(). When hosted, GetViewport() is the factory's dedicated SubViewport, so this now
        // scopes cleanly per Solitaire instance. PhysicsObjectPicking is set explicitly so picking
        // works whether hosted (the factory sets it too) or solitaire.tscn is run bare.
        GetViewport().PhysicsObjectPicking = true;
        GetViewport().PhysicsObjectPickingSort = true;
        GetViewport().PhysicsObjectPickingFirstOnly = true;
    }

    /// <summary>
    /// Sets up the minigame instance before it enters the scene tree.
    /// </summary>
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;
        // Add any difficulty dependent location and instancing here

        Node2D dragLayer = GetNode<Node2D>("Stage/DragLayer");
        if (dragLayer == null) { throw new NullReferenceException("Solitaire: missing required child 'Stage/DragLayer'"); }

        _tableaus.Clear();
        for (int i = 0; i < 7; i++)
        {
            string path = $"Stage/TableauPiles/TableauPile{i}";
            TableauPile tableau = GetNode<TableauPile>(path);
            if (tableau == null) { throw new NullReferenceException($"Solitaire: missing required child '{path}'"); }
            _tableaus.Add(tableau);
        }

        _foundations.Clear();
        for (int i = 0; i < 4; i++)
        {
            string path = $"Stage/FoundationPiles/Foundation{(Suit)i}";
            FoundationPile foundation = GetNode<FoundationPile>(path);
            if (foundation == null) { throw new NullReferenceException($"Solitaire: missing required child '{path}'"); }
            _foundations.Add(foundation);
        }

        StockPile stock = GetNode<StockPile>("Stage/StockPile");
        if (stock == null) { throw new NullReferenceException("Solitaire: missing required child 'Stage/StockPile'"); }
        WastePile waste = GetNode<WastePile>("Stage/WastePile");
        if (waste == null) { throw new NullReferenceException("Solitaire: missing required child 'Stage/WastePile'"); }
        stock.Waste = waste;

        // [18/11/2026] TODO: BoardOverride is an interim exploration source; replace or extend once the final
        // deal sourcing (e.g. "short" boards) is decided
        if (BoardOverride != null)
        {
            GD.Print($"Solitaire: BoardOverride forces '{BoardOverride.ResourcePath}'");
            DealBuilder.DealBoard(BoardOverride.Parse(), _cardScene, dragLayer, _tableaus, _foundations, stock, waste);

            // A pre-won board would otherwise sit there unwinnable (Filled only fires on a King being added)
            int filledCount = CountFilledFoundations();
            if (filledCount >= _foundationsRequiredToWin)
            {
                throw new InvalidOperationException($"Solitaire: board '{BoardOverride.ResourcePath}' is already won ({filledCount} foundations hold a King, {_foundationsRequiredToWin} required)");
            }
        }
        else if (_randomizeDeal) { DealBuilder.Deal(_cardScene, dragLayer, _tableaus, stock); }

        // Subscribed after dealing, so Kings a board places on foundations can't call Victory() from inside Setup()
        foreach (FoundationPile foundation in _foundations) { foundation.Filled += OnFoundationFilled; }
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
        if (CountFilledFoundations() >= _foundationsRequiredToWin) { Victory(); }
    }

    // How many foundations currently hold a King
    private int CountFilledFoundations()
    {
        int filledCount = 0;
        foreach (FoundationPile foundation in _foundations)
        {
            if (foundation.TopCard != null && foundation.TopCard.Rank == 13) { filledCount++; }
        }
        return filledCount;
    }
}
