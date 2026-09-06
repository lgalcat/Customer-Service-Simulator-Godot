using Godot;
using System;

/// <summary>
/// A sample "when" heuristic for <see cref="DistractionManager"/>: requests a minigame after a
/// randomised wait following each completion (and once at startup, if enabled).
/// <para>Entirely swappable for a different trigger without touching the factory or manager - that
/// decoupling is the whole reason <see cref="DistractionManager.RequestDistraction"/> exists on its
/// own.</para>
/// </summary>
public partial class IntervalDistractionScheduler : Node
{
    // Seconds to wait, after a distraction completes (or after startup, for the very first one),
    // before requesting the next. Waiting for completion rather than re-arming on a timer of its own
    // gives every minigame room to run as long as it needs.
    [Export] private float _minWait = 8f;
    [Export] private float _maxWait = 20f;
    [Export] private NodePath _managerPath = "..";
    // Off by default so a scene can host this component without it immediately starting to spawn
    [Export] private bool _autoStart = false;

    private DistractionManager _manager = null!;
    private Timer _timer = null!;

    public override void _Ready()
    {
        _manager = GetNode<DistractionManager>(_managerPath);
        if (_manager == null) { throw new NullReferenceException($"IntervalDistractionScheduler: missing required node '{_managerPath}'"); }

        _timer = new Timer { OneShot = true };
        AddChild(_timer);
        _timer.Timeout += OnTimeout;

        _manager.DistractionCompleted += OnDistractionCompleted;

        if (_autoStart) { ScheduleNext(); }
    }

    // Requests the next distraction - deliberately does NOT reschedule itself.
    // Note: if RequestDistraction() is ever denied for a reason other than "one's already active"
    // (no such reason exists today), this scheduler stalls (no restart system in place).
    private void OnTimeout()
    {
        _manager.RequestDistraction();
    }

    // Default mechanism for queuing new requests after startup
    // Check OnTimeout() description for stalling risks
    private void OnDistractionCompleted()
    {
        ScheduleNext();
    }

    // Re-rolls the wait and (re)starts the timer, currently only called via OnDistractionCompleted()
    private void ScheduleNext()
    {
        _timer.WaitTime = GD.RandRange(_minWait, _maxWait);
        _timer.Start();
    }
}