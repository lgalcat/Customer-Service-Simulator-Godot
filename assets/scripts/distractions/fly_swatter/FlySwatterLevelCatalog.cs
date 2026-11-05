using Godot;
using System;

/// <summary>
/// The level presets <see cref="FlySwatter"/> rolls from during its Setup, each rated with a difficulty band
/// <para>Shared as a <c>.tres</c>; the roll happens per spawn, nothing is decided in advance</para>
/// </summary>
[GlobalClass]
public partial class FlySwatterLevelCatalog : Resource
{
    /// <summary>
    /// The level presets to pick from
    /// </summary>
    [Export] public Godot.Collections.Array<FlySwatterLevelEntry> Entries { get; set; } = new();

    /// <summary>
    /// Rolls a level for <paramref name="difficulty"/>: a random entry among those whose band contains it or,
    /// when none does, among those whose band lies nearest (see <see cref="DifficultyBands.PickNearest"/>).
    /// Null for an empty catalog
    /// </summary>
    /// <exception cref="InvalidOperationException">An entry is mis-authored (empty slot, no level, or an
    /// inverted band).</exception>
    public FlySwatterLevel? Pick(int difficulty)
    {
        // Payload check first; empty slots are left for PickNearest to report
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != null && Entries[i].Level == null) { throw new InvalidOperationException($"FlySwatterLevelCatalog '{ResourcePath}': entry {i} has no Level"); }
        }
        return DifficultyBands.PickNearest(Entries, difficulty, $"FlySwatterLevelCatalog '{ResourcePath}'")?.Level;
    }
}
