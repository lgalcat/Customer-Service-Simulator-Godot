using Godot;
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

/// <summary>
/// Base script for every permanently-resident point-and-click screen hosted by an <see
/// cref="OfficeManager"/>. Screens are never instantiated or freed at runtime - only the current
/// one is <see cref="Activate"/>d; every other screen sits <see cref="Deactivate"/>d (invisible,
/// non-interactable) while its own logic (timers, animations, code accessibility) keeps running
/// underneath, by default - only a designer/programmer's explicit opt-out should change that.
/// <para>This class never deactivates itself - <see cref="OfficeManager"/> owns the starting
/// baseline. An element inside the screen asks to navigate via <see cref="RequestScreenChange"/>,
/// which raises <see cref="ScreenChangeRequested"/> for the host to handle.</para>
/// <para>Outside systems reach into a screen only through what it declares in <see
/// cref="Fixtures"/> (resident nodes to bind to, and empty <see cref="Node2D"/> anchors to place
/// content at) and make content of their own part of the screen with <see cref="Place"/> /
/// <see cref="RegisterInteractable"/>. This class knows nothing about who those systems are.</para>
/// </summary>
public partial class OfficeScreen : Node2D
{
    private readonly List<IOfficeInteractable> _interactables = new();
    private readonly Dictionary<string, Node> _fixtureIndex = new();
    // Whether this screen's interactables participate in pointer interaction while it is active;
    // lets a host keep a screen visible but inert (see SetInputEnabled)
    private bool _inputEnabled = true;

