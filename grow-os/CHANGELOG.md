# Changelog

## 2.0.0-beta.30

**Beta.** Your tap water gets an opinion, and the feed chart can set the targets.

- New — **the water traffic light.** Your water profile held numbers and said
  nothing about them. Now every value you entered gets a sentence: what a
  carbonate hardness of 10 °dH does to your pH through the week, what a source
  EC of 1,4 mS/cm leaves you for fertilizer, whether that sodium figure matters.
  **Every threshold names its source** — Penn State Extension for the
  horticultural limits, the German detergent law (WRMG § 9) for soft/medium/hard
  — because a traffic light that turns red without cause sends you shopping for
  a reverse-osmosis unit you don't need.
  - Soft water reads as an **advantage**, not a deficiency: the greenhouse
    guidance assumes irrigation water is your calcium source; in a recirculating
    system your feed program is. Only without a CalMag-carrying program does the
    note appear.
  - Calcium, magnesium and nitrate deliberately get **no verdict** — just the
    reminder to count them toward your feed instead of adding on top.
  - Anything you left blank stays silent. A half-filled report is the normal case.
- New — **the feed chart can set your targets.** Under the mixing plan there is
  now a checkbox: use these weekly targets on screen too. Off by default — the
  chart is a manufacturer's suggestion, not a rule. Switched on, its EC and pH
  reach the live tiles, the advisory report and the dosing suggestion, so you
  never read "target EC 1,5" while mixing and something else on the dashboard.
  Only EC and pH are adopted; temperature, ORP and light stay with your phase
  profile, which is all the chart knows. The single EC number moves your
  existing target band without narrowing it — a target with no width would
  put every reading off-target.

## 2.0.0-beta.29

**Beta.** The app stops hoarding its knowledge.

- New — **the mixing plan.** Pick your nutrient program on the grow (the
  program card now actually binds) and the addback page tells you what goes
  into the bucket: "Bloom A 137 ml · PK 341 ml" — computed from the program's
  week chart and your system volume, with EC and pH target for that week.
  The Athena Blended chart ships digitized (mL/gal converted to mL/L, source
  named). Weeks beyond the chart keep the last column instead of going silent.
- New — **the due-routine watch.** Your procedures have always carried their
  rhythms ("water change every 7 days, warn after 8, critical after 10") —
  and nothing ever read them. Now the app does: with a running grow, overdue
  routines appear under Aufgaben by themselves. "Last done" comes from what
  you already record — the solution-change mark of a measurement, or a
  completed procedure.
- New — **companion level.** Full guidance (default), important-only, or
  expert. The expert gets no unsolicited reminders — only the alarms they set
  themselves. Nothing is hidden; nothing is pushed.
- Fixed — **the beta.27 Blended procedure change never actually shipped.** It
  was written to the runtime knowledge copy instead of the shipped defaults —
  the changelog promised it, installations never received it. Both it and the
  new feed chart now live in the shipped defaults, guarded by a test that
  reads exactly there.

## 2.0.0-beta.28

**Beta.** No more 5 °C advice for seedlings.

- Fixed — **the derived temperature target could recommend absurd cold.** The
  temperature band is back-calculated from the VPD target and the measured
  humidity. Physics found "5.3 °C" for a seedling at 54 % RH — correctly
  computed, agronomically nonsense. Temperatures outside 18–32 °C are no
  longer recommended; a band reaching into that range is trimmed (every value
  inside still hits the VPD target). Where no sensible target exists, the tile
  now names the real lever: raise the humidity — for seedlings, with the
  concrete tool (dome or humidifier).

## 2.0.0-beta.27

**Beta.** What did this grow cost — and the Blended kit gets its own path.

- New — **cost per grow.** Enter your electricity price (Einstellungen) and a
  price per litre on each dosing pump; the archive then shows what a finished
  run cost, down to €/g next to the yield. Everything is calculated and says
  so: power is lamp watts × light hours × days (a lower bound — side consumers
  are excluded on purpose), nutrients come from the dosing log. Hand additions
  are in no log, and the report says that too.
- Changed — **the soft-water mixing procedure knows Athena Blended.** Blended
  Balance contains silicate; running it alongside a separate potassium
  silicate would double-dose. The procedure now asks which you run: with
  Balance, it moves to the FRONT of the mixing order (per Athena) and the
  separate silicate step disappears. Sourced from Athena's own Balance
  documents. The addback procedure carries the same warning.

## 2.0.0-beta.26

**Beta.** For growers without permanent probes — the app now carries both worlds.

- New — **every value shows where it came from.** Tiles have always fallen back
  to your hand measurements when no sensor is mapped; what was missing was the
  honesty. A hand-measured value now reads "Hand · vor 2 Std" — and after 36
  hours it turns into "Hand · vor 3 Tagen — nachmessen?". A pH from last week
  no longer poses as live data. Each metric carries its own age: a merged
  report used to stamp everything with the newest timestamp.
