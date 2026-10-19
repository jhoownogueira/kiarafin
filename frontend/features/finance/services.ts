import { api } from "@/lib/api/client"
import type { Account, AccountInput, Category, CategoryInput, CategoryType, PagedResponse, Transaction, TransactionFilters, TransactionInput, TransferInput } from "@/features/finance/types"

export const financeKeys = {
  accounts: (householdId: string, page = 1, active?: boolean) => ["accounts", householdId, page, active] as const,
  accountOptions: (householdId: string) => ["accounts", householdId, "options"] as const,
  categories: (householdId: string, page = 1, type?: CategoryType | "", active?: boolean) => ["categories", householdId, page, type, active] as const,
  categoryOptions: (householdId: string) => ["categories", householdId, "options"] as const,
  transactions: (householdId: string, filters: TransactionFilters) => ["transactions", householdId, filters] as const,
}

const root = (householdId: string) => `/households/${householdId}`

export const accountService = {
  async list(householdId: string, page = 1, active?: boolean, pageSize = 20) { const { data } = await api.get<PagedResponse<Account>>(`${root(householdId)}/accounts`, { params: { page, pageSize, active } }); return data },
  async create(householdId: string, input: AccountInput) { const { data } = await api.post<Account>(`${root(householdId)}/accounts`, input); return data },
  async update(householdId: string, id: string, input: AccountInput) { const update = { name: input.name, type: input.type, institution: input.institution, color: input.color, icon: input.icon, responsibleUserId: input.responsibleUserId }; const { data } = await api.put<Account>(`${root(householdId)}/accounts/${id}`, update); return data },
  async setActive(householdId: string, id: string, active: boolean) { if (active) await api.post(`${root(householdId)}/accounts/${id}/activate`); else await api.delete(`${root(householdId)}/accounts/${id}`) },
}

export const categoryService = {
  async list(householdId: string, page = 1, type?: CategoryType | "", active?: boolean, pageSize = 20) { const { data } = await api.get<PagedResponse<Category>>(`${root(householdId)}/categories`, { params: { page, pageSize, type: type || undefined, active } }); return data },
  async create(householdId: string, input: CategoryInput) { const { data } = await api.post<Category>(`${root(householdId)}/categories`, input); return data },
  async update(householdId: string, id: string, input: CategoryInput) { const update = { name: input.name, color: input.color, icon: input.icon }; const { data } = await api.put<Category>(`${root(householdId)}/categories/${id}`, update); return data },
  async setActive(householdId: string, id: string, active: boolean) { if (active) await api.post(`${root(householdId)}/categories/${id}/activate`); else await api.delete(`${root(householdId)}/categories/${id}`) },
}

export const transactionService = {
  async list(householdId: string, filters: TransactionFilters, pageSize = 20) { const { data } = await api.get<PagedResponse<Transaction>>(`${root(householdId)}/transactions`, { params: { ...filters, status: filters.status || undefined, pageSize } }); return data },
  async listMonth(householdId: string, filters: TransactionFilters) {
    const [year, month] = (filters.from ?? "").split("-").map(Number)
    await api.post(`${root(householdId)}/recurring-transactions/generate/${year}/${month}`)

    const firstPage = await transactionService.list(householdId, { ...filters, page: 1 }, 100)
    if (firstPage.totalPages <= 1) return firstPage

    const remainingPages = await Promise.all(
      Array.from({ length: firstPage.totalPages - 1 }, (_, index) =>
        transactionService.list(householdId, { ...filters, page: index + 2 }, 100)),
    )

    return {
      ...firstPage,
      items: [firstPage, ...remainingPages].flatMap((page) => page.items),
      page: 1,
      pageSize: firstPage.totalCount,
      totalPages: firstPage.totalCount > 0 ? 1 : 0,
    }
  },
  async create(householdId: string, input: TransactionInput) {
    const payload = {
      ...input,
      settledDate: input.settledDate || null,
      notes: input.notes || null,
    }
    const { data } = await api.post<Transaction>(`${root(householdId)}/transactions`, payload)
    return data
  },
  async update(householdId: string, id: string, input: TransactionInput) { const update = { description: input.description, amount: input.amount, categoryId: input.categoryId, accountId: input.accountId, competenceDate: input.competenceDate, dueDate: input.dueDate, notes: input.notes, responsibleUserId: input.responsibleUserId, scope: input.scope }; const { data } = await api.put<Transaction>(`${root(householdId)}/transactions/${id}`, update); return data },
  async updateMetadata(householdId: string, id: string, input: Pick<TransactionInput, "categoryId" | "notes">) { const { data } = await api.patch<Transaction>(`${root(householdId)}/transactions/${id}/metadata`, { categoryId: input.categoryId, notes: input.notes || null }); return data },
  async settle(householdId: string, id: string, settledDate: string) { const { data } = await api.post<Transaction>(`${root(householdId)}/transactions/${id}/settle`, { settledDate }); return data },
  async cancel(householdId: string, id: string) { const { data } = await api.post<Transaction>(`${root(householdId)}/transactions/${id}/cancel`); return data },
  async transfer(householdId: string, input: TransferInput) { const { data } = await api.post<Transaction>(`${root(householdId)}/transactions/transfers`, input); return data },
}
