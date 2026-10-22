using System;
using Godot;

/// <summary>
/// One labelled line of a Fill Document form: shows either a pre-filled value or a blank (<see
/// cref="DocumentInput"/>) to type into, plus the mark a submit left on that blank.
/// <para>A mark is cleared as soon as the player edits the field, so it never describes a value
/// that is no longer there.</para>
/// </summary>
public partial class DocumentLine : HBoxContainer
{
    /// <summary>
    /// The field this line shows. Set in the document scene, one line per field.
    /// </summary>
    [Export] public FieldSpec? Spec { get; set; }

    // [21/10/2026] PLACEHOLDER marks: plain text and colour until there is art for them
    [Export] private string _correctMark = "OK";
    [Export] private string _wrongMark = "X";
    [Export] private Color _correctColor = new(0.1f, 0.5f, 0.15f);
    [Export] private Color _wrongColor = new(0.75f, 0.1f, 0.1f);

    /// <summary>
    /// Invoked when the player presses Enter in this line's blank.
    /// </summary>
    public Action<DocumentLine>? InputSubmitted;

    private Label? _caption;
    private Label? _value;
    private DocumentInput? _input;
    private Label? _mark;

    /// <summary>
    /// Whether this line is currently a blank the player has to fill.
    /// </summary>
    public bool IsBlank { get; private set; }

    /// <summary>
    /// What the player has typed into the blank.
    /// </summary>
    public string InputText => InputField.Text;

    /// <summary>
    /// The blank's text field (named so it doesn't hide Godot's static <c>Input</c> class).
    /// </summary>
    public DocumentInput InputField => _input ??= Require<DocumentInput>("Input");

    private Label Caption => _caption ??= Require<Label>("Caption");
    private Label Value => _value ??= Require<Label>("Value");
    private Label Mark => _mark ??= Require<Label>("Mark");

    // Children are resolved on first use rather than in _Ready: the job fills its lines in Setup(),
    // before anything is in the tree
    private T Require<T>(string path) where T : Node
    {
        return GetNodeOrNull<T>(path) ?? throw new NullReferenceException($"DocumentLine: missing required child '{path}'");
    }

    public override void _Ready()
    {
        InputField.TextChanged += _ => ClearMark();
        InputField.TextSubmitted += _ => InputSubmitted?.Invoke(this);
    }

    /// <summary>
    /// Shows <paramref name="value"/> already written on the form.
    /// </summary>
    public void ShowFilled(string value)
    {
        IsBlank = false;
        Caption.Text = Spec?.Label ?? "";
        Value.Text = value;
        Value.Visible = true;
        InputField.Visible = false;
        ClearMark();
    }

    /// <summary>
    /// Shows an empty field for the player to fill.
    /// </summary>
    public void ShowBlank()
    {
        IsBlank = true;
        Caption.Text = Spec?.Label ?? "";
        Value.Visible = false;
        InputField.Visible = true;
        InputField.Text = "";
        InputField.MaxLength = Spec?.MaxLength ?? 0;
        ClearMark();
    }

    /// <summary>
    /// Marks the blank as correct or wrong, as found by a submit. The field stays editable.
    /// </summary>
    public void ShowResult(bool correct)
    {
        Color color = correct ? _correctColor : _wrongColor;
        Mark.Text = correct ? _correctMark : _wrongMark;
        Mark.AddThemeColorOverride("font_color", color);
        InputField.AddThemeColorOverride("font_color", color);
        InputField.AddThemeColorOverride("font_uneditable_color", color);
    }

    /// <summary>
    /// Removes any mark a submit left.
    /// </summary>
    public void ClearMark()
    {
        Mark.Text = "";
        Mark.RemoveThemeColorOverride("font_color");
        InputField.RemoveThemeColorOverride("font_color");
        InputField.RemoveThemeColorOverride("font_uneditable_color");
    }

    /// <summary>
    /// Moves keyboard focus to this line's blank.
    /// </summary>
    public void FocusInput()
    {
        InputField.GrabFocus();
    }
}
