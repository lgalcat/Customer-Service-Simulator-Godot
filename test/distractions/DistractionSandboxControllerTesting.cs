using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;

// Dedicated component suite for DistractionSandboxController, independent of the Distraction
// hierarchy. Dev-only harness - light coverage by design: just its safeguard and the two paths
// that actually reach RequestDistraction(), not exhaustive branch coverage
[TestSuite]
[RequireGodotRuntime]
public class DistractionSandboxControllerTesting
{
    // Flags this run as input/timing-sensitive (see InputSensitiveNotice) - fires once per test
    // process, from whichever sensitive suite runs first
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(DistractionSandboxControllerTesting));

    [TestCase]
    public void ReadyThrowsWhenManagerMissing()
    {
        DistractionSandboxController controller = AutoFree(new DistractionSandboxController())!;

        AssertThrown(() => controller._Ready()).IsInstanceOf<NullReferenceException>();
    }

    [TestCase]
    public void SpawnOnReadyForceSpawnsImmediately()
    {
        (DistractionSandboxController controller, DistractionManager manager) = BuildTree(spawnOnReady: true);
        AutoFree(controller);
        manager._Ready();

        controller._Ready();
        AutoFreeChildren(manager.GetNode<Control>("Overlay"));

        AssertThat(manager.IsDistractionActive).IsTrue();
    }

    // Live tree required - the successful-spawn branch of _UnhandledKeyInput calls GetViewport()
    [TestCase]
    public void F1KeyPressForceSpawnsMinigame()
    {
        (DistractionSandboxController controller, DistractionManager manager) = BuildTree();
        using ISceneRunner runner = ISceneRunner.Load(controller, true, true);

        controller._UnhandledKeyInput(new InputEventKey { Keycode = Key.F1, Pressed = true });

        AssertThat(manager.IsDistractionActive).IsTrue();
    }

    // Builds the real distraction_sandbox.tscn topology by hand (DistractionSandboxController ->
    // DistractionManager -> Overlay), not yet _Ready() - callers decide manual vs. live-tree setup
    private static (DistractionSandboxController controller, DistractionManager manager) BuildTree(bool spawnOnReady = false)
    {
        var controller = new DistractionSandboxController();
        var manager = new DistractionManager { Name = "DistractionManager" };
        var overlay = new Control { Name = "Overlay" };
        manager.AddChild(overlay);
        manager.InitialFactory = new DistractionFactory(SingleTypeMap());
        controller.AddChild(manager);
        if (spawnOnReady) { controller.Set("_spawnOnReady", true); }

        return (controller, manager);
    }

    // Maps the controller's own default _forceType (Platformer) - these tests never override it
    private static Dictionary<DistractionType, PackedScene> SingleTypeMap()
    {
        return new Dictionary<DistractionType, PackedScene>
        {
            { DistractionType.Platformer, StubDistraction.Pack() },
        };
    }

    // AutoFrees every child of `parent` - a successful ForceSpawn() adds a real host tree
    // mid-test, after `controller`/`manager` may already be AutoFree-wrapped (or loaded live), so
    // it needs its own registration or gdUnit4 reports a transient orphan
    private static void AutoFreeChildren(Node parent)
    {
        foreach (Node child in parent.GetChildren()) { AutoFree(child); }
    }
}