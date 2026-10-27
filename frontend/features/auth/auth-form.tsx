"use client"

import { useState } from "react"
import { useRouter } from "next/navigation"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation, useQueryClient } from "@tanstack/react-query"
import { Eye, EyeOff, LoaderCircle } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"

import { Button } from "@/components/ui/button"
import { Input } from "@/components/ui/input"
import { Label } from "@/components/ui/label"
import { authQueryKey, authService } from "@/features/auth/auth-service"
import { loginSchema, type LoginFormData } from "@/features/auth/schemas"
import { getApiErrorMessage } from "@/lib/api/errors"

type AuthFormProps = { mode: "login"; returnTo?: string }

function FieldError({ id, message }: { id: string; message?: string }) {
  if (!message) return null
  return <p id={id} role="alert" className="text-sm text-destructive">{message}</p>
}

function safeReturnTo(returnTo?: string) {
  return returnTo?.startsWith("/convite/") && !returnTo.startsWith("//") ? returnTo : "/dashboard"
}

export function AuthForm({ returnTo }: AuthFormProps) {
  return <LoginForm returnTo={returnTo} />
}

function LoginForm({ returnTo }: { returnTo?: string }) {
  const router = useRouter()
  const queryClient = useQueryClient()
  const [showPassword, setShowPassword] = useState(false)
  const form = useForm<LoginFormData>({ resolver: zodResolver(loginSchema), defaultValues: { email: "", password: "" } })
  const mutation = useMutation({
    mutationFn: authService.login,
    onSuccess: ({ user }) => {
      queryClient.setQueryData(authQueryKey, user)
      toast.success("Boas-vindas de volta!")
      router.replace(safeReturnTo(returnTo))
    },
    onError: (error) => toast.error(getApiErrorMessage(error, "E-mail ou senha inválidos.")),
  })

  return (
    <div>
      <h1 className="font-heading text-3xl font-semibold tracking-tight">Entre na sua conta</h1>
      <p className="mt-2 text-muted-foreground">Continue organizando a vida financeira da sua família.</p>
      <form className="mt-8 space-y-5" onSubmit={form.handleSubmit((values) => mutation.mutate(values))} noValidate>
        <div className="space-y-2">
          <Label htmlFor="email">E-mail</Label>
          <Input id="email" type="email" autoComplete="email" placeholder="voce@exemplo.com" aria-invalid={!!form.formState.errors.email} aria-describedby={form.formState.errors.email ? "email-error" : undefined} {...form.register("email")} />
          <FieldError id="email-error" message={form.formState.errors.email?.message} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="password">Senha</Label>
          <div className="relative">
            <Input id="password" type={showPassword ? "text" : "password"} autoComplete="current-password" className="pr-11" aria-invalid={!!form.formState.errors.password} aria-describedby={form.formState.errors.password ? "password-error" : undefined} {...form.register("password")} />
            <button type="button" className="absolute right-1 top-1 inline-flex size-9 items-center justify-center rounded-lg text-muted-foreground hover:bg-muted hover:text-foreground" onClick={() => setShowPassword((value) => !value)} aria-label={showPassword ? "Ocultar senha" : "Mostrar senha"}>{showPassword ? <EyeOff /> : <Eye />}</button>
          </div>
          <FieldError id="password-error" message={form.formState.errors.password?.message} />
        </div>
        <Button type="submit" size="lg" className="w-full" disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Entrar</Button>
      </form>
      <p className="mt-7 text-center text-sm text-muted-foreground">Acesso pessoal restrito a usuários existentes.</p>
    </div>
  )
}
