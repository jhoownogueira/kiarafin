export type ForecastEntryType = "Salary" | "ExtraIncome" | "FixedExpense"

export type ForecastEntry = {
  id: string
  name: string
  type: ForecastEntryType
  amount: number
  expectedDay: number
  createdAtUtc: string
  updatedAtUtc: string | null
}

export type ForecastEntryInput = Pick<ForecastEntry, "name" | "type" | "amount" | "expectedDay">
