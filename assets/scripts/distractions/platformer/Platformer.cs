using Godot;
using System;

/// <summary>
/// Main script of the Platformer minigame
/// </summary>
public partial class Platformer : Distraction
{
    // Expected display window for the minigame - smaller than the level itself,
    // which is revealed through it via a scrolling Camera2D (see platformer.tscn once built)
    private readonly int _viewportX = 240;
    public override int ViewportX { get => _viewportX; }
    private readonly int _viewportY = 240;
    public override int ViewportY { get => _viewportY; }

    // Child node reference found during "Setup"
    private Goal _goal = null!;

    // Find all necessary "child" nodes and set the minigame up before start
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;

        // Implement location and instancing of difficulty dependent elements here

        // Hosting (SubViewport sizing/tuning, centering) is DistractionFactory/DistractionManager's
        // job - Stage renders into whatever SubViewport ends up hosting this minigame, sized to
        // ViewportX/Y regardless of who built it (see project_distraction_infrastructure memory)

        _goal = GetNode<Goal>("Stage/Goal");
        if (_goal == null) { throw new NullReferenceException("Platformer: missing required child 'Stage/Goal'"); }
        _goal.Reached += Victory;
    }

    // Invoked by Goal, notifies relevant systems upstream
    public override void Victory()
    {
        // Insert any additional victory animations and logic here
        // Guarded: Victory() must be safely callable even if Setup() never ran (matches
        // FlySwatter.Victory()'s equivalent _flySpawner null-check)
        if (_goal != null) { _goal.Reached -= Victory; }

        GD.Print("Platformer Completed!!!");
        OnVictory?.Invoke();
    }
}
