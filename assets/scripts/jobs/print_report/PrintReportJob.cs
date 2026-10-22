using System;
using Godot;

/// <summary>
/// The "Print Report" job: the player prints a number of pages on <c>PrinterScreen</c> and picks up
/// the finished stack. The print button starts a page while the printer is ready. A print can run
/// into trouble: one of the function buttons blinks fast for a short window, and pressing it in time
/// saves the page. A wrong button or a missed warning jams the printer: the page sticks, a code plays
/// on the function buttons' lights, and the player repeats it to clear the jam (a wrong press resets
/// the entry; the print button replays the code). A jammed page doesn't count.
/// <para>Whether a print has trouble is rolled per print with <see cref="TroubleOdds"/>, which damps
/// lucky and unlucky streaks. The job drives the resident <see cref="PrinterPanel"/> (bound as a
/// fixture, its lights reset when the job ends) and places two parts of its own: the
/// <see cref="OutputTray"/> at <c>OutputAnchor</c> and the <see cref="PrintTicket"/> at
/// <c>TicketAnchor</c>.</para>
/// </summary>
public partial class PrintReportJob : Job
{
    private enum PrintState { err, ready, printing, warning, jammed, collecting, done }

    private static readonly StringName ScreenName = "PrinterScreen";
    private static readonly StringName PanelFixture = "PrinterPanel";
    private static readonly StringName OutputAnchor = "OutputAnchor";
    private static readonly StringName TicketAnchor = "TicketAnchor";

    // [22/10/2026] Placeholder balance (step = difficulty - 1): pages = _basePages + step * _pagesPerDifficulty;
    // code length = _baseCodeLength + step * _codeLengthPerDifficulty (2..8); warning window shrinks by
    // _warningSecondsPerDifficulty per step down to _minWarningSeconds. Trouble odds: the long-run trouble
    // rate is roughly rise / (rise + drop), so difficulty scales the rise, not the start
    [Export] private int _basePages = 3;
    [Export] private int _pagesPerDifficulty = 1;
    [Export] private float _printSeconds = 2.5f;
    [Export] private int _baseCodeLength = 3;
    [Export] private int _codeLengthPerDifficulty = 1;
    [Export] private float _baseWarningSeconds = 1f;
    [Export] private float _warningSecondsPerDifficulty = 0.1f;
    [Export] private float _minWarningSeconds = 0.4f;
    [Export] private float _startChance = 0.15f;
    [Export] private float _baseRise = 0.12f;
    [Export] private float _risePerDifficulty = 0.03f;
    [Export] private float _drop = 0.35f;
    [Export] private float _minChance = 0.05f;
    [Export] private float _maxChance = 0.75f;

    // Light timings, in seconds
    [Export] private float _printBlinkPeriod = 0.8f;
    [Export] private float _warningBlinkPeriod = 0.16f;
    [Export] private float _codeLeadIn = 0.5f;
    [Export] private float _codeFlashSeconds = 0.4f;
    [Export] private float _codeGapSeconds = 0.2f;
    [Export] private float _pressFlashSeconds = 0.2f;

    // Seconds between the pickup and completion (the stack vanishes first); 0 completes immediately
    [Export] private float _completionDelay = 0.6f;

    private OutputTray? _tray;
    private PrintTicket? _ticket;
    private Timer? _printTimer;
    private Timer? _completionTimer;
    private PrinterPanel? _panel;
    private TroubleOdds? _odds;
    private PrintState _state = PrintState.err;

    private int _pages;
    private int _printed;
    private int _codeLength;
    private float _warningSeconds;

    // The page being printed: whether its trouble is still to come, whether it had any, and when
    private bool _troublePending;
    private bool _pageHadTrouble;
    private float _troubleAt;
    private int _warningButton;

    private JamCode? _code;
    private Tween? _playback;
    private bool _playingBack;

    public override StringName TargetScreen => ScreenName;

