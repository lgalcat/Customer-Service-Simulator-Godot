using Godot;
using System;

/// <summary>
/// Where a <see cref="JobManager"/> currently is in the life of the one job it may host.
/// </summary>
public enum JobPhase
{
    /// <summary>Nothing is running; a request is accepted.</summary>
    Idle,
    /// <summary>A job's announcement is playing; its job is created but not yet started.</summary>
    Announcing,
    /// <summary>A job has been started and not yet completed.</summary>
    Active,
}

/// <summary>
/// Hosts the single active <see cref="Job"/> - and, first, the <see cref="JobAnnouncement"/> its scene
/// declares - and re-exposes its start and completion upstream.
/// <para>Not a layer and with nothing to draw itself: a job's parts are placed into the <see
/// cref="OfficeScreen"/> the job targets, found through <see cref="ScreenResolver"/>. That keeps
/// this class dependent on screens but not on <see cref="OfficeManager"/>; whoever composes the
/// level supplies the resolver.</para>
/// <para>Deliberately not the thing that decides *when* to request a job - see <see
/// cref="RequestJob"/> and <see cref="IntervalJobScheduler"/> for the seam that keeps that
/// heuristic decoupled from instancing - nor what the player may do meanwhile (navigation,
/// input locks): that policy belongs to the level.</para>
/// </summary>
public partial class JobManager : Node
{
    // Default difficulty for a request that doesn't specify one
    [Export] private int _baseDifficulty = 1;

    /// <summary>
    /// Fired once when an announcement has been attached and is about to play. Not fired for a job
    /// whose scene declares none.
    /// </summary>
    public Action? JobAnnouncing;

    /// <summary>
    /// Fired once whenever a job has been attached, added and become the active one. Never for a
    /// denied or failed request.
    /// </summary>
    public Action? JobStarted;

    /// <summary>
    /// Fired once whenever the active job is torn down after completion - the single event upstream
    /// systems (and the scheduler) should subscribe to.
    /// </summary>
    public Action? JobCompleted;

    /// <summary>
    /// Optionally supply a pre-built <see cref="JobFactory"/> before this manager enters the tree - a
    /// filtered/weighted pool or a test double. Left <see langword="null"/>, <see cref="_Ready"/>
    /// builds the default <c>res://</c>-backed factory. Read once in <see cref="_Ready"/>.
    /// </summary>
    public JobFactory? InitialFactory { get; set; }

    /// <summary>
    /// How a screen is found by name. Must be set before the first <see cref="RequestJob"/>
    /// (typically by the level, to <see cref="OfficeManager.TryGetScreen"/>).
    /// </summary>
    public Func<StringName, OfficeScreen?>? ScreenResolver { get; set; }

    /// <summary>
    /// The phase the manager is in.
    /// </summary>
    public JobPhase Phase { get; private set; } = JobPhase.Idle;

    /// <summary>
    /// The job currently being played, or <see langword="null"/> outside <see cref="JobPhase.Active"/>.
    /// </summary>
    public Job? ActiveJob { get; private set; }

    private JobFactory _factory = null!;
    // Created and set up, but held back (not attached, not in the tree) while its announcement plays
    private Job? _pendingJob;
    private JobAnnouncement? _announcement;

    public override void _Ready()
    {
        // [21/10/2026] TODO: designer-facing weighted/filtered pools via a JobPool resource, slotting
        // into this chain as InitialFactory ?? _pool?.BuildFactory() ?? CreateDefault()
        _factory = InitialFactory ?? JobFactory.CreateDefault();
    }

    public override void _ExitTree()
    {
        // The pending job is the one thing this manager owns that is not one of its children
        if (_pendingJob != null && IsInstanceValid(_pendingJob)) { _pendingJob.Free(); }
        _pendingJob = null;
    }

    /// <summary>
    /// Requests a job be created and started - the one "what" entry point, deliberately ignorant of
    /// the "when/why" behind the call. Returns <see langword="false"/> without effect unless the
    /// manager is <see cref="JobPhase.Idle"/>, since only one job may ever be in flight.
    /// </summary>
    /// <exception cref="InvalidOperationException">No <see cref="ScreenResolver"/> is set, or a
    /// target screen cannot be resolved.</exception>
    /// <remarks>A mis-authored job (unknown screen, anchor or fixture) is an authoring error: whatever
    /// was partly set up is taken back out, the manager is left <see cref="JobPhase.Idle"/>, and the
    /// exception propagates; no event is fired.</remarks>
    public bool RequestJob(JobType? type = null, int? difficulty = null)
    {
        if (Phase != JobPhase.Idle) { return false; }
        if (ScreenResolver is null) { throw new InvalidOperationException("JobManager: ScreenResolver must be set before requesting a job"); }

        JobFactory.JobInstance instance = _factory.Create(type ?? _factory.RandomType(), difficulty ?? _baseDifficulty);

        if (instance.Announcement is null) { StartJob(instance.Job); }
        else { BeginAnnouncement(instance.Job, instance.Announcement); }
        return true;
    }

    private OfficeScreen ResolveScreen(StringName name, string requester)
    {
        return ScreenResolver!(name) ?? throw new InvalidOperationException($"JobManager: {requester} targets screen '{name}', which does not exist");
    }

    private void BeginAnnouncement(Job job, JobAnnouncement announcement)
    {
        try
        {
            announcement.Attach(ResolveScreen(announcement.TargetScreen, announcement.GetType().Name));
            AddChild(announcement);
        }
        catch
        {
            announcement.Detach();
            announcement.QueueFree();
            job.QueueFree();
            throw;
        }

        _pendingJob = job;
        _announcement = announcement;
        Phase = JobPhase.Announcing;

        // Assigned before Play(): an announcement may finish synchronously
        announcement.OnFinished = OnAnnouncementFinished;
        JobAnnouncing?.Invoke();
        announcement.Play();
    }

    private void OnAnnouncementFinished()
    {
        if (_announcement is null || _pendingJob is null) { return; }

        JobAnnouncement announcement = _announcement;
        Job job = _pendingJob;
        _announcement = null;
        _pendingJob = null;

        announcement.OnFinished = null;
        announcement.Detach();
        announcement.QueueFree();
        StartJob(job);
    }

    private void StartJob(Job job)
    {
        // Attached while still outside the tree, so its parts enter the tree for the first time at
        // their anchors; only then does the job itself come in
        try
        {
            job.Attach(ResolveScreen(job.TargetScreen, job.GetType().Name));
            AddChild(job);
        }
        catch
        {
            job.Detach();
            job.QueueFree();
            Phase = JobPhase.Idle;
            throw;
        }

        job.OnCompleted = () => Complete(job);
        ActiveJob = job;
        Phase = JobPhase.Active;
        JobStarted?.Invoke();

        // A job that finished before it was started (during attach or _Ready) would have had nobody to tell
        if (job.IsCompleted) { Complete(job); }
    }

    // Consumes the job's OnCompleted, takes its content back out of the screen, and propagates
    // completion upstream
    private void Complete(Job job)
    {
        if (ActiveJob != job) { return; }

        job.OnCompleted = null;
        job.Detach();
        job.QueueFree();
        ActiveJob = null;
        Phase = JobPhase.Idle;
        JobCompleted?.Invoke();
    }
}
