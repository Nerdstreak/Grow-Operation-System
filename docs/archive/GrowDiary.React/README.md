# GrowDiary React

React-Frontend fuer das Grow Operation System. Die App laeuft parallel zum bestehenden ASP.NET-Core-Backend und nutzt ausschliesslich die JSON-Endpoints unter `/api/*`.

## Start

```bash
npm install
npm run dev
```

Frontend lokal: `http://127.0.0.1:5173`

Das Backend muss parallel auf `http://127.0.0.1:5076` laufen. Der Vite-Dev-Server proxyt `/api/*` automatisch dorthin.

## Relevante Screens

- `Dashboard`: aktive und archivierte Grows mit Suche und Statusueberblick
- `Grow-Detail`: Verlauf, offene Tasks, Journal und Quick Actions
- `Setup`: Home Assistant und Zelt-Mapping

## Qualitaet

```bash
npm run lint
npm run build
```

Playwright-Smoke-Test wurde lokal gegen Desktop und Mobile ausgefuehrt.
