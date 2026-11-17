using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;

// Suite for the difficulty-band roll shared by the minigame catalogs (DifficultyBandEntry + DifficultyBands)
// Entries are bare DifficultyBandEntry instances in a plain List: the roll doesn't depend on any payload
[TestSuite]
[RequireGodotRuntime]
public class DifficultyBandsTesting
{
    private const string Owner = "TestCatalog 'res://test.tres'";
    // Enough rolls that an entry which can be picked is missed with negligible probability (2^-199 for two entries)
    private const int Rolls = 200;

    [TestCase(0, 2)]
    [TestCase(1, 1)]
    [TestCase(2, 0)]
    [TestCase(4, 0)]
    [TestCase(5, 1)]
    public void DistanceToCountsStepsOutsideTheBand(int difficulty, int expected)
    {
        DifficultyBandEntry entry = Band(2, 4);

        AssertThat(entry.DistanceTo(difficulty)).IsEqual(expected);
    }

    [TestCase]
    public void EmptyListPicksNothing()
    {
        AssertThat(DifficultyBands.PickNearest(new List<DifficultyBandEntry>(), 1, Owner)).IsNull();
    }

    // Only one band contains the difficulty, so the roll has a single valid outcome
    [TestCase]
    public void ContainingBandBeatsTheOthers()
    {
        DifficultyBandEntry below = Band(1, 1);
        DifficultyBandEntry containing = Band(2, 3);
        DifficultyBandEntry above = Band(4, 5);
        var entries = new List<DifficultyBandEntry> { below, containing, above };

        for (int i = 0; i < Rolls; i++)
        {
            AssertThat(DifficultyBands.PickNearest(entries, 3, Owner)).IsSame(containing);
        }
    }

    // A non-containing entry is also listed, so returning "any entry" can't pass
    [TestCase]
    public void EveryContainingBandCanBePicked()
    {
        DifficultyBandEntry first = Band(1, 3);
        DifficultyBandEntry second = Band(2, 2);
        DifficultyBandEntry outside = Band(4, 4);
        var entries = new List<DifficultyBandEntry> { first, second, outside };

        AssertThat(RollAll(entries, 2)).ContainsExactlyInAnyOrder(first, second);
    }

    [TestCase(0)]
    [TestCase(9)]
    public void NearestBandIsPickedWhenNoneContainsTheDifficulty(int difficulty)
    {
        DifficultyBandEntry low = Band(1, 2);
        DifficultyBandEntry middle = Band(4, 5);
        DifficultyBandEntry high = Band(7, 8);
        var entries = new List<DifficultyBandEntry> { low, middle, high };
        DifficultyBandEntry expected = difficulty < 1 ? low : high;

        AssertThat(RollAll(entries, difficulty)).ContainsExactlyInAnyOrder(expected);
    }

    // Difficulty 3 lies one step from both bands
    [TestCase]
    public void EquallyNearBandsOnBothSidesCanBothBePicked()
    {
        DifficultyBandEntry below = Band(1, 2);
        DifficultyBandEntry above = Band(4, 5);
        DifficultyBandEntry farther = Band(7, 7);
        var entries = new List<DifficultyBandEntry> { below, above, farther };

        AssertThat(RollAll(entries, 3)).ContainsExactlyInAnyOrder(below, above);
    }

    // Boundary of the inverted-band guard
    [TestCase]
    public void SingleDifficultyBandIsValid()
    {
        DifficultyBandEntry single = Band(3, 3);

        AssertThat(DifficultyBands.PickNearest(new List<DifficultyBandEntry> { single }, 3, Owner)).IsSame(single);
    }

    [TestCase]
    public void EmptySlotThrows()
    {
        var entries = new List<DifficultyBandEntry> { Band(1, 1), null! };

        AssertThrown(() => DifficultyBands.PickNearest(entries, 1, Owner))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"{Owner}: entry 1 is empty");
    }

    [TestCase]
    public void InvertedBandThrows()
    {
        var entries = new List<DifficultyBandEntry> { Band(1, 1), Band(3, 2) };

        AssertThrown(() => DifficultyBands.PickNearest(entries, 1, Owner))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"{Owner}: entry 1 has MinDifficulty 3 above MaxDifficulty 2");
    }

    private static DifficultyBandEntry Band(int min, int max)
        => new() { MinDifficulty = min, MaxDifficulty = max };

    // Every distinct entry the roll returned over many tries
    private static List<DifficultyBandEntry> RollAll(List<DifficultyBandEntry> entries, int difficulty)
    {
        var seen = new List<DifficultyBandEntry>();
        for (int i = 0; i < Rolls; i++)
        {
            DifficultyBandEntry? picked = DifficultyBands.PickNearest(entries, difficulty, Owner);
            if (picked != null && !seen.Contains(picked)) { seen.Add(picked); }
        }
        return seen;
    }
}
