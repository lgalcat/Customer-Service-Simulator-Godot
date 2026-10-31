using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;
using static OfficeTestKit;
using static PrintReportTestKit;

// Print Report's game rules, played on the real PrinterScreen of the real office through the printer's own
// buttons: PRINT prints a page; a print may run into trouble, shown as a warning on one function button,
// and pressing that button in time saves the page; a wrong button or a missed warning jams the printer, and
// the code played on the lights clears it (a wrong press restarts the entry, PRINT replays the code); a
// jammed page doesn't count; once every page is printed the stack is picked up and the job completes.
// Nothing here pins the balance, the timings or the look.
// The job drives the resident PrinterPanel, so its restoration is checked here.
// Extends JobTesting for its office/runner setup only; redeclares none of its cases
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class PrintReportBehaviourTesting : JobTesting
{
    protected override Job CreateJob() => InstantiateJob();

    [BeforeTest]
    public override void Setup() => base.Setup();

    [AfterTest]
    public override void Teardown() => base.Teardown();

    private Play Start(int difficulty = 1, Action<Job>? configure = null) => PrintReportTestKit.Start(office, difficulty, configure);

    private static int Subscribers(PrinterPanel panel) => panel.ButtonPressed?.GetInvocationList().Length ?? 0;

    private static bool AnyLightLit(Play play) => Enumerable.Range(0, play.Panel.ButtonCount).Any(i => IsLit(play.Panel.GetLight(i)));

    // Whether the printer is still jammed is what PRINT does next: replay the code, or print a page. Ends
    // the case (either press changes the state)
    private static void AssertStillJammed(Play play)
    {
        PressPrint(play);
        AssertThat(Printing(play)).OverrideFailureMessage("the jam was cleared").IsFalse();
    }

    private static void AssertJamCleared(Play play)
    {
        PressPrint(play);
        AssertThat(Printing(play)).OverrideFailureMessage("the printer is still jammed").IsTrue();
    }

    private static void AssertJammed(Play play)
    {
        AssertThat(Printing(play)).OverrideFailureMessage("still printing").IsFalse();
        AssertThat(CodeOf(play)).OverrideFailureMessage("no code to clear").IsNotEmpty();
        AssertThat(PickupShown(play)).IsFalse();
    }

    // Each hitbox polygon of 'area', in screen coordinates. A hitbox of some other shape fails here rather
    // than going unchecked
    private static Vector2[][] HitboxesOf(Area2D area)
    {
        Vector2[][] hitboxes = area.GetChildren().OfType<CollisionPolygon2D>().Select(polygon => polygon.GlobalTransform * polygon.Polygon).ToArray();
        AssertThat(hitboxes.Length).OverrideFailureMessage($"{area.Name} has no polygon to compare").IsGreater(0);
        return hitboxes;
    }

    // ---- Placement ----

    [TestCase]
    public void PlacesTheTrayAtThePrintersOutputAndTheTicketBesideIt()
    {
        Play play = Start();

        AssertThat(DescendantsOf(play.Screen).OfType<OutputTray>().Select(tray => tray.GetParent().Name.ToString())).ContainsExactly("OutputAnchor");
        AssertThat(DescendantsOf(play.Screen).OfType<PrintTicket>().Select(ticket => ticket.GetParent().Name.ToString())).ContainsExactly("TicketAnchor");
    }

    // A pickup lying over a resident hotspot would steal (or lose) its clicks; checks no layout values
    [TestCase]
    public void ThePickupCanBeClickedWithoutOverlappingAnyOtherHotspot()
    {
        Play play = Start();
        Vector2[][] pickup = HitboxesOf(play.Pickup);

        AssertThat(HasUsableHitbox(play.Pickup)).IsTrue();
        foreach (SpriteInteractable hotspot in InteractablesUnder(play.Screen).Where(hotspot => hotspot != play.Pickup))
        {
            bool overlaps = HitboxesOf(hotspot).Any(theirs => pickup.Any(ours => Geometry2D.IntersectPolygons(ours, theirs).Count > 0));
            AssertThat(overlaps).OverrideFailureMessage($"the pickup overlaps {hotspot.Name}").IsFalse();
        }
    }

    // ---- Pages (placeholder balance: only its shape is pinned) ----

    [TestCase]
    public void ThePickupShowsRightAfterTheLastPageAndNotBefore()
    {
        Play play = Start(configure: Configure(WithPages(3), NoTrouble));

        for (int page = 1; page <= 3; page++)
        {
            AssertThat(PickupShown(play)).OverrideFailureMessage($"the pickup showed before page {page}").IsFalse();
            PrintCleanPage(play);
        }

        AssertThat(PickupShown(play)).IsTrue();
    }

    // Counted the way the player finds out: by printing until the stack can be picked up
    [TestCase]
    public async Task PageCountIsAtLeastOneAndNeverDropsAsDifficultyRises()
    {
        int previous = 0;

        for (int difficulty = 0; difficulty <= 10; difficulty++)
        {
            Play play = Start(difficulty, NoTrouble);
            int pages = 0;
            while (!PickupShown(play) && pages < 100)
            {
                PrintCleanPage(play);
                pages++;
            }
            play.Job.Detach();
            await runner.AwaitIdleFrame();

            AssertThat(pages).OverrideFailureMessage($"{pages} pages at difficulty {difficulty}").IsBetween(Math.Max(1, previous), 99);
            previous = pages;
        }
    }

    [TestCase]
    public void AnOrderHasAPageEvenWhenTunedForNone()
    {
        Play play = Start(configure: Configure(WithPages(0), NoTrouble));

        AssertThat(PickupShown(play)).IsFalse();
        PrintCleanPage(play);

        AssertThat(PickupShown(play)).IsTrue();
    }

    // ---- What the buttons do ----

    [TestCase]
    public void FunctionButtonsDoNothingWhileThePrinterIsReady()
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble));

        foreach (int button in play.FunctionButtons) { Press(play, button); }

        AssertThat(Printing(play)).IsFalse();
        PrintCleanPage(play);
        AssertThat(PickupShown(play)).IsTrue();
    }

    [TestCase]
    public void NoButtonChangesAPrintWithoutTrouble()
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble));
        PressPrint(play);

        foreach (int button in play.FunctionButtons) { Press(play, button); }

        AssertThat(Printing(play)).IsTrue();
        Expire(play.PrintTimer);
        AssertThat(PickupShown(play)).IsTrue();
    }

    // ---- Trouble and the warning ----

    // Nothing gives the trouble away at PRINT; partway through, the warning button's light (and only it)
    // comes up and the print goes on. Over 10 prints, so a light that ignored the drawn button can't pass
    // by matching it
    [TestCase]
    public void TroubleShowsAWarningOnOneFunctionButtonPartwayThroughThePrint()
    {
        Play play = Start(configure: Configure(WithPages(10), AlwaysTrouble));

        for (int print = 0; print < 10; print++)
        {
            PressPrint(play);
            StepTweens(play.Job, 0.02);
            AssertThat(LitFunctionButtons(play)).IsEmpty();

            Expire(play.PrintTimer);
            StepTweens(play.Job, 0.02);

            AssertThat(Printing(play)).IsTrue();
            AssertThat(play.FunctionButtons).Contains(WarningButtonOf(play));
            AssertThat(LitFunctionButtons(play)).ContainsExactly(WarningButtonOf(play));
            Press(play, WarningButtonOf(play));
            Expire(play.PrintTimer);
        }
    }

    [TestCase]
    public void PressingTheWarningButtonInTimeSavesThePage()
    {
        Play play = Start(configure: Configure(WithPages(1), AlwaysTrouble));
        PressPrint(play);
        Expire(play.PrintTimer);

        Press(play, WarningButtonOf(play));

        AssertThat(Printing(play)).IsTrue();
        Expire(play.PrintTimer);
        AssertThat(PickupShown(play)).IsTrue();
    }

    // The catch can land just as the window closes, with no print left to run: a timer can't be started for
    // that, so the page ends there and then. The time left is emptied by hand, as no time passes here
    [TestCase]
    public void ACatchWithNoPrintLeftFinishesThePageAtOnce()
    {
        Play play = Start(configure: Configure(WithPages(1), AlwaysTrouble));
        PressPrint(play);
        Expire(play.PrintTimer);

        play.Job.Set("_printSeconds", 0f);
        Press(play, WarningButtonOf(play));

        AssertThat(Printing(play)).IsFalse();
        AssertThat(PickupShown(play)).IsTrue();
    }

    [TestCase]
    public void PrintIsIgnoredDuringTheWarning()
    {
        Play play = Start(configure: Configure(WithPages(1), AlwaysTrouble));
        PressPrint(play);
        Expire(play.PrintTimer);

        PressPrint(play);

        Press(play, WarningButtonOf(play));
        Expire(play.PrintTimer);
        AssertThat(PickupShown(play)).IsTrue();
    }

    [TestCase]
    public void AWrongButtonDuringTheWarningJams()
    {
        Play play = Start(configure: Configure(WithPages(1), AlwaysTrouble));
        PressPrint(play);
        Expire(play.PrintTimer);

        Press(play, play.FunctionButtons.First(button => button != WarningButtonOf(play)));

        AssertJammed(play);
    }

    [TestCase]
    public void AMissedWarningJams()
    {
        Play play = Start(configure: Configure(WithPages(1), AlwaysTrouble));

        PrintIntoAJam(play);

        AssertJammed(play);
    }

    // Which button warns is a uniform random pick: over 60 prints every function button comes up (missing
    // one of three is a ~1e-10 event)
    [TestCase]
    public void TheWarningCanComeUpOnEveryFunctionButton()
    {
        Play play = Start(configure: Configure(WithPages(60), AlwaysTrouble));
        var warned = new HashSet<int>();

        for (int print = 0; print < 60; print++)
        {
            PressPrint(play);
            Expire(play.PrintTimer);
            warned.Add(WarningButtonOf(play));
            Press(play, WarningButtonOf(play));
            Expire(play.PrintTimer);
        }

        AssertThat(warned.OrderBy(button => button)).ContainsExactly(play.FunctionButtons.ToArray());
    }

    // ---- Jams and their code ----

    // PRINT replays the code, so a code using it could never be entered. Over 20 jams a PRINT would turn up,
    // and a function button left out would show
    [TestCase]
    public void CodesDrawFromEveryFunctionButtonAndNeverPrint()
    {
        Play play = Start(configure: Configure(WithPages(99), AlwaysTrouble));
        var used = new HashSet<int>();

        for (int jam = 0; jam < 20; jam++)
        {
            PrintIntoAJam(play);
            used.UnionWith(CodeOf(play));
            ClearJam(play);
        }

        AssertThat(used.OrderBy(button => button)).ContainsExactly(play.FunctionButtons.ToArray());
    }

    // What the player reads: the playback flashes exactly the code's buttons, in order, repeats included
    [TestCase]
    public void ThePlaybackFlashesTheCodesButtonsInOrder()
    {
        Play play = Start(configure: Configure(AlwaysTrouble, WithPlaybackTimings(0.2f, 0.3f, 0.2f)));
        PrintIntoAJam(play);
        var flashed = new List<int>();
        bool[] wasLit = new bool[play.Panel.ButtonCount];

        StepTweens(play.Job, 0.2 + CodeOf(play).Count * 0.5 + 0.5, () =>
        {
            foreach (int button in play.FunctionButtons)
            {
                bool lit = IsLit(play.Panel.GetLight(button));
                if (lit && !wasLit[button]) { flashed.Add(button); }
                wasLit[button] = lit;
            }
        });

        AssertThat(flashed).ContainsExactly(CodeOf(play).ToArray());
    }

    [TestCase]
    public void EnteringTheCodeClearsTheJamButTheJammedPageDoesNotCount()
    {
        Play play = Start(configure: Configure(WithPages(1), TroubleOnFirstPrintOnly));
        PrintIntoAJam(play);

        ClearJam(play);

        AssertThat(PickupShown(play)).IsFalse();
        PrintCleanPage(play);
        AssertThat(PickupShown(play)).IsTrue();
    }

    [TestCase]
    public void AWrongPressRestartsTheEntryButKeepsTheCode()
    {
        Play play = Start(configure: AlwaysTrouble);
        PrintIntoAJam(play);
        int[] code = CodeOf(play).ToArray();
        FinishPlayback(play);

        EnterCode(play, code.SkipLast(1));
        Press(play, play.FunctionButtons.First(button => button != code[^1]));

        AssertThat(CodeOf(play)).ContainsExactly(code);
        EnterCode(play, code);
        AssertJamCleared(play);
    }

    // A press mid-replay must not count as the code's first button: the rest of the code then falls short
    [TestCase]
    public void PressesDuringAReplayAreIgnored()
    {
        Play play = Start(configure: AlwaysTrouble);
        PrintIntoAJam(play);
        int[] code = CodeOf(play).ToArray();
        FinishPlayback(play);

        PressPrint(play);
        Press(play, code[0]);
        FinishPlayback(play);
        EnterCode(play, code.Skip(1));

        AssertStillJammed(play);
    }

    // A player who replays the code halfway through entering it starts again from its first button. A
    // code of one repeated button would clear even if the entry carried on, so jams are drawn until the
    // code has two different buttons
    [TestCase]
    public void AReplayRestartsTheEntry()
    {
        Play play = Start(configure: Configure(WithPages(99), AlwaysTrouble));
        PrintIntoAJam(play);
        for (int jam = 0; CodeOf(play).Distinct().Count() < 2; jam++)
        {
            AssertThat(jam).OverrideFailureMessage("every code was one repeated button").IsLess(30);
            ClearJam(play);
            PrintIntoAJam(play);
        }
        int[] code = CodeOf(play).ToArray();
        FinishPlayback(play);

        Press(play, code[0]);
        PressPrint(play);
        FinishPlayback(play);
        EnterCode(play, code);

        AssertJamCleared(play);
    }

    [TestCase]
    public async Task CodeLengthIsAtLeastOneAndNeverDropsAsDifficultyRises()
    {
        int previous = 0;

        for (int difficulty = 0; difficulty <= 10; difficulty++)
        {
            Play play = Start(difficulty, AlwaysTrouble);
            PrintIntoAJam(play);
            int length = CodeOf(play).Count;
            play.Job.Detach();
            await runner.AwaitIdleFrame();

            AssertThat(length).OverrideFailureMessage($"a code of {length} at difficulty {difficulty}").IsGreaterEqual(Math.Max(1, previous));
            previous = length;
        }
    }

    // ---- The odds (values forced by knobs, no balance pinned) ----

    // The odds slide on trouble (luck), not on whether it was caught (skill): with odds that alternate
    // clean / trouble, the print after a trouble is clean whatever the player did about it
    [TestCase("caught")]
    [TestCase("missed")]
    public void OddsMoveOnTroubleNotOnTheCatch(string warning)
    {
        Play play = Start(configure: Configure(WithPages(5), AlternatingTrouble));
        PrintCleanPage(play);
        AssertThat(Printing(play)).OverrideFailureMessage("print 1 had trouble").IsFalse();

        PressPrint(play);
        Expire(play.PrintTimer);
        AssertThat(Printing(play)).OverrideFailureMessage("print 2 had no trouble").IsTrue();
        if (warning == "caught")
        {
            Press(play, WarningButtonOf(play));
            Expire(play.PrintTimer);
        }
        else
        {
            Expire(play.PrintTimer);
            ClearJam(play);
        }

        PrintCleanPage(play);
        AssertThat(Printing(play)).OverrideFailureMessage("print 3 had trouble").IsFalse();
    }

    // ---- The Office lock (what a Distraction does) ----

    // Timers keep running while the player is away: the warning runs out and jams, and the code plays
    // unseen. Back at the printer, PRINT replays it
    [TestCase]
    public void AWarningMissedWhileLockedJamsAndTheCodeCanBeReplayedAfter()
    {
        Play play = Start(configure: Configure(WithPages(1), TroubleOnFirstPrintOnly));
        PressPrint(play);
        Expire(play.PrintTimer);

        office.InputLocked = true;
        Press(play, WarningButtonOf(play));
        Expire(play.PrintTimer);
        AssertJammed(play);
        int[] code = CodeOf(play).ToArray();
        FinishPlayback(play);
        office.InputLocked = false;

        PressPrint(play);
        FinishPlayback(play);
        EnterCode(play, code);
        PrintCleanPage(play);

        AssertThat(PickupShown(play)).IsTrue();
    }

    // ---- The page in the tray (how the tray shows it is OutputTrayTesting's; here, that the job drives it) ----

    [TestCase]
    public void APrintedPageComesOutAndJoinsTheStack()
    {
        Play play = Start(configure: Configure(WithPages(2), NoTrouble));

        PressPrint(play);
        AssertThat(PageShown(play)).IsTrue();

        Expire(play.PrintTimer);
        AssertThat(PageShown(play)).IsFalse();
        AssertThat(SheetsInTheStack(play)).IsEqual(1);
    }

    [TestCase]
    public void AJammedPageStaysPutUntilTheJamIsCleared()
    {
        Play play = Start(configure: Configure(WithPages(2), TroubleOnFirstPrintOnly));
        PrintIntoAJam(play);
        Vector2 stuck = play.Page.Position;

        FinishPlayback(play);

        AssertThat(PageShown(play)).IsTrue();
        // Give or take the tray's own shake
        AssertThat(play.Page.Position.DistanceTo(stuck)).IsLessEqual(play.Tray.Get("_shakeDistance").AsSingle() + 1e-3f);

        EnterCode(play, CodeOf(play));
        StepTweens(play.Job, play.Tray.Get("_discardSeconds").AsSingle() + 0.05);

        AssertThat(PageShown(play)).IsFalse();
    }

    // ---- Picking up and completing ----

    // Emitted straight from the pickup, bypassing the hiding that keeps it unclickable in play
    [TestCase]
    public void APickupBeforeEveryPageIsPrintedDoesNothing()
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble, WithCompletionDelay(0f)));

        play.Pickup.EmitSignal(SpriteInteractable.SignalName.Clicked);

        AssertThat(play.Job.IsCompleted).IsFalse();
        PrintCleanPage(play);
        AssertThat(PickupShown(play)).IsTrue();
    }

    [TestCase]
    public void PickingUpTheStackCompletesTheJobAfterTheDelay()
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble, WithCompletionDelay(1f)));
        PrintCleanPage(play);

        PickUp(play);

        AssertThat(PickupShown(play)).IsFalse();
        AssertThat(play.Job.IsCompleted).IsFalse();
        Expire(play.CompletionTimer);
        AssertThat(play.Job.IsCompleted).IsTrue();
    }

    [TestCase]
    public void WithNoDelayPickingUpCompletesAtOnce()
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble, WithCompletionDelay(0f)));
        PrintCleanPage(play);

        PickUp(play);

        AssertThat(play.Job.IsCompleted).IsTrue();
    }

    [TestCase]
    public void OnceEveryPageIsPrintedPrintStartsNoPage()
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble));
        PrintCleanPage(play);

        PressPrint(play);

        AssertThat(Printing(play)).IsFalse();
        AssertThat(PickupShown(play)).IsTrue();
    }

    // Job timers run on while the player is away, so the last page can finish then: the stack can't be
    // taken until they are back, and is still there when they are
    [TestCase("locked")]
    [TestCase("on another screen")]
    public void TheLastPageFinishingWhileAwayWaitsToBePickedUp(string away)
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble, WithCompletionDelay(0f)));
        PressPrint(play);
        if (away == "locked") { office.InputLocked = true; }
        else { office.GoToScreen("MainScreen"); }

        Expire(play.PrintTimer);
        PickUp(play);
        AssertThat(play.Job.IsCompleted).IsFalse();

        if (away == "locked") { office.InputLocked = false; }
        else { office.GoToScreen("PrinterScreen"); }
        PickUp(play);
        AssertThat(play.Job.IsCompleted).IsTrue();
    }

    // Complete is public and the panel outlives the job: whatever arrives after it moves nothing
    [TestCase("a button")]
    [TestCase("the print timer")]
    [TestCase("the pickup")]
    public void AfterCompletionNothingMovesTheJob(string arriving)
    {
        Play play = Start(configure: Configure(WithPages(1), NoTrouble));
        if (arriving == "the print timer") { PressPrint(play); }
        if (arriving == "the pickup") { PrintCleanPage(play); }
        bool pickupShown = PickupShown(play);
        play.Job.Complete();

        switch (arriving)
        {
            case "a button": PressPrint(play); break;
            case "the print timer": Expire(play.PrintTimer); break;
            case "the pickup": PickUp(play); break;
        }

        AssertThat(Printing(play)).IsFalse();
        AssertThat(PickupShown(play)).IsEqual(pickupShown);
    }

    // ---- Leaving the resident panel as found ----

    [TestCase]
    public void DetachingMidWarningLeavesThePanelAsFound()
    {
        PrinterPanel panel = office.TryGetScreen("PrinterScreen")!.GetFixture<PrinterPanel>("PrinterPanel");
        int subscribers = Subscribers(panel);
        Play play = Start(configure: AlwaysTrouble);
        PressPrint(play);
        Expire(play.PrintTimer);
        StepTweens(play.Job, 0.02);
        AssertThat(AnyLightLit(play)).IsTrue();

        play.Job.Detach();

        var lit = new List<bool>();
        StepTweens(panel, 1, () => lit.Add(AnyLightLit(play)));
        AssertThat(lit).NotContains(true);
        AssertThat(Subscribers(panel)).IsEqual(subscribers);
    }

    // The safety net path (the job freed mid-jam, e.g. with the level): its own playback must not keep
    // flashing resident lights after the panel was reset
    [TestCase]
    public async Task FreeingTheJobMidPlaybackLeavesTheLightsAtRest()
    {
        Play play = Start(configure: Configure(AlwaysTrouble, WithPlaybackTimings(0.2f, 0.3f, 0.2f)));
        PrintIntoAJam(play);
        StepTweens(play.Job, 0.3);
        AssertThat(AnyLightLit(play)).IsTrue();

        play.Job.QueueFree();
        await runner.AwaitIdleFrame();

        var lit = new List<bool>();
        StepTweens(play.Panel, 3, () => lit.Add(AnyLightLit(play)));
        AssertThat(lit).NotContains(true);
    }

    // ---- Through the real manager ----

    // The way the level plays it: requested by type from the real job_manager.tscn (its default factory,
    // shipped balance and odds), announced, started on PrinterScreen, played to the pickup, completed, and
    // the screen and its panel left as they were. The Visitor's walk is skipped by finishing it directly
    [TestCase]
    public async Task PlaysThroughTheRealManager()
    {
        JobManager manager = GD.Load<PackedScene>(JobManagerPath).Instantiate<JobManager>();
        manager.GetNode("Scheduler").Free();
        manager.ScreenResolver = office.TryGetScreen;
        office.AddChild(manager);
        var log = new EventLog();
        log.WatchJobs(manager);
        OfficeScreen printer = office.TryGetScreen("PrinterScreen")!;
        PrinterPanel panel = printer.GetFixture<PrinterPanel>("PrinterPanel");
        int baseline = CountDescendants(printer);
        int subscribers = Subscribers(panel);
        office.GoToScreen(printer);

        AssertThat(manager.RequestJob(JobType.PrintReport)).IsTrue();
        manager.GetChildren().OfType<JobAnnouncement>().Single().Finish();
        AssertThat(manager.ActiveJob).IsInstanceOf<PrintReportJob>();
        manager.ActiveJob!.Set("_completionDelay", 0f);
        Play play = PlayOn((PrintReportJob)manager.ActiveJob, printer);
        PrintEveryPage(play);
        PickUp(play);
        await runner.AwaitIdleFrame();

        AssertThat(log.Entries).ContainsExactly("Announcing@Announcing", "Started@Active", "Completed@Idle");
        AssertThat(CountDescendants(printer)).IsEqual(baseline);
        AssertThat(Enumerable.Range(0, panel.ButtonCount).All(i => AtRest(panel.GetLight(i)))).IsTrue();
        AssertThat(Subscribers(panel)).IsEqual(subscribers);
    }
}
