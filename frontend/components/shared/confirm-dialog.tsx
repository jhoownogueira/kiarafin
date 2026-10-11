"use client"

import * as AlertDialog from "@radix-ui/react-alert-dialog"
import { LoaderCircle } from "lucide-react"
import { Button } from "@/components/ui/button"

export function ConfirmDialog({ trigger, title, description, confirmLabel = "Confirmar", destructive = false, pending = false, onConfirm }: { trigger: React.ReactNode; title: string; description: string; confirmLabel?: string; destructive?: boolean; pending?: boolean; onConfirm: () => void }) {
  return (
    <AlertDialog.Root>
      <AlertDialog.Trigger asChild>{trigger}</AlertDialog.Trigger>
      <AlertDialog.Portal>
        <AlertDialog.Overlay className="fixed inset-0 z-50 bg-black/45" />
        <AlertDialog.Content className="fixed left-1/2 top-1/2 z-50 w-[calc(100%-2rem)] max-w-md -translate-x-1/2 -translate-y-1/2 rounded-2xl border bg-card p-6 shadow-2xl">
          <AlertDialog.Title className="font-heading text-xl font-semibold">{title}</AlertDialog.Title>
          <AlertDialog.Description className="mt-2 text-sm leading-6 text-muted-foreground">{description}</AlertDialog.Description>
          <div className="mt-6 flex justify-end gap-2"><AlertDialog.Cancel asChild><Button variant="outline">Voltar</Button></AlertDialog.Cancel><AlertDialog.Action asChild><Button variant={destructive ? "destructive" : "default"} disabled={pending} onClick={onConfirm}>{pending && <LoaderCircle className="animate-spin" />}{confirmLabel}</Button></AlertDialog.Action></div>
        </AlertDialog.Content>
      </AlertDialog.Portal>
    </AlertDialog.Root>
  )
}
