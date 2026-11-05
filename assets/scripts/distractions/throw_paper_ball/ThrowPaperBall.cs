using Godot;
using System;

/// <summary>
/// Main script of the Throw Paper Ball minigame
/// </summary>
public partial class ThrowPaperBall : Distraction
{
    // Control state machine
    private enum ThrowingState{ err, disabled, aiming, charging }
    private ThrowingState _state = ThrowingState.err;

    // Expected screenspace for the minigame
    private readonly int _viewportX = 340;
    public override int ViewportX { get => _viewportX; }
    private readonly int _viewportY = 220;
    public override int ViewportY { get => _viewportY; }

    // Ranges for the "throwing" angle and strength, copied from the layout during "Setup"
    // Keep in mind angles grow CLOCKWISE
    private float _minThrowAngle;
    private float _maxThrowAngle;
    private float _throwAngle = 0;
    private float _minThrowStrength;
    private float _maxThrowStrength;
    private float _throwStrength = 0;
    // Time (in seconds) "throwing" takes to cycle between min and max values, copied from the layout during "Setup"
    private float _throwCycle;
    private int _cycleScalar = 1;

    // Fixed speed used to draw the aim-preview arc before a throw is charged
    [Export] private float _aimPreviewSpeed = 250;
    // Per-step time deltas fed to the trajectory projection (aiming shows a short stub, charging the full arc)
    [Export] private float _aimProjectionTimeStep = 0.1f;
    [Export] private float _chargeProjectionTimeStep = 0.12f;

    // Layouts rolled from during "Setup" by difficulty; null or empty keeps the authored Stage
    [Export] private ThrowPaperBallLayoutCatalog? _layoutCatalog;

    /// <summary>
    /// Forces this layout instead of rolling one from the catalog; must be set before <see cref="Setup"/>
    /// <para>Meant for tests and playtesting: leave it empty in the saved scene, or the real game always plays it</para>
    /// </summary>
    [Export] public PackedScene? LayoutOverride { get; set; }

    // Child node references found during "Setup"
    private Ball _paperBall = null!;
    private TrashCan _paperBin = null!;
    private Projection _projection = null!;


    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Setup() is the hosting factory's job now (before tree entry); _Ready only kicks off the
        // state machine - ResetState() needs the Setup()-assigned _projection, and _projection's own
        // sprites, both ready by now (children _Ready() before parents)
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

                _projection.ProjectWithoutGravity(Vector2.FromAngle( Mathf.DegToRad(_throwAngle) ) * _aimPreviewSpeed, _aimProjectionTimeStep);

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

                _projection.Project(Vector2.FromAngle( Mathf.DegToRad(_throwAngle) ) * _throwStrength, _chargeProjectionTimeStep);
                
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

        // Pick the layout: the override, else a roll from the catalog, else the authored Stage
        if (LayoutOverride != null) { GD.Print($"ThrowPaperBall: LayoutOverride forces '{LayoutOverride.ResourcePath}'"); }
        PackedScene? chosenLayout = LayoutOverride ?? _layoutCatalog?.Pick(difficulty);
        if (chosenLayout != null) { UseLayout(chosenLayout); }

        // Find the layout and take its throw tuning
        ThrowPaperBallLayout? layout = GetNodeOrNull<ThrowPaperBallLayout>("Stage");
        if (layout == null) { throw new NullReferenceException("ThrowPaperBall: missing required child 'Stage' (a ThrowPaperBallLayout)"); }
        layout.Validate();
        _minThrowAngle = layout.MinThrowAngle;
        _maxThrowAngle = layout.MaxThrowAngle;
        _minThrowStrength = layout.MinThrowStrength;
        _maxThrowStrength = layout.MaxThrowStrength;
        _throwCycle = layout.ThrowCycle;

        // Find TrashCan and bind its Action(s)
        _paperBin = GetNode<TrashCan>("Stage/TrashCan");
        if (_paperBin == null) { throw new NullReferenceException("ThrowPaperBall: missing required child 'Stage/TrashCan'"); }
        _paperBin.MinigameCompleted += Victory;

        // Find Ball and bind its Action(s)
        _paperBall = GetNode<Ball>("Stage/Ball");
        if (_paperBall == null) { throw new NullReferenceException("ThrowPaperBall: missing required child 'Stage/Ball'"); }
        _paperBin.BallEntered += _paperBall.PauseTime;
        _paperBin.BallExited += _paperBall.ResumeTime;
        _paperBall.BallReset += ResetState;

        // Find Projection and set its parameters
        _projection = GetNode<Projection>("Stage/Projection");
        if (_projection == null) { throw new NullReferenceException("ThrowPaperBall: missing required child 'Stage/Projection'"); }
        // The arc starts at the ball's spawn whatever the layout authored for Projection (both are Stage children)
        _projection.Position = _paperBall.Position;
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

    // Replaces the authored Stage with an instance of "layout", unless the authored one already is that layout
    // Runs during "Setup", before tree entry, so the replaced Stage can be freed outright
    private void UseLayout(PackedScene layout)
    {
        Node? authored = GetNodeOrNull("Stage");
        if (authored != null && authored.SceneFilePath == layout.ResourcePath) { return; }

        Node instance = layout.Instantiate();
        if (instance is not ThrowPaperBallLayout stage)
        {
            instance.Free();
            throw new InvalidOperationException($"ThrowPaperBall: layout '{layout.ResourcePath}' has no ThrowPaperBallLayout root");
        }

        if (authored != null)
        {
            RemoveChild(authored);
            authored.Free();
        }
        stage.Name = "Stage";
        AddChild(stage);
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
        _throwStrength = _minThrowStrength + (_maxThrowStrength - _minThrowStrength) / 2;
        _cycleScalar = 1;
        _projection.DrawOneStep();
    }

}
