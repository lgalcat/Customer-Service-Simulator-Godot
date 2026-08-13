using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Linq;

// Dedicated component suite for Projection, independent of the Distraction hierarchy.
// Covers Projection's own trajectory simulation and step-visibility logic
// For per instance testing and component wiring tests see ThrowPaperBallBehaviourTesting
[TestSuite]
[RequireGodotRuntime]
public class ProjectionTesting
{
    private const int SpriteCount = 3;
    private Projection _projection = null!;
    private Sprite2D[] _sprites = null!;

    [BeforeTest]
    public void Setup()
    {
        // Barebones setup for a Projection object (testing independent from any scene)
        _projection = AutoFree(new Projection())!;
        _sprites = new Sprite2D[SpriteCount];
        for (int i = 0; i < SpriteCount; i++)
        {
            _sprites[i] = new Sprite2D();
            _projection.AddChild(_sprites[i]);
        }
        // Implement any additional pre test logic here
    }

    [AfterTest]
    public void Teardown()
    {
        // Node cleanup is handled by AutoFree(...)
        // Implement any additional post test logic here
    }

    // Verify proper child identification and filtering at start up
    [TestCase]
    public void ReadyPopulatesStepsFromSpriteChildren()
    {
        // Extra non-sprite child, created mid-test so it needs its own AutoFree registration,
        // to confirm the OfType<Sprite2D>() filter excludes it from step counting
        _projection.AddChild(AutoFree(new Node2D())!);
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();

        _projection.DrawMaxSteps();

        foreach (Sprite2D sprite in _sprites)
        {
            AssertThat(sprite.Visible).IsTrue();
        }
    }

    // Hand-unrolled (not a re-implemented loop) expected trajectory for the first two steps,
    // cross-checked against the real ProjectSettings gravity Projection._Ready() itself reads
    // [12/08/2026] Investigate existance of accesible physics API to check against real simulation values
    [TestCase]
    public void ProjectSimulatesGravityDampedTrajectory()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();
        // Default entry parameters
        _projection.GravityScale = 2f;
        _projection.Damp = 0.5f;
        Vector2 impulse = new Vector2(100f, -50f);
        const float timeStep = 0.1f;
        // Fetch current project settings (Projection is expected to do the same)
        Vector2 gravityVector = (Vector2)ProjectSettings.GetSetting("physics/2d/default_gravity_vector");
        float gravityMagnitude = (float)ProjectSettings.GetSetting("physics/2d/default_gravity");
        Vector2 gravity = gravityVector * gravityMagnitude * _projection.GravityScale;
        float dampFactor = Mathf.Max(0f, 1 - _projection.Damp * timeStep);
        // Replica of expected behaviour to compare resulting values
        Vector2 velocity = impulse;
        Vector2 position = Vector2.Zero;
        velocity += gravity * timeStep;
        velocity *= dampFactor;
        position += velocity * timeStep;
        Vector2 expectedStep0 = position;
        velocity += gravity * timeStep;
        velocity *= dampFactor;
        position += velocity * timeStep;
        Vector2 expectedStep1 = position;

        _projection.Project(impulse, timeStep, 2);

