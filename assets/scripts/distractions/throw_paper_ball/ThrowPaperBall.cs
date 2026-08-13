using Godot;
using System;

// Main script of the Throw Paper Ball minigame
public partial class ThrowPaperBall : Distraction
{
    // Control state machine
    private enum ThrowingState{ err, disabled, aiming, charging }
    private ThrowingState _state = ThrowingState.err;

    // Expected screenspace for the minigame
    private readonly float _viewportX = 100;
    public override float ViewportX { get => _viewportX; }
    private readonly float _viewportY = 100;
    public override float ViewportY { get => _viewportY; }

    // Ranges for the "throwing" angle and strength
    // Keep in mind angles grow CLOCKWISE
    private float _minThrowAngle = -60;
    private float _maxThrowAngle = 0;
    private float _throwAngle = 0;
    private float _minThrowStrength = 100;
    private float _maxThrowStrength = 500;
    private float _throwStrength = 0;
    // Time (in seconds) "throwing" takes to cycle between min and max values
    private float _throwCycle= 1;
    private int _cycleScalar = 1;

    // Child node references found during "Setup"
    private Ball _paperBall = null!;
    private TrashCan _paperBin = null!;
    private Projection _projection = null!;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);
        // Call to set the state machine to default values
        ResetState();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // State machine dependent behaviour
        switch (_state)
        {
            case ThrowingState.aiming:
                // _cycleScalar is intentionally carried over into "charging" (not reset here), so the two animated
                // steps keep a continuous back-and-forth motion instead of each restarting its own cycle
                _throwAngle = Oscillate(_throwAngle, _minThrowAngle, _maxThrowAngle, delta, ref _cycleScalar);

                _projection.ProjectWithoutGravity(Vector2.FromAngle( Mathf.DegToRad(_throwAngle) ) * 250, 0.1f);

                // Check for input to update state machine
                if (Input.IsActionJustPressed("JumpKey"))
                {
                    _state = ThrowingState.charging;
                    _projection.DrawMaxSteps();
                }
                break;
            case ThrowingState.charging:
                // Continues the _cycleScalar left over from "aiming" (see comment above)
                _throwStrength = Oscillate(_throwStrength, _minThrowStrength, _maxThrowStrength, delta, ref _cycleScalar);

                _projection.Project(Vector2.FromAngle( Mathf.DegToRad(_throwAngle) ) * _throwStrength, 0.12f);
                
                // Check for input to update state machine
                if (Input.IsActionJustReleased("JumpKey"))
                {
                    // Trigger "Throw" action
                    _paperBall.Throw(Vector2.FromAngle( Mathf.DegToRad(_throwAngle) ) * _throwStrength);
                    _state = ThrowingState.disabled;
                    _projection.HideAllSteps();
                }
                break;
            default:
                break;
        }
    }

    // Find all necessary "child" nodes and set the minigame up before start
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;

        // Implement location and instancing of difficulty dependent elements here

        // Find TrashCan and bind its Action(s)
        _paperBin = GetNode<TrashCan>("Stage/TrashCan");
        if (_paperBin == null) { throw new NullReferenceException(); }
        _paperBin.MinigameCompleted += Victory;

        // Find Ball and bind its Action(s)
        _paperBall = GetNode<Ball>("Stage/Ball");
        if (_paperBall == null) { throw new NullReferenceException(); }
        _paperBin.BallEntered += _paperBall.PauseTime;
        _paperBin.BallExited += _paperBall.ResumeTime;
        _paperBall.BallReset += ResetState;

        // Find Projection and set its parameters
        _projection = GetNode<Projection>("Stage/Projection");
        if (_projection == null) { throw new NullReferenceException(); }
        _projection.Damp = _paperBall.LinearDamp;
        _projection.GravityScale = _paperBall.GravityScale;
    }

    // Invoked by PaperBin, freezes simulations and notifies relevant systems upstream
    public override void Victory()
    {
        // Insert any additional victory animations and logic here

        GD.Print("ThrowPaperBall Completed!!!");
        OnVictory?.Invoke();
    }

    // Moves "value" toward max/min and back, flipping "scalar" at either bound; drives both the aiming and charging animated steps
    private float Oscillate(float value, float min, float max, double delta, ref int scalar)
    {
        float step = (float)(delta / _throwCycle) * (max - min);
        if (value >= max) { scalar = -1; }
        if (value <= min) { scalar = 1; }
        return value + step * scalar;
    }

    // Resets the state machine to default "aiming" state
    // Called after the ball resets via binding to Actions
    private void ResetState()
    {
        _state = ThrowingState.aiming;
        _throwAngle = _maxThrowAngle;
        _throwStrength = (_maxThrowStrength - _minThrowStrength) / 2;
        _cycleScalar = 1;
        _projection.DrawOneStep();
    }

}
