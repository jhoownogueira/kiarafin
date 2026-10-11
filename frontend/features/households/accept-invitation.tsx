"use client"

import Link from "next/link"
import { useRouter } from "next/navigation"
import axios from "axios"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { CheckCircle2, LoaderCircle, LogIn, UserPlus, Users } from "lucide-react"
import { toast } from "sonner"

import { BrandLogo } from "@/components/brand/brand-logo"
import { ThemeToggle } from "@/components/shared/theme-toggle"
import { Button } from "@/components/ui/button"
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from "@/components/ui/card"
import { Skeleton } from "@/components/ui/skeleton"
import { authQueryKey, authService } from "@/features/auth/auth-service"
import { householdService } from "@/features/households/household-service"
import { getApiErrorMessage } from "@/lib/api/errors"

function normalizeInvitationToken(token: string) {
  let normalized = token.trim()

  for (let attempt = 0; attempt < 3 && normalized.includes("%"); attempt += 1) {
    try {
      const decoded = decodeURIComponent(normalized)
      if (decoded === normalized) break
      normalized = decoded
    } catch {
      break
    }
  }

  return normalized
}

export function AcceptInvitation({ token }: { token: string }) {
  const router = useRouter()
  const queryClient = useQueryClient()
  const normalizedToken = normalizeInvitationToken(token)
  const session = useQuery({ queryKey: authQueryKey, queryFn: authService.me, retry: false })
  const unauthorized = axios.isAxiosError(session.error) && session.error.response?.status === 401
  const returnTo = `/convite/${encodeURIComponent(normalizedToken)}`
  const loginHref = `/login?returnTo=${encodeURIComponent(returnTo)}`
  const registerHref = `/cadastro?returnTo=${encodeURIComponent(returnTo)}`
  const accept = useMutation({
    mutationFn: () => householdService.acceptInvitation(normalizedToken),
    onSuccess: async (household) => {
      await queryClient.invalidateQueries({ queryKey: authQueryKey })
      toast.success(`Agora você faz parte de ${household.name}.`)
      router.replace("/familia")
    },
    onError: (error) => toast.error(getApiErrorMessage(error, "Não foi possível aceitar este convite.")),
  })

  return (
    <main className="min-h-dvh bg-[radial-gradient(circle_at_top_right,var(--accent),transparent_38%)] px-4 py-8 sm:px-6">
      <div className="mx-auto flex max-w-4xl items-center justify-between"><span className="rounded-2xl bg-brand-canvas p-3 shadow-sm"><BrandLogo className="h-11 w-[200px]" priority /></span><ThemeToggle /></div>
      <div className="mx-auto mt-16 max-w-lg sm:mt-24">
        <Card>
          <CardHeader className="text-center"><span className="mx-auto mb-3 flex size-14 items-center justify-center rounded-2xl bg-accent text-accent-foreground"><Users className="size-7" /></span><CardTitle className="text-2xl">Convite para compartilhar finanças</CardTitle><CardDescription>Entre no núcleo financeiro de alguém próximo e acompanhem tudo no mesmo espaço.</CardDescription></CardHeader>
          <CardContent>
            {normalizedToken.length < 40 ? (
              <div className="rounded-xl bg-destructive/10 p-4 text-center text-sm text-destructive">Este link de convite é inválido.</div>
            ) : session.isPending ? (
              <div className="space-y-3"><Skeleton className="h-12 w-full" /><Skeleton className="mx-auto h-4 w-3/4" /></div>
            ) : unauthorized ? (
              <div>
                <p className="text-center text-sm leading-6 text-muted-foreground">Para proteger os dados da família, entre na sua conta ou crie uma antes de aceitar.</p>
                <div className="mt-6 grid gap-3 sm:grid-cols-2"><Button asChild size="lg"><Link href={loginHref}><LogIn />Entrar</Link></Button><Button asChild size="lg" variant="outline"><Link href={registerHref}><UserPlus />Criar conta</Link></Button></div>
              </div>
            ) : session.isError ? (
              <div className="text-center"><p className="text-sm text-destructive">Não foi possível verificar sua sessão.</p><Button variant="outline" className="mt-4" onClick={() => session.refetch()}>Tentar novamente</Button></div>
            ) : (
              <div>
                <div className="rounded-xl bg-muted p-4 text-sm"><p className="font-medium">Você está conectado como</p><p className="mt-1 text-muted-foreground">{session.data.email}</p></div>
                <p className="mt-5 text-center text-sm leading-6 text-muted-foreground">Ao aceitar, este núcleo será adicionado à sua conta. Seus outros núcleos continuarão disponíveis.</p>
                <Button size="lg" className="mt-6 w-full" disabled={accept.isPending} onClick={() => accept.mutate()}>{accept.isPending ? <LoaderCircle className="animate-spin" /> : <CheckCircle2 />}Aceitar convite</Button>
              </div>
            )}
          </CardContent>
        </Card>
      </div>
    </main>
  )
}
