using Godot;
using System;
using System.Linq;

/// <summary>
/// Handler class for simple physics based trajectory projections
/// <para>Originally built around ThrowPaperBall, scope allows alternative uses</para>
/// </summary>
public partial class Projection : Node2D
{
    /// <summary>
    /// Gravity scale applied during simulation.
    /// <para><b>Should be set externally</b> to match the simulated body.</para>
    /// </summary>
    public float GravityScale { get; set; } = 1;

    /// <summary>
    /// Linear damping applied during simulation.
    /// <para><b>Should be set externally</b> to match the simulated body.</para>
    /// </summary>
    public float Damp { get; set; } = 0;
    private Vector2 _gravity;
    private int _steps;
    // Number of drawn steps
    private int _visibleSteps = 0;
    private Sprite2D[] _sprites = [];

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Get all sprites bound to this handler
        _sprites = GetChildren().OfType<Sprite2D>().ToArray();
        _steps = _sprites.Length;

        // Query the necessary physics parameters
        _gravity = (Vector2)ProjectSettings.GetSetting("physics/2d/default_gravity_vector")
         * (float)ProjectSettings.GetSetting("physics/2d/default_gravity");
    }

    /// <summary>
    /// Simulation of the movement of a body given a starting impulse and initialized parameters
    /// <para>By default simulates as many steps as drawn sprites, see <see cref="ModifyProjectionSteps"/></para>
    /// </summary>
    public void Project(Vector2 impulse, float timeStep, int? steps = null)
    {
        Simulate(impulse, timeStep, steps, _gravity * GravityScale);
    }

    /// <summary>
    /// Simulation of the movement of a body given a starting impulse and initialized parameters, ignoring gravity
    /// <para>By default simulates as many steps as drawn sprites, see <see cref="ModifyProjectionSteps"/></para>
    /// </summary>
    public void ProjectWithoutGravity(Vector2 impulse, float timeStep, int? steps = null)
    {
        Simulate(impulse, timeStep, steps, Vector2.Zero);
    }

    // Shared additive simulation loop used by both Project and ProjectWithoutGravity
    private void Simulate(Vector2 impulse, float timeStep, int? steps, Vector2 gravity)
    {
        // Safeguard for default and overflowing parameter values
        steps ??= _visibleSteps;
        if (steps > _steps) { steps = _steps; }

        Vector2 velocity = impulse;
        Vector2 position = Vector2.Zero;

        for (int i = 0; i < steps; i++)
        {
            velocity += gravity * timeStep;
            velocity *= Mathf.Max(0f, 1 - Damp * timeStep);
            position += velocity * timeStep;
            _sprites[i].Position = position;
        }
    }

    /// <summary>
    /// Modulate the amount of projection steps drawn, overflow and underflow are normalized
    /// </summary>
    public void ModifyProjectionSteps(int num)
    {
        _visibleSteps = num;
        // Normalize negative and overflowing values
        if (num < 0) { _visibleSteps = 0; }
        if (num > _steps) { _visibleSteps = _steps; }

        // Set visibility of each "step" accordingly
        for (int i = 0; i < _steps; i++)
        {
            if (i < _visibleSteps) { _sprites[i].Visible = true; }
            else { _sprites[i].Visible = false; }
        }
    }

    /// <summary>
    /// Sets the maximum number of projection steps available to draw
    /// <para>Parameterless call to <see cref="ModifyProjectionSteps"/></para>
    /// </summary>
    public void DrawMaxSteps()
    {
        ModifyProjectionSteps(_steps);
    }

    /// <summary>
    /// Hides all projection steps
    /// <para>Parameterless call to <see cref="ModifyProjectionSteps"/></para>
    /// </summary>
    public void HideAllSteps()
    {
        ModifyProjectionSteps(0);
    }

    /// <summary>
    /// Sets only the first projection step to draw
    /// <para>Parameterless call to <see cref="ModifyProjectionSteps"/></para>
    /// </summary>
    public void DrawOneStep()
    {
        ModifyProjectionSteps(1);
    }

}
