"use client"

import Link from "next/link"
import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { ArrowLeft, ChevronDown, Globe2 } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cardKeys, cardService, kindLabel, type CardItem, type ClosedInvoice } from "@/features/cards/card-types"
import { useFinanceContext } from "@/features/finance/selectors"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate, formatDateTime } from "@/lib/formatters"
import { cn } from "@/lib/utils"

type Tab = "current" | "upcoming" | "closed" | "payments"

export function CardDetailPage({ cardId }: { cardId: string }) {
  const { householdId } = useFinanceContext()
  const [tab, setTab] = useState<Tab>("current")
  const query = useQuery({ queryKey: cardKeys.detail(householdId, cardId), queryFn: () => cardService.get(householdId, cardId), enabled: !!householdId })

  if (query.isPending) return <Skeleton className="h-96" />
  if (query.isError) return <Card><CardContent className="p-6 text-sm text-destructive">{getApiErrorMessage(query.error, "Não foi possível carregar o cartão.")}</CardContent></Card>
  const { card, currentInvoice, upcoming, closedInvoices, payments } = query.data
  const tabs: { id: Tab; label: string; count: number }[] = [
    { id: "current", label: "Fatura atual", count: currentInvoice.itemCount },
    { id: "upcoming", label: "Próximas parcelas", count: upcoming.length },
    { id: "closed", label: "Faturas fechadas", count: closedInvoices.length },
    { id: "payments", label: "Pagamentos", count: payments.length },
  ]

  return <div>
    <Button variant="ghost" size="sm" asChild><Link href="/cartoes"><ArrowLeft />Cartões</Link></Button>
    <div className="mt-3 flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div><p className="text-sm font-semibold text-brand-green">{card.institution}</p><h1 className="mt-1 font-heading text-3xl font-semibold">{card.name}</h1><p className="mt-2 text-sm text-muted-foreground">{[card.brand, card.number ? `•••• ${card.number.slice(-4)}` : null, card.updatedAtUtc ? `atualizado em ${formatDateTime(card.updatedAtUtc)}` : null].filter(Boolean).join(" · ")}</p></div>
    </div>

    <div className="mt-6 grid gap-4 md:grid-cols-4">
      <Metric label="Fatura atual (aberta)" value={formatCurrency(currentInvoice.total)} hint={[currentInvoice.closingDate && `fecha ${formatDate(currentInvoice.closingDate)}`, currentInvoice.dueDate && `vence ${formatDate(currentInvoice.dueDate)}`].filter(Boolean).join(" · ") || undefined} emphasize />
      <Metric label="Fechada a vencer" value={formatCurrency(card.outstandingClosedInvoices)} hint="Fatura fechada ainda não vencida nem paga" />
      <Metric label="Limite disponível" value={card.availableLimit !== null ? formatCurrency(card.availableLimit) : "—"} hint={card.creditLimit !== null ? `de ${formatCurrency(card.creditLimit)}` : undefined} />
      <Metric label="Saldo informado pelo banco" value={formatCurrency(card.reportedBalance)} hint="Campo balance da Pluggy, para conferência" />
    </div>

    <div className="mt-7 flex flex-wrap gap-1 rounded-xl border bg-muted/30 p-1" role="tablist">
      {tabs.map((item) => <button key={item.id} type="button" role="tab" aria-selected={tab === item.id} onClick={() => setTab(item.id)} className={cn("rounded-lg px-4 py-2 text-sm font-semibold transition-colors", tab === item.id ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground")}>{item.label} <span className="ml-1 text-xs font-normal opacity-70">{item.count}</span></button>)}
    </div>

    <div className="mt-4">
      {tab === "current" && <ItemsCard items={currentInvoice.items ?? []} total={currentInvoice.total} empty="Nenhum lançamento na fatura aberta." note={(currentInvoice.items ?? []).some((x) => x.projected) ? "Parcelas marcadas como projetadas pertencem a esta fatura, mas o banco só as publica no fechamento." : undefined} />}
      {tab === "upcoming" && <ItemsCard items={upcoming} total={upcoming.reduce((sum, x) => sum + x.effect, 0)} empty="Nenhuma parcela futura informada pelo banco." note="Parcelas das próximas faturas. As marcadas como projetadas ainda não foram publicadas pelo banco: o Open Finance só envia cada parcela quando ela é faturada." />}
      {tab === "closed" && (closedInvoices.length ? <div className="space-y-3">{closedInvoices.map((invoice) => <ClosedInvoiceCard key={invoice.id} invoice={invoice} />)}</div> : <Empty text="O banco não retornou faturas fechadas." />)}
      {tab === "payments" && <ItemsCard items={payments} empty="Nenhum pagamento encontrado." showAmount />}
    </div>
  </div>
}

function Metric({ label, value, hint, emphasize = false }: { label: string; value: string; hint?: string; emphasize?: boolean }) {
  return <Card className={emphasize ? "border-brand-green/40" : ""}><CardContent className="p-5"><p className="text-xs text-muted-foreground">{label}</p><p className={cn("mt-2 font-semibold", emphasize ? "text-2xl" : "text-xl")}>{value}</p>{hint && <p className="mt-1 text-xs text-muted-foreground">{hint}</p>}</CardContent></Card>
}

function ClosedInvoiceCard({ invoice }: { invoice: ClosedInvoice }) {
  const [open, setOpen] = useState(false)
  const diverges = Math.abs(invoice.itemsTotal - invoice.totalAmount) >= 0.01
  return <Card><CardContent className="p-0">
    <button type="button" onClick={() => setOpen(!open)} className="flex w-full items-center gap-4 p-5 text-left">
      <div className="flex-1"><p className="font-semibold">Vencimento {formatDate(invoice.dueDate)}</p><p className="text-xs text-muted-foreground">{invoice.closingDate ? `Fechou em ${formatDate(invoice.closingDate)} · ` : ""}{invoice.isPaid ? "Paga" : `Em aberto: ${formatCurrency(invoice.outstanding)}`}{invoice.minimumPayment ? ` · mínimo ${formatCurrency(invoice.minimumPayment)}` : ""}</p></div>
      <span className={cn("rounded-full px-2 py-0.5 text-[10px] font-semibold uppercase", invoice.isPaid ? "bg-emerald-500/10 text-emerald-600 dark:text-emerald-400" : "bg-amber-500/10 text-amber-600 dark:text-amber-400")}>{invoice.isPaid ? "Paga" : "Aberta"}</span>
      <p className="text-lg font-semibold">{formatCurrency(invoice.totalAmount)}</p>
      <ChevronDown className={cn("size-4 transition-transform", open && "rotate-180")} />
    </button>
    {open && <div className="border-t px-5 pb-5">
      {diverges && <p className="mt-4 rounded-xl bg-muted/60 p-3 text-xs text-muted-foreground">Total oficial do banco: {formatCurrency(invoice.totalAmount)} · soma dos lançamentos vinculados: {formatCurrency(invoice.itemsTotal)}. A diferença costuma ser saldo anterior, juros ou encargos que o banco inclui no total.</p>}
      <ItemList items={invoice.items ?? []} empty="O banco não vinculou lançamentos a esta fatura." />
    </div>}
  </CardContent></Card>
}

function ItemsCard({ items, total, empty, note, showAmount = false }: { items: CardItem[]; total?: number; empty: string; note?: string; showAmount?: boolean }) {
  return <Card><CardContent className="p-5">
    {note && <p className="mb-3 text-xs text-muted-foreground">{note}</p>}
    <ItemList items={items} empty={empty} showAmount={showAmount} />
    {total !== undefined && items.length > 0 && <div className="mt-3 flex justify-between border-t pt-3 text-sm font-semibold"><span>Total</span><span>{formatCurrency(total)}</span></div>}
  </CardContent></Card>
}

function ItemList({ items, empty, showAmount = false }: { items: CardItem[]; empty: string; showAmount?: boolean }) {
  if (!items.length) return <p className="py-4 text-sm text-muted-foreground">{empty}</p>
  return <div className="divide-y">{items.map((item) => {
    const value = showAmount ? item.amount : item.effect
    return <div key={item.id} className="flex items-center justify-between gap-3 py-2.5">
      <div className="min-w-0">
        <p className={cn("truncate text-sm font-medium", item.projected && "italic")}>{item.description}{item.totalInstallments && item.totalInstallments > 1 ? <span className="ml-2 text-xs font-normal text-muted-foreground">{item.installmentNumber}/{item.totalInstallments}</span> : null}</p>
        <p className="text-xs text-muted-foreground">{formatDate(item.date)}{item.purchaseDate && item.purchaseDate !== item.date ? ` · compra ${formatDate(item.purchaseDate)}` : ""}{item.projected ? " · projetada" : item.isPending ? " · pendente" : ""}{item.kind !== "Purchase" ? ` · ${kindLabel[item.kind]}` : ""}{item.category ? ` · ${item.category}` : ""}</p>
        {item.currencyCode && item.originalAmount !== null && <p className="flex items-center gap-1 text-xs text-muted-foreground"><Globe2 className="size-3" />{item.originalAmount.toLocaleString("pt-BR", { style: "currency", currency: item.currencyCode })}</p>}
      </div>
      <p className={cn("shrink-0 text-sm font-semibold", value < 0 && "text-emerald-600 dark:text-emerald-400")}>{formatCurrency(value)}</p>
    </div>
  })}</div>
}

function Empty({ text }: { text: string }) { return <Card className="border-dashed"><CardContent className="p-6 text-sm text-muted-foreground">{text}</CardContent></Card> }
