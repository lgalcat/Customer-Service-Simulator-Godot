using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Locates, instances, configures, and hosts <see cref="Distraction"/> minigames on request.
/// <para>Owns "how a minigame comes to exist" - <see cref="DistractionManager"/> owns "when/why".</para>
/// </summary>
public class DistractionFactory
{
    /// <summary>
    /// A minigame paired with the display <see cref="Control"/> <see cref="Create"/> built to host
    /// it under - the same node is both what a caller adds to the scene tree/frees and what it
    /// resizes/positions, since the minigame always sits nested inside it (never the reverse).
    /// </summary>
    public readonly record struct DistractionInstance(Control Host, Distraction Distraction);

    private readonly IReadOnlyDictionary<DistractionType, PackedScene> _scenes;
    private readonly DistractionType[] _availableTypes;

    /// <summary>
    /// Builds a factory over an explicit type-to-scene map - the injectable seam tests use.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="scenes"/> is empty - a factory with no
    /// configured minigames can never create anything, so this is rejected at construction rather
    /// than deferred to a later, harder-to-trace <see cref="RandomType"/>/<see cref="Create"/> failure.</exception>
    // [8/09/2026] Later on this should become the main builder method (see 'CreateDefault()' comments)
    public DistractionFactory(IReadOnlyDictionary<DistractionType, PackedScene> scenes)
    {
        if (scenes.Count == 0)
        {
            throw new ArgumentException("DistractionFactory: scenes must not be empty", nameof(scenes));
        }

        _scenes = scenes;
        _availableTypes = scenes.Keys.ToArray();
    }

    /// <summary>
    /// Builds the real, <c>res://</c>-backed factory over every <see cref="Distraction"/> minigame.
    /// </summary>
    // [8/09/2026] In the future this should act as a fallback, a primary way of injecting richer
    // (weighted/filtered) minigame pools should be designed and implemented to enable designer flexibility
    public static DistractionFactory CreateDefault()
    {
        var scenes = new Dictionary<DistractionType, PackedScene>
        {
            { DistractionType.ThrowPaperBall, GD.Load<PackedScene>("res://assets/scenes/distractions/throw_paper_ball/throw_paper_ball.tscn") },
            { DistractionType.FlySwatter, GD.Load<PackedScene>("res://assets/scenes/distractions/fly_swatter/fly_swatter.tscn") },
            { DistractionType.Solitaire, GD.Load<PackedScene>("res://assets/scenes/distractions/solitaire/solitaire.tscn") },
            { DistractionType.Platformer, GD.Load<PackedScene>("res://assets/scenes/distractions/platformer/platformer.tscn") },
        };
        return new DistractionFactory(scenes);
    }

    /// <summary>
    /// Instances and configures the requested minigame, returning it already paired with a display
    /// host ready to be added to the scene tree and positioned.
    /// </summary>
    public DistractionInstance Create(DistractionType type, int difficulty)
    {
        if (!_scenes.TryGetValue(type, out PackedScene? scene))
        {
            throw new KeyNotFoundException($"DistractionFactory: no scene mapped for '{type}'");
        }

        Distraction distraction = scene.Instantiate<Distraction>();
        // Before any tree entry, per the Distraction.Setup() contract
        distraction.Setup(difficulty);

        return new DistractionInstance(BuildHost(distraction), distraction);
    }

    /// <summary>
    /// Picks uniformly among every minigame this factory can currently instance.
    /// </summary>
    public DistractionType RandomType()
    {
        return _availableTypes[GD.RandRange(0, _availableTypes.Length - 1)];
    }

    // Builds the display host: a SubViewport sized to the minigame's declared window inside a
    // SubViewportContainer - clips overflow, gives the manager a Control to position, and scopes
    // picking config per-minigame.
    //
    // [07/09/2026] Intended evolution: once frame flair lands (border, title, entry/exit animation)
    // this host becomes a dedicated frame "distraction_frame.tscn" + "DistractionFrame" thin script.
    // Ownership to keep: the frame owns its own structure, render config, animations and other components
    // the factory instances the frame and mounts the minigame into it
    // the manager only parents/positions it and drives show/hide timing.
    // Keep DistractionInstance.Host typed as Control so this swap never ripples past factory.
    private static SubViewportContainer BuildHost(Distraction distraction)
    {
        var viewport = new SubViewport
        {
            Size = distraction.ViewportSize,
            HandleInputLocally = false,
            RenderTargetUpdateMode = SubViewport.UpdateMode.Always,
            Disable3D = true,
            TransparentBg = true,
            PhysicsObjectPicking = true,
            // A SubViewport's own filter defaults to Linear regardless of project-wide settings
            // left alone this blurs every pixel-art minigame hosted here
            CanvasItemDefaultTextureFilter = Viewport.DefaultCanvasItemTextureFilter.Nearest,
        };

        var container = new SubViewportContainer
        {
            Size = distraction.ViewportSize,
            Stretch = true
        };
        container.AddChild(viewport);
        viewport.AddChild(distraction);

        return container;
    }
}