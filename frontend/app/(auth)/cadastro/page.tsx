import type { Metadata } from "next"
import { Suspense } from "react"
import { AuthForm } from "@/features/auth/auth-form"

export const metadata: Metadata = { title: "Criar conta" }

export default function RegisterPage({ searchParams }: { searchParams: Promise<{ returnTo?: string }> }) {
  return <Suspense fallback={<div className="h-[32rem] animate-pulse rounded-2xl bg-muted" />}><RegisterContent searchParams={searchParams} /></Suspense>
}

async function RegisterContent({ searchParams }: { searchParams: Promise<{ returnTo?: string }> }) {
  const { returnTo } = await searchParams
  return <AuthForm mode="register" returnTo={returnTo} />
}
