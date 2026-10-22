using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// The "Fill Document" job: a form on <c>ComputerScreen</c>'s desk has some blank lines, and the
/// player fills them by typing the values the monitor shows, then submits. A submit marks each blank
/// right or wrong (every field stays editable); the job completes once a submit finds them all right.
/// <para>The job generates one <see cref="DocumentRecord"/> in <see cref="Setup"/> and pushes it to
/// its two parts: the <see cref="DocumentView"/> (placed at the screen's <c>DocumentAnchor</c>) and
/// the <see cref="RecordView"/> (at <c>MonitorAnchor</c>). It touches no resident device, so there
/// is nothing to restore when it ends.</para>
/// </summary>
public partial class FillDocumentJob : Job
{
    private static readonly StringName ScreenName = "ComputerScreen";
    private static readonly StringName DocumentAnchor = "DocumentAnchor";
    private static readonly StringName MonitorAnchor = "MonitorAnchor";

    // [21/10/2026] Placeholder balance: blanks = _minBlanks + (difficulty - 1) * _blanksPerDifficulty,
    // capped at the number of lines on the form
    [Export] private int _minBlanks = 2;
    [Export] private int _blanksPerDifficulty = 1;

    // Seconds the approved form stays on the desk before the job completes (and its parts are taken
    // away); 0 completes immediately
    [Export] private float _completionDelay = 0.6f;

    private DocumentView? _document;
    private RecordView? _recordView;
    private Timer? _completionTimer;
    private DocumentRecord? _record;
    private bool _approved;

    public override StringName TargetScreen => ScreenName;

    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;
        ResolveParts();

        DocumentLine? unset = _document!.Lines.FirstOrDefault(line => line.Spec == null);
        if (unset != null) { throw new InvalidOperationException($"FillDocumentJob: document line '{unset.Name}' has no Spec"); }

        _record = DocumentRecord.Generate(_document.Lines.Select(line => line.Spec!));
        HashSet<string> blanks = PickBlanks(_record, difficulty);

        _document.Show(_record, blanks);
        _recordView!.Show(_record);
    }

    private void ResolveParts()
    {
        _document ??= GetNodeOrNull<DocumentView>("Document") ?? throw new NullReferenceException("FillDocumentJob: missing required child 'Document'");
        _recordView ??= GetNodeOrNull<RecordView>("RecordView") ?? throw new NullReferenceException("FillDocumentJob: missing required child 'RecordView'");
        if (_completionTimer == null)
        {
            _completionTimer = GetNodeOrNull<Timer>("CompletionDelay") ?? throw new NullReferenceException("FillDocumentJob: missing required child 'CompletionDelay'");
            _completionTimer.OneShot = true;
            _completionTimer.Timeout += Complete;
        }
    }

    // A uniformly random subset of the record's fields, via a partial Fisher-Yates shuffle
    private HashSet<string> PickBlanks(DocumentRecord record, int difficulty)
    {
        string[] keys = record.Entries.Select(entry => entry.Spec.Key.ToString()).ToArray();
        int count = Math.Clamp(_minBlanks + (difficulty - 1) * _blanksPerDifficulty, 1, keys.Length);

        for (int i = 0; i < count; i++)
        {
            int pick = GD.RandRange(i, keys.Length - 1);
            (keys[i], keys[pick]) = (keys[pick], keys[i]);
        }
        return keys.Take(count).ToHashSet();
    }

    protected override void OnAttached()
    {
        ResolveParts();
        Place(_document!, DocumentAnchor);
        Place(_recordView!, MonitorAnchor);
        // Both are this job's own parts, freed with it: no cleanup to register
        _document!.SubmitRequested += OnSubmitRequested;
    }

    private void OnSubmitRequested()
    {
        if (_approved || IsCompleted || _record == null || _document == null) { return; }

        Dictionary<string, bool> results = _document.ReadInputs()
            .ToDictionary(input => input.Key, input => FieldSpec.Matches(_record.ValueOf(input.Key), input.Value));
        _document.ShowResults(results);

        if (results.Values.Any(correct => !correct)) { return; }

        _approved = true;
        _document.ShowApproved();
        if (_completionDelay <= 0f) { Complete(); }
        else { _completionTimer!.Start(_completionDelay); }
    }
}
