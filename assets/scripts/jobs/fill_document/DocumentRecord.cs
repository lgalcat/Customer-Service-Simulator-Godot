using System;
using System.Collections.Generic;
using System.Linq;
using Godot;

/// <summary>
/// One complete set of field values - the customer or case a Fill Document form is about. Generated
/// by the job, then shown in full on the monitor and partly on the document.
/// </summary>
public sealed class DocumentRecord
{
    /// <summary>
    /// One field of the record and its value.
    /// </summary>
    public readonly record struct Entry(FieldSpec Spec, string Value);

    /// <summary>
    /// The record's fields, in the order they were generated (the document's line order).
    /// </summary>
    public IReadOnlyList<Entry> Entries { get; }

    private DocumentRecord(IReadOnlyList<Entry> entries)
    {
        Entries = entries;
    }

    /// <summary>
    /// Generates one value for each of <paramref name="specs"/>, keeping their order.
    /// </summary>
    /// <exception cref="ArgumentException">Two specs share a <see cref="FieldSpec.Key"/>.</exception>
    public static DocumentRecord Generate(IEnumerable<FieldSpec> specs)
    {
        Entry[] entries = specs.Select(spec => new Entry(spec, spec.Generate())).ToArray();

        string? duplicate = entries.GroupBy(entry => entry.Spec.Key.ToString()).FirstOrDefault(group => group.Count() > 1)?.Key;
        if (duplicate != null) { throw new ArgumentException($"DocumentRecord: duplicate field key '{duplicate}'", nameof(specs)); }

        return new DocumentRecord(entries);
    }

    /// <summary>
    /// The value of the field keyed <paramref name="key"/>.
    /// </summary>
    /// <exception cref="KeyNotFoundException">The record has no such field.</exception>
    public string ValueOf(StringName key)
    {
        foreach (Entry entry in Entries)
        {
            if (entry.Spec.Key == key) { return entry.Value; }
        }
        throw new KeyNotFoundException($"DocumentRecord: no field keyed '{key}'");
    }
}
