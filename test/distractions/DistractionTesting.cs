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
    /// Tests if the Victory function invokes the "OnVictory" action
    /// </summary>
    public virtual void VictoryInvoked()
    {
        bool check = false;
        distraction.OnVictory = () => { check = true; };
        distraction.Victory();
        AssertThat(check).IsTrue();
    }

    /// <summary>
    /// Tests if the minigame has specified a desired size for its viewport
    /// </summary>
    public virtual void NonZeroExpectedViewport()
    {
        AssertThat(distraction.ViewportX).IsGreater(0);
        AssertThat(distraction.ViewportY).IsGreater(0);
    }
}
