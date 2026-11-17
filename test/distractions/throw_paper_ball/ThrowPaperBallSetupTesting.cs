using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;

// Suite for ThrowPaperBall.Setup(): choosing the layout (LayoutOverride, else a catalog roll, else the authored Stage),
// swapping it in, and the guards on what it finds. Split from ThrowPaperBallBehaviourTesting as a cohesive sub-area
// Everything is hand-built and never enters a tree, as Setup() runs before tree entry
// Layouts are packed in memory and given a made-up res:// path: UseLayout recognises "the authored Stage already is
// this layout" by comparing the Stage's SceneFilePath with the layout's ResourcePath, so two pathless scenes would
// always look like the same layout
[TestSuite]
[RequireGodotRuntime]
public class ThrowPaperBallSetupTesting
{
    private const string LayoutAPath = "res://test/fixtures/throw_paper_ball_layout_a.tscn";
    private const string LayoutBPath = "res://test/fixtures/throw_paper_ball_layout_b.tscn";
    private const string NotALayoutPath = "res://test/fixtures/not_a_layout.tscn";
    // The fixture authors Projection away from the ball, so the case covering Setup() moving it can fail
    private static readonly Vector2 BallSpawn = new(-120, 20);

    [TestCase]
    public void LayoutOverrideWinsOverTheCatalog()
    {
        ThrowPaperBall shell = BuildShell(BuildLayout());
        shell.Set("_layoutCatalog", Catalog((PackLayout(LayoutBPath), 1, 1)));
        shell.LayoutOverride = PackLayout(LayoutAPath);

        shell.Setup(1);

        AssertThat(Stage(shell).SceneFilePath).IsEqual(LayoutAPath);
    }

    // The bands don't overlap, so only the right difficulty leads to B
    [TestCase]
    public void CatalogRollUsesTheSetupDifficulty()
    {
        ThrowPaperBall shell = BuildShell(BuildLayout());
        shell.Set("_layoutCatalog", Catalog((PackLayout(LayoutAPath), 1, 1), (PackLayout(LayoutBPath), 5, 5)));

        shell.Setup(5);

        AssertThat(Stage(shell).SceneFilePath).IsEqual(LayoutBPath);
    }

    [TestCase(false)]
    [TestCase(true)]
    public void AuthoredStageIsKeptWithoutAnOverrideOrCatalogPick(bool emptyCatalog)
    {
        ThrowPaperBallLayout authored = BuildLayout();
        ThrowPaperBall shell = BuildShell(authored);
        if (emptyCatalog) { shell.Set("_layoutCatalog", new ThrowPaperBallLayoutCatalog()); }

        shell.Setup(1);

        AssertThat(Stage(shell)).IsSame(authored);
    }

    [TestCase]
    public void AuthoredStageThatIsTheChosenLayoutIsKept()
    {
        PackedScene layoutA = PackLayout(LayoutAPath);
        Node authored = layoutA.Instantiate();
        ThrowPaperBall shell = BuildShell(authored);
        shell.LayoutOverride = layoutA;

        shell.Setup(1);

        AssertThat(Stage(shell)).IsSame(authored);
    }

    [TestCase]
    public void ChosenLayoutReplacesTheAuthoredStage()
    {
        Node authored = PackLayout(LayoutAPath).Instantiate();
        ThrowPaperBall shell = BuildShell(authored);
        shell.LayoutOverride = PackLayout(LayoutBPath);

        shell.Setup(1);

        AssertThat(Stage(shell).SceneFilePath).IsEqual(LayoutBPath);
        AssertThat(GodotObject.IsInstanceValid(authored)).IsFalse();
    }

    [TestCase]
    public void ChosenLayoutIsAddedWhenNoStageIsAuthored()
    {
        ThrowPaperBall shell = BuildShell(null);
        shell.LayoutOverride = PackLayout(LayoutAPath);

        shell.Setup(1);

        AssertThat(Stage(shell).SceneFilePath).IsEqual(LayoutAPath);
    }

