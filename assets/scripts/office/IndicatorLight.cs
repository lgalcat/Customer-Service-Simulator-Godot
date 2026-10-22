using Godot;

/// <summary>
/// A coloured light over an Office element's art: a copy of the element's own texture drawn on top of
/// it, tinted by <see cref="LightColor"/> and faded in through alpha. At rest it glows faintly, which
/// is what tells similar-looking elements apart (unlike a pulse, it never goes away on hover); a
/// system driving the element can turn it on, flash it, or blink it for attention.
/// <para>Scene convention: a <c>Sprite2D</c> child named "Light" after the element's "Sprite", with the
/// same full-canvas texture and <c>centered = false</c>. Drawn with normal blending, so
/// <see cref="LightColor"/> multiplies the art: channels above 1 compensate for grey art (the lit
/// colour is roughly art × <see cref="LightColor"/>).</para>
/// </summary>
public partial class IndicatorLight : Sprite2D
{
    /// <summary>
    /// The light's hue, multiplied with the art; its alpha is ignored (the level drives alpha).
    /// </summary>
    [Export] public Color LightColor { get; set; } = Colors.White;

    // Opacity of the tinted copy when idle (the identity glow) and when lit
    [Export] private float _restLevel = 0.35f;
    [Export] private float _peakLevel = 1f;

    private Tween? _tween;

    public override void _Ready()
    {
        SetLevel(_restLevel);
    }

    /// <summary>
    /// Lights up and stays lit.
    /// </summary>
    public void TurnOn()
    {
        StopTween();
        SetLevel(_peakLevel);
    }

    /// <summary>
    /// Back to the idle glow, stopping any flash or blink.
    /// </summary>
    public void Rest()
    {
        StopTween();
        SetLevel(_restLevel);
    }

    /// <summary>
    /// Lights up for <paramref name="seconds"/>, holding most of that time and fading back to rest at
    /// the end, so consecutive flashes read as separate.
    /// </summary>
    public void Flash(float seconds)
    {
        StopTween();
        SetLevel(_peakLevel);
        _tween = CreateTween();
        _tween.TweenInterval(seconds * 0.6f);
        _tween.TweenMethod(Callable.From<float>(SetLevel), _peakLevel, _restLevel, seconds * 0.4f);
    }

    /// <summary>
    /// Blinks on and off (a hard LED blink, not a fade) every <paramref name="period"/> seconds until
    /// <see cref="Rest"/>, <see cref="TurnOn"/> or <see cref="Flash"/>.
    /// </summary>
    public void StartBlink(float period)
    {
        StopTween();
        _tween = CreateTween().SetLoops();
        _tween.TweenCallback(Callable.From(() => SetLevel(_peakLevel)));
        _tween.TweenInterval(period * 0.5f);
        _tween.TweenCallback(Callable.From(() => SetLevel(_restLevel)));
        _tween.TweenInterval(period * 0.5f);
    }

    private void StopTween()
    {
        _tween?.Kill();
        _tween = null;
    }

    private void SetLevel(float level)
    {
        SelfModulate = new Color(LightColor.R, LightColor.G, LightColor.B, level);
    }
}
