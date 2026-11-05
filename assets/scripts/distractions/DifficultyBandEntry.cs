using Godot;
using System;

/// <summary>
/// Base of a catalog entry rated for a band of difficulties; subclasses add the payload being picked
/// (a layout scene, a level preset, ...). Picking among entries is <see cref="DifficultyBands.PickNearest"/>
/// <para>Not a global class on purpose: a bare band carries nothing to pick, so it isn't offered as a resource</para>
/// </summary>
public partial class DifficultyBandEntry : Resource
{
    /// <summary>
    /// Lowest difficulty the entry is rated for
    /// </summary>
    [Export] public int MinDifficulty { get; set; } = 1;

    /// <summary>
    /// Highest difficulty the entry is rated for
    /// </summary>
    [Export] public int MaxDifficulty { get; set; } = 1;

    /// <summary>
    /// How many difficulty steps <paramref name="difficulty"/> lies outside this entry's band; 0 inside it
    /// </summary>
    public int DistanceTo(int difficulty)
    {
        if (difficulty < MinDifficulty) { return MinDifficulty - difficulty; }
        return Math.Max(0, difficulty - MaxDifficulty);
    }
}
