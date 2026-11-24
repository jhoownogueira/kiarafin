"use client"

import Link from "next/link"
import { useQuery } from "@tanstack/react-query"
import { ArrowUp, CreditCard, Plus, ReceiptText, Sparkles, TrendingUp } from "lucide-react"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { useFinanceContext } from "@/features/finance/selectors"
import { CategoryBreakdown, type CategoryInvoiceGroup } from "@/features/forecasts/category-breakdown"
import type { ForecastEntryType } from "@/features/forecasts/forecast-types"
import { api } from "@/lib/api/client"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate } from "@/lib/formatters"
import { cn } from "@/lib/utils"

type BalanceForecast = {
  period: string
  salary: number
  extraIncome: number
  fixedExpenses: number
  cardInvoices: number
  net: number
  cards: { cardId: string; name: string; currentInvoice: number; currentInvoiceDueDate: string | null; outstandingClosedInvoices: number; total: number }[]
  entries: { id: string; name: string; type: ForecastEntryType; amount: number; expectedDay: number }[]
  currentInvoiceByCategory: CategoryInvoiceGroup[]
}

const typeLabels: Record<ForecastEntryType, string> = { Salary: "Salário", ExtraIncome: "Renda extra", FixedExpense: "Conta fixa" }
const monthTitle = (period: string) => { const value = new Intl.DateTimeFormat("pt-BR", { month: "long", year: "numeric", timeZone: "UTC" }).format(new Date(`${period}-01T12:00:00Z`)); return value.charAt(0).toUpperCase() + value.slice(1) }

export function BalanceForecastPage() {
  const { householdId } = useFinanceContext()
  const query = useQuery({ queryKey: ["balance-forecast", householdId], queryFn: async () => (await api.get<BalanceForecast>(`/households/${householdId}/forecasts/balance`)).data, enabled: !!householdId })

  return <div>
    <div><p className="text-sm font-semibold text-brand-green">Visão geral</p><h1 className="mt-1 font-heading text-3xl font-semibold">Previsão de saldo</h1><p className="mt-2 text-muted-foreground">Suas previsões do mês menos a fatura atual dos cartões.</p></div>
    {query.isPending ? <Skeleton className="mt-7 h-56" /> : query.isError ? <Card className="mt-7"><CardContent className="p-6 text-sm text-destructive">{getApiErrorMessage(query.error)}</CardContent></Card> : <ForecastView data={query.data} />}
  </div>
}

