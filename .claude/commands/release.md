---
description: Neue Beta-Version ausliefern — CI, Image, GHCR, dann erst hochzaehlen
---

Liefere eine neue Version aus. **Die Reihenfolge ist die Sache**, nicht die
einzelnen Befehle: der Docker-Bau führt keine Tests aus, ein Image aus rotem
Quelltext sieht genauso aus wie eines aus grünem.

## Schritt 0 — nachsehen, wo wir stehen

```
cat grow-os/config.yaml | head -2
git log --oneline -1 -- grow-os/config.yaml
git log --oneline <letzter-release-commit>..HEAD
```

Die neue Nummer ist die alte plus eins. **Nicht raten** — lesen.

## Schritt 1 — CI muss grün sein, VORHER

```
gh run list --workflow=ci.yml --limit 3
```

`ci.yml` hat keinen `workflow_dispatch`-Auslöser; sie läuft am Push. Ist der
letzte Lauf rot oder gehört er nicht zum aktuellen `HEAD` — **hier anhalten**
und die Ursache beheben. Am 19.08.2026 sind zwei Commits bei rotem CI gelandet,
weil lokal grün gemeldet und CI nie angesehen wurde.

```
gh run watch <id> --exit-status
```

## Schritt 2 — Image bauen

```
gh workflow run docker-publish.yml -f version=<X>
gh run watch <id> --exit-status
```

## Schritt 3 — das Manifest selbst ansehen

Dem Workflow glauben reicht nicht. Anfragen und die Architekturen lesen:

```
TOKEN=$(curl -s "https://ghcr.io/token?scope=repository:nerdstreak/grow-operation-system:pull" | python -c "import json,sys;print(json.load(sys.stdin)['token'])")
curl -s -o /dev/null -w "HTTP %{http_code}\n" -H "Authorization: Bearer $TOKEN" \
  -H "Accept: application/vnd.oci.image.index.v1+json" \
  "https://ghcr.io/v2/nerdstreak/grow-operation-system/manifests/<X>"
```

HTTP 200 und amd64 · arm64 · arm — sonst **anhalten**.

## Schritt 4 — erst jetzt hochzählen

`grow-os/config.yaml` auf die neue Nummer, `grow-os/CHANGELOG.md` um einen
Abschnitt ergänzen.

**Der Changelog ist auf DEUTSCH.** Bis beta.58 war er englisch — mit dem
Gedanken, er richte sich an Fremde. Der Nutzer hat das am 28.08.2026
widerrufen: „einmal müssen die Release Notes auf Deutsch sein, weil das unsere
Hauptsprache ist." Home Assistant zeigt genau diesen Text beim Update an, und
wer aktualisiert, ist kein Fremder.

Die Einträge vor beta.58 bleiben englisch; sie sind Geschichte. Gehalten wird
das von `src/release-notes-deutsch.node.test.ts` — die Prüfung liest den
NEUESTEN Eintrag und meldet englische Wendungen.

Schreibe darin, **was der Nutzer merkt**, nicht was im Code steht. Je Punkt:
was war, warum es passierte, und woran man es misst. Keine Aufzählung ohne
Substanz.

```
git commit -m "Release <X>" && git push
```

Nenne im Commit die Lauf-Nummern von CI und Image-Bau — dann ist die
Reihenfolge belegt und nicht behauptet.
