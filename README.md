# Grow OS — the RDWC/DWC grow add-on for Home Assistant

**Turn your Home Assistant sensors into a real grow-management cockpit.** Grow OS is a
free, local-first Home Assistant add-on for hydroponic (RDWC/DWC) growers: a live
instrument dashboard, grow documentation, SOPs, hardware & maintenance tracking,
sensor-driven diagnosis, and risk alerts — all running inside Home Assistant, all on
your own hardware. No cloud, no account, no SaaS.

<p align="center">
  <img src="docs/images/live-dashboard-desktop.png" alt="Grow OS live dashboard" width="100%">
</p>

## Why Grow OS

- **Home Assistant native.** It reads your existing HA entities, so *any* sensor HA
  supports works — pH, EC, water temp, DO, ORP, CO₂, PPFD, tent climate, cameras. No
  proprietary hardware.
- **Built for recirculating hydro (RDWC/DWC).** Reservoir, addback, water changes,
  targets per phase, and a diagnosis engine that maps deviations to symptoms and
  recommended treatments/SOPs.
- **One-click install, zero config.** As an add-on it connects to Home Assistant
  automatically — no URL, no token. Pick your sensors from a dropdown of your real
  entities.
- **Local-first.** Your data stays on your device and is included in Home Assistant's
  backups. Nothing leaves your network.

## Features

- 📊 **Live instrument dashboard** — climate, VPD, reservoir, light status and a
  system score at a glance, with a near-live tent camera.
- 💧 **Addback & water-change assistant** — measure, target, dose, re-check; nothing
  blind.
- 🧪 **Auto-measurements** — capture sensor readings (and camera snapshots) on a
  trigger, e.g. *30 min after lights-on*.
- 🩺 **Diagnosis & risk tracking** — deviations become symptoms with likely causes and
  linked treatments/SOPs; power/pump/DO emergencies get guided recovery SOPs.
- 📚 **Knowledge base** — searchable guides, SOPs, treatments, symptoms, pathogens,
  target values and nutrient programs.
- 🔧 **Hardware & maintenance** — lifespan, inspection and per-sensor calibration
  reminders.
- 📱 **Mobile-friendly** — works right inside the Home Assistant app.

<p align="center">
  <img src="docs/images/live-dashboard-mobile.png" alt="Grow OS on mobile" width="260">
</p>

## Install (Home Assistant add-on)

**Requires Home Assistant OS or Supervised** (add-ons aren't available on Home
Assistant Container/Core).

1. In Home Assistant: **Settings → Add-ons → Add-on Store**.
2. Top-right **⋮ → Repositories**, and add:

   ```
   https://github.com/Nerdstreak/Grow-Operation-System
   ```

3. **Grow OS** appears in the store → **Install** → **Start**.
4. Open it from the Home Assistant sidebar (🌱). It's already connected to Home
   Assistant — just pick your sensors from the dropdown.

Updates are a clean, one-click pull from Home Assistant; your data is preserved.

## Documentation

The docs live under [docs/](docs/) (currently in German):

- [Installation](docs/install.md)
- [Architecture](docs/architecture.md)
- [Grow domain notes](docs/grow-domain-notes.md)
- [Development](docs/development.md)

## License

Released under the [MIT License](LICENSE) — free to use, modify and redistribute.
