using System;
using System.Linq;
using GdUnit4;
using static GdUnit4.Assertions;

// JamCode on its own: entering a code press by press, and drawing random ones. Sequences use panel-like
// indices (1..3 = the function buttons) but nothing here depends on the real panel
[TestSuite]
[RequireGodotRuntime]
public class JamCodeTesting
{
    private const int Draws = 300;

    private static JamCode.Result[] PressAll(JamCode code, params int[] buttons) => buttons.Select(code.Press).ToArray();

    // ---- Building ----

    [TestCase]
    public void KeepsTheSequenceInOrderWithNothingEnteredYet()
    {
        var code = new JamCode(new[] { 2, 1, 3, 1 });

        AssertThat(code.Sequence).ContainsExactly(2, 1, 3, 1);
        AssertThat(code.Progress).IsEqual(0);
    }

    [TestCase]
    public void CopiesTheSequenceItIsGiven()
    {
        int[] source = { 1, 2 };
        var code = new JamCode(source);

        source[0] = 3;

        AssertThat(code.Sequence).ContainsExactly(1, 2);
    }

    [TestCase]
    public void EmptySequenceThrows()
    {
        AssertThrown(() => new JamCode(Array.Empty<int>())).IsInstanceOf<ArgumentException>();
    }

    // ---- Entering ----

    [TestCase]
    public void EnteringTheCodeInOrderSolvesItOnTheLastPress()
    {
        var code = new JamCode(new[] { 2, 1, 3 });

        AssertThat(PressAll(code, 2, 1, 3)).ContainsExactly(JamCode.Result.Correct, JamCode.Result.Correct, JamCode.Result.Solved);
    }

    [TestCase]
    public void AWrongPressResetsTheEntryButNotTheCode()
    {
        var code = new JamCode(new[] { 2, 1, 3 });
        PressAll(code, 2, 1);

        AssertThat(code.Press(2)).IsEqual(JamCode.Result.Wrong);
        AssertThat(code.Progress).IsEqual(0);
        AssertThat(code.Sequence).ContainsExactly(2, 1, 3);
    }

    // What the player does after a mistake. The wrong button here is the code's own first one: counted as
    // the start of a new entry, it would put the restart one step ahead, out of step with a code that
    // repeats its first button
    [TestCase]
    public void AfterAWrongPressTheWholeCodeFromTheStartSolves()
    {
        var code = new JamCode(new[] { 2, 2, 3 });
        PressAll(code, 2, 2, 2);

        AssertThat(PressAll(code, 2, 2, 3).Last()).IsEqual(JamCode.Result.Solved);
    }

    [TestCase]
    public void AfterAWrongPressTheRestOfTheCodeAloneDoesNotSolve()
    {
        var code = new JamCode(new[] { 2, 1, 3 });
        PressAll(code, 2, 3);

        AssertThat(PressAll(code, 1, 3)).ContainsExactly(JamCode.Result.Wrong, JamCode.Result.Wrong);
    }

    [TestCase]
    public void AfterSolvingTheNextPressStartsANewEntry()
    {
        var code = new JamCode(new[] { 2, 1 });
        PressAll(code, 2, 1);

        AssertThat(PressAll(code, 2, 1)).ContainsExactly(JamCode.Result.Correct, JamCode.Result.Solved);
    }

    [TestCase]
    public void ResetClearsTheEntryAndKeepsTheCode()
    {
        var code = new JamCode(new[] { 2, 1, 3 });
        PressAll(code, 2, 1);

        code.Reset();

        AssertThat(code.Progress).IsEqual(0);
        AssertThat(PressAll(code, 2, 1, 3).Last()).IsEqual(JamCode.Result.Solved);
    }

    // ---- Random ----

    [TestCase(0)]
    [TestCase(1)]
    [TestCase(5)]
    public void RandomCodeHasTheAskedLengthAndNeverNone(int length)
    {
        AssertThat(JamCode.Random(length, 1, 3).Sequence.Count).IsEqual(Math.Max(1, length));
    }

    // Both bounds are buttons a code may use: an exclusive end would leave the last button out
    [TestCase]
    public void RandomCodeUsesEveryButtonInItsRangeAndNothingElse()
    {
        int[] drawn = Enumerable.Range(0, Draws).SelectMany(_ => JamCode.Random(4, 1, 3).Sequence).ToArray();

        AssertThat(drawn.Distinct().OrderBy(button => button)).ContainsExactly(1, 2, 3);
    }
}
