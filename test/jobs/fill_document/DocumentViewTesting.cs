using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// DocumentView over a hand-built form of four single-value fields: what Show/ReadInputs/ShowResults/
// ShowApproved do to the lines, the submit request, and Enter moving through the blanks (live tree, for
// focus). What the job decides with these is FillDocumentBehaviourTesting's
[TestSuite]
[RequireGodotRuntime]
public class DocumentViewTesting
{
    private FieldSpec[] _specs = null!;
    private DocumentRecord _record = null!;
    private DocumentView _document = null!;

    [BeforeTest]
    public void Setup()
    {
        _specs = new[]
        {
            Spec("name", "Name", "Ana Torres"),
            Spec("city", "City", "Carrow"),
            Spec("id", "ID", "AB-12345"),
            Spec("phone", "Phone", "555 010 203"),
        };
        _record = DocumentRecord.Generate(_specs);
        _document = AutoFree(BuildDocument(_specs))!;
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    private DocumentLine Line(string key) => LineOf(_document, key);

    private static DocumentLine LineOf(DocumentView document, string key) => document.Lines.Single(line => line.Spec!.Key == key);

    private static HashSet<string> Keys(params string[] keys) => keys.ToHashSet();

    // A fresh form loaded live, for focus: built inside the test (and freed by the runner), since a
    // [BeforeTest] node moved into the tree mid-test is reported as an orphan
    private (ISceneRunner Runner, DocumentView Document) LoadLive()
    {
        DocumentView document = BuildDocument(_specs);
        return (ISceneRunner.Load(document, true, true), document);
    }

    // ---- Lines ----

    [TestCase]
    public void LinesListsTheFormsLinesInOrderSkippingAnythingElse()
    {
        Node lines = _document.GetNode("Sheet/Lines");
        Label heading = AutoFree(new Label { Name = "Heading" })!;
        lines.AddChild(heading);
        lines.MoveChild(heading, 1);

        AssertThat(_document.Lines.Select(line => line.Spec!.Key.ToString())).ContainsExactly("name", "city", "id", "phone");
    }

    // ---- Show ----

    [TestCase]
    public void ShowBlanksTheChosenLinesAndFillsTheOthersFromTheRecord()
    {
        _document.Show(_record, Keys("city", "phone"));

        AssertThat(Line("city").IsBlank).IsTrue();
        AssertThat(Line("phone").IsBlank).IsTrue();
        AssertThat(ShownValue(Line("name"))).IsEqual("Ana Torres");
        AssertThat(ShownValue(Line("id"))).IsEqual("AB-12345");
    }

    [TestCase]
    public void ShowHidesTheStamp()
    {
        _document.GetNode<CanvasItem>("Sheet/Stamp").Visible = true;

        _document.Show(_record, Keys("city"));

        AssertThat(StampShown(_document)).IsFalse();
    }

    [TestCase]
    public void ShowRejectsALineWithoutASpec()
    {
        _document.GetNode("Sheet/Lines").AddChild(AutoFree(BuildLine(null, "Unset")));

        AssertThrown(() => _document.Show(_record, Keys("city"))).IsInstanceOf<System.InvalidOperationException>()
            .HasMessage("DocumentView: line 'Unset' has no Spec");
    }

    // ---- ReadInputs / ShowResults ----

    // A filled line's (hidden) field is never read, whatever it holds
    [TestCase]
    public void ReadInputsReturnsWhatIsTypedInEachBlankByKey()
    {
        _document.Show(_record, Keys("city", "phone"));
        Line("city").InputField.Text = "Carr";
        Line("name").InputField.Text = "ignored";

        IReadOnlyDictionary<string, string> inputs = _document.ReadInputs();

        AssertThat(inputs.Keys).ContainsExactlyInAnyOrder("city", "phone");
        AssertThat(inputs["city"]).IsEqual("Carr");
        AssertThat(inputs["phone"]).IsEmpty();
    }

    [TestCase]
    public void ShowResultsMarksOnlyTheListedLines()
    {
        _document.Show(_record, Keys("city", "phone"));

        _document.ShowResults(new Dictionary<string, bool> { ["city"] = true });

        AssertThat(MarkOf(Line("city"))).IsEqual(Mark.Correct);
        AssertThat(MarkOf(Line("phone"))).IsEqual(Mark.None);
        AssertThat(MarkOf(Line("name"))).IsEqual(Mark.None);

        _document.ShowResults(new Dictionary<string, bool> { ["phone"] = false });

        AssertThat(MarkOf(Line("phone"))).IsEqual(Mark.Wrong);
    }

    // ---- Approval ----

    [TestCase]
    public void ShowApprovedLocksEveryFieldAndTheButtonAndShowsTheStamp()
    {
        _document.Show(_record, Keys("city", "phone"));

        _document.ShowApproved();

        AssertThat(_document.Lines.All(line => line.InputField.Locked)).IsTrue();
        AssertThat(SubmitOf(_document).Locked).IsTrue();
        AssertThat(StampShown(_document)).IsTrue();
    }

    // ---- Submit ----

    [TestCase]
    public void PressingSubmitRequestsASubmitOnce()
    {
        // Manual _Ready() call - no SceneTree to auto-trigger it
        _document._Ready();
        int requests = 0;
        _document.SubmitRequested += () => requests++;

        PressSubmit(_document);

        AssertThat(requests).IsEqual(1);
    }

    // ---- Enter ----

    [TestCase]
    public void EnterMovesToTheNextBlankSkippingFilledLines()
    {
        (ISceneRunner runner, DocumentView document) = LoadLive();
        using (runner)
        {
            document.Show(_record, Keys("name", "id", "phone"));
            LineOf(document, "name").FocusInput();

            PressEnter(LineOf(document, "name"));

            AssertThat(LineOf(document, "id").InputField.HasFocus()).IsTrue();
        }
    }

    // So a second Enter submits
    [TestCase]
    public void EnterInTheLastBlankMovesToSubmit()
    {
        (ISceneRunner runner, DocumentView document) = LoadLive();
        using (runner)
        {
            document.Show(_record, Keys("name", "city"));
            LineOf(document, "city").FocusInput();

            PressEnter(LineOf(document, "city"));

            AssertThat(SubmitOf(document).HasFocus()).IsTrue();
        }
    }

    // ---- Structure ----

    [TestCase("Sheet/Lines")]
    [TestCase("Sheet/Submit")]
    [TestCase("Sheet/Stamp")]
    public void UsingAMissingChildThrowsNamingIt(string missing)
    {
        // Its own form: freeing part of a [BeforeTest] fixture mid-test is reported as orphans
        DocumentView document = AutoFree(BuildDocument(_specs))!;
        document.GetNode(missing).Free();

        AssertThrown(() => document.ShowApproved()).IsInstanceOf<System.NullReferenceException>()
            .HasMessage($"DocumentView: missing required child '{missing}'");
    }
}
