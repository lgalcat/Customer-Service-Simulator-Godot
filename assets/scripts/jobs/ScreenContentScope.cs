using Godot;
using System;
using System.Collections.Generic;

/// <summary>
/// Everything one <see cref="Job"/> or <see cref="JobAnnouncement"/> puts into an <see
/// cref="OfficeScreen"/>, tracked so it can all be taken back out: the content placed at anchors and
/// the undo actions (typically event unsubscriptions) registered along the way.
/// <para>A plain C# helper held by composition - a Godot script can only derive from one <see
/// cref="Node"/> type, so sharing this through a base class was never an option. Disposing is
/// idempotent.</para>
/// </summary>
public sealed class ScreenContentScope : IDisposable
{
    private readonly List<Node2D> _placed = new();
    private readonly List<Action> _cleanups = new();
    private bool _disposed;

    /// <summary>
    /// The screen this scope places content into.
    /// </summary>
    public OfficeScreen Screen { get; }

    public ScreenContentScope(OfficeScreen screen)
    {
        Screen = screen;
    }

    /// <summary>
    /// Places <paramref name="content"/> at the screen's anchor fixture <paramref name="anchor"/>
    /// (see <see cref="OfficeScreen.Place"/>) and remembers it for <see cref="Dispose"/>. Content
    /// should not be in the scene tree yet.
    /// </summary>
    public void Place(Node2D content, StringName anchor)
    {
        if (_disposed) { throw new ObjectDisposedException(nameof(ScreenContentScope)); }

        Screen.Place(content, anchor);
        _placed.Add(content);
    }

    /// <summary>
    /// Registers an action to run on <see cref="Dispose"/> - the place to undo anything this scope's
    /// owner did to a resident fixture, above all a <c>-=</c> for each <c>+=</c>: a C# subscription to
    /// a resident emitter is not removed when the (transient) subscriber is freed.
    /// </summary>
    public void AddCleanup(Action undo)
    {
        if (_disposed) { throw new ObjectDisposedException(nameof(ScreenContentScope)); }

        _cleanups.Add(undo);
    }

    /// <summary>
    /// Runs every cleanup (last registered first), then removes every placed part (last placed
    /// first). A failing cleanup is logged and does not stop the rest - teardown has to finish.
    /// </summary>
    public void Dispose()
    {
        if (_disposed) { return; }
        _disposed = true;

        for (int i = _cleanups.Count - 1; i >= 0; i--)
        {
            try { _cleanups[i](); }
            catch (Exception exception) { GD.PushError($"ScreenContentScope: cleanup failed: {exception}"); }
        }
        _cleanups.Clear();

        bool screenAlive = GodotObject.IsInstanceValid(Screen);
        for (int i = _placed.Count - 1; i >= 0; i--)
        {
            // A screen freed first (e.g. the whole tree going down) took its content with it
            if (screenAlive) { Screen.Remove(_placed[i]); }
        }
        _placed.Clear();
    }
}
