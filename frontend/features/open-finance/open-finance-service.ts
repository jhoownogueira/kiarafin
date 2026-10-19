import { api } from "@/lib/api/client"
import type { PluggyIntegrationPreview, PluggyIntegrationStatus } from "@/features/open-finance/types"

const root = "/integrations/pluggy"

export const openFinanceKeys = {
  status: ["integrations", "pluggy", "status"] as const,
  preview: ["integrations", "pluggy", "preview"] as const,
}

export const openFinanceService = {
  async status() {
    const { data } = await api.get<PluggyIntegrationStatus>(`${root}/status`)
    return data
  },
  async preview() {
    const { data } = await api.post<PluggyIntegrationPreview>(`${root}/preview`)
    return data
  },
  async activate() {
    const { data } = await api.post<PluggyIntegrationStatus>(`${root}/activate`, { confirmed: true })
    return data
  },
  async sync() {
    await api.post(`${root}/sync`)
  },
}
