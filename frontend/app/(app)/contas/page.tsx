import type { Metadata } from "next"
import { AccountsPage } from "@/features/accounts/accounts-page"

export const metadata: Metadata = { title: "Contas" }
export const instant = false
export default function Page() { return <AccountsPage /> }
