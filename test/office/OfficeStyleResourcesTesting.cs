using System.Linq;
using System.Text.RegularExpressions;
using Godot;
using GdUnit4;
using static GdUnit4.Assertions;

// The three shipped Office style .tres files and the shader they drive. Invariants only (types,
// non-degenerate timings, shader contract) - exact tuning values are designer-owned and expected
// to drift, so they are never pinned here.
[TestSuite]
[RequireGodotRuntime]
public class OfficeStyleResourcesTesting
{
    private const string Dir = "res://assets/resources/office/";

    // ---- Script-level defaults ----

    // A looping Tween whose steps all take zero time is a hang risk, so a style's timings must stay positive
    [TestCase]
    public void StyleDefaultsKeepTheTweensNonDegenerate()
    {
        AssertThat(new IdlePulseStyle().HalfPeriod).IsGreater(0f);
        AssertThat(new IdlePulseStyle().RestPeriod).IsGreaterEqual(0f);
        AssertThat(new HoverOutlineStyle().FadeSeconds).IsGreater(0f);
    }

    // ---- Shipped resources ----

    [TestCase]
    public void IdlePulseStyleResourceLoadsWithPositiveHalfPeriod()
    {
        IdlePulseStyle style = GD.Load<IdlePulseStyle>(Dir + "idle_pulse_style.tres");

        AssertThat(style).IsNotNull();
        AssertThat(style.HalfPeriod).IsGreater(0f);
        AssertThat(style.RestPeriod).IsGreaterEqual(0f);
    }

    [TestCase]
    public void HoverOutlineStyleResourceLoadsWithTheShaderItNeeds()
    {
        HoverOutlineStyle style = GD.Load<HoverOutlineStyle>(Dir + "hover_outline_style.tres");

        AssertThat(style).IsNotNull();
        AssertThat(style.FadeSeconds).IsGreater(0f);
        AssertThat(style.OutlineShader).IsNotNull();
    }

    // InteractableFeedback sets "outline_color" on the material and "hover" per instance; a shader
    // missing either would fail only at runtime
    [TestCase]
    public void OutlineShaderExposesTheUniformsInteractableFeedbackSets()
    {
        HoverOutlineStyle style = GD.Load<HoverOutlineStyle>(Dir + "hover_outline_style.tres");

        var uniformNames = style.OutlineShader!.GetShaderUniformList()
            .Select(uniform => uniform.AsGodotDictionary()["name"].AsString()).ToList();

        AssertThat(uniformNames).Contains("outline_color");
        // GetShaderUniformList() leaves out instance uniforms, so "hover" is checked in the source
        AssertThat(Regex.IsMatch(style.OutlineShader.Code, @"instance\s+uniform\s+float\s+hover\b")).IsTrue();
    }

    [TestCase]
    public void ExitCursorHintResourceLoadsWithACursorTexture()
    {
        CursorHintStyle style = GD.Load<CursorHintStyle>(Dir + "exit_cursor_hint.tres");

        AssertThat(style).IsNotNull();
        AssertThat(style.Texture).IsNotNull();
        AssertThat(style.Slot).IsNotEqual(style.ShapeFallback);
    }
}
