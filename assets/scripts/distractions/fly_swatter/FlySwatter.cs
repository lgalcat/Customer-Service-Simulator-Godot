using Godot;
using System;

/// <summary>
/// Main script of the Fly Swatter minigame
/// </summary>
public partial class FlySwatter : Distraction
{
    // Expected screenspace for the minigame, matches the placeholder background texture
    private readonly float _viewportX = 220;
    /// <summary>
    /// Expected viewport width by the minigame.
    /// </summary>
    public override float ViewportX { get => _viewportX; }
    private readonly float _viewportY = 220;
    /// <summary>
    /// Expected viewport height by the minigame.
    /// </summary>
    public override float ViewportY { get => _viewportY; }

    // Score to reach (units in number of flies swatted)
    // [19/08/2026] This should be difficulty dependent when implemented (see "difficulty profiles" notes)
    private int _winScore = 15;
    private int _currentScore = 0;

    // Child node references found during "Setup"
    private FlySpawner _flySpawner = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // "Setup" call just for early testing purposes, delete when a factory and testing scene are implemented
        Setup(1);
    }

    /// <summary>
    /// Sets up the minigame instance before it enters the scene tree.
    /// </summary>
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;

        // Implement location, instancing and distribution of difficulty dependent values here

        // Find fly spawner and bind its actions
        _flySpawner = GetNode<FlySpawner>("Stage/FlySpawner");
        if (_flySpawner == null) { throw new NullReferenceException(); }
        _flySpawner.FlyDied += UpdateScore;
        // [19/08/2026] Implementation and wiring of visual score tracker pending
    }

    /// <summary>
    /// Invoked when the win condition has been met, notifies relevant systems upstream.
    /// </summary>
    public override void Victory()
    {
        // Decouple score updates after meeting theshold
        if (_flySpawner != null)
        {
            _flySpawner.FlyDied -= UpdateScore;
            _flySpawner.StopSpawning();
        }
        // Insert any additional victory animations and logic here

        GD.Print("FlySwatter Completed!!!");
        OnVictory?.Invoke();
    }

    // Method to update the score and check against win condition
    private void UpdateScore()
    {
        _currentScore++;
        // [19/08/2026] Implementation of a score indicator pending, insert update calls here
        if (_currentScore >= _winScore) { Victory(); }
    }
}
