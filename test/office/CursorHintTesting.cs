using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// The cursor shape actually shown can't be read back headless (GetCurrentCursorShape stays Arrow),
// so this covers only that no style/texture combination throws and the style resource keeps its
// Slot/fallback invariant.
[TestSuite]
[RequireGodotRuntime]
public class CursorHintTesting
{
    [AfterTest]
    public void Teardown()
    {
        // These calls reach the real window's cursor; restore the default shape (a registered custom
        // cursor image is not auto-reverted - known engine behaviour)
        Input.SetDefaultCursorShape(Input.CursorShape.Arrow);
    }

    private static CursorHint Build(CursorHintStyle? style)
    {
        CursorHint hint = new();
        if (style != null) { hint.Set("_style", style); }
        return hint;
    }

    // The shipped style, so the texture is a real project resource rather than a hand-made one
    private static CursorHintStyle TexturedStyle() =>
        GD.Load<CursorHintStyle>("res://assets/resources/office/exit_cursor_hint.tres");

    // ---- Every style combination is safe to hover ----

    [TestCase("none")]
    [TestCase("textureless")]
    [TestCase("textured")]
    public void ReadyAndHoverCallsNeverThrow(string kind)
    {
        CursorHintStyle? style = kind switch
        {
            "none" => null,
            "textureless" => new CursorHintStyle(),
            _ => TexturedStyle(),
        };
        CursorHint hint = AutoFree(Build(style))!;

        // Manual _Ready() call - no SceneTree to auto-trigger it (applies to every test below)
        hint._Ready();
        hint.OnHoverStarted();
        hint.OnHoverEnded();
    }

    // Public, unique-named, parameterless: the shape the scenes' editor [connection]s need
    [TestCase]
    public void HoverMethodsAreReachableByNameLikeAnEditorConnectionWouldCallThem()
    {
        CursorHint hint = AutoFree(Build(null))!;

        hint.Call("OnHoverStarted");
        hint.Call("OnHoverEnded");
    }

    // ---- CursorHintStyle ----

    // Slot must differ from the fallback shape, or binding a texture would reskin the fallback cursor
    [TestCase]
    public void SlotAndFallbackShapeDifferByDefault()
    {
        CursorHintStyle style = new();

        AssertThat(style.Slot).IsNotEqual(style.ShapeFallback);
        AssertThat(style.Texture).IsNull();
    }
}
