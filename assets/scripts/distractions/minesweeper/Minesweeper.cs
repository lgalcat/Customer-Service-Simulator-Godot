using Godot;
using System;

/// <summary>
/// Main script of the Minesweeper minigame
/// <para>Revealing every safe cell wins. Revealing a mine shows it briefly, then wipes the board (every cell
/// hidden, every flag cleared) while the mines stay where they are, so each failure leaves the player knowing
/// more. The board comes from a <see cref="MinesweeperLevel"/> picked by difficulty, and the window is sized to it
/// in <see cref="Setup"/></para>
/// </summary>
public partial class Minesweeper : Distraction
{
    private enum MinesweeperState { err, playing, detonated, cleared }
    private MinesweeperState _state = MinesweeperState.err;

    // Board played when no level is chosen (no override, null or empty catalog)
    [Export] private int _fallbackWidth = 9;
    [Export] private int _fallbackHeight = 9;
    [Export] private int _fallbackMines = 10;

    // Level presets rolled from during "Setup" by difficulty; null or empty keeps the fallback board
    [Export] private MinesweeperLevelCatalog? _levelCatalog;

    /// <summary>
    /// Forces this level instead of rolling one from the catalog; must be set before <see cref="Setup"/>. It has no
    /// band to scale across, so it plays its <see cref="MinesweeperLevel.MinMines"/>
    /// <para>Meant for tests and playtesting: leave it empty in the saved scene, or the real game always plays it</para>
    /// </summary>
    [Export] public MinesweeperLevel? LevelOverride { get; set; }

    // Window layout around the board, in pixels: a margin on every side plus the counter strip above the board
    [Export] private int _margin = 8;
    [Export] private int _hudHeight = 16;

    // Seconds the detonated mine stays on screen before the board is wiped
    [Export] private float _detonationDelay = 0.75f;

    // Tile size assumed for the window before Setup (matches minesweeper_tiles_wip); Setup reads the real one
    private const int DefaultTileSize = 16;

    // Window size computed in Setup; null until then
    private Vector2I? _windowSize;

    /// <summary>
    /// Expected viewport width by the minigame: the chosen board plus margins (the fallback board before Setup).
    /// </summary>
    public override int ViewportX { get => WindowSize.X; }

    /// <summary>
    /// Expected viewport height by the minigame: the chosen board, the counter strip and margins (the fallback
    /// board before Setup).
    /// </summary>
    public override int ViewportY { get => WindowSize.Y; }

    // Child node references found during "Setup"
    private MinesweeperBoard _board = null!;
    private MinesweeperField _field = null!;
    // Not core to the game loop: a missing or wrong-typed counter or background doesn't throw
    private Label? _minesCounter;
    private Control? _background;

    // Created in _Ready (needs the tree)
    private Timer? _detonationTimer;

