using Godot;
using System;

/// <summary>
/// Idle pulse and hover outline for the <see cref="SpriteInteractable"/> it is a direct child of. Attached directly
/// to the "Sprite" node it animates, so there is no separate node and no <c>NodePath</c> - <c>self_modulate</c>,
/// <c>material</c> and the shader's instance uniform are all set on <c>this</c>. Self-wires to the parent's signals
/// in code.
/// <para>
/// Each channel is independently optional: leave a style unset to skip that channel for this element (hover without
/// idle is the common "secondary object" case). The pulse runs on <c>self_modulate</c>, the outline on a shader
/// instance uniform - the two compose without competing for a property, so both may be set at once.
/// </para>
/// <para>
/// For an object that needs a genuinely different technique on one channel, give its "Sprite" a one-off script
/// instead of this one, subscribing to the same signals by hand - the path <see cref="SpriteInteractable"/>'s own
/// doc comment already names for "anything richer."
/// </para>
/// </summary>
public partial class InteractableFeedback : Sprite2D
{
    [Export] private IdlePulseStyle? _idle;
    [Export] private HoverOutlineStyle? _hover;

    /// <summary>Seconds after the element goes idle before the pulse starts; 0 starts it immediately.</summary>
    [Export] private float _idleDelay;

    private SpriteInteractable? _interactable;
    private Timer? _idleTimer;
    private Tween? _pulseTween;
    private Tween? _outlineTween;
    private float _outline;
    private bool _hovered;
    private bool _active = true;

    // Builds whichever of the two channels has a style assigned, then subscribes to only the
    // signals that channel actually needs - an object with neither style does nothing at all.
    public override void _Ready()
    {
        SpriteInteractable interactable = GetParent() as SpriteInteractable
            ?? throw new InvalidOperationException("InteractableFeedback: must be a direct child of a SpriteInteractable");
        _interactable = interactable;

        if (_hover != null)
        {
            // The shader's "hover" instance uniform defaults to 0, so a resting sprite renders
            // unchanged until FadeOutline/SetOutline touch it - no further setup needed here.
            var material = new ShaderMaterial { Shader = _hover.OutlineShader };
            material.SetShaderParameter("outline_color", _hover.OutlineColor);
            Material = material;
        }

        if (_idle != null)
        {
            // OneShot: fires once per ScheduleIdle() call, then re-armed next time rather than
            // looping itself - the loop is the pulse Tween, not this Timer.
            _idleTimer = new Timer { OneShot = true };
            _idleTimer.Timeout += StartPulse;
            AddChild(_idleTimer);
            interactable.Activated += OnActivated;
            interactable.Deactivated += OnDeactivated;
            ScheduleIdle();
        }

        // One shared subscription covers both channels: idle needs to pause while hovered, the
        // outline needs to animate on hover - OnHoverStarted/OnHoverEnded each guard per channel.
        if (_idle != null || _hover != null)
        {
            interactable.HoverStarted += OnHoverStarted;
            interactable.HoverEnded += OnHoverEnded;
        }
    }

    // Mirrors _Ready's subscriptions exactly; guarded in case _Ready threw before _interactable
    // was assigned (a scene-authoring mistake, not a normal path).
    public override void _ExitTree()
    {
        if (_interactable is null) { return; }
        _interactable.HoverStarted -= OnHoverStarted;
        _interactable.HoverEnded -= OnHoverEnded;
        _interactable.Activated -= OnActivated;
        _interactable.Deactivated -= OnDeactivated;
    }

    // Pointer entered: the pulse would otherwise keep animating self_modulate underneath the
    // outline, so freeze it at rest first, then ease the outline in.
    private void OnHoverStarted()
    {
        _hovered = true;
        if (_idle != null) { StopPulse(); }
        if (_hover != null) { FadeOutline(1f); }
    }

    // Pointer left: ease the outline back out, and let the idle pulse resume (immediately, or
    // after _idleDelay - ScheduleIdle decides which).
    private void OnHoverEnded()
    {
        _hovered = false;
        if (_hover != null) { FadeOutline(0f); }
        if (_idle != null) { ScheduleIdle(); }
    }

    // SetInteractable(true): the element is interactable again, so idle may resume.
    private void OnActivated()
    {
        _active = true;
        ScheduleIdle();
    }

    // SetInteractable(false): stop the pulse outright rather than let an infinite Tween keep
    // running on a hidden node. The outline needs no equivalent snap here - if this element was
    // hovered, SpriteInteractable already emitted HoverEnded first, which already faded it to 0.
    private void OnDeactivated()
    {
        _active = false;
        StopPulse();
    }

    // Starts the pulse now, or after _idleDelay, unless the element is currently inactive or
    // hovered - called from every place idle might need to (re)start, so those checks live here
    // once instead of at each call site.
    private void ScheduleIdle()
    {
        if (_idle == null || !_active || _hovered) { return; }
        if (_idleDelay > 0f) { _idleTimer?.Start(_idleDelay); }
        else { StartPulse(); }
    }

    // Re-checks the same guard as ScheduleIdle, since a delayed Timer firing later may find the
    // element since gone inactive or hovered. Loops forever between peak colour and white, eased
    // both ways so the pulse breathes rather than snaps, then holds at rest (TweenInterval - nothing
    // to ease, so no .SetTrans/.SetEase) before repeating.
    private void StartPulse()
    {
        if (_idle == null || !_active || _hovered) { return; }
        _pulseTween?.Kill();
        _pulseTween = CreateTween().SetLoops();
        _pulseTween.TweenProperty(this, "self_modulate", _idle.PulseColor, _idle.HalfPeriod)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        _pulseTween.TweenProperty(this, "self_modulate", Colors.White, _idle.HalfPeriod)
            .SetTrans(Tween.TransitionType.Sine).SetEase(Tween.EaseType.InOut);
        if (_idle.RestPeriod > 0f) { _pulseTween.TweenInterval(_idle.RestPeriod); }
    }

    // Cancels any pending delayed start and any running loop, and snaps back to the resting
    // colour immediately rather than easing - this is a hard stop, not a fade.
    private void StopPulse()
    {
        _idleTimer?.Stop();
        _pulseTween?.Kill();
        _pulseTween = null;
        SelfModulate = Colors.White;
    }

    // Eases the shader's "hover" instance uniform from its current value to target over
    // FadeSeconds. TweenMethod (not TweenProperty) because the destination is a per-call
    // SetInstanceShaderParameter, not a plain property.
    private void FadeOutline(float target)
    {
        if (_hover == null) { return; }
        _outlineTween?.Kill();
        _outlineTween = CreateTween();
        _outlineTween.TweenMethod(Callable.From<float>(SetOutline), _outline, target, _hover.FadeSeconds);
    }

    // Tween callback: records the value so the next fade can resume from here (mid-fade, not just
    // from 0 or 1), and pushes it to the shader.
    private void SetOutline(float amount)
    {
        _outline = amount;
        SetInstanceShaderParameter("hover", amount);
    }
}
