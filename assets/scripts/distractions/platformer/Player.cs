using Godot;
using System;

/// <summary>
/// Player-controlled character for the Platformer minigame. Ground movement uses separate
/// acceleration/deceleration/skid rates, airborne movement has its own (frictionless) acceleration,
/// and jump height varies with how long JumpKey is held - loosely modelled after NES Super Mario Bros.
/// Tuning values are derived from a condensed NES SMB physics reference (per-frame hex values converted
/// to px/sec). No further tile-scale correction is applied - this project's tiles are 16px, matching
/// SMB's own tile size exactly, so the converted values are used as-is.
/// </summary>
public partial class Player : CharacterBody2D
{
    // Control state machine - resolved from physics state each tick (see UpdateState), not driven
    // by animation signals: the physics rise/fall duration is variable (jump hold time), so nothing
    // about it can be inferred from a fixed-length animation completing.
    private enum PlayerState { err, idle, walking, jumping, falling }
    private PlayerState _state = PlayerState.err;

    // Below this horizontal speed the player counts as "idle" rather than "walking"
    [Export] private float _walkingSpeedThreshold = 1f;

    [Export] private float _moveSpeed = 94f;
    [Export] private float _groundAcceleration = 183f;
    [Export] private float _groundDeceleration = 183f;
    [Export] private float _skidDeceleration = 365.5f;
    // Below this speed, input opposing current motion just re-accelerates the other way instead of skidding first
    [Export] private float _skidTurnaroundSpeed = 34f;
    [Export] private float _airAcceleration = 133.5f;
    [Export] private float _jumpVelocity = 240f;
    [Export] private float _gravity = 1350f;
    // Gravity scale applied while still rising and JumpKey is held - lower means a taller jump the longer it's held
    [Export] private float _jumpHoldGravityScale = 0.31f;
    [Export] private float _maxFallSpeed = 270f;

    private AnimatedSprite2D _sprite = null!;

    public override void _Ready()
    {
        _sprite = GetNode<AnimatedSprite2D>("AnimatedSprite2D");
        // Lets other components (e.g. Goal) identify the player without a direct class reference
        AddToGroup("player");
    }

    public override void _PhysicsProcess(double delta)
    {
        float dt = (float)delta;
        float inputAxis = Input.GetAxis("MoveLeftKey", "MoveRightKey");

        ApplyGravityAndJump(dt);
        ApplyHorizontalMovement(inputAxis, dt);

        MoveAndSlide();

        UpdateFacing(inputAxis);
        UpdateState();
    }

    // CharacterBody2D has no built-in gravity. Full gravity applies except while the player is still
    // rising and holding JumpKey, where a reduced scale lets a longer hold produce a higher jump.
    private void ApplyGravityAndJump(float delta)
    {
        if (IsOnFloor())
        {
            if (Input.IsActionJustPressed("JumpKey"))
            {
                Velocity = new Vector2(Velocity.X, -_jumpVelocity);
            }
            return;
        }

        bool sustainingJump = Velocity.Y < 0 && Input.IsActionPressed("JumpKey");
        float gravity = sustainingJump ? _gravity * _jumpHoldGravityScale : _gravity;
        Velocity = new Vector2(Velocity.X, Mathf.Min(Velocity.Y + gravity * delta, _maxFallSpeed));
    }

    // Eases horizontal velocity toward the input-scaled target speed. Picks one of three rates:
    // accelerating toward the input direction, coasting to a stop with no input, or skidding to a
    // stop when input opposes current motion (the classic Mario "skid" before actually reversing).
    // Airborne movement uses its own rate and never "brakes" on its own - momentum just holds.
    private void ApplyHorizontalMovement(float inputAxis, float delta)
    {
        float target = inputAxis * _moveSpeed;
        float rate;

        if (!IsOnFloor())
        {
            rate = _airAcceleration;
        }
        else if (Mathf.IsZeroApprox(inputAxis))
        {
            rate = _groundDeceleration;
        }
        else if (Mathf.Abs(Velocity.X) > _skidTurnaroundSpeed && Mathf.Sign(inputAxis) != Mathf.Sign(Velocity.X))
        {
            rate = _skidDeceleration;
        }
        else
        {
            rate = _groundAcceleration;
        }

        Velocity = new Vector2(Mathf.MoveToward(Velocity.X, target, rate * delta), Velocity.Y);
    }

    // No separate left/right-facing frames exist - flip the sprite to face the last direction moved,
    // holding that facing while idle instead of snapping back to a default
    private void UpdateFacing(float inputAxis)
    {
        if (!Mathf.IsZeroApprox(inputAxis))
        {
            _sprite.FlipH = inputAxis < 0;
        }
    }

    // Resolves this tick's animation state from the post-move physics result and plays it on change
    private void UpdateState()
    {
        PlayerState next = !IsOnFloor()
            ? (Velocity.Y < 0 ? PlayerState.jumping : PlayerState.falling)
            : (Mathf.Abs(Velocity.X) > _walkingSpeedThreshold ? PlayerState.walking : PlayerState.idle);

        if (next == _state) { return; }
        _state = next;
        _sprite.Play(_state.ToString());
    }
}
