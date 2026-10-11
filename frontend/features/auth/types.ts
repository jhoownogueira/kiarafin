export type HouseholdSummary = { id: string; name: string; role: string }
export type AuthUser = { id: string; name: string; email: string; households: HouseholdSummary[] }
export type AuthResponse = { accessToken: string; user: AuthUser }
export type LoginInput = { email: string; password: string }
export type RegisterInput = { name: string; email: string; password: string; confirmPassword: string }
