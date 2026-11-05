using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// Component suite for TrashCan's path movement, split from TrashCanTesting (the dwell-timer logic is an
// unrelated sub-area). Every case runs in a live SceneTree: the engine only auto-enables physics processing
// for the _PhysicsProcess override at NOTIFICATION_READY, so an out-of-tree manual _Ready() would leave it
// off either way and make the "stays static" cases pass by construction.
// Movement is driven by calling _PhysicsProcess with fixed deltas (no frames are simulated), so these cases
// are deterministic and not timing-sensitive
[TestSuite]
[RequireGodotRuntime]
public class TrashCanMovementTesting
{
    // Non-zero, so a missing local-to-global conversion of the path's points can't go unnoticed
    private static readonly Vector2 PathOrigin = new(100, 40);
    private const float Tolerance = 0.5f;

    // Closed 100x100 square (last point repeats the first), 400 px long
    private static readonly Vector2[] ClosedSquare =
    {
        new(0, 0), new(100, 0), new(100, 100), new(0, 100), new(0, 0)
    };

    [TestCase]
    public void ReadyWithoutPathLeavesPhysicsProcessingOff()
    {
        Node2D root = BuildRig(ClosedSquare, 40, wirePath: false);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);

        AssertThat(GetTrashCan(root).IsPhysicsProcessing()).IsFalse();
    }

    [TestCase]
    public void ReadyWithZeroSpeedLeavesPhysicsProcessingOff()
    {
        Node2D root = BuildRig(ClosedSquare, 0);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);

        AssertThat(GetTrashCan(root).IsPhysicsProcessing()).IsFalse();
    }

    // A path that is set but unusable is a designer mistake: TrashCan warns (not asserted, log only)
    // and stays static
    [TestCase]
    public void ReadyWithSinglePointPathLeavesPhysicsProcessingOff()
    {
        Node2D root = BuildRig(new[] { new Vector2(0, 0) }, 40);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);

        AssertThat(GetTrashCan(root).IsPhysicsProcessing()).IsFalse();
    }

    // Two coincident points pass the point-count check, so this is the zero-length guard's own case
    [TestCase]
    public void ReadyWithZeroLengthPathLeavesPhysicsProcessingOff()
    {
        Node2D root = BuildRig(new[] { new Vector2(10, 10), new Vector2(10, 10) }, 40);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);

        AssertThat(GetTrashCan(root).IsPhysicsProcessing()).IsFalse();
    }

    [TestCase]
    public void ReadyPlacesCanAtPathFirstPoint()
    {
        Node2D root = BuildRig(ClosedSquare, 40);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        TrashCan trashCan = GetTrashCan(root);

        AssertThat(trashCan.IsPhysicsProcessing()).IsTrue();
        AssertNear(trashCan.GlobalPosition, PathOrigin + ClosedSquare[0]);
    }

    [TestCase]
    public void MovesAtConfiguredSpeedAlongPath()
    {
        Node2D root = BuildRig(ClosedSquare, 40);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        TrashCan trashCan = GetTrashCan(root);

        // 40 px/s * 0.5 s = 20 px along the first (horizontal) side
        trashCan._PhysicsProcess(0.5);

        AssertNear(trashCan.GlobalPosition, PathOrigin + new Vector2(20, 0));
    }

    [TestCase]
    public void NegativeSpeedRunsPathBackwards()
    {
        Node2D root = BuildRig(ClosedSquare, -40);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        TrashCan trashCan = GetTrashCan(root);

        // 20 px backwards from the first point runs up the closing (left) side
        trashCan._PhysicsProcess(0.5);

        AssertNear(trashCan.GlobalPosition, PathOrigin + new Vector2(0, 20));
    }

    // Overshoots the lap on purpose: SampleBaked clamps past the end of the curve, and the end of a closed
    // loop is its first point, so stopping exactly at one lap couldn't tell wrapping from clamping
    [TestCase]
    public void LoopsPastOneFullLap()
    {
        Node2D root = BuildRig(ClosedSquare, 40);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        TrashCan trashCan = GetTrashCan(root);

        // 11 steps of 40 px = the square's 400 px perimeter + 40 px into the second lap
        for (int i = 0; i < 11; i++) { trashCan._PhysicsProcess(1.0); }

        AssertNear(trashCan.GlobalPosition, PathOrigin + new Vector2(40, 0));
    }

    // L-shaped open path A(0,0) -> B(100,0) -> C(100,100): TrashCan closes it with a straight C -> A segment.
    // Halfway along that segment is (50,50); an unclosed path would instead wrap from C straight back to A
    // and land on the first side, at (70.7,0)
    [TestCase]
    public void OpenPathIsClosedWithStraightSegment()
    {
        Vector2[] lShape = { new(0, 0), new(100, 0), new(100, 100) };
        Node2D root = BuildRig(lShape, 100);
        using ISceneRunner runner = ISceneRunner.Load(root, true, true);
        TrashCan trashCan = GetTrashCan(root);
        float halfDiagonal = new Vector2(100, 100).Length() / 2;

        trashCan._PhysicsProcess((200 + halfDiagonal) / 100);

        AssertNear(trashCan.GlobalPosition, PathOrigin + new Vector2(50, 50));
        // The closing point goes on a copy: the designer's (possibly shared) curve resource is untouched
        AssertThat(root.GetNode<Path2D>("Path").Curve.PointCount).IsEqual(3);
    }

    // Wrapper root holding a Path2D (at PathOrigin) and a TrashCan placed away from the path, with the only
    // child its _Ready() requires
    private static Node2D BuildRig(Vector2[] points, float speed, bool wirePath = true)
    {
        var root = new Node2D();
        var curve = new Curve2D();
        foreach (Vector2 point in points) { curve.AddPoint(point); }
        var path = new Path2D { Name = "Path", Position = PathOrigin, Curve = curve };
        root.AddChild(path);

        var trashCan = new TrashCan { Name = "TrashCan", Position = new Vector2(-50, -50) };
        trashCan.AddChild(new Area2D { Name = "TrashCanInside" });
        if (wirePath) { trashCan.Set("_movementPath", path); }
        trashCan.Set("_movementSpeed", speed);
        root.AddChild(trashCan);
        return root;
    }

    private static TrashCan GetTrashCan(Node2D root) => root.GetNode<TrashCan>("TrashCan");

    private static void AssertNear(Vector2 actual, Vector2 expected)
        => AssertThat(actual.DistanceTo(expected)).IsLess(Tolerance);
}
