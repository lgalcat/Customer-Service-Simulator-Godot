using Godot;
using System;

// Main script for the paper ball in "ThrowPaperBall"
// handles all logic local to the physics object and its state machine
public partial class Ball : RigidBody2D
{
    // State machine
    // [22/07/2026] May be redundant, only used internally for extra validation
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
    private Timer _timer;
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
    public override void _IntegrateForces(PhysicsDirectBodyState2D state)
    {
        // Handles physics reset when necessary
        if (_state != BallState.overdue) { return; }
        
        // Erase all possible momentum kept from previous attempt
        state.AngularVelocity = 0;
        state.LinearVelocity = Vector2.Zero;
        // Return the body to its default position
        state.Transform = new Transform2D(0, _spawn);
        // Update state machine
        _state = BallState.idle;

        BallReset?.Invoke();
    }

    // Gives the ball an initial impulse and lets it move freely
    public void Throw(Vector2 force)
    {
        // Check correct state
        if (_state != BallState.idle) { return; }
        // Start the motion
        ApplyImpulse(force);
        Freeze = false;
        // Start reset timer
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

    // Pauses the reset timer (ment for use by PaperBin)
    /// <summary>
    /// Pauses the internal reset timer USE SPARINGLY
    /// </summary>
    public void PauseTime()
    {
        if (_state != BallState.thrown) { return; }
        _timer.Paused = true;
    }

    // Resumes the reset timer (ment for use by PaperBin)
    /// <summary>
    /// Resumes internal reset timer USE SPARINGLY
    /// </summary>
    public void ResumeTime()
    {
        if (!_timer.Paused) { return; }
        else { _timer.Paused = false; }
    }

}
