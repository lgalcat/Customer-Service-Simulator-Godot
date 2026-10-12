// OfficeScreen subclass that journals its protected hooks, to prove they run (and when, relative to
// the interactable sweep or to other screens) without modifying OfficeScreen itself.
// Entries are "OnActivated"/"OnDeactivated", or "<Label>.OnActivated" when a Label is set
public partial class HookProbeScreen : OfficeScreen
{
    public EventLog? Journal { get; set; }
    public string Label { get; set; } = "";

    private string Tag(string hook) => Label == "" ? hook : Label + "." + hook;

    protected override void OnActivated() => Journal?.Add(Tag("OnActivated"));

    protected override void OnDeactivated() => Journal?.Add(Tag("OnDeactivated"));
}
