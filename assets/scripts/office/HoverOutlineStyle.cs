using Godot;

/// <summary>
/// Shared hover-outline look for <see cref="InteractableFeedback"/>. One resource can drive any number of elements.
/// </summary>
[GlobalClass]
public partial class HoverOutlineStyle : Resource
{
    /// <summary>Colour of the one-texel outline drawn outside the art while hovered.</summary>
    [Export] public Color OutlineColor = Colors.White;

    /// <summary>Seconds to fade the outline in on hover and out on hover-end.</summary>
    [Export] public float FadeSeconds = 0.12f;

    /// <summary>Shader providing the outline; expected to expose an <c>outline_color</c> uniform and a <c>hover</c> instance uniform.</summary>
    [Export] public Shader? OutlineShader;
}
