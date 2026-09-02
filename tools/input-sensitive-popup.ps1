<#
    input-sensitive-popup.ps1

    Shows a small, always-on-top, borderless warning window while an input/timing-sensitive
    gdUnit4 test run is in progress. Spawned detached (non-blocking) by
    test/InputSensitiveNotice.cs; it is NOT meant to be run by hand.

    The window closes itself when the parent test process exits (polled once a second) or
    after -MaxMinutes as a safety net, so a crashed run never leaves it dangling.

    Launch via Windows PowerShell (powershell.exe): it is STA by default, which WPF's
    ShowDialog() requires. pwsh (7+) is MTA by default and would fail here.
#>
param(
    [int]$ParentPid = 0,
    [int]$MaxMinutes = 45
)

$ErrorActionPreference = 'Stop'

try {
    Add-Type -AssemblyName PresentationFramework, PresentationCore, WindowsBase
} catch {
    # No WPF available (e.g. running on a headless box) - nothing useful to show.
    exit 0
}

$window = New-Object System.Windows.Window
$window.Title = 'gdUnit4 - input-sensitive tests running'
$window.WindowStyle = 'None'
$window.ResizeMode = 'NoResize'
$window.Topmost = $true
$window.ShowInTaskbar = $true
$window.SizeToContent = 'WidthAndHeight'
$window.WindowStartupLocation = 'Manual'
$window.Background = [System.Windows.Media.Brushes]::DarkRed
$window.Left = ([System.Windows.SystemParameters]::PrimaryScreenWidth / 2) - 320
$window.Top = 24

$border = New-Object System.Windows.Controls.Border
$border.Padding = '24'
$border.BorderBrush = [System.Windows.Media.Brushes]::White
$border.BorderThickness = '2'

$stack = New-Object System.Windows.Controls.StackPanel

$title = New-Object System.Windows.Controls.TextBlock
$title.Text = [char]0x26A0 + '  INPUT-SENSITIVE TESTS ARE RUNNING'
$title.Foreground = [System.Windows.Media.Brushes]::White
$title.FontSize = 18
$title.FontWeight = 'Bold'
$stack.Children.Add($title) | Out-Null

$body = New-Object System.Windows.Controls.TextBlock
$body.Text = "Do not use the keyboard or mouse on this machine until the test run finishes.`n" +
             'Simulated input shares the OS input state; real input causes false test failures.'
$body.Foreground = [System.Windows.Media.Brushes]::White
$body.FontSize = 13
$body.Margin = '0,10,0,0'
$body.TextWrapping = 'Wrap'
$body.MaxWidth = 560
$stack.Children.Add($body) | Out-Null

$started = New-Object System.Windows.Controls.TextBlock
$started.Text = 'Started ' + (Get-Date -Format 'HH:mm:ss')
$started.Foreground = [System.Windows.Media.Brushes]::White
$started.FontSize = 11
$started.Margin = '0,10,0,0'
$started.Opacity = 0.8
$stack.Children.Add($started) | Out-Null

$border.Child = $stack
$window.Content = $border

$deadline = (Get-Date).AddMinutes($MaxMinutes)
$timer = New-Object System.Windows.Threading.DispatcherTimer
$timer.Interval = [TimeSpan]::FromSeconds(1)
$timer.Add_Tick({
    $parentGone = $false
    if ($ParentPid -gt 0) {
        try { $null = Get-Process -Id $ParentPid -ErrorAction Stop } catch { $parentGone = $true }
    }
    if ($parentGone -or ((Get-Date) -gt $deadline)) {
        $timer.Stop()
        $window.Close()
    }
})
$timer.Start()

$window.ShowDialog() | Out-Null
