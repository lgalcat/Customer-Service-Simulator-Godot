using Godot;

/// <summary>
/// One level preset a <see cref="FlySwatterLevelCatalog"/> can pick, with the band of difficulties it's rated for
/// (inherited from <see cref="DifficultyBandEntry"/>)
/// </summary>
[GlobalClass]
public partial class FlySwatterLevelEntry : DifficultyBandEntry
{
    /// <summary>
    /// The level preset
    /// </summary>
    [Export] public FlySwatterLevel? Level { get; set; }
}
