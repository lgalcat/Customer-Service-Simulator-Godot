using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Base script for every permanently-resident point-and-click screen hosted by an <see
/// cref="OfficeManager"/>. Screens are never instantiated or freed at runtime - only the current
/// one is <see cref="Activate"/>d; every other screen sits <see cref="Deactivate"/>d (invisible,
/// non-interactable) while its own logic (timers, animations, code accessibility) keeps running
/// underneath, by default - only a designer/programmer's explicit opt-out should change that.
/// <para>This class never deactivates itself - <see cref="OfficeManager"/> owns the starting
/// baseline. An element inside the screen asks to navigate via <see cref="RequestScreenChange"/>,
/// which raises <see cref="ScreenChangeRequested"/> for the host to handle.</para>
/// </summary>
public partial class OfficeScreen : Node2D
{
    private readonly List<IOfficeInteractable> _interactables = new();
    // Whether this screen's interactables participate in pointer interaction while it is active;
    // lets a host keep a screen visible but inert (see SetInputEnabled)
    private bool _inputEnabled = true;

    /// <summary>
    /// Raised with the target screen's name when something inside this screen asks to navigate
    /// elsewhere. <see cref="OfficeManager"/> subscribes to it for every screen it hosts.
    /// </summary>
    public Action<StringName>? ScreenChangeRequested;

    // Collection has to happen before OfficeManager's own _Ready() baselines the screens; Godot calls
    // _Ready() bottom-up, so every screen's _Ready() has already run by then.
    // Subclasses overriding _Ready() must call base._Ready().
    public override void _Ready()
    {
        CollectInteractables(this, _interactables);
    }

    // Hand-rolled instead of Node.FindChildren: FindChildren can't filter by a plain C# interface,
    // and its "owned" parameter defaults to requiring Node.Owner, which hand-built (e.g. test) trees
    // don't set - it would silently miss every interactable in a non-.tscn-instantiated tree.
    private static void CollectInteractables(Node node, List<IOfficeInteractable> into)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child is IOfficeInteractable interactable) { into.Add(interactable); }
            CollectInteractables(child, into);
        }
    }

    /// <summary>
    /// Asks the host to navigate to the screen named <paramref name="target"/>.
    /// Public and deliberately shaped for editor signal connections into C#.
    /// An empty target is an authoring mistake and throws (the engine's signal bridge
    /// logs it rather than crashing).
    /// </summary>
    public void RequestScreenChange(string target)
    {
        if (string.IsNullOrEmpty(target)) { throw new ArgumentException("OfficeScreen: RequestScreenChange needs a target screen name", nameof(target)); }
        // If any PRE-transition logic is designed implement it here (after safeguard)
        GD.Print(Name + ": Transition to " + target + " requested");

        ScreenChangeRequested?.Invoke(target);
    }

    /// <summary>
    /// Makes this screen visible and lets every <see cref="IOfficeInteractable"/> descendant
    /// participate in pointer interaction again. Safe to call repeatedly.
    /// </summary>
    public void Activate()
    {
        Visible = true;
        foreach (IOfficeInteractable interactable in _interactables) { interactable.SetInteractable(_inputEnabled); }
        OnActivated();
    }

    /// <summary>
    /// Sets whether this screen's <see cref="IOfficeInteractable"/> descendants participate in
    /// pointer interaction while it is active - the screen stays visible and its own logic keeps
    /// running either way. Remembered across <see cref="Activate"/>/<see cref="Deactivate"/>, which
    /// is what lets a host lock input before navigating to a screen. While the screen is inactive
    /// this only records the preference; <see cref="Activate"/> applies it.
    /// </summary>
    public void SetInputEnabled(bool enabled)
    {
        _inputEnabled = enabled;
        if (!Visible) { return; }

        foreach (IOfficeInteractable interactable in _interactables) { interactable.SetInteractable(enabled); }
    }

    /// <summary>
    /// Hides this screen and stops every <see cref="IOfficeInteractable"/> descendant from
    /// participating in pointer interaction - their own internal logic keeps running regardless.
    /// Safe to call repeatedly.
    /// </summary>
    public void Deactivate()
    {
        Visible = false;
        foreach (IOfficeInteractable interactable in _interactables) { interactable.SetInteractable(false); }
        OnDeactivated();
    }

    /// <summary>
    /// Hook for subclass-specific behavior once this screen has been made active (e.g. an intro
    /// animation). No-op by default.
    /// </summary>
    protected virtual void OnActivated() { }

    /// <summary>
    /// Hook for subclass-specific behavior once this screen has been made inactive. No-op by
    /// default.
    /// </summary>
    protected virtual void OnDeactivated() { }
}
