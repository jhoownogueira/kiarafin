import type { Metadata } from "next"
import { RecurringPage } from "@/features/recurring/recurring-page"
export const metadata: Metadata = { title: "Recorrências" }
export const instant = false
export default function Page() { return <RecurringPage /> }
