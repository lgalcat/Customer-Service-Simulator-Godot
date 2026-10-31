using System;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// PrintTicket's authoring guards only: what it says and how it looks are WIP
[TestSuite]
[RequireGodotRuntime]
public class PrintTicketTesting
{
    [TestCase("Progress")]
    [TestCase("Status")]
    public void AMissingLabelThrowsNamingIt(string missing)
    {
        PrintTicket ticket = AutoFree(new PrintTicket())!;
        Control note = new() { Name = "Note" };
        ticket.AddChild(note);
        foreach (string label in new[] { "Progress", "Status" })
        {
            if (label != missing) { note.AddChild(new Label { Name = label }); }
        }

        Action call = missing == "Progress" ? () => ticket.ShowProgress(0, 1) : () => ticket.ShowStatus("");
        AssertThrown(call).IsInstanceOf<NullReferenceException>()
            .HasMessage($"PrintTicket: missing required child 'Note/{missing}'");
    }
}
