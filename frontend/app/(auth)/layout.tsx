import type { ReactNode } from "react"
import { BrandLogo } from "@/components/brand/brand-logo"

export default function AuthLayout({ children }: { children: ReactNode }) {
  return (
    <main className="grid min-h-dvh lg:grid-cols-[minmax(360px,0.9fr)_1.1fr]">
      <section className="relative hidden overflow-hidden bg-brand-ink p-12 text-brand-canvas lg:flex lg:flex-col lg:justify-between">
        <div className="absolute -right-24 -top-20 size-80 rounded-full bg-brand-green/25 blur-3xl" />
        <div className="relative w-fit rounded-2xl bg-brand-canvas p-4 shadow-2xl"><BrandLogo className="h-16 w-[280px]" priority /></div>
        <div className="relative max-w-lg">
          <p className="mb-5 text-sm font-semibold uppercase tracking-[0.22em] text-emerald-300">Finanças em harmonia</p>
          <h1 className="font-heading text-5xl font-semibold leading-tight">Clareza para cuidar do que vocês constroem juntos.</h1>
          <p className="mt-5 max-w-md text-lg leading-8 text-emerald-50/75">Organize contas, compromissos e planos da família em um só lugar.</p>
        </div>
      </section>
      <section className="flex min-h-dvh items-center justify-center px-5 py-10 sm:px-8">
        <div className="w-full max-w-md">
          <div className="mb-10 flex justify-center lg:hidden"><span className="rounded-2xl bg-brand-canvas p-3"><BrandLogo className="h-12 w-[220px]" priority /></span></div>
          {children}
        </div>
      </section>
    </main>
  )
}
