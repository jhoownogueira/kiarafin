"use client"

import Link from "next/link"
import { useState } from "react"
import { useQuery } from "@tanstack/react-query"
import { ArrowRight, CreditCard as CardIcon, Plus } from "lucide-react"
import { Card, CardContent } from "@/components/ui/card"
import { Button } from "@/components/ui/button"
import { CardFormDialog } from "@/features/cards/card-dialogs"
import { invoiceStatusLabel, type CreditCard } from "@/features/cards/card-types"
import { useFinanceContext } from "@/features/finance/selectors"
import { api } from "@/lib/api/client"
import { formatCurrency, formatDate } from "@/lib/formatters"

export function CardsPage() {
  const { householdId, accounts, members } = useFinanceContext()
  const [open, setOpen] = useState(false)
  const query = useQuery({ queryKey: ["credit-cards", householdId], queryFn: async () => (await api.get<CreditCard[]>(`/households/${householdId}/credit-cards`)).data, enabled: !!householdId })

  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div><p className="text-sm font-semibold text-brand-green">Crédito</p><h1 className="mt-1 font-heading text-3xl font-semibold">Cartões e faturas</h1><p className="mt-2 text-muted-foreground">Escolha um cartão para consultar e administrar suas faturas mês a mês.</p></div>
      <Button onClick={() => setOpen(true)}><Plus />Novo cartão</Button>
    </div>

    {query.isPending ? <p className="mt-8 text-sm text-muted-foreground">Carregando…</p> : query.data?.length ? <div className="mt-6 grid gap-5 lg:grid-cols-2">
      {query.data.map((card) => {
        const nextInvoice = card.invoices
          .filter((invoice) => invoice.status !== "Paid" && invoice.status !== "Canceled")
          .sort((left, right) => left.dueDate.localeCompare(right.dueDate))[0] ?? card.invoices[0]
        return <Card key={card.id}><CardContent className="p-5">
          <div className="rounded-2xl p-5 text-white" style={{ background: card.color || "#01212B" }}>
            <div className="flex justify-between"><div><p className="text-xs opacity-75">{card.institution}</p><p className="text-lg font-semibold">{card.name}</p></div><CardIcon /></div>
            <div className="mt-8 grid grid-cols-3 gap-2 text-xs"><div><p className="opacity-70">Limite</p><p className="font-semibold">{formatCurrency(card.totalLimit)}</p></div><div><p className="opacity-70">Usado</p><p className="font-semibold">{formatCurrency(card.usedLimit)}</p></div><div><p className="opacity-70">Disponível</p><p className="font-semibold">{formatCurrency(card.availableLimit)}</p></div></div>
          </div>
          <div className="mt-5 flex items-center justify-between gap-4 border-t pt-4">
            <div>{nextInvoice ? <><p className="text-sm font-semibold">Próxima fatura: {formatCurrency(nextInvoice.totalAmount)}</p><p className="mt-1 text-xs text-muted-foreground">Vence {formatDate(nextInvoice.dueDate)} · {invoiceStatusLabel[nextInvoice.status]}</p></> : <><p className="text-sm font-semibold">Nenhuma fatura</p><p className="mt-1 text-xs text-muted-foreground">Registre uma compra para iniciar.</p></>}</div>
            <Button asChild><Link href={`/cartoes/${card.id}`}>Acessar cartão<ArrowRight /></Link></Button>
          </div>
        </CardContent></Card>
      })}
    </div> : <Card className="mt-6 border-dashed"><CardContent className="flex min-h-56 items-center justify-center text-sm text-muted-foreground">Nenhum cartão cadastrado.</CardContent></Card>}

    <CardFormDialog open={open} onOpenChange={setOpen} householdId={householdId} accounts={accounts} members={members} />
  </div>
}
