"use client"

import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { addMonths, endOfMonth, format, isSameMonth, startOfMonth } from "date-fns"
import { ptBR } from "date-fns/locale"
import { ArrowLeft, ArrowRight, ArrowRightLeft, CalendarClock, Check, CloudCheck, Edit3, LoaderCircle, Plus, ReceiptText, RotateCcw, X } from "lucide-react"
import { useForm, useWatch } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { ConfirmDialog } from "@/components/shared/confirm-dialog"
import { FormDialog } from "@/components/shared/form-dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { AccountSelect, CategorySelect, ResponsibleSelect, useFinanceContext } from "@/features/finance/selectors"
import { financeKeys, transactionService } from "@/features/finance/services"
import type { Transaction, TransactionInput, TransactionStatus, TransactionType, TransferInput } from "@/features/finance/types"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate, todayIso } from "@/lib/formatters"

const transactionSchema = z.object({
  description: z.string().trim().min(2), amount: z.number().positive(), type: z.enum(["Income", "Expense"]),
  categoryId: z.string().uuid(), accountId: z.string().uuid(), competenceDate: z.string().min(1), dueDate: z.string().min(1),
  settledDate: z.string().nullable().optional(), notes: z.string().max(1000).optional(), responsibleUserId: z.string().uuid(),
  scope: z.enum(["Individual", "Shared"]),
})
const institutionMetadataSchema = transactionSchema.extend({ accountId: z.string() })
const transferSchema = z.object({
  description: z.string().trim().min(2), amount: z.number().positive(), sourceAccountId: z.string().uuid(),
  destinationAccountId: z.string().uuid(), transferDate: z.string().min(1), responsibleUserId: z.string().uuid(),
  notes: z.string().max(1000).optional(),
}).refine((value) => value.sourceAccountId !== value.destinationAccountId, { path: ["destinationAccountId"], message: "Selecione contas diferentes." })

type TransactionFormData = z.infer<typeof transactionSchema>
type TransferFormData = z.infer<typeof transferSchema>

const statusLabel: Record<TransactionStatus, string> = { Pending: "Pendente", Paid: "Pago", Received: "Recebido", Overdue: "Vencido", Canceled: "Cancelado" }
const typeLabel: Record<TransactionType, string> = { Income: "Receita", Expense: "Despesa", Transfer: "Transferência", InvoicePayment: "Pagamento de fatura" }
const statusStyle: Record<TransactionStatus, string> = {
  Pending: "bg-amber-500/10 text-amber-700 dark:text-amber-300", Overdue: "bg-destructive/10 text-destructive",
  Paid: "bg-brand-green/10 text-brand-green", Received: "bg-brand-green/10 text-brand-green", Canceled: "bg-muted text-muted-foreground",
}

const toIso = (date: Date) => format(date, "yyyy-MM-dd")
const isSettled = (transaction: Transaction) => transaction.status === "Paid" || transaction.status === "Received"
function monthTitle(date: Date) { const value = format(date, "MMMM 'de' yyyy", { locale: ptBR }); return value.charAt(0).toUpperCase() + value.slice(1) }

