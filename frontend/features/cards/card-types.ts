export type InvoiceStatus = "Upcoming" | "Open" | "Closed" | "Paid" | "Canceled"

export type InvoiceSummary = {
  id: string
  closingDate: string | null
  dueDate: string
  totalAmount: number
  status: InvoiceStatus
  paidDate: string | null
}

export type InvoicePurchase = {
  id: string
  installmentPlanId: string | null
  cardSubscriptionId: string | null
  description: string
  amount: number
  totalPurchaseAmount: number
  competenceDate: string
  categoryId: string
  responsibleUserId: string
  scope: "Individual" | "Shared"
  installmentNumber: number | null
  installmentCount: number | null
  isRefund: boolean
}

export type InvoiceDetail = {
  id: string
  creditCardId: string
  cycleStartDate: string | null
  closingDate: string | null
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
  totalLimit: number | null
  usedLimit: number | null
  availableLimit: number | null
  closingDay: number | null
  dueDay: number | null
  responsibleUserId: string
  defaultPaymentAccountId: string | null
  status: "Active" | "Inactive"
  invoices: InvoiceSummary[]
  sourceProvider: string | null
  externalId: string | null
  institutionControlled: boolean
}

export const invoiceStatusLabel: Record<InvoiceStatus, string> = {
  Upcoming: "Em formação",
  Open: "Aberta",
  Closed: "Fechada",
  Paid: "Paga",
  Canceled: "Cancelada",
}

export type CreditCardSubscription = {
  id: string
  creditCardId: string
  name: string
  amount: number
  chargeDay: number
  startDate: string
  endDate: string | null
  categoryId: string
  responsibleUserId: string
  scope: "Individual" | "Shared"
  status: "Active" | "Paused" | "Ended"
}
