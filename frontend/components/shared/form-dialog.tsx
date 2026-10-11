"use client"

import * as Dialog from "@radix-ui/react-dialog"
import { X } from "lucide-react"
import { Button } from "@/components/ui/button"

export function FormDialog({ open, onOpenChange, trigger, title, description, children, maxWidth = "max-w-xl" }: { open: boolean; onOpenChange: (open: boolean) => void; trigger?: React.ReactNode; title: string; description: string; children: React.ReactNode; maxWidth?: string }) {
  return (
    <Dialog.Root open={open} onOpenChange={onOpenChange}>
      {trigger && <Dialog.Trigger asChild>{trigger}</Dialog.Trigger>}
      <Dialog.Portal>
        <Dialog.Overlay className="fixed inset-0 z-40 bg-black/45" />
        <Dialog.Content className={`fixed left-1/2 top-1/2 z-50 max-h-[calc(100dvh-2rem)] w-[calc(100%-2rem)] ${maxWidth} -translate-x-1/2 -translate-y-1/2 overflow-y-auto rounded-2xl border bg-card p-6 shadow-2xl focus:outline-none`}>
          <div className="flex items-start justify-between gap-4"><div><Dialog.Title className="font-heading text-xl font-semibold">{title}</Dialog.Title><Dialog.Description className="mt-1 text-sm text-muted-foreground">{description}</Dialog.Description></div><Dialog.Close asChild><Button variant="ghost" size="icon" aria-label="Fechar"><X /></Button></Dialog.Close></div>
          <div className="mt-6">{children}</div>
        </Dialog.Content>
      </Dialog.Portal>
    </Dialog.Root>
  )
}
