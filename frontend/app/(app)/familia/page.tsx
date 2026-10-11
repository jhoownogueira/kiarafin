import type { Metadata } from "next"
import { HouseholdPage } from "@/features/households/household-page"

export const metadata: Metadata = { title: "Família" }
export const instant = false
export default function FamilyPage() { return <HouseholdPage /> }
