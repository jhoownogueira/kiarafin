import { z } from "zod"

export const invitationSchema = z.object({
  email: z.string().trim().email("Informe um e-mail válido.").or(z.literal("")),
})

export type InvitationFormData = z.infer<typeof invitationSchema>
