"use client"

import { useQuery } from "@tanstack/react-query"
import { CloudCheck } from "lucide-react"

import { openFinanceKeys, openFinanceService } from "@/features/open-finance/open-finance-service"
import { formatDateTime } from "@/lib/formatters"

export function OpenFinanceIndicator() {
  const status = useQuery({
    queryKey: openFinanceKeys.status,
    queryFn: openFinanceService.status,
    staleTime: 60_000,
    retry: false,
  })

  if (!status.data?.initialImportCompleted) return null

  return (
    <div className="flex items-center gap-2 rounded-full border bg-card px-2.5 py-1.5 text-xs text-muted-foreground sm:px-3">
      <CloudCheck className="size-3.5 text-brand-green" />
      <span className="font-medium text-foreground"><span className="sm:hidden">Open Finance</span><span className="hidden sm:inline">Sincronizado via Open Finance</span></span>
      {status.data.lastSuccessfulSyncAtUtc && <span className="hidden lg:inline">· {formatDateTime(status.data.lastSuccessfulSyncAtUtc)}</span>}
    </div>
  )
}
