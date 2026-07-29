# Änderungen — Grow MCP

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
