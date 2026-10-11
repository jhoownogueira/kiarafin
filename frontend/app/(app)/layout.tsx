import type { ReactNode } from "react"
import { AuthBoundary } from "@/features/auth/auth-boundary"

// A sessão vive no navegador (access token em memória + refresh HttpOnly), então
// este segmento não pode participar da validação de navegação instantânea.
export const instant = false

export default function AuthenticatedLayout({ children }: { children: ReactNode }) {
  return <AuthBoundary>{children}</AuthBoundary>
}
