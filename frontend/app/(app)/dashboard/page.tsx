import type { Metadata } from "next"
import { BalanceForecastPage } from "@/features/forecasts/balance-forecast-page"

export const metadata: Metadata = { title: "Visão geral" }
export const instant = false

export default function DashboardPage() {
  return <BalanceForecastPage />
}
