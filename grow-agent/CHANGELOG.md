# Änderungen — Grow Berater

## 0.1.2

- Die Meldung, wenn Grow OS nicht erreichbar ist, nennt jetzt auch die nötige
  Mindestversion (2.0.0-beta.24) statt nur zu sagen, dass nichts antwortet.

## 0.1.1

- Der Berater teilt sich die Anbindung an Grow OS jetzt mit dem neuen Add-on
  **Grow MCP**. Für dich ändert sich nichts, ausser dass die gefundene Adresse
  gemerkt wird statt bei jeder Frage neu gesucht.

## 0.1.0

Erste Fassung.

- Chat-Oberfläche in der Home-Assistant-Seitenleiste, hell und dunkel.
- Findet Grow OS von selbst im internen Add-on-Netz, ohne Rechte an anderen
  Add-ons: der Name wird aus dem eigenen abgeleitet. Die Adresse lässt sich auch
  von Hand eintragen.
- Holt Lagebericht und Fachwissen bei jeder Frage frisch aus Grow OS — dieselbe
  Quelle wie die Berater-Mappe zum Herunterladen, die beiden können nicht
  auseinanderlaufen.
- Drei Anbieter zur Wahl: Anthropic, OpenAI und Ollama (läuft im eigenen Netz).
- Der Schlüssel liegt nur hier. Grow OS bleibt ohne KI und ohne Schlüssel.
