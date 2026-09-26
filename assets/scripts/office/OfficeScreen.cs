using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Base script for every permanently-resident point-and-click screen hosted by an <see
/// cref="OfficeManager"/>. Screens are never instantiated or freed at runtime - only the current
/// one is <see cref="Activate"/>d; every other screen sits <see cref="Deactivate"/>d (invisible,
/// non-interactable) while its own logic (timers, animations, code accessibility) keeps running
/// underneath, by default - only a designer/programmer's explicit opt-out should change that.
/// <para>A screen does not deactivate itself: <see cref="OfficeManager"/> establishes the
/// "everything but the initial screen starts inactive" baseline for the screens it hosts. Run on
/// its own (F6 on the screen's scene), a screen therefore stays visible and interactive as
/// authored - nothing calls <see cref="IOfficeInteractable.SetInteractable"/>, so each interactable
/// keeps its own default.</para>
/// <para>A screen is also the seam for its own elements' navigation requests: an element inside the
/// screen's scene can call <see cref="RequestScreenChange"/> (typically wired in the editor from an
/// interactable's signal), and whoever hosts the screen subscribes to
/// <see cref="ScreenChangeRequested"/> - the screen never needs to know its host.</para>
/// </summary>
public partial class OfficeScreen : Node2D
{
    private readonly List<IOfficeInteractable> _interactables = new();

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
        foreach (IOfficeInteractable interactable in _interactables) { interactable.SetInteractable(true); }
        OnActivated();
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
