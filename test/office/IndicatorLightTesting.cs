using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// IndicatorLight's states (rest, on, flash, blink) over a hand-built light in a live tree. Levels are read
// against the light's own _restLevel/_peakLevel and time is stepped (StepTweens), never waited: the
// tuning and the durations can change freely
[TestSuite]
[RequireGodotRuntime]
public class IndicatorLightTesting
{
    private ISceneRunner _runner = null!;
    private IndicatorLight _light = null!;

    [BeforeTest]
    public void Setup()
    {
        Node2D root = new();
        // Alpha matches neither level: the light must ignore it
        _light = new IndicatorLight { LightColor = new Color(0.4f, 1.3f, 0.7f, 0.9f) };
        root.AddChild(_light);
        _runner = ISceneRunner.Load(root, true, true);
    }

    [AfterTest]
    public void Teardown()
    {
        _runner.Dispose();
    }

    private float Level => _light.SelfModulate.A;

    private float PeakLevel => _light.Get("_peakLevel").AsSingle();

    // Whether the light is lit at each step while 'seconds' pass
    private List<bool> LitWhile(double seconds)
    {
        var samples = new List<bool>();
        StepTweens(_light, seconds, () => samples.Add(IsLit(_light)));
        return samples;
    }

    private static int RisingEdges(List<bool> samples)
    {
        int edges = 0;
        for (int i = 1; i < samples.Count; i++) { if (samples[i] && !samples[i - 1]) { edges++; } }
        return edges;
    }

    [TestCase]
    public void RestsAtItsRestLevelInItsOwnHueOnceReady()
    {
        AssertThat(AtRest(_light)).IsTrue();
        AssertThat(new[] { _light.SelfModulate.R, _light.SelfModulate.G, _light.SelfModulate.B })
            .ContainsExactly(_light.LightColor.R, _light.LightColor.G, _light.LightColor.B);
    }

    [TestCase]
    public void TurnOnHoldsThePeak()
    {
        _light.TurnOn();
        StepTweens(_light, 2);

        AssertThat(Level).IsEqual(PeakLevel);
    }

    [TestCase]
    public void RestGoesBackToTheRestLevel()
    {
        _light.TurnOn();

        _light.Rest();

        AssertThat(AtRest(_light)).IsTrue();
    }

    // How the flash fades is look; when it starts and that it is over within its time is what a code
    // played on the lights relies on
    [TestCase]
    public void FlashLightsAtOnceAndIsBackAtRestWithinItsTime()
    {
        _light.Flash(0.5f);
        AssertThat(Level).IsEqual(PeakLevel);

        StepTweens(_light, 0.52);
        AssertThat(AtRest(_light)).IsTrue();

        AssertThat(LitWhile(1)).NotContains(true);
    }

    // A second flash while one is fading starts over: the first one's fade must not dim it
    [TestCase]
    public void FlashRestartsAFlashInProgress()
    {
        _light.Flash(1f);
        StepTweens(_light, 0.8);

        _light.Flash(1f);
        StepTweens(_light, 0.15);

        AssertThat(Level).IsEqual(PeakLevel);
    }

    [TestCase]
    public void BlinkKeepsAlternatingUntilStopped()
    {
        _light.StartBlink(0.2f);

        AssertThat(RisingEdges(LitWhile(1))).IsGreaterEqual(2);
    }

    // Rest / TurnOn / Flash each end a blink for good: once settled, the light holds still
    [TestCase("Rest", false)]
    [TestCase("TurnOn", true)]
    [TestCase("Flash", false)]
    public void ABlinkStopsFor(string call, bool lit)
    {
        _light.StartBlink(0.2f);
        StepTweens(_light, 0.25);

        switch (call)
        {
            case "Rest": _light.Rest(); break;
            case "TurnOn": _light.TurnOn(); break;
            case "Flash": _light.Flash(0.1f); StepTweens(_light, 0.12); break;
        }

        AssertThat(LitWhile(1)).OverrideFailureMessage($"still blinking after {call}").NotContains(!lit);
    }
}
