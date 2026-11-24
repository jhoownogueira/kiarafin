export type PagedResponse<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type AccountType = "Checking" | "Savings" | "Cash" | "Other"
export type CategoryType = "Income" | "Expense"

export type Account = {
  id: string
  name: string
  type: AccountType
  institution: string | null
  color: string | null
  icon: string | null
  currentBalance: number
  isActive: boolean
  sourceProvider: string | null
  institutionControlled: boolean
  updatedAtUtc: string | null
}
export type AccountInput = { name: string; type: AccountType; institution?: string | null; color?: string | null; icon?: string | null; balance: number }
export type AccountTransaction = { id: string; description: string; amount: number; date: string; isPending: boolean; category: string | null }

export type Category = { id: string; name: string; type: CategoryType; color: string; icon: string; isSystem: boolean; isActive: boolean }
export type CategoryInput = { name: string; type?: CategoryType; color: string; icon: string }
