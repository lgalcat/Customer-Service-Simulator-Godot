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
    private readonly float _minThrowAngle = 0;
    private readonly float _maxThrowAngle = 60;
    private readonly float _minThrowStrength = 10;
    private readonly float _maxThrowStrength = 500;
    // Time "throwing" takes to cycle between min and max values
    private readonly float _ThrowCycle= 3;

    private Ball? _PaperBall;
    private TrashCan? _PaperBin;
    private Projection? _Projection;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);

        _state = ThrowingState.aiming;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // State machine dependent behaviour
        switch (_state)
        {
            case ThrowingState.aiming:
                // [22/07/2026] Implement aiming projection
                _Projection.Project(/*Change for real vector*/Vector2.Up, 1, 0.1f);

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
                    _PaperBall.Throw(/*Change for real vector*/Vector2.Right);
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

        throw new NotImplementedException();
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
        _Projection.DrawOneStep();
    }

}
