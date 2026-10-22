using Godot;

/// <summary>
/// A blank on a Fill Document form: a text field the player types into, which the owning <see
/// cref="OfficeScreen"/> switches on and off like any other <see cref="IOfficeInteractable"/>.
/// <para>Switching it off also gives up keyboard focus. The Distraction overlay only blocks the
/// mouse; a field that kept focus would go on receiving keystrokes meant for a keyboard-driven
/// minigame.</para>
/// </summary>
public partial class DocumentInput : LineEdit, IOfficeInteractable
{
    // Starts true so the document works when its scene is run on its own
    private bool _interactable = true;
    private bool _locked;

    /// <summary>
    /// Keeps the field read-only regardless of <see cref="SetInteractable"/> - an approved form stays
    /// closed even if the screen's input is switched back on before it is taken away.
    /// </summary>
    public bool Locked
    {
        get => _locked;
        set
        {
            _locked = value;
            ApplyState();
        }
    }

    public override void _Ready()
    {
        ContextMenuEnabled = false;
        ApplyState();
    }

    /// <summary>
    /// Turns typing and pointer interaction on or off. Going inactive releases keyboard focus.
    /// </summary>
    public void SetInteractable(bool active)
    {
        _interactable = active;
        ApplyState();
    }

    private void ApplyState()
    {
        bool enabled = _interactable && !_locked;
        if (!enabled && HasFocus()) { ReleaseFocus(); }

        Editable = enabled;
        FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }
}
