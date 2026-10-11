import Image from "next/image"
import { cn } from "@/lib/utils"

type BrandLogoProps = { variant?: "horizontal" | "icon"; className?: string; priority?: boolean }

export function BrandLogo({ variant = "horizontal", className, priority = false }: BrandLogoProps) {
  const isIcon = variant === "icon"
  return (
    <span className={cn("inline-flex shrink-0 items-center justify-center", className)}>
      <Image src={isIcon ? "/identidade/logo-icon.png" : "/identidade/logo.png"} alt="KiaraFin" width={isIcon ? 630 : 1779} height={548} priority={priority} className="h-full w-full object-contain" />
    </span>
  )
}
