using System;
using System.Collections.Generic;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static OfficeTestKit;

// PrinterPanel over hand-built buttons (SpriteInteractable + "Light"): the indexed click event, the lights,
// Reset and the authoring guards. The real printer's wiring is PrinterScreenSceneTesting's
[TestSuite]
[RequireGodotRuntime]
public class PrinterPanelTesting
{
    private ISceneRunner? _runner;

    [AfterTest]
    public void Teardown()
    {
        _runner?.Dispose();
        _runner = null;
    }

    private static SpriteInteractable Button(string name, bool withLight = true)
    {
        SpriteInteractable button = new() { Name = name };
        button.AddChild(new CollisionShape2D { Shape = new RectangleShape2D() });
        if (withLight) { button.AddChild(new IndicatorLight { Name = "Light" }); }
        return button;
    }

    private static PrinterPanel Panel(params Node?[] buttons)
    {
        PrinterPanel panel = new() { Name = "Panel" };
        panel.Set("_buttons", new Godot.Collections.Array<Node>(buttons!));
        return panel;
    }

    // Root > C, A, B (tree order) + a panel listing A, B, C (export order), live. The orders differ so an
    // index can only come from the export list
    private (PrinterPanel Panel, SpriteInteractable[] Buttons) BuildLive()
    {
        SpriteInteractable a = Button("A"), b = Button("B"), c = Button("C");
        Node2D root = new();
        root.AddChild(c);
        root.AddChild(a);
        root.AddChild(b);
        PrinterPanel panel = Panel(a, b, c);
        root.AddChild(panel);
        _runner = ISceneRunner.Load(root, true, true);
        return (panel, new[] { a, b, c });
    }

    [TestCase]
    public void AClickReportsTheButtonsIndexInTheExportedOrder()
    {
        var (panel, buttons) = BuildLive();
        var pressed = new List<int>();
        panel.ButtonPressed += pressed.Add;

        Click(buttons[2]);
        Click(buttons[0]);
        Click(buttons[1]);

        AssertThat(pressed).ContainsExactly(2, 0, 1);
    }

    [TestCase]
    public void ButtonsAndLightsFollowTheExportedOrder()
    {
        var (panel, buttons) = BuildLive();

        AssertThat(panel.ButtonCount).IsEqual(3);
        for (int i = 0; i < buttons.Length; i++)
        {
            AssertThat(panel.GetLight(i)).IsSame(buttons[i].GetNode("Light"));
        }
    }

    // What a job registers as its cleanup: every light at rest, nothing left running
    [TestCase]
    public void ResetPutsEveryLightBackAtRestForGood()
    {
        var (panel, _) = BuildLive();
        panel.GetLight(0).TurnOn();
        panel.GetLight(1).StartBlink(0.2f);
        panel.GetLight(2).Flash(1f);

        panel.Reset();

        var anyLit = new List<bool>();
        StepTweens(panel, 1, () => anyLit.Add(IsLit(panel.GetLight(0)) || IsLit(panel.GetLight(1)) || IsLit(panel.GetLight(2))));
        AssertThat(anyLit).NotContains(true);
    }

    // The panel is resident, but a C# += on a custom signal outlives its subscriber: leaving the tree must
    // undo the subscriptions _Ready made
    [TestCase]
    public void LeavingTheTreeStopsReportingClicks()
    {
        var (panel, buttons) = BuildLive();
        var pressed = new List<int>();
        panel.ButtonPressed += pressed.Add;

        panel.GetParent().RemoveChild(panel);
        AutoFree(panel);
        Click(buttons[0]);

        AssertThat(pressed).IsEmpty();
    }

    // ---- Authoring guards (_Ready called by hand, out of the tree) ----

    [TestCase]
    public void AnUnsetEntryThrowsNamingItsIndex()
    {
        PrinterPanel panel = AutoFree(Panel(AutoFree(Button("A")), null))!;

        AssertThrown(() => panel._Ready()).IsInstanceOf<InvalidOperationException>()
            .HasMessage("PrinterPanel: button entry 1 is unset or not a SpriteInteractable");
    }

    [TestCase]
    public void AnEntryThatIsNotAButtonThrowsNamingItsIndex()
    {
        PrinterPanel panel = AutoFree(Panel(AutoFree(Button("A")), AutoFree(new Node2D { Name = "Plain" })))!;

        AssertThrown(() => panel._Ready()).IsInstanceOf<InvalidOperationException>()
            .HasMessage("PrinterPanel: button entry 1 is unset or not a SpriteInteractable");
    }

    [TestCase]
    public void AButtonWithoutALightThrowsNamingIt()
    {
        PrinterPanel panel = AutoFree(Panel(AutoFree(Button("A")), AutoFree(Button("B", withLight: false))))!;

        AssertThrown(() => panel._Ready()).IsInstanceOf<NullReferenceException>()
            .HasMessage("PrinterPanel: missing required child 'B/Light'");
    }

    // A _Ready that threw part-way leaves entries unset: leaving the tree must not throw on top of that
    [TestCase]
    public void LeavingTheTreeAfterAFailedReadyDoesNotThrow()
    {
        PrinterPanel panel = AutoFree(Panel(AutoFree(Button("A")), null))!;
        AssertThrown(() => panel._Ready()).IsInstanceOf<InvalidOperationException>();

        panel._ExitTree();
    }
}
