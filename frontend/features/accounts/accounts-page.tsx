"use client"

import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { CloudCheck, Edit3, History, LoaderCircle, Plus, Power, PowerOff, WalletCards } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { ConfirmDialog } from "@/components/shared/confirm-dialog"
import { FormDialog } from "@/components/shared/form-dialog"
import { Pagination } from "@/components/shared/pagination"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { accountService, financeKeys } from "@/features/finance/services"
import { useFinanceContext } from "@/features/finance/selectors"
import type { Account, AccountInput } from "@/features/finance/types"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate, formatDateTime } from "@/lib/formatters"
import { cn } from "@/lib/utils"

const schema = z.object({
  name: z.string().trim().min(2, "Informe ao menos 2 caracteres."),
  type: z.enum(["Checking", "Savings", "Cash", "Other"]),
  institution: z.string().trim().max(120).optional(),
  color: z.string().min(4),
  balance: z.number().finite("Informe o saldo."),
})
type FormData = z.infer<typeof schema>
const typeLabel = { Checking: "Conta corrente", Savings: "Poupança", Cash: "Dinheiro", Other: "Outra" } as const

export function AccountsPage() {
  const { householdId } = useFinanceContext()
  const [page, setPage] = useState(1)
  const [active, setActive] = useState<boolean | undefined>(true)
  const [editing, setEditing] = useState<Account | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [historyOf, setHistoryOf] = useState<Account | null>(null)
  const queryClient = useQueryClient()
  const accounts = useQuery({ queryKey: financeKeys.accounts(householdId, page, active), queryFn: () => accountService.list(householdId, page, active), enabled: !!householdId })
  const toggle = useMutation({
    mutationFn: ({ account, value }: { account: Account; value: boolean }) => accountService.setActive(householdId, account.id, value),
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["accounts", householdId] }); toast.success("Status da conta atualizado.") },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })
  const total = accounts.data?.items.filter((x) => x.isActive).reduce((sum, x) => sum + x.currentBalance, 0) ?? 0

  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div><p className="text-sm font-semibold text-brand-green">Organização</p><h1 className="mt-1 font-heading text-3xl font-semibold">Contas</h1><p className="mt-2 text-muted-foreground">Saldos do Open Finance e contas manuais.</p></div>
      <Button onClick={() => { setEditing(null); setFormOpen(true) }}><Plus />Nova conta manual</Button>
    </div>
    <div className="mt-6 flex flex-wrap items-center justify-between gap-3">
      <div><Label htmlFor="account-status" className="sr-only">Filtrar por status</Label><select id="account-status" className="h-10 rounded-xl border bg-background px-3 text-sm" value={active === undefined ? "all" : String(active)} onChange={(e) => { setPage(1); setActive(e.target.value === "all" ? undefined : e.target.value === "true") }}><option value="true">Ativas</option><option value="false">Inativas</option><option value="all">Todas</option></select></div>
      {accounts.data && accounts.data.items.length > 0 && <p className="text-sm text-muted-foreground">Total em contas ativas: <span className="font-semibold text-foreground">{formatCurrency(total)}</span></p>}
    </div>
    {accounts.isPending ? <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-44" />)}</div>
      : accounts.isError ? <Card className="mt-6"><CardContent className="p-6 text-sm text-destructive">{getApiErrorMessage(accounts.error, "Não foi possível carregar as contas.")}</CardContent></Card>
      : accounts.data.items.length === 0 ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-64 flex-col items-center justify-center text-center"><WalletCards className="size-10 text-muted-foreground" /><h2 className="mt-4 font-semibold">Nenhuma conta encontrada</h2><p className="mt-1 text-sm text-muted-foreground">As contas do Open Finance aparecem aqui após a sincronização.</p></CardContent></Card>
      : <><div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">{accounts.data.items.map((account) => <Card key={account.id} className={!account.isActive ? "opacity-65" : ""}><CardContent className="p-5">
        <div className="flex items-start justify-between"><div><div className="flex flex-wrap items-center gap-2"><p className="font-semibold">{account.name}</p>{account.institutionControlled && <span className="inline-flex items-center gap-1 rounded-full bg-sky-500/10 px-2 py-0.5 text-[10px] font-semibold uppercase text-sky-700 dark:text-sky-300"><CloudCheck className="size-3" />Open Finance</span>}</div><p className="text-sm text-muted-foreground">{account.institution || typeLabel[account.type]}</p></div><span className="size-3 rounded-full" style={{ backgroundColor: account.color || "#028A61" }} /></div>
        <p className="mt-5 text-xs text-muted-foreground">Saldo atual</p><p className={cn("mt-1 text-2xl font-semibold", account.currentBalance < 0 && "text-rose-500")}>{formatCurrency(account.currentBalance)}</p>
        <p className="mt-3 text-xs text-muted-foreground">{account.updatedAtUtc ? `Atualizado em ${formatDateTime(account.updatedAtUtc)}` : typeLabel[account.type]}</p>
        <div className="mt-4 flex flex-wrap gap-2 border-t pt-4">
          {account.institutionControlled
            ? <Button size="sm" variant="outline" onClick={() => setHistoryOf(account)}><History />Movimentações</Button>
            : <><Button size="sm" variant="outline" onClick={() => { setEditing(account); setFormOpen(true) }}><Edit3 />Editar</Button><ConfirmDialog trigger={<Button size="sm" variant="ghost">{account.isActive ? <PowerOff /> : <Power />}{account.isActive ? "Desativar" : "Ativar"}</Button>} title={`${account.isActive ? "Desativar" : "Ativar"} conta?`} description="A conta deixa de aparecer entre as ativas." confirmLabel={account.isActive ? "Desativar" : "Ativar"} destructive={account.isActive} pending={toggle.isPending} onConfirm={() => toggle.mutate({ account, value: !account.isActive })} /></>}
        </div>
      </CardContent></Card>)}</div><Pagination page={accounts.data.page} totalPages={accounts.data.totalPages} totalCount={accounts.data.totalCount} onPageChange={setPage} /></>}
    <AccountForm open={formOpen} onOpenChange={setFormOpen} householdId={householdId} account={editing} />
    <AccountHistory householdId={householdId} account={historyOf} onClose={() => setHistoryOf(null)} />
  </div>
}

