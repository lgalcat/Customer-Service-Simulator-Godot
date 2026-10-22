using Godot;

/// <summary>
/// The Fill Document form's submit button, switched on and off by the owning <see
/// cref="OfficeScreen"/> like any other <see cref="IOfficeInteractable"/>. Clicking it takes focus
/// off the field being typed in.
/// </summary>
public partial class SubmitButton : Button, IOfficeInteractable
{
    // Starts true so the document works when its scene is run on its own
    private bool _interactable = true;
    private bool _locked;

    /// <summary>
    /// Keeps the button disabled regardless of <see cref="SetInteractable"/> (once the form is approved).
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
        ApplyState();
    }

    /// <summary>
    /// Turns pointer and keyboard interaction on or off. Going inactive releases keyboard focus.
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

        Disabled = !enabled;
        FocusMode = enabled ? FocusModeEnum.All : FocusModeEnum.None;
        MouseFilter = enabled ? MouseFilterEnum.Stop : MouseFilterEnum.Ignore;
    }
}
