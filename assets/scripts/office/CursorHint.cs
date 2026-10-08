using Godot;

/// <summary>
/// Swaps the mouse cursor while whatever is wired to it is hovered, restoring the plain system
/// arrow when it isn't - a reusable, purely signal-driven counterpart to <see
/// cref="SpriteInteractable"/>'s <c>HoverStarted</c>/<c>HoverEnded</c> (connect them in the editor;
/// no subclassing or code wiring needed). Not specific to "exit"/navigation hotspots - any hoverable
/// element can be wired to one.
/// <para>A small per-screen node, not a singleton/autoload - configuration dedup across screens is
/// instead handled by <see cref="CursorHintStyle"/>, a shared resource every instance can point
/// at.</para>
/// </summary>
public partial class CursorHint : Node
{
    [Export] private CursorHintStyle? _style;

    public override void _Ready()
    {
        if (_style?.Texture != null) { Input.SetCustomMouseCursor(_style.Texture, _style.Slot, _style.Hotspot); }
    }

    // Public so it's a valid target for an editor signal [connection].
    public void OnHoverStarted()
    {
        if (_style?.Texture != null) { Input.SetDefaultCursorShape(_style.Slot); }
        else { Input.SetDefaultCursorShape(_style?.ShapeFallback ?? Input.CursorShape.PointingHand); }
    }

    public void OnHoverEnded()
    {
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }
}
