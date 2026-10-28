using System;
using System.Linq;
using System.Threading.Tasks;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using static FillDocumentTestKit;

// Fill Document played from the keyboard: real key events go through the viewport's GUI input path
// (Viewport.PushInput, with the typed character as Unicode) to whatever has focus, as typing does.
// Covers what FillDocumentBehaviourTesting can only check as state: keystrokes land in the focused
// blank, and a locked office or an approved form lets no keystroke through. Keyboard-only navigation of
// the form (Enter/Tab between fields) is default Control behaviour, not a design requirement, so it is
// deliberately not pinned. Real keyboard input during a run reaches the same focused field, hence the
// banner.
// Extends JobTesting for its office/runner setup only; redeclares none of its cases
[TestSuite]
[RequireGodotRuntime]
[GodotExceptionMonitor]
public class FillDocumentInputTesting : JobTesting
{
    protected override Job CreateJob() => InstantiateJob();

    [Before]
    public void AnnounceInputSensitivity() => InputSensitiveNotice.AnnounceOnce(nameof(FillDocumentInputTesting));

    [BeforeTest]
    public override void Setup() => base.Setup();

    [AfterTest]
    public override void Teardown() => base.Teardown();

    private Play Start(Action<Job>? configure = null) => FillDocumentTestKit.Start(office, configure: configure);

    // One press and release per key. A LineEdit inserts the event's Unicode; the Key code only has to be a
    // plausible key, and for ASCII it is the upper-case character's
    private void PushKey(Key key, char unicode)
    {
        Viewport viewport = office.GetViewport();
        viewport.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = unicode, Pressed = true });
        viewport.PushInput(new InputEventKey { Keycode = key, PhysicalKeycode = key, Unicode = unicode, Pressed = false });
    }

    private async Task Type(string text)
    {
        foreach (char symbol in text) { PushKey((Key)char.ToUpperInvariant(symbol), symbol); }
        await runner.AwaitIdleFrame();
    }

    // Unlike a typed character, never refused by a field that is already at its MaxLength
    private async Task PressBackspace()
    {
        PushKey(Key.Backspace, '\0');
        await runner.AwaitIdleFrame();
    }

    // Everything typed anywhere on the form, top to bottom, pre-filled lines' (hidden) fields included
    private static string AllTyped(Play play) => string.Concat(play.Document.Lines.Select(line => line.InputText));

    [TestCase]
    public async Task TypedKeysLandInTheFocusedBlank()
    {
        Play play = Start();
        DocumentLine blank = play.Blanks[0];
        blank.FocusInput();

        await Type(ValueFor(play, blank));

        AssertThat(blank.InputText).IsEqual(ValueFor(play, blank));

        await PressBackspace();

        AssertThat(blank.InputText).IsEqual(ValueFor(play, blank)[..^1]);
    }

    // The Distraction overlay blocks the mouse only; the lock itself must stop the keystrokes. Unlocking
    // gives focus back to nobody, so keys still go nowhere until the player picks a field
    [TestCase]
    public async Task KeysTypedWhileTheOfficeIsLockedReachNoBlank()
    {
        Play play = Start(WithBlanks(2));
        play.Blanks[0].FocusInput();
        await Type("A");

        office.InputLocked = true;
        await Type("BC");

        AssertThat(AllTyped(play)).IsEqual("A");

        office.InputLocked = false;
        await Type("D");

        AssertThat(AllTyped(play)).IsEqual("A");
    }

    // Approval takes focus from the field being typed in, so a closed form takes no more keystrokes
    [TestCase]
    public async Task KeysTypedAfterApprovalChangeNothing()
    {
        Play play = Start(WithBlanks(1));
        DocumentLine blank = play.Blanks[0];
        blank.FocusInput();
        await Type(ValueFor(play, blank));
        PressSubmit(play.Document);
        AssertThat(StampShown(play.Document)).IsTrue();

        await PressBackspace();

        AssertThat(blank.InputText).IsEqual(ValueFor(play, blank));
        AssertThat(MarkOf(blank)).IsEqual(Mark.Correct);
    }
}
