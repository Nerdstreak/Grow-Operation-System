# Grow OS Dokumentation

Diese Dokumentation ist der zentrale Einstieg fuer Grow Operation System. Sie ersetzt die frueher verstreuten Root-Dokumente, ohne deren Inhalte zu loeschen: historische Originale liegen unter `docs/archive/`.

## Projektueberblick

Grow OS ist eine lokale, selfhosted Grow-Management-App fuer RDWC/DWC-orientierte Workflows. Sie kombiniert:

- Home Assistant Sensor- und Statusdaten
- Grow-, Measurement-, Journal-, Foto- und Task-Dokumentation
- Zelte, Hydro-Setups, Hardware und Wartung
- Addback- und Changeout-Protokolle
- SOPs, Empfehlungen und RiskEvents
- PWA-Nutzung fuer mobile Workflows

## Dokumente

- [Setup](setup.md): lokale Installation, Backend, Frontend, Tests und erster Start.
- [Architektur](architecture.md): Backend, Frontend, SQLite, Repository-Struktur und Domain-Repositories.
- [Entwicklung](development.md): Branch-Regeln, Build/Test-Pflicht, Review- und Merge-Checkliste.
- [Deployment](deployment.md): lokales Hosting, PWA, Release-ZIP, Docker, systemd und Remote-Zugriff.
- [Codex-Workflow](github-codex-workflow.md): Arbeitsregeln fuer Codex/ChatGPT-Prompts und Agenten.
- [Grow-Domaene](grow-domain-notes.md): fachliche Notizen zu Zelten, Hydro-Setups, Sensoren, Measurements, Addback und Home Assistant.
- [ADR 0001](decisions/adr-0001-local-first-pwa.md): lokale PWA statt nativer App.
- [ADR 0002](decisions/adr-0002-repository-refactor.md): GrowRepository als Facade nach Repository-Refactor.

## Archiv

`docs/archive/` enthaelt alte README-Dateien, Setup-Hinweise, Sprint-Zusammenfassungen, Patch-Notizen und fruehere Agenten-Anweisungen. Diese Dateien sind als historische Referenz gedacht und nicht mehr der primaere Einstieg.
