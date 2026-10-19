"use client"

import Link from "next/link"
import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { ArrowDown, ArrowUp, CalendarClock, CircleDollarSign, CreditCard, Plus, ReceiptText, Sparkles, TrendingUp, WalletCards } from "lucide-react"
import { Area, AreaChart, Bar, BarChart, CartesianGrid, Legend, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts"

import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { useFinanceContext } from "@/features/finance/selectors"
import type { ForecastEntryType } from "@/features/forecasts/forecast-types"
import { api } from "@/lib/api/client"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate } from "@/lib/formatters"
import { cn } from "@/lib/utils"

type BalanceForecast = {
  summary: { monthlySalary: number; monthlyExtraIncome: number; monthlyFixedExpenses: number; knownCardInvoices: number; estimatedCardInvoices: number; projectedBalance: number }
  months: { period: string; salary: number; extraIncome: number; fixedExpenses: number; cardInvoices: number; estimatedCardInvoices: number; net: number; projectedBalance: number }[]
  entries: { id: string; name: string; type: ForecastEntryType; amount: number; expectedDay: number }[]
}

type Dashboard = {
  period: string
  indicators: { availableBalance: number; monthIncome: number; monthExpenses: number; projectedBalance: number; upcomingBills: number; openInvoices: number; committedInstallments: number }
  incomeVsExpenses: { period: string; income: number; expenses: number }[]
  expensesByCategory: { categoryId: string; name: string; color: string; amount: number }[]
  balanceEvolution: { period: string; balance: number }[]
  upcomingDue: { id: string; description: string; amount: number; dueDate: string }[]
  latestTransactions: { id: string; description: string; amount: number; type: string; status: string; competenceDate: string }[]
  balanceForecast: BalanceForecast
}

const currentMonth = () => new Date().toISOString().slice(0, 7)
const monthLabel = (value: string) => new Intl.DateTimeFormat("pt-BR", { month: "short", year: "2-digit", timeZone: "UTC" }).format(new Date(`${value}-01T12:00:00Z`))
const forecastTypeLabels: Record<ForecastEntryType, string> = { Salary: "Salário", ExtraIncome: "Renda extra", FixedExpense: "Conta fixa" }

