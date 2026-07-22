# Changelog

## 1.0.31

- New — **multiple cameras per tent**. Map several camera entities to a tent (e.g. one per
  plant) on the Home Assistant page, then switch between them right on the live dashboard
  with the ‹ 1/3 › control on the camera view. Existing single-camera setups keep working
  unchanged.

## 1.0.30

- New — a new measurement is now **pre-filled from Home Assistant**. When you open the
  measurement page, the mapped sensor values (pH, EC, water temp, DO, ORP, level, climate)
  are filled in automatically from the tent's live values — you only correct what you need.
  An "Aus Home Assistant übernehmen" button re-pulls the current values on demand.

## 1.0.29

- Fixed — long pages (e.g. the observation section on the measurement page) were cut off
  in Firefox and only scrolled when you click-dragged. A global `overflow-x: hidden` was
  turning into an implicit vertical scroll container ("window in window"); switched it to
  `overflow-x: clip` so the page scrolls normally.

## 1.0.28

- New — the sidebar is reorganised into **collapsible groups** by what you want to do:
  Täglich · Meine Grows · Automatik & Regeln · Einrichten · Lernen & System. Each group
  opens and closes and remembers its state, and the group of the current page always stays
  open — so you can slim the menu down to just what you use. Grenzwerte and Benachrichtigungen
  now live together under "Automatik & Regeln".

## 1.0.27

- Refined — you can no longer manually create a "fixed sensor": those appear
  automatically from the Home Assistant mapping, so the add form only offers handheld
  meter and equipment. When editing a synced sensor, its kind is shown read-only. Also
  shortened the "Art" helper text.

## 1.0.26

- New — hardware now has an explicit **device kind**: fixed sensor (HA-mapped, live
  values), handheld meter (e.g. a BlueLab pen — calibrated, never mapped), or equipment
  (pump, chiller, UPS — maintenance only). Pick it in the hardware form; the kind shows
  on each card.
- Fixed — handheld meters and equipment no longer get a bogus "Mapping prüfen" warning
  on the Aufgaben page. Mapping warnings only apply to fixed sensors without an entity,
  and the HA card no longer warns when your setup simply has no fixed sensors.

## 1.0.25

- Improved — clickable elements now read as clickable on **every** page, consistently:
  a global pointer-cursor rule for all buttons/tabs/switches (browsers don't do this by
  default), plus matching green hover highlights for buttons, tabs, switches, tent chips
  and clickable risk rows — the same affordance the sidebar navigation got earlier.

## 1.0.24

- Fixed — the clock in the live dashboard's LIVE chip now ticks in real time. It used to
  show the last data-refresh timestamp (moving only every 30 seconds), which looked like a
  hanging clock. If the data itself ever goes stale (e.g. Home Assistant briefly down),
  the chip now says so explicitly ("Daten vor X min").

## 1.0.23

- New — **mapped entities become sensors automatically**. Map an entity on the Home
  Assistant page (e.g. your pH probe) and it appears under **Sensoren** as tracked
  hardware — with a sensible calibration interval per type (pH 14 days, EC/ORP/DO 30)
  and the calibration cycle armed, so the calibration push reminder works from day one.
  Your edits (name, interval) survive re-saving the mapping; unmapping keeps the item.
- New — **water level in liters or centimeters**: two separate mapping slots
  ("Wasserstand (Liter)" and "Wasserstand (cm)"), correct units on the live dashboard
  and threshold alerts for both.
- Fixed — the Settings page no longer claims "HA aus · keine URL" when running as an
  add-on; it now shows "aktiv · Über Add-on".

## 1.0.22

- Fixed — a threshold breach that starts during quiet hours (or while Home Assistant is
  briefly unreachable) is no longer silently swallowed: Grow OS now retries and delivers
  the push as soon as sending is possible again.
- Internal — major test expansion: Home Assistant HTTP behavior is now covered with faked
  HA responses (entity parsing, supervisor URL, circuit breaker, notify payloads) plus
  full-loop alert/notification behavior tests (548 tests total).

## 1.0.21

- New — **Notification Center** (Benachrichtigungen): one place to pick your phone once, set
  quiet hours, and choose what Grow OS pushes you. All notifications now share this single
  device and quiet-hours setting.
- New — **calibration-due push**: a daily reminder when a sensor calibration is due.
- New — **sensor-offline push**: get notified when a mapped sensor stops reporting values
  (and again when it recovers), with a short delay so a brief hiccup doesn't false-alarm.
- The threshold page is now called **Grenzwerte** and only sets min/max per sensor — the
  phone and categories moved to the Notification Center. (Set your phone there once.)

## 1.0.20

- Simplified — removed the confusing "HA Entity" field from the Sensors (hardware) form. It
  never actually connected anything: live values come only from the per-tent mapping on the
  Home Assistant page. Sensors are now purely physical inventory (name, type, tent,
  calibration, maintenance); entities are mapped in exactly one place — the Home Assistant tab.

## 1.0.19

- Fixed (major) — mapped RDWC/DWC reservoir sensors (pH, EC, water temp, ORP, DO, water
  level) now show their live values on the dashboard as soon as they are mapped, even
  before the grow has any measurements. Previously the reservoir tiles stayed blank ("—")
  unless the grow was recognized as active-hydro or a manual measurement already existed.
- Clearer wording on the Sensors page: the mapping hint no longer reads "HA getrennt"
  (which looked like a lost connection) — it now explains that entities are mapped under
  the Home Assistant tab, and that the add-on connection itself is always active.

## 1.0.18

- Improved — the sidebar navigation now reads clearly as clickable: a pointer cursor on
  hover, a distinct green hover highlight with an outline, and brighter idle text so the
  menu items no longer look like static labels.

## 1.0.17

- Fixed — the Home Assistant page no longer shows the connection as "inactive" when
  running as an add-on. As an add-on the connection is automatic (Supervisor token), so
  the status card now reads "active · via add-on".

## 1.0.16

- New — threshold alerts with push notifications. Under **Alarme** you can set a min/max
  per sensor (pH, EC, water temp, ORP, DO, air temp, humidity, VPD, CO₂). Grow OS sends a
  push to your phone through Home Assistant when a value goes out of range — pick your HA
  notify service from a dropdown and send a test push. Edge-triggered with a cooldown so
  you are not spammed.
- Fixed — the Reservoir section on the live dashboard now shows your reservoir sensor
  values as soon as they are mapped (RDWC/DWC group), even before a grow is running, with
  a hint that grow-specific targets and addback need a DWC/RDWC grow.

## 1.0.15

- Fixed: live sensor values could suddenly blank out (showing "—") and only came
  back after leaving and reopening the dashboard. A transient connection hiccup on
  the 30-second background refresh was wiping the values; the dashboard now keeps
  the last good readings instead of clearing them.

## 1.0.14

- Launch cleanup. Removed the unused in-app remote-access / admin-key settings —
  as a Home Assistant add-on, Home Assistant already handles authentication and
  remote access (web and mobile app), so no separate key is needed.
- Removed dead offline/PWA plumbing that never activated behind the ingress.
- Internal only: no action required, and your data on `/data` is preserved across
  the update as usual.
