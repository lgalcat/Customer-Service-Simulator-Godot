using Godot;
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
    public DistractionFactory(IReadOnlyDictionary<DistractionType, PackedScene> scenes)
    {
        _scenes = scenes;
        _availableTypes = scenes.Keys.ToArray();
    }

    /// <summary>
    /// Builds the real, <c>res://</c>-backed factory.
    /// </summary>
    // Only maps minigames whose .tscn is ready to be hosted through this system - grows as each of
    // ThrowPaperBall/FlySwatter/Solitaire migrates onto it (see CLAUDE.local.md)
    // [04/09/2026] TODO: add ThrowPaperBall, FlySwatter, Solitaire once each migrates
    public static DistractionFactory CreateDefault()
    {
        var scenes = new Dictionary<DistractionType, PackedScene>
        {
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

    // Wraps a minigame with no host of its own in a SubViewport sized to its declared window, inside
    // a SubViewportContainer - covers clipping (content past the fixed render target is never
    // drawn), centering (the container is a Control), and scopes picking config per-minigame
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