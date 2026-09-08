using Godot;
using System;

/// <summary>
/// Hosts, centers, and tears down the single active <see cref="Distraction"/> minigame, and
/// re-exposes its completion upstream.
/// <para>Deliberately not the thing that decides *when* to request a minigame - see
/// <see cref="RequestDistraction"/> and <see cref="IntervalDistractionScheduler"/> for the seam that
/// keeps that heuristic decoupled from instancing.</para>
/// </summary>
public partial class DistractionManager : Node
{
    // Default difficulty for a request that doesn't specify one
    [Export] private int _baseDifficulty = 1;
    // Full-rect Control every hosted minigame is parented under, so anchor-preset centering resolves
    // against the screen regardless of where DistractionManager itself sits in the tree
    [Export] private NodePath _overlayPath = "Overlay";

    /// <summary>
    /// Fired once whenever the active minigame is torn down after completion - the single event
    /// upstream game systems should subscribe to.
    /// </summary>
    public Action? DistractionCompleted;

    /// <summary>
    /// Optionally supply a pre-built <see cref="DistractionFactory"/> before this manager enters the
    /// tree - a filtered/weighted pool, a runtime-assembled set, or a test double. Left <see
    /// langword="null"/>, <see cref="_Ready"/> builds the default <c>res://</c>-backed factory. Read
    /// once in <see cref="_Ready"/>; changes afterwards have no effect.
    /// </summary>
    public DistractionFactory? InitialFactory { get; set; }

    private DistractionFactory _factory = null!;
    private Control _overlay = null!;
    private DistractionFactory.DistractionInstance? _active;

    /// <summary>
    /// Whether a minigame is currently hosted and being played.
    /// </summary>
    public bool IsDistractionActive => _active is not null;

    public override void _Ready()
    {
        // A caller may hand us a factory before tree entry (see InitialFactory) - a filtered/weighted
        // pool, a runtime-assembled set, or a test double. Otherwise fall back to the default res:// pool.
        // [4/09/2026] TODO: designer-facing weighted/filtered pools via a DistractionPool resource,
        // slotting into this chain as InitialFactory ?? _pool?.BuildFactory() ?? CreateDefault()
        _factory = InitialFactory ?? DistractionFactory.CreateDefault();

        _overlay = GetNode<Control>(_overlayPath);
        if (_overlay == null) { throw new NullReferenceException($"DistractionManager: missing required child '{_overlayPath}'"); }
    }

    /// <summary>
    /// Requests a minigame be created and hosted - the one "what" entry point, deliberately ignorant
    /// of the "when/why" behind the call. Returns <see langword="false"/> without effect if a
    /// minigame is already active, since only one may ever be active at once.
    /// </summary>
    public bool RequestDistraction(DistractionType? type = null, int? difficulty = null)
    {
        if (_active is not null) { return false; }

        DistractionFactory.DistractionInstance instance = _factory.Create(type ?? _factory.RandomType(), difficulty ?? _baseDifficulty);

        _overlay.AddChild(instance.Host);
        // Provided Controls don't have minimum size values, KeepSize is key to not collapsing window size
        instance.Host.SetAnchorsAndOffsetsPreset(Control.LayoutPreset.Center, Control.LayoutPresetMode.KeepSize);

        instance.Distraction.OnVictory = () => Complete(instance);
        _active = instance;
        return true;
    }

    // Consumes the minigame's OnVictory, tears its host down, and propagates completion upstream
    private void Complete(DistractionFactory.DistractionInstance instance)
    {
        instance.Distraction.OnVictory = null;
        instance.Host.QueueFree();
        _active = null;
        DistractionCompleted?.Invoke();
    }
}