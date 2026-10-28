using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// RecordView over a hand-built monitor. Built inside each test: Show creates its rows, and nodes created
// in a test under a [BeforeTest] owner are reported as orphans
[TestSuite]
[RequireGodotRuntime]
public class RecordViewTesting
{
    private static DocumentRecord Record(params (string Key, string Label, string Value)[] fields) =>
        DocumentRecord.Generate(fields.Select(field => Spec(field.Key, field.Label, field.Value)));

    [TestCase]
    public void ShowListsEveryFieldInOrder()
    {
        RecordView view = AutoFree(BuildRecordView())!;

        view.Show(Record(("name", "Name", "Ana Torres"), ("city", "City", "Carrow"), ("id", "ID", "AB-12345")));

        AssertThat(MonitorRows(view)).ContainsExactly(("Name", "Ana Torres"), ("City", "Carrow"), ("ID", "AB-12345"));
    }

    [TestCase]
    public void ShowReplacesWhatWasShown()
    {
        RecordView view = AutoFree(BuildRecordView())!;
        view.Show(Record(("name", "Name", "Ana Torres"), ("city", "City", "Carrow"), ("id", "ID", "AB-12345")));

        view.Show(Record(("phone", "Phone", "555"), ("amount", "Amount", "12.50")));

        AssertThat(MonitorRows(view)).ContainsExactly(("Phone", "555"), ("Amount", "12.50"));
    }

    // The monitor is read-only and sits over the screen: it must not block clicks
    [TestCase]
    public void RowsLetClicksThrough()
    {
        RecordView view = AutoFree(BuildRecordView())!;

        view.Show(Record(("name", "Name", "Ana Torres"), ("city", "City", "Carrow")));

        AssertThat(Descendants<Control>(view.GetNode("Display/Rows")).Where(control => control.MouseFilter == Control.MouseFilterEnum.Stop)).IsEmpty();
    }

    [TestCase]
    public void ShowWithoutItsRowsContainerThrowsNamingIt()
    {
        RecordView view = AutoFree(BuildRecordView())!;
        view.GetNode("Display/Rows").Free();

        AssertThrown(() => view.Show(Record(("name", "Name", "Ana Torres")))).IsInstanceOf<System.NullReferenceException>()
            .HasMessage("RecordView: missing required child 'Display/Rows'");
    }
}
