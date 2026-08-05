using Godot;
using System;

/// <summary>
/// Base class from which all distraction minigames are derived
/// </summary>
// should contain abstract or virtual declarations of all common properties and methods for distractions
public abstract partial class Distraction : Node
{
    /// <summary>
    /// The local difficulty of the minigame, set via <see cref="Setup"/>.
    /// </summary>
    public int Difficulty { get; protected set; }

    /// <summary>
    /// Expected viewport width by the minigame.
    /// </summary>
    public abstract float ViewportX { get; }

    /// <summary>
    /// Expected viewport height by the minigame.
    /// </summary>
    public abstract float ViewportY { get; }

    /// <summary>
    /// Invoked when the minigame is completed, during <see cref="Victory"/>
    /// </summary>
    // Time of invocation left to decide in "victory" method implementation
    public Action? OnVictory;


    /// <summary>
    /// Abstract factory method to invoke after instantiation but <b>before</b> insertion into the scene tree
    /// <para>Should be used to bind external dependencies and set up instance specific variance elements </para>
    /// </summary>
    public abstract void Setup(int difficulty);

    /// <summary>
    /// Abstract method to invoke when the win condition has been met
    /// <para>Invokes <see cref="OnVictory"/></para>
    /// </summary>
    // Exact moment of "OnVictory" invocation left to implementation
    public abstract void Victory();

}
