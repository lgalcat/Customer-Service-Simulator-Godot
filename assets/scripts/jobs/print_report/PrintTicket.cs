using System;
using Godot;

/// <summary>
/// The print order lying next to the printer during the Print Report job: how many pages are done
/// and what the printer needs right now. A part the job places at <c>PrinterScreen</c>'s
/// <c>TicketAnchor</c>; a thin <see cref="Node2D"/> root over a decorative, read-only
/// <see cref="Control"/> tree (every node ignores the mouse, so it never blocks a click).
/// </summary>
public partial class PrintTicket : Node2D
{
    private Label? _progress;
    private Label? _status;

    // Resolved on first use: the job fills the ticket in Setup, before tree entry
    private Label Progress => _progress ??= GetNodeOrNull<Label>("Note/Progress")
        ?? throw new NullReferenceException("PrintTicket: missing required child 'Note/Progress'");

    private Label Status => _status ??= GetNodeOrNull<Label>("Note/Status")
        ?? throw new NullReferenceException("PrintTicket: missing required child 'Note/Status'");

    /// <summary>
    /// Shows how many of the ordered pages are printed.
    /// </summary>
    public void ShowProgress(int printed, int total)
    {
        Progress.Text = $"Pages {printed} / {total}";
    }

    /// <summary>
    /// Shows what the printer is doing or needs.
    /// </summary>
    public void ShowStatus(string text)
    {
        Status.Text = text;
    }
}
