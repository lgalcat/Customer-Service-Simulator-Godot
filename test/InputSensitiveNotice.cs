using System;
using Godot;

/// <summary>
/// Awareness helper for test runs that contain input- or timing-sensitive gdUnit4 suites.
/// <para>
/// gdUnit4's <c>SimulateAction*</c> / <c>SimulateMouse*</c> act on the process-wide
/// <see cref="Input"/> singleton (and the real cursor), not on a per-test runner, so real
/// keyboard/mouse input on the machine - or heavy background CPU load, for the frame-stepping
/// suites - corrupts those tests and yields false failures. Isolating gdUnit4's input is out of
/// scope; this only makes an in-progress run <em>visible</em> regardless of which runner launched
/// it (<c>dotnet test</c>, the VS Code test window, or the gdUnit4 editor toolbar).
/// </para>
/// <para>
/// Call <see cref="AnnounceOnce"/> from a <c>[Before]</c> hook of every sensitive suite. It always
/// writes an stderr banner, and - unless the <c>GDUNIT_NO_INPUT_POPUP</c> environment variable is
/// set - spawns a detached, always-on-top warning window (<c>tools/input-sensitive-popup.*</c>)
/// that self-closes when this test process exits.
/// </para>
/// </summary>
public static class InputSensitiveNotice
{
    private const string PopupResPathWindows = "res://tools/input-sensitive-popup.ps1";
    private const string PopupResPathPosix = "res://tools/input-sensitive-popup.sh";

    private static readonly object Gate = new();
    private static bool _announced;

    /// <summary>
    /// Signals that an input/timing-sensitive test run is underway. Fires at most once per test
    /// process (all suites share a single Godot process), from whichever sensitive suite runs
    /// first; later calls are no-ops.
    /// </summary>
    /// <param name="phase">Short label for the first sensitive suite reached - banner detail only.</param>
    public static void AnnounceOnce(string phase)
    {
        lock (Gate)
        {
            if (_announced)
            {
                return;
            }

            _announced = true;
        }

        WriteBanner(phase);

        // Escape hatch for true-headless CI, where a popup is pointless (and the script's WPF /
        // zenity dependency is absent anyway).
        if (System.Environment.GetEnvironmentVariable("GDUNIT_NO_INPUT_POPUP") is { Length: > 0 })
        {
            return;
        }

        TrySpawnPopup();
    }

    private static void WriteBanner(string phase)
    {
        // stderr, not stdout: gdUnit4's <CaptureStdOut> only intercepts stdout, so this reaches
        // the real console (and the html/trx report) whatever the runner.
        string rule = new('=', 78);
        Console.Error.WriteLine(rule);
        Console.Error.WriteLine($"  INPUT/TIMING-SENSITIVE TESTS ARE RUNNING  ({phase})");
        Console.Error.WriteLine("  Do not use the keyboard or mouse on this machine until the run finishes.");
        Console.Error.WriteLine("  Simulated input shares the OS input state; real input causes false failures.");
        Console.Error.WriteLine("  Suppress the popup with GDUNIT_NO_INPUT_POPUP=1.");
        Console.Error.WriteLine(rule);
        Console.Error.Flush();
    }

    private static void TrySpawnPopup()
    {
        try
        {
            bool isWindows = OS.GetName() == "Windows";
            string resPath = isWindows ? PopupResPathWindows : PopupResPathPosix;
            if (!Godot.FileAccess.FileExists(resPath))
            {
                return;
            }

            string scriptPath = ProjectSettings.GlobalizePath(resPath);
            string parentPid = OS.GetProcessId().ToString();

            // CreateProcess is the non-blocking spawn (unlike OS.Execute); it returns the child
            // PID, or -1 on failure - which we just ignore, the banner already stands.
            if (isWindows)
            {
                OS.CreateProcess("powershell.exe", new[]
                {
                    "-NoProfile", "-ExecutionPolicy", "Bypass", "-WindowStyle", "Hidden",
                    "-File", scriptPath,
                    "-ParentPid", parentPid,
                });
            }
            else
            {
                OS.CreateProcess("/bin/sh", new[] { scriptPath, parentPid });
            }
        }
        catch (Exception e)
        {
            GD.PrintErr($"InputSensitiveNotice: popup spawn failed ({e.Message}); banner only.");
        }
    }
}
