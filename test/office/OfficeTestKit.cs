using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;

// Shared fixtures and drivers for the Office suites: the family's builders (interactables, screens,
// managers) and the ways to drive them
public static class OfficeTestKit
{
    // Polls for an outcome instead of counting frames. A runner is needed for the waits, so call it
    // with the suite's own live runner
    public static async Task<bool> WaitUntil(ISceneRunner runner, Func<bool> condition, int timeoutMs = 2000)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < timeoutMs)
        {
            if (condition()) { return true; }
            await runner.AwaitMillis(10);
        }
        return condition();
    }

    // Advances every Tween in the tree by 'seconds' of game time, synchronously, in 'step' slices (the list
    // is re-read each slice, so a tween started by another's callback runs too). Nothing else advances
    // them while the test awaits no frame, so what the tweens show is fully determined. 'afterEachStep'
    // observes in between
    public static void StepTweens(Node inTree, double seconds, Action? afterEachStep = null, double step = 0.02)
    {
        int steps = (int)Math.Ceiling(seconds / step - 1e-9);
        for (int i = 0; i < steps; i++)
        {
            foreach (Tween tween in inTree.GetTree().GetProcessedTweens())
            {
                if (tween.IsValid()) { tween.CustomStep(step); }
            }
            afterEachStep?.Invoke();
        }
    }

    // An IndicatorLight's state, read against its own levels so the tuning can change freely
    public static bool IsLit(IndicatorLight light) => light.SelfModulate.A > light.Get("_restLevel").AsSingle() + 1e-3f;

    public static bool AtRest(IndicatorLight light) => Mathf.IsEqualApprox(light.SelfModulate.A, light.Get("_restLevel").AsSingle());

    // Wrapper root so ISceneRunner.Load never has to dispose the node under test itself. The node must
    // be loaded into a live tree (ISceneRunner.Load(root, true, true)) before hover/click handlers
    // act: they check IsVisibleInTree()
    public static (Node2D Root, SpriteInteractable Interactable) BuildInteractable()
    {
        Node2D root = new();
        SpriteInteractable interactable = new();
        interactable.AddChild(new CollisionShape2D { Shape = new RectangleShape2D() });
        root.AddChild(interactable);
        return (root, interactable);
    }

    // A screen with the given children, not yet in a tree; call _Ready() manually to collect
    public static OfficeScreen BuildScreen(params Node[] children)
    {
        OfficeScreen screen = new();
        foreach (Node child in children) { screen.AddChild(child); }
        return screen;
    }

    // Manager > ScreenLayer > one HookProbeScreen per name (node name = Label, so the journal reads
    // "B.OnActivated"). 'initial' is set through the private [Export] before any _Ready(); null leaves
    // it unset. Not in a tree - pair with ReadyBottomUp
    public static OfficeManager BuildManager(EventLog journal, string? initial, params string[] screenNames)
    {
        OfficeManager manager = new();
        CanvasLayer layer = new() { Name = "ScreenLayer" };
        manager.AddChild(layer);
        foreach (string name in screenNames)
        {
            layer.AddChild(new HookProbeScreen { Name = name, Label = name, Journal = journal });
        }
        if (initial != null) { manager.Set("_initialScreenPath", new NodePath(initial)); }
        return manager;
    }

    // What the engine does on tree entry: every screen's _Ready() before the manager's. Done by hand
    // so the error-path tests can assert on the manager's _Ready() throwing
    public static void ReadyBottomUp(OfficeManager manager)
    {
        foreach (Node child in manager.GetNode("ScreenLayer").GetChildren()) { child._Ready(); }
        manager._Ready();
    }

    // Scene-level check: a hotspot needs at least one real hitbox, or the Area2D can never be picked
    public static bool HasUsableHitbox(Area2D area) =>
        area.GetChildren().Any(child => child is CollisionPolygon2D { Polygon.Length: >= 3 } or CollisionShape2D { Shape: not null });

    // True when 'source' has a connection on 'signal' whose callable targets 'target'.'method'. This is
    // how a scene-authored [connection] line is verified without firing it; a specific lookup,
    // never a count (counts are unreliable for C# signals)
    public static bool IsConnected(GodotObject source, StringName signal, GodotObject target, string method) =>
        source.GetSignalConnectionList(signal).Any(connection =>
        {
            Callable callable = connection["callable"].AsCallable();
            return ReferenceEquals(callable.Target, target) && callable.Method.ToString() == method;
        });

    // Every SpriteInteractable at any depth under 'root', in tree order
    public static IEnumerable<SpriteInteractable> InteractablesUnder(Node root)
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is SpriteInteractable interactable) { yield return interactable; }
            foreach (SpriteInteractable nested in InteractablesUnder(child)) { yield return nested; }
        }
    }

    private static InputEventMouseButton LeftPress() => new() { ButtonIndex = MouseButton.Left, Pressed = true };

    // Drive a SpriteInteractable through Godot's own built-in signals - no real input or physics
    public static void Press(SpriteInteractable interactable, InputEvent @event) =>
        interactable.EmitSignal(Area2D.SignalName.InputEvent, (Node)null!, @event, 0L);

    public static void Click(SpriteInteractable interactable) => Press(interactable, LeftPress());

    public static void PointerEnter(SpriteInteractable interactable) =>
        interactable.EmitSignal(Area2D.SignalName.MouseEntered);

    public static void PointerExit(SpriteInteractable interactable) =>
        interactable.EmitSignal(Area2D.SignalName.MouseExited);
}

// Synchronous, order-preserving record of what happened. Every Office emission happens inside the
// triggering call, so a plain list beats gdUnit4's AssertSignal wherever order, exact counts or
// absence matter
public sealed class EventLog
{
    private readonly List<string> _entries = new();

    public IReadOnlyList<string> Entries => _entries;

    public void Add(string entry) => _entries.Add(entry);

    public void Clear() => _entries.Clear();

    // Records each of the interactable's five signals; entries are "Clicked" or "label.Clicked"
    public void Watch(SpriteInteractable interactable, string? label = null)
    {
        string prefix = label == null ? "" : label + ".";
        interactable.Clicked += () => Add(prefix + "Clicked");
        interactable.HoverStarted += () => Add(prefix + "HoverStarted");
        interactable.HoverEnded += () => Add(prefix + "HoverEnded");
        interactable.Activated += () => Add(prefix + "Activated");
        interactable.Deactivated += () => Add(prefix + "Deactivated");
    }

    // Records each navigation request as "ScreenChange:<target>"
    // Appended after any existing subscriber, so a hosting OfficeManager's handler runs first
    public void Watch(OfficeScreen screen) =>
        screen.ScreenChangeRequested += target => Add("ScreenChange:" + target);

    // Same entries, but runs BEFORE existing subscribers. Needed when a hosted screen asks for a
    // bad target: the manager's handler throws, which aborts the rest of the invocation list, so a
    // recorder appended after it would never see the request
    public void WatchFirst(OfficeScreen screen)
    {
        Action<StringName>? existing = screen.ScreenChangeRequested;
        screen.ScreenChangeRequested = target => Add("ScreenChange:" + target);
        screen.ScreenChangeRequested += existing;
    }
}
