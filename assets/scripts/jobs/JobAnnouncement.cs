using Godot;
using System;

/// <summary>
/// Base class for the short "preamble" event that announces a <see cref="Job"/> before it starts -
/// a character walking up to the desk, a phone call - played on its own target screen (typically
/// the main one) while the job itself works on another.
/// <para>Same plumbing as <see cref="Job"/> (a <see cref="ScreenContentScope"/> to place parts at
/// anchors and bind to resident fixtures such as the phone or the door). Whether the player must
/// be moved to the screen, or take part (answer the phone), is policy owned by the level, not by
/// this class or by <see cref="JobManager"/>.</para>
/// </summary>
public abstract partial class JobAnnouncement : Node
{
    private ScreenContentScope? _scope;
    private bool _finished;

    /// <summary>
    /// Name of the <see cref="OfficeScreen"/> this announcement plays on.
    /// </summary>
    public abstract StringName TargetScreen { get; }

    /// <summary>
    /// The screen this announcement is attached to, or <see langword="null"/> before <see
    /// cref="Attach"/> / after <see cref="Detach"/>.
    /// </summary>
    public OfficeScreen? Screen => _scope?.Screen;

    /// <summary>
    /// Invoked once, by <see cref="Finish"/>. The host assigns it <b>before</b> calling <see
    /// cref="Play"/>, so an announcement that finishes synchronously still works.
    /// </summary>
    public Action? OnFinished;

    /// <summary>
    /// Hook run by <see cref="Attach"/> once <see cref="Screen"/> is available: place parts and bind
    /// to fixtures. Runs before the announcement enters the scene tree.
    /// </summary>
    protected abstract void OnAttached();

    /// <summary>
    /// Starts the event. Implementations must eventually call <see cref="Finish"/>.
    /// </summary>
    public abstract void Play();

    /// <summary>
    /// Connects the announcement to its target screen and runs <see cref="OnAttached"/>; anything
    /// placed is taken back out if that throws.
    /// </summary>
    public void Attach(OfficeScreen screen)
    {
        if (_scope != null) { throw new InvalidOperationException($"{GetType().Name}: already attached"); }

        _scope = new ScreenContentScope(screen);
        try { OnAttached(); }
        catch
        {
            Detach();
            throw;
        }
    }

    /// <summary>
    /// Undoes everything done through the announcement's scope. Safe to call repeatedly.
    /// </summary>
    public void Detach()
    {
        _scope?.Dispose();
        _scope = null;
    }

    /// <summary>
    /// Places a visible part at one of the target screen's anchors; removed again by <see
    /// cref="Detach"/>. Only valid from <see cref="OnAttached"/> onwards.
    /// </summary>
    protected void Place(Node2D part, StringName anchor)
    {
        RequireScope().Place(part, anchor);
    }

    /// <summary>
    /// Registers an undo action to run on <see cref="Detach"/>, in reverse order of registration.
    /// </summary>
    protected void AddCleanup(Action undo)
    {
        RequireScope().AddCleanup(undo);
    }

    private ScreenContentScope RequireScope()
    {
        return _scope ?? throw new InvalidOperationException($"{GetType().Name}: not attached to a screen yet (only valid from OnAttached onwards)");
    }

    /// <summary>
    /// Marks the announcement over and invokes <see cref="OnFinished"/>, once; later calls do nothing.
    /// </summary>
    public void Finish()
    {
        if (_finished) { return; }

        _finished = true;
        OnFinished?.Invoke();
    }

    // Safety net, as for Job. Subclasses overriding _ExitTree must call base._ExitTree().
    public override void _ExitTree()
    {
        Detach();
    }
}