    /// <summary>
    /// The nodes this screen deliberately exposes to outside systems, each identified by its node
    /// name: resident devices/interactables to bind to, and anchors to <see cref="Place"/> content
    /// at. Nothing is scanned or inferred - only listed nodes are reachable through <see
    /// cref="GetFixture{T}"/>. Declared in the screen's own scene (a public property rather than a
    /// private field so hand-built trees can set it before tree entry). Read once in <see
    /// cref="_Ready"/>.
    /// <para>An anchor is an empty <see cref="Node2D"/> authored at the draw-order position content
    /// should take among the screen's siblings (layering is tree order). It must stay at identity
    /// transform relative to the screen, as <c>ScreenLayer</c> does, so placed content keeps
    /// full-canvas coordinates - this is an authoring rule, not enforced.</para>
    /// </summary>
    [Export] public Godot.Collections.Array<Node> Fixtures { get; set; } = new();

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
        IndexFixtures();
        CollectInteractables(this, _interactables);
    }

    // Fails fast on a mis-authored list: every other symptom (a job binding to null, a duplicate
    // silently shadowing another) would surface far from the cause
    private void IndexFixtures()
    {
        for (int i = 0; i < Fixtures.Count; i++)
        {
            Node? fixture = Fixtures[i];
            if (fixture == null) { throw new InvalidOperationException($"OfficeScreen '{Name}': Fixtures entry {i} is unset"); }
            if (!IsAncestorOf(fixture)) { throw new InvalidOperationException($"OfficeScreen '{Name}': fixture '{fixture.Name}' (entry {i}) is not a descendant of the screen"); }
            if (!_fixtureIndex.TryAdd(fixture.Name.ToString(), fixture)) { throw new InvalidOperationException($"OfficeScreen '{Name}': duplicate fixture name '{fixture.Name}' (entry {i})"); }
        }
    }

    /// <summary>
    /// Returns the fixture named <paramref name="name"/> as <typeparamref name="T"/>. Only valid
    /// once this screen's <see cref="_Ready"/> has run.
    /// </summary>
    /// <exception cref="ArgumentException">No fixture has that name; the message lists the exposed ones.</exception>
    /// <exception cref="InvalidCastException">The fixture exists but is not a <typeparamref name="T"/>.</exception>
    public T GetFixture<T>(StringName name) where T : class
    {
        if (!_fixtureIndex.TryGetValue(name.ToString(), out Node? node))
        {
            throw new ArgumentException($"OfficeScreen '{Name}': no fixture named '{name}'. Exposed: {DescribeFixtures()}", nameof(name));
        }
        if (node is not T typed)
        {
            throw new InvalidCastException($"OfficeScreen '{Name}': fixture '{name}' is a {node.GetType().Name}, not a {typeof(T).Name}");
        }

        return typed;
    }

    /// <summary>
    /// Non-throwing <see cref="GetFixture{T}"/>: <see langword="false"/> if there is no such fixture
    /// or it is not a <typeparamref name="T"/>.
    /// </summary>
    public bool TryGetFixture<T>(StringName name, [NotNullWhen(true)] out T? fixture) where T : class
    {
        fixture = null;
        if (!_fixtureIndex.TryGetValue(name.ToString(), out Node? node)) { return false; }

        fixture = node as T;
        return fixture != null;
    }

    private string DescribeFixtures()
    {
        return _fixtureIndex.Count == 0
            ? "(none)"
            : string.Join(", ", _fixtureIndex.Select(pair => $"'{pair.Key}' ({pair.Value.GetType().Name})"));
    }

    /// <summary>
    /// Makes <paramref name="content"/> part of this screen under the anchor fixture named
    /// <paramref name="anchor"/>: parents it there (a node without a parent is added, one with a
    /// parent is reparented), registers every <see cref="IOfficeInteractable"/> it contains so
    /// <see cref="Activate"/>/<see cref="Deactivate"/>/<see cref="SetInputEnabled"/> reach it, and
    /// applies the screen's current interaction state to them.
    /// <para>Pass content that is not in the scene tree yet. Reparenting a node that already is
    /// fires its exit/enter notifications, and components that subscribe once in <c>_Ready</c> and
    /// unsubscribe in <c>_ExitTree</c> (e.g. <see cref="InteractableFeedback"/>) would lose their
    /// subscriptions.</para>
    /// </summary>
    /// <exception cref="ArgumentException">No fixture has that name.</exception>
    /// <exception cref="InvalidCastException">The fixture is not a <see cref="Node2D"/>.</exception>
    public void Place(Node2D content, StringName anchor)
    {
        Node2D target = GetFixture<Node2D>(anchor);

        if (content.GetParent() == null) { target.AddChild(content); }
        else
        {
            MakeOwnRoot(content);
            content.Reparent(target, false);
        }

        RegisterContent(content);
    }

    // A part authored as a child of another scene (e.g. a job's) is owned by that scene's root.
    // Moving it elsewhere would leave owner and ancestry inconsistent (the engine warns on every
    // such move), so the part becomes the owner of its own subtree instead - as if it had always
    // been a scene of its own. Nodes owned by something else (nested scene instances) are untouched.
    private static void MakeOwnRoot(Node content)
    {
        Node? oldOwner = content.Owner;
        if (oldOwner == null) { return; }

        content.Owner = null;
        ReassignOwner(content, oldOwner, content);
    }

    private static void ReassignOwner(Node node, Node from, Node to)
    {
        foreach (Node child in node.GetChildren())
        {
            if (child.Owner == from) { child.Owner = to; }
            ReassignOwner(child, from, to);
        }
    }

    /// <summary>
    /// Undoes <see cref="Place"/>: unregisters the content's interactables (each is switched off
    /// first, so a hover ends synchronously) and frees it. A no-op for content already freed.
    /// </summary>
    public void Remove(Node2D content)
    {
        if (!GodotObject.IsInstanceValid(content)) { return; }

        UnregisterContent(content);
        content.QueueFree();
    }

    /// <summary>
    /// Registers an interactable that entered the screen after <see cref="_Ready"/> (e.g. spawned
    /// by a job) so the screen's sweeps reach it, and applies the current interaction state to it.
    /// Safe to call repeatedly. Content added through <see cref="Place"/> is registered already;
    /// resident interactables are collected in <see cref="_Ready"/>.
    /// </summary>
    public void RegisterInteractable(IOfficeInteractable interactable)
    {
        if (_interactables.Contains(interactable)) { return; }

        _interactables.Add(interactable);
        interactable.SetInteractable(Visible && _inputEnabled);
    }

    /// <summary>
    /// Stops the screen sweeping <paramref name="interactable"/> and switches it off. Call before
    /// freeing an interactable that was registered; a freed one that was not is pruned lazily.
    /// </summary>
    public void UnregisterInteractable(IOfficeInteractable interactable)
    {
        if (!_interactables.Remove(interactable)) { return; }
        if (interactable is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject)) { return; }

        interactable.SetInteractable(false);
    }

    private void RegisterContent(Node2D content)
    {
        var found = new List<IOfficeInteractable>();
        if (content is IOfficeInteractable self) { found.Add(self); }
        CollectInteractables(content, found);

        foreach (IOfficeInteractable interactable in found) { RegisterInteractable(interactable); }
    }

    private void UnregisterContent(Node2D content)
    {
        var found = new List<IOfficeInteractable>();
        if (content is IOfficeInteractable self) { found.Add(self); }
        CollectInteractables(content, found);

        foreach (IOfficeInteractable interactable in found) { UnregisterInteractable(interactable); }
    }

    // Runs action over a snapshot of the interactables: switching one on or off emits signals whose
    // subscribers may register/unregister others mid-sweep. Entries whose node was freed without
    // being unregistered are dropped first rather than called into.
    private void ForEachInteractable(Action<IOfficeInteractable> action)
    {
        _interactables.RemoveAll(i => i is GodotObject godotObject && !GodotObject.IsInstanceValid(godotObject));

        foreach (IOfficeInteractable interactable in _interactables.ToArray()) { action(interactable); }
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
        ForEachInteractable(interactable => interactable.SetInteractable(_inputEnabled));
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

        ForEachInteractable(interactable => interactable.SetInteractable(enabled));
    }

    /// <summary>
    /// Hides this screen and stops every <see cref="IOfficeInteractable"/> descendant from
    /// participating in pointer interaction - their own internal logic keeps running regardless.
    /// Safe to call repeatedly.
    /// </summary>
    public void Deactivate()
    {
        Visible = false;
        ForEachInteractable(interactable => interactable.SetInteractable(false));
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
