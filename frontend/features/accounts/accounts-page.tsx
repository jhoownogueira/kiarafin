"use client"

import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Edit3, LoaderCircle, Plus, Power, PowerOff, WalletCards } from "lucide-react"
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
import { ResponsibleSelect, useFinanceContext } from "@/features/finance/selectors"
import type { Account, AccountInput } from "@/features/finance/types"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate, todayIso } from "@/lib/formatters"

const schema = z.object({ name: z.string().trim().min(2, "Informe ao menos 2 caracteres."), type: z.enum(["Checking", "Savings", "Cash", "Other"]), institution: z.string().trim().max(120).optional(), color: z.string().min(4), icon: z.string().trim().min(1), initialBalance: z.number().finite(), initialBalanceDate: z.string().min(1, "Informe a data."), responsibleUserId: z.string().uuid("Selecione o responsável.") })
type FormData = z.infer<typeof schema>
const typeLabel = { Checking: "Conta corrente", Savings: "Poupança", Cash: "Dinheiro", Other: "Outra" } as const

export function AccountsPage() {
  const { householdId, members } = useFinanceContext()
  const [page, setPage] = useState(1), [active, setActive] = useState<boolean | undefined>(true)
  const [editing, setEditing] = useState<Account | null>(null), [formOpen, setFormOpen] = useState(false)
  const queryClient = useQueryClient()
  const accounts = useQuery({ queryKey: financeKeys.accounts(householdId, page, active), queryFn: () => accountService.list(householdId, page, active), enabled: !!householdId })
  const toggle = useMutation({ mutationFn: ({ account, value }: { account: Account; value: boolean }) => accountService.setActive(householdId, account.id, value), onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["accounts", householdId] }); toast.success("Status da conta atualizado.") }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><p className="text-sm font-semibold text-brand-green">Organização</p><h1 className="mt-1 font-heading text-3xl font-semibold">Contas financeiras</h1><p className="mt-2 text-muted-foreground">Saldos calculados e retornados pela API.</p></div><Button onClick={() => { setEditing(null); setFormOpen(true) }}><Plus />Nova conta</Button></div>
    <div className="mt-6"><Label htmlFor="account-status" className="sr-only">Filtrar por status</Label><select id="account-status" className="h-10 rounded-xl border bg-background px-3 text-sm" value={active === undefined ? "all" : String(active)} onChange={(e) => { setPage(1); setActive(e.target.value === "all" ? undefined : e.target.value === "true") }}><option value="true">Ativas</option><option value="false">Inativas</option><option value="all">Todas</option></select></div>
    {accounts.isPending ? <div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">{Array.from({ length: 3 }).map((_, i) => <Skeleton key={i} className="h-44" />)}</div> : accounts.isError ? <Card className="mt-6"><CardContent className="p-6 text-sm text-destructive">{getApiErrorMessage(accounts.error, "Não foi possível carregar as contas.")}</CardContent></Card> : accounts.data.items.length === 0 ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-64 flex-col items-center justify-center text-center"><WalletCards className="size-10 text-muted-foreground" /><h2 className="mt-4 font-semibold">Nenhuma conta encontrada</h2><p className="mt-1 text-sm text-muted-foreground">Crie sua primeira conta para começar.</p></CardContent></Card> : <><div className="mt-6 grid gap-4 md:grid-cols-2 xl:grid-cols-3">{accounts.data.items.map((account) => <Card key={account.id} className={!account.isActive ? "opacity-65" : ""}><CardContent className="p-5"><div className="flex items-start justify-between"><div><p className="font-semibold">{account.name}</p><p className="text-sm text-muted-foreground">{account.institution || typeLabel[account.type]}</p></div><span className="size-3 rounded-full" style={{ backgroundColor: account.color || "#028A61" }} /></div><p className="mt-5 text-xs text-muted-foreground">Saldo atual</p><p className="mt-1 text-2xl font-semibold">{formatCurrency(account.currentBalance)}</p><div className="mt-4 flex justify-between text-xs text-muted-foreground"><span>Inicial em {formatDate(account.initialBalanceDate)}</span><span>{account.isActive ? "Ativa" : "Inativa"}</span></div><div className="mt-4 flex gap-2 border-t pt-4"><Button size="sm" variant="outline" onClick={() => { setEditing(account); setFormOpen(true) }}><Edit3 />Editar</Button><ConfirmDialog trigger={<Button size="sm" variant="ghost">{account.isActive ? <PowerOff /> : <Power />}{account.isActive ? "Desativar" : "Ativar"}</Button>} title={`${account.isActive ? "Desativar" : "Ativar"} conta?`} description="O histórico financeiro será preservado." confirmLabel={account.isActive ? "Desativar" : "Ativar"} destructive={account.isActive} pending={toggle.isPending} onConfirm={() => toggle.mutate({ account, value: !account.isActive })} /></div></CardContent></Card>)}</div><Pagination page={accounts.data.page} totalPages={accounts.data.totalPages} totalCount={accounts.data.totalCount} onPageChange={setPage} /></>}
    <AccountForm open={formOpen} onOpenChange={setFormOpen} householdId={householdId} members={members} account={editing} />
  </div>
}

