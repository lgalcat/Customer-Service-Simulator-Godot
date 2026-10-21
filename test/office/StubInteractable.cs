using Godot;

// Minimal IOfficeInteractable that records how it was toggled, for OfficeScreenTesting, which cares
// about the sweep (who, in what order, with what value), not about pointer behaviour.
// Entries go to the shared journal as "<label>:SetInteractable(<value>)"
public partial class StubInteractable : Node, IOfficeInteractable
{
    public string Label { get; set; } = "";
    public EventLog? Journal { get; set; }
    public int CallCount { get; private set; }
    public bool? LastValue { get; private set; }
    // Runs after the call is recorded - lets a test act from inside a sweep (e.g. register or
    // unregister another interactable mid-sweep)
    public System.Action<bool>? OnToggled { get; set; }

    public void SetInteractable(bool active)
    {
        CallCount++;
        LastValue = active;
        Journal?.Add(Label + ":SetInteractable(" + (active ? "true" : "false") + ")");
        OnToggled?.Invoke(active);
    }
}
