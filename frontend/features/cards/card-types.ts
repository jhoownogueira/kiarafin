export type InvoiceStatus = "Upcoming" | "Open" | "Closed" | "Paid" | "Canceled"

export type InvoiceSummary = {
  id: string
  closingDate: string
  dueDate: string
  totalAmount: number
  status: InvoiceStatus
  paidDate: string | null
}

export type InvoicePurchase = {
  id: string
  installmentPlanId: string | null
  description: string
  amount: number
  totalPurchaseAmount: number
  competenceDate: string
  categoryId: string
  responsibleUserId: string
  scope: "Individual" | "Shared"
  installmentNumber: number | null
  installmentCount: number | null
}

export type InvoiceDetail = {
  id: string
  creditCardId: string
  cycleStartDate: string
  closingDate: string
  dueDate: string
  totalAmount: number
  status: InvoiceStatus
  paidDate: string | null
  paymentTransactionId: string | null
  purchases: InvoicePurchase[]
}

export type CreditCard = {
  id: string
  name: string
  institution: string | null
  brand: string | null
  color: string | null
  totalLimit: number
  usedLimit: number
  availableLimit: number
  closingDay: number
  dueDay: number
  responsibleUserId: string
  defaultPaymentAccountId: string
  status: "Active" | "Inactive"
  invoices: InvoiceSummary[]
}

export const invoiceStatusLabel: Record<InvoiceStatus, string> = {
  Upcoming: "Em formação",
  Open: "Aberta",
  Closed: "Fechada",
  Paid: "Paga",
  Canceled: "Cancelada",
}