function AccountForm({ open, onOpenChange, householdId, members, account }: { open: boolean; onOpenChange: (v: boolean) => void; householdId: string; members: ReturnType<typeof useFinanceContext>["members"]; account: Account | null }) {
  const queryClient = useQueryClient()
  const form = useForm<FormData>({ resolver: zodResolver(schema), values: account ? { name: account.name, type: account.type, institution: account.institution ?? "", color: account.color ?? "#028A61", icon: account.icon ?? "Wallet", initialBalance: account.initialBalance, initialBalanceDate: account.initialBalanceDate, responsibleUserId: account.responsibleUserId } : { name: "", type: "Checking", institution: "", color: "#028A61", icon: "Wallet", initialBalance: 0, initialBalanceDate: todayIso(), responsibleUserId: members[0]?.userId ?? "" } })
  const mutation = useMutation({ mutationFn: (values: FormData) => account ? accountService.update(householdId, account.id, values as AccountInput) : accountService.create(householdId, values as AccountInput), onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["accounts", householdId] }); toast.success(account ? "Conta atualizada." : "Conta criada."); onOpenChange(false) }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  return <FormDialog open={open} onOpenChange={onOpenChange} title={account ? "Editar conta" : "Nova conta"} description={account ? "O saldo inicial não é alterado na edição." : "Informe o ponto de partida da conta."}><form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((v) => mutation.mutate(v))}><Field label="Nome" error={form.formState.errors.name?.message}><Input {...form.register("name")} /></Field><Field label="Tipo"><select className="h-11 w-full rounded-xl border bg-background px-3 text-sm" {...form.register("type")}>{Object.entries(typeLabel).map(([v, l]) => <option key={v} value={v}>{l}</option>)}</select></Field><Field label="Instituição"><Input {...form.register("institution")} /></Field><Field label="Cor"><Input type="color" className="p-1" {...form.register("color")} /></Field><Field label="Saldo inicial"><Input type="number" step="0.01" disabled={!!account} {...form.register("initialBalance", { valueAsNumber: true })} /></Field><Field label="Data do saldo inicial"><Input type="date" disabled={!!account} {...form.register("initialBalanceDate")} /></Field><div className="sm:col-span-2"><Field label="Responsável" error={form.formState.errors.responsibleUserId?.message}><ResponsibleSelect members={members} {...form.register("responsibleUserId")} /></Field></div><div className="flex justify-end sm:col-span-2"><Button type="submit" disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Salvar</Button></div></form></FormDialog>
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) { return <label className="space-y-2 text-sm font-medium"><span>{label}</span>{children}{error && <span className="block text-xs text-destructive">{error}</span>}</label> }
