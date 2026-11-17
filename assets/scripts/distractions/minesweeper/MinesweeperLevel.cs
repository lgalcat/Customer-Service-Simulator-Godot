using Godot;
using System;

/// <summary>
/// A Minesweeper level preset: one board size and the mine counts it scales between, picked during
/// <see cref="Minesweeper.Setup"/>
/// <para>The mine count follows the difficulty inside the band of the catalog entry rating this preset:
/// <see cref="MinMines"/> at the bottom of the band, <see cref="MaxMines"/> at the top, linear in between
/// (see <see cref="MinesFor"/>). The board size sets mostly how long a round lasts, the mine density how hard it is</para>
/// </summary>
[GlobalClass]
public partial class MinesweeperLevel : Resource
{
    /// <summary>
    /// Columns on the board
    /// </summary>
    [ExportGroup("Board")]
    [Export] public int Width { get; set; } = 9;

    /// <summary>
    /// Rows on the board
    /// </summary>
    [Export] public int Height { get; set; } = 9;

    /// <summary>
    /// Mines at the lowest difficulty of the entry's band
    /// </summary>
    [ExportGroup("Mines")]
    [Export] public int MinMines { get; set; } = 10;

    /// <summary>
    /// Mines at the highest difficulty of the entry's band
    /// </summary>
    [Export] public int MaxMines { get; set; } = 10;

    /// <summary>
    /// Checks the preset is usable: a board of at least 2x2, at least one mine, an ascending mine range, and at
    /// least one safe cell left at the most mines
    /// </summary>
    /// <exception cref="InvalidOperationException">The preset is mis-authored.</exception>
    public void Validate()
    {
        string level = string.IsNullOrEmpty(ResourcePath) ? ResourceName : ResourcePath;
        if (Width < 2) { throw new InvalidOperationException($"MinesweeperLevel '{level}': Width {Width} must be at least 2"); }
        if (Height < 2) { throw new InvalidOperationException($"MinesweeperLevel '{level}': Height {Height} must be at least 2"); }
        if (MinMines < 1) { throw new InvalidOperationException($"MinesweeperLevel '{level}': MinMines {MinMines} must be at least 1"); }
        if (MinMines > MaxMines) { throw new InvalidOperationException($"MinesweeperLevel '{level}': MinMines {MinMines} is above MaxMines {MaxMines}"); }
        int cells = Width * Height;
        if (MaxMines > cells - 1) { throw new InvalidOperationException($"MinesweeperLevel '{level}': MaxMines {MaxMines} leaves no safe cell on a {Width}x{Height} board (at most {cells - 1})"); }
    }

    /// <summary>
    /// Mines for <paramref name="difficulty"/> within the band <paramref name="bandMin"/>..<paramref name="bandMax"/>:
    /// <see cref="MinMines"/> at or below the bottom, <see cref="MaxMines"/> at or above the top, linear and rounded
    /// in between. A single-difficulty band gives <see cref="MinMines"/>. Call <see cref="Validate"/> first
    /// </summary>
    public int MinesFor(int difficulty, int bandMin, int bandMax)
    {
        if (bandMax <= bandMin) { return MinMines; }
        double t = Math.Clamp((difficulty - bandMin) / (double)(bandMax - bandMin), 0.0, 1.0);
        return MinMines + (int)Math.Round((MaxMines - MinMines) * t, MidpointRounding.AwayFromZero);
    }
}
