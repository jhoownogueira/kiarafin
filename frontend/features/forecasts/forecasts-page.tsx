"use client"

import { useEffect, useMemo, useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Banknote, LoaderCircle, Pencil, Plus, ReceiptText, Sparkles, Trash2 } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"

import { ConfirmDialog } from "@/components/shared/confirm-dialog"
import { FormDialog } from "@/components/shared/form-dialog"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { forecastKeys, forecastService } from "@/features/forecasts/forecast-service"
import type { ForecastEntry, ForecastEntryType } from "@/features/forecasts/forecast-types"
import { useFinanceContext } from "@/features/finance/selectors"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatCurrency } from "@/lib/formatters"

const schema = z.object({
  name: z.string().trim().min(2, "Informe um nome com pelo menos 2 caracteres."),
  type: z.enum(["Salary", "ExtraIncome", "FixedExpense"]),
  amount: z.number().positive("Informe um valor maior que zero."),
  expectedDay: z.number().int().min(1).max(31),
})
type FormData = z.infer<typeof schema>

const typeLabels: Record<ForecastEntryType, string> = {
  Salary: "Salário",
  ExtraIncome: "Renda extra",
  FixedExpense: "Conta fixa",
}

const typeStyles: Record<ForecastEntryType, string> = {
  Salary: "bg-emerald-500/10 text-emerald-500",
  ExtraIncome: "bg-sky-500/10 text-sky-500",
  FixedExpense: "bg-amber-500/10 text-amber-500",
}

export function ForecastsPage() {
  const { householdId } = useFinanceContext()
  const queryClient = useQueryClient()
  const [open, setOpen] = useState(false)
  const [editing, setEditing] = useState<ForecastEntry | null>(null)
  const query = useQuery({
    queryKey: forecastKeys.list(householdId),
    queryFn: () => forecastService.list(householdId),
    enabled: !!householdId,
  })
  const remove = useMutation({
    mutationFn: (id: string) => forecastService.remove(householdId, id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: forecastKeys.list(householdId) })
      queryClient.invalidateQueries({ queryKey: ["dashboard", householdId] })
      toast.success("Previsão excluída.")
    },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })
  const totals = useMemo(() => {
    const items = query.data ?? []
    return {
      salary: items.filter((item) => item.type === "Salary").reduce((sum, item) => sum + item.amount, 0),
      extras: items.filter((item) => item.type === "ExtraIncome").reduce((sum, item) => sum + item.amount, 0),
      fixed: items.filter((item) => item.type === "FixedExpense").reduce((sum, item) => sum + item.amount, 0),
    }
  }, [query.data])

  const startCreate = () => {
    setEditing(null)
    setOpen(true)
  }
  const startEdit = (item: ForecastEntry) => {
    setEditing(item)
    setOpen(true)
  }

  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div>
        <p className="text-sm font-semibold text-brand-green">Planejamento</p>
        <h1 className="mt-1 font-heading text-3xl font-semibold">Previsões mensais</h1>
        <p className="mt-2 max-w-2xl text-muted-foreground">Cadastre suas premissas atuais. Elas alimentam apenas a análise de previsão e nunca criam lançamentos.</p>
      </div>
      <Button onClick={startCreate}><Plus />Nova previsão</Button>
    </div>

    <div className="mt-7 grid gap-4 sm:grid-cols-3">
      <SummaryCard label="Salários previstos" value={totals.salary} icon={Banknote} tone="text-emerald-500" />
      <SummaryCard label="Rendas extras" value={totals.extras} icon={Sparkles} tone="text-sky-500" />
      <SummaryCard label="Contas fixas" value={totals.fixed} icon={ReceiptText} tone="text-amber-500" />
    </div>

    {query.isPending ? <p className="mt-8 text-sm text-muted-foreground">Carregando previsões…</p>
      : query.isError ? <p className="mt-8 rounded-xl border p-5 text-sm text-destructive">{getApiErrorMessage(query.error)}</p>
      : query.data.length === 0 ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-56 flex-col items-center justify-center text-center">
        <Sparkles className="size-10 text-muted-foreground" />
        <p className="mt-4 font-semibold">Nenhuma previsão cadastrada</p>
        <p className="mt-1 max-w-md text-sm text-muted-foreground">Adicione salário, rendas extras e contas fixas para visualizar sua projeção no dashboard.</p>
        <Button className="mt-5" onClick={startCreate}><Plus />Adicionar primeira previsão</Button>
      </CardContent></Card>
      : <Card className="mt-6 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full min-w-[680px] text-left text-sm">
            <thead className="border-b bg-muted/35 text-xs uppercase tracking-wide text-muted-foreground"><tr>
              <th className="px-5 py-4 font-medium">Descrição</th><th className="px-5 py-4 font-medium">Tipo</th><th className="px-5 py-4 font-medium">Dia esperado</th><th className="px-5 py-4 text-right font-medium">Valor mensal</th><th className="w-28 px-5 py-4" />
            </tr></thead>
            <tbody className="divide-y">{query.data.map((item) => <tr key={item.id} className="transition-colors hover:bg-muted/20">
              <td className="px-5 py-4 font-medium">{item.name}</td>
              <td className="px-5 py-4"><span className={`inline-flex rounded-full px-2.5 py-1 text-xs font-semibold ${typeStyles[item.type]}`}>{typeLabels[item.type]}</span></td>
              <td className="px-5 py-4 text-muted-foreground">Dia {item.expectedDay}</td>
              <td className="px-5 py-4 text-right font-semibold">{formatCurrency(item.amount)}</td>
              <td className="px-5 py-4"><div className="flex justify-end gap-1">
                <Button variant="ghost" size="icon" aria-label={`Editar ${item.name}`} onClick={() => startEdit(item)}><Pencil /></Button>
                <ConfirmDialog trigger={<Button variant="ghost" size="icon" aria-label={`Excluir ${item.name}`}><Trash2 className="text-destructive" /></Button>} title="Excluir previsão?" description={`“${item.name}” deixará de participar da análise do dashboard. Nenhum lançamento será alterado.`} confirmLabel="Excluir" destructive pending={remove.isPending} onConfirm={() => remove.mutate(item.id)} />
              </div></td>
            </tr>)}</tbody>
          </table>
        </div>
      </Card>}

    <ForecastForm open={open} onOpenChange={setOpen} householdId={householdId} entry={editing} />
  </div>
}

