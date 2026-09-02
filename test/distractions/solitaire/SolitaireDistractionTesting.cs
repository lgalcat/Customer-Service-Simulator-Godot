using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// TestSuite for testing of the Solitaire class
[TestSuite]
[RequireGodotRuntime]
public class SolitaireDistractionTesting : DistractionTesting
{
    protected override Distraction CreateDistraction()
    {
        // Barebones stand-in tree matching only the node names/types Setup() looks up.
        // _randomizeDeal is deliberately left at its default (false), so _cardScene never needs to
        // be assigned - Setup() then only exercises node-wiring, not DealBuilder. If the future
        // data-driven deal source changes what Setup() unconditionally requires, revisit this stand-in.
        var solitaire = new Solitaire();
        var stage = new Node2D { Name = "Stage" };
        solitaire.AddChild(stage);
        stage.AddChild(new Node2D { Name = "DragLayer" });

        var tableauPiles = new Node2D { Name = "TableauPiles" };
        stage.AddChild(tableauPiles);
        for (int i = 0; i < 7; i++)
        {
            tableauPiles.AddChild(new TableauPile { Name = $"TableauPile{i}" });
        }

        var foundationPiles = new Node2D { Name = "FoundationPiles" };
        stage.AddChild(foundationPiles);
        for (int i = 0; i < 4; i++)
        {
            foundationPiles.AddChild(new FoundationPile { Name = $"Foundation{(Suit)i}" });
        }

        stage.AddChild(new StockPile { Name = "StockPile" });
        stage.AddChild(new WastePile { Name = "WastePile" });

        return solitaire;
    }

    [BeforeTest]
    public override void Setup()
    {
        base.Setup();
    }

    [AfterTest]
    public override void Teardown()
    {
        base.Teardown();
    }

    [TestCase]
    public override void VictoryInvoked() { base.VictoryInvoked(); }

    [TestCase]
    public override void NonZeroExpectedViewport() { base.NonZeroExpectedViewport(); }

    [TestCase]
    public override void SetupAssignsDifficulty() { base.SetupAssignsDifficulty(); }

    [TestCase]
    public override void VictoryDoesNotThrowWhenOnVictoryUnset() { base.VictoryDoesNotThrowWhenOnVictoryUnset(); }
}
