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

    private RigidBody2D _PaperBall;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed("JumpKey"))
        {
            // Implement start of throwing action here
        }
    }

    // Find all necessary "child" nodes and set the minigame up before start
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;
        _PaperBall = (RigidBody2D)FindChild("Ball");
        if (_PaperBall == null) { throw new NullReferenceException(); }
        else
        {
            _PaperBall.Freeze = true;
        }
        throw new NotImplementedException();
    }

    // Invoked by the PaperBin, freezes simulations and notifies relevant systems upstream
    public override void Victory()
    {
        throw new NotImplementedException();
    }

}
