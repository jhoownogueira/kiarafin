import type { Metadata } from "next"
import { DashboardPage as DashboardFeature } from "@/features/dashboard/dashboard-page"

export const metadata: Metadata = { title: "Visão geral" }
export const instant = false

export default function DashboardPage() {
  return <DashboardFeature />
}
