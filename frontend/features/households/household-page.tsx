"use client"

import { useMemo, useState } from "react"
import * as Dialog from "@radix-ui/react-dialog"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation, useQuery } from "@tanstack/react-query"
import { format } from "date-fns"
import { ptBR } from "date-fns/locale"
import { Check, Copy, LoaderCircle, MailPlus, RefreshCw, Users, X } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { Skeleton } from "@/components/ui/skeleton"
import { authQueryKey, authService } from "@/features/auth/auth-service"
import { householdKeys, householdService } from "@/features/households/household-service"
import { invitationSchema, type InvitationFormData } from "@/features/households/schemas"
import { getApiErrorMessage } from "@/lib/api/errors"
import { useHouseholdSelection } from "@/providers/household-provider"

export function HouseholdPage() {
  const { data: user } = useQuery({ queryKey: authQueryKey, queryFn: authService.me })
  const { selectedHouseholdId, selectHousehold } = useHouseholdSelection()

  const household = useMemo(() => user?.households.find((item) => item.id === selectedHouseholdId) ?? user?.households.at(-1), [selectedHouseholdId, user])

  if (!household) return null

  return (
    <div>
      <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
        <div><p className="text-sm font-semibold text-brand-green">Núcleo financeiro</p><h1 className="mt-1 font-heading text-3xl font-semibold tracking-tight">Sua família</h1><p className="mt-2 text-muted-foreground">Gerencie quem compartilha este espaço financeiro com você.</p></div>
        {household.role === "Owner" && <InvitationDialog householdId={household.id} />}
      </div>

      {user && user.households.length > 1 && (
        <div className="mt-6 max-w-sm space-y-2">
          <Label htmlFor="household">Núcleo selecionado</Label>
          <select id="household" value={household.id} onChange={(event) => selectHousehold(event.target.value)} className="h-11 w-full rounded-xl border border-input bg-background px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30">
            {user.households.map((item) => <option key={item.id} value={item.id}>{item.name}</option>)}
          </select>
        </div>
      )}

      <Card className="mt-8">
        <CardHeader><div className="flex items-center gap-3"><span className="flex size-10 items-center justify-center rounded-xl bg-accent text-accent-foreground"><Users /></span><div><CardTitle>{household.name}</CardTitle><CardDescription>Pessoas com acesso às finanças deste núcleo.</CardDescription></div></div></CardHeader>
        <CardContent><MembersList householdId={household.id} /></CardContent>
      </Card>
    </div>
  )
}

function MembersList({ householdId }: { householdId: string }) {
  const members = useQuery({ queryKey: householdKeys.members(householdId), queryFn: () => householdService.getMembers(householdId) })
  if (members.isPending) return <div className="space-y-3">{Array.from({ length: 2 }).map((_, index) => <Skeleton key={index} className="h-16 w-full" />)}</div>
  if (members.isError) return <div className="rounded-xl border border-destructive/25 bg-destructive/5 p-4 text-sm"><p className="text-destructive">{getApiErrorMessage(members.error, "Não foi possível carregar os membros.")}</p><Button variant="outline" size="sm" className="mt-3" onClick={() => members.refetch()}><RefreshCw />Tentar novamente</Button></div>
  return (
    <ul className="divide-y" aria-label="Membros do núcleo financeiro">
      {members.data.map((member) => {
        const initials = member.name.split(" ").filter(Boolean).slice(0, 2).map((part) => part[0]).join("").toUpperCase()
        return (
          <li key={member.userId} className="flex items-center gap-3 py-4 first:pt-0 last:pb-0">
            <span className="flex size-10 shrink-0 items-center justify-center rounded-full bg-secondary text-xs font-bold text-secondary-foreground">{initials}</span>
            <div className="min-w-0 flex-1"><p className="truncate text-sm font-semibold">{member.name}</p><p className="truncate text-sm text-muted-foreground">{member.email}</p></div>
            <span className="rounded-full bg-muted px-2.5 py-1 text-xs font-medium">{member.role === "Owner" ? "Responsável" : "Membro"}</span>
          </li>
        )
      })}
    </ul>
  )
}

