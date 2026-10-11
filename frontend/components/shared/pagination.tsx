"use client"

import { ChevronLeft, ChevronRight } from "lucide-react"
import { Button } from "@/components/ui/button"

export function Pagination({ page, totalPages, totalCount, onPageChange }: { page: number; totalPages: number; totalCount: number; onPageChange: (page: number) => void }) {
  if (totalPages <= 1) return totalCount ? <p className="mt-4 text-xs text-muted-foreground">{totalCount} item(ns)</p> : null
  return <div className="mt-6 flex items-center justify-between gap-3"><p className="text-sm text-muted-foreground">Página {page} de {totalPages} · {totalCount} itens</p><div className="flex gap-2"><Button variant="outline" size="icon" disabled={page <= 1} onClick={() => onPageChange(page - 1)} aria-label="Página anterior"><ChevronLeft /></Button><Button variant="outline" size="icon" disabled={page >= totalPages} onClick={() => onPageChange(page + 1)} aria-label="Próxima página"><ChevronRight /></Button></div></div>
}
