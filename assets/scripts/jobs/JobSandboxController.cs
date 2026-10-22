using System;
using Godot;

/// <summary>
/// Dev-only harness for <see cref="JobManager"/>, hosted in "job_sandbox.tscn" next to the real
/// office. Lets a developer force-start one specific job and stand in for a Distraction's input lock,
/// without the main level's schedulers. The Jobs counterpart of <see cref="DistractionSandboxController"/>.
/// </summary>
public partial class JobSandboxController : Node
{
    // Path to the "JobManager" this harness drives.
    [Export] private NodePath _managerPath = "JobManager";

    // Path to the "OfficeManager" whose screens jobs are played on.
    [Export] private NodePath _officePath = "OfficeManager";

    // Job the force-start key (F1) requests. Must be one the default factory maps.
    [Export] private JobType _forceType = JobType.FillDocument;

    // Difficulty sent with a forced request.
    [Export] private int _forceDifficulty = 1;

    // Force-start the chosen job once when the sandbox loads, without waiting for the F1 key.
    [Export] private bool _startOnReady = false;

    private JobManager _manager = null!;
    private OfficeManager _office = null!;

    public override void _Ready()
    {
        _manager = GetNodeOrNull<JobManager>(_managerPath) ?? throw new NullReferenceException($"JobSandboxController: missing required node '{_managerPath}'");
        _office = GetNodeOrNull<OfficeManager>(_officePath) ?? throw new NullReferenceException($"JobSandboxController: missing required node '{_officePath}'");

        // What the level will do once it composes Jobs
        _manager.ScreenResolver = _office.TryGetScreen;
        _manager.JobStarted += () => GD.Print("JobSandbox: JobStarted");
        _manager.JobCompleted += () => GD.Print("JobSandbox: JobCompleted");

        if (_startOnReady) { ForceStart(); }
    }

    // Raw keycodes rather than InputMap actions, as in DistractionSandboxController. F1/F2 are not
    // consumed by a focused text field, so they work while typing into a job's form
    public override void _UnhandledKeyInput(InputEvent @event)
    {
        if (@event is not InputEventKey { Pressed: true, Echo: false } key) { return; }

        switch (key.Keycode)
        {
            case Key.F1:
                ForceStart();
                break;
            case Key.F2:
                ToggleInputLock();
                break;
            default:
                return;
        }
        GetViewport().SetInputAsHandled();
    }

    private void ForceStart()
    {
        bool started = _manager.RequestJob(_forceType, _forceDifficulty);
        GD.Print(started
            ? $"JobSandbox: requested {_forceType} (difficulty {_forceDifficulty})"
            : $"JobSandbox: request denied, manager is {_manager.Phase}");
    }

    // Stands in for MainLevel locking Office while a Distraction runs
    private void ToggleInputLock()
    {
        _office.InputLocked = !_office.InputLocked;
        GD.Print($"JobSandbox: office input {(_office.InputLocked ? "locked" : "unlocked")}");
    }
}
