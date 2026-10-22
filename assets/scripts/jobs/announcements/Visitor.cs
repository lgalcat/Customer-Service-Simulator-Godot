using System;
using Godot;

/// <summary>
/// A person walking up to the desk: the visible part of a <see cref="VisitorAnnouncement"/>, placed at
/// <c>MainScreen</c>'s <c>VisitorAnchor</c> (between the door and the cubicle's front, so the
/// partition hides everything below the shoulders). The root's origin is the person's feet.
/// <para>Purely a view: the announcement decides where the visitor goes and when (it moves the root
/// itself); the visitor only switches animation, facing and the request text.</para>
/// </summary>
public partial class Visitor : Node2D
{
    // Which way the body art faces unflipped
    [Export] private bool _artFacesLeft = true;

    private AnimatedSprite2D? _body;
    private Label? _request;

    // Resolved on first use: the announcement drives the visitor before it enters the tree
    private AnimatedSprite2D Body => _body ??= GetNodeOrNull<AnimatedSprite2D>("Body")
        ?? throw new NullReferenceException("Visitor: missing required child 'Body'");

    private Label Request => _request ??= GetNodeOrNull<Label>("Request")
        ?? throw new NullReferenceException("Visitor: missing required child 'Request'");

    /// <summary>
    /// Faces the horizontal direction of travel (negative = left) and plays the walk.
    /// </summary>
    public void Walk(float directionX)
    {
        Body.FlipH = (directionX > 0f) == _artFacesLeft;
        Body.Play("walk");
    }

    /// <summary>
    /// Stands still.
    /// </summary>
    public void Idle()
    {
        Body.Play("idle");
    }

    /// <summary>
    /// Shows the request text beside the visitor.
    /// </summary>
    public void Say(string text)
    {
        Request.Text = text;
        Request.Visible = true;
    }

    /// <summary>
    /// Hides the request text.
    /// </summary>
    public void Hush()
    {
        Request.Visible = false;
    }
}
