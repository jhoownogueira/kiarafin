import type { Metadata } from "next"
import { Suspense } from "react"
import { AcceptInvitation } from "@/features/households/accept-invitation"

export const metadata: Metadata = { title: "Convite para família", robots: { index: false, follow: false } }

type InvitationPageProps = { params: Promise<{ token: string }> }

export default function InvitationPage({ params }: InvitationPageProps) {
  return <Suspense fallback={<main className="min-h-dvh bg-background p-8"><div className="mx-auto h-96 max-w-lg animate-pulse rounded-2xl bg-muted" /></main>}><InvitationContent params={params} /></Suspense>
}

async function InvitationContent({ params }: InvitationPageProps) {
  const { token } = await params
  return <AcceptInvitation token={token} />
}