function InvitationDialog({ householdId }: { householdId: string }) {
  const [open, setOpen] = useState(false)
  const [invitationLink, setInvitationLink] = useState("")
  const form = useForm<InvitationFormData>({ resolver: zodResolver(invitationSchema), defaultValues: { email: "" } })
  const invitation = useMutation({
    mutationFn: (values: InvitationFormData) => householdService.createInvitation(householdId, values.email),
    onSuccess: (data) => {
      setInvitationLink(`${window.location.origin}/convite/${encodeURIComponent(data.token)}`)
      toast.success("Link de convite criado.")
    },
    onError: (error) => toast.error(getApiErrorMessage(error, "Não foi possível criar o convite.")),
  })

  function handleOpenChange(value: boolean) {
    setOpen(value)
    if (!value) { form.reset(); invitation.reset(); setInvitationLink("") }
  }

  async function copyLink() {
    try { await navigator.clipboard.writeText(invitationLink); toast.success("Link copiado.") }
    catch { toast.error("Não foi possível copiar. Selecione o link manualmente.") }
  }

  return (
    <Dialog.Root open={open} onOpenChange={handleOpenChange}>
      <Dialog.Trigger asChild><Button><MailPlus />Convidar pessoa</Button></Dialog.Trigger>
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-black/45 data-[state=open]:animate-in data-[state=closed]:animate-out" />
        <Dialog.Content className="fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-lg -translate-x-1/2 -translate-y-1/2 rounded-2xl border bg-card p-6 text-card-foreground shadow-2xl focus:outline-none">
          <div className="flex items-start justify-between gap-4"><div><Dialog.Title className="font-heading text-xl font-semibold">Convidar para a família</Dialog.Title><Dialog.Description className="mt-1 text-sm text-muted-foreground">O link é válido por sete dias e pode ser usado uma única vez.</Dialog.Description></div><Dialog.Close asChild><Button variant="ghost" size="icon" aria-label="Fechar"><X /></Button></Dialog.Close></div>
          {!invitation.data ? (
            <form className="mt-6 space-y-5" onSubmit={form.handleSubmit((values) => invitation.mutate(values))} noValidate>
              <div className="space-y-2"><Label htmlFor="invited-email">E-mail da pessoa <span className="font-normal text-muted-foreground">(opcional)</span></Label><Input id="invited-email" type="email" autoComplete="email" placeholder="pessoa@exemplo.com" aria-invalid={!!form.formState.errors.email} aria-describedby={form.formState.errors.email ? "invited-email-error" : "invited-email-help"} {...form.register("email")} /><p id="invited-email-help" className="text-xs text-muted-foreground">Se informado, apenas uma conta com este e-mail poderá aceitar.</p>{form.formState.errors.email && <p id="invited-email-error" role="alert" className="text-sm text-destructive">{form.formState.errors.email.message}</p>}</div>
              <div className="flex justify-end gap-2"><Dialog.Close asChild><Button type="button" variant="outline">Cancelar</Button></Dialog.Close><Button type="submit" disabled={invitation.isPending}>{invitation.isPending && <LoaderCircle className="animate-spin" />}Criar link</Button></div>
            </form>
          ) : (
            <div className="mt-6">
              <div className="flex items-center gap-2 text-sm font-medium text-brand-green"><Check />Convite pronto para compartilhar</div>
              <Label htmlFor="invitation-link" className="mt-5 block">Link do convite</Label>
              <div className="mt-2 flex gap-2"><Input id="invitation-link" readOnly value={invitationLink} onFocus={(event) => event.currentTarget.select()} /><Button type="button" size="icon" onClick={copyLink} aria-label="Copiar link"><Copy /></Button></div>
              <p className="mt-3 text-xs text-muted-foreground">Expira em {format(new Date(invitation.data.expiresAtUtc), "dd 'de' MMMM 'de' yyyy 'às' HH:mm", { locale: ptBR })}.</p>
              <div className="mt-6 flex justify-end"><Dialog.Close asChild><Button>Concluir</Button></Dialog.Close></div>
            </div>
          )}
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
