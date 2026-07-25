# Changelog

## 1.8.4

- New — **VPD is finally checked against its target.** A target band per stage has been in
  the reference data all along, and nothing read it: the value was calculated, charted and
  put on tiles, but never compared to anything. Grow OS now says when the leaf VPD leaves
  its band — and when it is *below* it, says why that matters here: RDWC transpires two to
  two and a half times as much as soil and wants the upper end, so a low VPD is holding the
  plant back rather than protecting it. Check the airflow at leaf level before reaching for
  temperature or humidity.

## 1.8.3

- Fixed — **the add-on log was 99 % noise.** Every call to Home Assistant produced four lines,
  once a minute, around the clock — 4,968 of 5,000 lines in a real log, with the 32 lines
  that meant something buried underneath and a Raspberry Pi writing the rest to its SD card
  all day. Warnings and errors still show; the chatter is gone.
- Fixed — **an update could be offered before its image existed.** Home Assistant reads the
  version straight from the repository, so it announced a new release the moment that file
  changed, while the image was still building — a few minutes in which pressing Update gave
  a bare "unknown error". The image is now published and verified first, and only then is
  the version announced.

## 1.8.2

- New — seven rules recovered from the workshop material, which turned out to be almost
  entirely graphics. Among them: **RDWC transpires two to two-and-a-half times as much as
  soil** and therefore wants slightly *higher* VPD than the usual recommendation, not lower;
  **airflow at leaf level** belongs to VPD (90–120 m/min for RDWC, about 10–15 % more than
  other systems) because it breaks the moist layer on the leaf; the canopy runs a gradient of
  roughly 26 / 24.5 / 23 °C top to bottom, so where the sensor hangs decides the number; and
  biofilm is where every RDWC problem starts — rising oxygen consumption with falling ORP is
  the early sign, long before anything shows on the roots.

## 1.8.1

- Changed — **a new tent now computes leaf VPD, not air VPD.** The leaf sits about 2 °C below
  air temperature, and every RDWC recommendation is drawn for that number; the offset used to
  default to 0, so a fresh tent quietly showed a different figure than the charts mean.
  Existing tents keep whatever you set — 0 might have been deliberate.
- New — **a reminder that ORP is a consumable.** It has to be brought back up with HOCl every
  two to three days because it decays while doing its job, and the day it is forgotten
  nothing else looks wrong. Grow OS now says so once it has been more than three days —
  and stays quiet for anyone who doesn't track ORP at all.
- New — four more rules from the workshop material, including one worth knowing: RDWC
  transpires differently from soil, so a VPD table from soil growing does not transfer.

## 1.8.0

- New — **the nutrient solution is diagnosed as a pattern, not a value.** SOP-N1 lays out a
  table and asks you to read five signals together, because a falling pH with stable EC and
  good oxygen is a plant feeding, while the *same* falling pH with rising EC and low oxygen
  is biofilm. Grow OS checked each of those separately and could never reach that
  conclusion. The Diagnose page now shows the whole table at once — and the two rows no
  sensor covers, the look and the smell of the water, are listed as checks for you rather
  than quietly dropped.
- Changed — **cuttings quarantine now follows SOP-C1**: the three-bath method as three
  separate baths per cutting, the substrate carrier handled properly (rockwool, EasyPlug,
  Jiffy — or none at all, in which case the whole section is skipped), the choice between
  HOCl and H₂O₂, the drying phase, and the release criteria.
- New — **an addback procedure**, which was missing entirely: fill to 90 %, add one
  component at a time in mixing order, stir between each, never more than 500 ml of any one
  part per container, and never straight into the control bucket.
- Changed — a step can now depend on more than one answer. Decontaminating a substrate plug
  needs both the agent and there being a plug; with a single condition, bare-root cuttings
  were told to dip something they don't have.

## 1.7.3

- New — **starting a routine now asks what it needs to know.** Root-rot treatment wants to
  know how badly the plants are affected and how many there are, and it asks before you
  start rather than half-way through — finding out mid-treatment that a different path
  applied is the thing a written procedure exists to prevent.
