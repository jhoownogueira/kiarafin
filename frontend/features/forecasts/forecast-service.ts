import { api } from "@/lib/api/client"
import type { ForecastEntry, ForecastEntryInput } from "@/features/forecasts/forecast-types"

const root = (householdId: string) => `/households/${householdId}/forecasts`

export const forecastKeys = {
  list: (householdId: string) => ["forecasts", householdId] as const,
}

export const forecastService = {
  async list(householdId: string) {
    const { data } = await api.get<ForecastEntry[]>(root(householdId))
    return data
  },
  async create(householdId: string, input: ForecastEntryInput) {
    const { data } = await api.post<ForecastEntry>(root(householdId), input)
    return data
  },
  async update(householdId: string, id: string, input: ForecastEntryInput) {
    const { data } = await api.put<ForecastEntry>(`${root(householdId)}/${id}`, input)
    return data
  },
  async remove(householdId: string, id: string) {
    await api.delete(`${root(householdId)}/${id}`)
  },
}
