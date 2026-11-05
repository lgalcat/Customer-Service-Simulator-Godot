using Godot;

/// <summary>
/// One layout a <see cref="ThrowPaperBallLayoutCatalog"/> can pick, with the band of difficulties it's rated for
/// (inherited from <see cref="DifficultyBandEntry"/>)
/// </summary>
[GlobalClass]
public partial class ThrowPaperBallLayoutEntry : DifficultyBandEntry
{
    /// <summary>
    /// The layout scene, an inherited scene of <c>throw_paper_ball_layout_base.tscn</c>
    /// </summary>
    [Export] public PackedScene? Layout { get; set; }
}
