using Godot;
using System;

public partial class ThrowPaperBall : Distraction
{
    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (Difficulty == null) { Difficulty = 1; }
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
