using System;
using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// JobFactory through its injection constructor, over packed StubJobs keyed by the kit's stub types -
// no manager, screen or SceneTree involved (mirrors DistractionFactoryTesting)
[TestSuite]
[RequireGodotRuntime]
public class JobFactoryTesting
{
    [BeforeTest]
    public void Setup()
    {
        StubJob.Created.Clear();
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    // Neither half of a created instance is in a tree, so both need registering
    private static void AutoFreeInstance(JobFactory.JobInstance instance)
    {
        AutoFree(instance.Job);
        if (instance.Announcement != null) { AutoFree(instance.Announcement); }
    }

    // Setup() must run before any tree entry, per the Job contract
    [TestCase]
    public void CreateSetsTheJobUpWithTheDifficultyOutsideTheTree()
    {
        JobFactory factory = Factory(StubJob.Pack());

        JobFactory.JobInstance instance = factory.Create(StubTypeA, 3);
        AutoFree(instance.Job);

        AssertThat(instance.Job.Difficulty).IsEqual(3);
        AssertThat(((StubJob)instance.Job).WasInTreeAtSetup).IsEqual(false);
    }

    // Placement and tree entry are the manager's job, in that order
    [TestCase]
    public void CreateLeavesTheJobUnattachedAndOutsideTheTree()
    {
        JobFactory factory = Factory(StubJob.Pack());

        JobFactory.JobInstance instance = factory.Create(StubTypeA, 1);
        AutoFree(instance.Job);

        AssertThat(instance.Job.Screen).IsNull();
        AssertThat(instance.Job.GetParent()).IsNull();
    }

    [TestCase]
    public void CreateReturnsNoAnnouncementWhenTheJobDeclaresNone()
    {
        JobFactory factory = Factory(StubJob.Pack());

        JobFactory.JobInstance instance = factory.Create(StubTypeA, 1);
        AutoFree(instance.Job);

        AssertThat(instance.Announcement).IsNull();
    }

    [TestCase]
    public void CreateInstancesTheAnnouncementTheJobDeclares()
    {
        PackedScene announcement = StubJobAnnouncement.Pack(a => a.Target = "Announcer");
        JobFactory factory = Factory(StubJob.Pack(j => j.AnnouncementScene = announcement));

        JobFactory.JobInstance instance = factory.Create(StubTypeA, 1);
        AutoFreeInstance(instance);

        AssertThat(instance.Announcement).IsInstanceOf<StubJobAnnouncement>();
        AssertThat(instance.Announcement!.TargetScreen.ToString()).IsEqual("Announcer");
        AssertThat(instance.Announcement.GetParent()).IsNull();
        AssertThat(instance.Announcement.Screen).IsNull();
    }

    [TestCase]
    public void CreateProducesIndependentInstancesPerCall()
    {
        PackedScene announcement = StubJobAnnouncement.Pack();
        JobFactory factory = Factory(StubJob.Pack(j => j.AnnouncementScene = announcement));

        JobFactory.JobInstance first = factory.Create(StubTypeA, 1);
        JobFactory.JobInstance second = factory.Create(StubTypeA, 1);
        AutoFreeInstance(first);
        AutoFreeInstance(second);

        AssertThat(first.Job).IsNotSame(second.Job);
        AssertThat(first.Announcement).IsNotSame(second.Announcement);
    }

    // A factory with no configured jobs can never create anything - rejected at construction rather
    // than deferred to a later RandomType()/Create() failure
    [TestCase]
    public void ConstructorThrowsForAnEmptyMap()
    {
        AssertThrown(() => new JobFactory(new Dictionary<JobType, PackedScene>())).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public void CreateThrowsForAnUnmappedType()
    {
        JobFactory factory = Factory(StubJob.Pack());

        AssertThrown(() => factory.Create(StubTypeB, 1)).IsInstanceOf<KeyNotFoundException>();
        AssertThat(StubJob.Created).IsEmpty();
    }

    [TestCase]
    public void RandomTypeOnlyReturnsConfiguredTypes()
    {
        JobFactory factory = Factory(StubJob.Pack());

        for (int i = 0; i < 10; i++)
        {
            AssertThat(factory.RandomType()).IsEqual(StubTypeA);
        }
    }

    // A single-entry map can't tell a real random draw from always returning the first entry
    [TestCase]
    public void RandomTypeDrawsFromEveryConfiguredTypeOverManyCalls()
    {
        JobFactory factory = Factory((StubTypeA, StubJob.Pack()), (StubTypeB, StubJob.Pack()));
        var drawn = new HashSet<JobType>();

        for (int i = 0; i < 30; i++)
        {
            JobType type = factory.RandomType();
            AssertThat(type == StubTypeA || type == StubTypeB).IsTrue();
            drawn.Add(type);
        }

        AssertThat(drawn.Count).IsEqual(2);
    }

    // Self-transitioning: while JobType is empty the default pool is empty, which the constructor
    // rejects (the reason the level doesn't instance a JobManager yet). Once real members exist every
    // one of them must be creatable, so a forgotten CreateDefault() entry fails here without this test
    // being touched
    [TestCase]
    public void CreateDefaultMapsEveryJobType()
    {
        JobType[] types = Enum.GetValues<JobType>();
        if (types.Length == 0)
        {
            AssertThrown(() => JobFactory.CreateDefault()).IsInstanceOf<ArgumentException>();
            return;
        }

        JobFactory factory = JobFactory.CreateDefault();
        foreach (JobType type in types)
        {
            AutoFreeInstance(factory.Create(type, 1));
        }
    }
}
