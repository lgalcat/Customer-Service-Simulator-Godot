using System;
using System.Collections.Generic;
using System.Linq;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// DocumentRecord over single-value specs, so every generated value is known
[TestSuite]
[RequireGodotRuntime]
public class DocumentRecordTesting
{
    // The record follows the document's line order, which is what the monitor lists
    [TestCase]
    public void GenerateMakesOneEntryPerSpecInSpecOrder()
    {
        FieldSpec name = Spec("name", "Name", "Ana Torres");
        FieldSpec city = Spec("city", "City", "Carrow");
        FieldSpec id = Spec("id", "ID", "AB-12345");

        DocumentRecord record = DocumentRecord.Generate(new[] { name, city, id });

        AssertThat(record.Entries.Select(entry => entry.Spec)).ContainsSameExactly(name, city, id);
        AssertThat(record.Entries.Select(entry => entry.Value)).ContainsExactly("Ana Torres", "Carrow", "AB-12345");
    }

    [TestCase]
    public void ValueOfReturnsTheValueGeneratedForThatKey()
    {
        DocumentRecord record = DocumentRecord.Generate(new[] { Spec("name", "Name", "Ana Torres"), Spec("city", "City", "Carrow") });

        AssertThat(record.ValueOf("city")).IsEqual("Carrow");
    }

    [TestCase]
    public void ValueOfAnUnknownKeyThrows()
    {
        DocumentRecord record = DocumentRecord.Generate(new[] { Spec("name", "Name", "Ana Torres") });

        AssertThrown(() => record.ValueOf("city")).IsInstanceOf<KeyNotFoundException>();
    }

    // Blanks and results are keyed by field: two lines sharing a key could never be told apart
    [TestCase]
    public void GenerateRejectsADuplicateKeyNamingIt()
    {
        FieldSpec[] specs = { Spec("name", "Name", "Ana"), Spec("name", "Full name", "Ben") };

        AssertThrown(() => DocumentRecord.Generate(specs)).IsInstanceOf<ArgumentException>()
            .StartsWithMessage("DocumentRecord: duplicate field key 'name'");
    }

    [TestCase]
    public void GeneratePropagatesAMisAuthoredSpec()
    {
        FieldSpec[] specs = { Spec("name", "Name", "Ana"), Spec("city", "City") };

        AssertThrown(() => DocumentRecord.Generate(specs)).IsInstanceOf<InvalidOperationException>()
            .StartsWithMessage("FieldSpec 'city'");
    }
}
