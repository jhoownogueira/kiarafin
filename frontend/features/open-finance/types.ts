export type PluggyIntegrationState =
  | "Disabled"
  | "PendingConfirmation"
  | "InitialSync"
  | "Connected"
  | "Syncing"
  | "Error"
  | "ConsentExpired"

export type PluggyIntegrationStatus = {
  status: PluggyIntegrationState
  itemId: string | null
  activatedAtUtc: string | null
  lastSuccessfulSyncAtUtc: string | null
  lastAttemptAtUtc: string | null
  accountCount: number
  transactionCount: number
  consentExpiresAtUtc: string | null
  lastError: string | null
  initialImportCompleted: boolean
  institutions?: string[]
  nextScheduledSyncAtUtc?: string | null
}

export type PluggyIntegrationPreview = {
  existingAccounts: number
  existingCreditCards: number
  existingTransactions: number
  pluggyAccounts: number
  pluggyTransactions: number
  consentExpiresAtUtc: string | null
}
