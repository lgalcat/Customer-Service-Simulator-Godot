using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Threading.Tasks;

// Abstract boilerplate testcases for all implementations of the Distractions abstract class
// Limitations on the GdUnit test discovery pipeline prevent inheritted methods to register as testcases
// All implementations of this class should explicitly declare an override + base for all testcases below
public abstract class DistractionTesting
{
    protected Distraction distraction;

    // Instantiation method to inject with individual initialization steps and artifacts
    protected abstract Distraction CreateDistraction();

    // Common setup steps for all tests
    public virtual void Setup()
    {
        distraction = AutoFree(CreateDistraction());
    }

    // Common teardown for all tests
    public virtual void Teardown()
    {
        distraction?.QueueFree();
    }


    // Tests if the Victory function emits the corresponding DistractionCompleted signal
    public virtual async Task CompletedEmited()
    {
        AssertSignal(distraction).StartMonitoring();
        distraction.Victory();
        await AssertSignal(distraction).IsEmitted(Distraction.SignalName.DistractionCompleted).WithTimeout(1000);
    }

    // Tests if the minigame has specified a desired size for its viewport
    public virtual void NonZeroExpectedViewport()
    {
        AssertThat(distraction.ViewportX).IsGreater(0);
        AssertThat(distraction.ViewportY).IsGreater(0);
    }
}