- New — **the aeration check.** Most growers have no DO meter, but everyone
  knows the number on the pump box. Enter your air pump's L/h on the hydro
  system and Grow OS tells you whether it is enough for your volume — green,
  tight, too little, or too much (throttle it: excessive turbulence damages
  young roots). Clearly labelled as a calculated rule of thumb, never shown as
  a measured mg/L.
- New — **the DO tile knows physics.** Without a DO reading it now shows the
  ceiling: how much oxygen your water can hold at its current temperature
  (USGS solubility table). Warm water holds little — cooling first often beats
  buying a bigger pump.

## 2.0.0-beta.25

**Beta.** Tap water gets a profile — and a long-dead field gets a job.

- New — **the tap-water profile** under *Anlage → Leitungswasser*. Every city
  publishes a drinking-water report; enter its values once and Grow OS knows
  what your water brings before the first drop of nutrients: starting EC,
  hardness (classified soft/medium/hard along the legal bands), Ca/Mg, the pH
  buffer, the disinfectant. The fields follow a real report — lay your PDF next
  to the form and every number has a place.
- New — **the situation report opens with the source water.** Without it, an
  advisor reads "EC 0.28 before feeding" as residual salt and recommends a
  water change that would change nothing. The advisor folder and the Claude
  connection get this automatically.
- Fixed — **the water question your procedures kept asking.** A grow has
  carried a water source (tap/RO/mixed) since forever — entered on creation,
  read by nothing. Now the "what are you mixing with?" question at the start of
  a procedure comes pre-answered from the grow. Preselected, not enforced.

## 2.0.0-beta.24

**Beta.** The advisor gets a door — and a second add-on to walk through it.

- New — **Grow Berater, a separate add-on.** The folder from beta.23 works, but
  it has to be carried somewhere by hand. The advisor add-on skips the errand:
  a chat page in the Home Assistant sidebar that fetches the situation and the
  knowledge from Grow OS on every question. Same source as the download, so the
  two cannot drift apart. Anthropic, OpenAI or Ollama — with Ollama nothing
  leaves the house. Install it or don't: Grow OS itself still has no AI and no
  key.
- New — **read access from the internal add-on network** (172.30.32.0/23), which
  is what lets one add-on ask the other. GET only, and no shared secret — the
  admin key was removed on purpose and is not coming back. Nothing is reachable
  from outside Home Assistant.
- Changed — **the advisor page speaks to whoever opens it.** The old copy was
  written for someone who had already built the thing: "an advisor that only
  looks things up and does not connect them fails here" says nothing about what
  you are about to see. Page, readme and the hints on the four test questions
  now say what happens and how to spot a bad answer.
- Changed — **the instruction now permits documented numbers.** It forbade
  inventing them, which a careful assistant read as "give no numbers at all" —
  so it withheld a dosage that was printed in the material in front of it. The
  rule is now the one that was meant: quote what is documented, name where it
  comes from, invent nothing.

## 2.0.0-beta.23

**Beta.** Bring an RDWC advisor home — the knowledge, not just the readings.

- New — **the advisor folder.** Until now the export carried your measurements
  but not the material they are measured against, which leaves an assistant
  guessing from forum knowledge. The folder adds what already lives in Grow OS:
  11 procedures, 30 treatments, 20 symptoms, 8 pathogens, 37 rules and the
  setpoints, written out as readable text instead of JSON. Nine files, around
  120 KB — small enough for a Claude project, a GPT, or a local model via
  Ollama. Grow OS still sends nothing and needs no key.
- New — **four test questions with model answers**, so you can check the
  advisor before you trust it. A language model sounds equally certain when it
  is right and when it is inventing; the difference can only be tested, not
  heard.
- New — **the instruction sets four limits**: invent no numbers, do not
  overrule the operator's own setpoints, switch nothing, and ask rather than
  guess when the deciding value is missing. Dosing and automation stay in Grow
  OS behind their interlocks.
- Fixed — **journal entries without a title** printed as "2026-07-28 · — text"
  in the report, with a dash leading nowhere.

## 2.0.0-beta.22

**Beta.** Unreadable badges in light mode, and a test that catches the next one.

- Fixed — **"Kritisch" and "Info" on the diagnosis page** carried hard-coded
  light colours chosen for dark mode. In light mode the measured contrast was
  1.1 — effectively invisible. The text colour now comes from the theme:
  measured 5.5 in light, 10.7 in dark.
- Added — **a readability check in both themes.** It measures the contrast that
  is actually painted, compositing semi-transparent surfaces over what is beneath
  them; without that, a badge tinted at 10 % opacity reads as its own colour and
  the check reports nonsense. Two passes, because one is not enough: walking the
  routes only sees what renders without a backend, and empty pages have no
  badges — that pass ran green against the reinstated bug. The tone-carrying
  building blocks are therefore also placed into a real page and measured there.

