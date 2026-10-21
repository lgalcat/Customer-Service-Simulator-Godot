using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

// Minimal, configurable Job for the Jobs suites. JobManager only ever sees jobs instanced from a
// PackedScene, so the per-scenario behaviour is carried by [Export] knobs (Pack() serializes them);
// AttachHook is for tests that construct a StubJob directly and is never packed.
// OnAttached places every authored Node2D child at PartAnchor - the same shape as a real job, whose
// visible parts are authored as children of its own scene
public partial class StubJob : Job
{
    // Every instance that went through Setup (every one a JobFactory made), in creation order - the
    // only handle a test has on the job JobManager holds back while an announcement plays, and the way
    // to see that a denied request created nothing. Static, so suites using it clear it in [BeforeTest]
    // (test runs are single-threaded)
    public static List<StubJob> Created { get; } = new();

    [Export] public string Target { get; set; } = "Screen";
    [Export] public string PartAnchor { get; set; } = "Anchor";
    [Export] public bool ThrowAfterPlacing { get; set; }
    [Export] public bool CompleteOnAttach { get; set; }
    [Export] public bool CompleteOnReady { get; set; }
    [Export] public bool ThrowOnReady { get; set; }

    // Runs at the end of OnAttached, before the throw/complete knobs
    public Action<StubJob>? AttachHook { get; set; }

    public bool? WasInTreeAtSetup { get; private set; }
    public int ReadyCalls { get; private set; }
    // Cached on placement: a placed part has left the job, so it can't be found under it any more
    public List<Node2D> Parts { get; } = new();

    public override StringName TargetScreen => Target;

    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;
        WasInTreeAtSetup = IsInsideTree();
        Created.Add(this);
    }

    protected override void OnAttached()
    {
        foreach (Node2D part in GetChildren().OfType<Node2D>().ToArray())
        {
            Place(part, PartAnchor);
            Parts.Add(part);
        }

        AttachHook?.Invoke(this);
        if (ThrowAfterPlacing) { throw new InvalidOperationException("StubJob: ThrowAfterPlacing"); }
        if (CompleteOnAttach) { Complete(); }
    }

    public override void _Ready()
    {
        ReadyCalls++;
        if (ThrowOnReady) { throw new InvalidOperationException("StubJob: ThrowOnReady"); }
        if (CompleteOnReady) { Complete(); }
    }

    // Public doors to the protected scope helpers, for AttachHook bodies
    public void PlacePart(Node2D part, StringName anchor) => Place(part, anchor);

    public void AddUndo(Action undo) => AddCleanup(undo);

    // Packs a StubJob with the given knobs and authored parts into an in-memory PackedScene (the
    // StubDistraction.Pack idiom). Parts are owned by the root, as an editor-authored scene's would be,
    // or Pack() would leave them out
    public static PackedScene Pack(Action<StubJob>? configure = null, params Node2D[] parts)
    {
        var source = new StubJob();
        configure?.Invoke(source);
        foreach (Node2D part in parts)
        {
            source.AddChild(part);
            SetOwnerRecursive(part, source);
        }

        var packedScene = new PackedScene();
        packedScene.Pack(source);
        // Pack() copies the node's state into the resource, it doesn't take ownership
        source.Free();
        return packedScene;
    }

    internal static void SetOwnerRecursive(Node node, Node owner)
    {
        node.Owner = owner;
        foreach (Node child in node.GetChildren()) { SetOwnerRecursive(child, owner); }
    }
}