function AccountHistory({ householdId, account, onClose }: { householdId: string; account: Account | null; onClose: () => void }) {
  const [page, setPage] = useState(1)
  const history = useQuery({ queryKey: financeKeys.accountHistory(householdId, account?.id ?? "", page), queryFn: () => accountService.history(householdId, account!.id, page), enabled: !!householdId && !!account })
  return <FormDialog open={!!account} onOpenChange={(open) => { if (!open) { setPage(1); onClose() } }} title={account ? `Movimentações · ${account.name}` : "Movimentações"} description="Lançamentos retornados pelo Open Finance.">
    {history.isPending ? <p className="text-sm text-muted-foreground">Carregando…</p> : history.isError ? <p className="text-sm text-destructive">{getApiErrorMessage(history.error)}</p> : history.data.items.length === 0 ? <p className="text-sm text-muted-foreground">Nenhuma movimentação.</p> : <>
      <div className="max-h-[60vh] divide-y overflow-y-auto">{history.data.items.map((item) => <div key={item.id} className="flex items-center justify-between gap-3 py-2.5"><div className="min-w-0"><p className="truncate text-sm font-medium">{item.description}</p><p className="text-xs text-muted-foreground">{formatDate(item.date)}{item.isPending ? " · pendente" : ""}{item.category ? ` · ${item.category}` : ""}</p></div><p className={cn("shrink-0 text-sm font-semibold", item.amount >= 0 ? "text-emerald-600 dark:text-emerald-400" : "")}>{formatCurrency(item.amount)}</p></div>)}</div>
      <Pagination page={history.data.page} totalPages={history.data.totalPages} totalCount={history.data.totalCount} onPageChange={setPage} />
    </>}
  </FormDialog>
}

function AccountForm({ open, onOpenChange, householdId, account }: { open: boolean; onOpenChange: (v: boolean) => void; householdId: string; account: Account | null }) {
  const queryClient = useQueryClient()
  const form = useForm<FormData>({ resolver: zodResolver(schema), values: account ? { name: account.name, type: account.type, institution: account.institution ?? "", color: account.color ?? "#028A61", balance: account.currentBalance } : { name: "", type: "Checking", institution: "", color: "#028A61", balance: 0 } })
  const mutation = useMutation({
    mutationFn: (values: FormData) => { const input: AccountInput = { ...values, icon: "Wallet" }; return account ? accountService.update(householdId, account.id, input) : accountService.create(householdId, input) },
    onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["accounts", householdId] }); toast.success(account ? "Conta atualizada." : "Conta criada."); onOpenChange(false) },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })
  return <FormDialog open={open} onOpenChange={onOpenChange} title={account ? "Editar conta" : "Nova conta manual"} description="Contas manuais têm o saldo informado por você.">
    <form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((v) => mutation.mutate(v))}>
      <Field label="Nome" error={form.formState.errors.name?.message}><Input {...form.register("name")} /></Field>
      <Field label="Tipo"><select className="h-11 w-full rounded-xl border bg-background px-3 text-sm" {...form.register("type")}>{Object.entries(typeLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></Field>
      <Field label="Instituição"><Input {...form.register("institution")} /></Field>
      <Field label="Cor"><Input type="color" className="p-1" {...form.register("color")} /></Field>
      <div className="sm:col-span-2"><Field label="Saldo atual" error={form.formState.errors.balance?.message}><Input type="number" step="0.01" {...form.register("balance", { valueAsNumber: true })} /></Field></div>
      <div className="flex justify-end sm:col-span-2"><Button type="submit" disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Salvar</Button></div>
    </form>
  </FormDialog>
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) { return <label className="space-y-2 text-sm font-medium"><span>{label}</span>{children}{error && <span className="block text-xs text-destructive">{error}</span>}</label> }
