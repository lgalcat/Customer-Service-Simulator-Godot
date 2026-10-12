using Godot;
using System;

/// <summary>
/// Root of the composed game level: the one place that knows both <see cref="OfficeManager"/> and
/// <see cref="DistractionManager"/>, so neither has to know the other exists.
/// <para>Its only job is policy glue: Office cannot be interacted with while a <see
/// cref="Distraction"/> is active. Draw order is not handled here - it falls out of the two systems'
/// own <c>CanvasLayer</c> values (Office's screen layer 0, the distraction manager's layer above it).
/// It deliberately knows nothing about what decides *when* a distraction appears (the cadence node
/// lives inside <c>distraction_manager.tscn</c>) or which minigames exist.</para>
/// </summary>
public partial class MainLevel : Node
{
    [Export] private NodePath _officePath = "OfficeManager";
    [Export] private NodePath _distractionsPath = "DistractionManager";

    public override void _Ready()
    {
        var office = GetNode<OfficeManager>(_officePath);
        if (office == null) { throw new NullReferenceException($"MainLevel: missing required child '{_officePath}'"); }

        var distractions = GetNode<DistractionManager>(_distractionsPath);
        if (distractions == null) { throw new NullReferenceException($"MainLevel: missing required child '{_distractionsPath}'"); }

        // Plain Action wiring; all three nodes live exactly as long as the level, so nothing to unsubscribe
        distractions.DistractionStarted += () => office.InputLocked = true;
        distractions.DistractionCompleted += () => office.InputLocked = false;
    }
}
