using Godot;

/// <summary>
/// Shared idle-pulse look for <see cref="InteractableFeedback"/>. One resource can drive any number of elements.
/// </summary>
[GlobalClass]
public partial class IdlePulseStyle : Resource
{
    /// <summary>Peak colour of the pulse; multiplies the sprite's own colour.</summary>
    [Export] public Color PulseColor = new(1.15f, 1.15f, 1.15f);

    /// <summary>Seconds from resting to peak, and again back to rest.</summary>
    [Export] public float HalfPeriod = 0.8f;

    /// <summary>Seconds to hold at rest between pulses; 0 pulses back-to-back with no pause.</summary>
    [Export] public float RestPeriod = 2.5f;
}
