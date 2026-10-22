using Godot;
using System;

/// <summary>
/// Root of the composed game level: the one place that knows <see cref="OfficeManager"/>, <see
/// cref="DistractionManager"/> and <see cref="JobManager"/>, so none of them has to know the others exist.
/// <para>Its only job is policy glue: Office cannot be interacted with while a <see cref="Distraction"/>
/// is active, and jobs find the screens they are played on through Office. Draw order is not handled
/// here - it falls out of the systems' own <c>CanvasLayer</c> values (Office's screen layer 0, the
/// distraction manager's layer above it; jobs draw inside Office's screens). It deliberately knows
/// nothing about what decides *when* a distraction or a job appears (each cadence node lives inside its
/// manager's scene) or which minigames and jobs exist.</para>
/// <para>Distraction and job cadence are independent by design: a distraction may start while a job
/// is announcing or being played, and the job keeps running underneath, merely locked.</para>
/// </summary>
public partial class MainLevel : Node
{
    [Export] private NodePath _officePath = "OfficeManager";
    [Export] private NodePath _distractionsPath = "DistractionManager";
    [Export] private NodePath _jobsPath = "JobManager";

    public override void _Ready()
    {
        var office = GetNode<OfficeManager>(_officePath);
        if (office == null) { throw new NullReferenceException($"MainLevel: missing required child '{_officePath}'"); }

        var distractions = GetNode<DistractionManager>(_distractionsPath);
        if (distractions == null) { throw new NullReferenceException($"MainLevel: missing required child '{_distractionsPath}'"); }

        var jobs = GetNode<JobManager>(_jobsPath);
        if (jobs == null) { throw new NullReferenceException($"MainLevel: missing required child '{_jobsPath}'"); }

        // Plain Action wiring; all four nodes live exactly as long as the level, so nothing to unsubscribe
        distractions.DistractionStarted += () => office.InputLocked = true;
        distractions.DistractionCompleted += () => office.InputLocked = false;

        // _Ready runs bottom-up, so the job scheduler has already armed its timer; it cannot fire before
        // this line, as a timer only counts down in process frames, which start once the whole tree is ready
        jobs.ScreenResolver = office.TryGetScreen;
    }
}
