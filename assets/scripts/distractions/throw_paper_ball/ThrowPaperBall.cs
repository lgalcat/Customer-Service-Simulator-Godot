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
    private readonly float _minThrowAngle = -60;
    private readonly float _maxThrowAngle = 0;
    private float _throwAngle = 0;
    private readonly float _minThrowStrength = 10;
    private readonly float _maxThrowStrength = 500;
    private float _throwStrength = 0;
    // Time (in seconds) "throwing" takes to cycle between min and max values
    private readonly float _throwCycle= 1;
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
                // [22/07/2026] Implement aiming projection
                // Calculate angle increase from last frame
                float aimingDelta = (float)(delta / _throwCycle);
                float angleDelta = (_maxThrowAngle - _minThrowAngle) * aimingDelta;
                if (_throwAngle >= _maxThrowAngle) { _cycleScalar = -1; }
                if (_throwAngle <= _minThrowAngle) { _cycleScalar = 1; }
                _throwAngle += angleDelta * _cycleScalar;

                // [5/08/2026] Consider ignoring gravity in this projection for cleaner visuals
                _projection.Project(Vector2.FromAngle( Mathf.DegToRad(_throwAngle) ) * 200, 0.1f);

                // Check for input to update state machine
                if (Input.IsActionJustPressed("JumpKey"))
                {
                    _state = ThrowingState.charging;
                    _projection.DrawMaxSteps();
                }
                break;
            case ThrowingState.charging:
                // [22/07/2026] Implement charging simulation
                // Calculate strength increase
                float throwDelta = (float)(delta / _throwCycle);
                float strengthDelta = (_maxThrowStrength - _minThrowStrength) * throwDelta;
                if (_throwStrength >= _maxThrowStrength) { _cycleScalar = -1; }
                if (_throwStrength <= _minThrowStrength) { _cycleScalar = 1; }
                _throwStrength += strengthDelta * _cycleScalar;

                _projection.Project(Vector2.FromAngle( Mathf.DegToRad(_throwAngle) ) * _throwStrength, 0.5f);
                
                // Check for input to update state machine
                if (Input.IsActionJustReleased("JumpKey"))
                {
                    // Trigger "Throw" action
                    _paperBall.Throw(/*Change for real vector*/new Vector2(500, 0));
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
        throw new NotImplementedException();
    }

    // Resets the state machine to default "aiming" state
    // Called after the ball resets via binding to Actions
    private void ResetState()
    {
        _state = ThrowingState.aiming;
        _throwAngle = _maxThrowAngle;
        _throwStrength = _minThrowStrength;
        _cycleScalar = 1;
        _projection.DrawOneStep();
    }

}
