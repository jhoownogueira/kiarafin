import type { Metadata } from "next"
import { CardsPage } from "@/features/cards/cards-page"
export const metadata: Metadata = { title: "Cartões" }
export default function Page() { return <CardsPage /> }
