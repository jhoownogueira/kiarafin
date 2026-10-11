"use client"

import Link from "next/link"
import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { ArrowDown, ArrowUp, CalendarClock, CircleDollarSign, Plus, WalletCards } from "lucide-react"
import { Area, AreaChart, Bar, BarChart, CartesianGrid, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { api } from "@/lib/api/client"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency, formatDate } from "@/lib/formatters"
import { useFinanceContext } from "@/features/finance/selectors"

type Dashboard = { period: string; indicators: { availableBalance: number; monthIncome: number; monthExpenses: number; projectedBalance: number; upcomingBills: number; openInvoices: number; committedInstallments: number }; incomeVsExpenses: { period: string; income: number; expenses: number }[]; expensesByCategory: { categoryId: string; name: string; color: string; amount: number }[]; balanceEvolution: { period: string; balance: number }[]; upcomingDue: { id: string; description: string; amount: number; dueDate: string }[]; latestTransactions: { id: string; description: string; amount: number; type: string; status: string; competenceDate: string }[] }
const currentMonth = () => new Date().toISOString().slice(0, 7)
const monthLabel = (value: string) => new Intl.DateTimeFormat("pt-BR", { month: "short", year: "2-digit", timeZone: "UTC" }).format(new Date(`${value}-01T12:00:00Z`))

export function DashboardPage() {
  const { householdId } = useFinanceContext()
  const [month, setMonth] = useState(currentMonth)
  const query = useQuery({ queryKey: ["dashboard", householdId, month], queryFn: async () => (await api.get<Dashboard>(`/households/${householdId}/dashboard`, { params: { month } })).data, enabled: !!householdId })
  if (query.isPending) return <p className="text-sm text-muted-foreground">Carregando visão financeira…</p>
  if (query.isError) return <p className="rounded-xl border p-5 text-sm text-destructive">{getApiErrorMessage(query.error)}</p>
  const data = query.data
  const cards = [
    { label: "Saldo disponível", value: data.indicators.availableBalance, icon: WalletCards },
    { label: "Receitas do mês", value: data.indicators.monthIncome, icon: ArrowUp },
    { label: "Despesas do mês", value: data.indicators.monthExpenses, icon: ArrowDown },
    { label: "Saldo projetado", value: data.indicators.projectedBalance, icon: CircleDollarSign },
  ]
  const empty = data.latestTransactions.length === 0
  return <div><div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><p className="text-sm font-semibold text-brand-green">Visão geral</p><h1 className="mt-1 font-heading text-3xl font-semibold">Sua vida financeira, com clareza</h1><p className="mt-2 text-muted-foreground">Indicadores calculados com dados reais do núcleo familiar.</p></div><div className="flex gap-2"><input aria-label="Mês" type="month" className="h-10 rounded-xl border bg-background px-3" value={month} onChange={(e) => setMonth(e.target.value)} /><Button asChild><Link href="/transacoes"><Plus />Novo lançamento</Link></Button></div></div>
    <div className="mt-7 grid gap-4 sm:grid-cols-2 xl:grid-cols-4">{cards.map(({ label, value, icon: Icon }) => <Card key={label}><CardContent className="p-5"><div className="flex items-center justify-between"><p className="text-sm text-muted-foreground">{label}</p><Icon className="size-5 text-brand-green" /></div><p className="mt-3 text-2xl font-semibold">{formatCurrency(value)}</p></CardContent></Card>)}</div>
    <div className="mt-4 grid gap-4 sm:grid-cols-3"><Small label="Contas a vencer" value={String(data.indicators.upcomingBills)} /><Small label="Faturas em aberto" value={formatCurrency(data.indicators.openInvoices)} /><Small label="Parcelas comprometidas" value={formatCurrency(data.indicators.committedInstallments)} /></div>
    {empty ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-56 flex-col items-center justify-center text-center"><CircleDollarSign className="size-10 text-muted-foreground" /><p className="mt-4 font-semibold">Comece pelo primeiro lançamento</p><p className="mt-1 text-sm text-muted-foreground">Os gráficos serão preenchidos automaticamente, sem dados fictícios.</p></CardContent></Card> : <><div className="mt-6 grid gap-4 lg:grid-cols-2"><ChartCard title="Receitas versus despesas"><ResponsiveContainer width="100%" height={260}><BarChart data={data.incomeVsExpenses}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="period" tickFormatter={monthLabel} /><YAxis hide /><Tooltip formatter={(v) => formatCurrency(Number(v))} labelFormatter={(v) => monthLabel(String(v))} /><Bar dataKey="income" name="Receitas" fill="#028A61" radius={[6, 6, 0, 0]} /><Bar dataKey="expenses" name="Despesas" fill="#d97757" radius={[6, 6, 0, 0]} /></BarChart></ResponsiveContainer></ChartCard><ChartCard title="Evolução do saldo"><ResponsiveContainer width="100%" height={260}><AreaChart data={data.balanceEvolution}><CartesianGrid strokeDasharray="3 3" vertical={false} /><XAxis dataKey="period" tickFormatter={monthLabel} /><YAxis hide /><Tooltip formatter={(v) => formatCurrency(Number(v))} labelFormatter={(v) => monthLabel(String(v))} /><Area dataKey="balance" name="Saldo" stroke="#028A61" fill="#028A6122" /></AreaChart></ResponsiveContainer></ChartCard></div><div className="mt-4 grid gap-4 lg:grid-cols-2"><Card><CardContent className="p-5"><h2 className="font-semibold">Próximos vencimentos</h2><div className="mt-4 space-y-3">{data.upcomingDue.length ? data.upcomingDue.map((item) => <div key={item.id} className="flex items-center gap-3"><CalendarClock className="size-4 text-muted-foreground" /><div className="flex-1"><p className="text-sm font-medium">{item.description}</p><p className="text-xs text-muted-foreground">{formatDate(item.dueDate)}</p></div><p className="text-sm font-semibold">{formatCurrency(item.amount)}</p></div>) : <p className="text-sm text-muted-foreground">Nenhum vencimento próximo.</p>}</div></CardContent></Card><Card><CardContent className="p-5"><h2 className="font-semibold">Últimos lançamentos</h2><div className="mt-4 space-y-3">{data.latestTransactions.map((item) => <div key={item.id} className="flex items-center justify-between"><div><p className="text-sm font-medium">{item.description}</p><p className="text-xs text-muted-foreground">{formatDate(item.competenceDate)}</p></div><p className="text-sm font-semibold">{formatCurrency(item.amount)}</p></div>)}</div></CardContent></Card></div></>}
  </div>
}

function Small({ label, value }: { label: string; value: string }) { return <Card><CardContent className="p-4"><p className="text-xs text-muted-foreground">{label}</p><p className="mt-1 font-semibold">{value}</p></CardContent></Card> }
function ChartCard({ title, children }: { title: string; children: React.ReactNode }) { return <Card><CardContent className="p-5"><h2 className="mb-4 font-semibold">{title}</h2>{children}</CardContent></Card> }
