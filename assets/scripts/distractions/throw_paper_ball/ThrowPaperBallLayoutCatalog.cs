using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The layouts <see cref="ThrowPaperBall"/> rolls from during its Setup, each rated with a difficulty band
/// <para>Shared as a <c>.tres</c>; the roll happens per spawn, nothing is decided in advance</para>
/// </summary>
[GlobalClass]
public partial class ThrowPaperBallLayoutCatalog : Resource
{
    /// <summary>
    /// The layouts to pick from
    /// </summary>
    [Export] public Godot.Collections.Array<ThrowPaperBallLayoutEntry> Entries { get; set; } = new();

    /// <summary>
    /// Rolls a layout for <paramref name="difficulty"/>: a random entry among those whose band contains it or,
    /// when none does, among those whose band lies nearest. Null for an empty catalog
    /// </summary>
    /// <exception cref="InvalidOperationException">An entry is mis-authored (empty slot, no layout, or an
    /// inverted band).</exception>
    public PackedScene? Pick(int difficulty)
    {
        var nearest = new List<PackedScene>();
        int nearestDistance = int.MaxValue;
        for (int i = 0; i < Entries.Count; i++)
        {
            ThrowPaperBallLayoutEntry? entry = Entries[i];
            if (entry == null) { throw new InvalidOperationException($"ThrowPaperBallLayoutCatalog '{ResourcePath}': entry {i} is empty"); }
            if (entry.Layout == null) { throw new InvalidOperationException($"ThrowPaperBallLayoutCatalog '{ResourcePath}': entry {i} has no Layout"); }
            if (entry.MinDifficulty > entry.MaxDifficulty) { throw new InvalidOperationException($"ThrowPaperBallLayoutCatalog '{ResourcePath}': entry {i} has MinDifficulty {entry.MinDifficulty} above MaxDifficulty {entry.MaxDifficulty}"); }

            int distance = entry.DistanceTo(difficulty);
            if (distance > nearestDistance) { continue; }
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest.Clear();
            }
            nearest.Add(entry.Layout);
        }

        if (nearest.Count == 0) { return null; }
        return nearest[GD.RandRange(0, nearest.Count - 1)];
    }
}
