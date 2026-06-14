#!/usr/bin/env bash
#
# Grow OS - Ein-Zeilen-Installer fuer Raspberry Pi / Linux
#
#   curl -fsSL https://raw.githubusercontent.com/Nerdstreak/Grow-Operation-System/main/scripts/install.sh | bash
#
# Installiert bei Bedarf Docker, laedt das fertige Grow-OS-Image herunter und
# startet es als Dienst (laeuft nach jedem Neustart automatisch weiter).
# Erneutes Ausfuehren = Update auf die neueste Version.

set -euo pipefail

IMAGE="ghcr.io/nerdstreak/grow-operation-system:latest"
APP_DIR="${GROWOS_DIR:-$HOME/grow-os}"
PORT="${GROWOS_PORT:-5076}"

c_green=$'\033[1;32m'; c_yellow=$'\033[1;33m'; c_red=$'\033[1;31m'; c_reset=$'\033[0m'
info() { printf '\n%s==>%s %s\n' "$c_green" "$c_reset" "$1"; }
warn() { printf '%s[!]%s %s\n' "$c_yellow" "$c_reset" "$1"; }
die()  { printf '%s[Fehler]%s %s\n' "$c_red" "$c_reset" "$1" >&2; exit 1; }

[ "$(id -u)" -eq 0 ] && SUDO="" || SUDO="sudo"
command -v curl >/dev/null 2>&1 || die "curl wird benoetigt, ist aber nicht installiert."

# 1) Docker sicherstellen ----------------------------------------------------
NEED_RELOGIN=0
if ! command -v docker >/dev/null 2>&1; then
  info "Docker ist nicht installiert - wird jetzt eingerichtet (dauert ein paar Minuten)..."
  curl -fsSL https://get.docker.com | $SUDO sh || die "Docker-Installation fehlgeschlagen."
  if [ -n "$SUDO" ] && [ -n "${USER:-}" ]; then
    $SUDO usermod -aG docker "$USER" 2>/dev/null && NEED_RELOGIN=1 || true
  fi
  $SUDO systemctl enable --now docker 2>/dev/null || true
fi

# Docker Compose v2 (Plugin) oder klassisches docker-compose finden
if docker compose version >/dev/null 2>&1; then
  COMPOSE=(docker compose)
elif command -v docker-compose >/dev/null 2>&1; then
  COMPOSE=(docker-compose)
else
  die "Docker Compose wurde nicht gefunden."
fi

# Falls der aktuelle Nutzer noch nicht in der docker-Gruppe ist: mit sudo arbeiten
DOCKER_PREFIX=()
if ! docker info >/dev/null 2>&1; then
  if [ -n "$SUDO" ] && $SUDO docker info >/dev/null 2>&1; then
    DOCKER_PREFIX=($SUDO)
  fi
fi

# 2) App-Verzeichnis + Compose-Datei ----------------------------------------
info "Richte Grow OS in $APP_DIR ein..."
mkdir -p "$APP_DIR/data"
cat > "$APP_DIR/docker-compose.yml" <<YAML
services:
  grow-os:
    image: $IMAGE
    container_name: grow-os
    ports:
      - "$PORT:5076"
    volumes:
      - ./data:/data
    environment:
      - ASPNETCORE_URLS=http://0.0.0.0:5076
      - GROWDIARY_DB_PATH=/data/grow-diary.db
    restart: unless-stopped
YAML

# 3) Image laden + starten ---------------------------------------------------
info "Lade Grow OS herunter (das fertige Image, kein Build noetig)..."
( cd "$APP_DIR" && "${DOCKER_PREFIX[@]}" "${COMPOSE[@]}" pull ) || die "Download des Images fehlgeschlagen."
info "Starte Grow OS..."
( cd "$APP_DIR" && "${DOCKER_PREFIX[@]}" "${COMPOSE[@]}" up -d ) || die "Start fehlgeschlagen."

# 4) Zugriff anzeigen --------------------------------------------------------
IP="$(hostname -I 2>/dev/null | awk '{print $1}')"
[ -z "${IP:-}" ] && IP="<pi-ip-adresse>"

cat <<DONE

${c_green}=========================================================${c_reset}
${c_green} Grow OS laeuft! 🌱${c_reset}

   Im Browser oeffnen:   ${c_green}http://$IP:$PORT${c_reset}
   (vom Pi selbst:        http://localhost:$PORT )

   Startet ab jetzt nach jedem Neustart automatisch.
${c_green}=========================================================${c_reset}

 Verwaltung (im Ordner $APP_DIR):
   Aktualisieren:  ${COMPOSE[*]} pull && ${COMPOSE[*]} up -d
   Neustarten:     ${COMPOSE[*]} restart
   Stoppen:        ${COMPOSE[*]} down
   Logs ansehen:   ${COMPOSE[*]} logs -f

DONE

if [ "$NEED_RELOGIN" -eq 1 ]; then
  warn "Damit Docker ohne 'sudo' laeuft: einmal abmelden/anmelden oder den Pi neu starten."
fi
