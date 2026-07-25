export type InternodeSpacing = 'Unknown' | 'Tight' | 'Medium' | 'Wide'

export interface PhenoEvaluationDto {
  plantInstanceId: number
  vigorScore: number | null
  internodeSpacing: InternodeSpacing | null
  branchingScore: number | null
  leafToBudScore: number | null
  heightAtFlipCm: number | null
  trainingMethods: string[]
  trainingResponseScore: number | null
  stressToleranceScore: number | null
  pestResistanceScore: number | null
  floweringDays: number | null
  heightAtHarvestCm: number | null
  wetYieldG: number | null
  dryYieldG: number | null
  budDensityScore: number | null
  resinScore: number | null
  trimEaseScore: number | null
  aromaScore: number | null
  aromaNotes: string | null
  flavorScore: number | null
  effectScore: number | null
  effectNotes: string | null
  thcPercent: number | null
  cbdPercent: number | null
  terpeneNotes: string | null
  manualOverallScore: number | null
  isKeeper: boolean
  confirmedInSecondRun: boolean
  notes: string | null
  stretchFactor: number | null
}

export interface PhenoScoreDto {
  total: number | null
  yield: number | null
  quality: number | null
  potency: number | null
  resilience: number | null
  structure: number | null
  isManual: boolean
}

export interface PhenoPlantDto {
  plantInstanceId: number
  label: string
  phenoLabel: string | null
  strainName: string | null
  strainId: number | null
  plantRole: string
  plantStatus: string
  parentPlantId: number | null
  evaluation: PhenoEvaluationDto | null
  score: PhenoScoreDto
}

export interface PhenoWeightsDto {
  yield: number
  quality: number
  potency: number
  resilience: number
  structure: number
}

export interface PhenoHuntDto {
  growId: number
  weights: PhenoWeightsDto
  plants: PhenoPlantDto[]
}
