#!/usr/bin/env bash
# Vor `git commit`: das volle lokale Tor. Rot => der Commit findet nicht statt.
#
# WOZU. Am 19.08.2026 sind zwei Commits hintereinander bei rotem CI gelandet,
# weil ich lokal gruen gemeldet und CI nie angesehen habe. Das Tor hier ist
# dasselbe, das ci.yml faehrt — was hier durchkommt, kommt dort durch.
#
# Reine Text-Aenderungen (Changelog, Notizen) laufen ohne Tor durch: dort kann
# nichts uebersetzen.
set -uo pipefail

WURZEL="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
EINGABE="$(cat)"

befehl="$(printf '%s' "$EINGABE" | python -c "import json,sys
try: print(json.load(sys.stdin).get('tool_input',{}).get('command',''))
except Exception: print('')" 2>/dev/null)"

# Nur beim echten Commit, nicht bei `git log --grep commit` o.ae.
printf '%s' "$befehl" | grep -qE '(^|[;&|]|\s)git\s+(-[^ ]+\s+)*commit(\s|$)' || exit 0

vorgemerkt="$(cd "$WURZEL" && git diff --cached --name-only)"
[ -z "$vorgemerkt" ] && exit 0

# Nur Text? Dann gibt es nichts zu uebersetzen.
if ! printf '%s\n' "$vorgemerkt" | grep -qE '\.(cs|ts|tsx|css|json|csproj|slnx)$'; then
  exit 0
fi

fehler=""

if printf '%s\n' "$vorgemerkt" | grep -qE '\.(cs|csproj|slnx)$'; then
  if ! a="$(cd "$WURZEL" && dotnet test GrowDiary.slnx --nologo -v q 2>&1)"; then
    fehler="$fehler
BACKEND ROT:
$(printf '%s' "$a" | grep -E 'FAIL|Fehler:|error ' | head -10)"
  fi
fi

if printf '%s\n' "$vorgemerkt" | grep -qE '\.(ts|tsx|css|json)$'; then
  if ! a="$(cd "$WURZEL/GrowDiary.React" && npx tsc -b --force 2>&1)"; then
    fehler="$fehler
TYPEN ROT:
$(printf '%s' "$a" | grep -E 'error TS' | head -10)"
  fi
  if ! a="$(cd "$WURZEL/GrowDiary.React" && npm run lint 2>&1)"; then
    fehler="$fehler
LINT ROT:
$(printf '%s' "$a" | grep -E 'error' | head -10)"
  fi
  if ! a="$(cd "$WURZEL/GrowDiary.React" && npx vitest run 2>&1)"; then
    fehler="$fehler
VITEST ROT:
$(printf '%s' "$a" | grep -E 'FAIL|×' | head -10)"
  fi
fi

if [ -n "$fehler" ]; then
  {
    echo "Der Commit wurde NICHT ausgefuehrt — das Tor ist rot:"
    echo "$fehler"
    echo
    echo "Dasselbe Tor faehrt ci.yml. Wer hier vorbeikommt, macht CI rot."
  } >&2
  exit 2
fi
exit 0
