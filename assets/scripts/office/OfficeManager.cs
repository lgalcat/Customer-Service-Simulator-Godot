using Godot;
using System;

/// <summary>
/// Hosts every permanently-resident <see cref="OfficeScreen"/> and switches which one is active.
/// <para>Screens are never instantiated or freed at runtime - only one is ever <see
/// cref="OfficeScreen.Activate"/>d at a time; the rest sit <see cref="OfficeScreen.Deactivate"/>d
/// while their own logic keeps running underneath, per <see cref="OfficeScreen"/>'s contract.</para>
/// <para>Owns the starting baseline: on <see cref="_Ready"/> every hosted screen except the initial
/// one is deactivated. Screens added under the screen layer afterwards are not baselined.</para>
/// <para>Also subscribes to every hosted screen's <see cref="OfficeScreen.ScreenChangeRequested"/>, so
/// elements inside a screen ask their own screen to navigate and never need to find this manager.</para>
/// </summary>
public partial class OfficeManager : Node
{
    // Structural child every OfficeScreen (one instanced scene each) lives under; also establishes
    // Office's draw-order slot via its own Layer property (see office.tscn)
    [Export] private NodePath _screenLayerPath = "ScreenLayer";
    // Which ScreenLayer child (by name) starts active; left unset is tolerated - every screen then
    // starts inactive
    [Export] private NodePath _initialScreenPath = "";

    private CanvasLayer _screenLayer = null!;
    private bool _inputLocked;

    /// <summary>
    /// The currently active screen, or <see langword="null"/> if none has been activated yet.
    /// </summary>
    public OfficeScreen? CurrentScreen { get; private set; }

    /// <summary>
    /// While <see langword="true"/> the player cannot interact with Office: the current screen stays
    /// visible but every interactable is switched off (hover ends, idle feedback stops), and any
    /// screen navigated to meanwhile comes up the same way. Player-originated screen-change requests
    /// are ignored; a programmatic <see cref="GoToScreen(OfficeScreen)"/> still works. Whatever owns
    /// the reason (e.g. an active Distraction) sets it - Office knows nothing about why.
    /// </summary>
    public bool InputLocked
    {
        get => _inputLocked;
        set
        {
            if (_inputLocked == value) { return; }

            _inputLocked = value;
            CurrentScreen?.SetInputEnabled(!value);
        }
    }
    public override void _Ready()
    {
        _screenLayer = GetNode<CanvasLayer>(_screenLayerPath);
        if (_screenLayer == null) { throw new NullReferenceException($"OfficeManager: missing required child '{_screenLayerPath}'"); }

        OfficeScreen? initial = string.IsNullOrEmpty(_initialScreenPath.ToString())
            ? null
            : _screenLayer.GetNode<OfficeScreen>(_initialScreenPath);

        // Godot calls _Ready() bottom-up, so every screen has already collected its interactables by
        // now. A non-OfficeScreen child fails fast: a script-less screen root would otherwise stay
        // visible and interactive on top of whichever screen is active.
        foreach (Node child in _screenLayer.GetChildren())
        {
            if (child is not OfficeScreen screen) { throw new InvalidOperationException($"OfficeManager: '{child.Name}' under '{_screenLayerPath}' is not an OfficeScreen"); }

            // Screens (currently) never leave the tree, so this plain C# subscription needs no unsubscribing
            screen.ScreenChangeRequested += OnScreenChangeRequested;
            if (screen != initial) { screen.Deactivate(); }
        }

        if (initial != null) { GoToScreen(initial); }
    }

    // Player-originated navigation: dropped while input is locked (the interactables that raise it are
    // already switched off, this covers anything that slips through)
    private void OnScreenChangeRequested(StringName screenName)
    {
        if (_inputLocked) { return; }
        GoToScreen(screenName);
    }

    /// <summary>
    /// Switches the active screen by node name, resolved against <see cref="_screenLayerPath"/>'s
    /// direct children - Godot's own node-name resolution serves as the screen identifier, no
    /// separate ID registry needed. Throws <see cref="ArgumentException"/> if no screen has that name.
    /// </summary>
    public void GoToScreen(StringName screenName)
    {
        // GetNodeOrNull: a plain GetNode would log an engine error and hand back null, which then
        // fails with an unhelpful NullReferenceException further down
        OfficeScreen? target = _screenLayer.GetNodeOrNull<OfficeScreen>(screenName.ToString());
        if (target == null) { throw new ArgumentException($"OfficeManager: no screen named '{screenName}' under '{_screenLayerPath}'", nameof(screenName)); }

        GoToScreen(target);
    }

    /// <summary>
    /// Switches the active screen. No-op if <paramref name="target"/> is already <see
    /// cref="CurrentScreen"/>. Throws if <paramref name="target"/> isn't a direct child of the
    /// screen layer.
    /// </summary>
    public void GoToScreen(OfficeScreen target)
    {
        if (target.GetParent() != _screenLayer) { throw new ArgumentException($"OfficeManager: '{target.Name}' is not a registered screen", nameof(target)); }
        if (target == CurrentScreen) { return; }

        CurrentScreen?.Deactivate();
        // Set before Activate() so a screen reached while locked never briefly becomes interactable
        target.SetInputEnabled(!_inputLocked);
        target.Activate();
        CurrentScreen = target;
    }
}
