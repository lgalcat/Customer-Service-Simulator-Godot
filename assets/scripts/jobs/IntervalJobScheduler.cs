using Godot;
using System;

/// <summary>
/// A sample "when" heuristic for <see cref="JobManager"/>: requests a job after a randomised wait
/// following each completion (and once at startup, if enabled).
/// <para>Entirely swappable for a different trigger without touching the factory or manager - that
/// decoupling is the whole reason <see cref="JobManager.RequestJob"/> exists on its own.</para>
/// </summary>
public partial class IntervalJobScheduler : Node
{
    // Seconds to wait, after a job completes (or after startup, for the very first one), before
    // requesting the next. Waiting for completion rather than re-arming on a timer of its own lets
    // every job, and its announcement, take as long as it needs. Placeholder values: balance is
    // a later pass.
    [Export] private float _minWait = 5f;
    [Export] private float _maxWait = 12f;
    [Export] private NodePath _managerPath = "..";
    // Off by default so a scene can host this component without it immediately starting to assign
    [Export] private bool _autoStart = false;

    private JobManager _manager = null!;
    private Timer _timer = null!;

    public override void _Ready()
    {
        _manager = GetNode<JobManager>(_managerPath);
        if (_manager == null) { throw new NullReferenceException($"IntervalJobScheduler: missing required node '{_managerPath}'"); }

        _timer = new Timer { OneShot = true };
        AddChild(_timer);
        _timer.Timeout += OnTimeout;

        _manager.JobCompleted += OnJobCompleted;

        if (_autoStart) { ScheduleNext(); }
    }

    // Requests the next job - deliberately does NOT reschedule itself. The only non-throwing denial is
    // "not idle", which cannot happen here (the timer only runs while idle) and would reschedule
    // through JobCompleted anyway. A mis-authored job throws instead: that is logged by the engine
    // and the chain stops, which is the loud failure an authoring error should be.
    private void OnTimeout()
    {
        _manager.RequestJob();
    }

    // Default mechanism for queuing new requests after startup
    private void OnJobCompleted()
    {
        ScheduleNext();
    }

    // Re-rolls the wait and (re)starts the timer
    private void ScheduleNext()
    {
        _timer.WaitTime = GD.RandRange(_minWait, _maxWait);
        _timer.Start();
    }
}
