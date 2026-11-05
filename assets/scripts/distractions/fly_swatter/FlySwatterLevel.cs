using Godot;
using System;

/// <summary>
/// One round's concrete numbers, rolled from a <see cref="FlySwatterLevel"/>
/// </summary>
public struct FlySwatterLevelPlan
{
    public int WinScore;
    public FlyWavePlan Waves;
}

/// <summary>
/// A Fly Swatter level preset: the ranges a round's win threshold and wave pacing are rolled from, once per
/// spawn, during <see cref="FlySwatter.Setup"/>
/// <para>The total of flies is the win score plus a spare count, so a rolled round is always winnable.
/// Periodic wave sizes stay a range the spawner rolls per wave</para>
/// </summary>
[GlobalClass]
public partial class FlySwatterLevel : Resource
{
    /// <summary>
    /// Fewest flies the player must smack to win
    /// </summary>
    [ExportGroup("Score")]
    [Export] public int MinWinScore { get; set; } = 15;

    /// <summary>
    /// Most flies the player must smack to win
    /// </summary>
    [Export] public int MaxWinScore { get; set; } = 15;

    /// <summary>
    /// Fewest flies spawned on top of the win score (0 means every fly must be smacked)
    /// </summary>
    [Export] public int MinSpareFlies { get; set; } = 5;

    /// <summary>
    /// Most flies spawned on top of the win score
    /// </summary>
    [Export] public int MaxSpareFlies { get; set; } = 5;

    /// <summary>
    /// Smallest first wave, spawned as the round starts
    /// </summary>
    [ExportGroup("Waves")]
    [Export] public int MinFirstWaveSize { get; set; } = 6;

    /// <summary>
    /// Largest first wave
    /// </summary>
    [Export] public int MaxFirstWaveSize { get; set; } = 6;

    /// <summary>
    /// Smallest periodic wave; each wave rolls its own size
    /// </summary>
    [Export] public int MinFliesPerWave { get; set; } = 3;

    /// <summary>
    /// Largest periodic wave
    /// </summary>
    [Export] public int MaxFliesPerWave { get; set; } = 3;

    /// <summary>
    /// Shortest time between periodic waves, in seconds
    /// </summary>
    [Export] public float MinWaveInterval { get; set; } = 4;

    /// <summary>
    /// Longest time between periodic waves, in seconds
    /// </summary>
    [Export] public float MaxWaveInterval { get; set; } = 4;

    /// <summary>
    /// When a death leaves fewer alive flies than this, the next wave comes at once; 0 turns it off
    /// </summary>
    [Export] public int RefillThreshold { get; set; } = 0;

    /// <summary>
    /// Checks the preset is usable: every range ascending, at least one fly to smack, no negative spare,
    /// non-empty waves, a positive interval and a non-negative refill threshold
    /// </summary>
    /// <exception cref="InvalidOperationException">The preset is mis-authored.</exception>
    public void Validate()
    {
        string level = string.IsNullOrEmpty(ResourcePath) ? ResourceName : ResourcePath;
        if (MinWinScore < 1) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinWinScore {MinWinScore} must be at least 1"); }
        if (MinWinScore > MaxWinScore) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinWinScore {MinWinScore} is above MaxWinScore {MaxWinScore}"); }
        if (MinSpareFlies < 0) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinSpareFlies {MinSpareFlies} must not be negative"); }
        if (MinSpareFlies > MaxSpareFlies) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinSpareFlies {MinSpareFlies} is above MaxSpareFlies {MaxSpareFlies}"); }
        if (MinFirstWaveSize < 1) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinFirstWaveSize {MinFirstWaveSize} must be at least 1"); }
        if (MinFirstWaveSize > MaxFirstWaveSize) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinFirstWaveSize {MinFirstWaveSize} is above MaxFirstWaveSize {MaxFirstWaveSize}"); }
        if (MinFliesPerWave < 1) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinFliesPerWave {MinFliesPerWave} must be at least 1"); }
        if (MinFliesPerWave > MaxFliesPerWave) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinFliesPerWave {MinFliesPerWave} is above MaxFliesPerWave {MaxFliesPerWave}"); }
        if (MinWaveInterval <= 0) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinWaveInterval {MinWaveInterval} must be positive"); }
        if (MinWaveInterval > MaxWaveInterval) { throw new InvalidOperationException($"FlySwatterLevel '{level}': MinWaveInterval {MinWaveInterval} is above MaxWaveInterval {MaxWaveInterval}"); }
        if (RefillThreshold < 0) { throw new InvalidOperationException($"FlySwatterLevel '{level}': RefillThreshold {RefillThreshold} must not be negative"); }
    }

    /// <summary>
    /// Rolls one round's numbers within the preset's ranges; call <see cref="Validate"/> first
    /// </summary>
    public FlySwatterLevelPlan Roll()
    {
        int winScore = GD.RandRange(MinWinScore, MaxWinScore);
        int spare = GD.RandRange(MinSpareFlies, MaxSpareFlies);
        return new FlySwatterLevelPlan
        {
            WinScore = winScore,
            Waves = new FlyWavePlan
            {
                TotalFlies = winScore + spare,
                FirstWaveSize = GD.RandRange(MinFirstWaveSize, MaxFirstWaveSize),
                MinFliesPerWave = MinFliesPerWave,
                MaxFliesPerWave = MaxFliesPerWave,
                WaveInterval = (float)GD.RandRange((double)MinWaveInterval, (double)MaxWaveInterval),
                RefillThreshold = RefillThreshold
            }
        };
    }
}
