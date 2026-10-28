using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// FieldSpec generation and comparison over hand-made specs. Generation is random, so every case checks
// a property over many draws instead of a value
[TestSuite]
[RequireGodotRuntime]
public class FieldSpecTesting
{
    private const int Draws = 200;

    private static IEnumerable<string> Generate(FieldSpec spec, int draws = Draws) =>
        Enumerable.Range(0, draws).Select(_ => spec.Generate()).ToArray();

    // The values are copied by the player character by character, so they must not follow the machine's
    // locale; restores the previous culture whatever happens
    private static T UnderCulture<T>(string culture, Func<T> action)
    {
        CultureInfo previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = new CultureInfo(culture);
        try { return action(); }
        finally { CultureInfo.CurrentCulture = previous; }
    }

    // ---- Pool ----

    // Exactly the pool: every value is reached and nothing else is ever drawn
    [TestCase]
    public void PoolDrawsEveryValueAndNothingElse()
    {
        FieldSpec spec = Spec("city", "City", "Ashford", "Bellmoor", "Carrow");

        AssertThat(Generate(spec).Distinct()).ContainsExactlyInAnyOrder("Ashford", "Bellmoor", "Carrow");
    }

    [TestCase]
    public void PoolWithNoValuesThrowsNamingTheField()
    {
        FieldSpec spec = Spec("city", "City");

        AssertThrown(() => spec.Generate()).IsInstanceOf<InvalidOperationException>().StartsWithMessage("FieldSpec 'city'");
    }

    // ---- Pattern ----

    [TestCase]
    public void PatternReplacesEachLWithALetterAndEachHashWithADigitKeepingTheRest()
    {
        const string pattern = "L#-x L.#";

        foreach (string value in Generate(PatternSpec(pattern)))
        {
            AssertThat(value).HasLength(pattern.Length);
            for (int i = 0; i < pattern.Length; i++)
            {
                bool fits = pattern[i] switch
                {
                    'L' => value[i] is >= 'A' and <= 'Z',
                    '#' => value[i] is >= '0' and <= '9',
                    _ => value[i] == pattern[i],
                };
                AssertThat(fits).OverrideFailureMessage($"'{value}' does not fit '{pattern}' at {i}").IsTrue();
            }
        }
    }

    // Catches a symbol range cut short at either end
    [TestCase]
    public void PatternReachesEveryLetterAndDigitOverManyDraws()
    {
        string[] values = Generate(PatternSpec("L#"), 2000).ToArray();

        AssertThat(values.Select(value => value[0]).Distinct().Count()).IsEqual(26);
        AssertThat(values.Select(value => value[1]).Distinct().Count()).IsEqual(10);
    }

    [TestCase]
    public void EmptyPatternThrowsNamingTheField()
    {
        AssertThrown(() => PatternSpec("").Generate()).IsInstanceOf<InvalidOperationException>().StartsWithMessage("FieldSpec 'pattern'");
    }

    // ---- Date ----

    [TestCase]
    public void DateFallsWithinItsYearRangeInItsFormat()
    {
        FieldSpec spec = DateSpec(2020, 2021, "yyyy-MM-dd");

        foreach (string value in Generate(spec))
        {
            DateTime date = DateTime.ParseExact(value, spec.DateFormat, CultureInfo.InvariantCulture);
            AssertThat(date >= new DateTime(2020, 1, 1) && date <= new DateTime(2021, 12, 31))
                .OverrideFailureMessage($"{value} is outside 2020-2021").IsTrue();
        }
    }

    // Equal bounds are valid: the inverted-range guard must not reject them
    [TestCase]
    public void DateWithASingleYearStaysInThatYear()
    {
        FieldSpec spec = DateSpec(2024, 2024, "yyyy-MM-dd");

        AssertThat(Generate(spec).All(value => value.StartsWith("2024-"))).IsTrue();
    }

