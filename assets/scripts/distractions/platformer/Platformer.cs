using Godot;
using System;

/// <summary>
/// Main script of the Platformer minigame
/// </summary>
public partial class Platformer : Distraction
{
    // Expected display window for the minigame - smaller than the level itself, which is revealed through it by
    // the layout's scrolling Player/MainCamera
    private readonly int _viewportX = 240;
    /// <summary>
    /// Expected viewport width by the minigame.
    /// </summary>
    public override int ViewportX { get => _viewportX; }
    private readonly int _viewportY = 240;
    /// <summary>
    /// Expected viewport height by the minigame.
    /// </summary>
    public override int ViewportY { get => _viewportY; }

    // Layouts rolled from during "Setup" by difficulty; null or empty keeps the authored Stage
    [Export] private PlatformerLayoutCatalog? _layoutCatalog;

    /// <summary>
    /// Forces this layout instead of rolling one from the catalog; must be set before <see cref="Setup"/>
    /// <para>Meant for tests and playtesting: leave it empty in the saved scene, or the real game always plays it</para>
    /// </summary>
    [Export] public PackedScene? LayoutOverride { get; set; }

    // Child node reference found during "Setup"
    private Goal? _goal;

    /// <summary>
    /// Sets up the minigame instance before it enters the scene tree: picks the layout and wires its Goal.
    /// </summary>
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;

        // Pick the layout: the override, else a roll from the catalog, else the authored Stage
        if (LayoutOverride != null) { GD.Print($"Platformer: LayoutOverride forces '{LayoutOverride.ResourcePath}'"); }
        PackedScene? chosenLayout = LayoutOverride ?? _layoutCatalog?.Pick(difficulty);
        if (chosenLayout != null) { UseLayout(chosenLayout); }

        // Find the layout and check it's usable
        PlatformerLayout? layout = GetNodeOrNull<PlatformerLayout>("Stage");
        if (layout == null) { throw new NullReferenceException("Platformer: missing required child 'Stage' (a PlatformerLayout)"); }
        layout.Validate();

        // Find Goal and bind its Action(s)
        _goal = GetNode<Goal>("Stage/Goal");
        if (_goal == null) { throw new NullReferenceException("Platformer: missing required child 'Stage/Goal'"); }
        _goal.Reached += Victory;
    }

    /// <summary>
    /// Invoked by the Goal when reached, notifies relevant systems upstream.
    /// </summary>
    public override void Victory()
    {
        // Insert any additional victory animations and logic here
        // Guarded: Victory() must be safely callable even if Setup() never ran
        if (_goal != null) { _goal.Reached -= Victory; }

        GD.Print("Platformer Completed!!!");
        OnVictory?.Invoke();
    }

    // Replaces the authored Stage with an instance of "layout", unless the authored one already is that layout
    // Runs during "Setup", before tree entry, so the replaced Stage can be freed outright
    // Same as ThrowPaperBall.UseLayout on purpose (second use); extract a shared helper on a third
    private void UseLayout(PackedScene layout)
    {
        Node? authored = GetNodeOrNull("Stage");
        if (authored != null && authored.SceneFilePath == layout.ResourcePath) { return; }

        Node instance = layout.Instantiate();
        if (instance is not PlatformerLayout stage)
        {
            instance.Free();
            throw new InvalidOperationException($"Platformer: layout '{layout.ResourcePath}' has no PlatformerLayout root");
        }

        if (authored != null)
        {
            RemoveChild(authored);
            authored.Free();
        }
        stage.Name = "Stage";
        AddChild(stage);
    }
}
