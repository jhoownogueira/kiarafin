import type { Metadata } from "next"
import { ForecastsPage } from "@/features/forecasts/forecasts-page"

export const metadata: Metadata = { title: "Previsões" }
export const instant = false

export default function Page() { return <ForecastsPage /> }
