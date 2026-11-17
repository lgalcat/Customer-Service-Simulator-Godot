using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;

// Level-content invariants checked on every layout the real game can play: the shell's catalog entries plus its
// authored Stage, read from the scenes themselves, so a new layout is covered without editing this suite
// Each layout plays through the real shell (LayoutOverride + Setup), loaded in a live tree for its real _Ready()s
// Each case collects "<layout>: <reason>" for every failing layout and asserts the list is empty, so one broken
// layout doesn't hide the others. No frames are simulated
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallLayoutContentTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/throw_paper_ball/throw_paper_ball.tscn";
    // Design-invariant thresholds - not derived from code
    // Keep margins comfortably passable/winnable as scene/tuning evolve
    private const float MinimumFitRatio = 1.5f; // win area / body opening must be >= this times the ball's width
    private const float MinimumTimerMargin = 0.5f; // seconds the ball must stay alive past the win timer

    // Covers the layout files themselves: a root that is a ThrowPaperBallLayout, valid throw tuning and the Stage
    // children Setup() needs (no tree needed)
    [TestCase]
    public void EveryLayoutSetsUp()
    {
        var failures = new List<string>();
        foreach (PackedScene layout in PlayableLayouts())
        {
            ThrowPaperBall throwPaperBall = InstantiateShell(layout);
            try { throwPaperBall.Setup(1); }
            catch (Exception exception) { failures.Add($"{layout.ResourcePath}: {exception.Message}"); }
            finally { throwPaperBall.Free(); }
        }

        AssertThat(failures).IsEmpty();
    }

    // Reads both values off the components' real Timer children, so it tracks whatever ends up driving them
    // (a layout can override either export, e.g. the can's _winTime)
    [TestCase]
    public void BallOutlivesTheWinTimerWithMargin()
    {
        var failures = new List<string>();
        ForEachLoadedLayout((layout, stage) =>
        {
            double lifeTime = TimerOf(stage.GetNode("Ball")).WaitTime;
            double winTime = TimerOf(stage.GetNode("TrashCan")).WaitTime;
            if (lifeTime - winTime < MinimumTimerMargin) { failures.Add($"{layout}: ball lifetime {lifeTime} s leaves less than {MinimumTimerMargin} s past the win time {winTime} s"); }
        });

        AssertThat(failures).IsEmpty();
    }

    // Win area and body opening must fit the ball with margin, measured on the placed nodes (global scale), so a
    // layout that scales the can or the ball is checked as it plays
    [TestCase]
    public void CanFitsTheBallWithMargin()
    {
        var failures = new List<string>();
        ForEachLoadedLayout((layout, stage) =>
        {
            float required = BallDiameter(stage) * MinimumFitRatio;

            var winArea = stage.GetNode<CollisionPolygon2D>("TrashCan/TrashCanInside/TrashInsideArea");
            float winAreaWidth = NarrowestHorizontalSpan(winArea.Polygon) * Mathf.Abs(winArea.GlobalScale.X);
            if (winAreaWidth < required) { failures.Add($"{layout}: win area {winAreaWidth} px is narrower than {required} px"); }

            // Designer-placed markers flag the edges of the intended entry point
            Vector2 edgeA = stage.GetNode<Marker2D>("TrashCan/TrashCanBody/EdgeA").GlobalPosition;
            Vector2 edgeB = stage.GetNode<Marker2D>("TrashCan/TrashCanBody/EdgeB").GlobalPosition;
            float openingWidth = edgeA.DistanceTo(edgeB);
            if (openingWidth < required) { failures.Add($"{layout}: body opening {openingWidth} px is narrower than {required} px"); }
        });

        AssertThat(failures).IsEmpty();
    }

    // A movement the layout authors must actually run. Both failures only log, leaving the can static:
    // - a saved path NodePath that resolves to nothing (its node deleted or renamed) leaves _movementPath null, so the
    //   NodePath is read from the layout file itself;
    // - a path that resolves but is unusable (under 2 points, zero length), with a non-zero speed
    [TestCase]
    public void AuthoredMovementRuns()
    {
        var failures = new List<string>();
        foreach (PackedScene layout in PlayableLayouts())
        {
            NodePath? savedPath = SavedMovementPath(layout);
            ThrowPaperBall throwPaperBall = InstantiateShell(layout);
            try { throwPaperBall.Setup(1); }
            catch (Exception)
            {
                // EveryLayoutSetsUp reports it
                throwPaperBall.Free();
                continue;
            }
            using ISceneRunner runner = ISceneRunner.Load(throwPaperBall, true, true);
            TrashCan trashCan = throwPaperBall.GetNode<TrashCan>("Stage/TrashCan");

            Path2D? path = trashCan.Get("_movementPath").As<Path2D>();
            if (savedPath != null && path == null) { failures.Add($"{layout.ResourcePath}: movement path '{savedPath}' resolves to no Path2D"); }
            if (path != null && trashCan.Get("_movementSpeed").AsSingle() != 0 && !trashCan.IsPhysicsProcessing())
            {
                failures.Add($"{layout.ResourcePath}: the can has a movement path and speed but stays static");
            }
        }

        AssertThat(failures).IsEmpty();
    }

    // The roll checks every entry whatever the difficulty, so one pick checks the whole saved catalog: an empty slot,
    // an entry with no layout or an inverted band. PlayableLayouts() skips such entries and the behaviour suite clears
    // the catalog, so nothing else would notice. The catalog itself is optional (none means the authored Stage)
    [TestCase]
    public void SavedCatalogIsWellFormed()
    {
        ThrowPaperBall throwPaperBall = GD.Load<PackedScene>(ScenePath).Instantiate<ThrowPaperBall>();
        var catalog = throwPaperBall.Get("_layoutCatalog").As<ThrowPaperBallLayoutCatalog>();
        throwPaperBall.Free();
        if (catalog == null) { return; }

        string? failure = null;
        try { catalog.Pick(1); }
        catch (Exception exception) { failure = exception.Message; }

        AssertThat(failure).IsNull();
    }

    // LayoutOverride is for tests and playtesting only: saved in the shell, the real game would always play that layout
    [TestCase]
    public void SavedShellHasNoLayoutOverride()
    {
        ThrowPaperBall throwPaperBall = AutoFree(GD.Load<PackedScene>(ScenePath).Instantiate<ThrowPaperBall>())!;

        AssertThat(throwPaperBall.LayoutOverride)
            .OverrideFailureMessage($"throw_paper_ball.tscn is saved with LayoutOverride = '{throwPaperBall.LayoutOverride?.ResourcePath}'; clear it")
            .IsNull();
    }

    // The catalog's layouts plus the authored Stage's scene, each once, as the shell is saved
    private static List<PackedScene> PlayableLayouts()
    {
        ThrowPaperBall throwPaperBall = GD.Load<PackedScene>(ScenePath).Instantiate<ThrowPaperBall>();
        var paths = new List<string> { throwPaperBall.GetNode("Stage").SceneFilePath };
        var catalog = throwPaperBall.Get("_layoutCatalog").As<ThrowPaperBallLayoutCatalog>();
        throwPaperBall.Free();
        if (catalog != null)
        {
            paths.AddRange(catalog.Entries.Where(entry => entry?.Layout != null).Select(entry => entry.Layout!.ResourcePath));
        }
        return paths.Distinct().Select(path => GD.Load<PackedScene>(path)).ToList();
    }

    // The real shell forced to "layout" (overriding whatever the scene has saved)
    private static ThrowPaperBall InstantiateShell(PackedScene layout)
    {
        ThrowPaperBall throwPaperBall = GD.Load<PackedScene>(ScenePath).Instantiate<ThrowPaperBall>();
        throwPaperBall.LayoutOverride = layout;
        return throwPaperBall;
    }

    // Sets each playable layout up, loads it in a live tree and hands its Stage to "check"
    // A layout that fails to set up is skipped here: EveryLayoutSetsUp reports it
    private static void ForEachLoadedLayout(Action<string, Node> check)
    {
        foreach (PackedScene layout in PlayableLayouts())
        {
            ThrowPaperBall throwPaperBall = InstantiateShell(layout);
            try { throwPaperBall.Setup(1); }
            catch (Exception)
            {
                throwPaperBall.Free();
                continue;
            }
            using ISceneRunner runner = ISceneRunner.Load(throwPaperBall, true, true);
            check(layout.ResourcePath, throwPaperBall.GetNode("Stage"));
        }
    }

    // The NodePath the layout file saves for TrashCan's _movementPath (an exported Node is saved as a NodePath), or
    // null. Only reads the layout's own scene: fine while every layout inherits the base directly, whose can has none
    private static NodePath? SavedMovementPath(PackedScene layout)
    {
        SceneState state = layout.GetState();
        for (int node = 0; node < state.GetNodeCount(); node++)
        {
            if (state.GetNodeName(node) != "TrashCan") { continue; }
            for (int property = 0; property < state.GetNodePropertyCount(node); property++)
            {
                if (state.GetNodePropertyName(node, property) == "_movementPath") { return state.GetNodePropertyValue(node, property).AsNodePath(); }
            }
        }
        return null;
    }

    // Ball and TrashCan each build one unnamed Timer in _Ready()
    private static Timer TimerOf(Node node) => node.GetChildren().OfType<Timer>().First();

    // Assumes the ball's shape is a CircleShape2D
    private static float BallDiameter(Node stage)
    {
        var ballShape = stage.GetNode<CollisionShape2D>("Ball/BallShape");
        return ((CircleShape2D)ballShape.Shape).Radius * 2 * Mathf.Abs(ballShape.GlobalScale.X);
    }

    // Narrowest horizontal span of a polygon across its y levels
    // Assumes a convex polygon whose width is monotonic between vertex y-levels (true for simple shapes like
    // trapezoids); a concave shape would not be correctly analyzed by this
    private static float NarrowestHorizontalSpan(Vector2[] polygon)
    {
        return polygon
            .GroupBy(point => point.Y)
            .Select(levelPoints => levelPoints.Max(point => point.X) - levelPoints.Min(point => point.X))
            .Min();
    }
}
