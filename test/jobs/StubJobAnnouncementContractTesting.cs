using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static JobTestKit;

// Runs the JobAnnouncementTesting contract against StubJobAnnouncement (one clickable part on the
// real MainScreen's TestAnchor, finishing as soon as it plays), so every inherited test body is
// exercised before any real announcement exists. Shape to copy for a concrete announcement's suite
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class StubJobAnnouncementContractTesting : JobAnnouncementTesting
{
    protected override JobAnnouncement CreateAnnouncement()
    {
        PackedScene scene = StubJobAnnouncement.Pack(a =>
        {
            a.Target = "MainScreen";
            a.PartAnchor = TestAnchor;
            a.FinishOnPlay = true;
        }, InteractablePart());
        return scene.Instantiate<StubJobAnnouncement>();
    }

    protected override OfficeManager InstantiateOffice() => InstantiateOfficeWithTestAnchors();

    [BeforeTest]
    public override void Setup() => base.Setup();

    [AfterTest]
    public override void Teardown() => base.Teardown();

    [TestCase]
    public override void TargetScreenExistsInTheOffice() => base.TargetScreenExistsInTheOffice();

    [TestCase]
    public override void AttachesToItsTargetScreen() => base.AttachesToItsTargetScreen();

    [TestCase]
    public override void EntersTheTreeAfterAttachWithoutErrors() => base.EntersTheTreeAfterAttachWithoutErrors();

    [TestCase]
    public override void PlacedInteractablesFollowTheScreensInputState() => base.PlacedInteractablesFollowTheScreensInputState();

    [TestCase]
    public override async Task PlayEventuallyFinishes() => await base.PlayEventuallyFinishes();

    [TestCase]
    public override async Task DetachGivesBackEverythingItPlaced() => await base.DetachGivesBackEverythingItPlaced();

    [TestCase]
    public override async Task FreeingTheAnnouncementGivesBackItsContent() => await base.FreeingTheAnnouncementGivesBackItsContent();
}