function ForecastView({ data }: { data: BalanceForecast }) {
  const income = data.salary + data.extraIncome
  return <>
    <div className="mt-7 overflow-hidden rounded-2xl border bg-gradient-to-br from-emerald-950 via-slate-950 to-slate-900 p-6 text-white shadow-lg sm:p-7">
      <div className="flex flex-col justify-between gap-6 lg:flex-row lg:items-end">
        <div><div className="flex items-center gap-2 text-sm font-semibold text-emerald-300"><TrendingUp className="size-4" />{monthTitle(data.period).toUpperCase()}</div><p className="mt-3 text-sm text-slate-300">Resultado previsto do mês</p><p className={cn("mt-1 text-4xl font-semibold tracking-tight", data.net < 0 && "text-rose-300")}>{formatCurrency(data.net)}</p></div>
        <div className="grid grid-cols-2 gap-x-8 gap-y-4 text-sm sm:grid-cols-3"><Metric label="Entradas" value={income} /><Metric label="Contas fixas" value={data.fixedExpenses} /><Metric label="Fatura atual" value={data.cardInvoices} /></div>
      </div>
    </div>

    {data.entries.length === 0 && <Card className="mt-6 border-dashed"><CardContent className="flex min-h-40 flex-col items-center justify-center text-center"><Sparkles className="size-8 text-brand-green" /><p className="mt-3 font-semibold">Cadastre suas previsões</p><p className="mt-1 max-w-lg text-sm text-muted-foreground">Salário, rendas extras e contas fixas entram neste cálculo.</p><Button className="mt-4" asChild><Link href="/previsoes"><Plus />Cadastrar previsões</Link></Button></CardContent></Card>}

    <CategoryBreakdown groups={data.currentInvoiceByCategory} />

    <div className="mt-6 grid gap-4 lg:grid-cols-[.9fr_1.1fr]">
      <Card><CardContent className="p-5">
        <h2 className="font-semibold">Cálculo</h2>
        <div className="mt-5 space-y-3">
          <Line label="Salários" value={data.salary} kind="income" />
          <Line label="Rendas extras" value={data.extraIncome} kind="income" />
          <Line label="Contas fixas" value={data.fixedExpenses} kind="expense" />
          <Line label="Fatura atual dos cartões" value={data.cardInvoices} kind="expense" icon={CreditCard} />
          <div className="border-t pt-3"><Line label="Resultado" value={data.net} kind="net" emphasize /></div>
        </div>
        {data.cards.length > 0 && <div className="mt-6 space-y-2">
          <p className="text-xs font-semibold uppercase text-muted-foreground">Cartões</p>
          {data.cards.map((card) => <Link key={card.cardId} href={`/cartoes/${card.cardId}`} className="block rounded-xl border p-3 transition-colors hover:bg-muted/40">
            <div className="flex items-center justify-between gap-3"><p className="text-sm font-medium">{card.name}</p><p className="text-sm font-semibold">{formatCurrency(card.total)}</p></div>
            <p className="mt-1 text-xs text-muted-foreground">Fatura aberta {formatCurrency(card.currentInvoice)}{card.currentInvoiceDueDate ? ` · vence ${formatDate(card.currentInvoiceDueDate)}` : ""}{card.outstandingClosedInvoices > 0 ? ` · + fechada a vencer ${formatCurrency(card.outstandingClosedInvoices)}` : ""}</p>
          </Link>)}
        </div>}
      </CardContent></Card>

      <Card><CardContent className="p-5">
        <div className="flex items-center justify-between gap-4"><div><h2 className="font-semibold">Previsões</h2><p className="mt-1 text-xs text-muted-foreground">Valores mensais informados por você.</p></div><Button variant="outline" size="sm" asChild><Link href="/previsoes">Editar</Link></Button></div>
        <div className="mt-5 divide-y">{data.entries.length ? data.entries.map((entry) => <div key={entry.id} className="flex items-center gap-3 py-3 first:pt-0">
          <div className={cn("flex size-9 items-center justify-center rounded-xl", entry.type === "FixedExpense" ? "bg-amber-500/10 text-amber-500" : "bg-emerald-500/10 text-emerald-500")}>{entry.type === "FixedExpense" ? <ReceiptText className="size-4" /> : <ArrowUp className="size-4" />}</div>
          <div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{entry.name}</p><p className="text-xs text-muted-foreground">{typeLabels[entry.type]} · dia {entry.expectedDay}</p></div>
          <p className="text-sm font-semibold">{formatCurrency(entry.amount)}</p>
        </div>) : <p className="py-4 text-sm text-muted-foreground">Nenhuma previsão cadastrada.</p>}</div>
      </CardContent></Card>
    </div>
  </>
}

function Metric({ label, value }: { label: string; value: number }) { return <div><p className="text-xs text-slate-400">{label}</p><p className="mt-1 font-semibold">{formatCurrency(value)}</p></div> }

function Line({ label, value, kind, emphasize = false, icon: Icon }: { label: string; value: number; kind: "income" | "expense" | "net"; emphasize?: boolean; icon?: typeof CreditCard }) {
  const positive = kind === "income" || (kind === "net" && value >= 0)
  const prefix = positive ? "+ " : value !== 0 ? "− " : ""
  return <div className={cn("flex items-center justify-between gap-3 text-sm", emphasize && "text-base font-semibold")}><span className="flex items-center gap-2 text-muted-foreground">{Icon && <Icon className="size-4" />}{label}</span><span className={positive ? "text-emerald-500" : value !== 0 ? "text-rose-500" : ""}>{prefix}{formatCurrency(Math.abs(value))}</span></div>
}
