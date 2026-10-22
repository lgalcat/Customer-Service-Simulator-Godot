using Godot;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Locates, instances and configures <see cref="Job"/>s (and the <see cref="JobAnnouncement"/> a job
/// declares) on request.
/// <para>Owns "how a job comes to exist" - <see cref="JobManager"/> owns "when/why". Unlike <see
/// cref="DistractionFactory"/> there is no display host to build: a job's visible parts are placed
/// into an office screen by the manager.</para>
/// </summary>
public class JobFactory
{
    /// <summary>
    /// A job paired with the announcement its scene declared, if any. Neither is in the scene tree
    /// yet; the job has already been through <see cref="Job.Setup"/>.
    /// </summary>
    public readonly record struct JobInstance(Job Job, JobAnnouncement? Announcement);

    private readonly IReadOnlyDictionary<JobType, PackedScene> _scenes;
    private readonly JobType[] _availableTypes;

    /// <summary>
    /// Builds a factory over an explicit type-to-scene map - the injectable seam tests use.
    /// </summary>
    /// <exception cref="System.ArgumentException"><paramref name="scenes"/> is empty - a factory with no
    /// configured jobs can never create anything, so this is rejected at construction rather than
    /// deferred to a later, harder-to-trace <see cref="RandomType"/>/<see cref="Create"/> failure.</exception>
    public JobFactory(IReadOnlyDictionary<JobType, PackedScene> scenes)
    {
        if (scenes.Count == 0)
        {
            throw new System.ArgumentException("JobFactory: scenes must not be empty", nameof(scenes));
        }

        _scenes = scenes;
        _availableTypes = scenes.Keys.ToArray();
    }

    /// <summary>
    /// Builds the real, <c>res://</c>-backed factory over every <see cref="Job"/>.
    /// </summary>
    // Each job adds its entry here, like DistractionFactory.CreateDefault()
    public static JobFactory CreateDefault()
    {
        var scenes = new Dictionary<JobType, PackedScene>
        {
            { JobType.FillDocument, GD.Load<PackedScene>("res://assets/scenes/jobs/fill_document/fill_document_job.tscn") },
        };
        return new JobFactory(scenes);
    }

    /// <summary>
    /// Instances and configures the requested job, plus its announcement when its scene declares
    /// one. Neither is attached to a screen or added to the tree.
    /// </summary>
    public JobInstance Create(JobType type, int difficulty)
    {
        if (!_scenes.TryGetValue(type, out PackedScene? scene))
        {
            throw new KeyNotFoundException($"JobFactory: no scene mapped for '{type}'");
        }

        Job job = scene.Instantiate<Job>();
        // Before any tree entry, per the Job.Setup() contract
        job.Setup(difficulty);

        JobAnnouncement? announcement = job.AnnouncementScene?.Instantiate<JobAnnouncement>();
        return new JobInstance(job, announcement);
    }

    /// <summary>
    /// Picks uniformly among every job this factory can currently instance.
    /// </summary>
    public JobType RandomType()
    {
        return _availableTypes[GD.RandRange(0, _availableTypes.Length - 1)];
    }
}
