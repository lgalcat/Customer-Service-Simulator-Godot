using System;
using System.Linq;
using GdUnit4;
using static GdUnit4.Assertions;

// TroubleOdds on its own, driven through its roll seam where a roll's value matters. Only the sliding
// rule is pinned; the values a job feeds it (and the long-run rate they give) are balance
[TestSuite]
[RequireGodotRuntime]
public class TroubleOddsTesting
{
    private const int Rolls = 300;

    private static TroubleOdds Odds(float start, float rise = 0.1f, float drop = 0.2f, float min = 0f, float max = 1f, Func<float>? roll = null) =>
        new(start, rise, drop, min, max, roll);

    // ---- Building ----

    [TestCase(0.5f, 0.5f)]
    [TestCase(-1f, 0.2f)]
    [TestCase(3f, 0.8f)]
    public void StartIsClampedIntoTheRange(float start, float expected)
    {
        AssertThat(Odds(start, min: 0.2f, max: 0.8f).Chance).IsEqual(expected);
    }

    // Math.Clamp would throw too, but without naming what was mis-configured
    [TestCase]
    public void MinAboveMaxThrowsNamingTheOdds()
    {
        AssertThrown(() => Odds(0.5f, min: 0.8f, max: 0.2f)).IsInstanceOf<ArgumentException>().StartsWithMessage("TroubleOdds:");
    }

    // Equal bounds are a fixed chance, not an error
    [TestCase]
    public void EqualBoundsAreValid()
    {
        AssertThat(Odds(0f, min: 0.4f, max: 0.4f).Chance).IsEqual(0.4f);
    }

    // ---- Rolling ----

    [TestCase(0.29f, true)]
    [TestCase(0.3f, false)]
    [TestCase(0.31f, false)]
    public void RollHitsOnlyBelowTheChance(float roll, bool hits)
    {
        AssertThat(Odds(0.3f, roll: () => roll).Roll()).IsEqual(hits);
    }

    [TestCase]
    public void RollingNeverMovesTheChance()
    {
        TroubleOdds odds = Odds(0.3f, roll: () => 0f);

        odds.Roll();
        odds.Roll();

        AssertThat(odds.Chance).IsEqual(0.3f);
    }

    // Without a seam it rolls Godot's own RNG: never at 0, always above 1, and both ways in between (missing
    // either outcome in 300 rolls at 0.5 is a ~1e-90 event)
    [TestCase]
    public void DefaultRollIsRandomWithinTheChance()
    {
        TroubleOdds never = Odds(0f);
        TroubleOdds always = Odds(1.5f, max: 1.5f);
        TroubleOdds even = Odds(0.5f);

        AssertThat(Enumerable.Range(0, Rolls).Any(_ => never.Roll())).IsFalse();
        AssertThat(Enumerable.Range(0, Rolls).All(_ => always.Roll())).IsTrue();
        AssertThat(Enumerable.Range(0, Rolls).Select(_ => even.Roll()).Distinct().Count()).IsEqual(2);
    }

    // ---- Sliding ----

    [TestCase]
    public void ACleanPrintRaisesTheChanceByRiseUpToMax()
    {
        TroubleOdds odds = Odds(0.5f, rise: 0.3f, max: 0.9f);

        odds.RecordClean();
        AssertThat(odds.Chance).IsEqualApprox(0.8f, 1e-5f);

        odds.RecordClean();
        AssertThat(odds.Chance).IsEqual(0.9f);
    }

    [TestCase]
    public void ATroubleLowersTheChanceByDropDownToMin()
    {
        TroubleOdds odds = Odds(0.5f, drop: 0.3f, min: 0.1f);

        odds.RecordTrouble();
        AssertThat(odds.Chance).IsEqualApprox(0.2f, 1e-5f);

        odds.RecordTrouble();
        AssertThat(odds.Chance).IsEqual(0.1f);
    }
}
