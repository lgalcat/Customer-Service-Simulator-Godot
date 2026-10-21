using System.Collections.Generic;
using Godot;

// A job part that records the name of its parent each time it enters the tree, to show that a part
// reaches the tree for the first (and only) time under its anchor. Packable, so it can be authored into
// a StubJob scene; read back through StubJob.Parts
public partial class TreeEntryProbe : Node2D
{
    public List<string> EnteredUnder { get; } = new();

    public override void _EnterTree()
    {
        EnteredUnder.Add(GetParent().Name);
    }
}
