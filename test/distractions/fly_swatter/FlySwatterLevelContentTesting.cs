using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;

// Level-content checks on every level the real game can play: the saved catalog's levels plus the authored fallback
// (catalog cleared), read from the shell itself, so a new level is covered without editing this suite
// Each level plays through the real shell (LevelOverride + Setup), loaded in a live tree for its real _Ready()s
// Each case collects "<level>: <reason>" for every failing level and asserts the list is empty, so one broken
// level doesn't hide the others. No frames are simulated
[TestSuite]
[RequireGodotRuntime]
public class FlySwatterLevelContentTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/fly_swatter/fly_swatter.tscn";
    private const string FallbackLabel = "fallback (no level)";

    // Covers each level's own values (Validate) and the round starting on them: the first wave spawns
    [TestCase]
    public void EveryLevelSetsUp()
    {
        var failures = new List<string>();
        ForEachLoadedLevel(failures, (label, flySwatter) =>
        {
            int flies = flySwatter.GetNode("Stage/FlySpawner").GetChildren().OfType<Fly>().Count();
            if (flies < 1) { failures.Add($"{label}: no fly spawned at the start of the round"); }
        });

        AssertThat(failures).IsEmpty();
    }

    // The tracker starts on the win score; smacking every fly the spawner will ever spawn must win
    [TestCase]
    public void EveryLevelIsWinnableWithinItsTotal()
    {
        var failures = new List<string>();
        ForEachLoadedLevel(failures, (label, flySwatter) =>
        {
            FlySpawner flySpawner = flySwatter.GetNode<FlySpawner>("Stage/FlySpawner");
            int winScore = int.Parse(flySwatter.GetNode<Label>("Stage/ScoreTracker").Text);
            if (winScore > flySpawner.MaxTotalFlies) { failures.Add($"{label}: win score {winScore} is above the {flySpawner.MaxTotalFlies} flies spawned"); }

            bool victoryCalled = false;
            flySwatter.OnVictory = () => { victoryCalled = true; };
            for (int i = 0; i < flySpawner.MaxTotalFlies; i++) { flySpawner.FlyDied?.Invoke(); }
            if (!victoryCalled) { failures.Add($"{label}: smacking all {flySpawner.MaxTotalFlies} flies doesn't win"); }
        });

        AssertThat(failures).IsEmpty();
    }

    // The roll checks every entry whatever the difficulty, so one pick checks the whole saved catalog: an empty slot,
    // an entry with no level or an inverted band. The catalog itself is optional (none means the fallback)
    [TestCase]
    public void SavedCatalogIsWellFormed()
    {
        FlySwatterLevelCatalog? catalog = SavedCatalog();
        if (catalog == null) { return; }

        string? failure = null;
        try { catalog.Pick(1); }
        catch (Exception exception) { failure = exception.Message; }

        AssertThat(failure).IsNull();
    }

    // LevelOverride is for tests and playtesting only: saved in the shell, the real game would always play that level
    [TestCase]
    public void SavedShellHasNoLevelOverride()
    {
        FlySwatter flySwatter = AutoFree(GD.Load<PackedScene>(ScenePath).Instantiate<FlySwatter>())!;

        AssertThat(flySwatter.LevelOverride)
            .OverrideFailureMessage($"fly_swatter.tscn is saved with LevelOverride = '{flySwatter.LevelOverride?.ResourcePath}'; clear it")
            .IsNull();
    }

    private static FlySwatterLevelCatalog? SavedCatalog()
    {
        FlySwatter flySwatter = GD.Load<PackedScene>(ScenePath).Instantiate<FlySwatter>();
        var catalog = flySwatter.Get("_levelCatalog").As<FlySwatterLevelCatalog>();
        flySwatter.Free();
        return catalog;
    }

    // The saved catalog's levels, each once, plus null for the authored fallback
    private static List<FlySwatterLevel?> PlayableLevels()
    {
        var levels = new List<FlySwatterLevel?> { null };
        FlySwatterLevelCatalog? catalog = SavedCatalog();
        if (catalog != null)
        {
            levels.AddRange(catalog.Entries.Where(entry => entry?.Level != null).Select(entry => entry.Level).Distinct());
        }
        return levels;
    }

    // Sets each playable level up through the real shell, loads it in a live tree and hands it to "check"
    // A level whose Setup() fails is reported here and skipped
    private static void ForEachLoadedLevel(List<string> failures, Action<string, FlySwatter> check)
    {
        foreach (FlySwatterLevel? level in PlayableLevels())
        {
            string label = level == null ? FallbackLabel : level.ResourcePath;
            FlySwatter flySwatter = GD.Load<PackedScene>(ScenePath).Instantiate<FlySwatter>();
            if (level == null) { flySwatter.Set("_levelCatalog", new Variant()); }
            flySwatter.LevelOverride = level;
            try { flySwatter.Setup(1); }
            catch (Exception exception)
            {
                failures.Add($"{label}: {exception.Message}");
                flySwatter.Free();
                continue;
            }
            using ISceneRunner runner = ISceneRunner.Load(flySwatter, true, true);
            check(label, flySwatter);
        }
    }
}
