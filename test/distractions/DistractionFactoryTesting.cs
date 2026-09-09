using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System;
using System.Collections.Generic;
using System.Linq;

// Dedicated component suite for DistractionFactory, independent of the Distraction hierarchy -
// exercises the constructor injection seam directly, no DistractionManager/live tree involved
[TestSuite]
[RequireGodotRuntime]
public class DistractionFactoryTesting
{
    [TestCase]
    public void CreateAssignsDifficultyBeforeReturning()
    {
        var factory = new DistractionFactory(SingleTypeMap());

        DistractionFactory.DistractionInstance instance = factory.Create(DistractionType.ThrowPaperBall, 3);
        AutoFree(instance.Host);

        AssertThat(instance.Distraction.Difficulty).IsEqual(3);
    }

    // Host -> SubViewport -> Distraction is BuildHost's exact nesting - walked here rather than
    // reaching into private state
    [TestCase]
    public void CreateWrapsDistractionInHostSizedToViewportSize()
    {
        var factory = new DistractionFactory(SingleTypeMap());

        DistractionFactory.DistractionInstance instance = factory.Create(DistractionType.ThrowPaperBall, 1);
        AutoFree(instance.Host);
        SubViewport viewport = instance.Host.GetChildren().OfType<SubViewport>().First();

        AssertThat(instance.Host.Size).IsEqual(instance.Distraction.ViewportSize);
        AssertThat(viewport.Size).IsEqual(instance.Distraction.ViewportSize);
    }

    // Pins the SubViewport config BuildHost sets - CanvasItemDefaultTextureFilter especially,
    // since a SubViewport defaults to Linear regardless of project settings and would blur every
    // pixel-art minigame hosted this way
    [TestCase]
    public void CreateConfiguresViewportForPixelArtAndPerInstancePicking()
    {
        var factory = new DistractionFactory(SingleTypeMap());

        DistractionFactory.DistractionInstance instance = factory.Create(DistractionType.ThrowPaperBall, 1);
        AutoFree(instance.Host);
        SubViewport viewport = instance.Host.GetChildren().OfType<SubViewport>().First();
        var container = (SubViewportContainer)instance.Host;

        AssertThat(viewport.CanvasItemDefaultTextureFilter).IsEqual(Viewport.DefaultCanvasItemTextureFilter.Nearest);
        AssertThat(viewport.Disable3D).IsTrue();
        AssertThat(viewport.TransparentBg).IsTrue();
        AssertThat(viewport.PhysicsObjectPicking).IsTrue();
        AssertThat(viewport.HandleInputLocally).IsFalse();
        AssertThat(viewport.RenderTargetUpdateMode).IsEqual(SubViewport.UpdateMode.Always);
        // Stretch=true is what makes the container report zero minimum size - the root cause
        // behind DistractionManager's KeepSize fix (see RequestDistractionKeepsHostSizeAfterCentering)
        AssertThat(container.Stretch).IsTrue();
    }

    // A factory with no configured minigames can never create anything - rejected at construction
    // rather than deferred to a later RandomType()/Create() failure
    [TestCase]
    public void ConstructorThrowsForAnEmptyMap()
    {
        AssertThrown(() => new DistractionFactory(new Dictionary<DistractionType, PackedScene>())).IsInstanceOf<ArgumentException>();
    }

    [TestCase]
    public void CreateThrowsForUnmappedType()
    {
        var factory = new DistractionFactory(SingleTypeMap());

        AssertThrown(() => factory.Create(DistractionType.Platformer, 1)).IsInstanceOf<KeyNotFoundException>();
    }

    [TestCase]
    public void RandomTypeOnlyReturnsConfiguredTypes()
    {
        var factory = new DistractionFactory(SingleTypeMap());

        for (int i = 0; i < 10; i++)
        {
            AssertThat(factory.RandomType()).IsEqual(DistractionType.ThrowPaperBall);
        }
    }

    // A single-entry map (see the test above) can't distinguish a real random draw from just
    // returning array[0] - a multi-entry map does, both by rejecting anything outside the
    // configured set and by actually reaching every entry over enough draws
    [TestCase]
    public void RandomTypeDrawsFromEveryConfiguredTypeOverManyCalls()
    {
        var scenes = new Dictionary<DistractionType, PackedScene>
        {
            { DistractionType.ThrowPaperBall, StubDistraction.Pack() },
            { DistractionType.FlySwatter, StubDistraction.Pack() },
        };
        var factory = new DistractionFactory(scenes);
        var drawn = new HashSet<DistractionType>();

        for (int i = 0; i < 30; i++)
        {
            DistractionType type = factory.RandomType();
            AssertThat(scenes.ContainsKey(type)).IsTrue();
            drawn.Add(type);
        }

        AssertThat(drawn.Count).IsEqual(2);
    }

    [TestCase]
    public void CreateProducesIndependentInstancesPerCall()
    {
        var factory = new DistractionFactory(SingleTypeMap());

        DistractionFactory.DistractionInstance first = factory.Create(DistractionType.ThrowPaperBall, 1);
        DistractionFactory.DistractionInstance second = factory.Create(DistractionType.ThrowPaperBall, 1);
        AutoFree(first.Host);
        AutoFree(second.Host);

        AssertThat(first.Host).IsNotSame(second.Host);
        AssertThat(first.Distraction).IsNotSame(second.Distraction);
    }

    // Sanity check that the real res://-backed pool stays complete - the fuller real-hosting proof
    // (actually requesting/completing each one through a live DistractionManager) lives in
    // DistractionSystemIntegrationTesting
    [TestCase]
    public void CreateDefaultMapsAllFourDistractionTypes()
    {
        DistractionFactory factory = DistractionFactory.CreateDefault();

        foreach (DistractionType type in Enum.GetValues<DistractionType>())
        {
            DistractionFactory.DistractionInstance instance = factory.Create(type, 1);
            AutoFree(instance.Host);
        }
    }

    private static Dictionary<DistractionType, PackedScene> SingleTypeMap()
    {
        return new Dictionary<DistractionType, PackedScene>
        {
            { DistractionType.ThrowPaperBall, StubDistraction.Pack() },
        };
    }
}