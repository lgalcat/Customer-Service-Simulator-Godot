using Godot;
using System;

/// <summary>
/// The layouts <see cref="Platformer"/> rolls from during its Setup, each rated with a difficulty band
/// <para>Shared as a <c>.tres</c>; the roll happens per spawn, nothing is decided in advance</para>
/// </summary>
[GlobalClass]
public partial class PlatformerLayoutCatalog : Resource
{
    /// <summary>
    /// The layouts to pick from
    /// </summary>
    [Export] public Godot.Collections.Array<PlatformerLayoutEntry> Entries { get; set; } = new();

    /// <summary>
    /// Rolls a layout for <paramref name="difficulty"/>: a random entry among those whose band contains it or,
    /// when none does, among those whose band lies nearest (see <see cref="DifficultyBands.PickNearest"/>).
    /// Null for an empty catalog
    /// </summary>
    /// <exception cref="InvalidOperationException">An entry is mis-authored (empty slot, no layout, or an
    /// inverted band).</exception>
    public PackedScene? Pick(int difficulty)
    {
        // Payload check first; empty slots are left for PickNearest to report
        for (int i = 0; i < Entries.Count; i++)
        {
            if (Entries[i] != null && Entries[i].Layout == null) { throw new InvalidOperationException($"PlatformerLayoutCatalog '{ResourcePath}': entry {i} has no Layout"); }
        }
        return DifficultyBands.PickNearest(Entries, difficulty, $"PlatformerLayoutCatalog '{ResourcePath}'")?.Layout;
    }
}
