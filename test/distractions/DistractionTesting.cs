using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// Abstract boilerplate testcases for all implementations of the Distractions abstract class
// Limitations on the GdUnit test discovery pipeline prevent inheritted methods to register as testcases
// All implementations of this class should explicitly declare an override + base for all testcases below
public abstract class DistractionTesting
{
    protected Distraction distraction = null!;

    /// <summary>
    /// Instantiation method to inject with individual initialization steps and artifacts
    /// </summary>
    protected abstract Distraction CreateDistraction();

    /// <summary>
    /// Common setup steps for all tests
    /// </summary>
    public virtual void Setup()
    {
        distraction = AutoFree(CreateDistraction());
    }

    /// <summary>
    /// Common teardown for all tests
    /// </summary>
    public virtual void Teardown()
    {
        // Cleanup is handled by AutoFree(...) in Setup(), no explicit freeing needed here
    }


    /// <summary>
    /// Tests if the Victory function invokes the "OnVictory" action exactly once
    /// </summary>
    public virtual void VictoryInvoked()
    {
        int invocationCount = 0;
        distraction.OnVictory = () => { invocationCount++; };
        distraction.Victory();
        AssertThat(invocationCount).IsEqual(1);
    }

    /// <summary>
    /// Tests if the minigame has specified a desired size for its viewport
    /// </summary>
    public virtual void NonZeroExpectedViewport()
    {
        AssertThat(distraction.ViewportX).IsGreater(0);
        AssertThat(distraction.ViewportY).IsGreater(0);
    }

    /// <summary>
    /// Tests if Setup assigns the given difficulty to the Difficulty property
    /// </summary>
    public virtual void SetupAssignsDifficulty()
    {
        const int difficulty = 3;
        distraction.Setup(difficulty);
        AssertThat(distraction.Difficulty).IsEqual(difficulty);
    }

    /// <summary>
    /// Tests that Victory does not throw when no listener has subscribed to OnVictory,
    /// since OnVictory is declared optional/nullable
    /// </summary>
    public virtual void VictoryDoesNotThrowWhenOnVictoryUnset()
    {
        AssertThat(distraction.OnVictory).IsNull();
        distraction.Victory();
    }
}
