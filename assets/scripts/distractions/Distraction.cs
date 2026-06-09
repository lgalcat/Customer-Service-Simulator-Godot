using Godot;
using System;

public abstract partial class Distraction : Node
{
    // Common Attributes to all Distractions
    // Property that sets the local difficulty of the minigame
    protected  int Difficulty { get; set; }

    // Size of the viewport the minigame desires to get
    // each implementation should specify it internally
    protected float ViewportX { get; }
    protected float ViewportY { get; }

    [Signal] public delegate void DistractionCompletedEventHandler();

    // Abstract method to invoke when the win condition has been met
    // should emmit DistractionCompleted at some point
    public abstract void Victory();
}
