export type PagedResponse<T> = { items: T[]; page: number; pageSize: number; totalCount: number; totalPages: number }
export type AccountType = "Checking" | "Savings" | "Cash" | "Other"
export type CategoryType = "Income" | "Expense"
export type TransactionType = "Income" | "Expense" | "Transfer" | "InvoicePayment"
export type TransactionStatus = "Pending" | "Paid" | "Received" | "Overdue" | "Canceled"
export type ExpenseScope = "Individual" | "Shared"

export type Account = { id: string; name: string; type: AccountType; institution: string | null; color: string | null; icon: string | null; initialBalance: number; initialBalanceDate: string; currentBalance: number; income: number; expenses: number; responsibleUserId: string; isActive: boolean }
export type AccountInput = { name: string; type: AccountType; institution?: string | null; color?: string | null; icon?: string | null; initialBalance?: number; initialBalanceDate?: string; responsibleUserId: string }
export type Category = { id: string; name: string; type: CategoryType; color: string; icon: string; isSystem: boolean; isActive: boolean }
export type CategoryInput = { name: string; type?: CategoryType; color: string; icon: string }
export type Transaction = { id: string; description: string; amount: number; type: TransactionType; status: TransactionStatus; categoryId: string | null; accountId: string | null; destinationAccountId: string | null; competenceDate: string; dueDate: string; settledDate: string | null; notes: string | null; responsibleUserId: string; scope: ExpenseScope; clientRequestId: string | null; createdAtUtc: string }
export type TransactionInput = { description: string; amount: number; type?: "Income" | "Expense"; categoryId: string; accountId: string; competenceDate: string; dueDate: string; settledDate?: string | null; notes?: string | null; responsibleUserId: string; scope: ExpenseScope }
export type TransferInput = { clientRequestId: string; description: string; amount: number; sourceAccountId: string; destinationAccountId: string; transferDate: string; responsibleUserId: string; notes?: string | null }
export type TransactionFilters = { page: number; from?: string; to?: string; accountId?: string; categoryId?: string; responsibleUserId?: string; status?: TransactionStatus | "" }
