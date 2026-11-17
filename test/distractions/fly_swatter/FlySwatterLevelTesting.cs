using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;

// Component suite for FlySwatterLevel: its validation and the round it rolls
// Levels are in-memory with chosen values (never the code defaults or a saved preset, which are WIP content)
// Values are whole numbers on purpose: the messages interpolate floats, whose decimal separator follows the culture
[TestSuite]
[RequireGodotRuntime]
public class FlySwatterLevelTesting
{
    // Enough rolls that a reachable value is missed with negligible probability (2^-199 for a 2-value range)
    private const int Rolls = 200;

    [TestCase]
    public void DefaultLevelIsValid()
    {
        // Any exception fails the case
        new FlySwatterLevel().Validate();
    }

    // Every value on its valid boundary at once: single-value ranges, no spare, refill off, minimum sizes
    [TestCase]
    public void BoundaryValuesAreValid()
    {
        FlySwatterLevel level = Level(winScore: 1, spare: 0, firstWave: 1, perWave: 1, interval: 1, refill: 0);

        // Any exception fails the case
        level.Validate();
    }

    // One row per guard, each at its boundary or just past it; every other value stays valid (see ValidLevel)
    [TestCase("MinWinScore", 0, "MinWinScore 0 must be at least 1")]
    [TestCase("MinWinScore", 11, "MinWinScore 11 is above MaxWinScore 10")]
    [TestCase("MinSpareFlies", -1, "MinSpareFlies -1 must not be negative")]
    [TestCase("MinSpareFlies", 3, "MinSpareFlies 3 is above MaxSpareFlies 2")]
    [TestCase("MinFirstWaveSize", 0, "MinFirstWaveSize 0 must be at least 1")]
    [TestCase("MinFirstWaveSize", 4, "MinFirstWaveSize 4 is above MaxFirstWaveSize 3")]
    [TestCase("MinFliesPerWave", 0, "MinFliesPerWave 0 must be at least 1")]
    [TestCase("MinFliesPerWave", 3, "MinFliesPerWave 3 is above MaxFliesPerWave 2")]
    [TestCase("MinWaveInterval", 0, "MinWaveInterval 0 must be positive")]
    [TestCase("MinWaveInterval", -1, "MinWaveInterval -1 must be positive")]
    [TestCase("MinWaveInterval", 5, "MinWaveInterval 5 is above MaxWaveInterval 4")]
    [TestCase("RefillThreshold", -1, "RefillThreshold -1 must not be negative")]
    public void MisAuthoredLevelThrows(string property, int value, string reason)
    {
        FlySwatterLevel level = ValidLevel();
        level.ResourceName = "fixture_level";
        switch (property)
        {
            case "MinWinScore": level.MinWinScore = value; break;
            case "MinSpareFlies": level.MinSpareFlies = value; break;
            case "MinFirstWaveSize": level.MinFirstWaveSize = value; break;
            case "MinFliesPerWave": level.MinFliesPerWave = value; break;
            case "MinWaveInterval": level.MinWaveInterval = value; break;
            case "RefillThreshold": level.RefillThreshold = value; break;
            default: throw new ArgumentException($"Unknown property '{property}'");
        }

        AssertThrown(() => level.Validate())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"FlySwatterLevel 'fixture_level': {reason}");
    }

    // A saved level is named by its file (which is what a designer needs to find it); an in-memory one by its name
    [TestCase(false)]
    [TestCase(true)]
    public void LevelIsNamedByPathElseByName(bool withPath)
    {
        const string path = "res://test/fixtures/fly_swatter_level.tres";
        FlySwatterLevel level = ValidLevel();
        level.ResourceName = "fixture_level";
        if (withPath) { level.TakeOverPath(path); }
        level.MinWinScore = 0;

        AssertThrown(() => level.Validate())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"FlySwatterLevel '{(withPath ? path : "fixture_level")}': MinWinScore 0 must be at least 1");
    }

    // The spare is read back as total - win score, so this also pins "total = win score + spare"
    [TestCase]
    public void RollStaysWithinTheRanges()
    {
        var level = new FlySwatterLevel
        {
            MinWinScore = 5, MaxWinScore = 9,
            MinSpareFlies = 1, MaxSpareFlies = 4,
            MinFirstWaveSize = 2, MaxFirstWaveSize = 6,
            MinFliesPerWave = 1, MaxFliesPerWave = 3,
            MinWaveInterval = 2, MaxWaveInterval = 5,
            RefillThreshold = 2
        };

        for (int i = 0; i < Rolls; i++)
        {
            FlySwatterLevelPlan plan = level.Roll();
            int spare = plan.Waves.TotalFlies - plan.WinScore;

            AssertThat(plan.WinScore).IsBetween(5, 9);
            AssertThat(spare).IsBetween(1, 4);
            AssertThat(plan.Waves.FirstWaveSize).IsBetween(2, 6);
            AssertThat(plan.Waves.WaveInterval).IsBetween(2f, 5f);
            // Periodic wave sizes stay a range (the spawner rolls each wave), and the refill threshold is fixed
            AssertThat(plan.Waves.MinFliesPerWave).IsEqual(1);
            AssertThat(plan.Waves.MaxFliesPerWave).IsEqual(3);
            AssertThat(plan.Waves.RefillThreshold).IsEqual(2);
        }
    }

    // Both ends of each integer range turn up: the bounds are inclusive and the roll actually varies
    [TestCase]
    public void RollReachesBothEndsOfEachRange()
    {
        var level = new FlySwatterLevel
        {
            MinWinScore = 5, MaxWinScore = 6,
            MinSpareFlies = 1, MaxSpareFlies = 2,
            MinFirstWaveSize = 2, MaxFirstWaveSize = 3
        };
        var winScores = new HashSet<int>();
        var spares = new HashSet<int>();
        var firstWaves = new HashSet<int>();

        for (int i = 0; i < Rolls; i++)
        {
            FlySwatterLevelPlan plan = level.Roll();
            winScores.Add(plan.WinScore);
            spares.Add(plan.Waves.TotalFlies - plan.WinScore);
            firstWaves.Add(plan.Waves.FirstWaveSize);
        }

        AssertThat(winScores).ContainsExactlyInAnyOrder(5, 6);
        AssertThat(spares).ContainsExactlyInAnyOrder(1, 2);
        AssertThat(firstWaves).ContainsExactlyInAnyOrder(2, 3);
    }

    // A valid level with single-value ranges, so a row changing one Min* value trips exactly one guard
    private static FlySwatterLevel ValidLevel()
        => Level(winScore: 10, spare: 2, firstWave: 3, perWave: 2, interval: 4, refill: 0);

    private static FlySwatterLevel Level(int winScore, int spare, int firstWave, int perWave, float interval, int refill)
        => new()
        {
            MinWinScore = winScore, MaxWinScore = winScore,
            MinSpareFlies = spare, MaxSpareFlies = spare,
            MinFirstWaveSize = firstWave, MaxFirstWaveSize = firstWave,
            MinFliesPerWave = perWave, MaxFliesPerWave = perWave,
            MinWaveInterval = interval, MaxWaveInterval = interval,
            RefillThreshold = refill
        };
}
