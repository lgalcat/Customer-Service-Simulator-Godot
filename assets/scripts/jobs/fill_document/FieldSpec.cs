using System;
using System.Globalization;
using System.Text;
using Godot;

/// <summary>
/// One kind of line a Fill Document form can hold (a date, a name, an ID...): its label and how a
/// value for it is generated. Shared as a <c>.tres</c> by every document line and record that uses it.
/// <para>Values are formatted with the invariant culture: the player copies them character by
/// character, so they must not change with the machine's locale.</para>
/// </summary>
[GlobalClass]
public partial class FieldSpec : Resource
{
    /// <summary>
    /// How <see cref="Generate"/> produces a value.
    /// </summary>
    public enum FieldKind
    {
        /// <summary>A random entry of <see cref="Values"/>.</summary>
        Pool,
        /// <summary><see cref="Pattern"/> with each <c>L</c> replaced by a random capital letter and
        /// each <c>#</c> by a random digit; every other character is kept as is.</summary>
        Pattern,
        /// <summary>A random day between <see cref="MinYear"/> and <see cref="MaxYear"/>, formatted
        /// with <see cref="DateFormat"/>.</summary>
        Date,
        /// <summary>A random amount between <see cref="MinAmount"/> and <see cref="MaxAmount"/>, with
        /// two decimals.</summary>
        Amount,
    }

    /// <summary>Identifies the field within one document; must be unique there.</summary>
    [Export] public StringName Key = "";

    /// <summary>Caption shown next to the value, on the document and on the monitor.</summary>
    [Export] public string Label = "";

    /// <summary>Maximum characters the player can type into a blank of this field; 0 means no limit.</summary>
    [Export] public int MaxLength = 24;

    [Export] public FieldKind Kind = FieldKind.Pool;

    /// <summary>Candidate values for <see cref="FieldKind.Pool"/>.</summary>
    [Export] public Godot.Collections.Array<string> Values = new();

    /// <summary>Template for <see cref="FieldKind.Pattern"/>, e.g. <c>LL-#####</c>.</summary>
    [Export] public string Pattern = "";

    /// <summary>First year a <see cref="FieldKind.Date"/> can fall in.</summary>
    [Export] public int MinYear = 2020;

    /// <summary>Last year a <see cref="FieldKind.Date"/> can fall in.</summary>
    [Export] public int MaxYear = 2026;

    /// <summary>.NET date format string for <see cref="FieldKind.Date"/>.</summary>
    [Export] public string DateFormat = "dd/MM/yyyy";

    /// <summary>Smallest <see cref="FieldKind.Amount"/>.</summary>
    [Export] public float MinAmount = 10f;

    /// <summary>Largest <see cref="FieldKind.Amount"/>.</summary>
    [Export] public float MaxAmount = 5000f;

    /// <summary>
    /// Produces a random value for this field.
    /// </summary>
    /// <exception cref="InvalidOperationException">The spec is mis-authored for its <see cref="Kind"/>
    /// (an empty pool or pattern, an inverted range).</exception>
    public string Generate()
    {
        return Kind switch
        {
            FieldKind.Pool => GeneratePool(),
            FieldKind.Pattern => GeneratePattern(),
            FieldKind.Date => GenerateDate(),
            FieldKind.Amount => GenerateAmount(),
            _ => throw new InvalidOperationException($"FieldSpec '{Key}': unknown kind {Kind}"),
        };
    }

    private string GeneratePool()
    {
        if (Values.Count == 0) { throw new InvalidOperationException($"FieldSpec '{Key}': Pool kind with no Values"); }

        return Values[GD.RandRange(0, Values.Count - 1)];
    }

    private string GeneratePattern()
    {
        if (string.IsNullOrEmpty(Pattern)) { throw new InvalidOperationException($"FieldSpec '{Key}': Pattern kind with an empty Pattern"); }

        var value = new StringBuilder(Pattern.Length);
        foreach (char symbol in Pattern)
        {
            value.Append(symbol switch
            {
                'L' => (char)('A' + GD.RandRange(0, 25)),
                '#' => (char)('0' + GD.RandRange(0, 9)),
                _ => symbol,
            });
        }
        return value.ToString();
    }

    private string GenerateDate()
    {
        if (MaxYear < MinYear) { throw new InvalidOperationException($"FieldSpec '{Key}': MaxYear {MaxYear} is before MinYear {MinYear}"); }

        var first = new DateTime(MinYear, 1, 1);
        int days = (new DateTime(MaxYear, 12, 31) - first).Days;
        return first.AddDays(GD.RandRange(0, days)).ToString(DateFormat, CultureInfo.InvariantCulture);
    }

    // Drawn in whole cents so every value is exactly representable with two decimals
    private string GenerateAmount()
    {
        if (MaxAmount < MinAmount) { throw new InvalidOperationException($"FieldSpec '{Key}': MaxAmount {MaxAmount} is below MinAmount {MinAmount}"); }

        int cents = GD.RandRange(Mathf.RoundToInt(MinAmount * 100), Mathf.RoundToInt(MaxAmount * 100));
        return (cents / 100m).ToString("0.00", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Whether the player's <paramref name="input"/> counts as <paramref name="expected"/>: both are
    /// compared after <see cref="Normalize"/>.
    /// </summary>
    public static bool Matches(string expected, string input)
    {
        return Normalize(expected) == Normalize(input);
    }

    /// <summary>
    /// The form two values are compared in: surrounding whitespace trimmed, inner whitespace runs
    /// collapsed to one space, upper-cased (invariant).
    /// </summary>
    public static string Normalize(string value)
    {
        var normalized = new StringBuilder(value.Length);
        bool pendingSpace = false;
        foreach (char symbol in value.Trim())
        {
            if (char.IsWhiteSpace(symbol))
            {
                pendingSpace = true;
                continue;
            }
            if (pendingSpace) { normalized.Append(' '); }
            pendingSpace = false;
            normalized.Append(char.ToUpperInvariant(symbol));
        }
        return normalized.ToString();
    }
}
