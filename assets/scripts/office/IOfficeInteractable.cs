/// <summary>
/// Contract for a screen-level interactive element's participation in pointer interaction.
/// <para>Implemented by whichever Godot node type fits the element (typically an <c>Area2D</c>,
/// per this project's established click-picking convention) - any implementation should
/// guarantee complete and reliable toggling of all its input consuming components </para>
/// </summary>
public interface IOfficeInteractable
{
    /// <summary>
    /// Called by the owning <see cref="OfficeScreen"/> on <see cref="OfficeScreen.Activate"/>/
    /// <see cref="OfficeScreen.Deactivate"/>. Governs <b>only</b> whether this element currently
    /// participates in pointer interaction (typically by toggling its own <c>InputPickable</c>/
    /// <c>Monitoring</c>) - it must not pause the element's own internal logic, timers, or
    /// animations by default; that only happens if a designer/programmer explicitly decides so.
    /// </summary>
    void SetInteractable(bool active);
}
