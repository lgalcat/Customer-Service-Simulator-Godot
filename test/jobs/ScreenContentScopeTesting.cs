using System;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static JobTestKit;

// ScreenContentScope over a hand-built screen. Each part carries a StubInteractable writing to the same
// EventLog as the cleanups, so "cleanups first, then parts, each last-first" is one ordered journal: a
// part's removal shows up as its interactable being switched off. No SceneTree needed
[TestSuite]
[RequireGodotRuntime]
public class ScreenContentScopeTesting
{
    private EventLog _log = null!;
    private OfficeScreen _screen = null!;
    private ScreenContentScope _scope = null!;

    [BeforeTest]
    public void Setup()
    {
        _log = new EventLog();
        _screen = AutoFree(BuildScreen("S"))!;
        // Manual _Ready() call - no SceneTree to auto-trigger it
        _screen._Ready();
        _scope = new ScreenContentScope(_screen);
    }

    [AfterTest]
    public void Teardown()
    {
        // Cleanup is handled by AutoFree(...)
    }

    private Node2D Part(string label)
    {
        Node2D part = new() { Name = label };
        part.AddChild(new StubInteractable { Label = label, Journal = _log });
        return part;
    }

    private Action Cleanup(string label) => () => _log.Add(label);

    // Undo actions may touch what the parts are (a part's own subscription), so they all run while
    // every part is still in place
    [TestCase]
    public void DisposeRunsCleanupsLastFirstThenRemovesPartsLastFirst()
    {
        _scope.Place(AutoFree(Part("P1"))!, "Anchor");
        _scope.AddCleanup(Cleanup("C1"));
        _scope.Place(AutoFree(Part("P2"))!, "Anchor");
        _scope.AddCleanup(Cleanup("C2"));
        _log.Clear();

        _scope.Dispose();

        AssertThat(_log.Entries).ContainsExactly("C2", "C1", "P2:SetInteractable(false)", "P1:SetInteractable(false)");
    }

    // Teardown has to finish: a throwing cleanup is logged and the rest still run
    [TestCase]
    public void AFailingCleanupDoesNotStopTheRest()
    {
        _scope.Place(AutoFree(Part("P1"))!, "Anchor");
        _scope.AddCleanup(Cleanup("C1"));
        _scope.AddCleanup(() => throw new InvalidOperationException("cleanup failed on purpose"));
        _scope.AddCleanup(Cleanup("C3"));
        _log.Clear();

        _scope.Dispose();

        AssertThat(_log.Entries).ContainsExactly("C3", "C1", "P1:SetInteractable(false)");
    }

    [TestCase]
    public void DisposeIsIdempotent()
    {
        _scope.Place(AutoFree(Part("P1"))!, "Anchor");
        _scope.AddCleanup(Cleanup("C1"));
        _scope.Dispose();
        _log.Clear();

        _scope.Dispose();

        AssertThat(_log.Entries).IsEmpty();
    }

    [TestCase]
    public void PlaceAndAddCleanupAfterDisposeThrow()
    {
        _scope.Dispose();
        Node2D part = AutoFree(Part("P1"))!;

        AssertThrown(() => _scope.Place(part, "Anchor")).IsInstanceOf<ObjectDisposedException>();
        AssertThrown(() => _scope.AddCleanup(Cleanup("C1"))).IsInstanceOf<ObjectDisposedException>();
        AssertThat(part.GetParent()).IsNull();
    }

    // The whole tree going down can free the screen first; it took its content with it
    [TestCase]
    public void DisposeAfterTheScreenWasFreedStillRunsCleanups()
    {
        OfficeScreen screen = BuildScreen("Doomed");
        screen._Ready();
        ScreenContentScope scope = new(screen);
        scope.Place(Part("P1"), "Anchor");
        scope.AddCleanup(Cleanup("C1"));
        screen.Free();
        _log.Clear();

        scope.Dispose();

        AssertThat(_log.Entries).ContainsExactly("C1");
    }

    [TestCase]
    public void DisposeToleratesAPartFreedElsewhere()
    {
        Node2D first = Part("P1");
        _scope.Place(first, "Anchor");
        _scope.Place(AutoFree(Part("P2"))!, "Anchor");
        first.Free();
        _log.Clear();

        _scope.Dispose();

        AssertThat(_log.Entries).ContainsExactly("P2:SetInteractable(false)");
    }

    // A placement the screen refused is not the scope's to take back out
    [TestCase]
    public void AFailedPlaceIsNotTracked()
    {
        Node2D part = AutoFree(Part("P1"))!;
        AssertThrown(() => _scope.Place(part, "Nope")).IsInstanceOf<ArgumentException>();

        _scope.Dispose();

        AssertThat(part.IsQueuedForDeletion()).IsFalse();
    }
}
