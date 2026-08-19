#!/usr/bin/env bash
# Nach jeder Aenderung an Quelltext: uebersetzen lassen.
#
# WOZU. Erfundene Bezeichner fallen sonst erst drei Schritte spaeter auf.
# Belegt am 19.08.2026: `HydroStyle.Rdwc` (heisst RDWC), `--warn` statt
# `--warn-text`, `DemoData__IsEnabled` statt `GROW_OS_DEMO`. Jedes Mal habe ich
# weitergearbeitet, bevor es jemand gemerkt hat.
#
# Exit 2 gibt stderr an Claude zurueck — wie eine Rueckmeldung des Nutzers.
set -uo pipefail

WURZEL="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
EINGABE="$(cat)"

datei="$(printf '%s' "$EINGABE" | python -c "import json,sys
try: print(json.load(sys.stdin).get('tool_input',{}).get('file_path',''))
except Exception: print('')" 2>/dev/null)"

[ -z "$datei" ] && exit 0
case "$datei" in
  *_test.go|*/node_modules/*|*/bin/*|*/obj/*|*/zz-*) exit 0 ;;
esac

meldung=""

case "$datei" in
  *.cs)
    # -p:UseAppHost=false: sonst scheitert der Bau an der laufenden App, die
    # die .exe sperrt — das ist kein Fehler im Quelltext.
    if ! ausgabe="$(cd "$WURZEL" && dotnet build GrowDiary.slnx -v q --nologo -p:UseAppHost=false 2>&1)"; then
      meldung="$(printf '%s' "$ausgabe" | grep -E "error [A-Z]+[0-9]+" | head -8)"
    fi
    ;;
  *.ts|*.tsx)
    # `tsc -b`, NICHT `--noEmit`: tsconfig.json hat "files": [] und nur
    # references. `tsc --noEmit` prueft damit NULL Dateien und ist immer gruen —
    # am 19.08.2026 mehrfach als "Typen ok" gemeldet, ohne etwas zu pruefen.
    if ! ausgabe="$(cd "$WURZEL/GrowDiary.React" && npx tsc -b 2>&1)"; then
      meldung="$(printf '%s' "$ausgabe" | grep -E "error TS" | head -8)"
    fi
    ;;
  *) exit 0 ;;
esac

if [ -n "$meldung" ]; then
  {
    echo "Der Bau ist rot nach der Aenderung an $(basename "$datei"):"
    echo "$meldung"
    echo
    echo "Behebe das, bevor du weiterarbeitest. Bezeichner aus der Datei holen, nicht aus dem Kopf."
  } >&2
  exit 2
fi
exit 0
