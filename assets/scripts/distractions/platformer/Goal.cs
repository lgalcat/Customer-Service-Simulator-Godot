using Godot;
using System;

/// <summary>
/// Win-condition pickup for the Platformer minigame. Reaching it ends the minigame immediately.
/// </summary>
public partial class Goal : Area2D
{
    /// <summary>
    /// Invoked once, the moment the player reaches this goal.
    /// </summary>
    public Action? Reached;

    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    // Checks group membership rather than a concrete type - keeps Goal decoupled from the Player
    // class entirely (mirrors Pile.cs's "piles" group; see Player.cs's matching AddToGroup call)
    private void OnBodyEntered(Node body)
    {
        if (!body.IsInGroup("player")) { return; }

        Reached?.Invoke();
        // Stop reacting to further contact. Platformer.Victory() also unsubscribes from Reached
        // (guards against double-invoking OnVictory) - this is a separate, redundant safeguard on
        // the detection side itself, and avoids continuing to physics-query an already-won goal.
        // Must be deferred: the physics server has this Area2D locked while dispatching
        // BodyEntered, so setting Monitoring directly here throws ("Function blocked during
        // in/out signal") - SetDeferred queues it for the next idle step instead.
        SetDeferred(PropertyName.Monitoring, false);
    }
}
