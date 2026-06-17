using Godot;
using System;

// Base class from which all distraction minigames are derived
// should contain abstract or virtual declarations of all common properties and methods for distractions
public abstract partial class Distraction : Node
{
    // Property that sets the local difficulty of the minigame
    public int Difficulty { get; protected set; }

    // Expected size of the viewport assigned to the minigame
    public abstract float ViewportX { get; }
    public abstract float ViewportY { get; }

    // Signal to emit when the minigame ends
    // appropiate moment of emision left to implementation
    [Signal] public delegate void DistractionCompletedEventHandler();

    // Abstract method to invoke when the win condition has been met
    // should emit DistractionCompleted at some point
    public abstract void Victory();
}
