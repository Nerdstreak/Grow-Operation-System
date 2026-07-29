# Grow Berater — Home Assistant Add-on

Ein Fachmann für RDWC/DWC, der deine Anlage kennt. Der Berater liest bei jeder
Frage den aktuellen Stand und das Fachwissen aus **Grow OS** und antwortet auf
dieser Grundlage — nicht aus dem Bauch heraus.

Grow OS selbst bleibt ohne KI und ohne Schlüssel. Wer den Berater nicht will,
installiert dieses Add-on einfach nicht.

## Voraussetzungen

- **Grow OS** ist installiert und läuft.
- Ein Zugang zu einem Sprachmodell. Zur Wahl stehen:
  - **Anthropic** (Claude) — Schlüssel unter <https://console.anthropic.com>
  - **OpenAI** — Schlüssel unter <https://platform.openai.com>
  - **Ollama** — läuft auf deinem eigenen Rechner, kostet nichts, braucht keinen
    Schlüssel. Dafür trägst du unter *Adresse* ein, wo Ollama erreichbar ist,
    z. B. `http://192.168.1.10:11434`.

## Installation

1. **Einstellungen → Add-ons → Add-on-Store**.
2. Oben rechts **⋮ → Repositories**, eintragen:
   `https://github.com/Nerdstreak/Grow-Operation-System`
3. **Grow Berater** installieren.
4. Unter **Konfiguration** den Anbieter wählen und den Schlüssel eintragen.
5. **Starten**, dann auf der Info-Seite **„Im Seitenleisten-Menü anzeigen"**
   einschalten.

## Einstellungen

| Feld | Bedeutung |
| --- | --- |
| `anbieter` | `anthropic`, `openai` oder `ollama` |
| `modell` | Der Modellname beim Anbieter, z. B. `claude-opus-5` |
| `schluessel` | Dein Zugangsschlüssel. Bei Ollama leer lassen. |
| `adresse` | Nur für Ollama oder einen eigenen Dienst nötig |
| `grow_os_adresse` | Nur nötig, falls der Berater Grow OS nicht selbst findet |

### Wenn Grow OS nicht gefunden wird

Normalerweise findet der Berater Grow OS von selbst, solange beide aus diesem
Repository stammen. Hast du Grow OS anders installiert, steht sein Name in Home
Assistant unter **Grow OS → Info → Hostname**. Trag ihn mit Port ein, zum
Beispiel:

```
http://a1b2c3d4-grow-os:5076
```

## Wo bleiben meine Daten?

- Der Schlüssel liegt in den Einstellungen dieses Add-ons und verlässt Home
  Assistant nur Richtung deines Anbieters.
- An das Modell gehen: deine Frage, der Lagebericht deines Grows (Werte,
  Journal, Warnungen) und das Fachwissen aus Grow OS. Keine Fotos, keine
  Zugangsdaten.
- Mit **Ollama** verlässt gar nichts dein Netzwerk.
- Der Berater liest nur aus Grow OS — er kann dort nichts ändern und keine
  Pumpe schalten.
- Von Home Assistant fragt er genau eines ab: seinen eigenen Add-on-Namen.
  Daraus leitet er ab, wo Grow OS liegt. Rechte an anderen Add-ons hat er nicht.

## Grenzen

Das Modell antwortet auf Grundlage des mitgelieferten Wissens. Es kann trotzdem
irren. Bei allem, was Pflanzen oder Technik beschädigen kann — Dosierungen,
Pumpen, Zeitpläne — bleibt die Entscheidung bei dir.
