using Godot;

/// <summary>
/// One level preset a <see cref="MinesweeperLevelCatalog"/> can pick, with the band of difficulties it's rated for
/// (inherited from <see cref="DifficultyBandEntry"/>). The band also drives the preset's mine count
/// </summary>
[GlobalClass]
public partial class MinesweeperLevelEntry : DifficultyBandEntry
{
    /// <summary>
    /// The level preset
    /// </summary>
    [Export] public MinesweeperLevel? Level { get; set; }

    /// <summary>
    /// The preset's mine count for <paramref name="difficulty"/> across this entry's band
    /// (see <see cref="MinesweeperLevel.MinesFor"/>); 0 without a level
    /// </summary>
    public int MinesFor(int difficulty) => Level?.MinesFor(difficulty, MinDifficulty, MaxDifficulty) ?? 0;
}
