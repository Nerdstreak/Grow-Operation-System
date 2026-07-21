export type AlertRuleDto = {
  metricKey: string
  minValue: number | null
  maxValue: number | null
  notifyService: string
  enabled: boolean
  cooldownMinutes: number
}

export type TentAlertRulesDto = {
  tentId: number
  rules: AlertRuleDto[]
}
