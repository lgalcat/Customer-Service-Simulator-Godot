using System;
using Godot;

/// <summary>
/// What comes out of the printer during the Print Report job: the page being printed, the stack of
/// finished pages, and the stack the player clicks to pick up at the end. A part the job places at
/// <c>PrinterScreen</c>'s <c>OutputAnchor</c>, behind the printer's front, so a page is hidden by the
/// printer body until it slides past the body's lower edge.
/// <para>Purely a view: the job decides what happens and when (job → view calls), and the tray only
/// reports the pickup click (<see cref="PickedUp"/>). Every call is only valid once the tray is in
/// the scene tree (they animate through Tweens).</para>
/// </summary>
public partial class OutputTray : Node2D
{
    // Page position (its top-left corner) when it starts out, fully hidden under the printer body, and
    // when done. The body's lower edge is at y 267 and the screen ends at 360, so at most ~93 px of
    // the page ever shows
    [Export] private Vector2 _feedStart = new(192, 100);
    [Export] private Vector2 _feedEnd = new(192, 196);

    // Tint and shake of a jammed page
    [Export] private Color _jamTint = new(1f, 0.72f, 0.72f);
    [Export] private float _shakeDistance = 2f;

    // Seconds a cleared jammed page takes to be pulled away
    [Export] private float _discardSeconds = 0.3f;

    // Offset between the sheets of the finished stack, and how many sheets it draws at most
    [Export] private Vector2 _stackStep = new(0, -2);
    [Export] private int _maxStackSheets = 8;

    private Sprite2D? _page;
    private Node2D? _stack;
    private SpriteInteractable? _pickup;
    private Tween? _pageTween;
    private int _stackCount;

    /// <summary>
    /// Invoked when the player clicks the finished stack (only clickable after <see cref="ShowPickup"/>).
    /// </summary>
    public Action? PickedUp;

    private Sprite2D Page => _page ??= GetNodeOrNull<Sprite2D>("Page")
        ?? throw new NullReferenceException("OutputTray: missing required child 'Page'");

    private Node2D Stack => _stack ??= GetNodeOrNull<Node2D>("Stack")
        ?? throw new NullReferenceException("OutputTray: missing required child 'Stack'");

    private SpriteInteractable Pickup => _pickup ??= GetNodeOrNull<SpriteInteractable>("Pickup")
        ?? throw new NullReferenceException("OutputTray: missing required child 'Pickup'");

    // The pickup stays hidden until ShowPickup: hiding is what keeps it unpickable, so the screen's own
    // input sweeps over it are never fought. Same lifetime as the tray, so no unsubscribe.
    public override void _Ready()
    {
        Page.Visible = false;
        Pickup.Visible = false;
        Pickup.Clicked += () => PickedUp?.Invoke();
    }

    /// <summary>
    /// Starts a new page sliding out of the printer over <paramref name="seconds"/>.
    /// </summary>
    public void BeginPage(float seconds)
    {
        StopPage();
        Page.Position = _feedStart;
        Page.Modulate = Colors.White;
        Page.Visible = true;
        _pageTween = CreateTween();
        _pageTween.TweenProperty(Page, "position", _feedEnd, seconds);
    }

    /// <summary>
    /// Stops the page where it is, tinted and shaking, as a paper jam.
    /// </summary>
    public void JamPage()
    {
        StopPage();
        Page.Modulate = _jamTint;
        Vector2 stuck = Page.Position;
        _pageTween = CreateTween().SetLoops();
        _pageTween.TweenProperty(Page, "position", stuck + new Vector2(_shakeDistance, 0), 0.05);
        _pageTween.TweenProperty(Page, "position", stuck - new Vector2(_shakeDistance, 0), 0.05);
        _pageTween.TweenProperty(Page, "position", stuck, 0.05);
        _pageTween.TweenInterval(0.6);
    }

    /// <summary>
    /// Pulls the jammed page away (it doesn't count as printed).
    /// </summary>
    public void DiscardPage()
    {
        StopPage();
        _pageTween = CreateTween();
        _pageTween.TweenProperty(Page, "modulate:a", 0f, _discardSeconds);
        _pageTween.TweenCallback(Callable.From(() => Page.Visible = false));
    }

    /// <summary>
    /// The page is done: it joins the finished stack.
    /// </summary>
    public void FinishPage()
    {
        StopPage();
        Page.Visible = false;
        _stackCount++;
        if (_stackCount > _maxStackSheets) { return; }

        // Each sheet a copy of the page's art, piled up from the page's final position
        var sheet = new Sprite2D
        {
            Texture = Page.Texture,
            Centered = false,
            Position = _feedEnd + _stackStep * (_stackCount - 1),
        };
        Stack.AddChild(sheet);
    }

    /// <summary>
    /// Swaps the finished stack for the clickable one.
    /// </summary>
    public void ShowPickup()
    {
        StopPage();
        Page.Visible = false;
        Stack.Visible = false;
        Pickup.Visible = true;
    }

    /// <summary>
    /// Hides everything (the pages were picked up).
    /// </summary>
    public void HideAll()
    {
        StopPage();
        Page.Visible = false;
        Stack.Visible = false;
        Pickup.Visible = false;
    }

    private void StopPage()
    {
        _pageTween?.Kill();
        _pageTween = null;
    }
}
