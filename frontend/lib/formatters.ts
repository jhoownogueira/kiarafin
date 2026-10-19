import { format, parseISO } from "date-fns"
import { ptBR } from "date-fns/locale"

export const currencyFormatter = new Intl.NumberFormat("pt-BR", { style: "currency", currency: "BRL" })

export function formatCurrency(value: number) { return currencyFormatter.format(value) }
export function formatDate(value: string) { return format(parseISO(value), "dd/MM/yyyy", { locale: ptBR }) }
export function formatDateTime(value: string) {
  return new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeStyle: "short", timeZone: "America/Sao_Paulo" }).format(new Date(value))
}
export function formatDateInSaoPaulo(value: string) {
  return new Intl.DateTimeFormat("pt-BR", { dateStyle: "short", timeZone: "America/Sao_Paulo" }).format(new Date(value))
}
export function todayIso() { return format(new Date(), "yyyy-MM-dd") }
