using Godot;
using System;

// Main script for the paper ball in "ThrowPaperBall"
// handles all logic local to the physics object and its state machine
public partial class Ball : RigidBody2D
{
    // State machine
    private enum BallState{ err, idle, thrown, overdue }
    private BallState _state = BallState.err;
    // Public accessor to check state machine
    /// <summary>
    /// external objects should refrain from modifying the object if this returns false
    /// </summary>
    public bool IsIdle { get => _state == BallState.idle; }

    // Coordinates the ball holds during the idle state
    private Vector2 _spawn = new Vector2();

    // Internal timer that controls how long the ball is left free moving before it resets
    private Timer _timer = null!;
    private float _lifeTime = 3;

    public Action? BallReset;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Create and configure the internal life timer
        _timer = new Timer
        {
            OneShot = true,
            WaitTime = _lifeTime
        };
        AddChild(_timer);
        _timer.Timeout += Reset;

        // Sets the initial state of physics and state machine
        _state = BallState.idle;
        Freeze = true;
        _spawn = Position;
    }

    // Godot's native physics processing method
    //Limit direct tampering of physics properties to this method
    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        // Handles physics reset when necessary
        if (_state != BallState.overdue) { return; }
        
        // Erase all possible momentum kept from previous attempt
        state.AngularVelocity = 0;
        state.LinearVelocity = Vector2.Zero;
        // Find the final spawn point in absolute coordinates
        Vector2 globalSpawn = GetParent<Node2D>().ToGlobal(_spawn);
        // Return the body to its default position
        state.Transform = new Transform2D(0, globalSpawn);
        // Update state machine
        _state = BallState.idle;

        BallReset?.Invoke();
    }

    /// <summary>
    /// Gives the ball an initial impulse and lets it move freely
    /// </summary>
    public void Throw(Vector2 impulse)
    {
        // Safeguard for correct state
        if (_state != BallState.idle) { return; }

        Freeze = false;
        ApplyImpulse(impulse);
        _timer.Start();

        // Update state machine
        _state = BallState.thrown;
    }

    // Resets the ball to its original position and freezes its movement
    private void Reset()
    {
        // Just-in-case timer reset
        _timer.Stop();
        _timer.Paused = false;

        Freeze = true;
        // Request physics reset via state machine update
        _state = BallState.overdue;
    }

    // Pauses the reset timer (meant for use by PaperBin)
    /// <summary>
    /// Pauses the internal reset timer <b>USE SPARINGLY</b>
    /// </summary>
    public void PauseTime()
    {
        if (_state != BallState.thrown) { return; }
        _timer.Paused = true;
    }

    // Resumes the reset timer (meant for use by PaperBin)
    /// <summary>
    /// Resumes internal reset timer <b>USE SPARINGLY</b>
    /// </summary>
    public void ResumeTime()
    {
        if (!_timer.Paused) { return; }
        _timer.Paused = false;
    }

}
