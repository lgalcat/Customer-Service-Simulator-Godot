using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The paper form of the Fill Document job: a part the job places on <c>ComputerScreen</c>'s desk.
/// A thin <see cref="Node2D"/> root, positioned through <see cref="Node2D.Position"/> like the
/// rest of a screen, over a <see cref="Control"/> tree (<c>Sheet</c>) that holds the form's lines,
/// its <see cref="SubmitButton"/> and the approval stamp.
/// <para>The scene is the form's template: which lines exist, in which order, comes from the
/// <see cref="DocumentLine"/>s authored under <c>Sheet/Lines</c>. Data flows job → view through the
/// methods below; the view reports back through <see cref="SubmitRequested"/>.</para>
/// </summary>
public partial class DocumentView : Node2D
{
    /// <summary>
    /// Invoked when the player presses the submit button.
    /// </summary>
    public Action? SubmitRequested;

    private Node? _lines;
    private SubmitButton? _submit;
    private CanvasItem? _stamp;

    private Node LinesNode => _lines ??= Require<Node>("Sheet/Lines");
    private SubmitButton Submit => _submit ??= Require<SubmitButton>("Sheet/Submit");
    private CanvasItem Stamp => _stamp ??= Require<CanvasItem>("Sheet/Stamp");

    /// <summary>
    /// The form's lines, top to bottom.
    /// </summary>
    public IReadOnlyList<DocumentLine> Lines => LinesNode.GetChildren().OfType<DocumentLine>().ToArray();

    // Children are resolved on first use rather than in _Ready: the job fills the form in Setup(),
    // before anything is in the tree
    private T Require<T>(string path) where T : Node
    {
        return GetNodeOrNull<T>(path) ?? throw new NullReferenceException($"DocumentView: missing required child '{path}'");
    }

    public override void _Ready()
    {
        Submit.Pressed += () => SubmitRequested?.Invoke();
        foreach (DocumentLine line in Lines) { line.InputSubmitted += FocusAfter; }
    }

    /// <summary>
    /// Fills the form from <paramref name="record"/>: lines keyed in <paramref name="blankKeys"/> are
    /// shown empty, every other line shows its value.
    /// </summary>
    public void Show(DocumentRecord record, IReadOnlySet<string> blankKeys)
    {
        foreach (DocumentLine line in Lines)
        {
            if (line.Spec == null) { throw new InvalidOperationException($"DocumentView: line '{line.Name}' has no Spec"); }

            if (blankKeys.Contains(line.Spec.Key.ToString())) { line.ShowBlank(); }
            else { line.ShowFilled(record.ValueOf(line.Spec.Key)); }
        }
        Stamp.Visible = false;
    }

    /// <summary>
    /// What the player has typed into each blank, by field key.
    /// </summary>
    public IReadOnlyDictionary<string, string> ReadInputs()
    {
        return Lines.Where(line => line.IsBlank && line.Spec != null)
                    .ToDictionary(line => line.Spec!.Key.ToString(), line => line.InputText);
    }

    /// <summary>
    /// Marks each blank listed in <paramref name="results"/> (by field key) as correct or wrong.
    /// Every field stays editable.
    /// </summary>
    public void ShowResults(IReadOnlyDictionary<string, bool> results)
    {
        foreach (DocumentLine line in Lines)
        {
            if (line.Spec != null && results.TryGetValue(line.Spec.Key.ToString(), out bool correct)) { line.ShowResult(correct); }
        }
    }

    /// <summary>
    /// Shows the approval stamp and closes the form: no field or button accepts input any more.
    /// </summary>
    public void ShowApproved()
    {
        foreach (DocumentLine line in Lines) { line.InputField.Locked = true; }
        Submit.Locked = true;
        Stamp.Visible = true;
    }

    // Enter moves on to the next blank; after the last one, to the submit button (so a second Enter
    // submits)
    private void FocusAfter(DocumentLine current)
    {
        DocumentLine? next = Lines.SkipWhile(line => line != current).Skip(1).FirstOrDefault(line => line.IsBlank);
        if (next != null) { next.FocusInput(); }
        else if (Submit.FocusMode != Control.FocusModeEnum.None) { Submit.GrabFocus(); }
    }
}
