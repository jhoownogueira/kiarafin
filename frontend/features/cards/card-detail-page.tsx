"use client"

import Link from "next/link"
import { useState } from "react"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { addDays, addMonths, format, isSameMonth, startOfMonth } from "date-fns"
import { ptBR } from "date-fns/locale"
import { ArrowLeft, ArrowRight, CheckCircle2, CreditCard as CardIcon, Edit3, LoaderCircle, ShoppingBag, Trash2 } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { ConfirmDialog } from "@/components/shared/confirm-dialog"
import { FormDialog } from "@/components/shared/form-dialog"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { PurchaseDialog } from "@/features/cards/card-dialogs"
import { invoiceStatusLabel, type CreditCard, type InvoiceDetail, type InvoicePurchase } from "@/features/cards/card-types"
import { CategorySelect, ResponsibleSelect, useFinanceContext } from "@/features/finance/selectors"
import { api } from "@/lib/api/client"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate, todayIso } from "@/lib/formatters"

const localDate = (value: string) => new Date(`${value}T12:00:00`)
const monthTitle = (date: Date) => { const value = format(date, "MMMM 'de' yyyy", { locale: ptBR }); return value.charAt(0).toUpperCase() + value.slice(1) }

export function CardDetailPage({ cardId }: { cardId: string }) {
  const { householdId, categories, members } = useFinanceContext()
  const queryClient = useQueryClient()
  const [selectedMonth, setSelectedMonth] = useState<Date | null>(null)
  const [purchaseOpen, setPurchaseOpen] = useState(false)
  const [closeOpen, setCloseOpen] = useState(false)
  const [dueDate, setDueDate] = useState(todayIso())
  const [editingPurchase, setEditingPurchase] = useState<InvoicePurchase | null>(null)
  const root = `/households/${householdId}/credit-cards/${cardId}`
  const cardQuery = useQuery({ queryKey: ["credit-cards", householdId, cardId], queryFn: async () => (await api.get<CreditCard>(root)).data, enabled: !!householdId && !!cardId })
  const suggestedInvoice = cardQuery.data?.invoices
    .filter((invoice) => invoice.status !== "Paid" && invoice.status !== "Canceled")
    .sort((left, right) => left.dueDate.localeCompare(right.dueDate))[0] ?? cardQuery.data?.invoices[0]
  const month = selectedMonth ?? (suggestedInvoice ? startOfMonth(localDate(suggestedInvoice.dueDate)) : startOfMonth(new Date()))
  const invoiceSummary = cardQuery.data?.invoices.find((invoice) => isSameMonth(localDate(invoice.dueDate), month))
  const invoiceQuery = useQuery({ queryKey: ["credit-cards", householdId, cardId, "invoice", invoiceSummary?.id], queryFn: async () => (await api.get<InvoiceDetail>(`${root}/invoices/${invoiceSummary!.id}`)).data, enabled: !!invoiceSummary })
  const invalidate = async () => queryClient.invalidateQueries({ queryKey: ["credit-cards", householdId] })

  const pay = useMutation({ mutationFn: async (invoice: InvoiceDetail) => api.post(`${root}/invoices/${invoice.id}/pay`, { accountId: cardQuery.data!.defaultPaymentAccountId, paidDate: todayIso() }), onSuccess: async () => { await invalidate(); toast.success("Fatura paga integralmente.") }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  const close = useMutation({ mutationFn: async ({ invoiceId, date }: { invoiceId: string; date: string }) => api.post(`${root}/invoices/${invoiceId}/close`, { dueDate: date }), onSuccess: async (_, variables) => { setSelectedMonth(startOfMonth(localDate(variables.date))); await invalidate(); setCloseOpen(false); toast.success("Fatura fechada.") }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  const removePurchase = useMutation({ mutationFn: async (purchaseId: string) => api.delete(`${root}/purchases/${purchaseId}`), onSuccess: async () => { await invalidate(); toast.success("Compra e suas parcelas foram excluídas.") }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  const card = cardQuery.data
  const invoice = invoiceQuery.data
  const editable = invoice?.status === "Upcoming" || invoice?.status === "Open" || invoice?.status === "Closed"
  const closable = invoice?.status === "Upcoming" || invoice?.status === "Open"

  if (cardQuery.isPending) return <div className="flex min-h-72 items-center justify-center gap-2 text-sm text-muted-foreground"><LoaderCircle className="animate-spin" />Carregando cartão…</div>
  if (cardQuery.isError || !card) return <div className="py-12 text-center"><p className="text-sm text-destructive">{getApiErrorMessage(cardQuery.error)}</p><Button asChild variant="outline" className="mt-4"><Link href="/cartoes">Voltar aos cartões</Link></Button></div>

  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div><Button asChild variant="ghost" className="-ml-3 mb-2"><Link href="/cartoes"><ArrowLeft />Todos os cartões</Link></Button><p className="text-sm font-semibold text-brand-green">{card.institution || "Cartão de crédito"}</p><h1 className="mt-1 font-heading text-3xl font-semibold">{card.name}</h1><p className="mt-2 text-muted-foreground">Consulte, feche e pague uma fatura por vez.</p></div>
      <Button onClick={() => setPurchaseOpen(true)}><ShoppingBag />Registrar compra</Button>
    </div>

    <div className="mt-6 grid gap-5 xl:grid-cols-[22rem_1fr]">
      <Card><CardContent className="p-5"><div className="rounded-2xl p-5 text-white" style={{ background: card.color || "#01212B" }}><div className="flex justify-between"><div><p className="text-xs opacity-75">{card.institution}</p><p className="text-lg font-semibold">{card.name}</p></div><CardIcon /></div><div className="mt-8 grid grid-cols-3 gap-2 text-xs"><div><p className="opacity-70">Limite</p><p className="font-semibold">{formatCurrency(card.totalLimit)}</p></div><div><p className="opacity-70">Usado</p><p className="font-semibold">{formatCurrency(card.usedLimit)}</p></div><div><p className="opacity-70">Disponível</p><p className="font-semibold">{formatCurrency(card.availableLimit)}</p></div></div></div><div className="mt-4 grid grid-cols-2 gap-3 text-sm"><div className="rounded-xl bg-muted/60 p-3"><p className="text-xs text-muted-foreground">Fechamento padrão</p><p className="mt-1 font-semibold">Dia {card.closingDay}</p></div><div className="rounded-xl bg-muted/60 p-3"><p className="text-xs text-muted-foreground">Vencimento padrão</p><p className="mt-1 font-semibold">Dia {card.dueDay}</p></div></div></CardContent></Card>

      <Card className="overflow-hidden"><CardContent className="p-0">
        <div className="flex items-center justify-between gap-3 border-b p-4 sm:px-6"><Button variant="outline" size="icon" aria-label="Mês anterior" onClick={() => setSelectedMonth(addMonths(month, -1))}><ArrowLeft /></Button><div className="text-center"><p className="text-xs font-semibold uppercase tracking-[0.18em] text-brand-green">Fatura mensal</p><h2 className="mt-1 font-heading text-xl font-semibold">{monthTitle(month)}</h2></div><Button variant="outline" size="icon" aria-label="Próximo mês" onClick={() => setSelectedMonth(addMonths(month, 1))}><ArrowRight /></Button></div>
        {!invoiceSummary ? <div className="flex min-h-80 flex-col items-center justify-center px-6 text-center"><CardIcon className="size-10 text-muted-foreground" /><p className="mt-4 font-semibold">Nenhuma fatura neste mês</p><p className="mt-1 max-w-sm text-sm text-muted-foreground">Não existem compras com vencimento em {monthTitle(month).toLowerCase()}.</p><Button className="mt-5" onClick={() => setPurchaseOpen(true)}><ShoppingBag />Registrar compra</Button></div>
          : invoiceQuery.isPending || !invoice ? <div className="flex min-h-80 items-center justify-center gap-2 text-sm text-muted-foreground"><LoaderCircle className="animate-spin" />Carregando fatura…</div>
          : <>
            <div className="flex flex-col justify-between gap-4 border-b bg-muted/25 p-5 sm:flex-row sm:items-center sm:px-6"><div><div className="flex items-center gap-2"><span className="rounded-full bg-brand-green/10 px-2.5 py-1 text-xs font-semibold text-brand-green">{invoiceStatusLabel[invoice.status]}</span>{invoice.paidDate && <span className="text-xs text-muted-foreground">Paga em {formatDate(invoice.paidDate)}</span>}</div><p className="mt-3 text-sm text-muted-foreground">Fecha em {formatDate(invoice.closingDate)} · vence em {formatDate(invoice.dueDate)}</p></div><div className="sm:text-right"><p className="text-xs text-muted-foreground">Total da fatura</p><p className="mt-1 text-2xl font-semibold tabular-nums">{formatCurrency(invoice.totalAmount)}</p></div></div>
            <div className="divide-y">{invoice.purchases.length ? invoice.purchases.map((purchase) => <div key={purchase.id} className="flex items-center justify-between gap-4 px-5 py-4 sm:px-6"><div className="min-w-0"><p className="truncate font-medium">{purchase.description}</p><p className="mt-1 text-xs text-muted-foreground">{formatDate(purchase.competenceDate)}{purchase.installmentNumber ? ` · ${purchase.installmentNumber}/${purchase.installmentCount}` : ""}</p></div><div className="flex items-center gap-2"><p className="whitespace-nowrap font-semibold tabular-nums">{formatCurrency(purchase.amount)}</p>{editable && <><Button size="icon" variant="ghost" aria-label={`Editar ${purchase.description}`} onClick={() => setEditingPurchase(purchase)}><Edit3 /></Button><ConfirmDialog trigger={<Button size="icon" variant="ghost" aria-label={`Excluir ${purchase.description}`}><Trash2 /></Button>} title="Excluir esta compra?" description="A compra completa e todas as parcelas futuras serão excluídas. O limite e as faturas serão recalculados." confirmLabel="Excluir compra" destructive pending={removePurchase.isPending} onConfirm={() => removePurchase.mutate(purchase.id)} /></>}</div></div>) : <p className="px-6 py-12 text-center text-sm text-muted-foreground">Esta fatura não possui compras.</p>}</div>
            <div className="flex flex-wrap justify-end gap-2 border-t p-4 sm:px-6">{closable && invoice.totalAmount > 0 && <Button variant="outline" onClick={() => { setDueDate(invoice.dueDate); setCloseOpen(true) }}><CheckCircle2 />Fechar fatura</Button>}{invoice.status === "Closed" && invoice.totalAmount > 0 && <ConfirmDialog trigger={<Button>Pagar fatura</Button>} title="Pagar fatura integralmente?" description={`Será debitado ${formatCurrency(invoice.totalAmount)} da conta padrão do cartão.`} confirmLabel="Pagar integralmente" pending={pay.isPending} onConfirm={() => pay.mutate(invoice)} />}</div>
          </>}
      </CardContent></Card>
    </div>

    <PurchaseDialog card={purchaseOpen ? card : null} onOpenChange={(value) => setPurchaseOpen(value)} householdId={householdId} categories={categories} members={members} />
    <FormDialog open={closeOpen} onOpenChange={setCloseOpen} title="Fechar fatura" description="Escolha o vencimento. As compras ainda poderão ser corrigidas até o pagamento."><form onSubmit={(event) => { event.preventDefault(); if (invoice) close.mutate({ invoiceId: invoice.id, date: dueDate }) }}><label className="space-y-2 text-sm font-medium"><span>Data de vencimento</span><Input required min={invoice ? format(addDays(localDate(invoice.closingDate), 1), "yyyy-MM-dd") : undefined} type="date" value={dueDate} onChange={(event) => setDueDate(event.target.value)} /></label><div className="mt-5 flex justify-end"><Button disabled={close.isPending}>{close.isPending && <LoaderCircle className="animate-spin" />}Fechar fatura</Button></div></form></FormDialog>
    <EditPurchaseDialog purchase={editingPurchase} onOpenChange={(value) => !value && setEditingPurchase(null)} root={root} householdId={householdId} categories={categories} members={members} />
  </div>
}

type EditPurchaseForm = { description: string; totalAmount: number; categoryId: string; responsibleUserId: string; scope: "Individual" | "Shared" }

function EditPurchaseDialog({ purchase, onOpenChange, root, householdId, categories, members }: { purchase: InvoicePurchase | null; onOpenChange: (value: boolean) => void; root: string; householdId: string; categories: ReturnType<typeof useFinanceContext>["categories"]; members: ReturnType<typeof useFinanceContext>["members"] }) {
  const queryClient = useQueryClient()
  const form = useForm<EditPurchaseForm>({ values: { description: purchase?.description ?? "", totalAmount: purchase?.totalPurchaseAmount ?? 0, categoryId: purchase?.categoryId ?? "", responsibleUserId: purchase?.responsibleUserId ?? "", scope: purchase?.scope ?? "Shared" } })
  const mutation = useMutation({ mutationFn: (value: EditPurchaseForm) => api.put(`${root}/purchases/${purchase!.id}`, value), onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["credit-cards", householdId] }); toast.success("Compra e parcelas atualizadas."); onOpenChange(false) }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  return <FormDialog open={!!purchase} onOpenChange={onOpenChange} title="Editar compra" description="A alteração será aplicada à compra completa e redistribuída entre todas as parcelas."><form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((value) => mutation.mutate(value))}><label className="space-y-2 text-sm font-medium"><span>Descrição</span><Input required {...form.register("description")} /></label><label className="space-y-2 text-sm font-medium"><span>Valor total</span><Input type="number" min="0.01" step="0.01" {...form.register("totalAmount", { valueAsNumber: true })} /></label><label className="space-y-2 text-sm font-medium"><span>Categoria</span><CategorySelect categories={categories} type="Expense" required {...form.register("categoryId")} /></label><label className="space-y-2 text-sm font-medium"><span>Responsável</span><ResponsibleSelect members={members} required {...form.register("responsibleUserId")} /></label><label className="space-y-2 text-sm font-medium"><span>Escopo</span><select className="h-11 w-full rounded-xl border bg-background px-3" {...form.register("scope")}><option value="Shared">Compartilhada</option><option value="Individual">Individual</option></select></label><div className="flex items-end justify-end"><Button disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Salvar alterações</Button></div></form></FormDialog>
}
