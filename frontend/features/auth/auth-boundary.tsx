"use client"

import { useEffect, type ReactNode } from "react"
import { useRouter } from "next/navigation"
import axios from "axios"
import { useQuery } from "@tanstack/react-query"
import { AlertCircle, RefreshCw } from "lucide-react"

import { BrandLogo } from "@/components/brand/brand-logo"
import { AppShell } from "@/components/layout/app-shell"
import { Button } from "@/components/ui/button"
import { Skeleton } from "@/components/ui/skeleton"
import { authQueryKey, authService } from "@/features/auth/auth-service"

export function AuthBoundary({ children }: { children: ReactNode }) {
  const router = useRouter()
  const session = useQuery({ queryKey: authQueryKey, queryFn: authService.me, retry: false })
  const unauthorized = axios.isAxiosError(session.error) && session.error.response?.status === 401

  useEffect(() => {
    if (unauthorized) router.replace("/login")
  }, [router, unauthorized])

  if (session.isPending || unauthorized) return <SessionLoading />
  if (session.isError) {
    return (
      <main className="flex min-h-dvh items-center justify-center p-6">
        <div className="max-w-sm text-center"><AlertCircle className="mx-auto size-10 text-destructive" /><h1 className="mt-4 font-heading text-2xl font-semibold">Não foi possível carregar sua sessão</h1><p className="mt-2 text-sm text-muted-foreground">Confira se a API está disponível e tente novamente.</p><Button className="mt-6" onClick={() => session.refetch()}><RefreshCw />Tentar novamente</Button></div>
      </main>
    )
  }
  return <AppShell user={session.data}>{children}</AppShell>
}

function SessionLoading() {
  return (
    <div className="grid min-h-dvh md:grid-cols-[260px_1fr]">
      <aside className="hidden border-r bg-sidebar p-4 md:block"><div className="rounded-xl bg-brand-canvas p-2.5"><BrandLogo className="h-11 w-[190px]" priority /></div><div className="mt-8 space-y-3">{Array.from({ length: 6 }).map((_, index) => <Skeleton key={index} className="h-11" />)}</div></aside>
      <main className="p-6 md:p-8"><Skeleton className="h-8 w-48" /><div className="mt-8 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">{Array.from({ length: 4 }).map((_, index) => <Skeleton key={index} className="h-32" />)}</div></main>
    </div>
  )
}
