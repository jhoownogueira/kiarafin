"use client"

import { useState, type ReactNode } from "react"
import Link from "next/link"
import { usePathname, useRouter } from "next/navigation"
import * as Dialog from "@radix-ui/react-dialog"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { BarChart3, CalendarClock, CreditCard, LayoutDashboard, LogOut, Menu, ReceiptText, Settings, Tags, UsersRound, WalletCards, X } from "lucide-react"
import { toast } from "sonner"

import { BrandLogo } from "@/components/brand/brand-logo"
import { Button } from "@/components/ui/button"
import { ThemeToggle } from "@/components/shared/theme-toggle"
import { authQueryKey, authService } from "@/features/auth/auth-service"
import type { AuthUser } from "@/features/auth/types"
import { OpenFinanceIndicator } from "@/features/open-finance/open-finance-indicator"
import { cn } from "@/lib/utils"
import { useHouseholdSelection } from "@/providers/household-provider"

const navigation = [
  { label: "Visão geral", href: "/dashboard", icon: LayoutDashboard },
  { label: "Contas", href: "/contas", icon: WalletCards },
  { label: "Lançamentos", href: "/transacoes", icon: ReceiptText },
  { label: "Categorias", href: "/categorias", icon: Tags },
  { label: "Família", href: "/familia", icon: UsersRound },
  { label: "Previsões", href: "/previsoes", icon: CalendarClock },
  { label: "Cartões", href: "/cartoes", icon: CreditCard },
  { label: "Relatórios", href: "/relatorios", icon: BarChart3 },
] as const

const AUTHORIZED_OPEN_FINANCE_EMAIL = "jhoownogueira@outlook.com"

function NavContent({ user, onNavigate }: { user: AuthUser; onNavigate?: () => void }) {
  const pathname = usePathname()
  const items = user.email.toLowerCase() === AUTHORIZED_OPEN_FINANCE_EMAIL
    ? [...navigation, { label: "Open Finance", href: "/configuracoes/open-finance", icon: Settings }]
    : navigation
  return (
    <nav aria-label="Navegação principal" className="space-y-1">
      {items.map((item) => {
        const Icon = item.icon
        const active = pathname === item.href
        return <Link key={item.label} href={item.href} onClick={onNavigate} aria-current={active ? "page" : undefined} className={cn("flex h-11 items-center gap-3 rounded-xl px-3 text-sm font-medium transition-colors", active ? "bg-sidebar-primary text-sidebar-primary-foreground" : "hover:bg-sidebar-accent hover:text-sidebar-accent-foreground")}><Icon />{item.label}</Link>
      })}
    </nav>
  )
}

export function AppShell({ user, children }: { user: AuthUser; children: ReactNode }) {
  const [mobileOpen, setMobileOpen] = useState(false)
  const router = useRouter()
  const queryClient = useQueryClient()
  const { selectedHouseholdId } = useHouseholdSelection()
  const selectedHousehold = user.households.find((item) => item.id === selectedHouseholdId) ?? user.households.at(-1)
  const logout = useMutation({
    mutationFn: authService.logout,
    onSuccess: () => {
      queryClient.removeQueries({ queryKey: authQueryKey })
      router.replace("/login")
    },
    onError: () => toast.error("Não foi possível encerrar a sessão."),
  })
  const initials = user.name.split(" ").filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase()

  return (
    <div className="min-h-dvh md:grid md:grid-cols-[260px_1fr]">
      <aside className="fixed inset-y-0 left-0 z-30 hidden w-[260px] flex-col border-r border-sidebar-border bg-sidebar p-4 text-sidebar-foreground md:flex">
        <div className="mb-8 rounded-xl bg-brand-canvas p-2.5"><BrandLogo className="h-11 w-[190px]" priority /></div>
        <div className="flex-1"><NavContent user={user} /></div>
        <div className="border-t border-sidebar-border pt-4">
          <div className="mb-3 flex items-center gap-3 px-2">
            <div className="flex size-9 items-center justify-center rounded-full bg-sidebar-primary text-xs font-bold text-sidebar-primary-foreground">{initials}</div>
            <div className="min-w-0 flex-1"><p className="truncate text-sm font-semibold">{user.name}</p><p className="truncate text-xs text-muted-foreground">{user.email}</p></div>
          </div>
          <Button variant="ghost" className="w-full justify-start" onClick={() => logout.mutate()} disabled={logout.isPending}><LogOut />Sair</Button>
        </div>
      </aside>

      <div className="md:col-start-2">
        <header className="sticky top-0 z-20 flex h-16 items-center justify-between border-b bg-background/90 px-4 backdrop-blur md:px-8">
          <div className="flex items-center gap-3 md:hidden">
            <Dialog.Root open={mobileOpen} onOpenChange={setMobileOpen}>
              <Dialog.Trigger asChild><Button variant="ghost" size="icon" aria-label="Abrir menu"><Menu /></Button></Dialog.Trigger>
              <Dialog.Portal>
                <Dialog.Overlay className="fixed inset-0 z-40 bg-black/45 data-[state=open]:animate-in data-[state=closed]:animate-out" />
                <Dialog.Content className="fixed inset-y-0 left-0 z-50 flex w-[min(88vw,320px)] flex-col border-r border-sidebar-border bg-sidebar p-4 text-sidebar-foreground shadow-2xl data-[state=open]:animate-in data-[state=open]:slide-in-from-left data-[state=closed]:animate-out data-[state=closed]:slide-out-to-left">
                  <Dialog.Title className="sr-only">Menu principal</Dialog.Title>
                  <div className="mb-8 flex items-center justify-between rounded-xl bg-brand-canvas p-2.5"><BrandLogo className="h-10 w-[175px]" /><Dialog.Close asChild><Button variant="ghost" size="icon" className="text-brand-ink" aria-label="Fechar menu"><X /></Button></Dialog.Close></div>
                  <div className="flex-1"><NavContent user={user} onNavigate={() => setMobileOpen(false)} /></div>
                  <Button variant="ghost" className="justify-start" onClick={() => logout.mutate()}><LogOut />Sair</Button>
                </Dialog.Content>
              </Dialog.Portal>
            </Dialog.Root>
            <BrandLogo variant="icon" className="h-9 w-10 rounded-lg bg-brand-canvas p-1" />
          </div>
          <div className="hidden md:block"><p className="text-sm text-muted-foreground">{selectedHousehold?.name ?? "Meu núcleo financeiro"}</p></div>
          <div className="flex items-center gap-2">{user.email.toLowerCase() === AUTHORIZED_OPEN_FINANCE_EMAIL && <OpenFinanceIndicator />}<ThemeToggle /></div>
        </header>
        <main className="mx-auto w-full max-w-[1440px] p-4 sm:p-6 md:p-8">{children}</main>
      </div>
    </div>
  )
}
