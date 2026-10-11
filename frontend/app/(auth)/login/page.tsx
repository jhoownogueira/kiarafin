import type { Metadata } from "next"
import { Suspense } from "react"
import { AuthForm } from "@/features/auth/auth-form"

export const metadata: Metadata = { title: "Entrar" }

export default function LoginPage({ searchParams }: { searchParams: Promise<{ returnTo?: string }> }) {
  return <Suspense fallback={<div className="h-96 animate-pulse rounded-2xl bg-muted" />}><LoginContent searchParams={searchParams} /></Suspense>
}

async function LoginContent({ searchParams }: { searchParams: Promise<{ returnTo?: string }> }) {
  const { returnTo } = await searchParams
  return <AuthForm mode="login" returnTo={returnTo} />
}