## 2.0.0-beta.21

**Beta.** Light mode on the grow pages.

- Fixed — **the grow picker and every input field were dark on dark in light
  mode.** Their background colour was hard-coded to near-black while the text
  colour follows the theme. Both now use the `--sunk` token, which exists for
  both themes and whose dark value matches what was there before.
- Fixed — **the grow name appeared twice, one line under the other:** once in
  the picker, once in the back link below it. The link now reads "Zur
  Grow-Übersicht".
- Fixed — **"Verlauf8 gesamt".** The card header was a block, so both labels
  sat flush against each other. Title left, count right.
- Fixed — **the edit button had a minimum height but nothing centring its
  label,** so the text stuck to the top edge and cut visually into the line
  above it.

## 2.0.0-beta.20

**Beta.** Timestamps that say UTC now come back as UTC.

- Fixed — **45 read sites across ten repository classes read "…Utc" columns
  as local time.** Storage was correct; reading was not. Display never showed
  it, because the value carries its offset and means the same instant. The
  damage was wherever something calculated with it: the dosing check "is the
  last water level younger than two hours?" saw a reading from ten minutes ago
  as two hours in the future and never fired; the status report said "measured
  -120 minutes ago"; the trend lines on the live screen applied the time zone a
  second time, shifting the axis by the offset.
- Fixed — **the status report shows local date and time.** A human reads it,
  and just before midnight it printed the wrong day.
- Added — **a test reads the source and rejects any new site of this kind.**
  The mistake is made by copying the line next to it, and 45 occurrences never
  turned a single test red. Both new tests were verified against the
  reinstated bug; they measure differences, not clock readings, because on a
  UTC build server the offset is zero and none of this is visible.

## 2.0.0-beta.19

**Beta.** Two numbers the rig already knows, no longer typed in by hand.

- New — **guided volume calibration.** The level sensor (eTape) reports
  centimetres, but dosing needs litres, and the sum of pot and tank size is
  only a nameplate figure. Grow OS now reads the sensor every second while you
  fill: 15 s of a steady level in the empty system marks the zero point, 60 s
  of steady level afterwards raises the question "full?". You confirm by hand,
  because a pause in filling looks exactly like finished to a sensor. From the
  two points and the litres off your water meter comes the line — and the
  conversion sits at the source, so tiles, history, thresholds and the dosing
  factor all see litres.
- New — **the light cycle is learned, not entered.** The Home Assistant entity
  only knows on and off; the cycle follows from the last five days of
  transitions (median over the full on-phases, so one forgotten lamp cannot
  skew it). That makes two things visible that nobody would otherwise catch:
  18/6 during flower prevents flowering, and light in the middle of the dark
  phase causes revegetation or hermaphrodites.
- Fixed — **the water level arrived as text instead of a number.** The tile
  took it for a label, dropped the unit and drew no trend line. Next to a
  centimetre sensor, "72" without "L" is simply ambiguous.
- Fixed — **light transitions were read as local time although stored as UTC.**
  The time zone was applied twice: "on at 08:00" for a lamp that comes on at
  06:00.
- Fixed — **new columns ran before their tables existed.** They sat in the
  block for migration metadata, which runs early; they now have their own
  place at the end of schema setup.
- Changed — **the calibration run survives a page reload.** Standing at the
  reservoir with a hose, a locked screen would otherwise have cost the zero
  point.

## 2.0.0-beta.18

**Beta.** Second reality pass over beta.17's new behaviour.

- Fixed — **reservoir alarms kept running during drying.** The reservoir is
  drained, the pH probe sits in air reading nonsense — and it would have
  alarmed through exactly the critical drying days. Reservoir rules now pause
  during the drying window, and only there: someone running a reservoir
  without a grow record who sets rules means them.
- Fixed — **the second nutrient half never checked circulation.** A required
  confirmed circulation, B ran unchecked five minutes later. If the pump dies
  in between, B now waits for the next tick instead of dosing into still
  water — and the whole tent holds while it waits.

## 2.0.0-beta.17

**Beta.** A pass over the app's real-world logic — how a grow actually behaves,
not how the code does.

- Fixed — **nighttime is no longer "off target".** Tiles, score and alerts
  compared value against target with no idea of the light cycle: PPFD 0 at
  lights-off is correct, CO₂ falls to ambient because the plant consumes none,
  and VPD targets mean the day. Grow OS now asks the light sensor first, then
  the tent's light schedule; at lights-off, PPFD/CO₂/VPD carry no verdict and
  send no alarms. Nightly false alarms are how real alarms stop being believed.
