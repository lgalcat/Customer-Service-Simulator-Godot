using System;
using Godot;

/// <summary>
/// A person walks up to the desk on <c>MainScreen</c>, asks for some work in writing, then walks back
/// out; the job starts once they are gone. Passive: it plays whether or not the player is looking.
/// <para>The only part is the <see cref="Visitor"/>, placed at <c>VisitorAnchor</c>. It enters and
/// leaves off the right edge of the screen, so it never pops in or out. The request text is the one
/// thing that differs per job (a scene per text).</para>
/// </summary>
public partial class VisitorAnnouncement : JobAnnouncement
{
    private static readonly StringName ScreenName = "MainScreen";
    private static readonly StringName VisitorAnchor = "VisitorAnchor";

    // What the visitor asks for
    [Export(PropertyHint.MultilineText)] private string _request = "";

    // [22/10/2026] Placeholder staging and timing (feet position; the entry point is off the right
    // edge, behind the cubicle's right wall). The whole sequence takes 2 * walk + _requestSeconds
    [Export] private Vector2 _entryPosition = new(680, 205);
    [Export] private Vector2 _standPosition = new(160, 205);
    [Export] private float _walkSpeed = 100f;
    [Export] private float _requestSeconds = 4f;

    private Visitor? _visitor;
    private Tween? _sequence;

    public override StringName TargetScreen => ScreenName;

    protected override void OnAttached()
    {
        _visitor ??= GetNodeOrNull<Visitor>("Visitor") ?? throw new NullReferenceException("VisitorAnnouncement: missing required child 'Visitor'");
        _visitor.Position = _entryPosition;
        Place(_visitor, VisitorAnchor);
    }

    public override void Play()
    {
        Visitor visitor = _visitor ?? throw new InvalidOperationException("VisitorAnnouncement: not attached to a screen yet (Play is only valid after Attach)");
        float walkSeconds = _walkSpeed > 0f ? _entryPosition.DistanceTo(_standPosition) / _walkSpeed : 0f;

        _sequence?.Kill();
        _sequence = CreateTween();
        _sequence.TweenCallback(Callable.From(() => visitor.Walk(_standPosition.X - _entryPosition.X)));
        _sequence.TweenProperty(visitor, "position", _standPosition, walkSeconds);
        _sequence.TweenCallback(Callable.From(() =>
        {
            visitor.Idle();
            visitor.Say(_request);
        }));
        _sequence.TweenInterval(_requestSeconds);
        _sequence.TweenCallback(Callable.From(() =>
        {
            visitor.Hush();
            visitor.Walk(_entryPosition.X - _standPosition.X);
        }));
        _sequence.TweenProperty(visitor, "position", _entryPosition, walkSeconds);
        _sequence.TweenCallback(Callable.From(Finish));
    }
}
