using Godot;

/// <summary>
/// Shared hover-cursor appearance for one or more <see cref="CursorHint"/> instances - edit this
/// resource once, every node referencing it updates together, instead of re-entering the same
/// texture/shape into separate identical node instances across multiple screens.
/// </summary>
[GlobalClass]
public partial class CursorHintStyle : Resource
{
    /// <summary>
    /// Custom cursor image shown on hover. Left unset, <see cref="ShapeFallback"/> is used instead.
    /// </summary>
    [Export] public Texture2D? Texture;

    [Export] public Vector2 Hotspot = Vector2.Zero;

    // Claims an otherwise-unused CursorShape slot so binding a Texture here doesn't reskin that shape
    // elsewhere in the project. Export, not hardcoded, so a future second style can pick its own slot.
    [Export] public Input.CursorShape Slot = Input.CursorShape.Help;

    /// <summary>
    /// Built-in shape shown while <see cref="Texture"/> is unset - a placeholder, superseded the
    /// moment a real cursor <see cref="Texture"/> exists.
    /// </summary>
    [Export] public Input.CursorShape ShapeFallback = Input.CursorShape.PointingHand;
}
