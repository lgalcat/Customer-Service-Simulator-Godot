using Godot;
using System;

/// <summary>
/// Handler script purpose built for the paper bin in "ThrowPaperBall"
/// <para>Handles win condition detection and the bin's optional movement: a constant-speed loop along an
/// editor-drawn <see cref="Path2D"/> (a sibling, never a child, since its curve is followed in absolute
/// coordinates). An open path is closed with a straight segment; no path or zero speed keeps the bin static.</para>
/// </summary>
public partial class TrashCan : Node2D
{
    // Path the bin loops along, starting from its first point; null keeps the bin static
    [ExportGroup("Movement")]
    [Export]
    private Path2D? _movementPath;
    // Pixels per second along the path; a negative value runs the loop backwards, zero keeps the bin static
    [Export]
    private float _movementSpeed = 40;

    // Closed copy of _movementPath's curve (or the curve itself if already closed) and the bin's progress along it
    private Curve2D? _loop;
    private float _loopLength;
    private float _loopOffset;

    // Area the ball needs to reach to win
    private Area2D _winSpace = null!;
    // Checks the minimum time the win condition needs to be met to be validated
    private Timer _winCounter = null!;
    [Export]
    private float _winTime = 1;

    /// <summary>
    /// Invoked when the paper ball enters the win-condition area.
    /// </summary>
    public Action? BallEntered;

    /// <summary>
    /// Invoked when the paper ball leaves the win-condition area before the win timer completes.
    /// </summary>
    public Action? BallExited;

    /// <summary>
    /// Invoked once the paper ball has remained inside the win-condition area for long enough to win.
    /// </summary>
    public Action? MinigameCompleted;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Find the win condition area within children
        _winSpace = GetNode<Area2D>("TrashCanInside");
        if (_winSpace == null) { throw new NullReferenceException("TrashCan: missing required child 'TrashCanInside'"); }
        // Bind area events to win condition handling
        _winSpace.BodyEntered += OnBodyEntered;
        _winSpace.BodyExited += OnBodyExited;

        // Create and configure the _winCounter object
        _winCounter = new Timer
        {
            OneShot = true,
            WaitTime = _winTime
        };
        AddChild(_winCounter);
        // Propagate timer completion events to outside components
        _winCounter.Timeout += () => MinigameCompleted?.Invoke();

        ArmMovement();
    }

    // Moves the bin along its loop at constant speed; only called while ArmMovement() left physics processing on
    public override void _PhysicsProcess(double delta)
    {
        _loopOffset = Mathf.PosMod(_loopOffset + _movementSpeed * (float)delta, _loopLength);
        GlobalPosition = _movementPath!.ToGlobal(_loop!.SampleBaked(_loopOffset));
    }

    // Validates the movement exports, closes the path and places the bin at its first point
    // Physics processing (auto-enabled by the engine for the _PhysicsProcess override) stays off unless the
    // movement is usable, so a static bin costs nothing per frame
    private void ArmMovement()
    {
        SetPhysicsProcess(false);
        if (_movementPath == null || _movementSpeed == 0) { return; }

        Curve2D? curve = _movementPath.Curve;
        // Also keeps GetPointPosition(0) below in range (a single point would fail the length check anyway)
        if (curve == null || curve.PointCount < 2)
        {
            GD.PushWarning("TrashCan: movement path needs at least 2 points, the bin stays static");
            return;
        }

        // Curve2D has no "closed" flag (only Curve3D does): close it on a copy so the shared resource stays untouched
        _loop = curve;
        Vector2 first = curve.GetPointPosition(0);
        if (!first.IsEqualApprox(curve.GetPointPosition(curve.PointCount - 1)))
        {
            _loop = (Curve2D)curve.Duplicate();
            _loop.AddPoint(first);
        }

        _loopLength = _loop.GetBakedLength();
        if (_loopLength <= 0)
        {
            GD.PushWarning("TrashCan: movement path has zero length, the bin stays static");
            return;
        }

        _loopOffset = 0;
        GlobalPosition = _movementPath.ToGlobal(_loop.SampleBaked(0));
        SetPhysicsProcess(true);
    }

    // Gets called when an object enters the bin (via _winArea)
    private void OnBodyEntered(Node body)
    {
        // Identify the paper ball by group membership rather than a direct type check, keeping this
        // class decoupled from Ball (mirrors Platformer's Goal.cs / Player.cs "player" group)
        if (body.IsInGroup("ball"))
        {
            // Notify other components of the match
            BallEntered?.Invoke();
            // Start win condition timer
            _winCounter.Start();
        }
    }

    // Gets called when an object leaves the bin (via _winArea)
    private void OnBodyExited(Node body)
    {
        // Check if the body is relevant (see OnBodyEntered)
        if (body.IsInGroup("ball"))
        {
            // Notify other components of the abortion
            BallExited?.Invoke();
            // Reset win condition timer
            _winCounter.Stop();
        }
    }

}