    public override void Setup(int difficulty)
    {
        Difficulty = difficulty;
        ResolveParts();

        int step = Math.Max(0, difficulty - 1);
        _pages = Math.Max(1, _basePages + step * _pagesPerDifficulty);
        _codeLength = Math.Clamp(_baseCodeLength + step * _codeLengthPerDifficulty, 2, 8);
        _warningSeconds = Math.Max(_minWarningSeconds, _baseWarningSeconds - step * _warningSecondsPerDifficulty);
        _odds = new TroubleOdds(_startChance, _baseRise + step * _risePerDifficulty, _drop, _minChance, _maxChance);

        _ticket!.ShowProgress(0, _pages);
    }

    private void ResolveParts()
    {
        _tray ??= GetNodeOrNull<OutputTray>("Tray") ?? throw new NullReferenceException("PrintReportJob: missing required child 'Tray'");
        _ticket ??= GetNodeOrNull<PrintTicket>("Ticket") ?? throw new NullReferenceException("PrintReportJob: missing required child 'Ticket'");
        if (_printTimer == null)
        {
            _printTimer = GetNodeOrNull<Timer>("PrintTimer") ?? throw new NullReferenceException("PrintReportJob: missing required child 'PrintTimer'");
            _printTimer.OneShot = true;
            _printTimer.Timeout += OnPrintTimeout;
        }
        if (_completionTimer == null)
        {
            _completionTimer = GetNodeOrNull<Timer>("CompletionDelay") ?? throw new NullReferenceException("PrintReportJob: missing required child 'CompletionDelay'");
            _completionTimer.OneShot = true;
            _completionTimer.Timeout += Complete;
        }
    }

    protected override void OnAttached()
    {
        ResolveParts();
        PrinterPanel panel = Screen!.GetFixture<PrinterPanel>(PanelFixture);
        _panel = panel;

        Place(_tray!, OutputAnchor);
        Place(_ticket!, TicketAnchor);
        // The tray is this job's own part, freed with it: no cleanup to register
        _tray!.PickedUp += OnPickedUp;

        // The panel is resident and outlives the job: unsubscribe, and leave its lights as found
        panel.ButtonPressed += OnButtonPressed;
        AddCleanup(() => panel.ButtonPressed -= OnButtonPressed);
        AddCleanup(panel.Reset);
    }

    // Lights and timers need the tree, which OnAttached runs before
    public override void _Ready()
    {
        if (_panel == null || _odds == null) { return; }
        EnterReady();
    }

    private void OnButtonPressed(int button)
    {
        if (IsCompleted) { return; }

        switch (_state)
        {
            case PrintState.ready:
                if (button == PrinterPanel.PrintButton) { StartPrint(); }
                break;
            case PrintState.warning:
                if (button == PrinterPanel.PrintButton) { break; }
                if (button == _warningButton) { CatchTrouble(); }
                else { Jam(); }
                break;
            case PrintState.jammed:
                if (_playingBack) { break; }
                if (button == PrinterPanel.PrintButton) { PlayCode(); }
                else { EnterCode(button); }
                break;
            default:
                // Printing with no warning up, collecting, done: the buttons do nothing
                break;
        }
    }

    private void EnterReady()
    {
        _state = PrintState.ready;
        _panel!.Reset();
        _panel.GetLight(PrinterPanel.PrintButton).TurnOn();
        _ticket!.ShowStatus("Ready: press PRINT");
    }

    private void StartPrint()
    {
        _state = PrintState.printing;
        _pageHadTrouble = _odds!.Roll();
        _troublePending = _pageHadTrouble;
        _tray!.BeginPage(_printSeconds);
        _panel!.GetLight(PrinterPanel.PrintButton).StartBlink(_printBlinkPeriod);
        _ticket!.ShowStatus("Printing...");

        if (!_pageHadTrouble)
        {
            StartTimer(_printSeconds);
            return;
        }

        _odds.RecordTrouble();
        _troubleAt = (float)GD.RandRange(0.25, 0.6) * _printSeconds;
        _warningButton = GD.RandRange(PrinterPanel.PrintButton + 1, _panel.ButtonCount - 1);
        StartTimer(_troubleAt);
    }

    private void OnPrintTimeout()
    {
        if (IsCompleted) { return; }

        if (_state == PrintState.printing)
        {
            if (_troublePending) { EnterWarning(); }
            else { FinishPage(); }
        }
        else if (_state == PrintState.warning) { Jam(); }
    }

