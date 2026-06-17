using Godot;
using System;

// Main script of the Throw Paper Ball minigame
public partial class ThrowPaperBall : Distraction
{
    // Expected screenspace for the minigame
    private readonly float _viewportX = 100;
    public override float ViewportX { get => _viewportX; }
    private readonly float _viewportY = 100;
    public override float ViewportY { get => _viewportY; }


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (Difficulty == 0) { Difficulty = 1; }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
    }

    // Invoked by the PaperBin, freezes simulations and notifies relevant systems upstream
    public override void Victory()
    {
        throw new NotImplementedException();
    }
}
