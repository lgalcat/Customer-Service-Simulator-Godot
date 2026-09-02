#!/bin/sh
# input-sensitive-popup.sh
#
# Best-effort POSIX counterpart of input-sensitive-popup.ps1: shows a warning while an
# input/timing-sensitive gdUnit4 run is in progress. Spawned detached by
# test/InputSensitiveNotice.cs; not meant to be run by hand.
#
# There is no portable always-on-top toast on Linux/macOS, so this degrades gracefully:
# zenity if present (Linux), else osascript (macOS), else nothing (the stderr banner from
# InputSensitiveNotice still stands).
#
# Args: $1 = parent pid (optional), $2 = max minutes (optional, default 45)

parent_pid="${1:-0}"
max_minutes="${2:-45}"
msg="Input-sensitive gdUnit4 tests are running. Do not use the keyboard or mouse on this machine until the run finishes."

if command -v zenity >/dev/null 2>&1; then
    # --timeout is the safety net; the watcher loop below closes it when the parent exits.
    zenity --warning --no-wait --title="gdUnit4" --text="$msg" --timeout=$((max_minutes * 60)) &
    dialog_pid=$!
elif command -v osascript >/dev/null 2>&1; then
    osascript -e "display notification \"$msg\" with title \"gdUnit4\"" >/dev/null 2>&1
    exit 0
else
    exit 0
fi

# Close the dialog once the parent test process is gone (or after the cap).
elapsed=0
while [ "$elapsed" -lt "$((max_minutes * 60))" ]; do
    if [ "$parent_pid" -gt 0 ] && ! kill -0 "$parent_pid" 2>/dev/null; then
        break
    fi
    sleep 1
    elapsed=$((elapsed + 1))
done
kill "$dialog_pid" 2>/dev/null
exit 0
