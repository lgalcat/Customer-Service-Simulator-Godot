using System;
using System.Linq;
using System.Threading.Tasks;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// Runs the JobTesting contract against the real Fill Document job, on the real office: its parts must
// fit ComputerScreen's anchors, follow the screen's input state (its blanks and Submit are GUI Control
// interactables) and leave nothing behind. Its announcement (the Visitor) is checked for attaching on
// MainScreen only; its full contract is left to a Visitor suite (not written yet). Also holds the job's
// own authoring guards (below the contract block)
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class FillDocumentJobTesting : JobTesting
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
    [TestCase("Document")]
    [TestCase("RecordView")]
    [TestCase("CompletionDelay")]
    public void SetupWithAMissingPartThrowsNamingIt(string missing)
    {
        FillDocumentJob job = AutoFree(InstantiateJob())!;
        job.GetNode(missing).Free();

        AssertThrown(() => job.Setup(1)).IsInstanceOf<NullReferenceException>()
            .HasMessage($"FillDocumentJob: missing required child '{missing}'");
    }

    [TestCase]
    public void SetupRejectsADocumentLineWithoutASpec()
    {
        FillDocumentJob job = AutoFree(InstantiateJob())!;
        DocumentLine line = Descendants<DocumentLine>(job).First();
        line.Spec = null;

        AssertThrown(() => job.Setup(1)).IsInstanceOf<InvalidOperationException>()
            .HasMessage($"FillDocumentJob: document line '{line.Name}' has no Spec");
    }
}
