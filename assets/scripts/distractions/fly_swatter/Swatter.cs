using Godot;
using System;

/// <summary>
/// Handler script purpose built for the player-controlled swatter in "FlySwatter"
/// <para>Handles mouse tracking, swing/cooldown state machine, and swat hit-detection</para>
/// </summary>
public partial class Swatter : Node2D
{
    // State machine
    private enum SwatterState { err, idle, cooldown }
    private SwatterState _state = SwatterState.err;
    /// <summary>
    /// external objects should check the object's state via this attribute
    /// </summary>
    public bool CanSwat { get => _state == SwatterState.idle; }

    // How quickly the swatter closes the gap to the mouse each frame; higher = snappier, lower = more trailing lag
    [Export]
    private float _followSharpness = 12f;
    // Time before another swing is allowed after one lands
    [Export]
    private float _cooldownDuration = 1f;

    // Internal timer that gates how soon another swing is allowed
    private Timer _cooldownTimer = null!;

    // Child node references found during "_Ready"
    private ShapeCast2D _hitScan = null!;
    private AnimatedSprite2D _smackFx = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Find the hit-scan and smack FX components
        _hitScan = GetNode<ShapeCast2D>("HitScan");
        if (_hitScan == null) { throw new NullReferenceException(); }

        _smackFx = GetNode<AnimatedSprite2D>("SmackFx");
        if (_smackFx == null) { throw new NullReferenceException(); }
        _smackFx.Visible = false;
        _smackFx.AnimationFinished += () => _smackFx.Visible = false;

        // Create and configure the internal cooldown timer
        _cooldownTimer = new Timer
        {
            OneShot = true,
            WaitTime = _cooldownDuration
        };
        AddChild(_cooldownTimer);
        _cooldownTimer.Timeout += () => _state = SwatterState.idle;

        // Sets the initial state of the state machine
        _state = SwatterState.idle;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // Trailing mouse-follow, independent of state - the swatter keeps tracking the cursor through cooldown
        Vector2 target = GetParent<Node2D>().ToLocal(GetGlobalMousePosition());
        Position = Position.Lerp(target, 1f - Mathf.Exp(-_followSharpness * (float)delta));
    }

    // Called for input events not already consumed elsewhere in the pipeline
    public override void _UnhandledInput(InputEvent @event)
    {
        // [17/08/2026] Currently discussing whether to create an input event bound to Left Click
        // [17/08/2026] Maybe this should be JustPressed instead of only Pressed ¿?
        if (@event is InputEventMouseButton mouseButton && mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
        {
            Swing();
            GetViewport().SetInputAsHandled();
        }
    }

    // Scans the hit area for flies and triggers the swing's visual/cooldown side effects
    private void Swing()
    {
        // Safeguard for correct state
        if (_state != SwatterState.idle) { return; }

        _hitScan.ForceShapecastUpdate();
        for (int i = 0; i < _hitScan.GetCollisionCount(); i++)
        {
            if (_hitScan.GetCollider(i) is Fly fly && fly.IsAlive)
            {
                // [17/08/2026] In the future Swat() may have a return type, if it does this method will also need to change
                fly.Swat();
            }
        }

        _smackFx.Visible = true;
        _smackFx.Play("smack");

        _cooldownTimer.Start();

        // Update state machine
        _state = SwatterState.cooldown;
    }

}