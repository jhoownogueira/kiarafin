"use client"

import { useEffect, useState } from "react"
import * as AlertDialog from "@radix-ui/react-alert-dialog"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { AlertTriangle, Building2, CheckCircle2, Clock3, Cloud, CloudOff, LoaderCircle, RefreshCw, ShieldCheck, WalletCards } from "lucide-react"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { authQueryKey, authService } from "@/features/auth/auth-service"
import { getApiErrorMessage } from "@/lib/api/errors"
import { formatDateInSaoPaulo, formatDateTime } from "@/lib/formatters"
import { openFinanceKeys, openFinanceService } from "@/features/open-finance/open-finance-service"
import type { PluggyIntegrationPreview, PluggyIntegrationState } from "@/features/open-finance/types"

const AUTHORIZED_EMAIL = "jhoownogueira@outlook.com"

const stateLabel: Record<PluggyIntegrationState, string> = {
  Disabled: "Desativada",
  PendingConfirmation: "Aguardando confirmação",
  InitialSync: "Importação inicial",
  Connected: "Conectada",
  Syncing: "Sincronizando",
  Error: "Atenção necessária",
  ConsentExpired: "Consentimento expirado",
}

const busyStates: PluggyIntegrationState[] = ["InitialSync", "Syncing"]

export function OpenFinancePage() {
  const session = useQuery({ queryKey: authQueryKey, queryFn: authService.me, retry: false })

  if (session.isPending) return <p className="text-sm text-muted-foreground">Verificando acesso…</p>
  if (session.data?.email.toLowerCase() !== AUTHORIZED_EMAIL) {
    return <Card><CardContent className="flex min-h-56 flex-col items-center justify-center p-6 text-center"><ShieldCheck className="size-10 text-muted-foreground" /><h1 className="mt-4 font-heading text-2xl font-semibold">Acesso restrito</h1><p className="mt-2 max-w-md text-sm text-muted-foreground">Esta integração não está disponível para este usuário.</p></CardContent></Card>
  }

  return <AuthorizedOpenFinancePage />
}

