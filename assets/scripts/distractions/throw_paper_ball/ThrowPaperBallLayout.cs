using Godot;
using System;

/// <summary>
/// Root of a Throw Paper Ball layout scene (the minigame's <c>Stage</c>): holds the layout's throw tuning,
/// which <see cref="ThrowPaperBall"/> reads during its Setup
/// <para>The layout's physical content (ball spawn, bin, movement paths, obstacles) is the scene's own nodes.
/// Every layout is an inherited scene of <c>throw_paper_ball_layout_base.tscn</c></para>
/// </summary>
public partial class ThrowPaperBallLayout : Node2D
{
    /// <summary>
    /// Lowest throw angle, in degrees. Angles grow CLOCKWISE from the +X axis, so upward throws are negative
    /// </summary>
    [ExportGroup("Throw")]
    [Export] public float MinThrowAngle { get; set; } = -60;

    /// <summary>
    /// Highest throw angle, in degrees; aiming starts here
    /// </summary>
    [Export] public float MaxThrowAngle { get; set; } = 0;

    /// <summary>
    /// Lowest throw impulse
    /// </summary>
    [Export] public float MinThrowStrength { get; set; } = 100;

    /// <summary>
    /// Highest throw impulse
    /// </summary>
    [Export] public float MaxThrowStrength { get; set; } = 500;

    /// <summary>
    /// Time (in seconds) aiming and charging each take to sweep between the ends of their range
    /// </summary>
    [Export] public float ThrowCycle { get; set; } = 1;

    /// <summary>
    /// Checks the throw tuning is usable: both ranges ascending and a positive cycle
    /// </summary>
    /// <exception cref="InvalidOperationException">The layout is mis-authored.</exception>
    public void Validate()
    {
        // SceneFilePath is set on instantiation, so it names the offending layout even before tree entry
        string layout = string.IsNullOrEmpty(SceneFilePath) ? Name.ToString() : SceneFilePath;
        if (MinThrowAngle >= MaxThrowAngle) { throw new InvalidOperationException($"ThrowPaperBallLayout '{layout}': MinThrowAngle {MinThrowAngle} is not below MaxThrowAngle {MaxThrowAngle}"); }
        if (MinThrowStrength >= MaxThrowStrength) { throw new InvalidOperationException($"ThrowPaperBallLayout '{layout}': MinThrowStrength {MinThrowStrength} is not below MaxThrowStrength {MaxThrowStrength}"); }
        if (ThrowCycle <= 0) { throw new InvalidOperationException($"ThrowPaperBallLayout '{layout}': ThrowCycle {ThrowCycle} must be positive"); }
    }
}
