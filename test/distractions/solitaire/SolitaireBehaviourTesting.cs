using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

// TestSuite for Solitaire-specific behaviour
// (see DistractionTesting/SolitaireDistractionTesting for the contract-level checks)
[TestSuite]
[RequireGodotRuntime]
public class SolitaireBehaviourTesting : DistractionTesting
{
    private const string ScenePath = "res://assets/scenes/distractions/solitaire/solitaire.tscn";
    private const string CardScenePath = "res://assets/scenes/distractions/solitaire/card.tscn";

    protected override Distraction CreateDistraction()
    {
        // Full scene instancing (as opposed to barebones object instancing)
        return GD.Load<PackedScene>(ScenePath).Instantiate<Solitaire>();
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

    // Every test below needs a live SceneTree: Setup()'s _Ready()-triggered call does real dealing/
    // wiring work these tests specifically observe, so they load their own runner rather than using
    // the shared "distraction" field the base Setup() builds (which never enters a live tree)
    private static ISceneRunner LoadRunner(out Solitaire solitaire)
    {
        ISceneRunner runner = ISceneRunner.Load(ScenePath, true, true);
        solitaire = (Solitaire)runner.Scene()!;
        return runner;
    }

    private static FoundationPile GetFoundation(Solitaire solitaire, Suit suit)
    {
        return solitaire.GetNode<FoundationPile>($"Stage/FoundationPiles/Foundation{suit}");
    }

    // Builds a manufactured King, parented under dragLayer first (Pile.AddCards' Reparent() needs an
    // existing parent, mirrors DealBuilder's own technique) - mid-test-body creation needs explicit AutoFree
    private static Card BuildKingCard(Node2D dragLayer, Suit suit)
    {
        Card card = AutoFree(GD.Load<PackedScene>(CardScenePath).Instantiate<Card>())!;
        card.Configure(suit, 13, false, dragLayer);
        dragLayer.AddChild(card);
        return card;
    }

    // Rule: a full standard 52-card deck is in play from the start, none of it sitting on foundations/waste.
    // Exercises DealBuilder via the real scene's _randomizeDeal = true wiring - once the alternate
    // data-driven deal source exists (and may become the scene's default), this test (and
    // SetupDealsOnlyTopTableauCardFaceUp) will need patching or re-pointing, or DealBuilder may
    // deserve its own dedicated component suite decoupled from Solitaire.Setup() entirely
    [TestCase]
    public void SetupDealsAllFiftyTwoCardsAcrossTableausAndStock()
    {
        using ISceneRunner runner = LoadRunner(out Solitaire solitaire);

        int dealtCount = 0;
        for (int i = 0; i < 7; i++)
        {
            dealtCount += solitaire.GetNode<TableauPile>($"Stage/TableauPiles/TableauPile{i}").Cards.Count;
        }
        dealtCount += solitaire.GetNode<StockPile>("Stage/StockPile").Cards.Count;

        AssertThat(dealtCount).IsEqual(52);
        foreach (Suit suit in Enum.GetValues<Suit>())
        {
            AssertThat(GetFoundation(solitaire, suit).Cards.Count).IsEqual(0);
        }
        AssertThat(solitaire.GetNode<WastePile>("Stage/WastePile").Cards.Count).IsEqual(0);
    }

    // Rule: traditional Klondike dealing only turns each tableau's last-dealt card face-up
    // Same _randomizeDeal-dependency reminder as SetupDealsAllFiftyTwoCardsAcrossTableausAndStock
    [TestCase]
    public void SetupDealsOnlyTopTableauCardFaceUp()
    {
        using ISceneRunner runner = LoadRunner(out Solitaire solitaire);

        for (int i = 0; i < 7; i++)
        {
            TableauPile tableau = solitaire.GetNode<TableauPile>($"Stage/TableauPiles/TableauPile{i}");
            for (int cardIndex = 0; cardIndex < tableau.Cards.Count; cardIndex++)
            {
                bool expectedFaceUp = cardIndex == tableau.Cards.Count - 1;
                AssertThat(tableau.Cards[cardIndex].IsFaceUp).IsEqual(expectedFaceUp);
            }
        }
    }

    // Proves stock.Waste = waste (set in Setup()) actually took effect, not just that it compiles -
    // a real click at the stock's own position (no hardcoded coordinates) must move a card to waste
    [TestCase]
    public async Task SetupWiresStockToWaste()
    {
        using ISceneRunner runner = LoadRunner(out Solitaire solitaire);
        StockPile stock = solitaire.GetNode<StockPile>("Stage/StockPile");
        WastePile waste = solitaire.GetNode<WastePile>("Stage/WastePile");
        int stockCountBefore = stock.Cards.Count;

        runner.SimulateMouseMove(stock.GlobalPosition);
        await runner.SimulateFrames(2);
        runner.SimulateMouseButtonPressed(MouseButton.Left);
        await runner.SimulateFrames(2);

        AssertThat(stock.Cards.Count).IsEqual(stockCountBefore - 1);
        AssertThat(waste.Cards.Count).IsEqual(1);
        AssertThat(waste.TopCard!.IsFaceUp).IsTrue();
    }

    // Rule: fewer than the required number of simultaneously-Kinged foundations must not win the game
    [TestCase]
    public void FoundationFillsBelowRequiredThresholdDoNotTriggerVictory()
    {
        using ISceneRunner runner = LoadRunner(out Solitaire solitaire);
        Node2D dragLayer = solitaire.GetNode<Node2D>("Stage/DragLayer");
        int requiredToWin = solitaire.Get("_foundationsRequiredToWin").AsInt32();
        bool victoryCalled = false;
        solitaire.OnVictory = () => { victoryCalled = true; };
        Suit[] suits = Enum.GetValues<Suit>();

        for (int i = 0; i < requiredToWin - 1; i++)
        {
            GetFoundation(solitaire, suits[i]).AddCards(new List<Card> { BuildKingCard(dragLayer, suits[i]) });
        }

        AssertThat(victoryCalled).IsFalse();
    }

    // Rule: the game is won exactly when the required number of foundations simultaneously hold a King
    [TestCase]
    public void WinConditionTriggersWhenRequiredFoundationsSimultaneouslyHoldKings()
    {
        using ISceneRunner runner = LoadRunner(out Solitaire solitaire);
        Node2D dragLayer = solitaire.GetNode<Node2D>("Stage/DragLayer");
        int requiredToWin = solitaire.Get("_foundationsRequiredToWin").AsInt32();
        int victoryCount = 0;
        solitaire.OnVictory = () => { victoryCount++; };
        Suit[] suits = Enum.GetValues<Suit>();

        for (int i = 0; i < requiredToWin; i++)
        {
            GetFoundation(solitaire, suits[i]).AddCards(new List<Card> { BuildKingCard(dragLayer, suits[i]) });
        }

        AssertThat(victoryCount).IsEqual(1);
    }

    // Regression test for OnFoundationFilled's live-recheck design: since foundation drag-off is
    // legal, Filled can re-fire for a foundation that isn't newly completing anything (a King
    // dragged off and back on). A naive accumulating counter would miscount this replay as a new
    // fill; the live recheck must not be fooled by it, before or after the real win condition
    [TestCase]
    public void FoundationDragOffAndBackDoesNotPrematurelyCountTowardVictory()
    {
        using ISceneRunner runner = LoadRunner(out Solitaire solitaire);
        Node2D dragLayer = solitaire.GetNode<Node2D>("Stage/DragLayer");
        int requiredToWin = solitaire.Get("_foundationsRequiredToWin").AsInt32();
        int victoryCount = 0;
        solitaire.OnVictory = () => { victoryCount++; };
        Suit[] suits = Enum.GetValues<Suit>();

        for (int i = 0; i < requiredToWin - 1; i++)
        {
            GetFoundation(solitaire, suits[i]).AddCards(new List<Card> { BuildKingCard(dragLayer, suits[i]) });
        }

        FoundationPile replayedFoundation = GetFoundation(solitaire, suits[0]);
        Card replayedKing = replayedFoundation.TopCard!;
        replayedFoundation.RemoveCards(new List<Card> { replayedKing });
        replayedFoundation.AddCards(new List<Card> { replayedKing });
        AssertThat(victoryCount).IsEqual(0);

        GetFoundation(solitaire, suits[requiredToWin - 1]).AddCards(new List<Card> { BuildKingCard(dragLayer, suits[requiredToWin - 1]) });

        AssertThat(victoryCount).IsEqual(1);
    }

    // Tests that Victory's foreach "Filled -= OnFoundationFilled" unsubscribe actually takes effect:
    // a further foundation replay after winning must not re-trigger Victory
    [TestCase]
    public void VictoryUnsubscribesFoundationListeners()
    {
        using ISceneRunner runner = LoadRunner(out Solitaire solitaire);
        Node2D dragLayer = solitaire.GetNode<Node2D>("Stage/DragLayer");
        int requiredToWin = solitaire.Get("_foundationsRequiredToWin").AsInt32();
        int victoryCount = 0;
        solitaire.OnVictory = () => { victoryCount++; };
        Suit[] suits = Enum.GetValues<Suit>();
        for (int i = 0; i < requiredToWin; i++)
        {
            GetFoundation(solitaire, suits[i]).AddCards(new List<Card> { BuildKingCard(dragLayer, suits[i]) });
        }
        AssertThat(victoryCount).IsEqual(1);

        FoundationPile foundation = GetFoundation(solitaire, suits[0]);
        Card king = foundation.TopCard!;
        foundation.RemoveCards(new List<Card> { king });
        foundation.AddCards(new List<Card> { king });

        AssertThat(victoryCount).IsEqual(1);
    }
}
