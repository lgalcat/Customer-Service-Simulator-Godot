using Godot;
using System;

/// <summary>
/// Handler script purpose built for a fly in "FlySwatter"
/// <para>Handles all logic local to the fly and its state machine</para>
/// </summary>
public partial class Fly : Area2D
{
    // State machine
    private enum FlyState { err, alive, dead }
    private FlyState _state = FlyState.err;
    /// <summary>
    /// external objects should check the object's state via this attribute
    /// </summary>
    public bool IsAlive { get => _state == FlyState.alive; }

    /// <summary>
    /// Invoked once, when this fly is swatted.
    /// </summary>
    public Action? Died;

    // Internal timer that controls how long the fly is left drifting in its "dead" state before it deletes itself
    private Timer _timer = null!;
    [Export]
    private float _deathDuration = 2;
    // Constant drift applied to Position while dead
    [Export]
    private Vector2 _deadVelocity = new Vector2(0, 40);

    // Alive-state movement: speed range a step can roll into
    [Export]
    private float _minSpeed = 30;
    [Export]
    private float _maxSpeed = 80;
    // Alive-state movement: how long a single step (straight or arc) lasts before a new one is rolled
    [Export]
    private float _minStepDuration = 0.3f;
    [Export]
    private float _maxStepDuration = 0.8f;
    // Alive-state movement: chance a new step is a sweeping arc rather than a straight line
    [Export]
    private float _arcChance = 0.7f;
    // Alive-state movement: angular velocity range (magnitude, sign rolled separately) for arc steps
    [Export]
    private float _minTurnRate = 120;
    [Export]
    private float _maxTurnRate = 300;
    // Alive-state movement: magnitude range (sign rolled separately) of the drastic per-step redirect
    [Export]
    private float _minHeadingDeviation = 60;
    [Export]
    private float _maxHeadingDeviation = 160;
    // Alive-state movement: chance a new step also re-rolls speed, rather than carrying the previous one over
    [Export]
    private float _speedChangeChance = 0.6f;
    // Alive-state movement: playspace limits (Stage-local space) Position is clamped into every frame
    [Export]
    private Rect2 _movementBounds = new Rect2(-110, -110, 220, 220);

    // Alive-state movement: current heading/turn-rate/speed and time left on the current step
    private float _headingDeg;
    private float _turnRateDegPerSec;
    private float _speed;
    private float _stepTimeRemaining;

    // Child node references found during "_Ready"
    private AnimatedSprite2D _sprite = null!;
    // Corrects for the source art's neutral facing direction; heading 0 points along +X, tune in the inspector once the sprite is visible
    [Export]
    private float _spriteFacingOffsetDeg = 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Find the sprite component and start the "alive" animation
        _sprite = GetNode<AnimatedSprite2D>("FlySprite");
        if (_sprite == null) { throw new NullReferenceException(); }
        _sprite.AnimationChanged += OnAnimationChanged;
        _sprite.Play("alive");

        // Create and configure the internal death timer
        _timer = new Timer
        {
            OneShot = true,
            WaitTime = _deathDuration
        };
        AddChild(_timer);
        _timer.Timeout += QueueFree;

        // Seed an initial speed directly so the fly never starts stalled at 0 while waiting on RollNewStep's chance-based reroll
        _speed = RandRange(_minSpeed, _maxSpeed);
        RollNewStep();

        // Sets the initial state of the state machine
        _state = FlyState.alive;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // State machine dependent behaviour
        switch (_state)
        {
            case FlyState.alive:
                _stepTimeRemaining -= (float)delta;
                if (_stepTimeRemaining <= 0f) { RollNewStep(); }

                _headingDeg += _turnRateDegPerSec * (float)delta;
                Position += Vector2.FromAngle(Mathf.DegToRad(_headingDeg)) * _speed * (float)delta;
                ContainWithinBounds();
                _sprite.Rotation = Mathf.DegToRad(_headingDeg + _spriteFacingOffsetDeg);
                break;
            case FlyState.dead:
                // Prospective death movement pattern, refine later
                Position += _deadVelocity * (float)delta;
                _sprite.Rotation = _deadVelocity.Angle() + Mathf.DegToRad(_spriteFacingOffsetDeg);
                break;
            default:
                break;
        }
    }

    // Rolls a new erratic movement step: a large heading redirect, arc-vs-straight (+ turn rate for arcs),
    // and a chance to also change speed - mirroring real houseflies' unpredictable straight/sweeping zig-zagging
    private void RollNewStep()
    {
        _stepTimeRemaining = RandRange(_minStepDuration, _maxStepDuration);

        // Drastic redirect: always deviate from the current heading by a large angle, in a random turn direction
        float deviationSign = GD.Randf() < 0.5f ? -1f : 1f;
        float deviation = RandRange(_minHeadingDeviation, _maxHeadingDeviation) * deviationSign;
        _headingDeg = Mathf.Wrap(_headingDeg + deviation, 0f, 360f);

        // Arc steps curve continuously (in a random direction); straight steps hold heading fixed
        if (GD.Randf() < _arcChance)
        {
            float turnSign = GD.Randf() < 0.5f ? -1f : 1f;
            _turnRateDegPerSec = RandRange(_minTurnRate, _maxTurnRate) * turnSign;
        }
        else
        {
            _turnRateDegPerSec = 0f;
        }

        if (GD.Randf() < _speedChangeChance)
        {
            _speed = RandRange(_minSpeed, _maxSpeed);
        }
    }

    // Wrapper for GD.RandRange's double signature to avoid repeated casts at call sites
    private static float RandRange(float min, float max)
    {
        return (float)GD.RandRange((double)min, (double)max);
    }

    // Guarantees Position never leaves _movementBounds; reflects heading on whichever axis was actually
    // clamped so the fly visibly turns away from the edge instead of sliding along it
    private void ContainWithinBounds()
    {
        Vector2 min = _movementBounds.Position;
        Vector2 max = _movementBounds.End;

        bool clampedX = Position.X <= min.X || Position.X >= max.X;
        bool clampedY = Position.Y <= min.Y || Position.Y >= max.Y;

        Position = Position.Clamp(min, max);

        if (clampedX) { _headingDeg = Mathf.Wrap(180f - _headingDeg, 0f, 360f); }
        if (clampedY) { _headingDeg = Mathf.Wrap(360f - _headingDeg, 0f, 360f); }
    }

    /// <summary>
    /// Marks this fly as swatted, triggering its death sequence and notifying upstream via <see cref="Died"/>
    /// </summary>
    public void Swat()
    {
        // Safeguard for correct state
        if (_state != FlyState.alive) { return; }

        _sprite.Play("dead");
        // Prevent an already-dead fly from being detected again while it drifts
        Monitorable = false;
        _timer.Start();

        // Update state machine
        _state = FlyState.dead;

        Died?.Invoke();
    }

    // Helper function to modulate the transparency of the sprite component
    private void OnAnimationChanged()
    {
        switch(_sprite.Animation)
        {
            case "alive":
                _sprite.Modulate = new Color(1f, 1f, 1f, 1f);
                break;
            case "dead":
                _sprite.Modulate = new Color(1f, 1f, 1f, 0.6f);
                break;
            default:
                break;
        }
    }

}
