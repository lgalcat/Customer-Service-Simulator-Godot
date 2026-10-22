using System;
using Godot;

/// <summary>
/// The chance that a print in the Print Report job runs into trouble, rolled once per print. It
/// slides to dampen streaks without removing bad luck: it rises after each clean print and drops
/// after each trouble, within [<see cref="Min"/>, <see cref="Max"/>].
/// <para>It moves on trouble (the luck), not on whether the player then caught it (the skill), so a
/// player who catches every warning is not handed more trouble for it.</para>
/// </summary>
public class TroubleOdds
{
    private readonly Func<float> _roll;

    /// <summary>
    /// Current probability that the next print has trouble.
    /// </summary>
    public float Chance { get; private set; }

    /// <summary>Added to <see cref="Chance"/> after a clean print.</summary>
    public float Rise { get; }

    /// <summary>Subtracted from <see cref="Chance"/> after a print with trouble.</summary>
    public float Drop { get; }

    /// <summary>Lowest <see cref="Chance"/> can go.</summary>
    public float Min { get; }

    /// <summary>Highest <see cref="Chance"/> can go.</summary>
    public float Max { get; }

    /// <summary>
    /// Builds the odds. <paramref name="roll"/> supplies uniform values in [0, 1]; it defaults to
    /// Godot's own RNG (<c>GD.Randf</c>) and is the seam for driving the odds deterministically.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="min"/> is above <paramref name="max"/>.</exception>
    public TroubleOdds(float start, float rise, float drop, float min, float max, Func<float>? roll = null)
    {
        if (min > max) { throw new ArgumentException($"TroubleOdds: min ({min}) is above max ({max})", nameof(min)); }

        Rise = rise;
        Drop = drop;
        Min = min;
        Max = max;
        Chance = Math.Clamp(start, min, max);
        _roll = roll ?? GD.Randf;
    }

    /// <summary>
    /// Whether the next print has trouble. Doesn't move the odds: report the outcome with
    /// <see cref="RecordClean"/> / <see cref="RecordTrouble"/>.
    /// </summary>
    public bool Roll()
    {
        return _roll() < Chance;
    }

    /// <summary>
    /// A print finished without trouble: trouble becomes more likely.
    /// </summary>
    public void RecordClean()
    {
        Chance = Math.Clamp(Chance + Rise, Min, Max);
    }

    /// <summary>
    /// A print ran into trouble (caught or not): trouble becomes less likely.
    /// </summary>
    public void RecordTrouble()
    {
        Chance = Math.Clamp(Chance - Drop, Min, Max);
    }
}
