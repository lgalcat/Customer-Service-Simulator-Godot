using System.Collections.Generic;
using System.Linq;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// The shipped form (document_view.tscn and the field .tres it points at), checked only against what
// makes the job playable at all - never against the data itself, which is still being authored
[TestSuite]
[RequireGodotRuntime]
public class FillDocumentContentTesting
{
    private DocumentView _document = null!;

    [BeforeTest]
    public void Setup()
    {
        _document = AutoFree(GD.Load<PackedScene>(DocumentViewPath).Instantiate<DocumentView>())!;
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // Every other case would pass on an empty form
    private IReadOnlyList<DocumentLine> Lines()
    {
        IReadOnlyList<DocumentLine> lines = _document.Lines;
        AssertThat(lines).IsNotEmpty();
        return lines;
    }

    private IEnumerable<FieldSpec> Specs() => Lines().Select(line => line.Spec!);

    // gdUnit4's ContainsExactlyInAnyOrder does not count repeats, so uniqueness is checked as "no value
    // appears twice"
    private static IEnumerable<string> Repeated(IEnumerable<string> values) =>
        values.GroupBy(value => value).Where(group => group.Count() > 1).Select(group => group.Key);

    [TestCase]
    public void EveryLineHasASpec()
    {
        AssertThat(Lines().Where(line => line.Spec == null).Select(line => line.Name.ToString())).IsEmpty();
    }

    [TestCase]
    public void FieldKeysAreUnique()
    {
        AssertThat(Repeated(Specs().Select(spec => spec.Key.ToString()))).IsEmpty();
    }

    // The player matches a blank to the monitor's row by its caption
    [TestCase]
    public void CaptionsAreNonEmptyAndUnique()
    {
        string[] captions = Specs().Select(spec => spec.Label).ToArray();

        AssertThat(captions.Where(string.IsNullOrWhiteSpace)).IsEmpty();
        AssertThat(Repeated(captions)).IsEmpty();
    }

    // Every value a spec can produce: a pool's exhaustively, the other kinds sampled (which also proves
    // each shipped spec generates without throwing)
    private static IEnumerable<string> PossibleValues(FieldSpec spec) =>
        spec.Kind == FieldSpec.FieldKind.Pool ? spec.Values : Enumerable.Range(0, 300).Select(_ => spec.Generate());

    // A LineEdit cuts typed text at MaxLength (0 = no limit), so a longer value could never be typed in full;
    // and an empty value would make an untouched blank correct. Either way the job would silently stop
    // working as a game
    [TestCase]
    public void EveryValueIsNonEmptyAndFitsItsBlank()
    {
        foreach (FieldSpec spec in Specs())
        {
            foreach (string value in PossibleValues(spec))
            {
                AssertThat(string.IsNullOrWhiteSpace(value)).OverrideFailureMessage($"'{spec.Key}' can produce an empty value").IsFalse();
                AssertThat(spec.MaxLength == 0 || value.Length <= spec.MaxLength)
                    .OverrideFailureMessage($"'{spec.Key}' can produce '{value}' ({value.Length} chars), longer than its MaxLength {spec.MaxLength}")
                    .IsTrue();
            }
        }
    }
}
