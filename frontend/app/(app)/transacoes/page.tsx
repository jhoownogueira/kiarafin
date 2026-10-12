import type { Metadata } from "next"
import { TransactionsPage as TransactionsFeature } from "@/features/transactions/transactions-page"

export const metadata: Metadata = { title: "Lançamentos" }
export const instant = false

export default function TransactionsPage() {
  return <TransactionsFeature />
}
