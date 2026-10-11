import type { LucideIcon } from "lucide-react"

import { Card, CardContent } from "@/components/ui/card"

type ModuleEmptyStateProps = {
  icon: LucideIcon
  title: string
  description: string
}

export function ModuleEmptyState({ icon: Icon, title, description }: ModuleEmptyStateProps) {
  return (
    <Card className="mt-8 border-dashed bg-card/60">
      <CardContent className="flex min-h-72 flex-col items-center justify-center px-6 py-12 text-center">
        <span className="flex size-16 items-center justify-center rounded-2xl bg-accent text-accent-foreground"><Icon className="size-8" /></span>
        <h2 className="mt-5 font-heading text-xl font-semibold">{title}</h2>
        <p className="mt-2 max-w-md text-sm leading-6 text-muted-foreground">{description}</p>
      </CardContent>
    </Card>
  )
}
