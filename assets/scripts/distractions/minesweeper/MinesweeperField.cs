using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// The rules of one Minesweeper board: where the mines are, what each cell shows, and how reveals, flags and
/// chords play out
/// <para>Plain C# with no node or scene, so the rules can be tested on hand-picked boards. The mine layout is
/// fixed for the field's lifetime: <see cref="Reset"/> only hides the board again</para>
/// </summary>
public class MinesweeperField
{
    /// <summary>
    /// What the player can see of a cell
    /// </summary>
    public enum CellState { Hidden, Flagged, Revealed }

    /// <summary>
    /// The outcome of <see cref="Reveal"/> or <see cref="Chord"/>
    /// </summary>
    public enum RevealResult
    {
        /// <summary>Nothing changed (the cell wasn't playable, or the field is finished)</summary>
        Ignored,
        /// <summary>One or more safe cells were revealed</summary>
        Safe,
        /// <summary>A mine was revealed; the field is detonated until <see cref="Reset"/></summary>
        Mine
    }

    // Per cell, indexed y * Width + x
    private readonly bool[] _mines;
    private readonly int[] _adjacent;
    private readonly CellState[] _states;
    // Safe cells revealed so far, so the win check is O(1)
    private int _revealedSafe;

    /// <summary>
    /// Columns on the board
    /// </summary>
    public int Width { get; }

    /// <summary>
    /// Rows on the board
    /// </summary>
    public int Height { get; }

    /// <summary>
    /// Mines on the board
    /// </summary>
    public int MineCount { get; }

    /// <summary>
    /// Flags currently placed, right or wrong
    /// </summary>
    public int FlagCount { get; private set; }

    /// <summary>
    /// Mines minus flags, as the player's counter shows it; negative when more flags than mines are placed
    /// </summary>
    public int RemainingMines => MineCount - FlagCount;

    /// <summary>
    /// True once every safe cell is revealed (flags aren't required)
    /// </summary>
    public bool IsCleared => _revealedSafe == Width * Height - MineCount;

    /// <summary>
    /// The mine that was revealed, until <see cref="Reset"/>; null while nothing has detonated
    /// </summary>
    public Vector2I? DetonatedCell { get; private set; }

    /// <summary>
    /// True when the field takes no more moves: a mine detonated (until <see cref="Reset"/>) or the board is cleared
    /// </summary>
    public bool IsFinished => DetonatedCell.HasValue || IsCleared;

