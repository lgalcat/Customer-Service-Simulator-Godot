using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;

// Component suite for ThrowPaperBallLayout's throw-tuning validation
// Values are whole numbers on purpose: the messages interpolate floats, whose decimal separator follows the culture
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallLayoutTesting
{
    private const string BaseLayoutPath = "res://assets/scenes/distractions/throw_paper_ball/layouts/throw_paper_ball_layout_base.tscn";

    [TestCase]
    public void DefaultTuningIsValid()
    {
        ThrowPaperBallLayout layout = AutoFree(new ThrowPaperBallLayout())!;

        // Any exception fails the case
        layout.Validate();
    }

    // One row per guard, each at its boundary (equal ends) or past it; the other values stay valid
    [TestCase(-30, -30, 100, 500, 1, "MinThrowAngle -30 is not below MaxThrowAngle -30")]
    [TestCase(0, -60, 100, 500, 1, "MinThrowAngle 0 is not below MaxThrowAngle -60")]
    [TestCase(-60, 0, 300, 300, 1, "MinThrowStrength 300 is not below MaxThrowStrength 300")]
    [TestCase(-60, 0, 500, 100, 1, "MinThrowStrength 500 is not below MaxThrowStrength 100")]
    [TestCase(-60, 0, 100, 500, 0, "ThrowCycle 0 must be positive")]
    [TestCase(-60, 0, 100, 500, -1, "ThrowCycle -1 must be positive")]
    public void MisTunedLayoutThrows(float minAngle, float maxAngle, float minStrength, float maxStrength, float cycle, string reason)
    {
        ThrowPaperBallLayout layout = AutoFree(new ThrowPaperBallLayout
        {
            Name = "Stage",
            MinThrowAngle = minAngle,
            MaxThrowAngle = maxAngle,
            MinThrowStrength = minStrength,
            MaxThrowStrength = maxStrength,
            ThrowCycle = cycle
        })!;

        AssertThrown(() => layout.Validate())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"ThrowPaperBallLayout 'Stage': {reason}");
    }

    // An instanced layout is named by its scene file (every layout's root is called "Stage", so the name
    // alone wouldn't say which one is broken)
    [TestCase]
    public void InstancedLayoutIsNamedByItsSceneFile()
    {
        ThrowPaperBallLayout layout = AutoFree(GD.Load<PackedScene>(BaseLayoutPath).Instantiate<ThrowPaperBallLayout>())!;
        layout.ThrowCycle = 0;

        AssertThrown(() => layout.Validate())
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"ThrowPaperBallLayout '{BaseLayoutPath}': ThrowCycle 0 must be positive");
    }
}
