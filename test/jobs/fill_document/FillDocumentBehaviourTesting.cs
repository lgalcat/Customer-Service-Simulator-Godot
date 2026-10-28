using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;
using static JobTestKit;

// Fill Document's game rules, played on the real ComputerScreen of the real office: copy each blank from
// the monitor, submit, get each blank marked; the form is approved (and the job completes after a short
// delay) only once every blank is right, and nothing but approval closes it. Expected values are read
// off the monitor, as the player does, so the record shown and the record checked are proven to be the
// same. Nothing here pins the data, the number of blanks or the look: blank counts are checked as bounds
// and monotonicity, balance knobs are set through their [Export]s where a case needs a known count, and
// time is driven by emitting the completion timer's Timeout.
// The job touches no resident device, so there is no device state to check restored; the contract's
// descendant count covers what it places.
// Extends JobTesting for its office/runner setup only; redeclares none of its cases
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class FillDocumentBehaviourTesting : JobTesting
{
    protected override Job CreateJob() => InstantiateJob();

    [BeforeTest]
    public override void Setup() => base.Setup();

    [AfterTest]
    public override void Teardown() => base.Teardown();

    private Play Start(int difficulty = 1, Action<Job>? configure = null) => FillDocumentTestKit.Start(office, difficulty, configure);

    private static void AssertFormOpen(Play play)
    {
        AssertThat(StampShown(play.Document)).IsFalse();
        AssertThat(play.Blanks.Any(blank => blank.InputField.Locked || !blank.InputField.Editable)).IsFalse();
        AssertThat(SubmitOf(play.Document).Disabled).IsFalse();
        AssertThat(play.Job.IsCompleted).IsFalse();
        AssertThat(play.CompletionTimer.IsStopped()).IsTrue();
    }

    private static void AssertFormClosed(Play play)
    {
        AssertThat(play.Document.Lines.All(line => !line.InputField.Editable)).IsTrue();
        AssertThat(SubmitOf(play.Document).Disabled).IsTrue();
    }

    // ---- Starting state ----

    // Looked up across the whole screen, not at the anchors (where Start's own lookup already expects them)
    [TestCase]
    public void PlacesOneFormOnTheDeskAndOneRecordOnTheMonitor()
    {
        Play play = Start();

        AssertThat(Descendants<DocumentView>(play.Screen).Select(form => form.GetParent().Name.ToString())).ContainsExactly("DocumentAnchor");
        AssertThat(Descendants<RecordView>(play.Screen).Select(record => record.GetParent().Name.ToString())).ContainsExactly("MonitorAnchor");
    }

    [TestCase]
    public void TheFormStartsWithEmptyUnmarkedBlanks()
    {
        Play play = Start();

        AssertThat(play.Blanks).IsNotEmpty();
        AssertThat(play.Blanks.All(blank => blank.InputText == "" && MarkOf(blank) == Mark.None)).IsTrue();
        AssertThat(StampShown(play.Document)).IsFalse();
    }

    // The record behind the form is the record on the monitor
    [TestCase]
    public void PrefilledLinesShowTheMonitorsValues()
    {
        Play play = Start();
        IReadOnlyDictionary<string, string> monitor = MonitorValues(play.Monitor);

        foreach (DocumentLine line in play.Document.Lines.Where(line => !line.IsBlank))
        {
            AssertThat(ShownValue(line)).OverrideFailureMessage($"'{CaptionOf(line)}' differs from the monitor").IsEqual(monitor[CaptionOf(line)]);
        }
    }

    // Every blank can be looked up
    [TestCase]
    public void TheMonitorListsEveryLineOfTheForm()
    {
        Play play = Start();

        AssertThat(MonitorRows(play.Monitor).Select(row => row.Caption)).ContainsExactly(play.Document.Lines.Select(CaptionOf).ToArray());
    }

    // ---- How many blanks (placeholder balance: only its shape is pinned) ----

    [TestCase]
    public void BlankCountStaysWithinTheFormAndNeverDropsAsDifficultyRises()
    {
        Job template = CreateJob();
        int lines = Descendants<DocumentView>(template).Single().Lines.Count;
        template.Free();
        int previous = 0;

        for (int difficulty = 0; difficulty <= lines + 2; difficulty++)
        {
            Job job = CreateJob();
            job.Setup(difficulty);
            int blanks = Descendants<DocumentView>(job).Single().Lines.Count(line => line.IsBlank);
            job.Free();

            AssertThat(blanks).OverrideFailureMessage($"{blanks} blanks at difficulty {difficulty}").IsBetween(Math.Max(1, previous), lines);
            previous = blanks;
        }
    }

    // A form with no blank would be approved by its first, empty, submit
    [TestCase]
    public void AFormHasABlankEvenWhenTunedForNone()
    {
        Play play = Start(configure: WithBlanks(0));

        AssertThat(play.Blanks.Length).IsGreaterEqual(1);
    }

    // Which lines are blanked is a uniform random pick: over many forms with one blank each, every line
    // is chosen at some point (a line missed in 200 draws of 1-in-8 is a ~1e-11 event)
    [TestCase]
    public void EveryLineOfTheFormCanBeTheBlank()
    {
        var blanked = new HashSet<string>();
        string[] captions = Array.Empty<string>();

        for (int i = 0; i < 200; i++)
        {
            Job job = CreateJob();
            WithBlanks(1)(job);
            job.Setup(1);
            DocumentView document = Descendants<DocumentView>(job).Single();
            captions = document.Lines.Select(CaptionOf).ToArray();
            blanked.UnionWith(document.Lines.Where(line => line.IsBlank).Select(CaptionOf));
            job.Free();
        }

        AssertThat(blanked).ContainsExactlyInAnyOrder(captions);
    }

    // ---- Submitting ----

    [TestCase]
    public void CopyingTheMonitorAndSubmittingApprovesTheForm()
    {
        Play play = Start();
        CopyFromMonitor(play);

        PressSubmit(play.Document);

        AssertThat(play.Blanks.All(blank => MarkOf(blank) == Mark.Correct)).IsTrue();
        AssertThat(StampShown(play.Document)).IsTrue();
        AssertFormClosed(play);
    }

    // The approved paper stays on the desk a moment before the job ends and takes it away
    [TestCase]
    public void TheJobCompletesWhenTheApprovedFormsDelayRunsOut()
    {
        Play play = Start();
        int completions = 0;
        play.Job.OnCompleted = () => completions++;
        CopyFromMonitor(play);

        PressSubmit(play.Document);

        AssertThat(play.Job.IsCompleted).IsFalse();
        AssertThat(play.CompletionTimer.IsStopped()).IsFalse();

        play.CompletionTimer.EmitSignal(Timer.SignalName.Timeout);

        AssertThat(play.Job.IsCompleted).IsTrue();
        AssertThat(completions).IsEqual(1);
    }

    [TestCase]
    public void WithNoDelayTheApprovingSubmitCompletesTheJob()
    {
        Play play = Start(configure: WithCompletionDelay(0f));
        CopyFromMonitor(play);

        PressSubmit(play.Document);

        AssertThat(play.Job.IsCompleted).IsTrue();
    }

    // Every line blank, so some typed value really differs from the monitor (digit-only fields don't)
    [TestCase]
    public void TypingInLowerCaseCounts()
    {
        Play play = Start(configure: WithBlanks(EveryLine));
        AssertThat(play.Blanks.Any(blank => ValueFor(play, blank) != ValueFor(play, blank).ToLowerInvariant())).IsTrue();
        CopyFromMonitor(play, value => value.ToLowerInvariant());

        PressSubmit(play.Document);

        AssertThat(StampShown(play.Document)).IsTrue();
    }

    [TestCase]
    public void AWrongBlankIsMarkedAndTheFormStaysOpen()
    {
        Play play = Start(configure: WithBlanks(2));
        CopyFromMonitor(play);
        DocumentLine wrong = play.Blanks[0];
        Edit(wrong, WrongVersionOf(ValueFor(play, wrong)));

        PressSubmit(play.Document);

        AssertThat(MarkOf(wrong)).IsEqual(Mark.Wrong);
        AssertThat(MarkOf(play.Blanks[1])).IsEqual(Mark.Correct);
        AssertFormOpen(play);
    }

    [TestCase]
    public void AnEmptySubmitMarksEveryBlankWrong()
    {
        Play play = Start(configure: WithBlanks(2));

        PressSubmit(play.Document);

        AssertThat(play.Blanks.All(blank => MarkOf(blank) == Mark.Wrong)).IsTrue();
        AssertFormOpen(play);
    }

    // Retries are unlimited
    [TestCase]
    public void FixingTheWrongBlankAndSubmittingAgainApproves()
    {
        Play play = Start(configure: WithBlanks(2));
        CopyFromMonitor(play);
        DocumentLine wrong = play.Blanks[0];
        Edit(wrong, WrongVersionOf(ValueFor(play, wrong)));
        PressSubmit(play.Document);

        Edit(wrong, ValueFor(play, wrong));
        PressSubmit(play.Document);

        AssertThat(StampShown(play.Document)).IsTrue();
    }

    // A submit locks nothing (user decision: being distracted into breaking a correct value fits the
    // game), so only a submit where everything is right at that moment approves
    [TestCase]
    public void ACorrectBlankStaysEditableAndBreakingItBlocksApproval()
    {
        Play play = Start(configure: WithBlanks(2));
        DocumentLine first = play.Blanks[0], second = play.Blanks[1];
        Edit(first, ValueFor(play, first));
        Edit(second, WrongVersionOf(ValueFor(play, second)));
        PressSubmit(play.Document);

        AssertThat(MarkOf(first)).IsEqual(Mark.Correct);
        AssertThat(first.InputField.Editable).IsTrue();

        Edit(first, WrongVersionOf(ValueFor(play, first)));
        Edit(second, ValueFor(play, second));
        PressSubmit(play.Document);

        AssertThat(MarkOf(first)).IsEqual(Mark.Wrong);
        AssertThat(MarkOf(second)).IsEqual(Mark.Correct);
        AssertFormOpen(play);
    }

    // ---- After approval ----

    // The button is disabled, but whatever reaches the job (here, the signal itself) must not re-mark the
    // form from what the fields now hold
    [TestCase]
    public void ASubmitAfterApprovalChangesNothing()
    {
        Play play = Start();
        CopyFromMonitor(play);
        PressSubmit(play.Document);
        DocumentLine blank = play.Blanks[0];
        blank.InputField.Text = WrongVersionOf(ValueFor(play, blank));
        AssertThat(blank.InputText).IsEqual(WrongVersionOf(ValueFor(play, blank)));

        PressSubmit(play.Document);

        AssertThat(MarkOf(blank)).IsEqual(Mark.Correct);
        AssertThat(StampShown(play.Document)).IsTrue();
    }

    // An approved form is closed for good, even if the office's input is switched off and on (a
    // Distraction) before the job takes it away
    [TestCase]
    public void AnInputLockCycleLeavesTheApprovedFormClosed()
    {
        Play play = Start();
        CopyFromMonitor(play);
        PressSubmit(play.Document);

        office.InputLocked = true;
        office.InputLocked = false;

        AssertFormClosed(play);
    }

    // ---- The office locked mid-typing (a Distraction starting) ----

    // The lock blocks the mouse; keystrokes would still reach a focused field, so the field must give
    // focus up. Unlocking makes the form usable again but leaves focus to the player
    [TestCase]
    public void LockingTheOfficeMidTypingTakesFocusAwayAndUnlockingDoesNotGiveItBack()
    {
        Play play = Start();
        DocumentLine typing = play.Blanks[0];
        typing.FocusInput();
        AssertThat(typing.InputField.HasFocus()).IsTrue();

        office.InputLocked = true;

        AssertThat(typing.InputField.HasFocus()).IsFalse();
        AssertThat(play.Blanks.Any(blank => blank.InputField.Editable)).IsFalse();
        AssertThat(SubmitOf(play.Document).Disabled).IsTrue();

        office.InputLocked = false;

        AssertThat(play.Blanks.All(blank => blank.InputField.Editable)).IsTrue();
        AssertThat(SubmitOf(play.Document).Disabled).IsFalse();
        // Anywhere on the form: the engine lets even a hidden field (a pre-filled line's) take focus
        Control? focused = play.Document.GetViewport().GuiGetFocusOwner();
        AssertThat(focused != null && play.Document.IsAncestorOf(focused)).OverrideFailureMessage($"'{focused?.Name}' has focus").IsFalse();
    }

    // ---- Through the real manager ----

    // The way the level plays it: requested by type from the real job_manager.tscn (its default factory),
    // announced, started on ComputerScreen, completed, and the screen left as it was. The Visitor's walk
    // is skipped by finishing the announcement directly
    [TestCase]
    public async Task PlaysThroughTheRealManager()
    {
        JobManager manager = GD.Load<PackedScene>(JobManagerPath).Instantiate<JobManager>();
        manager.GetNode("Scheduler").Free();
        manager.ScreenResolver = office.TryGetScreen;
        office.AddChild(manager);
        var log = new EventLog();
        log.WatchJobs(manager);
        OfficeScreen computer = office.TryGetScreen("ComputerScreen")!;
        int baseline = CountDescendants(computer);
        office.GoToScreen(computer);

        AssertThat(manager.RequestJob(JobType.FillDocument)).IsTrue();
        manager.GetChildren().OfType<JobAnnouncement>().Single().Finish();
        AssertThat(manager.ActiveJob).IsInstanceOf<FillDocumentJob>();
        manager.ActiveJob!.Set("_completionDelay", 0f);
        Play play = PlayOn((FillDocumentJob)manager.ActiveJob, computer);
        CopyFromMonitor(play);
        PressSubmit(play.Document);
        await runner.AwaitIdleFrame();

        AssertThat(log.Entries).ContainsExactly("Announcing@Announcing", "Started@Active", "Completed@Idle");
        AssertThat(CountDescendants(computer)).IsEqual(baseline);
    }
}
