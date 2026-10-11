"use client"

import { createContext, useContext, useMemo, useSyncExternalStore, type ReactNode } from "react"

const storageKey = "kiarafin:selected-household"
type HouseholdSelection = { selectedHouseholdId: string | null; selectHousehold: (id: string) => void }
const HouseholdContext = createContext<HouseholdSelection | null>(null)

export function HouseholdProvider({ children }: { children: ReactNode }) {
  const selectedHouseholdId = useSyncExternalStore(
    (notify) => {
      window.addEventListener("storage", notify)
      window.addEventListener("kiarafin-household-change", notify)
      return () => { window.removeEventListener("storage", notify); window.removeEventListener("kiarafin-household-change", notify) }
    },
    () => window.localStorage.getItem(storageKey),
    () => null,
  )
  const value = useMemo<HouseholdSelection>(() => ({
    selectedHouseholdId,
    selectHousehold: (id) => { window.localStorage.setItem(storageKey, id); window.dispatchEvent(new Event("kiarafin-household-change")) },
  }), [selectedHouseholdId])
  return <HouseholdContext.Provider value={value}>{children}</HouseholdContext.Provider>
}

export function useHouseholdSelection() {
  const context = useContext(HouseholdContext)
  if (!context) throw new Error("useHouseholdSelection deve ser usado dentro de HouseholdProvider.")
  return context
}
