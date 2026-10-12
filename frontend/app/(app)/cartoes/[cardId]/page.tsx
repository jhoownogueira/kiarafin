import type { Metadata } from "next"
import { CardDetailPage } from "@/features/cards/card-detail-page"

export const metadata: Metadata = { title: "Detalhes do cartão" }
export const instant = false

export default async function Page({ params }: { params: Promise<{ cardId: string }> }) {
  const { cardId } = await params
  return <CardDetailPage cardId={cardId} />
}