        AssertThat(_sprites[0].Position.DistanceTo(expectedStep0)).IsLess(0.001f);
        AssertThat(_sprites[1].Position.DistanceTo(expectedStep1)).IsLess(0.001f);
    }

    // Verify gravity-less simulations correctly ignore gravity parameters.
    // No gravity applied means the projection will always
    // stay collinear with the initial impulse vector
    [TestCase]
    public void ProjectWithoutGravityIgnoresGravityScale()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();
        _projection.GravityScale = 100f;
        Vector2 impulse = new Vector2(100f, 0f);
        const float timeStep = 0.1f;

        _projection.ProjectWithoutGravity(impulse, timeStep, 2);

        AssertThat(_sprites[1].Position.Normalized().DistanceTo(impulse.Normalized())).IsLess(0.001f);
    }

    // Verify expected default parameter values in main logic
    // not providing explicit numbers should default to previous render configuration
    [TestCase]
    public void ProjectDefaultsToVisibleStepCountWhenStepsNotSpecified()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();
        _projection.ModifyProjectionSteps(2);

        _projection.Project(new Vector2(100f, 0f), 0.1f);

        AssertThat(_sprites[0].Position).IsNotEqual(Vector2.Zero);
        AssertThat(_sprites[1].Position).IsNotEqual(Vector2.Zero);
        // [12/08/2026] As of today sprites are never explicitly reset to Zero between steps, consider if they should
        AssertThat(_sprites[2].Position).IsEqual(Vector2.Zero);
    }

    // Verify out of bounds input parameters are treated as expected
    // Requesting more projection steps than available just defaults to max
    [TestCase]
    public void ProjectClampsStepsToAvailableSpriteCount()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();

        _projection.Project(new Vector2(100f, 0f), 0.1f, 100);

        foreach (Sprite2D sprite in _sprites)
        {
            AssertThat(sprite.Position).IsNotEqual(Vector2.Zero);
        }
    }

    // Unlike ModifyProjectionSteps, Simulate has no explicit clamp for negative steps -
    // this verifies that it's still a harmless no-op (the loop condition never executes)
    [TestCase]
    public void ProjectWithNegativeStepsIsANoOp()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();

        _projection.Project(new Vector2(100f, 0f), 0.1f, -5);

        foreach (Sprite2D sprite in _sprites)
        {
            AssertThat(sprite.Position).IsEqual(Vector2.Zero);
        }
    }

    // Verify public render configuration interface properly applies changes
    // Also verifies out of bounds input parameter safeguards
    // [12/08/2026] Maybe a [TestCase(inputValues)] approach could reduce code duplication¿?, consider later
    [TestCase]
    public void ModifyProjectionStepsClampsAndTogglesVisibility()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();

        // Normal behaviour assertion
        _projection.ModifyProjectionSteps(2);
        AssertThat(_sprites[0].Visible).IsTrue();
        AssertThat(_sprites[1].Visible).IsTrue();
        AssertThat(_sprites[2].Visible).IsFalse();

        // Negative out of bounds assertion
        _projection.ModifyProjectionSteps(-5);
        foreach (Sprite2D sprite in _sprites)
        {
            AssertThat(sprite.Visible).IsFalse();
        }

        // Positive out of bounds assertion
        _projection.ModifyProjectionSteps(100);
        foreach (Sprite2D sprite in _sprites)
        {
            AssertThat(sprite.Visible).IsTrue();
        }
    }

    // Verify render configuration wrappers correctly delegate to main method
    // collectively checks all 3 wrappers of the same general method
    [TestCase]
    public void DrawMaxStepsHideAllStepsAndDrawOneStepDelegateCorrectly()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();

        _projection.DrawMaxSteps();
        AssertThat(_sprites.Count(s => s.Visible)).IsEqual(SpriteCount);

        _projection.HideAllSteps();
        AssertThat(_sprites.Count(s => s.Visible)).IsEqual(0);

        _projection.DrawOneStep();
        AssertThat(_sprites[0].Visible).IsTrue();
        AssertThat(_sprites.Count(s => s.Visible)).IsEqual(1);
    }

    // Verify damp cutoff safeguards in simulation logic
    // Very high damp or slow speeds could result in inversion of movement direction if untreated
    [TestCase]
    public void HighDampingClampsVelocityInsteadOfReversing()
    {
        // Manual setup call, no SceneTree to call _Ready()
        _projection._Ready();
        _projection.GravityScale = 0f; // isolate damping from gravity
        _projection.Damp = 1000f;
        Vector2 impulse = new Vector2(100f, 0f);
        const float timeStep = 0.1f;

        _projection.Project(impulse, timeStep, 2);

        // Damp*timeStep >> 1 clamps to a zero velocity factor (Mathf.Max(0f, ...)) rather than
        // going negative and reversing direction - position should never move from the origin
        AssertThat(_sprites[0].Position).IsEqual(Vector2.Zero);
        AssertThat(_sprites[1].Position).IsEqual(Vector2.Zero);
    }
}