export type NotificationSettingsDto = {
  notifyService: string | null
  quietHoursStartHour: number | null
  quietHoursEndHour: number | null
  thresholds: boolean
  calibration: boolean
  maintenance: boolean
  sensorOffline: boolean
  risks: boolean
}
