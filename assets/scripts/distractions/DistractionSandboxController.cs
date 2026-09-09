using Godot;
using System;

/// <summary>
/// Dev-only harness for <see cref="DistractionManager"/>, hosted in "distraction_sandbox.tscn". Lets
/// a developer force-spawn one specific minigame/difficulty instead of running its bare .tscn
/// directly - the workflow every migrated minigame's dropped dev "_Ready(){ Setup(1); }" shim relied
/// on before.
/// </summary>
public partial class DistractionSandboxController : Node2D
{
    // Path to the "DistractionManager" this harness drives.
    [Export] private NodePath _managerPath = "DistractionManager";

    // Minigame the force-spawn key (F1) requests. Must be one the factory supported maps
    // Picking an unmapped type throws on spawn.
    [Export] private DistractionType _forceType = DistractionType.Platformer;

    // Difficulty sent with a forced request. Scheduled requests use the manager's own base difficulty instead.
    [Export] private int _forceDifficulty = 1;

    // Force-spawn the chosen minigame once when the sandbox loads, without waiting for the F1 key.
    [Export] private bool _spawnOnReady = false;

    private DistractionManager _manager = null!;

    public override void _Ready()
    {
        _manager = GetNode<DistractionManager>(_managerPath);
        if (_manager == null) { throw new NullReferenceException($"DistractionSandboxController: missing required node '{_managerPath}'"); }

        _manager.DistractionCompleted += () => GD.Print("DistractionSandbox: DistractionCompleted");

        if (_spawnOnReady) { ForceSpawn(); }
    }

    // Raw keycode check rather than a new InputMap action - avoids hand-authoring a project.godot
    // [input] resource literal blind, matching Swatter.cs's own precedent for a single hardcoded key
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is InputEventKey key && key.Pressed && !key.Echo && key.Keycode == Key.F1)
        {
            ForceSpawn();
            GetViewport().SetInputAsHandled();
        }
    }

    private void ForceSpawn()
    {
        bool spawned = _manager.RequestDistraction(_forceType, _forceDifficulty);
        GD.Print(spawned
            ? $"DistractionSandbox: requested {_forceType} (difficulty {_forceDifficulty})"
            : "DistractionSandbox: request denied, a distraction is already active");
    }
}
