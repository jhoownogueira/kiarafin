"use client"

import { useQuery } from "@tanstack/react-query"
import { authQueryKey, authService } from "@/features/auth/auth-service"
import { accountService, categoryService, financeKeys } from "@/features/finance/services"
import type { Account, Category, CategoryType } from "@/features/finance/types"
import { householdKeys, householdService } from "@/features/households/household-service"
import type { HouseholdMember } from "@/features/households/types"
import { cn } from "@/lib/utils"
import { useHouseholdSelection } from "@/providers/household-provider"

const selectClass = "h-11 w-full rounded-xl border border-input bg-background px-3 text-sm outline-none focus-visible:border-ring focus-visible:ring-2 focus-visible:ring-ring/30 disabled:opacity-50"

export function useFinanceContext() {
  const session = useQuery({ queryKey: authQueryKey, queryFn: authService.me })
  const { selectedHouseholdId } = useHouseholdSelection()
  const household = session.data?.households.find((item) => item.id === selectedHouseholdId) ?? session.data?.households.at(-1)
  const householdId = household?.id ?? ""
  const accounts = useQuery({ queryKey: financeKeys.accountOptions(householdId), queryFn: () => accountService.list(householdId, 1, true, 100), enabled: !!householdId })
  const categories = useQuery({ queryKey: financeKeys.categoryOptions(householdId), queryFn: () => categoryService.list(householdId, 1, undefined, true, 100), enabled: !!householdId })
  const members = useQuery({ queryKey: householdKeys.members(householdId), queryFn: () => householdService.getMembers(householdId), enabled: !!householdId })
  return { household, householdId, accounts: accounts.data?.items ?? [], categories: categories.data?.items ?? [], members: members.data ?? [], optionsPending: accounts.isPending || categories.isPending || members.isPending }
}

type SelectProps = React.SelectHTMLAttributes<HTMLSelectElement> & { placeholder?: string }

export function AccountSelect({ accounts, placeholder = "Selecione uma conta", className, ...props }: SelectProps & { accounts: Account[] }) {
  return <select className={cn(selectClass, className)} {...props}><option value="">{placeholder}</option>{accounts.map((account) => <option key={account.id} value={account.id}>{account.name}</option>)}</select>
}

export function CategorySelect({ categories, type, placeholder = "Selecione uma categoria", className, ...props }: SelectProps & { categories: Category[]; type?: CategoryType }) {
  return <select className={cn(selectClass, className)} {...props}><option value="">{placeholder}</option>{categories.filter((category) => !type || category.type === type).map((category) => <option key={category.id} value={category.id}>{category.name}</option>)}</select>
}

export function ResponsibleSelect({ members, placeholder = "Selecione o responsável", className, ...props }: SelectProps & { members: HouseholdMember[] }) {
  return <select className={cn(selectClass, className)} {...props}><option value="">{placeholder}</option>{members.map((member) => <option key={member.userId} value={member.userId}>{member.name}</option>)}</select>
}
