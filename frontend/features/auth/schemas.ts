import { z } from "zod"

export const loginSchema = z.object({
  email: z.string().trim().min(1, "Informe seu e-mail.").email("Informe um e-mail válido."),
  password: z.string().min(1, "Informe sua senha."),
})

export const registerSchema = z.object({
  name: z.string().trim().min(2, "Informe seu nome.").max(120, "Use no máximo 120 caracteres."),
  email: z.string().trim().min(1, "Informe seu e-mail.").email("Informe um e-mail válido."),
  password: z.string().min(8, "A senha deve ter pelo menos 8 caracteres.").max(128, "Use no máximo 128 caracteres."),
  confirmPassword: z.string().min(1, "Confirme sua senha."),
}).refine((values) => values.password === values.confirmPassword, { path: ["confirmPassword"], message: "As senhas não coincidem." })

export type LoginFormData = z.infer<typeof loginSchema>
export type RegisterFormData = z.infer<typeof registerSchema>
