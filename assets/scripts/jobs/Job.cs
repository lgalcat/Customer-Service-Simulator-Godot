using Godot;
using System;

/// <summary>
/// Base class from which all jobs (the office-work objectives the player completes in sequence) are
/// derived. The counterpart of <see cref="Distraction"/>: a distraction is a self-contained minigame
/// in its own window, a job is played inside an <see cref="OfficeScreen"/> using that screen's own
/// fixtures and interactables.
/// <para>A job is a logic holder, not a visual root. It lives under <see cref="JobManager"/>; what
/// the player sees is its visible parts (authored as children of the job's scene), each moved to an
/// anchor of the target screen in <see cref="OnAttached"/> through <see cref="Place"/>. Resident
/// devices on that screen are bound through <see cref="Screen"/>'s <see
/// cref="OfficeScreen.GetFixture{T}"/>.</para>
/// </summary>
public abstract partial class Job : Node
{
    private ScreenContentScope? _scope;
    private bool _completed;

    /// <summary>
    /// The local difficulty of the job, set via <see cref="Setup"/>.
    /// </summary>
    public int Difficulty { get; protected set; }

    /// <summary>
    /// Optional scene (a <see cref="JobAnnouncement"/>) played on its own target screen before this
    /// job starts. Left unset, the job starts immediately.
    /// </summary>
    [Export] public PackedScene? AnnouncementScene { get; set; }

    /// <summary>
    /// Name of the <see cref="OfficeScreen"/> this job is played on (its node name under the
    /// office's screen layer, the same identifier navigation uses).
    /// </summary>
    public abstract StringName TargetScreen { get; }

    /// <summary>
    /// The screen this job is attached to, or <see langword="null"/> before <see cref="Attach"/> /
    /// after <see cref="Detach"/>.
    /// </summary>
    public OfficeScreen? Screen => _scope?.Screen;

    /// <summary>
    /// Whether <see cref="Complete"/> has been called.
    /// </summary>
    public bool IsCompleted => _completed;

    /// <summary>
    /// Invoked once, by <see cref="Complete"/>.
    /// </summary>
    public Action? OnCompleted;

    /// <summary>
    /// Abstract factory method to invoke after instantiation but <b>before</b> insertion into the scene tree
    /// <para>Should be used to bind external dependencies and set up instance specific variance elements </para>
    /// </summary>
    public abstract void Setup(int difficulty);

    /// <summary>
    /// Hook run by <see cref="Attach"/> once <see cref="Screen"/> is available: place parts, bind to
    /// fixtures, subscribe to events (registering each undo through <see cref="AddCleanup"/>).
    /// <para>Runs <b>before</b> the job itself enters the scene tree (so parts reach the tree for the
    /// first time at their anchors); tree-dependent initialisation belongs in <c>_Ready</c>.</para>
    /// </summary>
    protected abstract void OnAttached();

    /// <summary>
    /// Connects the job to its target screen and runs <see cref="OnAttached"/>. Called once by the
    /// hosting <see cref="JobManager"/>. If <see cref="OnAttached"/> throws, everything placed so far is
    /// taken back out before the exception propagates.
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
    /// Undoes everything done through the job's scope: cleanups run, placed parts are removed. Safe
    /// to call repeatedly or without a prior <see cref="Attach"/>.
    /// </summary>
    public void Detach()
    {
        _scope?.Dispose();
        _scope = null;
    }

    /// <summary>
    /// Places a visible part at one of the target screen's anchors (see <see
    /// cref="OfficeScreen.Place"/>); the part is removed again by <see cref="Detach"/>. Only valid
    /// from <see cref="OnAttached"/> onwards.
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
    /// Marks the job done and invokes <see cref="OnCompleted"/>, once; later calls do nothing.
    /// Safe to call without a prior <see cref="Setup"/> or <see cref="Attach"/>. Handlers a job wired to
    /// resident emitters should ignore events once <see cref="IsCompleted"/>.
    /// </summary>
    public void Complete()
    {
        if (_completed) { return; }

        _completed = true;
        OnCompleted?.Invoke();
    }

    // Safety net: a job freed without going through the manager's teardown still gives back what it
    // placed. Subclasses overriding _ExitTree must call base._ExitTree().
    public override void _ExitTree()
    {
        Detach();
    }
}
