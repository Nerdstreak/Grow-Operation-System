# Grow OS installieren (fuer Nutzer)

Diese Anleitung ist fuer den normalen Betrieb gedacht - **keine Programmierkenntnisse noetig**.
Du brauchst nur einmal einen Befehl zu kopieren. Danach laeuft Grow OS dauerhaft und
startet nach jedem Neustart automatisch.

> Entwickler, die den Quellcode bauen wollen, finden alles unter [setup.md](setup.md).

## So sieht Grow OS aus

<p align="center">
  <img src="images/live-dashboard-desktop.png" alt="Live-Dashboard am PC" width="100%">
</p>
<p align="center">
  <img src="images/live-dashboard-mobile.png" alt="Live-Ansicht am Handy (PWA)" width="260">
</p>

---

## Variante A: Home Assistant Add-on (empfohlen)

Der einfachste Weg. Grow OS bezieht seine Sensordaten aus Home Assistant, deshalb
laeuft es am rundesten direkt **als Add-on** in Home Assistant: ein Klick, keine URL
und kein Token noetig - die Verbindung entsteht automatisch.

**Voraussetzung:** Home Assistant OS oder Supervised (bei Home Assistant
Container/Core gibt es keine Add-ons).

### Schritt 1 - Repository hinzufuegen
In Home Assistant: **Einstellungen -> Add-ons -> Add-on-Store**, oben rechts
**... -> Repositories**, und diese Adresse einfuegen:

```
https://github.com/Nerdstreak/Grow-Operation-System
```

### Schritt 2 - Installieren und starten
**Grow OS** erscheint im Store -> **Installieren** (der erste Aufbau dauert ein paar
Minuten) -> **Starten**.

### Schritt 3 - Oeffnen
Grow OS erscheint in der **Seitenleiste** von Home Assistant (Symbol 🌱). Es ist
sofort verbunden - deine Sensoren waehlst du beim Zelt-Mapping bequem aus einem
Dropdown (kein Abtippen von Entity-IDs noetig).

> Home Assistant verwaltet Updates und sichert die Grow-OS-Daten in seinen eigenen
> Backups automatisch mit.

---

## Variante B: Raspberry Pi (eigenstaendig)

Fuer den Betrieb neben einer bestehenden Home-Assistant-Instanz.
Ideal: Raspberry Pi 4 oder 5 mit **Raspberry Pi OS (64-bit)**. Ein Pi 3 funktioniert auch.

### Schritt 1 - Mit dem Pi verbinden
Oeffne ein Terminal auf dem Pi (Menue -> Zubehoer -> Terminal) **oder** verbinde dich
per SSH von einem anderen Rechner (`ssh pi@<pi-ip-adresse>`).

### Schritt 2 - Diesen einen Befehl einfuegen und Enter druecken

```bash
curl -fsSL https://raw.githubusercontent.com/Nerdstreak/Grow-Operation-System/main/scripts/install.sh | bash
```

Das Skript erledigt alles automatisch:
- installiert Docker (falls noch nicht vorhanden),
- laedt das fertige Grow-OS-Paket herunter,
- startet es als Dienst (laeuft nach jedem Neustart von allein weiter).

Beim allerersten Mal kann das ein paar Minuten dauern. Am Ende zeigt das Skript die
Adresse an, unter der Grow OS erreichbar ist.

### Schritt 3 - Im Browser oeffnen
Auf einem beliebigen Geraet im selben Netzwerk (Handy, Laptop):

```
http://<pi-ip-adresse>:5076
```

Die genaue Adresse steht am Ende der Installation. Fertig! 🌱

> **Hinweis:** Wurde Docker frisch installiert, einmal den Pi neu starten
> (`sudo reboot`) - danach laeuft alles ohne Zutun weiter.

### Spaeter aktualisieren
Einfach denselben Befehl aus Schritt 2 noch einmal ausfuehren - er holt automatisch
die neueste Version.

---

## Variante C: Windows-PC (eigenstaendig)

### Schritt 1 - PowerShell oeffnen
Start-Menue -> "PowerShell" eintippen -> **Windows PowerShell** anklicken.

### Schritt 2 - Diesen einen Befehl einfuegen und Enter druecken

```powershell
irm https://raw.githubusercontent.com/Nerdstreak/Grow-Operation-System/main/scripts/install.ps1 | iex
```

Das Skript installiert die noetige .NET-Runtime, laedt Grow OS herunter, legt eine
Desktop-Verknuepfung an und richtet den Autostart ein. Der Browser oeffnet sich danach
automatisch.

### Schritt 3 - Nutzen
Grow OS laeuft unter `http://localhost:5076` und startet kuenftig automatisch mit
Windows. Zum manuellen Start gibt es die Verknuepfung **"Grow OS"** auf dem Desktop.

---

## Home Assistant verbinden (nur bei Variante B/C)

Beim **Add-on (Variante A)** entsteht die Verbindung automatisch - dieser Schritt
entfaellt komplett. Fuer die eigenstaendigen Varianten richtest du sie einmalig ein:

1. In Grow OS oben auf **Einstellungen** gehen.
2. **Home Assistant Base URL** eintragen, z. B. `http://homeassistant.local:8123`.
3. In Home Assistant einen **Long-Lived Access Token** erstellen
   (Profil -> Sicherheit -> Long-Lived Access Tokens) und in Grow OS einfuegen.
4. Zelte anlegen und Sensoren zuordnen.

Wichtig: Der Rechner/Pi mit Grow OS muss Home Assistant im Netzwerk erreichen koennen.
Grow OS bezieht praktisch alle Live-Werte aus Home Assistant - ohne diese Verbindung
bleiben Dashboard und Auswertungen leer.

---

## Zugriff vom Handy unterwegs

Standardmaessig laeuft Grow OS nur im eigenen Heimnetz. Fuer den Zugriff von unterwegs
empfiehlt sich ein VPN wie **Tailscale**. Details und der eingebaute Admin-Key-Schutz
stehen unter [deployment.md](deployment.md).

---

## Probleme?

- **Seite laedt nicht?** Pruefe, ob du im selben WLAN/Netzwerk bist wie der Pi/PC, und
  ob die IP-Adresse stimmt.
- **Add-on:** Logs im Add-on-Tab **Protokoll** ansehen; nach Aenderungen **Neu starten**.
  Dropdown leer? Dann pruefe, ob deine Sensoren in Home Assistant sichtbar sind.
- **Raspberry Pi:** Logs ansehen mit `cd ~/grow-os && docker compose logs -f`.
- **Neustart der App (Pi):** `cd ~/grow-os && docker compose restart`.
- Fragen oder Fehler bitte als [GitHub Issue](https://github.com/Nerdstreak/Grow-Operation-System/issues) melden.
