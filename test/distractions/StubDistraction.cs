using Godot;

// Minimal stand-in Distraction for tests that need a lightweight, controllable minigame without
// touching a real minigame's own scene/logic - shared by the hosting-layer suites (Factory/
// Manager/Scheduler/Controller) that each need one, rather than each duplicating its own copy
public partial class StubDistraction : Distraction
{
    public override int ViewportX => 64;
    public override int ViewportY => 64;

    public override void Setup(int difficulty) => Difficulty = difficulty;

    public override void Victory() => OnVictory?.Invoke();

    // Packs a fresh StubDistraction into a new in-memory PackedScene - the "required PackedScene
    // doesn't need a real .tscn" idiom (see FlySpawnerTesting.BuildFlyScene), bundled here since
    // every caller needs the same few lines
    public static PackedScene Pack()
    {
        var source = new StubDistraction();
        var packedScene = new PackedScene();
        packedScene.Pack(source);
        // Pack() copies the node's state into the resource, it doesn't take ownership
        source.Free();
        return packedScene;
    }
}