- Changed — the steps you get are the steps that apply. A lightly affected plant skips the
  root cut and gets the short rinse; a badly affected one gets the cut and the long one. And
  the treatment is laid out **one plant at a time**: plant 1 goes from lifting out all the
  way to the quarantine container, including disinfecting the shears, before plant 2 is
  touched at all. That order is the whole point — it is what stops the pathogen crossing to
  the next plant.

## 1.7.2

- New — **procedures can branch and repeat.** The source SOPs are not flat lists: root-rot
  treatment handles a badly affected plant differently from a healthy one, and the block that
  matters most — rinse, then disinfect the shears and the surface — runs once per plant,
  because that disinfection is what stops the pathogen travelling. Steps can now carry a
  condition and repeat per plant, so the app can follow the document instead of summarising it.
- Changed — **root-rot treatment now follows SOP-S1 step by step**: eighteen steps instead of
  fourteen, split into the passive path (no cutting, 1–2 minutes in the second bath) and the
  active one (cut first, then 180 seconds), with the ORP levels the SOP specifies for each
  bath, the spray bottle and the refilled system.
- Fixed — that procedure claimed root rot was confirmed below **4 mg/L** of dissolved oxygen.
  SOP-S1 says **6 mg/L**.
- New — six rules from the RDWC Procedure, including two that are easy to get backwards:
  **ORP shock looks exactly like a nutrient deficiency** (yellowing, dry foliage — feeding it
  makes it worse), and **the smell tells you which way you're wrong** — putrid means anaerobic,
  fresh bean sprouts means healthy, chlorine means over-oxidised.

## 1.7.1

Checking the code against the source SOPs — rather than only against the knowledge files —
turned up four places where Grow OS contradicted the documents it is built on.

- New — **pH drift is now judged by speed, not just by position.** SOP-N1 separates a normal
  swing (0.1-0.4 a day, the plant feeding) from a real drift (0.5 or more within 12-24 h,
  which points at instability, biofilm or precipitation). Grow OS only ever looked at the
  absolute value, so a jump from 5.8 to 6.3 overnight — which never leaves the target band —
  went unmentioned. It is now reported with the SOP's own list of immediate checks.
- Fixed — **dissolved oxygen was flagged too late.** SOP-N1 calls for action below
  6.5 mg/L; Grow OS stayed silent until 6.0. That is exactly the range where root rot starts
  while nothing looks wrong. Below 6.0 now counts as confirmed, per SOP-S1.
- Fixed — **flushing was reported as a mistake.** The growplan ends at EC 0.4, but the Finish
  setpoint said 1.1-1.6 — the peak of flower. Anyone following the plan down was told their
  value was out of range. Finish is now 0.4-1.1.
- Fixed — two numbers for the DWC multiplier (1.3 vs 1.35) and a third hard-coded copy of the
  pH thresholds. Both now come from one place.
- New — eight rules from the SOPs added to the knowledge base, each citing its document and
  section, so every recommendation can be traced back to where it is written down.

## 1.7.0

- New — **the watchdog now notices slow failures.** It used to spot only that monitoring
  itself had stopped. It now also reports a value drifting the same direction day after day
  (even while it stays inside its band), consumption collapsing — the plant telling you it
  stopped drinking — consumption doubling, which usually means a leak, and a water change
  that never happened. Each finding names the growplan rule behind it. One message per
  finding, not one per check, and a restart doesn't replay what was already reported.
  Deterministic: no model, no API key, works on every install.
- New — **search.** One box in the sidebar, Ctrl+K from anywhere, and in the "Mehr" panel on
  a phone. It finds pages, grows, tents, systems, strains, SOPs and knowledge entries — and
  it knows the words you'd actually reach for: "kamera" finds Zelte, "mangel" finds Diagnose.
- Fixed — **things were hidden.** Eight of twenty-three destinations sat in groups that were
  collapsed on first visit, including everything a new install needs: Zelte, Hydro, Sensoren,
  Home Assistant. And Einstellungen lived under a group called "Wissen". Regrouped.
