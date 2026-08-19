using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Handler script purpose built for spawning "Fly" instances in "FlySwatter"
/// <para>Handles wave pacing, per-instance attribute variance, and forwarding fly deaths upstream</para>
/// </summary>
public partial class FlySpawner : Node2D
{
    // Scene to instance for each fly (assign fly.tscn in the Inspector)
    [Export]
    private PackedScene _flyScene = null!;

    // Wave pacing
    [Export]
    private int _firstWaveSize = 6;
    [Export]
    private int _fliesPerWave = 3;
    [Export]
    private float _waveInterval = 4f;
    // [18/08/2026] Max flies is set to public for now, when a difficulty profile system is implemented privacy should be revised
    [Export]
    public int _maxTotalFlies = 20;

    // Corridor spawn points: playspace rect (Stage-local space, also propagated to each Fly as its own
    // movementBounds) and how deep the spawn band reaches inward from the edge
    [Export]
    private Rect2 _stageBounds = new Rect2(-110, -110, 220, 220);
    [Export]
    private float _corridorWidth = 30f;

    /// <summary>
    /// Invoked once per fly death, after it's been dropped from internal tracking.
    /// </summary>
    public Action? FlyDied;

    // Internal timer that paces waves
    private Timer _waveTimer = null!;
    private int _totalSpawned = 0;
    private List<Fly> _activeFlies = new();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (_flyScene == null) { throw new NullReferenceException(); }

        // Create and configure the wave timer
        _waveTimer = new Timer
        {
            OneShot = false,
            WaitTime = _waveInterval
        };
        AddChild(_waveTimer);
        _waveTimer.Timeout += SpawnWave;

        // Spawn the first wave immediately so the stage isn't empty at minigame start; independently
        // sized from periodic waves via _firstWaveSize, so starting conditions can be tuned on their own
        SpawnFlies(_firstWaveSize);
        _waveTimer.Start();
    }

    // Spawns a periodic wave (_fliesPerWave flies)
    private void SpawnWave()
    {
        SpawnFlies(_fliesPerWave);
    }

    // Spawns up to "count" flies, respecting the total-spawned cap, and stops the timer once it's reached
    private void SpawnFlies(int count)
    {
        int remaining = _maxTotalFlies - _totalSpawned;
        int actual = Mathf.Min(count, remaining);
        for (int i = 0; i < actual; i++)
        {
            SpawnFly();
        }

        if (_totalSpawned >= _maxTotalFlies) { _waveTimer.Stop(); }
    }

    // Instances, configures, and tracks a single fly
    private void SpawnFly()
    {
        Fly fly = _flyScene.Instantiate<Fly>();
        fly.Position = RollSpawnPosition();
        fly.Configure(BuildProfile(), _stageBounds);
        fly.Died += () => OnFlyDied(fly);
        AddChild(fly);

        _activeFlies.Add(fly);
        _totalSpawned++;
    }

    // Drops a fly from tracking and forwards its death upstream
    private void OnFlyDied(Fly fly)
    {
        _activeFlies.Remove(fly);
        FlyDied?.Invoke();
    }

    // [18/08/2026] Fixed placeholder profile; Difficulty-driven formula pending
    private FlyDifficultyProfile BuildProfile()
    {
        return new FlyDifficultyProfile
        {
            MinSpeed = 50,
            MaxSpeed = 120,
            MinStepDuration = 0.3f,
            MaxStepDuration = 0.8f,
            ArcChance = 0.8f,
            MinTurnRate = 60,
            MaxTurnRate = 300,
            MinHeadingDeviation = 0,
            MaxHeadingDeviation = 90,
            SpeedChangeChance = 0.8f
        };
    }

    // Picks a random point in the corridor band along one of the four edges of _stageBounds
    private Vector2 RollSpawnPosition()
    {
        int edge = (int)(GD.Randf() * 4f);
        float alongWidth = RandRange(0f, _stageBounds.Size.X);
        float alongHeight = RandRange(0f, _stageBounds.Size.Y);
        float depth = RandRange(0f, _corridorWidth);

        Vector2 min = _stageBounds.Position;
        Vector2 max = _stageBounds.End;

        return edge switch
        {
            0 => new Vector2(min.X + alongWidth, min.Y + depth),      // top edge, inward = +Y
            1 => new Vector2(max.X - depth, min.Y + alongHeight),     // right edge, inward = -X
            2 => new Vector2(min.X + alongWidth, max.Y - depth),      // bottom edge, inward = -Y
            _ => new Vector2(min.X + depth, min.Y + alongHeight),     // left edge, inward = +X
        };
    }

    // Wrapper for GD.RandRange's double signature to avoid repeated casts at call sites
    private static float RandRange(float min, float max)
    {
        return (float)GD.RandRange((double)min, (double)max);
    }

    /// <summary>
    /// Stops spawning further waves.
    /// </summary>
    public void StopSpawning()
    {
        _waveTimer.Stop();
    }

}
