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

    // Child node reference found during "Setup"
    private Goal _goal = null!;

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

        // [29/08/2026] SubViewport/Window sizing from ViewportX/ViewportY still pending - left for a later stage

        _goal = GetNode<Goal>("Window/SubViewport/Stage/Goal");
        if (_goal == null) { throw new NullReferenceException(); }
        _goal.Reached += Victory;
    }

    // Invoked by Goal, notifies relevant systems upstream
    public override void Victory()
    {
        // Insert any additional victory animations and logic here
        // Guarded: Victory() must be safely callable even if Setup() never ran (matches
        // FlySwatter.Victory()'s equivalent _flySpawner null-check)
        if (_goal != null) { _goal.Reached -= Victory; }

        GD.Print("Platformer Completed!!!");
        OnVictory?.Invoke();
    }
}
