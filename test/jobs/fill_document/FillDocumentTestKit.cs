using System.Collections.Generic;
using System.Linq;
using Godot;

// Shared fixtures and readers for the Fill Document suites: hand-made field specs whose output is fully
// known, hand-built views, the readers the form and the monitor are looked at through, and the drivers that
// start and play the real job. The shipped .tres data is a work in progress, so the component suites never
// read it - only FillDocumentContentTesting does, and only for invariants. Outside the views' own component
// suites, the readers are the one place that knows how the views lay themselves out
public static class FillDocumentTestKit
{
    public const string DocumentViewPath = "res://assets/scenes/jobs/fill_document/document_view.tscn";
    public const string JobPath = "res://assets/scenes/jobs/fill_document/fill_document_job.tscn";

    public enum Mark { None, Correct, Wrong }

    // ---- Specs ----

    // A Pool spec; a single value makes what it generates deterministic
    public static FieldSpec Spec(string key, string label, params string[] values) => new()
    {
        Key = key,
        Label = label,
        Kind = FieldSpec.FieldKind.Pool,
        Values = new Godot.Collections.Array<string>(values),
    };

    public static FieldSpec PatternSpec(string pattern) => new() { Key = "pattern", Kind = FieldSpec.FieldKind.Pattern, Pattern = pattern };

    public static FieldSpec DateSpec(int minYear, int maxYear, string format = "dd/MM/yyyy") =>
        new() { Key = "date", Kind = FieldSpec.FieldKind.Date, MinYear = minYear, MaxYear = maxYear, DateFormat = format };

    public static FieldSpec AmountSpec(float min, float max) =>
        new() { Key = "amount", Kind = FieldSpec.FieldKind.Amount, MinAmount = min, MaxAmount = max };

    // ---- Hand-built views (not in a tree, _Ready not called) ----

    // The children DocumentLine resolves: Caption, Value, Input (a DocumentInput) and Mark
    public static DocumentLine BuildLine(FieldSpec? spec, string name = "Line")
    {
        DocumentLine line = new() { Name = name, Spec = spec };
        line.AddChild(new Label { Name = "Caption" });
        line.AddChild(new Label { Name = "Value" });
        line.AddChild(new DocumentInput { Name = "Input" });
        line.AddChild(new Label { Name = "Mark" });
        return line;
    }

    // DocumentView > Sheet > Lines (one line per spec, named after its key) + Submit + Stamp
    public static DocumentView BuildDocument(params FieldSpec[] specs)
    {
        DocumentView document = new() { Name = "Document" };
        Control sheet = new() { Name = "Sheet" };
        VBoxContainer lines = new() { Name = "Lines" };
        foreach (FieldSpec spec in specs) { lines.AddChild(BuildLine(spec, spec.Key)); }
        sheet.AddChild(lines);
        sheet.AddChild(new SubmitButton { Name = "Submit" });
        sheet.AddChild(new Label { Name = "Stamp" });
        document.AddChild(sheet);
        return document;
    }

    // RecordView > Display > Rows
    public static RecordView BuildRecordView()
    {
        RecordView view = new() { Name = "RecordView" };
        Control display = new() { Name = "Display" };
        display.AddChild(new VBoxContainer { Name = "Rows" });
        view.AddChild(display);
        return view;
    }

    // ---- Reading the form ----

    public static string CaptionOf(DocumentLine line) => line.GetNode<Label>("Caption").Text;

    // The pre-filled value, or null while the line shows a blank instead
    public static string? ShownValue(DocumentLine line)
    {
        Label value = line.GetNode<Label>("Value");
        return value.Visible ? value.Text : null;
    }

    // Read against the line's own configured marks, so the glyphs can change freely
    public static Mark MarkOf(DocumentLine line)
    {
        string text = line.GetNode<Label>("Mark").Text;
        if (text == "") { return Mark.None; }
        if (text == line.Get("_correctMark").AsString()) { return Mark.Correct; }
        if (text == line.Get("_wrongMark").AsString()) { return Mark.Wrong; }
        throw new System.InvalidOperationException($"Line '{line.Name}' shows an unknown mark '{text}'");
    }

    public static SubmitButton SubmitOf(DocumentView document) => Descendants<SubmitButton>(document).Single();

    public static bool StampShown(DocumentView document) => document.GetNode<CanvasItem>("Sheet/Stamp").Visible;