    [TestCase]
    public void DateWithAnInvertedRangeThrowsNamingTheField()
    {
        AssertThrown(() => DateSpec(2026, 2020).Generate()).IsInstanceOf<InvalidOperationException>().StartsWithMessage("FieldSpec 'date'");
    }

    // '/' in a .NET date format is the culture's date separator ('.' in de-DE)
    [TestCase]
    public void DateIgnoresTheMachineCulture()
    {
        FieldSpec spec = DateSpec(2020, 2026, "dd/MM/yyyy");

        string[] values = UnderCulture("de-DE", () => Generate(spec).ToArray());

        AssertThat(values.All(value => Regex.IsMatch(value, @"^\d{2}/\d{2}/\d{4}$"))).IsTrue();
    }

    // ---- Amount ----

    [TestCase]
    public void AmountFallsWithinItsRangeWithTwoDecimals()
    {
        FieldSpec spec = AmountSpec(10f, 20f);

        foreach (string value in Generate(spec))
        {
            AssertThat(Regex.IsMatch(value, @"^\d+\.\d{2}$")).OverrideFailureMessage($"'{value}' is not an amount with two decimals").IsTrue();
            decimal amount = decimal.Parse(value, CultureInfo.InvariantCulture);
            AssertThat(amount >= 10m && amount <= 20m).OverrideFailureMessage($"{value} is outside 10-20").IsTrue();
        }
    }

    // Equal bounds are valid (see the date case); also pins the two-decimal format on a known value
    [TestCase]
    public void AmountWithEqualBoundsIsExactlyThatAmount()
    {
        AssertThat(Generate(AmountSpec(12.5f, 12.5f)).Distinct()).ContainsExactly("12.50");
    }

    [TestCase]
    public void AmountWithAnInvertedRangeThrowsNamingTheField()
    {
        AssertThrown(() => AmountSpec(20f, 10f).Generate()).IsInstanceOf<InvalidOperationException>().StartsWithMessage("FieldSpec 'amount'");
    }

    [TestCase]
    public void AmountIgnoresTheMachineCulture()
    {
        string[] values = UnderCulture("de-DE", () => Generate(AmountSpec(10f, 5000f)).ToArray());

        AssertThat(values.All(value => value.Contains('.') && !value.Contains(','))).IsTrue();
    }

    // ---- Kind ----

    [TestCase]
    public void UnknownKindThrows()
    {
        FieldSpec spec = new() { Key = "odd", Kind = (FieldSpec.FieldKind)99 };

        AssertThrown(() => spec.Generate()).IsInstanceOf<InvalidOperationException>().StartsWithMessage("FieldSpec 'odd'");
    }

    // ---- Comparison ----
    // Deliberately not pinned, open for the content pass: whether spacing inside digit fields ("555 010 203"
    // vs "555010203") or diacritics should be ignored

    [TestCase("  Ana Torres  ", "ANA TORRES")]
    [TestCase("12   Elm\t\tStreet", "12 ELM STREET")]
    [TestCase("a \t b", "A B")]
    [TestCase("ab-12/x", "AB-12/X")]
    [TestCase("", "")]
    [TestCase(" \t ", "")]
    public void NormalizeTrimsCollapsesWhitespaceAndUpperCases(string value, string expected)
    {
        AssertThat(FieldSpec.Normalize(value)).IsEqual(expected);
    }

    // Turkish upper-cases 'i' to a dotted 'İ'
    [TestCase]
    public void NormalizeIgnoresTheMachineCulture()
    {
        AssertThat(UnderCulture("tr-TR", () => FieldSpec.Normalize("city"))).IsEqual("CITY");
    }

    [TestCase("Ana Torres", "Ana Torres", true)]
    [TestCase("Ana Torres", "  ana   TORRES ", true)]
    [TestCase("Ana Torres", "Ana Torre", false)]
    [TestCase("Ana Torres", "", false)]
    public void MatchesComparesNormalizedValues(string expected, string input, bool matches)
    {
        AssertThat(FieldSpec.Matches(expected, input)).IsEqual(matches);
    }
}