- New — **dosing safety from the real reservoir.** Nobody doses into still
  water: a confirmed-stopped circulation pump now blocks even a manual dose,
  and the automation requires confirmed *running* circulation — a dead
  circulation pump is often the very reason the values drift. The mixing pause
  now belongs to the reservoir, not the pump: after *any* dose into the same
  water the reading says nothing for a while, whoever dosed. While a second
  nutrient half is outstanding, the whole tent holds; the automation doses
  nutrients before pH and at most once per tent per tick.
- New — **a mold ceiling on the humidity advice.** The VPD inversion knows only
  physics; in warm flower air it recommended humidity where grey mold becomes
  likely. Each phase now has a ceiling (seedling 80 % … finish 55 %); when the
  ceiling eats the whole band, the tile says the honest thing: lower the
  temperature, don't raise the humidity.
- Fixed — **a rooted clone is veg from day one.** It never had cotyledons; the
  seedling phase belongs to seeds. Before, every clone got 14 estimated
  seedling days with seedling EC.
- Fixed — **a CO₂ sensor is not CO₂ enrichment.** Without a burner, ambient
  ~420 ppm sat forever "off target". New tent switch "CO₂-Anreicherung"; without
  it the tile carries no target and explains why.
- New — **doses scale with the fill level.** The learned effect per ml comes
  from a full reservoir; in half the water the same dose works nearly twice as
  hard. Doses now shrink with the level (never grow). And the learning window
  cuts at the last water change — fresh water buffers differently.
- New — **drying is watched.** After harvest the tent becomes a drying room —
  the highest mold risk of the whole cycle, and the app used to look away.
  While the last grow is harvested, less than three weeks old and has no dry
  weight entered, the temperature and humidity tiles carry the 60/60 targets.
- New — **"Finish beginnt"** on the grow page: flushing starts when the
  trichomes say so, not when the breeder's weeks run out. Works for
  autoflowers too — the current phase now comes from one resolver for buttons
  and tiles alike.

## 2.0.0-beta.16

**Beta.** The main navigation moves to the top on phones.

- Fixed — **the bottom bar was cut off inside the Home Assistant app**, leaving
  only the top edge of the active item on screen. Not a contrast problem, a
  geometric one: under ingress Grow OS runs in an iframe whose height comes from
  the *large* viewport, so it extends below what the phone actually shows — and
  `position: fixed; bottom: 0` sticks to that iframe's bottom edge, not the
  screen's. Nothing inside the frame can measure how much is cut off. The four
  main entries now sit in their own row directly under the header, where the top
  edge is always visible.

## 2.0.0-beta.15

**Beta.** The seedling phase is now something you observe, not something the
calendar decides.

- New — **"Sämling ist durch"** on the grow page. The transition to veg does not
  hang on a date: it hangs on the plant. Real serrated leaves instead of the two
  round cotyledons, a thicker stem, new leaf pairs coming regularly, side shoots
  at the nodes, visibly more water going. One to three weeks after germination
  is typical — typical, not certain. Press the button when you see it, and the
  targets follow.
- Fixed — **the phase bar and the targets contradicted each other.** The bar said
  "Veg day 8" while the tiles showed seedling targets, because the app carried
  two different phase models: the bar knew germination/veg/flower, the targets
  additionally knew a seedling. The bar now shows the seedling too, and marks it
  "geschätzt" until you record the transition.
- Fixed — **a seed grow without a germination date stayed a seedling forever.**
  That is the normal case, since almost nobody records germination — and after
  three months a full-grown plant would still have been fed seedling EC.
- Fixed — **an empty temperature target now explains itself.** At 40 % humidity
  no temperature reaches a VPD target of 0.40–0.50 kPa: even at 5 °C the VPD is
  already 0.41. The calculation knew this and said nothing, leaving the tile
  blank. It now says so, and points at the humidity — which is the thing that
  actually needs changing.
- Removed — **the second QR code** on the Home Assistant page. It pointed at the
  ingress address *with* its token, so it worked once at best, and "generate new
  code" produced the same address every time. The working one lives under
  "Aufs Handy holen".
- Fixed — **the bottom bar on a phone was hard to see.** Against the page
  background it had a contrast of 1.04 — the same shade, with a one-pixel border
  as its only edge.

## 2.0.0-beta.14

**Beta.** Two fixes from the first real use of beta.13.

- Fixed — **the QR code led to a blank page.** It pointed at
  `/hassio/ingress/<slug>`; Home Assistant registers an add-on's sidebar panel
  at plain `/<slug>`. Under the wrong path the frontend finds no panel and draws
  nothing at all — no error, just an empty screen. Scanning the code now lands
  where clicking Grow OS in the sidebar lands. The page also states the
  precondition: "Show in sidebar" has to be on, or Home Assistant never creates
  an address for Grow OS in the first place.