export function DashboardPage() {
  const { householdId } = useFinanceContext()
  const [month, setMonth] = useState(currentMonth)
  const [tab, setTab] = useState<"overview" | "forecast">("overview")
  const query = useQuery({ queryKey: ["dashboard", householdId, month], queryFn: async () => (await api.get<Dashboard>(`/households/${householdId}/dashboard`, { params: { month } })).data, enabled: !!householdId })
  if (query.isPending) return <p className="text-sm text-muted-foreground">Carregando visão financeira…</p>
  if (query.isError) return <p className="rounded-xl border p-5 text-sm text-destructive">{getApiErrorMessage(query.error)}</p>
  const data = query.data

  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div><p className="text-sm font-semibold text-brand-green">Visão geral</p><h1 className="mt-1 font-heading text-3xl font-semibold">Sua vida financeira, com clareza</h1><p className="mt-2 text-muted-foreground">Acompanhe o que aconteceu e planeje os próximos meses.</p></div>
      <div className="flex gap-2"><input aria-label="Mês" type="month" className="h-10 rounded-xl border bg-background px-3" value={month} onChange={(event) => setMonth(event.target.value)} /><Button asChild><Link href="/transacoes"><Plus />Novo lançamento</Link></Button></div>
    </div>
    <div className="mt-7 inline-flex rounded-xl border bg-muted/30 p-1" role="tablist" aria-label="Visualização do dashboard">
      <button type="button" role="tab" aria-selected={tab === "overview"} className={cn("rounded-lg px-4 py-2 text-sm font-semibold transition-colors", tab === "overview" ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground")} onClick={() => setTab("overview")}>Resumo financeiro</button>
      <button type="button" role="tab" aria-selected={tab === "forecast"} className={cn("rounded-lg px-4 py-2 text-sm font-semibold transition-colors", tab === "forecast" ? "bg-background text-foreground shadow-sm" : "text-muted-foreground hover:text-foreground")} onClick={() => setTab("forecast")}><span className="inline-flex items-center gap-2"><Sparkles className="size-4 text-brand-green" />Previsão de saldo</span></button>
    </div>
    {tab === "overview" ? <Overview data={data} /> : <ForecastAnalysis forecast={data.balanceForecast} />}
  </div>
}

function Overview({ data }: { data: Dashboard }) {
  const cards = [
    { label: "Saldo disponível", value: data.indicators.availableBalance, icon: WalletCards },
    { label: "Receitas do mês", value: data.indicators.monthIncome, icon: ArrowUp },
    { label: "Despesas do mês", value: data.indicators.monthExpenses, icon: ArrowDown },
    { label: "Saldo projetado", value: data.indicators.projectedBalance, icon: CircleDollarSign },
  ]
  const empty = data.latestTransactions.length === 0
  return <>
    <div className="mt-7 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">{cards.map(({ label, value, icon: Icon }) => <Card key={label}><CardContent className="p-5"><div className="flex items-center justify-between"><p className="text-sm text-muted-foreground">{label}</p><Icon className="size-5 text-brand-green" /></div><p className="mt-3 text-2xl font-semibold">{formatCurrency(value)}</p></CardContent></Card>)}</div>
    <div className="mt-4 grid gap-4 sm:grid-cols-3"><Small label="Contas a vencer" value={String(data.indicators.upcomingBills)} /><Small label="Faturas em aberto" value={formatCurrency(data.indicators.openInvoices)} /><Small label="Parcelas comprometidas" value={formatCurrency(data.indicators.committedInstallments)} /></div>
    {empty ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-56 flex-col items-center justify-center text-center"><CircleDollarSign className="size-10 text-muted-foreground" /><p className="mt-4 font-semibold">Comece pelo primeiro lançamento</p><p className="mt-1 text-sm text-muted-foreground">Os gráficos serão preenchidos automaticamente, sem dados fictícios.</p></CardContent></Card> : <>
      <div className="mt-6 grid gap-4 lg:grid-cols-2"><ChartCard title="Receitas versus despesas"><ResponsiveContainer width="100%" height={260}><BarChart data={data.incomeVsExpenses}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="period" tickFormatter={monthLabel} /><YAxis hide /><Tooltip formatter={(value) => formatCurrency(Number(value))} labelFormatter={(value) => monthLabel(String(value))} /><Bar dataKey="income" name="Receitas" fill="#028A61" radius={[6, 6, 0, 0]} /><Bar dataKey="expenses" name="Despesas" fill="#d97757" radius={[6, 6, 0, 0]} /></BarChart></ResponsiveContainer></ChartCard><ChartCard title="Evolução do saldo"><ResponsiveContainer width="100%" height={260}><AreaChart data={data.balanceEvolution}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="period" tickFormatter={monthLabel} /><YAxis hide /><Tooltip formatter={(value) => formatCurrency(Number(value))} labelFormatter={(value) => monthLabel(String(value))} /><Area dataKey="balance" name="Saldo" stroke="#028A61" fill="#028A6122" /></AreaChart></ResponsiveContainer></ChartCard></div>
      <div className="mt-4 grid gap-4 lg:grid-cols-2"><Card><CardContent className="p-5"><h2 className="font-semibold">Próximos vencimentos</h2><div className="mt-4 space-y-3">{data.upcomingDue.length ? data.upcomingDue.map((item) => <div key={item.id} className="flex items-center gap-3"><CalendarClock className="size-4 text-muted-foreground" /><div className="flex-1"><p className="text-sm font-medium">{item.description}</p><p className="text-xs text-muted-foreground">{formatDate(item.dueDate)}</p></div><p className="text-sm font-semibold">{formatCurrency(item.amount)}</p></div>) : <p className="text-sm text-muted-foreground">Nenhum vencimento próximo.</p>}</div></CardContent></Card><Card><CardContent className="p-5"><h2 className="font-semibold">Últimos lançamentos</h2><div className="mt-4 space-y-3">{data.latestTransactions.map((item) => <div key={item.id} className="flex items-center justify-between"><div><p className="text-sm font-medium">{item.description}</p><p className="text-xs text-muted-foreground">{formatDate(item.competenceDate)}</p></div><p className="text-sm font-semibold">{formatCurrency(item.amount)}</p></div>)}</div></CardContent></Card></div>
    </>}
  </>
}

function ForecastAnalysis({ forecast }: { forecast: BalanceForecast }) {
  const { summary, months, entries } = forecast
  const currentMonthData = months[0]
  const monthlyIncome = summary.monthlySalary + summary.monthlyExtraIncome
  const configured = entries.length > 0
  return <>
    <div className="mt-7 overflow-hidden rounded-2xl border bg-gradient-to-br from-emerald-950 via-slate-950 to-slate-900 p-6 text-white shadow-lg sm:p-7">
      <div className="flex flex-col justify-between gap-6 lg:flex-row lg:items-end">
        <div><div className="flex items-center gap-2 text-sm font-semibold text-emerald-300"><TrendingUp className="size-4" />PROJEÇÃO PARA 6 MESES, INCLUINDO O ATUAL</div><p className="mt-3 text-sm text-slate-300">Resultado acumulado somente das previsões e cartões</p><p className={cn("mt-1 text-4xl font-semibold tracking-tight", summary.projectedBalance < 0 && "text-rose-300")}>{formatCurrency(summary.projectedBalance)}</p></div>
        <div className="grid grid-cols-2 gap-x-8 gap-y-4 text-sm sm:grid-cols-4"><ForecastMetric label="Entradas/mês" value={monthlyIncome} /><ForecastMetric label="Fixas/mês" value={summary.monthlyFixedExpenses} /><ForecastMetric label="Cartão atual" value={(currentMonthData?.cardInvoices ?? 0) + (currentMonthData?.estimatedCardInvoices ?? 0)} /><ForecastMetric label="Resultado mês atual" value={currentMonthData?.net ?? 0} /></div>
      </div>
    </div>

    {!configured && summary.knownCardInvoices === 0 && summary.estimatedCardInvoices === 0 ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-56 flex-col items-center justify-center text-center"><Sparkles className="size-10 text-brand-green" /><p className="mt-4 font-semibold">Configure suas premissas mensais</p><p className="mt-1 max-w-lg text-sm text-muted-foreground">Cadastre salário, rendas extras e contas fixas para construir uma previsão útil.</p><Button className="mt-5" asChild><Link href="/previsoes"><Plus />Cadastrar previsões</Link></Button></CardContent></Card> : <>
      <div className="mt-6 grid gap-4 xl:grid-cols-2">
        <ChartCard title="Entradas e saídas previstas" subtitle="Faturas futuras sem dados reais usam a média das 3 últimas faturas pagas."><ResponsiveContainer width="100%" height={290}><BarChart data={months}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="period" tickFormatter={monthLabel} interval={0} /><YAxis hide /><Tooltip formatter={(value) => formatCurrency(Number(value))} labelFormatter={(value) => monthLabel(String(value))} /><Legend /><Bar dataKey="salary" stackId="income" name="Salário" fill="#059669" radius={[5, 5, 0, 0]} /><Bar dataKey="extraIncome" stackId="income" name="Extras" fill="#38bdf8" radius={[5, 5, 0, 0]} /><Bar dataKey="fixedExpenses" stackId="expenses" name="Contas fixas" fill="#f59e0b" radius={[5, 5, 0, 0]} /><Bar dataKey="cardInvoices" stackId="expenses" name="Cartões reais" fill="#f43f5e" radius={[5, 5, 0, 0]} /><Bar dataKey="estimatedCardInvoices" stackId="expenses" name="Cartões estimados" fill="#a78bfa" radius={[5, 5, 0, 0]} /></BarChart></ResponsiveContainer></ChartCard>
        <ChartCard title="Evolução do resultado previsto" subtitle="Acumulado das previsões após contas fixas e faturas, sem usar o saldo bancário."><ResponsiveContainer width="100%" height={290}><AreaChart data={months} margin={{ left: 24, right: 24 }}><defs><linearGradient id="forecastFill" x1="0" y1="0" x2="0" y2="1"><stop offset="5%" stopColor="#10b981" stopOpacity={0.35} /><stop offset="95%" stopColor="#10b981" stopOpacity={0.02} /></linearGradient></defs><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="period" tickFormatter={monthLabel} interval={0} /><YAxis hide /><Tooltip formatter={(value) => formatCurrency(Number(value))} labelFormatter={(value) => monthLabel(String(value))} /><Area type="monotone" dataKey="projectedBalance" name="Resultado acumulado" stroke="#10b981" strokeWidth={3} fill="url(#forecastFill)" /></AreaChart></ResponsiveContainer></ChartCard>
      </div>

      <div className="mt-4 grid gap-4 lg:grid-cols-[1.2fr_.8fr]">
        <Card><CardContent className="p-5"><div className="flex items-center justify-between gap-4"><div><h2 className="font-semibold">Premissas mensais</h2><p className="mt-1 text-xs text-muted-foreground">Valores informados manualmente por você.</p></div><Button variant="outline" size="sm" asChild><Link href="/previsoes">Editar previsões</Link></Button></div><div className="mt-5 divide-y">{entries.length ? entries.map((entry) => <div key={entry.id} className="flex items-center gap-3 py-3 first:pt-0"><div className={cn("flex size-9 items-center justify-center rounded-xl", entry.type === "FixedExpense" ? "bg-amber-500/10 text-amber-500" : "bg-emerald-500/10 text-emerald-500")}>{entry.type === "FixedExpense" ? <ReceiptText className="size-4" /> : <ArrowUp className="size-4" />}</div><div className="min-w-0 flex-1"><p className="truncate text-sm font-medium">{entry.name}</p><p className="text-xs text-muted-foreground">{forecastTypeLabels[entry.type]} · dia {entry.expectedDay}</p></div><p className="text-sm font-semibold">{formatCurrency(entry.amount)}</p></div>) : <p className="py-4 text-sm text-muted-foreground">Nenhuma premissa cadastrada.</p>}</div></CardContent></Card>
        <Card><CardContent className="p-5"><h2 className="font-semibold">Composição de {currentMonthData ? monthLabel(currentMonthData.period) : "agora"}</h2><p className="mt-1 text-xs text-muted-foreground">Detalhamento do primeiro mês projetado.</p>{currentMonthData && <div className="mt-5 space-y-3"><Breakdown label="Salários" value={currentMonthData.salary} kind="income" /><Breakdown label="Rendas extras" value={currentMonthData.extraIncome} kind="income" /><Breakdown label="Contas fixas" value={currentMonthData.fixedExpenses} kind="expense" /><Breakdown label="Faturas de cartão" value={currentMonthData.cardInvoices} kind="expense" icon={CreditCard} />{currentMonthData.estimatedCardInvoices > 0 && <Breakdown label="Cartões estimados" value={currentMonthData.estimatedCardInvoices} kind="expense" icon={Sparkles} />}<div className="border-t pt-3"><Breakdown label="Resultado do mês" value={currentMonthData.net} kind="net" emphasize /></div></div>}<p className="mt-5 rounded-xl bg-muted/50 p-3 text-xs leading-5 text-muted-foreground">O valor real do Pluggy sempre substitui a estimativa do cartão naquele mês. Movimentações de conta não são duplicadas nesta análise.</p></CardContent></Card>
      </div>
    </>}
  </>
}

function ForecastMetric({ label, value }: { label: string; value: number }) { return <div><p className="text-xs text-slate-400">{label}</p><p className="mt-1 font-semibold">{formatCurrency(value)}</p></div> }
function Breakdown({ label, value, kind, emphasize = false, icon: Icon }: { label: string; value: number; kind: "income" | "expense" | "net"; emphasize?: boolean; icon?: typeof CreditCard }) {
  const positive = kind === "income" || (kind === "net" && value >= 0)
  const prefix = positive ? "+ " : value !== 0 ? "− " : ""
  return <div className={cn("flex items-center justify-between gap-3 text-sm", emphasize && "font-semibold")}><span className="flex items-center gap-2 text-muted-foreground">{Icon && <Icon className="size-4" />}{label}</span><span className={positive ? "text-emerald-500" : value !== 0 ? "text-rose-500" : ""}>{prefix}{formatCurrency(Math.abs(value))}</span></div>
}
function Small({ label, value }: { label: string; value: string }) { return <Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 font-semibold">{value}</p></CardContent></Card> }
function ChartCard({ title, subtitle, children }: { title: string; subtitle?: string; children: React.ReactNode }) { return <Card><CardContent className="p-5"><h2 className="font-semibold">{title}</h2>{subtitle && <p className="mt-1 text-xs text-muted-foreground">{subtitle}</p>}<div className="mt-4">{children}</div></CardContent></Card> }
