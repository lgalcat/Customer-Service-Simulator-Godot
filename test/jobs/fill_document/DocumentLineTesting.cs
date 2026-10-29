using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// DocumentLine over a hand-built line: filled vs blank display, result marks and when they clear, and
// Enter. Marks and colours are never compared to literals, so the placeholder look can change freely
[TestSuite]
[RequireGodotRuntime]
public class DocumentLineTesting
{
    private FieldSpec _spec = null!;
    private DocumentLine _line = null!;

    [BeforeTest]
    public void Setup()
    {
        _spec = Spec("city", "City", "Carrow");
        _spec.MaxLength = 7;
        _line = AutoFree(BuildLine(_spec))!;
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // ---- Filled / blank ----

    [TestCase]
    public void ShowFilledShowsTheValueUnderTheSpecsCaption()
    {
        _line.ShowFilled("Carrow");

        AssertThat(_line.IsBlank).IsFalse();
        AssertThat(CaptionOf(_line)).IsEqual("City");
        AssertThat(ShownValue(_line)).IsEqual("Carrow");
        AssertThat(_line.InputField.Visible).IsFalse();
    }

    [TestCase]
    public void ShowBlankShowsAnEmptyFieldLimitedToTheSpecsLength()
    {
        _line.ShowBlank();

        AssertThat(_line.IsBlank).IsTrue();
        AssertThat(CaptionOf(_line)).IsEqual("City");
        AssertThat(ShownValue(_line)).IsNull();
        AssertThat(_line.InputField.Visible).IsTrue();
        AssertThat(_line.InputText).IsEmpty();
        AssertThat(_line.InputField.MaxLength).IsEqual(7);
    }

    [TestCase]
    public void ShowBlankEmptiesWhatWasTypedBefore()
    {
        _line.ShowBlank();
        _line.InputField.Text = "Carr";

        _line.ShowBlank();

        AssertThat(_line.InputText).IsEmpty();
    }

    [TestCase]
    public void ALineSwitchesBetweenBlankAndFilled()
    {
        _line.ShowBlank();
        _line.ShowFilled("Carrow");

        AssertThat(_line.IsBlank).IsFalse();
        AssertThat(ShownValue(_line)).IsEqual("Carrow");
        AssertThat(_line.InputField.Visible).IsFalse();

        _line.ShowBlank();

        AssertThat(_line.IsBlank).IsTrue();
        AssertThat(ShownValue(_line)).IsNull();
        AssertThat(_line.InputField.Visible).IsTrue();
    }

    // ---- Marks ----

    // The colour is also set for the uneditable state, so the marks stay readable on an approved
    // (locked) form
    [TestCase]
    public void ShowResultMarksCorrectAndWrongDistinctly()
    {
        _line.ShowBlank();

        _line.ShowResult(true);
        Godot.Color correctColor = _line.InputField.GetThemeColor("font_color");

        AssertThat(MarkOf(_line)).IsEqual(Mark.Correct);
        AssertThat(_line.InputField.HasThemeColorOverride("font_color")).IsTrue();
        AssertThat(_line.InputField.HasThemeColorOverride("font_uneditable_color")).IsTrue();

        _line.ShowResult(false);

        AssertThat(MarkOf(_line)).IsEqual(Mark.Wrong);
        AssertThat(_line.InputField.GetThemeColor("font_color")).IsNotEqual(correctColor);
    }

    [TestCase]
    public void ClearMarkRemovesTheMarkAndItsColours()
    {
        _line.ShowBlank();
        _line.ShowResult(false);

        _line.ClearMark();

        AssertThat(MarkOf(_line)).IsEqual(Mark.None);
        AssertThat(_line.InputField.HasThemeColorOverride("font_color")).IsFalse();
        AssertThat(_line.InputField.HasThemeColorOverride("font_uneditable_color")).IsFalse();
    }

    // A mark must never describe a value that is no longer there
    [TestCase]
    public void EditingTheFieldClearsItsMark()
    {
        // Manual _Ready() call - no SceneTree to auto-trigger it (applies to every test below that relies on
        // the line's signal wiring)
        _line._Ready();
        _line.ShowBlank();
        _line.ShowResult(true);

        Edit(_line, "Carrows");

        AssertThat(MarkOf(_line)).IsEqual(Mark.None);
        AssertThat(_line.InputField.HasThemeColorOverride("font_color")).IsFalse();
    }

    [TestCase]
    public void ShowingTheLineAgainClearsItsMark()
    {
        _line.ShowBlank();
        _line.ShowResult(false);
        _line.ShowBlank();

        AssertThat(MarkOf(_line)).IsEqual(Mark.None);

        _line.ShowResult(false);
        _line.ShowFilled("Carrow");

        AssertThat(MarkOf(_line)).IsEqual(Mark.None);
    }

    // ---- Enter ----

    [TestCase]
    public void EnterInTheFieldReportsThisLine()
    {
        _line._Ready();
        _line.ShowBlank();
        DocumentLine? reported = null;
        int reports = 0;
        _line.InputSubmitted += line => { reported = line; reports++; };

        PressEnter(_line);

        AssertThat(reports).IsEqual(1);
        AssertThat(reported).IsSame(_line);
    }

    // ---- Structure ----

    [TestCase("Caption")]
    [TestCase("Value")]
    [TestCase("Input")]
    [TestCase("Mark")]
    public void ShowingWithAMissingChildThrowsNamingIt(string missing)
    {
        // Its own line: freeing part of a [BeforeTest] fixture mid-test is reported as orphans
        DocumentLine line = AutoFree(BuildLine(_spec))!;
        line.GetNode(missing).Free();

        AssertThrown(() => line.ShowBlank()).IsInstanceOf<System.NullReferenceException>()
            .HasMessage($"DocumentLine: missing required child '{missing}'");
    }
}
