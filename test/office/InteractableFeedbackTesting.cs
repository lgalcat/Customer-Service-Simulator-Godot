using System;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// Idle pulse (self_modulate tween) and hover outline (shader instance uniform) of
// InteractableFeedback, driven through its parent SpriteInteractable's own signals. Styles use tiny
// periods and assertions are on thresholds, never exact in-between values; immediate effects (a
// snap to white, a stopped Timer) are asserted synchronously.
[TestSuite]
[RequireGodotRuntime]
public class InteractableFeedbackTesting
{
    private const string OutlineShaderPath = "res://assets/shaders/office/interactable_outline.gdshader";

    private ISceneRunner? _runner;
    private SpriteInteractable _interactable = null!;
    private InteractableFeedback _feedback = null!;

    // Frame-stepping against real tweens: sensitive to heavy background CPU load
    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(InteractableFeedbackTesting));

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    // peak 2.0 on R is far from rest (1.0), so thresholds have wide margins at any frame rate
    private static IdlePulseStyle FastPulse(float rest = 0.3f) =>
        new() { PulseColor = new Color(2f, 2f, 2f), HalfPeriod = 0.1f, RestPeriod = rest };

    private static HoverOutlineStyle FastOutline(float fadeSeconds = 0.1f) =>
        new() { FadeSeconds = fadeSeconds, OutlineShader = GD.Load<Shader>(OutlineShaderPath) };

    // Builds Root > SpriteInteractable > InteractableFeedback with the given private [Export]s set
    // before tree entry, then loads it (which runs _Ready())
    private void Start(IdlePulseStyle? idle, HoverOutlineStyle? hover, float idleDelay = 0f)
    {
        var (root, interactable) = BuildInteractable();
        InteractableFeedback feedback = new();
        if (idle != null) { feedback.Set("_idle", idle); }
        if (hover != null) { feedback.Set("_hover", hover); }
        feedback.Set("_idleDelay", idleDelay);
        interactable.AddChild(feedback);
        _interactable = interactable;
        _feedback = feedback;
        _runner = ISceneRunner.Load(root, true, true);
    }

    private bool IsWhite() => _feedback.SelfModulate.R <= 1.01f;

    private bool HasPulsed() => _feedback.SelfModulate.R > 1.5f;

    // The uniform reads as an empty Variant until first written (resting sprite, never hovered)
    private float Outline()
    {
        Variant value = _feedback.GetInstanceShaderParameter("hover");
        return value.VariantType == Variant.Type.Nil ? 0f : value.AsSingle();
    }

    private Timer IdleTimer() => _feedback.GetChildren().OfType<Timer>().Single();

    private Task<bool> WaitUntil(Func<bool> condition, int timeoutMs = 2000) =>
        OfficeTestKit.WaitUntil(_runner!, condition, timeoutMs);

    // True when 'condition' held at every sample for the whole window
    private async Task<bool> HoldsFor(Func<bool> condition, int windowMs)
    {
        Stopwatch clock = Stopwatch.StartNew();
        while (clock.ElapsedMilliseconds < windowMs)
        {
            if (!condition()) { return false; }
            await _runner!.AwaitMillis(10);
        }
        return true;
    }

    // ---- _Ready(): which channels get built ----

    [TestCase]
    public void ReadyThrowsWhenNotUnderASpriteInteractable()
    {
        InteractableFeedback orphan = AutoFree(new InteractableFeedback())!;
        Node2D wrongParent = AutoFree(new Node2D())!;
        wrongParent.AddChild(orphan);

        // Manual _Ready() call - no SceneTree to auto-trigger it
        AssertThrown(() => orphan._Ready()).IsInstanceOf<InvalidOperationException>();
    }

    [TestCase]
    public void HoverOnlyBuildsTheOutlineMaterialAndNoIdleTimer()
    {
        HoverOutlineStyle hover = FastOutline();
        hover.OutlineColor = new Color(0.2f, 0.4f, 0.6f);

        Start(null, hover);

        AssertThat(_feedback.Material).IsInstanceOf<ShaderMaterial>();
        ShaderMaterial material = (ShaderMaterial)_feedback.Material!;
        AssertThat(material.Shader).IsSame(hover.OutlineShader);
        AssertThat(material.GetShaderParameter("outline_color").AsColor()).IsEqual(hover.OutlineColor);
        AssertThat(_feedback.GetChildren().OfType<Timer>().Count()).IsEqual(0);
    }

    [TestCase]
    public void IdleOnlyBuildsAOneShotTimerAndNoMaterial()
    {
        Start(FastPulse(), null);

        AssertThat(_feedback.Material).IsNull();
        AssertThat(IdleTimer().OneShot).IsTrue();
    }

    // No style on either channel: parent requirement still applies, but nothing is built or animated
    [TestCase]
    public async Task WithNoStylesNothingIsBuiltAndHoverChangesNothing()
    {
        Start(null, null);

        PointerEnter(_interactable);
        bool stayedWhite = await HoldsFor(IsWhite, 200);

        AssertThat(_feedback.Material).IsNull();
        AssertThat(_feedback.GetChildren().OfType<Timer>().Count()).IsEqual(0);
        AssertThat(stayedWhite).IsTrue();
        AssertThat(Outline()).IsEqual(0f);
    }

    // ---- Idle pulse ----

    [TestCase]
    public async Task PulseStartsImmediatelyWithoutADelay()
    {
        Start(FastPulse(), null);

        AssertThat(await WaitUntil(HasPulsed)).IsTrue();
    }

    // Counts distinct peaks (a peak, then back near white, then peak again) up to 'target' or the timeout
    private async Task<int> CountPeaks(int target, int timeoutMs = 3000)
    {
        int peaks = 0;
        bool atRest = true;
        Stopwatch clock = Stopwatch.StartNew();

        while (clock.ElapsedMilliseconds < timeoutMs && peaks < target)
        {
            float r = _feedback.SelfModulate.R;
            if (atRest && r > 1.7f) { peaks++; atRest = false; }
            else if (!atRest && r < 1.1f) { atRest = true; }
            await _runner!.AwaitMillis(10);
        }
        return peaks;
    }

    // Loops (back to rest, then peaks again), not a one-shot
    [TestCase]
    public async Task PulseKeepsLoopingBetweenPeakAndRest()
    {
        Start(FastPulse(rest: 0.2f), null);

        AssertThat(await CountPeaks(3)).IsGreaterEqual(3);
    }

    // RestPeriod 0 skips the rest hold entirely (no TweenInterval): the pulse must still loop
    [TestCase]
    public async Task PulseWithNoRestPeriodStillLoops()
    {
        Start(FastPulse(rest: 0f), null);

        AssertThat(await CountPeaks(3)).IsGreaterEqual(3);
    }

    // The delay is a held-back Timer: nothing pulses and the Timer is armed until it fires
    [TestCase]
    public async Task IdleDelayHoldsTheFirstPulseBack()
    {
        Start(FastPulse(), null, idleDelay: 0.4f);

        AssertThat(IdleTimer().IsStopped()).IsFalse();
        AssertThat(IsWhite()).IsTrue();
        AssertThat(await WaitUntil(HasPulsed)).IsTrue();
    }

    // After a hover the delay applies again before the pulse resumes (ScheduleIdle, not StartPulse)
    [TestCase]
    public async Task HoverEndRestartsTheIdleDelay()
    {
        Start(FastPulse(), null, idleDelay: 0.4f);
        PointerEnter(_interactable);
        AssertThat(IdleTimer().IsStopped()).IsTrue();

        PointerExit(_interactable);

        AssertThat(IdleTimer().IsStopped()).IsFalse();
        AssertThat(IsWhite()).IsTrue();
        AssertThat(await WaitUntil(HasPulsed)).IsTrue();
    }

    [TestCase]
    public async Task HoverStopsThePulseAndSnapsToWhiteAtOnce()
    {
        Start(FastPulse(), null);
        await WaitUntil(HasPulsed);

        PointerEnter(_interactable);

        AssertThat(IsWhite()).IsTrue();
        AssertThat(await HoldsFor(IsWhite, 400)).IsTrue();
    }

    [TestCase]
    public async Task HoverEndResumesThePulse()
    {
        Start(FastPulse(), null);
        PointerEnter(_interactable);
        PointerExit(_interactable);

        AssertThat(await WaitUntil(HasPulsed)).IsTrue();
    }

    [TestCase]
    public async Task DeactivatedStopsThePulseAndSnapsToWhiteAtOnce()
    {
        Start(FastPulse(), null);
        await WaitUntil(HasPulsed);

        _interactable.SetInteractable(false);

        AssertThat(IsWhite()).IsTrue();
        AssertThat(await HoldsFor(IsWhite, 400)).IsTrue();
    }

    [TestCase]
    public async Task ActivatedResumesThePulse()
    {
        Start(FastPulse(), null);
        _interactable.SetInteractable(false);
        _interactable.SetInteractable(true);

        AssertThat(await WaitUntil(HasPulsed)).IsTrue();
    }

    // _hovered gates ScheduleIdle: reactivating a still-hovered element must not start a pulse
    [TestCase]
    public async Task ActivatedWhileHoveredDoesNotPulse()
    {
        Start(FastPulse(), null);
        PointerEnter(_interactable);

        _interactable.SetInteractable(true);

        AssertThat(await HoldsFor(IsWhite, 400)).IsTrue();
    }

    // A delayed start already armed must not fire once the element went inactive
    [TestCase]
    public async Task DeactivationCancelsAPendingDelayedPulse()
    {
        Start(FastPulse(), null, idleDelay: 0.25f);

        _interactable.SetInteractable(false);

        AssertThat(IdleTimer().IsStopped()).IsTrue();
        AssertThat(await HoldsFor(IsWhite, 600)).IsTrue();
    }

    // ---- Hover outline ----

    [TestCase]
    public async Task HoverFadesTheOutlineInAndHoverEndFadesItOut()
    {
        Start(null, FastOutline());

        PointerEnter(_interactable);
        AssertThat(await WaitUntil(() => Outline() >= 0.99f)).IsTrue();

        PointerExit(_interactable);
        AssertThat(await WaitUntil(() => Outline() <= 0.01f)).IsTrue();
    }

    // Hover without an idle style never touches self_modulate
    [TestCase]
    public async Task OutlineOnlyElementNeverPulsesOrTints()
    {
        Start(null, FastOutline());

        PointerEnter(_interactable);

        AssertThat(await HoldsFor(IsWhite, 300)).IsTrue();
    }

    // Reversing mid-fade continues from where the outline is, instead of snapping to an end
    [TestCase]
    public async Task ReversingMidFadeContinuesFromTheCurrentValue()
    {
        Start(null, FastOutline(fadeSeconds: 0.6f));
        PointerEnter(_interactable);
        await WaitUntil(() => Outline() >= 0.4f && Outline() <= 0.7f);
        float atReversal = Outline();
        AssertThat(atReversal).IsBetween(0.4f, 0.7f);

        PointerExit(_interactable);
        await _runner!.SimulateFrames(1);

        // One frame into a 0.6 s fade: a small step down from where it was, not a jump to 0 or 1
        AssertThat(Outline()).IsBetween(atReversal - 0.3f, atReversal + 0.01f);
        AssertThat(await WaitUntil(() => Outline() <= 0.01f)).IsTrue();
    }

    // ---- Both channels together ----

    // HoverEnded (emitted first by SetInteractable) briefly reschedules the idle, then Deactivated
    // must cancel it: no pulse and no armed Timer survive, and the outline still fades out
    [TestCase]
    public async Task DeactivatingWhileHoveredLeavesNoPulseAndFadesTheOutlineOut()
    {
        Start(FastPulse(), FastOutline(), idleDelay: 0.3f);
        PointerEnter(_interactable);
        await WaitUntil(() => Outline() >= 0.99f);

        _interactable.SetInteractable(false);

        AssertThat(IsWhite()).IsTrue();
        AssertThat(IdleTimer().IsStopped()).IsTrue();
        AssertThat(await WaitUntil(() => Outline() <= 0.01f)).IsTrue();
        AssertThat(await HoldsFor(IsWhite, 600)).IsTrue();
    }

    [TestCase]
    public async Task HoverShowsTheOutlineWhileThePulseIsPaused()
    {
        Start(FastPulse(), FastOutline());
        await WaitUntil(HasPulsed);

        PointerEnter(_interactable);

        AssertThat(await WaitUntil(() => Outline() >= 0.99f)).IsTrue();
        AssertThat(IsWhite()).IsTrue();
    }

    // ---- _ExitTree() ----

    // Out of the tree the Tween no longer advances, so SelfModulate stays frozen mid-pulse; a handler
    // that was still subscribed would StopPulse() on hover and snap it to white. (Connection counts
    // can't show this: C# subscribers to a custom signal share one engine-level connection.)
    [TestCase]
    public async Task LeavingTheTreeStopsReactingToTheParentsSignals()
    {
        Start(FastPulse(), null);
        await WaitUntil(HasPulsed);
        _interactable.RemoveChild(_feedback);
        AutoFree(_feedback);
        float frozen = _feedback.SelfModulate.R;

        PointerEnter(_interactable);

        AssertThat(_feedback.SelfModulate.R).IsEqual(frozen);
        AssertThat(frozen).IsGreater(1.5f);
    }
}
