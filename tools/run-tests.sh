#!/bin/sh
# run-tests.sh - preferred local / agent entry point for the gdUnit4 suite.
#
# Warns that some suites simulate keyboard/mouse through the OS-global Input state (real input
# during the run causes false failures), pauses briefly so it can be aborted, then runs
# `dotnet test "$@"` with any arguments passed through.
#
# Examples:
#   tools/run-tests.sh
#   tools/run-tests.sh --filter "FullyQualifiedName~PlayerTesting"
set -e
root="$(cd "$(dirname "$0")/.." && pwd)"

cat <<'EOF'

================================================================================
  gdUnit4 TEST RUN - INPUT/TIMING-SENSITIVE SUITES INCLUDED
================================================================================
  Some suites simulate keyboard/mouse through the OS-global Input state. Real
  keyboard/mouse input on this machine during the run will cause false failures.
  Close input-generating apps and keep hands off until it finishes (~a few min).

  Sensitive suites: PlayerTesting, PlatformerBehaviourTesting,
  ThrowPaperBallBehaviourTesting, CardTesting, SwatterTesting,
  SolitaireBehaviourTesting, BallTesting, GoalTesting, FlyTesting,
  FlySpawnerTesting, TrashCanTesting, FlySwatterBehaviourTesting

  A warning dialog shows while the run is active (zenity/osascript, best effort).
  Suppress it with GDUNIT_NO_INPUT_POPUP=1.
================================================================================
EOF

i=5
while [ "$i" -ge 1 ]; do
    printf '\r  Starting in %ss ... (Ctrl-C to abort) ' "$i"
    sleep 1
    i=$((i - 1))
done
printf '\r  Starting now.                              \n\n'

cd "$root"
exec dotnet test "$@"