    private void EnterWarning()
    {
        _state = PrintState.warning;
        _troublePending = false;
        _panel!.GetLight(_warningButton).StartBlink(_warningBlinkPeriod);
        _ticket!.ShowStatus("Warning! Press the blinking button");
        // The window never outlasts the print itself
        StartTimer(Math.Min(_warningSeconds, _printSeconds - _troubleAt));
    }

    private void CatchTrouble()
    {
        float elapsed = (float)(_printTimer!.WaitTime - _printTimer.TimeLeft);
        _state = PrintState.printing;
        _panel!.GetLight(_warningButton).Flash(_pressFlashSeconds);
        _ticket!.ShowStatus("Printing...");
        StartTimer(_printSeconds - _troubleAt - elapsed);
    }

    private void Jam()
    {
        _printTimer!.Stop();
        _state = PrintState.jammed;
        _tray!.JamPage();
        // [22/10/2026] Only one jam kind so far. A second one (e.g. "overheat": leave the printer alone
        // for a while, any press restarts the cooldown) would pick its kind here
        _code = JamCode.Random(_codeLength, PrinterPanel.PrintButton + 1, _panel!.ButtonCount - 1);
        PlayCode();
    }

    // Plays the jam code on the function buttons' lights; input is ignored meanwhile. Afterwards the
    // print button stays lit: pressing it plays the code again
    private void PlayCode()
    {
        _playback?.Kill();
        _code!.Reset();
        _playingBack = true;
        _panel!.Reset();
        _ticket!.ShowStatus("PAPER JAM: repeat the code (PRINT replays it)");

        _playback = CreateTween();
        _playback.TweenInterval(_codeLeadIn);
        foreach (int button in _code.Sequence)
        {
            IndicatorLight light = _panel.GetLight(button);
            _playback.TweenCallback(Callable.From(() => light.Flash(_codeFlashSeconds)));
            _playback.TweenInterval(_codeFlashSeconds + _codeGapSeconds);
        }
        _playback.TweenCallback(Callable.From(FinishPlayback));
    }

    private void FinishPlayback()
    {
        _playingBack = false;
        _panel!.GetLight(PrinterPanel.PrintButton).TurnOn();
    }

    private void EnterCode(int button)
    {
        switch (_code!.Press(button))
        {
            case JamCode.Result.Correct:
                _panel!.GetLight(button).Flash(_pressFlashSeconds);
                _ticket!.ShowStatus($"PAPER JAM: code {_code.Progress} / {_code.Sequence.Count}");
                break;
            case JamCode.Result.Wrong:
                for (int i = PrinterPanel.PrintButton + 1; i < _panel!.ButtonCount; i++) { _panel.GetLight(i).Flash(_pressFlashSeconds); }
                _ticket!.ShowStatus("Wrong code: try again (PRINT replays it)");
                break;
            case JamCode.Result.Solved:
                _code = null;
                _tray!.DiscardPage();
                EnterReady();
                _panel!.GetLight(button).Flash(_pressFlashSeconds);
                break;
        }
    }

    private void FinishPage()
    {
        _tray!.FinishPage();
        if (!_pageHadTrouble) { _odds!.RecordClean(); }
        _printed++;
        _ticket!.ShowProgress(_printed, _pages);

        if (_printed < _pages)
        {
            EnterReady();
            return;
        }

        _state = PrintState.collecting;
        _panel!.Reset();
        _tray.ShowPickup();
        _ticket.ShowStatus("Done: collect the pages");
    }

    private void OnPickedUp()
    {
        if (_state != PrintState.collecting || IsCompleted) { return; }

        _state = PrintState.done;
        _tray!.HideAll();
        _ticket!.ShowStatus("Collected");
        if (_completionDelay <= 0f) { Complete(); }
        else { _completionTimer!.Start(_completionDelay); }
    }

    // Timer.Start rejects a zero wait, so a segment that has already run out just ends now
    private void StartTimer(float seconds)
    {
        if (seconds <= 0.01f)
        {
            _printTimer!.Stop();
            OnPrintTimeout();
            return;
        }
        _printTimer!.Start(seconds);
    }
}