function SummaryCard({ label, value, icon: Icon, tone }: { label: string; value: number; icon: typeof Banknote; tone: string }) {
  return <Card><CardContent className="p-5"><div className="flex items-center justify-between"><p className="text-sm text-muted-foreground">{label}</p><Icon className={`size-5 ${tone}`} /></div><p className="mt-3 text-2xl font-semibold">{formatCurrency(value)}</p><p className="mt-1 text-xs text-muted-foreground">por mês</p></CardContent></Card>
}

function ForecastForm({ open, onOpenChange, householdId, entry }: { open: boolean; onOpenChange: (open: boolean) => void; householdId: string; entry: ForecastEntry | null }) {
  const queryClient = useQueryClient()
  const form = useForm<FormData>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", type: "FixedExpense", amount: 0, expectedDay: 10 },
  })
  useEffect(() => {
    form.reset(entry
      ? { name: entry.name, type: entry.type, amount: entry.amount, expectedDay: entry.expectedDay }
      : { name: "", type: "FixedExpense", amount: 0, expectedDay: 10 })
  }, [entry, form, open])
  const mutation = useMutation({
    mutationFn: (values: FormData) => entry
      ? forecastService.update(householdId, entry.id, values)
      : forecastService.create(householdId, values),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: forecastKeys.list(householdId) })
      queryClient.invalidateQueries({ queryKey: ["dashboard", householdId] })
      toast.success(entry ? "Previsão atualizada." : "Previsão criada.")
      onOpenChange(false)
    },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })
  return <FormDialog open={open} onOpenChange={onOpenChange} title={entry ? "Editar previsão" : "Nova previsão"} description="O valor será repetido como premissa mensal até você alterá-lo.">
    <form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((values) => mutation.mutate(values))}>
      <Field label="Nome" error={form.formState.errors.name?.message}><Input placeholder="Ex.: Salário principal" {...form.register("name")} /></Field>
      <Field label="Tipo"><select className="h-11 w-full rounded-xl border bg-background px-3" {...form.register("type")}><option value="Salary">Salário</option><option value="ExtraIncome">Renda extra</option><option value="FixedExpense">Conta fixa</option></select></Field>
      <Field label="Valor mensal" error={form.formState.errors.amount?.message}><Input type="number" min="0.01" step="0.01" {...form.register("amount", { valueAsNumber: true })} /></Field>
      <Field label="Dia esperado" error={form.formState.errors.expectedDay?.message}><Input type="number" min="1" max="31" {...form.register("expectedDay", { valueAsNumber: true })} /></Field>
      <div className="flex justify-end sm:col-span-2"><Button disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Salvar previsão</Button></div>
    </form>
  </FormDialog>
}

function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) {
  return <label className="space-y-2 text-sm font-medium"><span>{label}</span>{children}{error && <span className="block text-xs text-destructive">{error}</span>}</label>
}
