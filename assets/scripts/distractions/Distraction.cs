using Godot;
using System;

/// <summary>
/// Base class from which all distraction minigames are derived
/// </summary>
// should contain abstract or virtual declarations of all common properties and methods for distractions
public abstract partial class Distraction : Node
{
    // Property that sets the local difficulty of the minigame
    public int Difficulty { get; protected set; }

    // Expected size of the viewport assigned to the minigame
    public abstract float ViewportX { get; }
    public abstract float ViewportY { get; }

    // Action to invoke during the "victory" method to notify the upstream manager
    // time of invocation left to decide in "victory" method implementation
    /// <summary>
    /// Invoked when the minigame is completed
    /// </summary>
    public Action? OnVictory;


    // Abstract method to invoke after instantiation but before insertion into the scene tree (EnterTree() and _Ready())
    // Should be used to communicate external dependencies and set up instance specific variance elements
    public abstract void Setup(int difficulty);

    // Abstract method to invoke when the win condition has been met
    // should invoke "OnVictory" at some point
    public abstract void Victory();

}
