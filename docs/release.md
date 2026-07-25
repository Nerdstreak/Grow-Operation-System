# Release-Ablauf

**Die Reihenfolge ist der ganze Punkt dieses Dokuments.**

Home Assistant liest `grow-os/config.yaml` direkt aus dem Repository. In dem Moment, in dem
ein Versions-Bump auf `main` landet, bietet jede Installation das Update an — unabhängig
davon, ob das Image überhaupt existiert. Wer in diesem Fenster klickt, bekommt:

```
Could not pull image to update app …: [404] failed to resolve reference
"ghcr.io/nerdstreak/grow-operation-system:1.8.2": not found
```

Am 2026-07-26 ist genau das passiert: `config.yaml` um 00:59:23 gepusht, das Image erst um
01:04:50 fertig. Fünfeinhalb Minuten Lücke, drei fehlgeschlagene Versuche beim Nutzer.

Ironie am Rand: Das Fenster war vorher kleiner. Es wurde größer, als nach dem 1.6.1-Vorfall
die Regel „erst CI abwarten, dann Image bauen" eingeführt wurde — die Korrektur für ein
Problem hat ein anderes vergrößert. Deshalb steht hier jetzt der ganze Ablauf.

## Ablauf

**1. Code committen — ohne Versionsnummer.**
Änderungen, Tests, Changelog-Eintrag vorbereiten, aber `config.yaml` noch nicht anfassen.

```bash
git push origin main
```

**2. CI abwarten, bis sie grün ist.**
Der Docker-Workflow führt keine Tests aus; ein grüner Bildbau sagt nichts über Korrektheit.

```bash
gh run watch "$(gh run list --workflow=ci.yml --limit 1 --json databaseId -q '.[0].databaseId')" --exit-status
```

**3. Image mit der neuen Version bauen — Version explizit übergeben.**
Der Workflow nimmt die Nummer als Eingabe entgegen, statt sie aus `config.yaml` zu lesen.
Das Image ist inhaltlich identisch: Weder `config.yaml` noch die Versionsnummer landen
darin.

```bash
gh workflow run docker-publish.yml --ref main -f version=1.8.3
```

**4. Prüfen, dass das Image wirklich abrufbar ist.**

```bash
TOKEN=$(curl -s "https://ghcr.io/token?scope=repository:nerdstreak/grow-operation-system:pull" | sed -n 's/.*"token":"\([^"]*\)".*/\1/p')
curl -s -o /dev/null -w "%{http_code}\n" -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/vnd.oci.image.index.v1+json" \
  "https://ghcr.io/v2/nerdstreak/grow-operation-system/manifests/1.8.3"
```

Erwartet: `200`.

**5. Erst jetzt die Version veröffentlichen.**
`grow-os/config.yaml` und `CHANGELOG.md` bumpen, committen, pushen. In dem Moment sehen die
Nutzer das Update — und das Image liegt bereits bereit.

## Was wann eine Versionsnummer bekommt

- **Patch** (1.8.2 → 1.8.3): Fehlerbehebungen, Textänderungen, Wissenseinträge
- **Minor** (1.8.x → 1.9.0): neue Funktionen
- Keine neun Releases an einem Tag. Änderungen sammeln.

## Offener Punkt

Der Supervisor warnt bei jeder Prüfung:

```
App config 'arch' uses deprecated values ['armv7'].
Please report this to the maintainer of Grow OS
```

`armv7` in `grow-os/config.yaml` ist abgekündigt. Das Entfernen würde 32-Bit-Installationen
ausschließen — eine Produktentscheidung, keine reine Aufräumarbeit, deshalb noch offen.
