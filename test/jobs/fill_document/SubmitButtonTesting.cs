using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// SubmitButton as an IOfficeInteractable; same shape as DocumentInputTesting (live tree, for focus)
[TestSuite]
[RequireGodotRuntime]
public class SubmitButtonTesting
{
    private ISceneRunner _runner = null!;
    private SubmitButton _button = null!;

    [BeforeTest]
    public void Setup()
    {
        Control root = new() { Name = "Root" };
        _button = new SubmitButton { Name = "Submit" };
        root.AddChild(_button);
        _runner = ISceneRunner.Load(root, true, true);
    }

    [AfterTest]
    public void Teardown()
    {
        _runner.Dispose();
    }

    private void AssertUsable()
    {
        AssertThat(_button.Disabled).IsFalse();
        AssertThat(_button.FocusMode).IsEqual(Control.FocusModeEnum.All);
        AssertThat(_button.MouseFilter).IsEqual(Control.MouseFilterEnum.Stop);
    }

    private void AssertInert()
    {
        AssertThat(_button.Disabled).IsTrue();
        AssertThat(_button.FocusMode).IsEqual(Control.FocusModeEnum.None);
        AssertThat(_button.MouseFilter).IsEqual(Control.MouseFilterEnum.Ignore);
    }

    [TestCase]
    public void IsUsableOnceReady()
    {
        AssertUsable();
    }

    [TestCase]
    public void SetInteractableFalseMakesItInert()
    {
        _button.SetInteractable(false);

        AssertInert();
    }

    [TestCase]
    public void GoingInertReleasesFocus()
    {
        _button.GrabFocus();
        AssertThat(_button.HasFocus()).IsTrue();

        _button.SetInteractable(false);

        AssertThat(_button.HasFocus()).IsFalse();
    }

    [TestCase]
    public void SetInteractableTrueRestoresItWithoutTakingFocus()
    {
        _button.GrabFocus();
        _button.SetInteractable(false);

        _button.SetInteractable(true);

        AssertUsable();
        AssertThat(_button.HasFocus()).IsFalse();
    }

    [TestCase]
    public void LockedWinsOverSetInteractable()
    {
        _button.Locked = true;
        _button.SetInteractable(false);
        _button.SetInteractable(true);

        AssertInert();
    }

    [TestCase]
    public void LockingReleasesFocus()
    {
        _button.GrabFocus();

        _button.Locked = true;

        AssertInert();
        AssertThat(_button.HasFocus()).IsFalse();
    }

    [TestCase]
    public void UnlockingRestoresItOnlyWhileInteractable()
    {
        _button.SetInteractable(false);
        _button.Locked = true;

        _button.Locked = false;
        AssertInert();

        _button.SetInteractable(true);
        AssertUsable();
    }
}
