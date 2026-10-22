using System;
using System.Collections.Generic;
using Godot;

/// <summary>
/// The button sequence that clears a paper jam in the Print Report job, plus how far the player's
/// entry has got. A wrong press resets the entry, never the code.
/// </summary>
public class JamCode
{
    /// <summary>
    /// What a single press did to the entry.
    /// </summary>
    public enum Result { Correct, Wrong, Solved }

    private readonly int[] _sequence;

    /// <summary>
    /// The buttons to press, in order (panel indices).
    /// </summary>
    public IReadOnlyList<int> Sequence => _sequence;

    /// <summary>
    /// How many buttons of <see cref="Sequence"/> the player has entered correctly so far.
    /// </summary>
    public int Progress { get; private set; }

    /// <summary>
    /// Builds a code over an explicit sequence.
    /// </summary>
    /// <exception cref="ArgumentException"><paramref name="sequence"/> is empty.</exception>
    public JamCode(IEnumerable<int> sequence)
    {
        _sequence = new List<int>(sequence).ToArray();
        if (_sequence.Length == 0) { throw new ArgumentException("JamCode: sequence must not be empty", nameof(sequence)); }
    }

    /// <summary>
    /// A random code of <paramref name="length"/> buttons drawn uniformly from
    /// <paramref name="firstButton"/>..<paramref name="lastButton"/> (inclusive); repeats allowed.
    /// </summary>
    public static JamCode Random(int length, int firstButton, int lastButton)
    {
        var sequence = new int[Math.Max(1, length)];
        for (int i = 0; i < sequence.Length; i++) { sequence[i] = GD.RandRange(firstButton, lastButton); }
        return new JamCode(sequence);
    }

    /// <summary>
    /// Records a press. Once <see cref="Result.Solved"/>, further presses start a new entry.
    /// </summary>
    public Result Press(int button)
    {
        if (Progress >= _sequence.Length) { Progress = 0; }

        if (_sequence[Progress] != button)
        {
            Progress = 0;
            return Result.Wrong;
        }

        Progress++;
        return Progress == _sequence.Length ? Result.Solved : Result.Correct;
    }

    /// <summary>
    /// Clears the entry (the code stays the same).
    /// </summary>
    public void Reset()
    {
        Progress = 0;
    }
}
