import { api } from "@/lib/api/client"
import type { Household, HouseholdInvitation, HouseholdMember } from "@/features/households/types"

export const householdKeys = {
  all: ["households"] as const,
  members: (householdId: string) => [...householdKeys.all, householdId, "members"] as const,
}

export const householdService = {
  async getMembers(householdId: string) {
    const { data } = await api.get<HouseholdMember[]>(`/households/${householdId}/members`)
    return data
  },
  async createInvitation(householdId: string, email?: string) {
    const { data } = await api.post<HouseholdInvitation>(`/households/${householdId}/invitations`, {
      email: email || null,
    })
    return data
  },
  async acceptInvitation(token: string) {
    const { data } = await api.post<Household>("/households/invitations/accept", { token })
    return data
  },
}
