using System;
using Godot;

/// <summary>
/// The record the player copies from, shown on <c>ComputerScreen</c>'s monitor: a part the Fill
/// Document job places there for as long as it runs. A thin <see cref="Node2D"/> root over a
/// read-only <see cref="Control"/> tree; one caption/value row per field, built by <see cref="Show"/>.
/// </summary>
public partial class RecordView : Node2D
{
    // Width of the caption column, so values line up
    [Export] private float _captionWidth = 90f;

    private Container? _rows;

    private Container Rows => _rows ??= GetNodeOrNull<Container>("Display/Rows")
        ?? throw new NullReferenceException("RecordView: missing required child 'Display/Rows'");

    /// <summary>
    /// Replaces whatever is shown with <paramref name="record"/>'s fields, in order.
    /// </summary>
    public void Show(DocumentRecord record)
    {
        foreach (Node row in Rows.GetChildren()) { row.Free(); }

        foreach (DocumentRecord.Entry entry in record.Entries)
        {
            var row = new HBoxContainer { MouseFilter = Control.MouseFilterEnum.Ignore };
            row.AddChild(new Label { Text = entry.Spec.Label, CustomMinimumSize = new Vector2(_captionWidth, 0) });
            row.AddChild(new Label { Text = entry.Value });
            Rows.AddChild(row);
        }
    }
}