function AuthorizedOpenFinancePage() {
  const [renderedAt] = useState(() => Date.now())
  const [preview, setPreview] = useState<PluggyIntegrationPreview | null>(null)
  const [confirmationOpen, setConfirmationOpen] = useState(false)
  const [acknowledged, setAcknowledged] = useState(false)
  const [syncRequestedAt, setSyncRequestedAt] = useState<number | null>(null)
  const queryClient = useQueryClient()
  useEffect(() => {
    if (syncRequestedAt === null) return
    const timeout = window.setTimeout(() => setSyncRequestedAt(null), 120_000)
    return () => window.clearTimeout(timeout)
  }, [syncRequestedAt])
  const status = useQuery({
    queryKey: openFinanceKeys.status,
    queryFn: openFinanceService.status,
    retry: false,
    refetchInterval: (query) =>
      (Boolean(query.state.data && busyStates.includes(query.state.data.status)) ||
        Boolean(syncRequestedAt && Date.now() - syncRequestedAt < 120_000))
        ? 3_000
        : false,
  })
  const prepare = useMutation({
    mutationFn: openFinanceService.preview,
    onSuccess: (data) => { setPreview(data); setAcknowledged(false); setConfirmationOpen(true) },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })
  const activate = useMutation({
    mutationFn: openFinanceService.activate,
    onSuccess: async (data) => {
      queryClient.setQueryData(openFinanceKeys.status, data)
      setConfirmationOpen(false)
      toast.success(data.initialImportCompleted ? "Integração ativada com sucesso." : "Importação inicial iniciada.")
      await queryClient.invalidateQueries({ queryKey: openFinanceKeys.status })
    },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })
  const sync = useMutation({
    mutationFn: openFinanceService.sync,
    onSuccess: async () => { setSyncRequestedAt(Date.now()); toast.success("Sincronização solicitada."); await queryClient.invalidateQueries({ queryKey: openFinanceKeys.status }) },
    onError: (error) => toast.error(getApiErrorMessage(error)),
  })

  const data = status.data
  const busy = !!data && busyStates.includes(data.status)
  const syncQueued = syncRequestedAt !== null &&
    (!data?.lastAttemptAtUtc || new Date(data.lastAttemptAtUtc).getTime() < syncRequestedAt)
  const consentNearExpiry = data?.consentExpiresAtUtc && new Date(data.consentExpiresAtUtc).getTime() - renderedAt < 30 * 86_400_000

  return <div>
    <div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end">
      <div><p className="text-sm font-semibold text-brand-green">Configurações</p><h1 className="mt-1 font-heading text-3xl font-semibold">Open Finance</h1><p className="mt-2 text-muted-foreground">Conecte os dados já autorizados no Meu Pluggy ao KiaraFin.</p></div>
      {data?.initialImportCompleted && <Button variant="outline" disabled={busy || syncQueued || sync.isPending || data.status === "ConsentExpired"} onClick={() => sync.mutate()}>{busy || syncQueued || sync.isPending ? <LoaderCircle className="animate-spin" /> : <RefreshCw />}{syncQueued ? "Sincronização solicitada" : "Sincronizar agora"}</Button>}
    </div>

    {status.isPending ? <Card className="mt-7"><CardContent className="flex min-h-48 items-center justify-center gap-2 p-6 text-sm text-muted-foreground"><LoaderCircle className="animate-spin" />Consultando a integração…</CardContent></Card>
      : status.isError ? <Card className="mt-7 border-destructive/40"><CardContent className="flex min-h-48 flex-col items-center justify-center p-6 text-center"><CloudOff className="size-9 text-destructive" /><p className="mt-3 text-sm text-destructive">{getApiErrorMessage(status.error)}</p><Button variant="outline" className="mt-4" onClick={() => status.refetch()}>Tentar novamente</Button></CardContent></Card>
      : data && <>
        {(data.status === "ConsentExpired" || consentNearExpiry) && <div className="mt-7 flex gap-3 rounded-2xl border border-amber-500/40 bg-amber-500/10 p-4 text-sm"><AlertTriangle className="mt-0.5 size-5 shrink-0 text-amber-700 dark:text-amber-300" /><div><p className="font-semibold">{data.status === "ConsentExpired" ? "Consentimento expirado" : "Consentimento próximo do vencimento"}</p><p className="mt-1 text-muted-foreground">{data.consentExpiresAtUtc ? `Data de expiração: ${formatDateInSaoPaulo(data.consentExpiresAtUtc)}. ` : ""}Renove o consentimento pelo Dashboard da Pluggy. A sincronização ficará suspensa quando ele expirar.</p></div></div>}

        <div className="mt-7 grid gap-4 lg:grid-cols-[1.4fr_1fr]">
          <Card><CardHeader><div className="flex items-start justify-between gap-4"><div><CardTitle>Meu Pluggy</CardTitle><CardDescription>Conexão bancária previamente autorizada</CardDescription></div><StatusPill state={data.status} /></div></CardHeader><CardContent>
            {busy && <div className="mb-5 rounded-xl border bg-muted/35 p-4"><div className="flex items-center gap-2 text-sm font-medium"><LoaderCircle className="animate-spin text-brand-green" />{data.status === "InitialSync" ? "Substituindo e importando seus dados…" : "Sincronização em andamento…"}</div><p className="mt-1 text-xs text-muted-foreground">As próximas sincronizações continuam automaticamente em segundo plano.</p></div>}
            <dl className="grid gap-4 sm:grid-cols-2"><Detail label="Contas sincronizadas" value={String(data.accountCount)} /><Detail label="Lançamentos importados" value={String(data.transactionCount)} /><Detail label="Última sincronização" value={data.lastSuccessfulSyncAtUtc ? formatDateTime(data.lastSuccessfulSyncAtUtc) : "Ainda não realizada"} /><Detail label="Consentimento" value={data.consentExpiresAtUtc ? `Válido até ${formatDateInSaoPaulo(data.consentExpiresAtUtc)}` : "Não informado pela instituição"} />{data.nextScheduledSyncAtUtc && <Detail label="Próxima verificação" value={formatDateTime(data.nextScheduledSyncAtUtc)} />}</dl>
            {data.lastError && <p className="mt-5 rounded-xl bg-destructive/10 p-3 text-sm text-destructive">{data.lastError}</p>}
          </CardContent></Card>

          <Card><CardHeader><CardTitle>Instituições conectadas</CardTitle><CardDescription>Dados retornados pelas contas importadas</CardDescription></CardHeader><CardContent>{data.institutions?.length ? <ul className="space-y-3">{data.institutions.map((institution) => <li key={institution} className="flex items-center gap-3 rounded-xl border p-3 text-sm font-medium"><Building2 className="size-4 text-brand-green" />{institution}</li>)}</ul> : <p className="text-sm text-muted-foreground">Nenhuma instituição disponibilizada até o momento.</p>}</CardContent></Card>
        </div>

        {!data.initialImportCompleted && <Card className="mt-4"><CardHeader><CardTitle>Ativar integração</CardTitle><CardDescription>A validação consulta a conexão e prepara uma prévia antes de qualquer alteração.</CardDescription></CardHeader><CardContent><div className="flex items-start gap-3 rounded-xl border bg-muted/30 p-4"><ShieldCheck className="mt-0.5 size-5 shrink-0 text-brand-green" /><p className="text-sm text-muted-foreground">Se a autenticação, o consentimento, a paginação ou os dados estiverem incompletos, seus dados atuais serão preservados.</p></div><Button className="mt-5" disabled={prepare.isPending || busy} onClick={() => prepare.mutate()}>{prepare.isPending ? <LoaderCircle className="animate-spin" /> : <Cloud />}Verificar credenciais e preparar prévia</Button></CardContent></Card>}
      </>}

    <ReplacementConfirmation open={confirmationOpen} onOpenChange={(open) => { if (!activate.isPending) setConfirmationOpen(open) }} preview={preview} acknowledged={acknowledged} onAcknowledged={setAcknowledged} pending={activate.isPending} onConfirm={() => activate.mutate()} />
  </div>
}

