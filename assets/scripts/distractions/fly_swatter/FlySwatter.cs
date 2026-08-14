using Godot;
using System;

/// <summary>
/// Main script of the Fly Swatter minigame
/// </summary>
public partial class FlySwatter : Distraction
{
    // Expected screenspace for the minigame, matches the placeholder background texture
    private readonly float _viewportX = 220;
    /// <summary>
    /// Expected viewport width by the minigame.
    /// </summary>
    public override float ViewportX { get => _viewportX; }
    private readonly float _viewportY = 220;
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

        // [14/08/2026] Implementation of Fly/Swatter/spawner lookups and event wiring pending
    }

    /// <summary>
    /// Invoked when the win condition has been met, notifies relevant systems upstream.
    /// </summary>
    public override void Victory()
    {
        // Insert any additional victory animations and logic here

        GD.Print("FlySwatter Completed!!!");
        OnVictory?.Invoke();
    }
}