    /// <summary>
    /// Builds a field with mines at exactly <paramref name="mineCells"/>, all cells hidden
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A size is below 1, or a mine is outside the board.</exception>
    /// <exception cref="ArgumentException">A mine is listed twice, or no safe cell is left.</exception>
    public MinesweeperField(int width, int height, IEnumerable<Vector2I> mineCells)
    {
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width), $"MinesweeperField: width {width} must be at least 1"); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height), $"MinesweeperField: height {height} must be at least 1"); }
        Width = width;
        Height = height;

        int cells = width * height;
        _mines = new bool[cells];
        _adjacent = new int[cells];
        _states = new CellState[cells];

        foreach (Vector2I mine in mineCells)
        {
            if (!IsInside(mine)) { throw new ArgumentOutOfRangeException(nameof(mineCells), $"MinesweeperField: mine {mine} is outside the {width}x{height} board"); }
            int index = IndexOf(mine);
            if (_mines[index]) { throw new ArgumentException($"MinesweeperField: mine {mine} is listed twice", nameof(mineCells)); }
            _mines[index] = true;
            MineCount++;
            foreach (int neighbour in Neighbours(index)) { _adjacent[neighbour]++; }
        }
        if (MineCount >= cells) { throw new ArgumentException($"MinesweeperField: {MineCount} mines leave no safe cell on a {width}x{height} board", nameof(mineCells)); }
    }

    /// <summary>
    /// Builds a field with <paramref name="mines"/> mines placed uniformly at random (a partial Fisher-Yates
    /// shuffle of the cells, using Godot's RNG)
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A size is below 1, or the mine count is negative or
    /// leaves no safe cell.</exception>
    public static MinesweeperField CreateRandom(int width, int height, int mines)
    {
        if (width < 1) { throw new ArgumentOutOfRangeException(nameof(width), $"MinesweeperField: width {width} must be at least 1"); }
        if (height < 1) { throw new ArgumentOutOfRangeException(nameof(height), $"MinesweeperField: height {height} must be at least 1"); }
        int cells = width * height;
        if (mines < 0 || mines >= cells) { throw new ArgumentOutOfRangeException(nameof(mines), $"MinesweeperField: {mines} mines must be between 0 and {cells - 1} on a {width}x{height} board"); }

        // Only the first "mines" slots need shuffling: each one takes a uniform pick from the cells not yet chosen
        int[] order = new int[cells];
        for (int i = 0; i < cells; i++) { order[i] = i; }
        var chosen = new Vector2I[mines];
        for (int i = 0; i < mines; i++)
        {
            int j = GD.RandRange(i, cells - 1);
            (order[i], order[j]) = (order[j], order[i]);
            chosen[i] = new Vector2I(order[i] % width, order[i] / width);
        }
        return new MinesweeperField(width, height, chosen);
    }

    /// <summary>
    /// True when <paramref name="cell"/> is on the board
    /// </summary>
    public bool IsInside(Vector2I cell) => cell.X >= 0 && cell.X < Width && cell.Y >= 0 && cell.Y < Height;

    /// <summary>
    /// What the player can see of <paramref name="cell"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell is outside the board.</exception>
    public CellState GetState(Vector2I cell) => _states[CheckedIndexOf(cell)];

    /// <summary>
    /// True when <paramref name="cell"/> holds a mine
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell is outside the board.</exception>
    public bool IsMine(Vector2I cell) => _mines[CheckedIndexOf(cell)];

    /// <summary>
    /// Mines in the up to eight cells around <paramref name="cell"/>
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell is outside the board.</exception>
    public int AdjacentMines(Vector2I cell) => _adjacent[CheckedIndexOf(cell)];

    /// <summary>
    /// Reveals a hidden, unflagged cell. A safe cell with no adjacent mines cascades: its neighbours are revealed
    /// too, breadth first, through every connected blank cell up to the numbered border. Flagged cells stop the
    /// cascade and stay flagged
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell is outside the board.</exception>
    public RevealResult Reveal(Vector2I cell)
    {
        int index = CheckedIndexOf(cell);
        if (IsFinished || _states[index] != CellState.Hidden) { return RevealResult.Ignored; }
        if (_mines[index])
        {
            Detonate(index);
            return RevealResult.Mine;
        }
        Cascade(index);
        return RevealResult.Safe;
    }

    /// <summary>
    /// Chords a revealed number: when exactly that many flags surround it, reveals every hidden, unflagged
    /// neighbour (cascading as <see cref="Reveal"/> does). A wrong flag means a mine among them, which detonates
    /// before anything else is revealed. Any other flag count does nothing
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell is outside the board.</exception>
    public RevealResult Chord(Vector2I cell)
    {
        int index = CheckedIndexOf(cell);
        if (IsFinished || _states[index] != CellState.Revealed || _adjacent[index] == 0) { return RevealResult.Ignored; }

        int flags = 0;
        foreach (int neighbour in Neighbours(index))
        {
            if (_states[neighbour] == CellState.Flagged) { flags++; }
        }
        if (flags != _adjacent[index]) { return RevealResult.Ignored; }

        foreach (int neighbour in Neighbours(index))
        {
            if (_states[neighbour] == CellState.Hidden && _mines[neighbour])
            {
                Detonate(neighbour);
                return RevealResult.Mine;
            }
        }

        RevealResult result = RevealResult.Ignored;
        foreach (int neighbour in Neighbours(index))
        {
            // A previous neighbour's cascade may already have revealed this one
            if (_states[neighbour] != CellState.Hidden) { continue; }
            Cascade(neighbour);
            result = RevealResult.Safe;
        }
        return result;
    }

    /// <summary>
    /// Flags a hidden cell or unflags a flagged one; true when something changed
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">The cell is outside the board.</exception>
    public bool ToggleFlag(Vector2I cell)
    {
        int index = CheckedIndexOf(cell);
        if (IsFinished) { return false; }
        switch (_states[index])
        {
            case CellState.Hidden:
                _states[index] = CellState.Flagged;
                FlagCount++;
                return true;
            case CellState.Flagged:
                _states[index] = CellState.Hidden;
                FlagCount--;
                return true;
            default:
                return false;
        }
    }

    /// <summary>
    /// Hides every cell and clears every flag and the detonation; the mines stay where they are
    /// </summary>
    public void Reset()
    {
        Array.Fill(_states, CellState.Hidden);
        FlagCount = 0;
        _revealedSafe = 0;
        DetonatedCell = null;
    }

    // Reveals a safe cell and, from every blank cell reached, its hidden neighbours. A cell is marked revealed
    // when it's queued, so the board state itself is the visited set; a blank cell's neighbours are never mines
    private void Cascade(int start)
    {
        var queue = new Queue<int>();
        RevealSafe(start, queue);
        while (queue.Count > 0)
        {
            int index = queue.Dequeue();
            foreach (int neighbour in Neighbours(index))
            {
                if (_states[neighbour] == CellState.Hidden) { RevealSafe(neighbour, queue); }
            }
        }
    }

    private void RevealSafe(int index, Queue<int> queue)
    {
        _states[index] = CellState.Revealed;
        _revealedSafe++;
        if (_adjacent[index] == 0) { queue.Enqueue(index); }
    }

    private void Detonate(int index)
    {
        _states[index] = CellState.Revealed;
        DetonatedCell = new Vector2I(index % Width, index / Width);
    }

    // The up to eight on-board cells around a cell
    private IEnumerable<int> Neighbours(int index)
    {
        int x = index % Width;
        int y = index / Width;
        for (int dy = -1; dy <= 1; dy++)
        {
            for (int dx = -1; dx <= 1; dx++)
            {
                if (dx == 0 && dy == 0) { continue; }
                int nx = x + dx;
                int ny = y + dy;
                if (nx >= 0 && nx < Width && ny >= 0 && ny < Height) { yield return ny * Width + nx; }
            }
        }
    }

    private int IndexOf(Vector2I cell) => cell.Y * Width + cell.X;

    private int CheckedIndexOf(Vector2I cell)
    {
        if (!IsInside(cell)) { throw new ArgumentOutOfRangeException(nameof(cell), $"MinesweeperField: cell {cell} is outside the {Width}x{Height} board"); }
        return IndexOf(cell);
    }
}
