using Godot;
using System;

/// <summary>
/// Main script of the Platformer minigame
/// </summary>
public partial class Platformer : Distraction
{
    // Expected display window for the minigame - smaller than the level itself,
    // which is revealed through it via a scrolling Camera2D (see platformer.tscn once built)
    private readonly float _viewportX = 240;
    public override float ViewportX { get => _viewportX; }
    private readonly float _viewportY = 240;
    public override float ViewportY { get => _viewportY; }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    // Find all necessary "child" nodes and set the minigame up before start
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;

        // Implement location and instancing of difficulty dependent elements here

        // Node lookups/event wiring (Player, Goal, SubViewport sizing) land here once
        // those pieces of the scene/scripts exist - deliberately left for a later stage
    }

    // Invoked by Goal, notifies relevant systems upstream
    public override void Victory()
    {
        // Insert any additional victory animations and logic here

        GD.Print("Platformer Completed!!!");
        OnVictory?.Invoke();
    }
}
