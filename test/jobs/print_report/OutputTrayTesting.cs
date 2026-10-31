using System;
using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// OutputTray over a hand-built tray (Page, Stack, Pickup + hitbox) in a live tree: what each job call
// shows, the pickup click, and the page animations' hand-overs (a new page must not inherit what the
// last one was doing). Animations are stepped (StepTweens); their look (slide, tint, shake, stack
// drawing) is not pinned, only compared with the tray's own tuning
[TestSuite]
[RequireGodotRuntime]
public class OutputTrayTesting
{
    private ISceneRunner? _runner;

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    private static OutputTray Build(string? without = null)
    {
        OutputTray tray = new();
        if (without != "Page") { tray.AddChild(new Sprite2D { Name = "Page" }); }
        if (without != "Stack") { tray.AddChild(new Node2D { Name = "Stack" }); }
        if (without != "Pickup")
        {
            SpriteInteractable pickup = new() { Name = "Pickup" };
            pickup.AddChild(new CollisionShape2D { Shape = new RectangleShape2D() });
            tray.AddChild(pickup);
        }
        return tray;
    }

    // A tray loaded live under a wrapper root (its _Ready has run)
    private OutputTray BuildLive()
    {
        Node2D root = new();
        OutputTray tray = Build();
        root.AddChild(tray);
        _runner = ISceneRunner.Load(root, true, true);
        return tray;
    }

    private static Sprite2D Page(OutputTray tray) => tray.GetNode<Sprite2D>("Page");

    private static Node2D Stack(OutputTray tray) => tray.GetNode<Node2D>("Stack");

    private static SpriteInteractable Pickup(OutputTray tray) => tray.GetNode<SpriteInteractable>("Pickup");

    private static float DiscardSeconds(OutputTray tray) => tray.Get("_discardSeconds").AsSingle();

    // ---- What each call shows ----

    // Hiding is what keeps the pickup unclickable until the job shows it
    [TestCase]
    public void StartsWithNoPageAndAnUnclickablePickup()
    {
        OutputTray tray = BuildLive();
        int pickedUp = 0;
        tray.PickedUp += () => pickedUp++;

        Click(Pickup(tray));

        AssertThat(Page(tray).Visible).IsFalse();
        AssertThat(Pickup(tray).Visible).IsFalse();
        AssertThat(pickedUp).IsEqual(0);
    }

    [TestCase]
    public void BeginPageShowsThePage()
    {
        OutputTray tray = BuildLive();

        tray.BeginPage(1f);

        AssertThat(Page(tray).Visible).IsTrue();
    }

    [TestCase]
    public void ShowPickupShowsThePickupInsteadOfTheStackAndThePage()
    {
        OutputTray tray = BuildLive();
        tray.BeginPage(1f);
        tray.FinishPage();
        tray.BeginPage(1f);

        tray.ShowPickup();

        AssertThat(Pickup(tray).Visible).IsTrue();
        AssertThat(Stack(tray).Visible).IsFalse();
        AssertThat(Page(tray).Visible).IsFalse();
    }

    [TestCase]
    public void HideAllHidesEverything()
    {
        OutputTray tray = BuildLive();
        tray.BeginPage(1f);
        tray.ShowPickup();
        tray.BeginPage(1f);

        tray.HideAll();

        AssertThat(Pickup(tray).Visible).IsFalse();
        AssertThat(Stack(tray).Visible).IsFalse();
        AssertThat(Page(tray).Visible).IsFalse();
    }

    [TestCase]
    public void ClickingTheShownPickupReportsItOnce()
    {
        OutputTray tray = BuildLive();
        int pickedUp = 0;
        tray.PickedUp += () => pickedUp++;
        tray.ShowPickup();

        Click(Pickup(tray));

        AssertThat(pickedUp).IsEqual(1);
    }

    [TestCase]
    public void FinishPageHidesThePageAndTheStackNeverExceedsItsCap()
    {
        OutputTray tray = BuildLive();
        int cap = tray.Get("_maxStackSheets").AsInt32();

        for (int i = 0; i < cap + 3; i++)
        {
            tray.BeginPage(1f);
            tray.FinishPage();
        }

        AssertThat(Page(tray).Visible).IsFalse();
        AssertThat(Stack(tray).GetChildCount()).IsEqual(cap);
    }

    // ---- The page's animations ----

    // The sheet stops where it jammed (it may shake, never further than the tray's own shake) and stays
    [TestCase]
    public void AJammedPageStaysWhereItStopped()
    {
        OutputTray tray = BuildLive();
        tray.BeginPage(1f);
        StepTweens(tray, 0.4);

        tray.JamPage();
        Vector2 stuck = Page(tray).Position;
        float shake = tray.Get("_shakeDistance").AsSingle();
        var drift = new List<float>();
        StepTweens(tray, 2, () => drift.Add(Page(tray).Position.DistanceTo(stuck)));

        AssertThat(Page(tray).Visible).IsTrue();
        AssertThat(drift.TrueForAll(distance => distance <= shake + 1e-3f)).OverrideFailureMessage($"the page drifted up to {drift.Max()} px").IsTrue();
    }

    [TestCase]
    public void DiscardPageHidesThePageOnceDone()
    {
        OutputTray tray = BuildLive();
        tray.BeginPage(1f);
        tray.JamPage();

        tray.DiscardPage();
        StepTweens(tray, DiscardSeconds(tray) + 0.05);

        AssertThat(Page(tray).Visible).IsFalse();
    }

    // A player clears a jam and presses PRINT, with the old page gone or still going. The new page must look
    // like any fresh one, and the old page's animations must not reach it
    [TestCase("after a discard")]
    [TestCase("during a discard")]
    public void ANewPageStartsFresh(string when)
    {
        OutputTray tray = BuildLive();
        tray.BeginPage(1f);
        Color fresh = Page(tray).Modulate;
        tray.JamPage();
        tray.DiscardPage();
        StepTweens(tray, when == "during a discard" ? DiscardSeconds(tray) / 2 : DiscardSeconds(tray) + 0.05);

        tray.BeginPage(1f);
        StepTweens(tray, DiscardSeconds(tray) * 2);

        AssertThat(Page(tray).Visible).IsTrue();
        AssertThat(Page(tray).Modulate).IsEqual(fresh);
    }

    // ---- Authoring guard ----

    // ShowPickup reaches all three children, and works out of the tree
    [TestCase("Page")]
    [TestCase("Stack")]
    [TestCase("Pickup")]
    public void AMissingPartThrowsNamingIt(string missing)
    {
        OutputTray tray = AutoFree(Build(without: missing))!;

        AssertThrown(() => tray.ShowPickup()).IsInstanceOf<NullReferenceException>()
            .HasMessage($"OutputTray: missing required child '{missing}'");
    }
}
