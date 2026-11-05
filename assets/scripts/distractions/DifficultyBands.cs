using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Difficulty-band picking shared by the minigame catalogs (layouts, level presets)
/// <para>The roll happens per call, nothing is decided in advance</para>
/// </summary>
public static class DifficultyBands
{
    /// <summary>
    /// Rolls an entry for <paramref name="difficulty"/>: a random one among those whose band contains it or,
    /// when none does, among those whose band lies nearest. Null for an empty list
    /// </summary>
    /// <param name="entries">The catalog's entries, in authored order</param>
    /// <param name="difficulty">The difficulty to pick for</param>
    /// <param name="owner">Prefix naming the catalog in error messages, e.g. <c>"XCatalog 'res://...'"</c></param>
    /// <exception cref="InvalidOperationException">An entry is mis-authored (empty slot or an inverted band).</exception>
    public static T? PickNearest<T>(IReadOnlyList<T> entries, int difficulty, string owner) where T : DifficultyBandEntry
    {
        var nearest = new List<T>();
        int nearestDistance = int.MaxValue;
        for (int i = 0; i < entries.Count; i++)
        {
            T? entry = entries[i];
            if (entry == null) { throw new InvalidOperationException($"{owner}: entry {i} is empty"); }
            if (entry.MinDifficulty > entry.MaxDifficulty) { throw new InvalidOperationException($"{owner}: entry {i} has MinDifficulty {entry.MinDifficulty} above MaxDifficulty {entry.MaxDifficulty}"); }

            int distance = entry.DistanceTo(difficulty);
            if (distance > nearestDistance) { continue; }
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearest.Clear();
            }
            nearest.Add(entry);
        }

        if (nearest.Count == 0) { return null; }
        return nearest[GD.RandRange(0, nearest.Count - 1)];
    }
}
