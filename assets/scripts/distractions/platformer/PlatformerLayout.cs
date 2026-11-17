using Godot;
using System;

/// <summary>
/// Root of a Platformer layout scene (the minigame's <c>Stage</c>), which <see cref="Platformer"/> checks during its
/// Setup
/// <para>The layout's content (Ground tiles, the Player's spawn, the Goal, the camera limits) is the scene's own nodes.
/// Every layout is an inherited scene of <c>platformer_layout_base.tscn</c>. Holds no tuning yet; per-layout knobs
/// belong here</para>
/// </summary>
public partial class PlatformerLayout : Node2D
{
    /// <summary>
    /// Checks the layout is usable: Player/MainCamera's limits are ascending and enclose the Player's spawn and the Goal
    /// </summary>
    /// <exception cref="NullReferenceException">A required child is missing.</exception>
    /// <exception cref="InvalidOperationException">The layout is mis-authored.</exception>
    public void Validate()
    {
        // SceneFilePath is set on instantiation, so it names the offending layout even before tree entry
        string layout = string.IsNullOrEmpty(SceneFilePath) ? Name.ToString() : SceneFilePath;

        Node2D? player = GetNodeOrNull<Node2D>("Player");
        if (player == null) { throw new NullReferenceException($"PlatformerLayout '{layout}': missing required child 'Player'"); }
        Camera2D? camera = GetNodeOrNull<Camera2D>("Player/MainCamera");
        if (camera == null) { throw new NullReferenceException($"PlatformerLayout '{layout}': missing required child 'Player/MainCamera'"); }
        Node2D? goal = GetNodeOrNull<Node2D>("Goal");
        if (goal == null) { throw new NullReferenceException($"PlatformerLayout '{layout}': missing required child 'Goal'"); }

        if (camera.LimitLeft >= camera.LimitRight) { throw new InvalidOperationException($"PlatformerLayout '{layout}': camera LimitLeft {camera.LimitLeft} is not below LimitRight {camera.LimitRight}"); }
        if (camera.LimitTop >= camera.LimitBottom) { throw new InvalidOperationException($"PlatformerLayout '{layout}': camera LimitTop {camera.LimitTop} is not above LimitBottom {camera.LimitBottom}"); }

        // Player and Goal are Stage children, as the limits are in the Stage's space (the Stage sits at the origin)
        if (!WithinLimits(camera, player.Position)) { throw new InvalidOperationException($"PlatformerLayout '{layout}': the Player's spawn {player.Position} is outside the camera limits"); }
        if (!WithinLimits(camera, goal.Position)) { throw new InvalidOperationException($"PlatformerLayout '{layout}': the Goal {goal.Position} is outside the camera limits"); }
    }

    // Whether "point" lies inside the camera's limits, edges included
    private static bool WithinLimits(Camera2D camera, Vector2 point)
    {
        return point.X >= camera.LimitLeft && point.X <= camera.LimitRight
            && point.Y >= camera.LimitTop && point.Y <= camera.LimitBottom;
    }
}
