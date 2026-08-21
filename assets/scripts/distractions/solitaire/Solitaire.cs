using Godot;
using System;

/// <summary>
/// Main script of the Solitaire (Klondike) minigame
/// </summary>
public partial class Solitaire : Distraction
{
    // [21/08/2026] Background/card art is being reworked (wider stage, smaller card sprites) - keep this in sync with whichever background asset is actually wired in once that lands
    private readonly float _viewportX = 390;
    /// <summary>
    /// Expected viewport width by the minigame.
    /// </summary>
    public override float ViewportX { get => _viewportX; }
    private readonly float _viewportY = 240;
    /// <summary>
    /// Expected viewport height by the minigame.
    /// </summary>
    public override float ViewportY { get => _viewportY; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);
    }

    /// <summary>
    /// Sets up the minigame instance before it enters the scene tree.
    /// </summary>
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;

        // [21/08/2026] TODO: resolve pile nodes (tableau/foundation/stock/waste), configure Area2D input picking, instance and deal the deck
    }

    /// <summary>
    /// Invoked when the win condition has been met, notifies relevant systems upstream.
    /// </summary>
    public override void Victory()
    {
        // [21/08/2026] TODO: unsubscribe foundation-fill tracking once FoundationPile exists

        GD.Print("Solitaire Completed!!!");
        OnVictory?.Invoke();
    }
}