    private Vector2I WindowSize => _windowSize ?? WindowSizeFor(new Vector2I(_fallbackWidth, _fallbackHeight), new Vector2I(DefaultTileSize, DefaultTileSize));

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        _detonationTimer = new Timer
        {
            OneShot = true,
            WaitTime = _detonationDelay
        };
        AddChild(_detonationTimer);
        _detonationTimer.Timeout += EndDetonation;
    }

    /// <summary>
    /// Sets up the minigame instance before it enters the scene tree.
    /// </summary>
    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;

        Node2D stage = GetNode<Node2D>("Stage");
        if (stage == null) { throw new NullReferenceException("Minesweeper: missing required child 'Stage'"); }
        _board = GetNode<MinesweeperBoard>("Stage/Board");
        if (_board == null) { throw new NullReferenceException("Minesweeper: missing required child 'Stage/Board'"); }

        // Pick the board: the override, else a roll from the catalog, else the fallback values
        int width = _fallbackWidth;
        int height = _fallbackHeight;
        int mines = _fallbackMines;
        if (LevelOverride != null)
        {
            GD.Print($"Minesweeper: LevelOverride forces '{LevelOverride.ResourcePath}'");
            LevelOverride.Validate();
            (width, height, mines) = (LevelOverride.Width, LevelOverride.Height, LevelOverride.MinMines);
        }
        else if (_levelCatalog?.PickEntry(difficulty) is MinesweeperLevelEntry entry && entry.Level != null)
        {
            entry.Level.Validate();
            (width, height, mines) = (entry.Level.Width, entry.Level.Height, entry.MinesFor(difficulty));
        }

        // A chosen level is validated above; CreateRandom guards the fallback values
        _field = MinesweeperField.CreateRandom(width, height, mines);
        _board.Bind(_field);
        _board.FieldChanged += RefreshMinesCounter;
        _board.MineHit += OnMineHit;
        _board.Cleared += Victory;

        // GetNodeOrNull<T> gives null without an engine error for a missing node, and for a wrong-typed one too
        _minesCounter = GetNodeOrNull<Label>("Stage/MinesCounter");
        _background = GetNodeOrNull<Control>("Stage/Background");
        LayOut(stage);
        RefreshMinesCounter();

        _state = MinesweeperState.playing;
    }

    /// <summary>
    /// Invoked when the win condition has been met, notifies relevant systems upstream.
    /// </summary>
    public override void Victory()
    {
        _state = MinesweeperState.cleared;
        // Guarded: Victory must be safe without a prior Setup
        if (_board != null)
        {
            _board.InputEnabled = false;
            _board.FieldChanged -= RefreshMinesCounter;
            _board.MineHit -= OnMineHit;
            _board.Cleared -= Victory;
        }
        _detonationTimer?.Stop();

        GD.Print("Minesweeper Completed!!!");
        OnVictory?.Invoke();
    }

    // Sizes the window to the board and places everything around the Stage, which sits at the window's centre:
    // the counter strip on top, the board centred below it, the background covering the whole window
    private void LayOut(Node2D stage)
    {
        Vector2I boardPixels = _board.BoardPixelSize;
        Vector2I window = WindowSizeFor(new Vector2I(_field.Width, _field.Height), _board.TileSet.TileSize);
        _windowSize = window;

        Vector2 half = (Vector2)window / 2;
        stage.Position = half;
        _board.Position = new Vector2(-boardPixels.X / 2f, -half.Y + _margin + _hudHeight);

        if (_minesCounter != null)
        {
            _minesCounter.Position = new Vector2(-boardPixels.X / 2f, -half.Y + _margin);
            _minesCounter.Size = new Vector2(boardPixels.X, _hudHeight);
        }
        if (_background != null)
        {
            _background.Position = -half;
            _background.Size = window;
        }
    }

    private Vector2I WindowSizeFor(Vector2I cells, Vector2I tileSize)
    {
        return new Vector2I(cells.X * tileSize.X + 2 * _margin, cells.Y * tileSize.Y + _hudHeight + 2 * _margin);
    }

    // Shows the mines still unflagged (negative when more flags than mines are placed) on the counter (if existing)
    private void RefreshMinesCounter()
    {
        if (_minesCounter != null) { _minesCounter.Text = _field.RemainingMines.ToString(); }
    }

    // A mine went off: freeze the board on it, then wipe it once the delay runs out
    private void OnMineHit(Vector2I cell)
    {
        _state = MinesweeperState.detonated;
        _board.InputEnabled = false;
        // Without a tree there's no timer to wait on, so the wipe happens at once
        if (_detonationTimer != null) { _detonationTimer.Start(); }
        else { EndDetonation(); }
    }

    // Wipes the board back to all hidden with the same mines, and hands control back to the player
    private void EndDetonation()
    {
        if (_state != MinesweeperState.detonated) { return; }
        _field.Reset();
        _board.Refresh();
        RefreshMinesCounter();
        _board.InputEnabled = true;
        _state = MinesweeperState.playing;
    }
}
