using Godot;

/// <summary>
/// A clickable, hoverable Office element: an <c>Area2D</c> that reports pointer interaction as
/// Godot signals, so simple reactions are wired in the owning screen's <c>.tscn</c>.
/// Anything richer should implement bespoke code either through inheritance or a sibling script.
/// <para>Relies on the root viewport's physics picking, which is on by default via the
/// <c>physics/common/enable_object_picking</c> project setting; it would need enabling explicitly if
/// Office were ever hosted inside a <c>SubViewport</c>.</para>
/// </summary>
// Scene convention (not enforced here, this script touches none of it): the 'Area2D'
// root, a 'Sprite2D' child with the art, and a hitbox ('CollisionShape2D' or
// 'CollisionPolygon2D') child. The editor already warns about an 'Area2D' with no shape.
public partial class SpriteInteractable : Area2D, IOfficeInteractable
{
    /// <summary>
    /// Emitted on a left-button press over this element while it is interactable and visible.
    /// </summary>
    [Signal] public delegate void ClickedEventHandler();

    /// <summary>
    /// Emitted when the pointer enters this element while it is interactable and visible.
    /// </summary>
    [Signal] public delegate void HoverStartedEventHandler();

    /// <summary>
    /// Emitted when the pointer leaves this element, or when it stops being interactable while
    /// hovered.
    /// </summary>
    [Signal] public delegate void HoverEndedEventHandler();

    /// <summary>
    /// Emitted after <see cref="SetInteractable"/> turned interaction on.
    /// </summary>
    [Signal] public delegate void ActivatedEventHandler();

    /// <summary>
    /// Emitted after <see cref="SetInteractable"/> turned interaction off.
    /// </summary>
    [Signal] public delegate void DeactivatedEventHandler();

    // Starts true so a screen run on its own (nothing calls SetInteractable) still works
    private bool _interactable = true;
    private bool _isHovered;

    public override void _Ready()
    {
        // Only pointer-picking is used, not physics overlap detection
        Monitoring = false;
        Monitorable = false;

        InputEvent += OnInputEvent;
        MouseEntered += OnMouseEntered;
        MouseExited += OnMouseExited;
    }

    // Godot delivers enter/input events for every object hit in a picking pass, even ones a
    // subscriber deactivated earlier in that same pass - hence the state checks in each handler
    private void OnInputEvent(Node viewport, InputEvent @event, long shapeIdx)
    {
        // Fires at mouse-motion rate while hovering, so filter before doing anything else. Press-only
        // also keeps the matching release from clicking through onto a screen navigated to.
        if (@event is not InputEventMouseButton { ButtonIndex: MouseButton.Left, Pressed: true }) { return; }
        if (!_interactable || !IsVisibleInTree()) { return; }

        GD.Print(Name + " clicked!");
        EmitSignalClicked();
    }

    private void OnMouseEntered()
    {
        if (_isHovered || !_interactable || !IsVisibleInTree()) { return; }

        _isHovered = true;
        GD.Print(Name + " hovered by mouse");
        EmitSignalHoverStarted();
    }

    // Idempotent: the engine's exit for an element deactivated mid-hover arrives a physics tick or
    // more after SetInteractable already ended the hover itself
    private void OnMouseExited()
    {
        if (!_isHovered) { return; }

        _isHovered = false;
        GD.Print(Name + " no longer hovered");
        EmitSignalHoverEnded();
    }

    /// <summary>
    /// Turns pointer interaction on or off. Going inactive while hovered ends the hover immediately
    /// rather than waiting for the engine's deferred <c>mouse_exited</c>.
    /// </summary>
    public void SetInteractable(bool active)
    {
        _interactable = active;
        InputPickable = active;

        if (!active && _isHovered)
        {
            _isHovered = false;
            EmitSignalHoverEnded();
        }

        if (active) { EmitSignalActivated(); }
        else { EmitSignalDeactivated(); }
    }
}
