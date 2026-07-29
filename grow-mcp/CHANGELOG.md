# Änderungen — Grow MCP

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
- Der Verlauf ist der eigentliche Grund für dieses Add-on. Der **Grow Berater**
  bekommt einen Textblock mit dem Stand von jetzt; hier fragt das Modell selbst
  nach, wie sich ein Wert über Tage bewegt hat.
- Einrichtungsseite in der Seitenleiste mit fertigem Verbindungsbefehl. Der
  Schlüssel wird beim ersten Start erzeugt und muss nicht abgeschrieben werden.
- Zwei getrennte Türen: die Seite mit dem Schlüssel hängt am Ingress, die
  Schnittstelle am Netz-Port. Über das Netz ist die Seite nicht erreichbar.
- Kein Werkzeug schreibt. Dosieren und Schalten bleiben in Grow OS.
