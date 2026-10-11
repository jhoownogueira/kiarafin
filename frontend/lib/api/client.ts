import axios, { AxiosError, type InternalAxiosRequestConfig } from "axios"
import { tokenStore } from "@/lib/api/token-store"

type RetryableConfig = InternalAxiosRequestConfig & { _retry?: boolean }
type RefreshResponse = { accessToken: string }
const baseURL = process.env.NEXT_PUBLIC_API_BASE_URL ?? "/api"

export const api = axios.create({ baseURL, timeout: 15_000, withCredentials: true, headers: { Accept: "application/json", "Content-Type": "application/json" } })
const refreshClient = axios.create({ baseURL, timeout: 15_000, withCredentials: true })
let refreshPromise: Promise<string> | null = null

async function refreshAccessToken() {
  refreshPromise ??= refreshClient.post<RefreshResponse>("/auth/refresh").then(({ data }) => {
    tokenStore.set(data.accessToken)
    return data.accessToken
  }).catch((error) => {
    tokenStore.clear()
    throw error
  }).finally(() => { refreshPromise = null })
  return refreshPromise
}

api.interceptors.request.use((config) => {
  const token = tokenStore.get()
  if (token) config.headers.Authorization = `Bearer ${token}`
  return config
})

api.interceptors.response.use((response) => response, async (error: AxiosError) => {
  const config = error.config as RetryableConfig | undefined
  const isAuthOperation = config?.url?.includes("/auth/login") || config?.url?.includes("/auth/register") || config?.url?.includes("/auth/refresh")
  if (error.response?.status !== 401 || !config || config._retry || isAuthOperation) throw error
  config._retry = true
  const token = await refreshAccessToken()
  config.headers.Authorization = `Bearer ${token}`
  return api(config)
})