- Fixed — **you could not look at the shipped setpoint profiles.** RDWC and DWC
  offered only "copy", so finding out what is actually in them meant creating a
  copy first — changing something in order to read something. They now open
  read-only, with the ranges written as `6–6,2` rather than two numbers running
  into each other.

## 2.0.0-beta.13

**Beta.** Dosing that suggests and then acts, a report for your own AI, and a
dashboard you can rearrange with your thumb.

- Fixed — **no pump had ever learned anything.** Every dose recorded the value
  before it and nothing wrote the value after, so the calculation that derives
  the effect per millilitre skipped every row. Grow OS now fills it in once the
  solution has mixed. The window is one mixing period wide: earlier you measure
  a streak, later you measure the plants drinking, and a wrongly attributed
  effect would sit inside every dose that follows.
- New — **"What would be needed now?"** on the dosing page. Grow OS computes the
  amount from what that pump has learned and shows its reasoning: the reading and
  where it came from, its age, the target and where *that* came from. The
  suggestion passes the same limits as a real dose, so it never shows an amount
  that would be refused.
- Fixed — that calculation used to read only your last hand-entered measurement
  and only a threshold you had typed yourself. It now takes the sensor value
  when that is the newer one, and falls back to your phase profile for the
  target. Without both, most setups got no suggestion at all.
- New — **automatic dosing.** Off by default, per pump. It requires an auto-off
  in Home Assistant, refuses to dose against a stale reading, and stays locked
  while the probe is uncalibrated or overdue — a drifting probe reports 6.0 while
  5.4 sits in the reservoir, and the pump would confidently dose the wrong way.
  Every unattended dose also goes out as a notification.
- New — **two-part nutrients (A and B).** Pair two pumps with a ratio; A runs,
  the separation time passes, then B. They must never meet concentrated:
  calcium from A precipitates with the sulphates and phosphates from B, and what
  flocculates never reaches the plant. The waiting half is stored in the
  database, so a restart between A and B cannot silently swallow it.
- New — **a report for your own AI agent**, on the grow page. Phase and day,
  current values with their targets and where each target came from, open
  issues, recent doses and journal entries. Grow OS sends nothing: you download
  the file and decide who sees it. There is still no AI inside the app.
- Fixed — **the hardware form never offered the wear templates.** All twelve
  existed in the knowledge base; none was reachable, so every device you added
  had an empty lifespan and the maintenance reminder that hangs off it never
  fired. A UV-C lamp keeps glowing past 9000 hours and stops clarifying — exactly
  the case nobody notices without a reminder.
- New — **airflow at leaf level** as a measurement, in m/min (RDWC 90–120,
  otherwise 60–90). It belongs with VPD: airflow breaks up the humid boundary
  layer at the leaf, and when that layer sits still your hygrometer reads a
  number the leaf never experiences. **Water flow** is deliberately a three-way
  choice, not a number — the source says "moderate, not strong" and names no
  throughput.
- New — **rearrange dashboard tiles with your finger.** HTML5 drag-and-drop has
  no touch support, so on a phone you could previously only add, remove and
  rename. Drag by the handle; the rest of the tile still scrolls.

## 2.0.0-beta.12

**Beta.** A QR code that puts Grow OS on your phone's home screen.

- New — **Anlage → Aufs Handy holen.** Scan the code with your phone, log into
  Home Assistant once, then "Add to Home Screen". You get an icon that opens
  straight into Grow OS.
- The obvious route is the broken one: the address in your address bar carries
  an ingress token that changes on every request, so a bookmark on it is dead
  the next day. The code points at the stable sidebar path instead
  (`/hassio/ingress/<slug>`), which Grow OS asks the Supervisor for — the slug
  differs depending on how the add-on was installed and cannot be guessed.
- The page builds the full address in your browser, because the server only
  knows Home Assistant as `http://supervisor/core` and has no idea what name
  you reach it under. Enter a different one if you need to; `localhost` is
  refused (on a phone it points at the phone), and a `.local` name gets a note
  about Android rather than a ban.
- Said plainly on the page: this does not remove Home Assistant's frame around
  Grow OS, and the home-screen icon belongs to Home Assistant. That would need
  an open port, and then Grow OS would need a login of its own.

## 2.0.0-beta.11

**Beta.** Calibrate a pump by volume, not by stopwatch — and your own limits
now reach the diagnosis too.

- New — **calibrate to 100 ml.** The old way ran the pump for 30 seconds and
  asked what was in the cup. At 23 ml, misreading by 1 ml is a 4 % error, and
  that error sits inside every dose afterwards. Grow OS now runs until roughly
  100 ml has come out, where the same misreading is worth 1 %. A brand-new pump
  still starts with the timed run — before the first calibration nobody knows
  how long 100 ml takes. On a slow pump the target drops to 50 or 25 ml so the
  run still fits inside the allowed time.
