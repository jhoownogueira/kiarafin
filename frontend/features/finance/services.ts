import { api } from "@/lib/api/client"
import type { Account, AccountInput, AccountTransaction, Category, CategoryInput, CategoryType, PagedResponse } from "@/features/finance/types"

export const financeKeys = {
  accounts: (householdId: string, page = 1, active?: boolean) => ["accounts", householdId, page, active] as const,
  accountHistory: (householdId: string, accountId: string, page: number) => ["accounts", householdId, accountId, "history", page] as const,
  categories: (householdId: string, page = 1, type?: CategoryType | "", active?: boolean) => ["categories", householdId, page, type, active] as const,
}

const root = (householdId: string) => `/households/${householdId}`

export const accountService = {
  async list(householdId: string, page = 1, active?: boolean, pageSize = 20) {
    const { data } = await api.get<PagedResponse<Account>>(`${root(householdId)}/accounts`, { params: { page, pageSize, active } })
    return data
  },
  async history(householdId: string, accountId: string, page = 1, pageSize = 30) {
    const { data } = await api.get<PagedResponse<AccountTransaction>>(`${root(householdId)}/accounts/${accountId}/history`, { params: { page, pageSize } })
    return data
  },
  async create(householdId: string, input: AccountInput) {
    const { data } = await api.post<Account>(`${root(householdId)}/accounts`, input)
    return data
  },
  async update(householdId: string, id: string, input: AccountInput) {
    const { data } = await api.put<Account>(`${root(householdId)}/accounts/${id}`, input)
    return data
  },
  async setActive(householdId: string, id: string, active: boolean) {
    if (active) await api.post(`${root(householdId)}/accounts/${id}/activate`)
    else await api.delete(`${root(householdId)}/accounts/${id}`)
  },
}

export const categoryService = {
  async list(householdId: string, page = 1, type?: CategoryType | "", active?: boolean, pageSize = 20) {
    const { data } = await api.get<PagedResponse<Category>>(`${root(householdId)}/categories`, { params: { page, pageSize, type: type || undefined, active } })
    return data
  },
  async create(householdId: string, input: CategoryInput) {
    const { data } = await api.post<Category>(`${root(householdId)}/categories`, input)
    return data
  },
  async update(householdId: string, id: string, input: CategoryInput) {
    const { data } = await api.put<Category>(`${root(householdId)}/categories/${id}`, { name: input.name, color: input.color, icon: input.icon })
    return data
  },
  async setActive(householdId: string, id: string, active: boolean) {
    if (active) await api.post(`${root(householdId)}/categories/${id}/activate`)
    else await api.delete(`${root(householdId)}/categories/${id}`)
  },
}
