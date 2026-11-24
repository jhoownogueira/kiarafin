import { api } from "@/lib/api/client"

export type CardTransactionKind = "Purchase" | "Credit" | "Payment"

export type CardItem = {
  id: string
  description: string
  amount: number
  effect: number
  kind: CardTransactionKind
  date: string
  purchaseDate: string | null
  isPending: boolean
  installmentNumber: number | null
  totalInstallments: number | null
  category: string | null
  currencyCode: string | null
  originalAmount: number | null
  projected: boolean
}

export type OpenInvoice = { total: number; closingDate: string | null; dueDate: string | null; itemCount: number; items: CardItem[] | null }

export type ClosedInvoice = {
  id: string
  dueDate: string
  closingDate: string | null
  totalAmount: number
  minimumPayment: number | null
  paidAmount: number
  outstanding: number
  isPaid: boolean
  itemsTotal: number
  items: CardItem[] | null
}

export type CardSummary = {
  id: string
  name: string
  institution: string | null
  brand: string | null
  number: string | null
  creditLimit: number | null
  availableLimit: number | null
  reportedBalance: number
  isActive: boolean
  updatedAtUtc: string | null
  currentInvoice: OpenInvoice
  outstandingClosedInvoices: number
  lastClosedInvoice: ClosedInvoice | null
}

export type CardDetail = {
  card: CardSummary
  currentInvoice: OpenInvoice
  upcoming: CardItem[]
  closedInvoices: ClosedInvoice[]
  payments: CardItem[]
}

const root = (householdId: string) => `/households/${householdId}/cards`

export const cardKeys = {
  list: (householdId: string) => ["cards", householdId] as const,
  detail: (householdId: string, cardId: string) => ["cards", householdId, cardId] as const,
}

export const cardService = {
  async list(householdId: string) { return (await api.get<CardSummary[]>(root(householdId))).data },
  async get(householdId: string, cardId: string) { return (await api.get<CardDetail>(`${root(householdId)}/${cardId}`)).data },
}

export const kindLabel: Record<CardTransactionKind, string> = { Purchase: "Compra", Credit: "Estorno/crédito", Payment: "Pagamento" }
