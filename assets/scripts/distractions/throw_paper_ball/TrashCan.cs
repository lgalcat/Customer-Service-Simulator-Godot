using Godot;
using System;

/// <summary>
/// Main script of the paper bin in "ThrowPaperBall"
/// <para>Handles possible movement patterns and win condition detection</para>
/// </summary>
public partial class TrashCan : Node2D
{
    // [18/07/2026] Implementation of movement attributes and logic pending

    // Area the ball needs to reach to win
    private Area2D _winSpace = null!;
    // Checks the minimum time the win condition needs to be met to be validated
    private Timer _winCounter = null!;
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
        if (_winSpace == null) { throw new NullReferenceException(); }
        else
        {
            // Bind area events to win condition handling
            _winSpace.BodyEntered += OnBodyEntered;
            _winSpace.BodyExited += OnBodyExited;
        }

        // Create and configure the _winCounter object
        _winCounter = new Timer
        {
            OneShot = true,
            WaitTime = _winTime
        };
        AddChild(_winCounter);
        // Propagate timer completion events to outside components
        _winCounter.Timeout += () => MinigameCompleted?.Invoke();
    }

    // Gets called when an object enters the bin (via _winArea)
    private void OnBodyEntered(Node body)
    {
        // Check if the body is relevant (the paper ball), via name for better class decoupling
        // [18/07/2026] Object name is hardcoded, consider parametrization for more flexibility
        if (body.Name == "Ball")
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
        // Check if the body is relevant, via name for better class decoupling
        // [18/07/2026] Object name is hardcoded, consider parametrization for more flexibility
        if (body.Name == "Ball")
        {
            // Notify other components of the abortion
            BallExited?.Invoke();
            // Reset win condition timer
            _winCounter.Stop();
        }
    }

}
