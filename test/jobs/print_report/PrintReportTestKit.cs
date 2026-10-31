using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Godot;
using static OfficeTestKit;

// Shared knobs, drivers and readers for the Print Report suites. Balance and timings are a work in progress:
// a case needing a known count or outcome sets the private [Export] knobs, and time is driven (a timer
// expired by hand, Tweens stepped), never waited. The player's side goes through the real panel. CodeOf and
// WarningButtonOf read off the job what the player reads off the lights; the lights are checked against them
// separately
public static class PrintReportTestKit
{
    public const string JobPath = "res://assets/scenes/jobs/print_report/print_report_job.tscn";

    // The real job, not set up and not in the tree
    public static PrintReportJob InstantiateJob() => GD.Load<PackedScene>(JobPath).Instantiate<PrintReportJob>();

    // ---- Playing the real job ----

    public sealed record Play(PrintReportJob Job, OfficeScreen Screen, PrinterPanel Panel, OutputTray Tray, PrintTicket Ticket)
    {
        public Timer PrintTimer => Job.GetNode<Timer>("PrintTimer");
        public Timer CompletionTimer => Job.GetNode<Timer>("CompletionDelay");
        public SpriteInteractable Pickup => Tray.GetNode<SpriteInteractable>("Pickup");
        public Sprite2D Page => Tray.GetNode<Sprite2D>("Page");

        // Every button but PRINT, by panel index
        public IEnumerable<int> FunctionButtons => Enumerable.Range(PrinterPanel.PrintButton + 1, Panel.ButtonCount - 1);
    }

    // The real job started on PrinterScreen in 'office' (JobTestKit.StartJob), its buttons taking clicks
    public static Play Start(OfficeManager office, int difficulty = 1, Action<Job>? configure = null)
    {
        PrintReportJob job = JobTestKit.StartJob<PrintReportJob>(office, JobPath, difficulty, configure);
        return PlayOn(job, job.Screen!);
    }

    // The parts of a job already started on 'screen', found anywhere under it (where they sit is the
    // placement case's to check)
    public static Play PlayOn(PrintReportJob job, OfficeScreen screen) =>
        new(job, screen, screen.GetFixture<PrinterPanel>("PrinterPanel"), PartOf<OutputTray>(screen), PartOf<PrintTicket>(screen));

    private static T PartOf<T>(OfficeScreen screen) where T : Node => JobTestKit.DescendantsOf(screen).OfType<T>().Single();

    // ---- Knobs (set before Setup) ----

    public static Action<Job> Configure(params Action<Job>[] knobs) => job =>
    {
        foreach (Action<Job> knob in knobs) { knob(job); }
    };

    // Exactly 'pages' pages at any difficulty (the job still asks for at least one)
    public static Action<Job> WithPages(int pages) => job =>
    {
        job.Set("_basePages", pages);
        job.Set("_pagesPerDifficulty", 0);
    };

    public static Action<Job> WithCompletionDelay(float seconds) => job => job.Set("_completionDelay", seconds);

    // How a jam code is played on the lights: a pause, then each button's flash followed by a gap
    public static Action<Job> WithPlaybackTimings(float leadIn, float flash, float gap) => job =>
    {
        job.Set("_codeLeadIn", leadIn);
        job.Set("_codeFlashSeconds", flash);
        job.Set("_codeGapSeconds", gap);
    };

    // The trouble odds, at any difficulty. A chance above 1 always hits (rolls are in [0, 1])
    public static Action<Job> WithOdds(float start, float rise, float drop, float min, float max) => job =>
    {
        job.Set("_startChance", start);
        job.Set("_baseRise", rise);
        job.Set("_risePerDifficulty", 0f);
        job.Set("_drop", drop);
        job.Set("_minChance", min);
        job.Set("_maxChance", max);
    };

