"use client"

import { useQuery } from "@tanstack/react-query"
import { authQueryKey, authService } from "@/features/auth/auth-service"
import { useHouseholdSelection } from "@/providers/household-provider"

/** Núcleo financeiro do usuário logado (uso pessoal: sempre um). */
export function useFinanceContext() {
  const session = useQuery({ queryKey: authQueryKey, queryFn: authService.me })
  const { selectedHouseholdId } = useHouseholdSelection()
  const households = session.data?.households ?? []
  const household = households.find((item) => item.id === selectedHouseholdId) ?? households.find((item) => item.role === "Owner") ?? households.at(0)
  return { household, householdId: household?.id ?? "" }
}
