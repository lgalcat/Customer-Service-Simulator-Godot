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
    /// Expected width, in pixels, of the display window this minigame occupies when hosted.
    /// <para>For minigames whose content fits entirely within this window, it also describes total
    /// content size; a minigame whose level is larger than its window (e.g. a scrolling minigame)
    /// instead renders a window of this size onto that larger content.</para>
    /// </summary>
    // Integer, not float: the project's base resolution and assets are whole-pixel
    // throughout (window/stretch/scale_mode="integer"), and this is what SubViewport.Size expects
    public abstract int ViewportX { get; }

    /// <summary>
    /// Expected height, in pixels, of the display window this minigame occupies when hosted.
    /// <para>See <see cref="ViewportX"/> for the full contract.</para>
    /// </summary>
    public abstract int ViewportY { get; }

    /// <summary>
    /// <see cref="ViewportX"/>/<see cref="ViewportY"/> combined, in the same type a hosting
    /// <see cref="SubViewport"/>'s own <c>Size</c> expects.
    /// </summary>
    public Vector2I ViewportSize => new(ViewportX, ViewportY);

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
