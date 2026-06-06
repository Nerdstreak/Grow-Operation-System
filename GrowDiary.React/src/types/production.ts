import type { MotherHealthStatus, PlantRole, PlantStatus, QuarantineResult, SetupStatus, SetupType, StrainDominance } from './shared'

export type TentType = 'Production' | 'Mother' | 'Quarantine' | 'Propagation' | 'MultiPurpose'
export type TentStatus = 'Active' | 'Archived'
export interface SetupDto {
  id: number
  tentId: number
  name: string
  setupType: SetupType
  status: SetupStatus
  notes: string | null
  cloneCounterTotal: number | null
  lastCloneCutAt: string | null
  motherHealthStatus: MotherHealthStatus | null
  quarantineStartedAt: string | null
  quarantinePlannedEndAt: string | null
  quarantineResult: QuarantineResult | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateSetupRequest {
  tentId: number
  name: string
  setupType: SetupType
  notes?: string | null
  cloneCounterTotal?: number | null
  lastCloneCutAt?: string | null
  motherHealthStatus?: MotherHealthStatus | null
  quarantineStartedAt?: string | null
  quarantinePlannedEndAt?: string | null
  quarantineResult?: QuarantineResult | null
}

export interface UpdateSetupRequest {
  name: string
  status: SetupStatus
  notes?: string | null
  cloneCounterTotal?: number | null
  lastCloneCutAt?: string | null
  motherHealthStatus?: MotherHealthStatus | null
  quarantineStartedAt?: string | null
  quarantinePlannedEndAt?: string | null
  quarantineResult?: QuarantineResult | null
}

export interface StrainDto {
  id: number
  name: string
  breeder: string | null
  dominance: StrainDominance
  flowerWeeksMin: number | null
  flowerWeeksMax: number | null
  notes: string | null
  nutrientDemandFactor: number | null
  stretchFactor: number | null
  vpdPreferenceShift: number | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreateStrainRequest {
  name: string
  breeder?: string | null
  dominance: StrainDominance
  flowerWeeksMin?: number | null
  flowerWeeksMax?: number | null
  notes?: string | null
  nutrientDemandFactor?: number | null
  stretchFactor?: number | null
  vpdPreferenceShift?: number | null
}

export type UpdateStrainRequest = CreateStrainRequest

export interface PlantInstanceDto {
  id: number
  strainId: number | null
  setupId: number | null
  growId: number | null
  parentPlantId: number | null
  label: string
  plantRole: PlantRole
  plantStatus: PlantStatus
  phenoLabel: string | null
  startedAt: string | null
  endedAt: string | null
  notes: string | null
  strainName: string | null
  createdAtUtc: string
  updatedAtUtc: string
}

export interface CreatePlantInstanceRequest {
  strainId?: number | null
  setupId?: number | null
  growId?: number | null
  parentPlantId?: number | null
  label: string
  plantRole: PlantRole
  plantStatus: PlantStatus
  phenoLabel?: string | null
  startedAt?: string | null
  endedAt?: string | null
  notes?: string | null
}

export type UpdatePlantInstanceRequest = CreatePlantInstanceRequest

export interface CreateCloneFromMotherRequest {
  motherPlantId: number
  targetSetupId?: number | null
  label: string
  phenoLabel?: string | null
  notes?: string | null
  strainId?: number | null
  cutAt?: string | null
}

export type QuarantineDecision = 'Cleared' | 'Rejected'

export interface DecideQuarantinePlantRequest {
  plantId: number
  decision: QuarantineDecision
  targetSetupId?: number | null
  targetGrowId?: number | null
  decidedAt?: string | null
  notes?: string | null
}

export type ChangeoutKind = 'Partial' | 'Full'

export interface ChangeoutDto {
  id: number
  growId: number
  hydroSetupId: number | null
  kind: ChangeoutKind
  performedAtUtc: string
  volumeChangedLiters: number | null
  percentChanged: number | null
  ecBefore: number | null
  ecAfter: number | null
  phBefore: number | null
  phAfter: number | null
  notes: string | null
  createdAtUtc: string
}

export interface CreateChangeoutRequest {
  kind: ChangeoutKind
  performedAtUtc?: string | null
  volumeChangedLiters?: number | null
  percentChanged?: number | null
  ecBefore?: number | null
  ecAfter?: number | null
  phBefore?: number | null
  phAfter?: number | null
  notes?: string | null
}