export function TransactionsPage() {
  const { householdId, accounts, categories, members } = useFinanceContext()
  const [selectedMonth, setSelectedMonth] = useState(() => startOfMonth(new Date()))
  const [status, setStatus] = useState<TransactionStatus | "">("")
  const [editing, setEditing] = useState<Transaction | null>(null)
  const [formOpen, setFormOpen] = useState(false)
  const [transferOpen, setTransferOpen] = useState(false)
  const queryClient = useQueryClient()
  const filters = { page: 1, from: toIso(startOfMonth(selectedMonth)), to: toIso(endOfMonth(selectedMonth)), status: "" as const }
  const transactions = useQuery({ queryKey: financeKeys.transactions(householdId, filters), queryFn: () => transactionService.listMonth(householdId, filters), enabled: !!householdId })
  const today = new Date()
  const defaultEntryDate = toIso(new Date(selectedMonth.getFullYear(), selectedMonth.getMonth(), Math.min(today.getDate(), endOfMonth(selectedMonth).getDate())))

  const invalidateLedger = async () => Promise.all([
    queryClient.invalidateQueries({ queryKey: ["transactions", householdId] }),
    queryClient.invalidateQueries({ queryKey: ["accounts", householdId] }),
    queryClient.invalidateQueries({ queryKey: ["dashboard", householdId] }),
  ])
  const settle = useMutation({ mutationFn: (id: string) => transactionService.settle(householdId, id, todayIso()), onSuccess: async () => { await invalidateLedger(); toast.success("Lançamento liquidado.") }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  const cancel = useMutation({ mutationFn: (id: string) => transactionService.cancel(householdId, id), onSuccess: async () => { await invalidateLedger(); toast.success("Lançamento cancelado.") }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  const monthItems = [...(transactions.data?.items ?? [])].sort((left, right) => left.dueDate.localeCompare(right.dueDate) || left.description.localeCompare(right.description, "pt-BR"))
  const items = status ? monthItems.filter((item) => item.status === status) : monthItems
  const settledCount = monthItems.filter(isSettled).length
  const recurringCount = monthItems.filter((item) => item.recurringTransactionId).length
  const pendingCount = monthItems.filter((item) => item.status === "Pending" || item.status === "Overdue").length

  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div><p className="text-sm font-semibold text-brand-green">Caderno financeiro</p><h1 className="mt-1 font-heading text-3xl font-semibold">Lançamentos</h1><p className="mt-2 text-muted-foreground">Uma folha para cada mês da vida financeira da família.</p></div>
      <div className="flex flex-wrap gap-2"><Button variant="outline" onClick={() => setTransferOpen(true)}><ArrowRightLeft />Transferir</Button><Button onClick={() => { setEditing(null); setFormOpen(true) }}><Plus />Novo lançamento</Button></div>
    </div>

    <section className="notebook-paper relative mt-7 overflow-hidden rounded-[1.75rem] border shadow-[0_24px_60px_-38px_rgba(1,33,43,0.45)]">
      <div className="notebook-binding" aria-hidden="true" />
      <div className="relative border-b px-5 py-5 sm:px-9 lg:px-12">
        <div className="flex flex-col gap-5 lg:flex-row lg:items-center lg:justify-between">
          <div className="flex items-center gap-2 sm:gap-3">
            <Button variant="outline" size="icon" onClick={() => setSelectedMonth((month) => addMonths(month, -1))} aria-label="Mês anterior"><ArrowLeft /></Button>
            <div className="min-w-0 flex-1 text-center sm:min-w-64"><p className="text-xs font-semibold uppercase tracking-[0.2em] text-brand-green">Folha mensal</p><h2 className="mt-1 font-heading text-xl font-semibold sm:text-2xl">{monthTitle(selectedMonth)}</h2></div>
            <Button variant="outline" size="icon" onClick={() => setSelectedMonth((month) => addMonths(month, 1))} aria-label="Próximo mês"><ArrowRight /></Button>
            {!isSameMonth(selectedMonth, new Date()) && <Button variant="ghost" size="sm" onClick={() => setSelectedMonth(startOfMonth(new Date()))}><RotateCcw />Hoje</Button>}
          </div>
          <select aria-label="Filtrar status" className="h-10 rounded-xl border bg-background/80 px-3 text-sm backdrop-blur" value={status} onChange={(event) => setStatus(event.target.value as TransactionStatus | "")}>
            <option value="">Todos os status</option>{Object.entries(statusLabel).map(([value, label]) => <option key={value} value={value}>{label}</option>)}
          </select>
        </div>
        <div className="mt-5 grid grid-cols-3 gap-2 sm:max-w-xl sm:gap-3"><NotebookCounter label="Em aberto" value={pendingCount} /><NotebookCounter label="Liquidados" value={settledCount} /><NotebookCounter label="Recorrentes" value={recurringCount} /></div>
      </div>

      <div key={`${filters.from}-${status}`} className="relative animate-in fade-in slide-in-from-right-2 duration-300">
        {transactions.isPending ? <div className="flex min-h-72 items-center justify-center gap-2 text-sm text-muted-foreground"><LoaderCircle className="animate-spin" />Preparando a folha e as recorrências…</div>
          : transactions.isError ? <div className="flex min-h-72 flex-col items-center justify-center px-6 text-center"><p className="text-sm text-destructive">{getApiErrorMessage(transactions.error)}</p><Button variant="outline" className="mt-4" onClick={() => transactions.refetch()}>Tentar novamente</Button></div>
          : items.length === 0 ? <div className="flex min-h-72 flex-col items-center justify-center px-6 text-center"><ReceiptText className="size-10 text-muted-foreground" /><p className="mt-4 font-semibold">Esta folha ainda está em branco</p><p className="mt-1 max-w-sm text-sm text-muted-foreground">Não há lançamentos nem ocorrências recorrentes para este mês e filtro.</p><Button className="mt-5" onClick={() => { setEditing(null); setFormOpen(true) }}><Plus />Adicionar primeiro lançamento</Button></div>
          : <div className="divide-y">{items.map((transaction) => <NotebookEntry key={transaction.id} transaction={transaction} onEdit={() => { setEditing(transaction); setFormOpen(true) }} onSettle={() => settle.mutate(transaction.id)} onCancel={() => cancel.mutate(transaction.id)} settling={settle.isPending} canceling={cancel.isPending} />)}</div>}
      </div>
      {transactions.data && <p className="relative border-t px-6 py-3 text-right text-xs text-muted-foreground sm:px-12">{status ? `${items.length} de ` : ""}{transactions.data.totalCount} lançamento(s) nesta folha</p>}
    </section>

    <TransactionForm open={formOpen} onOpenChange={setFormOpen} householdId={householdId} accounts={accounts} categories={categories} members={members} transaction={editing} defaultDate={defaultEntryDate} />
    <TransferForm open={transferOpen} onOpenChange={setTransferOpen} householdId={householdId} accounts={accounts} members={members} defaultDate={defaultEntryDate} />
  </div>
}

function NotebookCounter({ label, value }: { label: string; value: number }) { return <div className="rounded-xl border bg-background/65 px-3 py-2 backdrop-blur"><p className="text-lg font-semibold tabular-nums">{value}</p><p className="text-[11px] text-muted-foreground sm:text-xs">{label}</p></div> }

function NotebookEntry({ transaction, onEdit, onSettle, onCancel, settling, canceling }: { transaction: Transaction; onEdit: () => void; onSettle: () => void; onCancel: () => void; settling: boolean; canceling: boolean }) {
  const settled = isSettled(transaction), canceled = transaction.status === "Canceled"
  const institutionControlled = transaction.institutionControlled || transaction.sourceProvider === "PLUGGY"
  const editable = institutionControlled || transaction.status === "Pending" || transaction.status === "Overdue"
  const cancelable = !institutionControlled && !canceled && transaction.type !== "Transfer" && transaction.type !== "InvoicePayment"
  return <article className={`group grid min-h-24 grid-cols-[3.25rem_1fr] gap-3 px-5 py-4 transition-colors hover:bg-brand-green/[0.035] sm:grid-cols-[4rem_1fr_auto] sm:items-center sm:px-9 lg:px-12 ${settled || canceled ? "notebook-entry-crossed" : ""}`}>
    <div className="text-center"><p className="font-heading text-2xl font-semibold tabular-nums">{format(new Date(`${transaction.dueDate}T12:00:00`), "dd")}</p><p className="text-[10px] font-semibold uppercase tracking-wider text-muted-foreground">{format(new Date(`${transaction.dueDate}T12:00:00`), "EEE", { locale: ptBR })}</p></div>
    <div className="min-w-0"><div className="flex flex-wrap items-center gap-2"><h3 className={`font-semibold ${settled || canceled ? "line-through decoration-2 decoration-current/60" : ""}`}>{transaction.description}</h3>{transaction.recurringTransactionId && <span className="inline-flex items-center gap-1 rounded-full bg-brand-green/10 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-brand-green"><CalendarClock className="size-3" />Recorrente</span>}{transaction.isRefund && <span className="rounded-full bg-brand-green/10 px-2 py-0.5 text-[10px] font-semibold uppercase text-brand-green">Estorno</span>}{institutionControlled && <span className="inline-flex items-center gap-1 rounded-full bg-sky-500/10 px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide text-sky-700 dark:text-sky-300"><CloudCheck className="size-3" />Open Finance</span>}<span className={`rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase tracking-wide ${statusStyle[transaction.status]}`}>{statusLabel[transaction.status]}</span></div><p className="mt-1 text-xs text-muted-foreground">{transaction.isRefund ? "Estorno" : typeLabel[transaction.type]} · competência {formatDate(transaction.competenceDate)}{transaction.settledDate ? ` · liquidado em ${formatDate(transaction.settledDate)}` : ""}</p></div>
    <div className="col-start-2 flex items-center justify-between gap-3 sm:col-start-auto sm:justify-end"><p className={`whitespace-nowrap text-base font-semibold tabular-nums ${transaction.type === "Income" || transaction.isRefund ? "text-brand-green" : ""} ${settled || canceled ? "line-through decoration-2 decoration-current/60" : ""}`}>{transaction.type === "Expense" && !transaction.isRefund ? "− " : transaction.type === "Income" || transaction.isRefund ? "+ " : ""}{formatCurrency(transaction.amount)}</p><div className="flex min-w-28 justify-end gap-1">{editable && <Button size="icon" variant="ghost" aria-label={`${institutionControlled ? "Editar categoria e observações de" : "Editar"} ${transaction.description}`} onClick={onEdit}><Edit3 /></Button>}{editable && !institutionControlled && <ConfirmDialog trigger={<Button size="icon" variant="ghost" aria-label={`Liquidar ${transaction.description}`}><Check /></Button>} title="Liquidar lançamento?" description="O saldo da conta será atualizado e esta linha será riscada no caderno." confirmLabel="Liquidar" pending={settling} onConfirm={onSettle} />}{cancelable && <ConfirmDialog trigger={<Button size="icon" variant="ghost" aria-label={`Cancelar ${transaction.description}`}><X /></Button>} title="Cancelar lançamento?" description="Se já estiver liquidado, a API criará o estorno correspondente." confirmLabel="Cancelar" destructive pending={canceling} onConfirm={onCancel} />}</div></div>
  </article>
}

function TransactionForm({ open, onOpenChange, householdId, accounts, categories, members, transaction, defaultDate }: { open: boolean; onOpenChange: (value: boolean) => void; householdId: string; accounts: ReturnType<typeof useFinanceContext>["accounts"]; categories: ReturnType<typeof useFinanceContext>["categories"]; members: ReturnType<typeof useFinanceContext>["members"]; transaction: Transaction | null; defaultDate: string }) {
  const queryClient = useQueryClient()
  const institutionControlled = !!transaction?.institutionControlled || transaction?.sourceProvider === "PLUGGY"
  const defaults: TransactionFormData = transaction ? { description: transaction.description, amount: transaction.amount, type: transaction.type as "Income" | "Expense", categoryId: transaction.categoryId ?? "", accountId: transaction.accountId ?? "", competenceDate: transaction.competenceDate, dueDate: transaction.dueDate, settledDate: transaction.settledDate, notes: transaction.notes ?? "", responsibleUserId: transaction.responsibleUserId, scope: transaction.scope } : { description: "", amount: 0, type: "Expense", categoryId: "", accountId: "", competenceDate: defaultDate, dueDate: defaultDate, settledDate: null, notes: "", responsibleUserId: members[0]?.userId ?? "", scope: "Shared" }
  const form = useForm<TransactionFormData>({ resolver: zodResolver(institutionControlled ? institutionMetadataSchema : transactionSchema), values: defaults })
  const type = useWatch({ control: form.control, name: "type" })
  const mutation = useMutation({ mutationFn: (value: TransactionFormData) => transaction ? (institutionControlled ? transactionService.updateMetadata(householdId, transaction.id, value) : transactionService.update(householdId, transaction.id, value as TransactionInput)) : transactionService.create(householdId, { ...value, settledDate: value.settledDate || null } as TransactionInput), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["transactions", householdId] }); toast.success(transaction ? "Lançamento atualizado." : "Lançamento criado."); onOpenChange(false) }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  return <FormDialog open={open} onOpenChange={onOpenChange} title={transaction ? "Editar lançamento" : "Novo lançamento"} description={institutionControlled ? "Os dados da instituição são somente leitura. Categoria e observações permanecem sob seu controle." : "O saldo só muda quando o lançamento é liquidado."}><form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((value) => mutation.mutate(value))}><Field label="Descrição"><Input readOnly={institutionControlled} {...form.register("description")} /></Field><Field label="Valor"><Input readOnly={institutionControlled} type="number" step="0.01" {...form.register("amount", { valueAsNumber: true })} /></Field><Field label="Tipo">{institutionControlled ? <><Input readOnly value={type === "Income" ? "Receita" : "Despesa"} /><input type="hidden" {...form.register("type")} /></> : <select disabled={!!transaction} className="h-11 w-full rounded-xl border bg-background px-3" {...form.register("type")}><option value="Expense">Despesa</option><option value="Income">Receita</option></select>}</Field><Field label="Categoria"><CategorySelect categories={categories} type={type} {...form.register("categoryId")} /></Field><Field label="Conta">{institutionControlled ? <><Input readOnly value={accounts.find((account) => account.id === transaction?.accountId)?.name ?? "Cartão da instituição"} /><input type="hidden" {...form.register("accountId")} /></> : <AccountSelect accounts={accounts} {...form.register("accountId")} />}</Field><Field label="Responsável">{institutionControlled ? <><Input readOnly value={members.find((member) => member.userId === transaction?.responsibleUserId)?.name ?? "Responsável atual"} /><input type="hidden" {...form.register("responsibleUserId")} /></> : <ResponsibleSelect members={members} {...form.register("responsibleUserId")} />}</Field><Field label="Competência"><Input readOnly={institutionControlled} type="date" {...form.register("competenceDate")} /></Field><Field label="Vencimento"><Input readOnly={institutionControlled} type="date" {...form.register("dueDate")} /></Field><Field label="Escopo">{institutionControlled ? <><Input readOnly value={transaction?.scope === "Individual" ? "Individual" : "Compartilhado"} /><input type="hidden" {...form.register("scope")} /></> : <select className="h-11 w-full rounded-xl border bg-background px-3" {...form.register("scope")}><option value="Shared">Compartilhado</option><option value="Individual">Individual</option></select>}</Field>{!transaction && <Field label="Data do pagamento/recebimento (opcional)"><Input type="date" {...form.register("settledDate", { setValueAs: (value) => value || null })} /></Field>}<div className="sm:col-span-2"><Field label="Observações"><Input {...form.register("notes")} /></Field></div><div className="flex justify-end sm:col-span-2"><Button disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Salvar</Button></div></form></FormDialog>
}

function TransferForm({ open, onOpenChange, householdId, accounts, members, defaultDate }: { open: boolean; onOpenChange: (value: boolean) => void; householdId: string; accounts: ReturnType<typeof useFinanceContext>["accounts"]; members: ReturnType<typeof useFinanceContext>["members"]; defaultDate: string }) {
  const queryClient = useQueryClient()
  const form = useForm<TransferFormData>({ resolver: zodResolver(transferSchema), values: { description: "Transferência entre contas", amount: 0, sourceAccountId: "", destinationAccountId: "", transferDate: defaultDate, responsibleUserId: members[0]?.userId ?? "", notes: "" } })
  const mutation = useMutation({ mutationFn: (value: TransferFormData) => transactionService.transfer(householdId, { ...value, clientRequestId: crypto.randomUUID() } as TransferInput), onSuccess: async () => { await Promise.all([queryClient.invalidateQueries({ queryKey: ["transactions", householdId] }), queryClient.invalidateQueries({ queryKey: ["accounts", householdId] })]); toast.success("Transferência concluída."); onOpenChange(false) }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  return <FormDialog open={open} onOpenChange={onOpenChange} title="Transferir entre contas" description="A operação movimenta as duas contas sem alterar relatórios de receitas e despesas."><form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((value) => mutation.mutate(value))}><Field label="Descrição"><Input {...form.register("description")} /></Field><Field label="Valor"><Input type="number" step="0.01" {...form.register("amount", { valueAsNumber: true })} /></Field><Field label="Conta de origem"><AccountSelect accounts={accounts} {...form.register("sourceAccountId")} /></Field><Field label="Conta de destino"><AccountSelect accounts={accounts} {...form.register("destinationAccountId")} /></Field><Field label="Data"><Input type="date" {...form.register("transferDate")} /></Field><Field label="Responsável"><ResponsibleSelect members={members} {...form.register("responsibleUserId")} /></Field><div className="flex justify-end sm:col-span-2"><Button disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Transferir</Button></div></form></FormDialog>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="space-y-2 text-sm font-medium"><span>{label}</span>{children}</label> }
