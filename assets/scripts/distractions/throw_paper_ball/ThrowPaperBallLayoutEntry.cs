using Godot;
using System;

/// <summary>
/// One layout a <see cref="ThrowPaperBallLayoutCatalog"/> can pick, with the band of difficulties it's rated for
/// </summary>
[GlobalClass]
public partial class ThrowPaperBallLayoutEntry : Resource
{
    /// <summary>
    /// The layout scene, an inherited scene of <c>throw_paper_ball_layout_base.tscn</c>
    /// </summary>
    [Export] public PackedScene? Layout { get; set; }

    /// <summary>
    /// Lowest difficulty the layout is rated for
    /// </summary>
    [Export] public int MinDifficulty { get; set; } = 1;

    /// <summary>
    /// Highest difficulty the layout is rated for
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