    // The rejected instance is freed inside Setup() (an orphan warning in the .trx would mean it isn't)
    [TestCase]
    public void LayoutWithoutALayoutRootThrows()
    {
        ThrowPaperBallLayout authored = BuildLayout();
        ThrowPaperBall shell = BuildShell(authored);
        var notALayout = new Node2D { Name = "Stage" };
        var packed = new PackedScene();
        packed.Pack(notALayout);
        notALayout.Free();
        packed.TakeOverPath(NotALayoutPath);
        shell.LayoutOverride = packed;

        AssertThrown(() => shell.Setup(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage($"ThrowPaperBall: layout '{NotALayoutPath}' has no ThrowPaperBallLayout root");
        AssertThat(Stage(shell)).IsSame(authored);
    }

    // Stage is fetched with GetNodeOrNull<T>, which gives null for a wrong-typed node too, so both shapes reach the guard
    [TestCase(false)]
    [TestCase(true)]
    public void MissingStageThrows(bool wrongType)
    {
        ThrowPaperBall shell = BuildShell(wrongType ? new Node2D { Name = "Stage" } : null);

        AssertThrown(() => shell.Setup(1))
            .IsInstanceOf<NullReferenceException>()
            .HasMessage("ThrowPaperBall: missing required child 'Stage' (a ThrowPaperBallLayout)");
    }

    [TestCase]
    public void MisTunedLayoutFailsSetup()
    {
        ThrowPaperBallLayout authored = BuildLayout();
        authored.ThrowCycle = 0;
        ThrowPaperBall shell = BuildShell(authored);

        AssertThrown(() => shell.Setup(1))
            .IsInstanceOf<InvalidOperationException>()
            .HasMessage("ThrowPaperBallLayout 'Stage': ThrowCycle 0 must be positive");
    }

    [TestCase("TrashCan")]
    [TestCase("Ball")]
    [TestCase("Projection")]
    public void MissingStageChildThrows(string child)
    {
        ThrowPaperBall shell = BuildShell(BuildLayout(omit: child));

        AssertThrown(() => shell.Setup(1))
            .IsInstanceOf<NullReferenceException>()
            .HasMessage($"ThrowPaperBall: missing required child 'Stage/{child}'");
    }

    // GetNode<T>() throws its own cast exception for a present-but-wrong-type child, before the guard
    [TestCase("TrashCan")]
    [TestCase("Ball")]
    [TestCase("Projection")]
    public void WrongTypedStageChildThrows(string child)
    {
        ThrowPaperBallLayout layout = BuildLayout(omit: child);
        layout.AddChild(new Node2D { Name = child });
        ThrowPaperBall shell = BuildShell(layout);

        AssertThrown(() => shell.Setup(1)).IsInstanceOf<InvalidCastException>();
    }

    [TestCase]
    public void ProjectionIsPlacedOnTheBallsSpawn()
    {
        ThrowPaperBall shell = BuildShell(BuildLayout());

        shell.Setup(1);

        AssertThat(shell.GetNode<Projection>("Stage/Projection").Position).IsEqual(BallSpawn);
    }

    // Shell with "stage" (if any) as its authored Stage
    private ThrowPaperBall BuildShell(Node? stage)
    {
        ThrowPaperBall shell = AutoFree(new ThrowPaperBall())!;
        if (stage != null)
        {
            stage.Name = "Stage";
            shell.AddChild(stage);
        }
        return shell;
    }

    // Only the nodes Setup() looks up; Owner is set so Pack() keeps the children
    private static ThrowPaperBallLayout BuildLayout(string? omit = null)
    {
        var stage = new ThrowPaperBallLayout { Name = "Stage" };
        if (omit != "TrashCan") { AddOwned(stage, new TrashCan { Name = "TrashCan" }); }
        if (omit != "Ball") { AddOwned(stage, new Ball { Name = "Ball", Position = BallSpawn }); }
        if (omit != "Projection") { AddOwned(stage, new Projection { Name = "Projection", Position = Vector2.Zero }); }
        return stage;
    }

    private static void AddOwned(Node stage, Node child)
    {
        stage.AddChild(child);
        child.Owner = stage;
    }

    // Pack() copies the tree, so the source is freed at once
    private static PackedScene PackLayout(string path)
    {
        ThrowPaperBallLayout source = BuildLayout();
        var packed = new PackedScene();
        packed.Pack(source);
        source.Free();
        packed.TakeOverPath(path);
        return packed;
    }

    private static ThrowPaperBallLayoutCatalog Catalog(params (PackedScene layout, int min, int max)[] entries)
    {
        var catalog = new ThrowPaperBallLayoutCatalog();
        foreach (var (layout, min, max) in entries)
        {
            catalog.Entries.Add(new ThrowPaperBallLayoutEntry { Layout = layout, MinDifficulty = min, MaxDifficulty = max });
        }
        return catalog;
    }

    private static Node Stage(ThrowPaperBall shell) => shell.GetNode("Stage");
}
