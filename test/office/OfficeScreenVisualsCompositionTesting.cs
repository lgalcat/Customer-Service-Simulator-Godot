using Godot;
using GdUnit4;
using static GdUnit4.Assertions;
using System.Threading.Tasks;

// Standing regression check for every Office screen's layered art composition.
// One [TestCase] per screen - independent per case (its own ISceneRunner, own images),
// no shared state, so parameterized over sequential per-screen blocks per project conventions.
//
// This is a coarse tripwire, not a correctness oracle: a real seam and expected anti-aliasing/
// tonal noise can land in the same rough pixel-count ballpark (confirmed empirically against
// main_screen). A failure here means "go run the documented investigation",
// not "the art is definitely broken".
[TestSuite]
[RequireGodotRuntime]
public class OfficeScreenVisualsCompositionTesting
{
    // Out of 255 per channel - not 0, since sequential alpha-blending of several
    // separately-authored RGBA layers isn't guaranteed bit-identical to a single flattened paint.
    private const int ChannelTolerance = 2;

    // Fraction of total pixels allowed to exceed ChannelTolerance before this fails. 1% is
    // calibrated against main_screen's validated baseline (~1674/230400 once real defects were
    // fixed) while still catching a repeat of the back/front alpha-crossfade seam this suite
    // originally found (~2559/230400) - see the case log for both numbers.
    private const double MismatchFractionTolerance = 0.01;

    [TestCase("main_screen",
        "res://assets/scenes/office/main_screen/main_screen.tscn",
        "res://assets/textures/office/main_screen/office_main_base_v0.png")]
    public async Task CompositedVisualsMatchReference(string screenName, string scenePath, string referencePath)
    {
        using ISceneRunner runner = ISceneRunner.Load(scenePath, true, true);
        // Render is one frame behind logic - two idle frames to be safe before reading it back.
        await runner.AwaitIdleFrame();
        await runner.AwaitIdleFrame();

        Image actual = runner.Scene()!.GetViewport().GetTexture().GetImage();
        Image expected = GD.Load<Texture2D>(referencePath).GetImage();

        // Tripwire: fails loudly if the runner's viewport doesn't match the reference size,
        // instead of silently comparing garbage.
        AssertThat(actual.GetSize()).IsEqual(expected.GetSize());

        actual.Convert(Image.Format.Rgba8);
        expected.Convert(Image.Format.Rgba8);

        int width = expected.GetWidth();
        int height = expected.GetHeight();
        int mismatches = 0;
        int maxDelta = 0;
        Vector2I worstPixel = new(-1, -1);
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                Color a = actual.GetPixel(x, y);
                Color e = expected.GetPixel(x, y);
                int delta = Mathf.Max(Mathf.Max(Mathf.Abs(a.R8 - e.R8), Mathf.Abs(a.G8 - e.G8)),
                                       Mathf.Max(Mathf.Abs(a.B8 - e.B8), Mathf.Abs(a.A8 - e.A8)));
                if (delta > maxDelta) { maxDelta = delta; worstPixel = new Vector2I(x, y); }
                if (delta > ChannelTolerance) { mismatches++; }
            }
        }

        int allowedMismatches = (int)(width * height * MismatchFractionTolerance);
        GD.Print($"[{screenName}] visuals pixel diff: {mismatches}/{width * height} pixels exceed tolerance {ChannelTolerance} (allowed {allowedMismatches}); max delta {maxDelta} at {worstPixel}");
        AssertThat(mismatches).IsLessEqual(allowedMismatches);
    }
}
