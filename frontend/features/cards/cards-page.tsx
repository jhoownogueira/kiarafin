"use client"

import Link from "next/link"
import { useQuery } from "@tanstack/react-query"
import { ArrowRight, CreditCard as CardIcon } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { cardKeys, cardService } from "@/features/cards/card-types"
import { useFinanceContext } from "@/features/finance/selectors"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate } from "@/lib/formatters"

export function CardsPage() {
  const { householdId } = useFinanceContext()
  const query = useQuery({ queryKey: cardKeys.list(householdId), queryFn: () => cardService.list(householdId), enabled: !!householdId })

  return <div>
    <div><p className="text-sm font-semibold text-brand-green">Crédito</p><h1 className="mt-1 font-heading text-3xl font-semibold">Cartões</h1><p className="mt-2 text-muted-foreground">Faturas montadas a partir dos dados do Open Finance.</p></div>

    {query.isPending ? <div className="mt-6 grid gap-5 lg:grid-cols-2">{Array.from({ length: 2 }).map((_, i) => <Skeleton key={i} className="h-64" />)}</div>
      : query.isError ? <Card className="mt-6"><CardContent className="flex min-h-40 flex-col items-center justify-center p-6 text-center"><p className="text-sm text-destructive">{getApiErrorMessage(query.error, "Não foi possível carregar os cartões.")}</p><Button variant="outline" className="mt-4" onClick={() => query.refetch()}>Tentar novamente</Button></CardContent></Card>
      : query.data.length === 0 ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-64 flex-col items-center justify-center text-center"><CardIcon className="size-10 text-muted-foreground" /><h2 className="mt-4 font-semibold">Nenhum cartão sincronizado</h2><p className="mt-1 text-sm text-muted-foreground">Os cartões aparecem aqui após a sincronização do Open Finance.</p><Button variant="outline" className="mt-4" asChild><Link href="/configuracoes/open-finance">Abrir Open Finance</Link></Button></CardContent></Card>
      : <div className="mt-6 grid gap-5 lg:grid-cols-2">{query.data.map((card) => {
        const used = card.creditLimit && card.availableLimit !== null ? card.creditLimit - card.availableLimit : null
        const usedPercent = used !== null && card.creditLimit ? Math.min(100, Math.max(0, (used / card.creditLimit) * 100)) : null
        return <Card key={card.id} className={!card.isActive ? "opacity-65" : ""}><CardContent className="p-5">
          <div className="rounded-2xl bg-gradient-to-br from-slate-900 via-slate-800 to-emerald-950 p-5 text-white">
            <div className="flex items-start justify-between gap-3"><div><p className="text-xs opacity-70">{card.institution}</p><p className="text-lg font-semibold">{card.name}</p></div><p className="text-xs font-semibold uppercase opacity-80">{card.brand}</p></div>
            <p className="mt-6 text-xs opacity-70">Fatura atual (aberta)</p>
            <p className="mt-1 text-3xl font-semibold tracking-tight">{formatCurrency(card.currentInvoice.total)}</p>
            <div className="mt-3 flex flex-wrap gap-x-5 gap-y-1 text-xs opacity-80">
              {card.currentInvoice.closingDate && <span>Fecha em {formatDate(card.currentInvoice.closingDate)}</span>}
              {card.currentInvoice.dueDate && <span>Vence em {formatDate(card.currentInvoice.dueDate)}</span>}
              <span>{card.currentInvoice.itemCount} lançamento(s)</span>
            </div>
            {card.number && <p className="mt-4 font-mono text-sm tracking-widest opacity-80">•••• {card.number.slice(-4)}</p>}
          </div>
          {card.outstandingClosedInvoices > 0 && <p className="mt-4 rounded-xl bg-amber-500/10 p-3 text-sm text-amber-700 dark:text-amber-300">Fatura fechada a vencer: <span className="font-semibold">{formatCurrency(card.outstandingClosedInvoices)}</span>{card.lastClosedInvoice && <> · vence {formatDate(card.lastClosedInvoice.dueDate)}</>}</p>}
          <div className="mt-4 grid grid-cols-2 gap-3 text-sm">
            <div><p className="text-xs text-muted-foreground">Limite total</p><p className="font-semibold">{card.creditLimit !== null ? formatCurrency(card.creditLimit) : "—"}</p></div>
            <div><p className="text-xs text-muted-foreground">Limite disponível</p><p className="font-semibold">{card.availableLimit !== null ? formatCurrency(card.availableLimit) : "—"}</p></div>
          </div>
          {usedPercent !== null && <div className="mt-3 h-2 overflow-hidden rounded-full bg-muted"><div className="h-full rounded-full bg-brand-green" style={{ width: `${usedPercent}%` }} /></div>}
          {card.lastClosedInvoice && <p className="mt-4 text-xs text-muted-foreground">Última fatura fechada: {formatCurrency(card.lastClosedInvoice.totalAmount)} · venc. {formatDate(card.lastClosedInvoice.dueDate)} · {card.lastClosedInvoice.isPaid ? "paga" : "em aberto"}</p>}
          <Button variant="outline" className="mt-4 w-full" asChild><Link href={`/cartoes/${card.id}`}>Ver faturas<ArrowRight /></Link></Button>
        </CardContent></Card>
      })}</div>}
  </div>
}
