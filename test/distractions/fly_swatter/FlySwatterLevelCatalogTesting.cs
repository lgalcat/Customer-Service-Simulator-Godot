using GdUnit4;
using static GdUnit4.Assertions;
using System;

// Component suite for FlySwatterLevelCatalog: its own payload check and what it hands back from the shared roll
// (the roll itself is DifficultyBandsTesting's). The catalogs are in-memory, so their ResourcePath is empty
[TestSuite]
[RequireGodotRuntime]
public class FlySwatterLevelCatalogTesting
{
    private const string Owner = "FlySwatterLevelCatalog ''";

    [TestCase]
    public void PickReturnsThePickedEntrysLevel()
    {
        var level = new FlySwatterLevel();
        var catalog = new FlySwatterLevelCatalog();
        catalog.Entries.Add(Entry(level, 1, 3));

        AssertThat(catalog.Pick(2)).IsSame(level);
    }

    [TestCase]
    public void EmptyCatalogPicksNothing()
    {
        AssertThat(new FlySwatterLevelCatalog().Pick(1)).IsNull();
    }

    // The broken entry is the second one, so a hardcoded index can't pass
    [TestCase]
    public void EntryWithoutLevelThrows()
    {
        var catalog = new FlySwatterLevelCatalog();
        catalog.Entries.Add(Entry(new FlySwatterLevel(), 1, 1));
        catalog.Entries.Add(Entry(null, 1, 1));

        AssertThrown(() => catalog.Pick(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"{Owner}: entry 1 has no Level");
    }

    // The payload check skips empty slots and leaves them to the shared roll, which reports them by index
    // instead of the payload check dereferencing them
    [TestCase]
    public void EmptySlotIsReportedAsEmpty()
    {
        var catalog = new FlySwatterLevelCatalog();
        catalog.Entries.Add(null!);

        AssertThrown(() => catalog.Pick(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"{Owner}: entry 0 is empty");
    }

    private static FlySwatterLevelEntry Entry(FlySwatterLevel? level, int min, int max)
        => new() { Level = level, MinDifficulty = min, MaxDifficulty = max };
}
