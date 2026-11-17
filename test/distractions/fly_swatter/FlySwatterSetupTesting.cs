using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;

// Suite for FlySwatter.Setup(): choosing the level (LevelOverride, else a catalog roll, else the authored fallback),
// pushing the rolled plan into the spawner and the tracker, and the guards on what it finds. Split from
// FlySwatterBehaviourTesting as a cohesive sub-area
// Everything is hand-built and never enters a tree, as Setup() runs before tree entry. Levels are in-memory with
// single-value ranges, so the rolled plan is exact; it's read back through FlySpawner.MaxTotalFlies (win + spare)
// and the tracker (the win score, nothing smacked yet)
// The override notice is a GD.Print with no observable outcome; the override path itself is covered below
[TestSuite]
[RequireGodotRuntime]
public class FlySwatterSetupTesting
{
    [TestCase]
    public void LevelOverrideWinsOverTheCatalog()
    {
        FlySwatter shell = BuildShell();
        shell.Set("_levelCatalog", Catalog((Level(winScore: 4, spare: 1), 1, 1)));
        shell.LevelOverride = Level(winScore: 6, spare: 1);

        shell.Setup(1);

        AssertThat(Tracker(shell).Text).IsEqual("6");
    }

    // The bands don't overlap, so only the right difficulty leads to the second level
    [TestCase]
    public void CatalogRollUsesTheSetupDifficulty()
    {
        FlySwatter shell = BuildShell();
        shell.Set("_levelCatalog", Catalog((Level(winScore: 4, spare: 1), 1, 1), (Level(winScore: 6, spare: 1), 5, 5)));

        shell.Setup(5);

        AssertThat(Tracker(shell).Text).IsEqual("6");
    }

    [TestCase]
    public void SetupPushesTheRolledPlanIntoSpawnerAndTracker()
    {
        FlySwatter shell = BuildShell();
        shell.LevelOverride = Level(winScore: 7, spare: 3);

        shell.Setup(1);

        AssertThat(Spawner(shell).MaxTotalFlies).IsEqual(10);
        AssertThat(Tracker(shell).Text).IsEqual("7");
    }

    // The fallback values are set here, so the case doesn't depend on the authored ones
    [TestCase(false)]
    [TestCase(true)]
    public void FallbackIsKeptWithoutAnOverrideOrCatalogPick(bool emptyCatalog)
    {
        FlySwatter shell = BuildShell();
        shell.Set("_winScore", 4);
        Spawner(shell).Set("_maxTotalFlies", 9);
        if (emptyCatalog) { shell.Set("_levelCatalog", new FlySwatterLevelCatalog()); }

        shell.Setup(1);

        AssertThat(Spawner(shell).MaxTotalFlies).IsEqual(9);
        AssertThat(Tracker(shell).Text).IsEqual("4");
    }

    // Setup() validates the chosen level before rolling it
    [TestCase]
    public void MisAuthoredLevelFailsSetup()
    {
        FlySwatter shell = BuildShell();
        FlySwatterLevel level = Level(winScore: 4, spare: 1);
        level.ResourceName = "fixture_level";
        level.MinWinScore = 0;
        shell.LevelOverride = level;

        AssertThrown(() => shell.Setup(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("FlySwatterLevel 'fixture_level': MinWinScore 0 must be at least 1");
    }

    // Only the fallback path can reach this guard: a rolled level is winnable by construction
    [TestCase]
    public void WinScoreAboveTheSpawnersTotalThrows()
    {
        FlySwatter shell = BuildShell();
        shell.Set("_winScore", 10);
        Spawner(shell).Set("_maxTotalFlies", 9);

        AssertThrown(() => shell.Setup(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("FlySwatter: win score 10 is above the spawner's 9 total flies");
    }

    // Boundary of the guard above: smacking every fly is a valid round
    [TestCase]
    public void WinScoreEqualToTheTotalSetsUp()
    {
        FlySwatter shell = BuildShell();
        shell.Set("_winScore", 9);
        Spawner(shell).Set("_maxTotalFlies", 9);

        shell.Setup(1);

        AssertThat(Tracker(shell).Text).IsEqual("9");
    }

    [TestCase]
    public void MissingFlySpawnerThrows()
    {
        FlySwatter shell = BuildShell(spawner: null);

        AssertThrown(() => shell.Setup(1))
            .IsInstanceOf<NullReferenceException>()
            .HasMessage("FlySwatter: missing required child 'Stage/FlySpawner'");
    }

    // GetNode<T>() throws its own cast exception for a present-but-wrong-type child, before the guard
    [TestCase]
    public void WrongTypedFlySpawnerThrows()
    {
        FlySwatter shell = BuildShell(spawner: new Node2D());

        AssertThrown(() => shell.Setup(1)).IsInstanceOf<InvalidCastException>();
    }

    // The tracker is optional, so a node of the wrong type counts as no tracker rather than failing Setup()
    // (a missing one is FlySwatterBehaviourTesting.SetupToleratesMissingScoreTracker)
    [TestCase]
    public void WrongTypedScoreTrackerIsIgnored()
    {
        FlySwatter shell = BuildShell(new FlySpawner(), tracker: new Node2D());
        shell.LevelOverride = Level(winScore: 7, spare: 3);

        shell.Setup(1);

        AssertThat(Spawner(shell).MaxTotalFlies).IsEqual(10);
    }

    // The spawner never runs its _Ready(), so it needs no fly scene
    private static FlySwatter BuildShell() => BuildShell(new FlySpawner());

    // Shell with "spawner" (if any) as Stage/FlySpawner and "tracker" (a Label unless given) as Stage/ScoreTracker
    private static FlySwatter BuildShell(Node? spawner, Node? tracker = null)
    {
        FlySwatter shell = AutoFree(new FlySwatter())!;
        var stage = new Node2D { Name = "Stage" };
        shell.AddChild(stage);
        if (spawner != null)
        {
            spawner.Name = "FlySpawner";
            stage.AddChild(spawner);
        }
        tracker ??= new Label();
        tracker.Name = "ScoreTracker";
        stage.AddChild(tracker);
        return shell;
    }

    private static FlySwatterLevel Level(int winScore, int spare)
        => new()
        {
            MinWinScore = winScore, MaxWinScore = winScore,
            MinSpareFlies = spare, MaxSpareFlies = spare,
            MinFirstWaveSize = 1, MaxFirstWaveSize = 1,
            MinFliesPerWave = 1, MaxFliesPerWave = 1,
            MinWaveInterval = 4, MaxWaveInterval = 4
        };

    private static FlySwatterLevelCatalog Catalog(params (FlySwatterLevel level, int min, int max)[] entries)
    {
        var catalog = new FlySwatterLevelCatalog();
        foreach (var (level, min, max) in entries)
        {
            catalog.Entries.Add(new FlySwatterLevelEntry { Level = level, MinDifficulty = min, MaxDifficulty = max });
        }
        return catalog;
    }

    private static FlySpawner Spawner(FlySwatter shell) => shell.GetNode<FlySpawner>("Stage/FlySpawner");

    private static Label Tracker(FlySwatter shell) => shell.GetNode<Label>("Stage/ScoreTracker");
}