- New — **cameras on the dashboard** (from 1.6.1): a tent with three of them shows all three
  at once, tiles carry a width, and sections can be reordered by drag or by arrow buttons —
  dragging does nothing on a touchscreen.
- New — groundwork for an optional AI assistant: connect Claude, OpenAI or a local model,
  see exactly what would be sent before anything is, and have every claim checked against
  the documents it cites. Nothing is switched on unless you set it up, and the app is fully
  usable without it.

## 1.6.2

- Fixed — some knowledge entries in 1.6.1 linked to source PDFs that aren't part of the
  image, so the link led nowhere. The source is still named — document title and section —
  it just isn't a link any more where the document isn't shipped.

## 1.6.1

- New — **cameras on the dashboard**. A camera can now be a tile, so a tent with three of
  them shows all three at once instead of paging through one at a time. Tiles carry a width
  as well (− / +), and cameras start two columns wide. The separate camera panel steps aside
  once you've placed your own camera tiles.
- New — **sections can be moved**. Drag a section, or use ↑ ↓ — dragging does nothing on a
  touchscreen, and the dashboard is mostly read on a phone.
- Fixed — asking the camera proxy for an entity that isn't assigned to the tent used to
  quietly serve the tent's *first* camera instead. It looked like a working feature while
  showing the wrong tent; it now says the camera isn't assigned.
- Changed — **pH is no longer nagged over normal drift**. The growplan says to let pH swing
  freely between 5.8 and 6.2 and only correct below 5.5 or above 6.5. Grow OS did the
  opposite: it warned as soon as the value left the narrow per-stage band and suggested
  pH-Down. Your mixing target is still shown as a hint.
- New — **a warning when the light is too strong for the CO₂ you have**. The PPFD targets
  assume CO₂ enrichment. Without it, 800–900 is the ceiling, so above 900 PPFD with CO₂
  under 800 ppm Grow OS now says so — reduce in steps of 50, keep 30 cm to the tips.
- Fixed — ORP targets sat at 300–400 for every stage. They now follow the plan: 400–450 in
  flower, 450–500 in finish.
- Fixed — **knowledge updates never reached existing installations.** The reference data was
  copied once on first start and then left alone forever, so corrections like the ones above
  only ever reached new installs. Grow OS now compares against what it last shipped: its own
  files are refreshed, anything you edited yourself is left alone. Files older than this
  mechanism are backed up next to the original as `*.user-backup` before being refreshed.

## 1.6.0

- New — **build your own live dashboard**. "Dashboard anpassen" on the live screen turns the
  value sections into something you arrange: drag tiles to reorder them or move them between
  sections, remove what you don't need, rename a section or create new ones — saved per tent.
  "Auf Standard zurücksetzen" always brings the built-in arrangement back.
- New — **your own sensors on the dashboard**. Any Home Assistant entity can become a tile,
  including ones Grow OS knows nothing about — a UV clarifier, a pump, a switch. Non-numeric
  states (on/off) are shown as they are. Give it your own caption and unit if you like.

## 1.5.0

- New — **Pheno Hunt**. Compare the siblings from one batch of seed and pick the keeper.
  Each plant gets a score sheet that fills up as the run goes: structure and vigour while
  growing, which training it got (LST, topping, supercropping, lollipopping …) and how it
  took it, stress and pest resilience, then flowering days, stretch, yield, bud density and
  resin at harvest, and finally aroma, flavour, effect and THC after the cure. Everything
  optional — an unrated trait simply doesn't count.
- New — **the ranking follows your goals**. You set once what matters (yield, quality,
  potency, resilience, structure) and Grow OS ranks the plants accordingly, showing the
  breakdown per plant so the number is explainable. Yield and THC are scored against the
  other plants of the same hunt, because those numbers only mean something in comparison.
  You can always override a plant's score by hand, mark a keeper, and note when a pheno was
  confirmed in a second run.

## 1.4.0

- New — **strain library**. A new "Sorten" page under Meine Grows is your own genetics
  catalogue: name, breeder, indica/sativa/hybrid, flowering weeks, free notes — plus the
  traits that actually change how you grow a plant: feeding appetite, stretch, and
  preferred VPD. (The backend for this existed but had no screen at all.)
