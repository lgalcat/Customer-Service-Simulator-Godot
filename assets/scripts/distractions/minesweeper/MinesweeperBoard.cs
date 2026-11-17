using Godot;
using System;

/// <summary>
/// The Minesweeper board on screen: draws a <see cref="MinesweeperField"/> as one tile per cell and turns mouse
/// clicks into moves
/// <para>One node handles every cell, with no per-tile picking (boards exceed the 64-object picking-sort limit).
/// A left click acts on release over the cell it was pressed on, so dragging off cancels it: a revealed number
/// is chorded, anything else revealed. A right press toggles a flag</para>
/// </summary>
public partial class MinesweeperBoard : TileMapLayer
{
    // Atlas row layout of minesweeper_tiles_wip.png: hidden, flag, mine (unused for now), exploded mine, then
    // revealed safe cells at x = 4 + adjacent mines (4 is blank, 5..12 are 1..8)
    private static readonly Vector2I HiddenTile = new(0, 0);
    private static readonly Vector2I FlagTile = new(1, 0);
    private static readonly Vector2I ExplodedTile = new(3, 0);
    private const int RevealedTileBaseX = 4;

    // Atlas source in the TileSet holding the tiles above
    [Export] private int _atlasSourceId = 0;

    private MinesweeperField? _field;
    // Cell a left press landed on, until its release; null when no press of ours is pending
    private Vector2I? _pressedCell;

    /// <summary>
    /// Invoked after any move that changed the field (reveal, chord or flag)
    /// </summary>
    public Action? FieldChanged;

    /// <summary>
    /// Invoked when a move reveals a mine, with the detonated cell, after <see cref="FieldChanged"/>
    /// </summary>
    public Action<Vector2I>? MineHit;

    /// <summary>
    /// Invoked when a move reveals the last safe cell, after <see cref="FieldChanged"/>
    /// </summary>
    public Action? Cleared;

    private bool _inputEnabled = true;
    /// <summary>
    /// When false, clicks are ignored and a pending left press is dropped
    /// </summary>
    public bool InputEnabled
    {
        get => _inputEnabled;
        set
        {
            _inputEnabled = value;
            if (!value) { _pressedCell = null; }
        }
    }

    /// <summary>
    /// The field this board shows, once <see cref="Bind"/> has run
    /// </summary>
    public MinesweeperField? Field => _field;

    /// <summary>
    /// Pixel size of the bound board (cells times the TileSet's tile size); zero before <see cref="Bind"/>
    /// </summary>
    public Vector2I BoardPixelSize => _field == null || TileSet == null
        ? Vector2I.Zero
        : new Vector2I(_field.Width * TileSet.TileSize.X, _field.Height * TileSet.TileSize.Y);

    /// <summary>
    /// Shows <paramref name="field"/>, replacing any previous one; safe before tree entry
    /// </summary>
    /// <exception cref="InvalidOperationException">The board has no TileSet.</exception>
    public void Bind(MinesweeperField field)
    {
        if (TileSet == null) { throw new InvalidOperationException("MinesweeperBoard: no TileSet assigned"); }
        _field = field;
        _pressedCell = null;
        Clear();
        Refresh();
    }

    /// <summary>
    /// Redraws every cell from the field's current state (at most a few hundred tiles, at click frequency)
    /// </summary>
    public void Refresh()
    {
        if (_field == null) { return; }
        for (int y = 0; y < _field.Height; y++)
        {
            for (int x = 0; x < _field.Width; x++)
            {
                var cell = new Vector2I(x, y);
                SetCell(cell, _atlasSourceId, TileFor(cell));
            }
        }
    }

    // Called for input events not already consumed elsewhere in the pipeline
    public override void _UnhandledInput(InputEvent @event)
    {
        if (_field == null || !_inputEnabled || @event is not InputEventMouseButton button) { return; }

        Vector2I cell = LocalToMap(((InputEventMouseButton)MakeInputLocal(button)).Position);
        bool inside = _field.IsInside(cell);

        if (button.ButtonIndex == MouseButton.Left && button.Pressed)
        {
            _pressedCell = inside ? cell : null;
            if (inside) { GetViewport().SetInputAsHandled(); }
        }
        else if (button.ButtonIndex == MouseButton.Left)
        {
            // Release: only a press that landed on the board is ours to finish
            Vector2I? pressed = _pressedCell;
            _pressedCell = null;
            if (pressed == null) { return; }
            GetViewport().SetInputAsHandled();
            if (inside && cell == pressed.Value) { Act(cell); }
        }
        else if (button.ButtonIndex == MouseButton.Right && button.Pressed && inside)
        {
            GetViewport().SetInputAsHandled();
            if (_field.ToggleFlag(cell))
            {
                Refresh();
                FieldChanged?.Invoke();
            }
        }
    }

    // A left click: chord a revealed number, reveal anything else
    private void Act(Vector2I cell)
    {
        if (_field == null) { return; }
        MinesweeperField.RevealResult result = _field.GetState(cell) == MinesweeperField.CellState.Revealed
            ? _field.Chord(cell)
            : _field.Reveal(cell);
        if (result == MinesweeperField.RevealResult.Ignored) { return; }

        Refresh();
        FieldChanged?.Invoke();
        if (result == MinesweeperField.RevealResult.Mine && _field.DetonatedCell is Vector2I detonated) { MineHit?.Invoke(detonated); }
        else if (_field.IsCleared) { Cleared?.Invoke(); }
    }

    private Vector2I TileFor(Vector2I cell)
    {
        if (_field == null) { return HiddenTile; }
        return _field.GetState(cell) switch
        {
            MinesweeperField.CellState.Flagged => FlagTile,
            MinesweeperField.CellState.Revealed when _field.DetonatedCell == cell => ExplodedTile,
            MinesweeperField.CellState.Revealed => new Vector2I(RevealedTileBaseX + _field.AdjacentMines(cell), 0),
            _ => HiddenTile
        };
    }
}
