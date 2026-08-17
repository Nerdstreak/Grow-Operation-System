/**
 * Stichwörter aus der Wissensbasis, zu denen es keinen eigenen Eintrag gibt.
 *
 * <b>Wozu.</b> Die Erreger- und Maßnahmen-Dateien verweisen auf 65 Symptom-
 * Schlüssel, die als Datensatz nicht existieren — bei „Bakterielle Fäule" steht
 * etwa `slimy-roots-foul-smell`. Die Oberfläche behandelte sie wie Verweise und
 * zeigte deshalb den rohen Schlüssel: 69 Stellen, an denen ein Entwickler-Wort
 * mitten im deutschen Text stand.
 *
 * <b>Was hier NICHT passiert.</b> Es wird nichts erfunden und nichts fachlich
 * ergänzt — nur der vorhandene englische Schlüssel ins Deutsche gebracht. Wo
 * eine Übersetzung fehlt, macht {@link stichwort} den Schlüssel wenigstens
 * lesbar, statt ihn roh durchzureichen.
 *
 * Bekommt eines dieser Stichwörter später einen echten Eintrag in der
 * Wissensbasis, gewinnt der automatisch: die Oberfläche fragt zuerst dort.
 */
const UEBERSETZUNG: Record<string, string> = {
  // --- Wurzeln ---
  'brown-roots-dry': 'Braune, trockene Wurzeln',
  'brown-roots-some': 'Einzelne braune Wurzeln',
  'slimy-roots-foul-smell': 'Schleimige Wurzeln, fauliger Geruch',
  'reddish-roots': 'Rötliche Wurzeln',
  'root-rot-active': 'Aktive Wurzelfäule',
  'root-rot-localized': 'Örtlich begrenzte Wurzelfäule',
  'emergency-root-rot': 'Wurzelfäule — Notfall',
  'complete-root-destruction': 'Wurzelwerk vollständig zerstört',
  'stem-rot-base': 'Stängelfäule am Ansatz',
  'vascular-blockage': 'Verstopfte Leitbahnen',
  'vascular-discoloration': 'Verfärbte Leitbahnen',

  // --- Blätter ---
  'leaf-yellowing-progressive': 'Fortschreitende Blattvergilbung',
  'leaf-mosaic-pattern': 'Mosaikmuster auf den Blättern',
  'mottled-leaves': 'Gefleckte Blätter',
  'praying-leaves': 'Betende Blätter (nach oben gestellt)',
  'led-bleaching': 'Ausbleichen durch zu viel Licht',
  'light-stress': 'Lichtstress',
  'heat-stress': 'Hitzestress',
  'deformed-growth': 'Verformter Wuchs',
  'abnormal-flower-structure': 'Auffällige Blütenstruktur',
  'trichome-reduction': 'Weniger Trichome',

  // --- Wuchs ---
  'growth-stagnation': 'Wuchs steht still',
  'stunted-growth-progressive': 'Zunehmend gehemmter Wuchs',
  'stunted-development': 'Gehemmte Entwicklung',
  'slow-growth-good-values': 'Langsamer Wuchs trotz guter Werte',
  'stretching-toward-light': 'Strecken zum Licht',
  'weak-stems': 'Schwache Stängel',
  'reduced-yield-progressive': 'Zunehmend geringerer Ertrag',
  'dudding-disease': 'Dudding (Kümmerwuchs)',
  'transplant-stress': 'Umpflanz-Stress',
  'environmental-stress': 'Umgebungsstress',
  'damping-off-cuttings': 'Umfallkrankheit bei Stecklingen',
  'rapid-plant-collapse': 'Pflanze bricht schnell zusammen',
  'rapid-deterioration': 'Schnelle Verschlechterung',
  'wilting-despite-water': 'Welken trotz Wasser',
  'wilting-systemic': 'Welken der ganzen Pflanze',
  'wilting-yellow-leaves': 'Welken mit gelben Blättern',

  // --- Nährlösung und Wasser ---
  'ec-rising-stagnant': 'EC steigt bei stehendem Verbrauch',
  'salt-accumulation': 'Salzanreicherung',
  'hungry-feeding-too-low': 'Unterversorgt — zu schwach gefüttert',
  'calmag-deficiency-severe': 'Starker CalMag-Mangel',
  'calmag-baseline-soft-water': 'CalMag-Grundgabe bei weichem Wasser',
  'water-cloudiness': 'Trübes Wasser',
  'water-color-abnormal': 'Auffällige Wasserfarbe',
  'orp-drift-down': 'ORP fällt ab',
  'system-contaminated': 'System verkeimt',
  'contamination-suspected': 'Verkeimung vermutet',

  // --- Technik ---
  'ec-sensor-drift': 'EC-Sonde driftet',
  'ec-calibration-overdue': 'EC-Kalibrierung überfällig',
  'ph-calibration-overdue': 'pH-Kalibrierung überfällig',
  'chiller-suspected-failure': 'Kühler vermutlich ausgefallen',
  'no-chiller-available': 'Kein Kühler vorhanden',
  'water-temp-emergency': 'Wassertemperatur — Notfall',
  'top-canopy-too-hot': 'Zu heiß an der Blattspitze',
  'late-flower-humidity-issue': 'Zu feucht in der Spätblüte',
  'post-power-outage': 'Nach einem Stromausfall',

  // --- Schädlinge ---
  'aphids': 'Blattläuse',
  'root-aphids': 'Wurzelläuse',
  'fungus-gnat-larvae': 'Trauermückenlarven',
  'spider-mites': 'Spinnmilben',
  'thrips': 'Thripse',
  'thrips-larvae': 'Thripslarven',
  'white-flies': 'Weiße Fliegen',
  'soil-pests': 'Schädlinge im Substrat',
  'root-pests-detected': 'Schädlinge an den Wurzeln',
}

/**
 * Ein Stichwort in lesbarer Form.
 *
 * Ohne Übersetzung wird wenigstens der Schlüssel entzerrt — „led bleaching"
 * statt „led-bleaching". Ein englisches Wort ist unschön, ein Bindestrich-
 * Bezeichner mitten im Satz ist schlimmer.
 */
export function stichwort(id: string): string {
  const bekannt = UEBERSETZUNG[id]
  if (bekannt) return bekannt
  const entzerrt = id.replace(/[-_]+/g, ' ').trim()
  return entzerrt.charAt(0).toUpperCase() + entzerrt.slice(1)
}

/** Nur für Tests: wie viele Stichwörter übersetzt sind. */
export const UEBERSETZTE_STICHWOERTER = Object.keys(UEBERSETZUNG)
