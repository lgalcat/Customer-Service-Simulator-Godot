using Godot;
using System;
using System.Linq;

public partial class Projection : Node2D
{
    // Should be set externally
    public float _gravityScale = 1;
    // Should be set externally
    public float _damp = 0;
    private Vector2 _gravity;
    private int _steps;
    private Sprite2D[] _sprites = [];

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Get all sprites bound to this handler
        _sprites = GetChildren().OfType<Sprite2D>().ToArray();
        _steps = _sprites.Length;

        // Query the necessary physics parameters
        _gravity = (Vector2)ProjectSettings.GetSetting("physics/2d/default_gravity_vector") * (float)ProjectSettings.GetSetting("physics/2d/default_gravity");
    }

    // Simulation of the movement of a body given a starting impulse and initialized parameters
    public void Project(Vector2 impulse, int steps, float timeStep)
    {
        if (steps > _steps) { steps = _steps; }
        Vector2 velocity = impulse;
        Vector2 position = Vector2.Zero;
        for ( int i = 0; i < steps; i++)
        {
            velocity += _gravity * _gravityScale * timeStep;
            velocity *= Mathf.Max(0f, 1 - _damp * timeStep);
            position += velocity * timeStep;
            _sprites[i].Position = position;
        }
    }

    // [22/07/2026] Consider implementing dynamic detection of steps set to "Visible" before simulation
    /// <summary>
    /// Modulate the amount of projection steps drawn, overflow and underflow are normalized
    /// </summary>
    /// <param name="num"></param>
    public void ModifyProjectionSteps(int num)
    {
        int steps = num;
        // Normalize negative and overflowing values
        if (num < 0) { steps = 0; }
        if (num > _steps) { steps = _steps; }
        // Set visibility of each "step" accordingly
        for (int i = 0; i < _steps; i++)
        {
            if (i < num) { _sprites[i].Visible = true; }
            else { _sprites[i].Visible = false; }
        }
    }

    /// <summary>
    /// Sets the maximum number of projection steps available to draw
    /// </summary>
    public void DrawMaxSteps()
    {
        ModifyProjectionSteps(_steps);
    }

    /// <summary>
    /// Hides all projection steps
    /// </summary>
    public void HideAllSteps()
    {
        ModifyProjectionSteps(0);
    }

    /// <summary>
    /// Sets only the first projection step to draw
    /// </summary>
    public void DrawOneStep()
    {
        ModifyProjectionSteps(1);
    }

}
