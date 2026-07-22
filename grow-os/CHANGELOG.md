# Changelog

## 1.0.47

- Improved — the **Archive page** now uses the same clean look as the rest of the app
  (big header, stat cards, list rows) instead of the old table style, and shows each
  grow's yield inline plus a total-yield figure.
- Fixed — errors on the **Grows page** are shown as a proper banner instead of a bare
  line of red text, matching how the rest of the app surfaces errors.

## 1.0.46

- New — **set the light cycle per tent**. The tent page now has a "Lichtzyklus" section
  where you enter when the light goes on and off; it shows the resulting photoperiod
  (e.g. 18/6, 12/12). This is the precondition the light-based automations ("30 min after
  lights on/off") trigger on — previously it only lived in Home Assistant, now it's
  visible and editable in Grow OS.

## 1.0.45

- New — **start a routine yourself**. The SOPs page now has a catalog of the built-in
  routines (weekly water change, system cleaning, root-rot treatment, flip to flower,
  harvest flush, …) with a "Starten" button each — so you can run an SOP whenever you
  want, not only when a risk happens to recommend one. Routines already running are
  marked "Läuft".

## 1.0.44

- Simplified — the **Diagnose page** was three overlapping cards full of internal terms
  (deviations, symptom ids, confidence levels). It's now one clear shape: "Handlungsbedarf"
  up top — what's actually wrong, each with its actions (acknowledge, resolve, start an
  SOP) — and below it a quiet, plain-language "Auffällige Werte & Tipps" list of the
  underlying readings and suggestions. Nothing lost, just far less noise.

## 1.0.43

- New — **pick which camera** for a measurement snapshot. When a tent has several cameras
  you now choose which one to snapshot from (with readable names derived from the entity,
  e.g. "Hauptzelt"), and with a single camera it shows which one it uses.
- Changed — **the harvest no longer vanishes**. Your yield now shows up in the Archive
  (dry weight and rating per grow, plus a total-yield figure), and the harvest page has a
  "Speichern & Grow abschließen" button that saves the harvest and moves the grow to the
  archive in one step — closing the grow's lifecycle instead of leaving it running.

## 1.0.42

- Fixed (real) — the **double scrollbar**, this time at the root. A global
  `overflow-x: hidden` on the body turned it into an internal vertical scroll container,
  which inside the Home Assistant ingress iframe showed up as a second scrollbar next to
  the iframe's own. Switched to `overflow-x: clip` so the document scrolls naturally —
  one scrollbar.
- New — **test your calibration reminder**. On the Notification Center, the "Kalibrierung
  fällig" card has a "Test-Erinnerung senden" button that runs the real reminder path now
  and tells you the result: it either sends the push to your phone, or explains why it
  wouldn't (no phone saved, category off, quiet hours, or nothing due). If you set up a
  daily calibration and got no reminder, this shows you why — most likely the phone was
  never saved before the fix in 1.0.40.

- Changed — the **grow pages (Automatik, Diagnose, Journal & Fotos, SOPs) now match the
  rest of the app**. They used a different, smaller header, and the grow switcher was
  broken on desktop (cut-off dropdown) and missing entirely on mobile. They now use the
  standard page header with a clean grow switcher that works on phone and desktop.

## 1.0.40

- Fixed — **double scrollbar**. Making the sidebar scrollable added a second full-size
  scrollbar next to the page's, which looked wrong and made scrolling feel broken. The
  sidebar now has a thin, subtle scrollbar so there's one clear page scrollbar again.
- Fixed — **measurement snapshots are now visible**. After "Snapshot aufnehmen" (or
  picking photos) you now see thumbnails of the attached images, each removable with an
  ×, instead of only a filename.
- Fixed — the **camera mapping on the Home Assistant page** was cramped into a half-width
  column; it now spans the full width with a clean row of actions.
- Fixed — the **Save button on the Notifications page** sat too low; page headers now
  align their action button to the top.
- Fixed — task and SOP rows on the **Aufgaben** page pointed at the old grow tabs; they
  now open the Journal / SOPs page for that grow.

## 1.0.39

- Fixed — the **sidebar now scrolls**. With every menu group expanded it could run past
  the bottom of the screen and the lowest entries were unreachable; it scrolls now.
- Fixed — on the **Notifications page your phone is actually saved**. The only Save
  button was buried at the bottom in an unrelated section, so entering a push service
  and tapping "Test" never saved it. There's now a Save button at the top, and "Test-Push"
  saves first before sending.
- Fixed — **Automatik showed "Kamera-Snapshot ins Journal" twice** (once per active
  template). It's a single switch now that applies to all active automations.

## 1.0.38

- Simplified (big) — **Automatik is now just switches**. The old page asked you to build
  configs with metric keys, aggregations, and field mappings — things a grower should
  never have to touch. It's gone. Now you pick a grow and flip on ready-made templates:
  "Messung 30 Min nach Licht AN" and "…nach Licht AUS". The measurement automatically
  captures whatever sensors you've mapped in Home Assistant — no entities to choose. Each
  active template has one extra switch, "Kamera-Snapshot ins Journal", which drops a camera
  image into that grow's photo diary on every automatic measurement.

## 1.0.37

- Changed — **lifecycle confirmations now live on the measurement page**. Confirming
  germination, rooting, or the flip to 12/12 is something you do when you check the
  plant, so those buttons moved into the measurement page's context card (shown only
  when they apply to the selected grow). The grow overview no longer carries them.
- Changed — **Harvest only shows when the grow is ready**. The Ernte action on the grow
  overview now appears only once the grow is in Flower, Finish, or Dry — otherwise it's
  hidden. Export stays on the overview.

## 1.0.36

- Fixed — removed the duplicate "Messungen" entry from the sidebar. Recording a
  measurement lives under Täglich → Messung; a grow's measurement history is still
  reachable from that grow's overview.

## 1.0.35

- Changed (big) — **features are no longer hidden inside a grow**. The six tabs that
  used to live inside a grow (Messungen, Diagnose, Journal & Fotos, SOPs,
  Automatisierung) are now their own top-level pages, each doing one thing, each with
  a grow switcher up top — so you see and use them right away without opening a grow
  first. New sidebar grouping: Täglich · Verlauf & Daten · Automatik & Regeln · Meine
  Grows · Einrichten · Wissen. Automatik is now fully editable on its own page (create
  the 30-min light preset, add configs, edit field mappings, enable/disable) instead
  of only being reachable inside a grow. Opening a grow now shows a clean overview
  only, with quick links to that grow's pages.
- Internal — Playwright end-to-end smoke tests now load every route and fail the build
  if a page crashes while rendering; CI also runs eslint and the e2e suite.

## 1.0.34

- New — **Automatik overview page**. A new "Automatik" entry under "Automatik & Regeln"
  surfaces features that used to be buried: all your auto-measurements across every grow
  (with their trigger, e.g. "30 min after lights-on") and every sensor's calibration
  interval — each with a link straight to where you edit it. Nothing hidden in a grow tab
  anymore.

## 1.0.33

- New — **daily digest push**. In the Notification Center you can enable a once-a-day
  summary at a time you choose (e.g. 5:30) — so first thing in the morning you know the
  system is up and how the values look. Pick the format: short ("all OK / N issues") or
  detailed (the key values per tent). The digest is delivered even during quiet hours,
  since you chose the time deliberately.

## 1.0.32

- New — **attach a camera snapshot to a measurement**. On the measurement page you can pick
  one of the tent's cameras and take a snapshot of the current image with one click; it's
  attached as a photo and saved with the measurement.

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