function StatusPill({ state }: { state: PluggyIntegrationState }) {
  const healthy = state === "Connected"
  const Icon = healthy ? CheckCircle2 : state === "Error" || state === "ConsentExpired" ? AlertTriangle : Clock3
  return <span className={`inline-flex items-center gap-1.5 rounded-full px-2.5 py-1 text-xs font-semibold ${healthy ? "bg-brand-green/10 text-brand-green" : state === "Error" || state === "ConsentExpired" ? "bg-destructive/10 text-destructive" : "bg-amber-500/10 text-amber-700 dark:text-amber-300"}`}><Icon className="size-3.5" />{stateLabel[state]}</span>
}

function Detail({ label, value }: { label: string; value: string }) { return <div><dt className="text-xs text-muted-foreground">{label}</dt><dd className="mt-1 text-sm font-semibold">{value}</dd></div> }

function ReplacementConfirmation({ open, onOpenChange, preview, acknowledged, onAcknowledged, pending, onConfirm }: { open: boolean; onOpenChange: (open: boolean) => void; preview: PluggyIntegrationPreview | null; acknowledged: boolean; onAcknowledged: (value: boolean) => void; pending: boolean; onConfirm: () => void }) {
  return <AlertDialog.Root open={open} onOpenChange={onOpenChange}><AlertDialog.Portal><AlertDialog.Overlay className="fixed inset-0 z-50 bg-black/55" /><AlertDialog.Content className="fixed left-1/2 top-1/2 z-50 max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] max-w-2xl -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-2xl border bg-card p-6 shadow-2xl"><AlertDialog.Title className="font-heading text-2xl font-semibold">Substituir seus dados financeiros?</AlertDialog.Title><AlertDialog.Description asChild><div className="mt-3 space-y-3 text-sm leading-6 text-muted-foreground"><p>Ao ativar a integração com Meu Pluggy, todas as contas, cartões, faturas e lançamentos financeiros cadastrados anteriormente serão excluídos e substituídos pelos dados recebidos da sua conexão bancária.</p><p>Esta operação é irreversível e não será realizado backup dos dados antigos.</p><p>Depois da integração, suas movimentações serão sincronizadas automaticamente, conforme as atualizações disponibilizadas pelo Meu Pluggy.</p></div></AlertDialog.Description>
    {preview && <div className="mt-5 grid gap-3 sm:grid-cols-2"><PreviewCount icon={WalletCards} label="Contas existentes" value={preview.existingAccounts} /><PreviewCount icon={WalletCards} label="Cartões existentes" value={preview.existingCreditCards} /><PreviewCount icon={Clock3} label="Lançamentos existentes" value={preview.existingTransactions} /><PreviewCount icon={Cloud} label="Contas encontradas na Pluggy" value={preview.pluggyAccounts} /><PreviewCount icon={Cloud} label="Transações disponíveis" value={preview.pluggyTransactions} /></div>}
    <label className="mt-5 flex cursor-pointer items-start gap-3 rounded-xl border border-destructive/30 bg-destructive/5 p-4 text-sm"><input type="checkbox" className="mt-1 size-4 accent-destructive" checked={acknowledged} onChange={(event) => onAcknowledged(event.target.checked)} /><span>Confirmo que li o aviso e autorizo a exclusão irreversível dos meus dados financeiros atuais, sem backup.</span></label>
    <div className="mt-6 flex flex-col-reverse justify-end gap-2 sm:flex-row"><AlertDialog.Cancel asChild><Button variant="outline" disabled={pending}>Cancelar</Button></AlertDialog.Cancel><Button variant="destructive" disabled={!acknowledged || pending} onClick={onConfirm}>{pending && <LoaderCircle className="animate-spin" />}Excluir meus dados e integrar</Button></div>
  </AlertDialog.Content></AlertDialog.Portal></AlertDialog.Root>
}

function PreviewCount({ icon: Icon, label, value }: { icon: typeof WalletCards; label: string; value: number }) { return <div className="flex items-center gap-3 rounded-xl border p-3"><Icon className="size-4 text-muted-foreground" /><div><p className="text-xs text-muted-foreground">{label}</p><p className="font-semibold tabular-nums">{value}</p></div></div> }
