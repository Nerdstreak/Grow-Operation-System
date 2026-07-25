import type { PhenoEvaluationDto, PhenoPlantDto } from '../../types/pheno'

// The techniques a grower actually applies, so the sheet offers a checklist rather than
// free text — that keeps it comparable across runs.
export const TRAINING_METHODS = ['LST', 'Topping', 'Supercropping', 'Lollipopping', 'Entlauben', 'SCROG', 'SOG', 'Fimming'] as const

/** The editable part of a score sheet (identity and derived values excluded). */
export type SheetDraft = Omit<PhenoEvaluationDto, 'plantInstanceId' | 'stretchFactor'>

export function emptySheet(): SheetDraft {
  return {
    vigorScore: null, internodeSpacing: 'Unknown', branchingScore: null, leafToBudScore: null, heightAtFlipCm: null,
    trainingMethods: [], trainingResponseScore: null, stressToleranceScore: null, pestResistanceScore: null,
    floweringDays: null, heightAtHarvestCm: null, wetYieldG: null, dryYieldG: null, budDensityScore: null,
    resinScore: null, trimEaseScore: null,
    aromaScore: null, aromaNotes: null, flavorScore: null, effectScore: null, effectNotes: null,
    thcPercent: null, cbdPercent: null, terpeneNotes: null,
    manualOverallScore: null, isKeeper: false, confirmedInSecondRun: false, notes: null,
  }
}

export function sheetFrom(plant: PhenoPlantDto): SheetDraft {
  const evaluation = plant.evaluation
  if (!evaluation) return emptySheet()
  return {
    vigorScore: evaluation.vigorScore,
    internodeSpacing: evaluation.internodeSpacing,
    branchingScore: evaluation.branchingScore,
    leafToBudScore: evaluation.leafToBudScore,
    heightAtFlipCm: evaluation.heightAtFlipCm,
    trainingMethods: evaluation.trainingMethods,
    trainingResponseScore: evaluation.trainingResponseScore,
    stressToleranceScore: evaluation.stressToleranceScore,
    pestResistanceScore: evaluation.pestResistanceScore,
    floweringDays: evaluation.floweringDays,
    heightAtHarvestCm: evaluation.heightAtHarvestCm,
    wetYieldG: evaluation.wetYieldG,
    dryYieldG: evaluation.dryYieldG,
    budDensityScore: evaluation.budDensityScore,
    resinScore: evaluation.resinScore,
    trimEaseScore: evaluation.trimEaseScore,
    aromaScore: evaluation.aromaScore,
    aromaNotes: evaluation.aromaNotes,
    flavorScore: evaluation.flavorScore,
    effectScore: evaluation.effectScore,
    effectNotes: evaluation.effectNotes,
    thcPercent: evaluation.thcPercent,
    cbdPercent: evaluation.cbdPercent,
    terpeneNotes: evaluation.terpeneNotes,
    manualOverallScore: evaluation.manualOverallScore,
    isKeeper: evaluation.isKeeper,
    confirmedInSecondRun: evaluation.confirmedInSecondRun,
    notes: evaluation.notes,
  }
}
