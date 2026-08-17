# Änderungen — Grow MCP

## 0.1.4

Bilder. Bisher konnte die eigene KI alles lesen, was Grow OS in Zahlen weiss,
aber nichts davon sehen — dabei ist gerade die Pflanze das, was man ansieht,
bevor man misst.

- `fotos` — was für einen Grow fotografiert wurde: Motiv, Datum, Bildunterschrift
  und die Messung, an der das Bild hängt.
- `foto_ansehen` — das Bild selbst, dazu ein Satz, der sagt, was darauf zu sehen
  sein soll: „Grow 4 · Motiv: Root · vor 3 Tagen aufgenommen · Notiz des
  Betreibers: ‚Wurzeln wirken braun' · gehört zur Messung 12".

Dieser Satz ist kein Beiwerk. Braune Wurzeln nach einem Wasserwechsel bedeuten
etwas anderes als braune Wurzeln in Woche sieben, und ein Modell, das nur die
Bildpunkte bekommt, kann diesen Unterschied nicht kennen. Was Grow OS über eine
Aufnahme nicht weiss, steht auch nicht in dem Satz — lieber eine kurze Notiz als
eine erfundene.

Bilder über 6 MB werden abgelehnt, statt sie zu laden und dann zu verwerfen.
Gelesen wird weiterhin ausschliesslich.

## 0.1.3

Sieben Werkzeuge mehr. Bisher war nur ein Bruchteil dessen angeschlossen, was
Grow OS ohnehin weiss — nicht aus Vorsicht, sondern weil es beim Bauen
untergegangen ist. Gelesen wird weiterhin ausschliesslich.

- `alarme` — offene Risiko-Ereignisse und die eingestellten Grenzwerte.
- `dosierungen` — die Pumpen am Zelt und das Protokoll der letzten Dosen.
- `dosier_vorschlag` — was Grow OS für eine Pumpe jetzt dosieren würde, samt
  Begründung und den Sperren, die dabei greifen. Es wird nur gerechnet.
- `ablauf_fortschritt` — welche Abläufe laufen und wie weit sie sind.
- `pflanzen` — die einzelnen Pflanzen, dazu der Pheno Hunt, falls einer läuft.
- `licht` — der eingestellte Zyklus und die tatsächlich beobachteten
  Schaltzeitpunkte. Zeigt, ob die Lampe tut, was der Plan sagt.
- `technik` — Geräte mit Zustand, anstehende Wartungen und Kalibrierungen.

Fehlt ein Teil, weil es ihn für diesen Grow gar nicht gibt — etwa kein Pheno
Hunt —, steht dieser Teil auf `null` und der Rest bleibt lesbar. Vorher hätte das
die ganze Antwort mitgerissen.

## 0.1.2

- Behoben — **`wissen_liste` war keine Übersicht.** Bei Abläufen und
  Behandlungen kam jeder Eintrag vollständig zurück, mit allen Schritten,
  Materiallisten und Quellen: elf Abläufe auf einen Schlag, rund 15.000 Tokens.
  Genau der Papierstapel, den dieses Add-on gegenüber der Mappe vermeiden soll.
  Jetzt kommen nur die Kopfdaten; den ganzen Eintrag holt `wissen_nachschlagen`.
  Symptome, Erreger und Sollwerte bleiben vollständig — sie sind kurz und haben
  keinen Einzelabruf.
- Umlaute kommen als Umlaute zurück statt als `ä`.

## 0.1.1

- Behoben — **Grow OS wurde nicht gefunden.** Der Server leitete den Namen von
  Grow OS aus seinem eigenen ab, suchte dabei aber nach dem Namen des
  Berater-Add-ons. Kam Grow OS aus dem Store, ging die Suche darum immer leer
  aus.
- Behoben — **die Seite bot den Verbindungsbefehl an, obwohl nichts erreichbar
  war.** Wer ihn ausführte, verband sich erfolgreich mit einem Server, bei dem
  danach jede Frage ins Leere lief. Jetzt steht dort erst eine Prüfliste.
- Die Meldung nennt jetzt auch, dass Grow OS mindestens **2.0.0-beta.24** sein
  muss — ältere Fassungen lassen andere Add-ons nicht mitlesen.

## 0.1.0

Erste Fassung.

- Elf lesende Werkzeuge auf Grow OS: Grows, Lagebericht, Messwert-Verlauf,
  Trends, Abweichungen, Anlage, Sorte, Journal, Fachwissen und Volltextsuche.
- Der Verlauf ist der eigentliche Grund für dieses Add-on. Die Berater-Mappe zum
  Herunterladen hält den Stand von jetzt fest; hier fragt das Modell selbst nach,
  wie sich ein Wert über Tage bewegt hat.
- Einrichtungsseite in der Seitenleiste mit fertigem Verbindungsbefehl. Der
  Schlüssel wird beim ersten Start erzeugt und muss nicht abgeschrieben werden.
- Zwei getrennte Türen: die Seite mit dem Schlüssel hängt am Ingress, die
  Schnittstelle am Netz-Port. Über das Netz ist die Seite nicht erreichbar.
- Kein Werkzeug schreibt. Dosieren und Schalten bleiben in Grow OS.
