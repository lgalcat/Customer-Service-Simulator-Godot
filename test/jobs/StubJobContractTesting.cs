using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static JobTestKit;

// Runs the JobTesting contract against StubJob, so every inherited test body is exercised (and has
// been seen to fail) before any real job exists. The real office gets a TestAnchor on every screen
// (none are authored yet); the stub places one clickable part there and declares an announcement, so
// no case passes vacuously. Shape to copy for a concrete job's <Feature>JobTesting
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class StubJobContractTesting : JobTesting
{
    protected override Job CreateJob()
    {
        PackedScene announcement = StubJobAnnouncement.Pack(a =>
        {
            a.Target = "MainScreen";
            a.PartAnchor = TestAnchor;
        }, InteractablePart("AnnouncementPart"));
        PackedScene scene = StubJob.Pack(j =>
        {
            j.Target = "ComputerScreen";
            j.PartAnchor = TestAnchor;
            j.AnnouncementScene = announcement;
        }, InteractablePart());
        return scene.Instantiate<StubJob>();
    }

    protected override OfficeManager InstantiateOffice() => InstantiateOfficeWithTestAnchors();

    [BeforeTest]
    public override void Setup() => base.Setup();

    [AfterTest]
    public override void Teardown() => base.Teardown();

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
    public override async Task DetachGivesBackEverythingItPlaced() => await base.DetachGivesBackEverythingItPlaced();

    [TestCase]
    public override async Task FreeingTheJobGivesBackItsContent() => await base.FreeingTheJobGivesBackItsContent();

    [TestCase]
    public override async Task AnnouncementSceneIsPlayable() => await base.AnnouncementSceneIsPlayable();
}