- New — the button counts down. A 100 ml run takes over two minutes; without a
  number on screen that looks like a crash.
- Fixed — **your own thresholds now count in the diagnosis.** They were handed
  to the analysis and then dropped on the floor: the field was never assigned,
  so the diagnosis kept reading only the shipped knowledge while the alerts
  already used your values. The same measurement got two different verdicts on
  two pages.
- Fixed — a pH range you enter yourself is now binding, even when it is
  narrower than the comfort zone. The shipped number is a mix-to target and is
  deliberately widened to avoid noise; a number you type is a threshold. Anyone
  deliberately running tighter than the comfort zone was told nothing at all.

## 2.0.0-beta.10

**Beta.** DWC gets its own targets, and you can write your own.

- New — **DWC has its own setpoint profile.** Until now there was one shipped
  set of values, for RDWC, and DWC was produced from it by multiplying EC in
  code — only EC, so everything else was identical, and NFT or aeroponics
  quietly got RDWC values with nothing saying so. DWC now carries its own
  numbers per phase (EC about 30 % higher, the smaller buffer), and profiles
  are picked by growing style.
- New — **Wissen → Sollwert-Profile**: copy a shipped profile and write your
  own experience into it, per phase. Only what you actually change becomes
  yours; everything you leave alone keeps receiving our updates. A full copy
  would have cut you off from every later improvement at the first save.
- New — **choose a profile where it belongs.** The hydro system sets the
  default, because DWC or RDWC is a property of your hardware — set it once
  and every grow in it inherits. A single grow may differ, because setpoints
  describe how you run *that* plant. Two runs in the same reservoir are
  allowed to differ.
- New — the tile names the profile when it is not the shipped one, the same
  way it already says "dein Wert". A threshold you enter on the tent still
  beats every profile.


## 2.0.0-beta.9

**Beta.** Your own limits now win — everywhere, and the tile says so.

- Fixed — **the app had two opinions about the same value.** With pH limits of
  5,60–5,90 entered and a reading of 5,99, the live tile said "zu niedrig"
  (against the shipped 6,00–6,10) while the alert said "zu hoch" (against
  yours). Follow the tile and you dose pH up; follow your own limit and you
  dose it down — opposite directions, with nothing saying which one applied.
  One place now decides, and the live tiles, the diagnosis, the dosing and the
  alerts all read from it.
- New — **the tile names the source**: "Ziel 5,60–5,90 · dein Wert". Shipped
  values stay unlabelled, so the note only appears where it answers something.
- Note — what you did *not* enter stays with the shipped phase values. Setting
  your own pH does not flatten the phase staircase for EC, VPD or anything
  else. A switched-off limit does not count, and half a range is allowed:
  "not above 6,2" leaves the bottom open.


## 2.0.0-beta.8

**Beta.** Two fixes everyone gets, plus a test mode for people who develop
Grow OS.

- Fixed — **no more false alarm right after a restart.** For the first seconds
  after starting, the watchdog reported "Überwachung steht", because the
  heartbeat lives in memory and no round had run yet. That fired after every
  restart and every update — exactly when you are looking at the screen. A
  fresh start is now its own quiet state; a stall that follows a completed
  round still counts as one.
- Fixed — with no Home Assistant configured, the camera request went out
  anyway, failed, and tripped the connection guard, so "Home Assistant
  antwortet nicht" appeared even where no camera was ever set up.
- New (for developers) — **test data**: start with `GROW_OS_DEMO=1` and Grow OS
  fills itself with invented but plausible readings, backfills 24 hours of
  history, and draws a placeholder camera frame. pH and EC drift upwards over
  the day so there is something real to correct, which is what the dosing needs
  to be tried against. A strip across the app says the values are invented for
  as long as it is on. Environment variable only — there is deliberately no
  switch in the interface, because invented readings in a running tent would
  not merely be wrong: alerts and the dosing hang off them.


## 2.0.0-beta.7

**Beta.** Grow OS can act, not just watch: dosing pumps — by hand for now.

- New — **Anlage → Dosierung.** Set up a peristaltic pump, tell Grow OS what
  it doses and which Home Assistant entity switches it, calibrate it, and give
  a dose at the press of a button. The pump on screen turns for exactly as long
  as it really runs. Nothing happens on its own yet; the automation follows
  once the arithmetic and the limits have proven themselves on real tents.
- New — **calibration**: the pump runs into a measuring cup, you enter what
  landed in it, and Grow OS knows its ml/min. Without that, millilitres are not
  a runtime and it refuses rather than assuming a flow rate. A worn tube pumps
  less than a new one, so the pump carries its own due date.
- New — **test mode**, to walk the whole thing through without hardware: it
  computes, waits out the real runtime and logs, but switches nothing. Test
  doses are marked as such everywhere and never count towards what Grow OS
  learns — otherwise there would later be a number with no drop behind it.
