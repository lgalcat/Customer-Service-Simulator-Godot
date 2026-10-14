using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// MainLevel's own logic in isolation: its required-child guards and the one policy it exists for -
// Office is locked exactly while a Distraction is active. Hand-built Office and Distraction managers
// (stub minigame), so nothing here depends on the real scenes; the real composition is covered by
// MainLevelIntegrationTesting. How Office reacts to the lock is OfficeManagerTesting's business.
[TestSuite]
[RequireGodotRuntime]
public class MainLevelTesting
{
    [TestCase("OfficeManager")]
    [TestCase("DistractionManager")]
    public void ReadyThrowsWhenARequiredChildIsMissing(string missing)
    {
        // Manual _Ready() calls - no SceneTree to auto-trigger them (applies to every test below)
        MainLevel level = BuildLevel(omit: missing);

        // The exact message tells the guard apart from an incidental NullReferenceException
        AssertThrown(() => level._Ready()).IsInstanceOf<NullReferenceException>().HasMessage($"MainLevel: missing required child '{missing}'");
    }

    // A wrong-type child throws GetNode<T>()'s InvalidCastException before the manual guard can run
    [TestCase]
    public void ReadyThrowsWhenAChildIsTheWrongType()
    {
        MainLevel level = BuildLevel(omit: "OfficeManager");
        level.AddChild(new Node2D { Name = "OfficeManager" });

        AssertThrown(() => level._Ready()).IsInstanceOf<InvalidCastException>();
    }

    [TestCase]
    public void StartedLocksOfficeAndCompletedUnlocksIt()
    {
        MainLevel level = BuildLevel();
        level._Ready();
        var office = level.GetNode<OfficeManager>("OfficeManager");
        var distractions = level.GetNode<DistractionManager>("DistractionManager");
        AssertThat(office.InputLocked).IsFalse();

        distractions.RequestDistraction();
        Control overlay = distractions.GetNode<Control>("Overlay");
        foreach (Node child in overlay.GetChildren()) { AutoFree(child); }
        AssertThat(office.InputLocked).IsTrue();

        // A denied request must not touch the lock
        distractions.RequestDistraction();
        AssertThat(office.InputLocked).IsTrue();

        FindHostedDistraction(overlay).Victory();
        AssertThat(office.InputLocked).IsFalse();
    }

    // A MainLevel over hand-built, already-ready managers named as its exports expect; 'omit' leaves
    // one of them out. MainLevel's own _Ready() is left for the test to call
    private static MainLevel BuildLevel(string? omit = null)
    {
        MainLevel level = AutoFree(new MainLevel())!;

        if (omit != "OfficeManager")
        {
            OfficeManager office = BuildManager(new EventLog(), "A", "A", "B");
            office.Name = "OfficeManager";
            level.AddChild(office);
            ReadyBottomUp(office);
        }

        if (omit != "DistractionManager")
        {
            var distractions = new DistractionManager { Name = "DistractionManager" };
            distractions.AddChild(new Control { Name = "Overlay" });
            distractions.InitialFactory = new DistractionFactory(new Dictionary<DistractionType, PackedScene>
            {
                { DistractionType.ThrowPaperBall, StubDistraction.Pack() },
            });
            level.AddChild(distractions);
            distractions._Ready();
        }

        return level;
    }

    // Host -> SubViewport -> Distraction is BuildHost's exact nesting
    private static Distraction FindHostedDistraction(Control overlay)
    {
        Node host = overlay.GetChild(0);
        SubViewport viewport = host.GetChildren().OfType<SubViewport>().First();
        return viewport.GetChildren().OfType<Distraction>().First();
    }
}
