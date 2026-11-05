using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Wave pacing for one round of <see cref="FlySpawner"/>, applied via <see cref="FlySpawner.ConfigureWaves"/>
/// <para>Periodic wave sizes stay a range: each wave rolls its own size when it spawns</para>
/// </summary>
public struct FlyWavePlan
{
    public int TotalFlies;
    public int FirstWaveSize;
    public int MinFliesPerWave;
    public int MaxFliesPerWave;
    public float WaveInterval;
    public int RefillThreshold;
}

/// <summary>
/// Handler script purpose built for spawning "Fly" instances in "FlySwatter"
/// <para>Handles wave pacing, per-instance attribute variance, and forwarding fly deaths upstream</para>
/// </summary>
public partial class FlySpawner : Node2D
{
    // Scene to instance for each fly (assign fly.tscn in the Inspector)
    [Export]
    private PackedScene _flyScene = null!;

    // Wave pacing; these authored values are the fallback, a FlySwatterLevel overwrites them via ConfigureWaves
    [Export]
    private int _firstWaveSize = 6;
    // Periodic waves roll their size in this range, per wave
    [Export]
    private int _minFliesPerWave = 3;
    [Export]
    private int _maxFliesPerWave = 3;
    [Export]
    private float _waveInterval = 4f;
    [Export]
    private int _maxTotalFlies = 20;
    // Early refill: when a death leaves fewer alive flies than this, the next wave comes at once (0 = off)
    [Export]
    private int _refillThreshold = 0;

    /// <summary>
    /// Total flies this spawner will spawn over the round.
    /// </summary>
    public int MaxTotalFlies { get => _maxTotalFlies; }

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
    // Set once no further waves may come (StopSpawning or the total cap), so an early refill can't restart them
    private bool _spawningStopped = false;
    // Alive flies, counted by the early refill; also kept for a future round-end cleanup of still-alive flies
    // ([02/09/2026] that behaviour is still undecided)
    private List<Fly> _activeFlies = new();

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        if (_flyScene == null) { throw new NullReferenceException("FlySpawner: _flyScene is not assigned (expected fly.tscn in the Inspector)"); }

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
        // A first wave that already used up the total cap has stopped spawning
        if (!_spawningStopped) { _waveTimer.Start(); }
    }

    // Spawns a periodic wave, its size rolled per wave within [_minFliesPerWave, _maxFliesPerWave]
    private void SpawnWave()
    {
        SpawnFlies(GD.RandRange(_minFliesPerWave, _maxFliesPerWave));
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

        if (_totalSpawned >= _maxTotalFlies) { StopSpawning(); }
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

    // Drops a fly from tracking, forwards its death upstream, then brings the next wave early if the stage
    // runs low. The refill check comes after the forward so a winning kill's StopSpawning() takes precedence
    private void OnFlyDied(Fly fly)
    {
        _activeFlies.Remove(fly);
        FlyDied?.Invoke();

        if (!_spawningStopped && _refillThreshold > 0 && _activeFlies.Count < _refillThreshold)
        {
            SpawnWave();
            // Restart the interval so a regular wave doesn't land right after the refill
            if (!_spawningStopped) { _waveTimer.Start(); }
        }
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
    /// Applies one round's wave pacing. Must be called before this node enters the scene tree, mirroring
    /// <see cref="Distraction.Setup"/>'s own "before entering the tree" contract.
    /// </summary>
    public void ConfigureWaves(FlyWavePlan plan)
    {
        _maxTotalFlies = plan.TotalFlies;
        _firstWaveSize = plan.FirstWaveSize;
        _minFliesPerWave = plan.MinFliesPerWave;
        _maxFliesPerWave = plan.MaxFliesPerWave;
        _waveInterval = plan.WaveInterval;
        _refillThreshold = plan.RefillThreshold;
    }

    /// <summary>
    /// Stops spawning further waves, including early refills.
    /// </summary>
    public void StopSpawning()
    {
        _spawningStopped = true;
        _waveTimer.Stop();
    }

}
