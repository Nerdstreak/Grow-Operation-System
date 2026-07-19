# Grow OS — Home Assistant Add-on

Runs [Grow OS](https://github.com/Nerdstreak/Grow-Operation-System) directly inside
Home Assistant. Because Grow OS relies on Home Assistant for all sensor data, this
is the simplest way to install it: one click, no separate server, no manual token.

## Installation

1. In Home Assistant, go to **Settings → Add-ons → Add-on Store**.
2. Top-right **⋮ → Repositories**, add:
   `https://github.com/Nerdstreak/Grow-Operation-System`
3. **Grow OS** appears in the store — click **Install**, then **Start**.
4. Open it from the Home Assistant sidebar (🌱 Grow OS).

## What "native integration" means here

- **No manual connection.** As an add-on, Grow OS receives a Supervisor token and
  reaches Home Assistant at `http://supervisor/core` automatically. You never paste
  a URL or a long-lived access token.
- **Pick sensors from a dropdown.** Grow OS reads your Home Assistant entities and
  lets you choose them from a searchable list (filtered by device class), instead
  of typing entity IDs.
- **Automatic backups.** The Grow OS database lives on the add-on's `/data` volume,
  which is included in Home Assistant snapshots.

## Data & persistence

All Grow OS data (SQLite database, uploads, snapshots) is stored under `/data` and
survives restarts and updates. Uninstalling the add-on removes this data — take a
Home Assistant backup first if you want to keep it.

## Notes

- First install builds the image on the device, so it can take a few minutes.
- Requires a Home Assistant OS or Supervised installation (add-ons are not
  available on Home Assistant Container or Core installs).