- New — **the log records refusals too**, with the reason. Otherwise you are
  left wondering why nothing happened overnight.
- Note — the dose is not computed from the concentration. How hard your
  solution resists a pH change depends on water hardness and nutrients, so
  Grow OS measures instead of guessing: the first doses you give yourself, and
  from three of them onward it knows the effect per millilitre in *your*
  reservoir.
- Safety — every dose passes hard limits: largest single dose, mixing pause,
  daily count and volume, and a runtime ceiling. Grow OS switches every
  configured pump off once at startup, in case a crash left one running. The
  later automation stays locked until you confirm a Home-Assistant-side
  auto-off — the only thing that helps if Grow OS dies between on and off.


## 2.0.0-beta.6

**Beta.** The Live screen judges your values again — without waiting for a
hand-typed measurement first.

- Fixed — **target ranges no longer wait for a manual measurement.** Every
  target hung off your last typed-in measurement, because the phase was read
  from it. A grow with live sensors but nothing typed in got no target on any
  tile: no colour, no "im Ziel", no "daneben" — while the header above it said
  "Veg · Tag 7". The phase comes from the grow now. A measurement you do
  record still wins over the calculation.
- New — **Luft and RLF are judged too.** The knowledge base only carries a VPD
  target for climate, so the two largest tiles on the screen stayed silent
  while the small VPD tile beside them was judged. They now show a range read
  back out of the VPD target: at 46 % humidity, the temperature that lands in
  your VPD target. Same knowledge, read the other way round — nothing invented.
- New — **each of those ranges says what it depends on**: "Ziel 15,8–19,6 °C ·
  bei 46 % RLF". Without that line it reads as "cool the tent down", when the
  real fix may be raising the humidity. Now you can see both levers.
- Fixed — **one climate problem counts once.** Luft, RLF and VPD describe the
  same situation; deducting for all three turned a mild offset into "Kritisch"
  and listed three names for one problem.
- Fixed — the header said "Alle Messwerte im Zielband" even when there were no
  target ranges at all: "all good" where it had to say "I checked nothing". The
  score printed a number out of the missing-sensor penalty alone, with a verdict
  beside it. Both now say plainly when nothing could be judged.


## 2.0.0-beta.5

**Beta.** Two things the 2.0 rebuild had dropped, brought back.

- New — **arrange the Live screen yourself again.** Press "Anpassen" and the
  fixed rows become movable: drag tiles within a section or into another one,
  rename sections, add your own, remove what you never look at. Any Home
  Assistant entity can become a tile — a UV clarifier, a socket's power draw,
  a fan — including things Grow OS knows nothing about. Saved per tent, with
  "Zurücksetzen" to get the standard back.
  Nothing changes unless you press it: without an arrangement of your own the
  screen looks exactly as it does today, and entering the mode starts from
  what is on screen, so no tile appears or disappears on the way in.
- New — **each tile shows its last 24 hours as a curve.** "Too high" does not
  tell you whether a value is still climbing or already coming back down;
  the curve does. It takes the place of the target bar rather than being added
  below it, so the tiles stay the size they were, and it is drawn in the
  colour of the tile's status.
- Note — an arrangement saved before the 2.0 rebuild is not brought back.
  It was built for a different screen, is missing everything added since, and
  would quietly take values off your dashboard. Your Live screen therefore
  looks the same after this update as before it.
- Fixed — dragging on a phone still does not work (a browser limitation that
  was there before as well). Adding, removing, renaming and the ↑ ↓ buttons
  work everywhere.


## 2.0.0-beta.4

**Beta.** The watchdog learned to see each tent on its own.