    public static readonly Action<Job> NoTrouble = WithOdds(0f, 0f, 0f, 0f, 0f);
    public static readonly Action<Job> AlwaysTrouble = WithOdds(2f, 0f, 0f, 2f, 2f);
    public static readonly Action<Job> TroubleOnFirstPrintOnly = WithOdds(2f, 0f, 2f, 0f, 2f);
    // Clean, trouble, clean, ... as long as the odds slide the way the job is designed to
    public static readonly Action<Job> AlternatingTrouble = WithOdds(0f, 2f, 2f, 0f, 2f);

    // ---- The player's side ----

    // A click on the real button the panel lists at 'index'
    public static void Press(Play play, int index) =>
        Click((SpriteInteractable)play.Panel.Get("_buttons").AsGodotArray<Node>()[index]);

    public static void PressPrint(Play play) => Press(play, PrinterPanel.PrintButton);

    public static void EnterCode(Play play, IEnumerable<int> buttons)
    {
        foreach (int button in buttons) { Press(play, button); }
    }

    public static void PickUp(Play play) => Click(play.Pickup);

    // A one-shot timer running out: it stops, then times out
    public static void Expire(Timer timer)
    {
        timer.Stop();
        timer.EmitSignal(Timer.SignalName.Timeout);
    }

    // With no trouble: PRINT, then the page runs out
    public static void PrintCleanPage(Play play)
    {
        PressPrint(play);
        Expire(play.PrintTimer);
    }

    // With trouble: PRINT, the warning comes up, and runs out
    public static void PrintIntoAJam(Play play)
    {
        PressPrint(play);
        Expire(play.PrintTimer);
        Expire(play.PrintTimer);
    }

    // Lets a code playback run to its end (presses are ignored until then)
    public static void FinishPlayback(Play play)
    {
        float perButton = play.Job.Get("_codeFlashSeconds").AsSingle() + play.Job.Get("_codeGapSeconds").AsSingle();
        StepTweens(play.Job, play.Job.Get("_codeLeadIn").AsSingle() + CodeOf(play).Count * perButton + 0.1);
    }

    // What a player who watched the playback does
    public static void ClearJam(Play play)
    {
        FinishPlayback(play);
        EnterCode(play, CodeOf(play));
    }

    // A competent player under whatever odds the job has: prints, catches every warning, until the pages are
    // ready to collect. Fails rather than loop forever
    public static void PrintEveryPage(Play play)
    {
        for (int prints = 0; !PickupShown(play); prints++)
        {
            GdUnit4.Assertions.AssertThat(prints).OverrideFailureMessage("the pickup never showed").IsLess(100);
            PressPrint(play);
            Expire(play.PrintTimer);
            if (!Printing(play)) { continue; }
            Press(play, WarningButtonOf(play));
            Expire(play.PrintTimer);
        }
    }

    // ---- Reading the job ----

    // A page is being printed (stopped while ready, jammed and once everything is printed)
    public static bool Printing(Play play) => !play.PrintTimer.IsStopped();

    public static bool PickupShown(Play play) => play.Pickup.Visible;

    public static bool PageShown(Play play) => play.Page.Visible;

    public static int SheetsInTheStack(Play play) => play.Tray.GetNode("Stack").GetChildCount();

    public static int[] LitFunctionButtons(Play play) => play.FunctionButtons.Where(i => IsLit(play.Panel.GetLight(i))).ToArray();

    // The current jam's code (empty when not jammed); what the playback shows the player
    public static IReadOnlyList<int> CodeOf(Play play) =>
        (Field("_code").GetValue(play.Job) as JamCode)?.Sequence ?? Array.Empty<int>();

    // The button the current (or last) warning is on; what its blinking light shows the player
    public static int WarningButtonOf(Play play) => (int)Field("_warningButton").GetValue(play.Job)!;

    private static FieldInfo Field(string name) =>
        typeof(PrintReportJob).GetField(name, BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new InvalidOperationException($"PrintReportJob has no field '{name}': update PrintReportTestKit");
}