    // ---- Reading the monitor ----

    // Each row as (caption, value), top to bottom
    public static IReadOnlyList<(string Caption, string Value)> MonitorRows(RecordView view) =>
        view.GetNode("Display/Rows").GetChildren()
            .Select(row => Descendants<Label>(row).Select(label => label.Text).ToArray())
            .Select(texts => (texts[0], texts[1]))
            .ToArray();

    // What the player reads to fill a blank: the value next to that caption
    public static IReadOnlyDictionary<string, string> MonitorValues(RecordView view) =>
        MonitorRows(view).ToDictionary(row => row.Caption, row => row.Value);

    // ---- Playing the real job ----

    public sealed record Play(FillDocumentJob Job, OfficeScreen Screen, DocumentView Document, RecordView Monitor)
    {
        public DocumentLine[] Blanks => Document.Lines.Where(line => line.IsBlank).ToArray();
        public Timer CompletionTimer => Job.GetNode<Timer>("CompletionDelay");
    }

    // The real job, not set up and not in the tree
    public static FillDocumentJob InstantiateJob() => GD.Load<PackedScene>(JobPath).Instantiate<FillDocumentJob>();

    // The real job started on its screen in 'office' (JobTestKit.StartJob), its fields taking input
    public static Play Start(OfficeManager office, int difficulty = 1, System.Action<Job>? configure = null)
    {
        FillDocumentJob job = JobTestKit.StartJob<FillDocumentJob>(office, JobPath, difficulty, configure);
        return PlayOn(job, job.Screen!);
    }

    // The parts of a job already started on 'screen', found where the player sees them
    public static Play PlayOn(FillDocumentJob job, OfficeScreen screen) =>
        new(job, screen, PartAt<DocumentView>(screen, "DocumentAnchor"), PartAt<RecordView>(screen, "MonitorAnchor"));

    private static T PartAt<T>(OfficeScreen screen, string anchor) where T : Node =>
        screen.GetFixture<Node2D>(anchor).GetChildren().OfType<T>().Single();

    // More blank lines than any form has: WithBlanks caps it at the line count
    public const int EveryLine = int.MaxValue;

    // Balance knobs, set before Setup: exactly 'blanks' blank lines (clamped to 1..the form's line count),
    // whatever the placeholder balance is
    public static System.Action<Job> WithBlanks(int blanks) => job =>
    {
        job.Set("_minBlanks", blanks);
        job.Set("_blanksPerDifficulty", 0);
    };

    public static System.Action<Job> WithCompletionDelay(float seconds) => job => job.Set("_completionDelay", seconds);

    // What the monitor shows next to the line's caption
    public static string ValueFor(Play play, DocumentLine line) => MonitorValues(play.Monitor)[CaptionOf(line)];

    // What the player does: type into each blank what the monitor shows next to its caption
    public static void CopyFromMonitor(Play play, System.Func<string, string>? typing = null)
    {
        foreach (DocumentLine blank in play.Blanks) { Edit(blank, (typing ?? (value => value))(ValueFor(play, blank))); }
    }

    // A value of the same length (a longer one would be cut at the blank's MaxLength) that differs from
    // the right one however it is compared
    public static string WrongVersionOf(string value) =>
        (char.ToUpperInvariant(value[0]) == 'Q' ? "R" : "Q") + value[1..];

    // ---- Driving ----

    // What typing does: LineEdit's Text setter alone does not emit TextChanged
    public static void Edit(DocumentLine line, string text)
    {
        line.InputField.Text = text;
        line.InputField.EmitSignal(LineEdit.SignalName.TextChanged, text);
    }

    // Enter in the line's field
    public static void PressEnter(DocumentLine line) =>
        line.InputField.EmitSignal(LineEdit.SignalName.TextSubmitted, line.InputField.Text);

    // Fires even on a disabled button: what reaches the job is then down to its own guards
    public static void PressSubmit(DocumentView document) =>
        SubmitOf(document).EmitSignal(BaseButton.SignalName.Pressed);

    // Every T at any depth under 'root', in tree order
    public static IEnumerable<T> Descendants<T>(Node root) where T : class
    {
        foreach (Node child in root.GetChildren())
        {
            if (child is T match) { yield return match; }
            foreach (T nested in Descendants<T>(child)) { yield return nested; }
        }
    }
}
