# Grow OS Dokumentation

Diese Dokumentation ist der zentrale Einstieg fuer Grow Operation System.

## Projektueberblick

Grow OS ist ein **Home-Assistant-Add-on** fuer RDWC/DWC-orientierte Grow-Workflows. Es kombiniert:

- Home Assistant Sensor- und Statusdaten (Verbindung automatisch ueber das Add-on)
- Grow-, Measurement-, Journal-, Foto- und Task-Dokumentation
- Zelte, Hydro-Setups, Hardware und Wartung
- Addback- und Changeout-Protokolle
- SOPs, Empfehlungen und RiskEvents
- Mobile Nutzung direkt in der Home-Assistant-App

## Dokumente

- [Installation](install.md): Grow OS als Home-Assistant-Add-on installieren.
- [Setup](setup.md): Quellcode bauen (Backend, Frontend, Tests) fuer die Entwicklung.
- [Architektur](architecture.md): Backend, Frontend, SQLite, Repository-Struktur und Domain-Repositories.
- [Entwicklung](development.md): Branch-Regeln, Build/Test-Pflicht, Review- und Merge-Checkliste.
- [Grow-Domaene](grow-domain-notes.md): fachliche Notizen zu Zelten, Hydro-Setups, Sensoren, Measurements, Addback und Home Assistant.
- [ADR 0001](decisions/adr-0001-local-first-pwa.md): lokale PWA statt nativer App.
- [ADR 0002](decisions/adr-0002-repository-refactor.md): GrowRepository als Facade nach Repository-Refactor.
