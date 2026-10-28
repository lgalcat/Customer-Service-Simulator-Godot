using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// DocumentInput as an IOfficeInteractable: what SetInteractable and Locked switch, and that going inert
// gives up keyboard focus (the Distraction overlay blocks the mouse, not keystrokes to a focused field).
// Live tree: focus only exists there. SubmitButtonTesting is the same shape for the button
[TestSuite]
[RequireGodotRuntime]
public class DocumentInputTesting
{
    private ISceneRunner _runner = null!;
    private DocumentInput _input = null!;

    [BeforeTest]
    public void Setup()
    {
        Control root = new() { Name = "Root" };
        _input = new DocumentInput { Name = "Input" };
        root.AddChild(_input);
        _runner = ISceneRunner.Load(root, true, true);
    }

    [AfterTest]
    public void Teardown()
    {
        _runner.Dispose();
    }

    private void AssertUsable()
    {
        AssertThat(_input.Editable).IsTrue();
        AssertThat(_input.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(_input.MouseFilter).IsEqual(Control.MouseFilterEnum.Stop);
    }

    private void AssertInert()
    {
        AssertThat(_input.Editable).IsFalse();
        AssertThat(_input.FocusMode).IsEqual(Control.FocusModeEnum.None);
        AssertThat(_input.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
    }

    // Usable on its own, so the document scene works when run by itself
    [TestCase]
    public void IsUsableOnceReady()
    {
        AssertUsable();
    }

    [TestCase]
    public void SetInteractableFalseMakesItInert()
    {
        _input.SetInteractable(false);

        AssertInert();
    }

    [TestCase]
    public void GoingInertReleasesFocus()
    {
        _input.GrabFocus();
        AssertThat(_input.HasFocus()).IsTrue();

        _input.SetInteractable(false);

        AssertThat(_input.HasFocus()).IsFalse();
    }

    // Focus is the player's to give back (a click), not the screen's
    [TestCase]
    public void SetInteractableTrueRestoresItWithoutTakingFocus()
    {
        _input.GrabFocus();
        _input.SetInteractable(false);

        _input.SetInteractable(true);

        AssertUsable();
        AssertThat(_input.HasFocus()).IsFalse();
    }

    // An approved form must stay closed through an input-lock cycle
    [TestCase]
    public void LockedWinsOverSetInteractable()
    {
        _input.Locked = true;
        _input.SetInteractable(false);
        _input.SetInteractable(true);

        AssertInert();
    }

    [TestCase]
    public void LockingReleasesFocus()
    {
        _input.GrabFocus();

        _input.Locked = true;

        AssertInert();
        AssertThat(_input.HasFocus()).IsFalse();
    }

    [TestCase]
    public void UnlockingRestoresItOnlyWhileInteractable()
    {
        _input.SetInteractable(false);
        _input.Locked = true;

        _input.Locked = false;
        AssertInert();

        _input.SetInteractable(true);
        AssertUsable();
    }
}