- Fixed — **one dark tent no longer hides behind another's fresh data.** The
  watchdog judged "newest reading anywhere", so a tent going silent while a
  second one kept reporting raised nothing at all. It now keeps a pulse per
  tent: the push names the dark tent ("Zelt 'Hauptzelt' liefert seit 45
  Minuten nichts"), and a further tent failing later is a new message instead
  of being swallowed as a repetition of the old one.
- New — **the system watch is visible where it matters.** The
  Systemüberwachung card lists for each tent when its data last arrived, and Live
  shows a warning strip above the metric tiles whenever monitoring itself has
  a problem — right where fresh-looking numbers would otherwise lie. Quiet
  when everything is fine.
- Fixed — switching cameras could leave the previous camera's image on stage
  under the new camera's name when the new one delivered nothing.
- Fixed — picking a different strain in the grow form kept the flowering
  weeks the previous pick had filled in, as if they were your own numbers.


## 2.0.0-beta.3

**Beta.** One page, rebuilt after real use: the knowledge base.

- New — **SOPs & Bibliothek is organised by urgency** instead of showing 93
  equal cards. Emergencies come first: the root-rot and power-outage SOPs sit
  at the top as red cards with a guided start, next to "I see something —
  what is it?" leading into the symptom list. Below, the routine SOPs form one
  table with kind, duration and step count — the things you actually compare.
  The reference material (symptoms, treatments, pathogens, target values,
  wear) sits in six compact panels; each row shows the one fact that helps
  while scanning, like where a symptom leads or how long an air stone lasts.
- Changed — search results are compact rows tagged by category, no longer a
  wall of cards.


## 2.0.0-beta.2

**Beta.** Everything the first beta was missing or got wrong, found by using it.

- New — **plan how long a grow stays in veg.** Without it the timeline could only
  ever say "day 68 and counting": no flip date, no harvest estimate. Enter the
  intended veg days and the timeline shows when the flip is due ("in 8 days",
  or "overdue by 12"), plus an estimated harvest. Leave it empty and nothing is
  invented — the run simply stays open.
- New — **the timeline shows all three phases**: germination, veg, flowering.
  Phases without data say so instead of disappearing, and the running phase
  fills up so you can see where in the plan today sits. Same timeline
  everywhere now: Live, the grow list and the grow itself.
- New — **a grow can point at a strain from your library.** Picking one fills in
  the breeder and its flowering weeks, and the strain statistics ("runs",
  "average yield") finally count the right runs instead of matching names,
  where a typo silently dropped a run.
- New — **ask the assistant a question.** The model could be connected and
  tested, but there was no way to ask it anything. Answers now show which of
  your records back each statement, and anything unbacked is marked before you
  read it.
- New — **the first run guides you**: an empty installation now shows the three
  steps in the order they have to happen instead of an empty cockpit.
- Fixed — **tasks could be created but never ticked off.** The button lived on a
  panel that was replaced during the redesign.
- Fixed — **light schedules and plant management were unreachable.** The tent
  detail page lost its only link, taking the light times, the tent history and
  the whole setup and plant management with it — which also broke the pheno
  hunt, since that is where plants are created.
- Fixed — **the pheno hunt lost its weighting**, so scores could no longer be
  tuned.
- Fixed — **the score ring was green while the score said "critical."** Ring,
  number and word now agree. The tent page also had a second, different scoring
  formula; there is one now.
- Fixed — **the sidebar scrolled away on long pages**, leaving a bare strip
  underneath.
- Fixed — the start date is required; without one, the day you create the grow
  is day one.
- Fixed — thresholds now show the target range of the current phase beside them,
  from the same source the tiles use, and can be filled from it.
- Changed — **the tent/grow selectors at the top are gone.** They steered
  nothing: no page ever read them, while each page picked its own grow. Every
  page now chooses for itself, visibly, and the sidebar counters count across
  all running grows — which is what their pages show.

## 2.0.0-beta.1

**Beta.** The complete UI redesign — every screen rebuilt 1:1 from the designer's
handoff. Marked beta because a few rough edges are still expected; data, automations
and the API are untouched, and 1.8.4 remains the last stable release.

- New — **the whole app follows one design language now**: instrument-cluster panels,
  hairline borders, mono labels, a dark and a light theme with a proper toggle
  (sidebar and settings stay in sync).
- New — **Live** is a single cockpit: score ring with the reasons behind the number,
  climate and nutrient bands with target ranges, camera stage, the current risk with
  its SOP, today's tasks and the slow-moving observations — plus the grow phase
  timeline.
- New — **Messen** checks values while you type and shows deviations live; saving can
  jump straight into the matching Addback.
- New — **Addback** and **Grow anlegen** are one page each instead of wizards.
- New — **Aufgaben** sorts by what matters: risks first, then appointments, then
  maintenance — one main action per row.
- New — **Grows** shows each run as a card with its phase bar; finished runs live in
  **Ernte & Archiv** as a yield table with a two-run comparison.
- New — **Sorten & Pheno-Hunt** on one page: the library with runs, average yield and
  keeper per strain, and the candidate strip with the scoring sheet inline.
- New — **Journal & Fotos** is one stream — entries, measurement photos and events
  together, with a photos-only filter.
- New — **SOPs & Bibliothek** merges knowledge into one searchable collection;
  emergency SOPs are tagged and highlighted.
- New — **Regeln & Automatik** puts thresholds (with per-rule cooldown), auto
  measurements, notifications and the AI assistant behind one set of tabs.
- New — **Home Assistant** shows the mapping as rows with live values and adds
  a QR panel to pair the phone in the grow room.
- New — **Zelte & Räume** is a master-detail view: climate, light, air and occupancy
  per tent, camera and mapped sensors included.
- Fixed — a global reset was overriding half the design system's spacing.
- Fixed — the tent's active grows were never populated, which silently disabled
  alert rows and tile target ranges.
- Fixed — the live score could show 100 while values were out of range; it now
  counts real deviations.

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
