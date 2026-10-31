using System;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static PrintReportTestKit;

// Runs the JobTesting contract against the real Print Report job, on the real office: its parts must fit
// PrinterScreen's anchors and panel, follow the screen's input state (the pickup is a SpriteInteractable)
// and leave nothing behind. Its announcement (the Visitor's Print Report variant) is checked for attaching
// on MainScreen only; its full contract is left to a Visitor suite (not written yet). Also holds the job's
// own authoring guards (below the contract block)
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class PrintReportJobTesting : JobTesting
{
    protected override Job CreateJob() => InstantiateJob();

    [BeforeTest]
    public override void Setup() => base.Setup();

    [AfterTest]
    public override void Teardown() => base.Teardown();


    // Block containing base calls to inherited TestCases, no class specific logic should be present further down
    [TestCase]
    public override void SetupAssignsDifficulty() => base.SetupAssignsDifficulty();

    [TestCase]
    public override void TargetScreenExistsInTheOffice() => base.TargetScreenExistsInTheOffice();

    [TestCase]
    public override void AttachesToItsTargetScreen() => base.AttachesToItsTargetScreen();

    [TestCase]
    public override void EntersTheTreeAfterAttachWithoutErrors() => base.EntersTheTreeAfterAttachWithoutErrors();

    [TestCase]
    public override void PlacedInteractablesFollowTheScreensInputState() => base.PlacedInteractablesFollowTheScreensInputState();

    [TestCase]
    public override void ControlsThatTakeNoInputLetClicksThrough() => base.ControlsThatTakeNoInputLetClicksThrough();

    [TestCase]
    public override async Task DetachGivesBackEverythingItPlaced() => await base.DetachGivesBackEverythingItPlaced();

    [TestCase]
    public override async Task FreeingTheJobGivesBackItsContent() => await base.FreeingTheJobGivesBackItsContent();

    [TestCase]
    public override async Task AnnouncementSceneIsPlayable() => await base.AnnouncementSceneIsPlayable();


    // The job's own authoring guards: a mis-built job scene fails in Setup, naming what is wrong, rather
    // than mid-play. Each test builds its own job (freeing part of a [BeforeTest] fixture is reported as
    // orphans)
    [TestCase("Tray")]
    [TestCase("Ticket")]
    [TestCase("PrintTimer")]
    [TestCase("CompletionDelay")]
    public void SetupWithAMissingPartThrowsNamingIt(string missing)
    {
        PrintReportJob job = AutoFree(InstantiateJob())!;
        job.GetNode(missing).Free();

        AssertThrown(() => job.Setup(1)).IsInstanceOf<NullReferenceException>()
            .HasMessage($"PrintReportJob: missing required child '{missing}'");
    }

    // The scene run on its own (F6) has no panel to drive: it sits idle, it doesn't throw from _Ready (which
    // the class-level monitor would report)
    [TestCase(true)]
    [TestCase(false)]
    public void EnteringTheTreeUnattachedDoesNothing(bool setUp)
    {
        PrintReportJob job = AutoFree(InstantiateJob())!;
        if (setUp) { job.Setup(1); }

        office.AddChild(job);

        AssertThat(job.GetNode<Timer>("PrintTimer").IsStopped()).IsTrue();
    }
}
