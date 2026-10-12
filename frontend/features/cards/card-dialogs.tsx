"use client"

import { useMutation, useQueryClient } from "@tanstack/react-query"
import { LoaderCircle } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { FormDialog } from "@/components/shared/form-dialog"
import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import type { CreditCard } from "@/features/cards/card-types"
import { AccountSelect, CategorySelect, ResponsibleSelect, useFinanceContext } from "@/features/finance/selectors"
import { api } from "@/lib/api/client"
import { getApiErrorMessage } from "@/lib/api/errors"
import { todayIso } from "@/lib/formatters"

type CardForm = { name: string; institution: string; brand: string; color: string; totalLimit: number; closingDay: number; dueDay: number; responsibleUserId: string; defaultPaymentAccountId: string }
type PurchaseForm = { description: string; totalAmount: number; installmentCount: number; purchaseDate: string; firstInstallmentDate: string; categoryId: string; responsibleUserId: string; scope: "Individual" | "Shared" }

export function CardFormDialog({ open, onOpenChange, householdId, accounts, members }: { open: boolean; onOpenChange: (value: boolean) => void; householdId: string; accounts: ReturnType<typeof useFinanceContext>["accounts"]; members: ReturnType<typeof useFinanceContext>["members"] }) {
  const queryClient = useQueryClient()
  const form = useForm<CardForm>({ values: { name: "", institution: "", brand: "", color: "#01212B", totalLimit: 0, closingDay: 5, dueDay: 12, responsibleUserId: members[0]?.userId ?? "", defaultPaymentAccountId: accounts[0]?.id ?? "" } })
  const mutation = useMutation({
    mutationFn: (value: CardForm) => api.post(`/households/${householdId}/credit-cards`, value),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["credit-cards", householdId] }); toast.success("Cartão criado."); onOpenChange(false) },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })

  return <FormDialog open={open} onOpenChange={onOpenChange} title="Novo cartão" description="Configure fechamento, vencimento e conta de pagamento.">
    <form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((value) => mutation.mutate(value))}>
      <Field label="Nome"><Input required {...form.register("name")} /></Field><Field label="Instituição"><Input {...form.register("institution")} /></Field>
      <Field label="Bandeira"><Input {...form.register("brand")} /></Field><Field label="Cor"><Input type="color" {...form.register("color")} /></Field>
      <Field label="Limite"><Input type="number" step="0.01" {...form.register("totalLimit", { valueAsNumber: true })} /></Field><Field label="Fechamento"><Input type="number" min="1" max="31" {...form.register("closingDay", { valueAsNumber: true })} /></Field>
      <Field label="Vencimento"><Input type="number" min="1" max="31" {...form.register("dueDay", { valueAsNumber: true })} /></Field><Field label="Conta de pagamento"><AccountSelect accounts={accounts} required {...form.register("defaultPaymentAccountId")} /></Field>
      <Field label="Responsável"><ResponsibleSelect members={members} required {...form.register("responsibleUserId")} /></Field>
      <div className="flex justify-end sm:col-span-2"><Button disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Salvar</Button></div>
    </form>
  </FormDialog>
}

export function PurchaseDialog({ card, onOpenChange, householdId, categories, members }: { card: CreditCard | null; onOpenChange: (value: boolean) => void; householdId: string; categories: ReturnType<typeof useFinanceContext>["categories"]; members: ReturnType<typeof useFinanceContext>["members"] }) {
  const queryClient = useQueryClient()
  const form = useForm<PurchaseForm>({ values: { description: "", totalAmount: 0, installmentCount: 1, purchaseDate: todayIso(), firstInstallmentDate: todayIso(), categoryId: "", responsibleUserId: members[0]?.userId ?? "", scope: "Shared" } })
  const mutation = useMutation({
    mutationFn: (value: PurchaseForm) => api.post(`/households/${householdId}/credit-cards/${card!.id}/purchases`, { ...value, idempotencyKey: crypto.randomUUID() }),
    onSuccess: async () => { await queryClient.invalidateQueries({ queryKey: ["credit-cards", householdId] }); toast.success("Compra distribuída nas faturas."); onOpenChange(false) },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })

  return <FormDialog open={!!card} onOpenChange={onOpenChange} title="Compra no cartão" description="O limite considera o valor total; a conta bancária não será debitada agora.">
    <form className="grid gap-4 sm:grid-cols-2" onSubmit={form.handleSubmit((value) => mutation.mutate(value))}>
      <Field label="Descrição"><Input required {...form.register("description")} /></Field><Field label="Valor total"><Input type="number" step="0.01" {...form.register("totalAmount", { valueAsNumber: true })} /></Field>
      <Field label="Parcelas"><Input type="number" min="1" max="120" {...form.register("installmentCount", { valueAsNumber: true })} /></Field><Field label="Data da compra"><Input type="date" {...form.register("purchaseDate")} /></Field>
      <Field label="Primeira parcela"><Input type="date" {...form.register("firstInstallmentDate")} /></Field><Field label="Categoria"><CategorySelect categories={categories} type="Expense" required {...form.register("categoryId")} /></Field>
      <Field label="Responsável"><ResponsibleSelect members={members} required {...form.register("responsibleUserId")} /></Field><Field label="Escopo"><select className="h-11 w-full rounded-xl border bg-background px-3" {...form.register("scope")}><option value="Shared">Compartilhada</option><option value="Individual">Individual</option></select></Field>
      <div className="flex justify-end sm:col-span-2"><Button disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Registrar compra</Button></div>
    </form>
  </FormDialog>
}

function Field({ label, children }: { label: string; children: React.ReactNode }) { return <label className="space-y-2 text-sm font-medium"><span>{label}</span>{children}</label> }
