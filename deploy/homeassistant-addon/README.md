# Grow OS — Home Assistant Add-on repository

This folder is the source for the Grow OS Home Assistant add-on. It lets users
install Grow OS with one click from inside Home Assistant, instead of running the
`curl | bash` installer and wiring up a Home Assistant token by hand.

## Structure

```
homeassistant-addon/
├── repository.yaml        # add-on repository metadata
└── grow-os/
    ├── config.yaml        # the add-on manifest
    ├── Dockerfile         # wraps the published GHCR image
    └── DOCS.md            # user-facing docs (shown in the add-on's "Documentation" tab)
```

## How it works

- The add-on wraps the already-published multi-arch image
  `ghcr.io/nerdstreak/grow-operation-system:latest`.
- `ingress: true` serves the Grow OS UI inside the Home Assistant sidebar; Home
  Assistant handles authentication.
- `homeassistant_api: true` gives the container a `SUPERVISOR_TOKEN`, which Grow OS
  detects (`HomeAssistantAddon`) to connect to Home Assistant automatically — no
  URL or long-lived token needed.

## Publishing

Home Assistant expects an add-on **repository** to live at the root of a git repo
(`repository.yaml` at the top level, each add-on in its own subfolder). Two options:

1. **Dedicated repo (recommended):** copy the contents of this folder to the root of
   a new public repo, e.g. `Nerdstreak/grow-os-addon`. Users then add that repo URL.
2. **This repo:** point users at this repository URL; if Home Assistant does not pick
   up the nested path, move `repository.yaml` + `grow-os/` to the repo root.

## Optional: instant install (no on-device build)

The `Dockerfile` route builds on the Pi at install time (a few minutes). To make
installs instant, publish per-architecture image tags
(`...-aarch64`, `...-armv7`, `...-amd64`) and replace the `Dockerfile` with an
`image:` key in `config.yaml`:

```yaml
image: ghcr.io/nerdstreak/grow-operation-system-{arch}
```
