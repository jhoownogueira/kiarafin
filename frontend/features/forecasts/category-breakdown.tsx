"use client"

import { useState } from "react"
import { ChevronRight, PieChart } from "lucide-react"
import { FormDialog } from "@/components/shared/form-dialog"
import { Card, CardContent } from "@/components/ui/card"
import type { CardItem } from "@/features/cards/card-types"
import { formatCurrency, formatDate } from "@/lib/formatters"
import { cn } from "@/lib/utils"

export type CategoryInvoiceGroup = {
  categoryId: string | null
  name: string
  color: string | null
  total: number
  items: { cardName: string; item: CardItem }[]
}

const percent = new Intl.NumberFormat("pt-BR", { style: "percent", maximumFractionDigits: 1 })

/**
 * Ranking em barras horizontais (uma série, magnitude): cada linha é um botão que abre os
 * lançamentos da categoria. O nome identifica a categoria; a cor da barra não carrega identidade,
 * exceto o cinza que marca "Sem categoria".
 */
export function CategoryBreakdown({ groups }: { groups: CategoryInvoiceGroup[] }) {
  const [selected, setSelected] = useState<CategoryInvoiceGroup | null>(null)
  const total = groups.reduce((sum, group) => sum + group.total, 0)
  const max = Math.max(0, ...groups.map((group) => group.total))

  return <Card className="mt-6"><CardContent className="p-5">
    <div className="flex flex-wrap items-end justify-between gap-3">
      <div><h2 className="font-semibold">Fatura atual por categoria</h2><p className="mt-1 text-xs text-muted-foreground">Faturas abertas dos cartões, com parcelas projetadas. Clique para ver os lançamentos.</p></div>
      <p className="text-sm text-muted-foreground">Total <span className="font-semibold text-foreground">{formatCurrency(total)}</span></p>
    </div>
    {groups.length === 0 ? <div className="flex min-h-32 flex-col items-center justify-center text-center"><PieChart className="size-8 text-muted-foreground" /><p className="mt-2 text-sm text-muted-foreground">Nenhum lançamento na fatura atual.</p></div> :
      <ul className="mt-5 space-y-1" aria-label="Fatura atual por categoria">{groups.map((group) => {
        const uncategorized = group.categoryId === null
        const width = max > 0 ? Math.max(0, (group.total / max) * 100) : 0
        return <li key={group.categoryId ?? "none"}>
          <button type="button" onClick={() => setSelected(group)} title={`${group.items.length} lançamento(s)`}
            className="grid w-full grid-cols-[minmax(6rem,11rem)_1fr_7.5rem] items-center gap-3 rounded-lg px-2 py-2 text-left transition-colors hover:bg-muted/50 focus-visible:bg-muted/50 focus-visible:outline-none sm:grid-cols-[minmax(8rem,13rem)_1fr_7.5rem_3.5rem]">
            <span className={cn("truncate text-sm font-medium", uncategorized && "text-muted-foreground")}>{group.name}</span>
            <span className="h-3 overflow-hidden rounded-r bg-muted/60" aria-hidden>
              <span className={cn("block h-full rounded-r", uncategorized ? "bg-muted-foreground/45" : "bg-brand-green")} style={{ width: `${width}%` }} />
            </span>
            <span className="text-right text-sm font-semibold tabular-nums">{formatCurrency(group.total)}</span>
            <span className="hidden text-right text-xs tabular-nums text-muted-foreground sm:block">{total > 0 ? percent.format(group.total / total) : "—"}</span>
          </button>
        </li>
      })}</ul>}
    <CategoryItemsDialog group={selected} onClose={() => setSelected(null)} />
  </CardContent></Card>
}

function CategoryItemsDialog({ group, onClose }: { group: CategoryInvoiceGroup | null; onClose: () => void }) {
  return <FormDialog open={!!group} onOpenChange={(open) => { if (!open) onClose() }} maxWidth="max-w-2xl"
    title={group?.name ?? ""} description={group ? `${group.items.length} lançamento(s) · ${formatCurrency(group.total)}` : ""}>
    <div className="divide-y">{group?.items.map(({ cardName, item }) => <div key={item.id} className="flex items-center justify-between gap-3 py-2.5">
      <div className="min-w-0">
        <p className={cn("truncate text-sm font-medium", item.projected && "italic")}>{item.description}{item.totalInstallments && item.totalInstallments > 1 ? <span className="ml-2 text-xs font-normal text-muted-foreground">{item.installmentNumber}/{item.totalInstallments}</span> : null}</p>
        {item.alias && <p className="truncate text-sm text-brand-green">({item.alias})</p>}
        <p className="text-xs text-muted-foreground">{cardName} · {formatDate(item.date)}{item.projected ? " · projetada" : item.isPending ? " · pendente" : ""}{item.kind === "Credit" ? " · estorno" : ""}</p>
      </div>
      <p className={cn("shrink-0 text-sm font-semibold tabular-nums", item.effect < 0 && "text-emerald-600 dark:text-emerald-400")}>{formatCurrency(item.effect)}</p>
    </div>)}</div>
    <div className="mt-2 flex items-center justify-end gap-1 text-xs text-muted-foreground"><ChevronRight className="size-3" />Para mudar a categoria, use o lápis do lançamento em Cartões.</div>
  </FormDialog>
}