- Fixed — a strain that prefers **higher humidity could not be saved**: the VPD preference
  is a shift in kPa, so negative values are perfectly normal, but it was being validated
  like a multiplier that must stay above zero.

## 1.3.0

- New — **Watchdog: Grow OS now tells you when the monitoring itself goes quiet.** A normal
  alert says "this value is wrong"; the watchdog says "I can't see anything right now" —
  the case where silence used to be ambiguous. Every minute it checks whether the
  background worker is still running, whether Home Assistant is answering, and whether
  fresh sensor values are actually arriving. If one of those stops it sends a single clear
  push (and one when it recovers) — never a repeated complaint.
- New — the Notification Center shows the **current system state in plain words** ("Alles
  wach — letzte Sensordaten vor 3 Minuten") and has a "Systemtest senden" button that
  pushes that state to your phone, so you can prove the path works.

## 1.2.0

- New — **leaf temperature offset for VPD**. What a plant actually feels is leaf VPD, and
  leaves sit 1-3 °C below air temperature (more under LED, which has no infrared). You can
  now set that offset per tent ("Blatt kühler als Luft"), and both the live dashboard and
  the measurement page use it — the measurement page even shows which offset it applied.
  Left at 0 you get the plain air VPD as before.
- Improved — when no VPD sensor is mapped, the calculated value now comes from the **live**
  temperature and humidity instead of the last stored measurement, which could be days old.
- Fixed — the status gauge's glow was **clipped into a square** by its own SVG bounds; it
  now fades out as a full circle.

## 1.1.1

- New — **the live tiles now show a real 24-hour curve**. Each sensor tile on the live
  dashboard draws the last day's trend where a fixed decorative bar used to sit, in the
  tile's own colour — so the day/night rhythm and any drift are visible at a glance.
  Tiles without recorded history are unchanged.

## 1.1.0

- New — **your sensor history is finally visible**. Grow OS has been recording every
  mapped sensor every 5 minutes and condensing it into daily statistics each night — but
  there was no way to look at it. The tent page now has a **Verlauf** section with a curve
  per metric (pH, EC, water temp, air temp, humidity, VPD) over 7, 14 or 30 days. Each
  curve shows the daily median as a line, the day's min/max as a band, and — if you've set
  thresholds for that tent — your target range behind it, so "am I inside my limits?" is
  answered at a glance.

## 1.0.52

- Fixed — on **mobile**, a grow's name showed twice on its page (once in the summary
  card, once in the KPI card below). The KPI card no longer repeats the name.

## 1.0.51

- Improved — the **diagnosis, SOPs, journal and measurement views now match the rest of
  the app**. Those grow pages still used the older, denser card style; they now use the
  same surfaces, badges, buttons and typography as everything else.

## 1.0.50

- Fixed — **"Alle Kameras testen" now shows every camera**, not just the first. On the
  Home Assistant page a snapshot preview appears for each mapped camera (with readable
  labels), each with its own state so a broken one doesn't hide the others.
- Fixed — the grow's name **no longer shows twice** on the grow page (removed the small
  duplicate in the top bar; the big title stays).

## 1.0.49

- Fixed (major) — **threshold alerts now repeat reliably**. They were edge-triggered:
  you got one push when a value first crossed the limit and then silence, even while it
  stayed out of range — and the check only ran every 5 minutes, so per-minute settings
  did nothing. Now alerts are level-triggered: while a value stays out of range Grow OS
  re-notifies every "Erneut erinnern alle N Minuten", checked once a minute by a
  dedicated watcher.
- New — **immediate push when you save a threshold**. If the current value is already out
  of range when you save, you get the alert right away instead of waiting for the interval.

## 1.0.48

- Fixed — opening a grow on a **desktop** now shows the "Zu diesem Grow" links
  (Messungen · Diagnose · Journal & Fotos · SOPs · Automatik, each pre-selected to that
  grow). They were inside a mobile-only block, so on desktop there was no way to get
  from a grow to its own pages.

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
