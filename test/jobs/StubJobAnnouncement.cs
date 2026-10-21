using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Minimal, configurable JobAnnouncement - the StubJob counterpart (same knob/Pack shape). Left with
// FinishOnPlay off it plays until a test calls Finish(), which is how an asynchronous announcement
// (a character walking up) is stood in for
public partial class StubJobAnnouncement : JobAnnouncement
{
    [Export] public string Target { get; set; } = "Announcer";
    [Export] public string PartAnchor { get; set; } = "Anchor";
    [Export] public bool ThrowAfterPlacing { get; set; }
    [Export] public bool FinishOnPlay { get; set; }

    // Runs at the end of OnAttached, before the throw knob; never packed
    public Action<StubJobAnnouncement>? AttachHook { get; set; }

    public int PlayCalls { get; private set; }
    public List<Node2D> Parts { get; } = new();

    public override StringName TargetScreen => Target;

    protected override void OnAttached()
    {
        foreach (Node2D part in GetChildren().OfType<Node2D>().ToArray())
        {
            Place(part, PartAnchor);
            Parts.Add(part);
        }

        AttachHook?.Invoke(this);
        if (ThrowAfterPlacing) { throw new InvalidOperationException("StubJobAnnouncement: ThrowAfterPlacing"); }
    }

    public override void Play()
    {
        PlayCalls++;
        if (FinishOnPlay) { Finish(); }
    }

    public void PlacePart(Node2D part, StringName anchor) => Place(part, anchor);

    public void AddUndo(Action undo) => AddCleanup(undo);

    public static PackedScene Pack(Action<StubJobAnnouncement>? configure = null, params Node2D[] parts)
    {
        var source = new StubJobAnnouncement();
        configure?.Invoke(source);
        foreach (Node2D part in parts)
        {
            source.AddChild(part);
            StubJob.SetOwnerRecursive(part, source);
        }

        var packedScene = new PackedScene();
        packedScene.Pack(source);
        source.Free();
        return packedScene;
    }
}
