import { api } from "@/lib/api/client"
import { tokenStore } from "@/lib/api/token-store"
import type { AuthResponse, AuthUser, LoginInput, RegisterInput } from "@/features/auth/types"

export const authService = {
  async login(input: LoginInput) { const { data } = await api.post<AuthResponse>("/auth/login", input); tokenStore.set(data.accessToken); return data },
  async register(input: RegisterInput) { const { data } = await api.post<AuthResponse>("/auth/register", input); tokenStore.set(data.accessToken); return data },
  async me() { const { data } = await api.get<AuthUser>("/auth/me"); return data },
  async logout() { try { await api.post("/auth/logout") } finally { tokenStore.clear() } },
}

export const authQueryKey = ["auth", "me"] as const
