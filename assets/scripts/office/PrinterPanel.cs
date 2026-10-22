using System;
using Godot;

/// <summary>
/// The printer's button panel on <c>PrinterScreen</c>: a resident device component that turns its
/// buttons' clicks into one indexed event and gives access to each button's <see cref="IndicatorLight"/>.
/// It holds no job logic; a job binding to it (as a fixture) drives the lights and must call
/// <see cref="Reset"/> when it ends, since the panel outlives it.
/// </summary>
public partial class PrinterPanel : Node
{
    /// <summary>
    /// Index of the print button; the others are the function buttons, in panel order.
    /// </summary>
    public const int PrintButton = 0;

    // The panel's buttons in index order (print button first): SpriteInteractables, each with an
    // IndicatorLight child named "Light"
    [Export] private Godot.Collections.Array<Node> _buttons = new();

    private SpriteInteractable[] _interactables = Array.Empty<SpriteInteractable>();
    private IndicatorLight[] _lights = Array.Empty<IndicatorLight>();
    private SpriteInteractable.ClickedEventHandler[] _handlers = Array.Empty<SpriteInteractable.ClickedEventHandler>();

    /// <summary>
    /// Invoked with the button's index when one of the panel's buttons is clicked.
    /// </summary>
    public Action<int>? ButtonPressed;

    /// <summary>
    /// Number of buttons on the panel. Valid after <see cref="_Ready"/>.
    /// </summary>
    public int ButtonCount => _lights.Length;

    public override void _Ready()
    {
        _interactables = new SpriteInteractable[_buttons.Count];
        _lights = new IndicatorLight[_buttons.Count];
        _handlers = new SpriteInteractable.ClickedEventHandler[_buttons.Count];

        for (int i = 0; i < _buttons.Count; i++)
        {
            SpriteInteractable button = _buttons[i] as SpriteInteractable
                ?? throw new InvalidOperationException($"PrinterPanel: button entry {i} is unset or not a SpriteInteractable");
            _interactables[i] = button;
            _lights[i] = button.GetNodeOrNull<IndicatorLight>("Light")
                ?? throw new NullReferenceException($"PrinterPanel: missing required child '{button.Name}/Light'");

            int index = i;
            _handlers[i] = () => ButtonPressed?.Invoke(index);
            button.Clicked += _handlers[i];
        }
    }

    // A C# += on a custom signal is not undone when the subscriber is freed, so mirror _Ready. Entries
    // stay null if _Ready threw part-way (a scene-authoring mistake, not a normal path)
    public override void _ExitTree()
    {
        for (int i = 0; i < _handlers.Length; i++)
        {
            if (_interactables[i] == null || _handlers[i] == null) { continue; }
            _interactables[i].Clicked -= _handlers[i];
        }
    }

    /// <summary>
    /// The light of the button at <paramref name="index"/>.
    /// </summary>
    public IndicatorLight GetLight(int index)
    {
        return _lights[index];
    }

    /// <summary>
    /// Puts every light back to its idle glow.
    /// </summary>
    public void Reset()
    {
        foreach (IndicatorLight light in _lights) { light.Rest(); }
    }
}
