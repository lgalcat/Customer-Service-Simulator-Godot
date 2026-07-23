using Godot;
using System;
using System.Runtime.Serialization;

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
    private readonly float _minThrowAngle = -45;
    private readonly float _maxThrowAngle = 0;
    private float _throwAngle = 0;
    private readonly float _minThrowStrength = 10;
    private readonly float _maxThrowStrength = 500;
    private float _throwStrength = 0;
    // Frames "throwing" takes to cycle between min and max values
    private readonly float _throwCycle= 10;
    private int _cycleScalar = 1;

    private Ball? _PaperBall;
    private TrashCan? _PaperBin;
    private Projection? _Projection;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);

        _state = ThrowingState.aiming;
        _throwAngle = _maxThrowAngle;
        _throwStrength = _minThrowStrength;
        GD.Print(_throwAngle);
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // State machine dependent behaviour
        switch (_state)
        {
            case ThrowingState.aiming:
                // [22/07/2026] Implement aiming projection
                // [23/07/2026] There seem to be issues with the unitary vector generation (may be caused by expected radian based values)
                // Calculate angle increase from last frame
                float cycleDelta = (float)(delta / _throwCycle);
                float angleDelta = (_maxThrowAngle - _minThrowAngle) * cycleDelta;
                if (_throwAngle >= _maxThrowAngle) { _cycleScalar = -1; GD.Print("Minus"); }
                if (_throwAngle <= _minThrowAngle) { _cycleScalar = 1; GD.Print("Plus"); }
                _throwAngle += angleDelta * _cycleScalar;

                _Projection.Project(Vector2.FromAngle(_throwAngle)*100, 1, 0.1f);

                // Check for input to update state machine
                if (Input.IsActionJustPressed("JumpKey"))
                {
                    _state = ThrowingState.charging;
                    _Projection.DrawMaxSteps();
                }
                break;
            case ThrowingState.charging:
                // [22/07/2026] Implement charging simulation
                _Projection.Project(/*Change for real vector */Vector2.Right, 5, 0.5f);
                
                // Check for input to update state machine
                if (Input.IsActionJustReleased("JumpKey"))
                {
                    // Trigger "Throw" action
                    _PaperBall.Throw(/*Change for real vector*/new Vector2(500, 0));
                    _state = ThrowingState.disabled;
                    _Projection.HideAllSteps();
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
        _PaperBin = GetNode<TrashCan>("Stage/TrashCan");
        if (_PaperBin == null) { throw new NullReferenceException(); }
        _PaperBin.MinigameCompleted += Victory;

        // Find Ball and bind its Action(s)
        _PaperBall = GetNode<Ball>("Stage/Ball");
        if (_PaperBall == null) { throw new NullReferenceException(); }
        _PaperBin.BallEntered += _PaperBall.PauseTime;
        _PaperBin.BallExited += _PaperBall.ResumeTime;
        _PaperBall.BallReset += ResetState;

        // Find Projection and set its parameters
        _Projection = GetNode<Projection>("Stage/Projection");
        if (_Projection == null) { throw new NullReferenceException(); }
        _Projection._damp = _PaperBall.LinearDamp;
        _Projection._gravityScale = _PaperBall.GravityScale;

        // throw new NotImplementedException();
    }

    // Invoked by PaperBin, freezes simulations and notifies relevant systems upstream
    public override void Victory()
    {
        throw new NotImplementedException();
    }

    // Resets the state machine after the ball resets
    private void ResetState()
    {
        _state = ThrowingState.aiming;
        _throwAngle = _maxThrowAngle;
        _throwStrength = _minThrowStrength;
        _cycleScalar = 1;
        _Projection.DrawOneStep();
    }

}
