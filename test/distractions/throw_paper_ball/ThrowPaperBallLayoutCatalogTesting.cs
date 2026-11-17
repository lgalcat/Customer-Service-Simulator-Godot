using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;

// Component suite for ThrowPaperBallLayoutCatalog: its own payload check and what it hands back from the shared roll
// (the roll itself is DifficultyBandsTesting's). The catalogs are in-memory, so their ResourcePath is empty
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallLayoutCatalogTesting
{
    private const string Owner = "ThrowPaperBallLayoutCatalog ''";

    [TestCase]
    public void PickReturnsThePickedEntrysLayout()
    {
        var layout = new PackedScene();
        var catalog = new ThrowPaperBallLayoutCatalog();
        catalog.Entries.Add(Entry(layout, 1, 3));

        AssertThat(catalog.Pick(2)).IsSame(layout);
    }

    [TestCase]
    public void EmptyCatalogPicksNothing()
    {
        AssertThat(new ThrowPaperBallLayoutCatalog().Pick(1)).IsNull();
    }

    // The broken entry is the second one, so a hardcoded index can't pass
    [TestCase]
    public void EntryWithoutLayoutThrows()
    {
        var catalog = new ThrowPaperBallLayoutCatalog();
        catalog.Entries.Add(Entry(new PackedScene(), 1, 1));
        catalog.Entries.Add(Entry(null, 1, 1));

        AssertThrown(() => catalog.Pick(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"{Owner}: entry 1 has no Layout");
    }

    // The payload check skips empty slots and leaves them to the shared roll, which reports them by index
    // instead of the payload check dereferencing them
    [TestCase]
    public void EmptySlotIsReportedAsEmpty()
    {
        var catalog = new ThrowPaperBallLayoutCatalog();
        catalog.Entries.Add(null!);

        AssertThrown(() => catalog.Pick(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"{Owner}: entry 0 is empty");
    }

    private static ThrowPaperBallLayoutEntry Entry(PackedScene? layout, int min, int max)
        => new() { Layout = layout, MinDifficulty = min, MaxDifficulty = max };
}
