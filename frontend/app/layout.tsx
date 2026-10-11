import type { Metadata, Viewport } from "next"
import { Geist, Geist_Mono } from "next/font/google"
import { AppProviders } from "@/providers/app-providers"
import "./globals.css"

const geistSans = Geist({ variable: "--font-geist-sans", subsets: ["latin"] })
const geistMono = Geist_Mono({ variable: "--font-geist-mono", subsets: ["latin"] })

export const metadata: Metadata = {
  title: { default: "KiaraFin", template: "%s | KiaraFin" },
  description: "Organize as finanças da sua família com clareza e tranquilidade.",
  icons: { icon: "/identidade/favicon.png" },
}
export const viewport: Viewport = { colorScheme: "light dark", themeColor: "#F9F4F1" }

export default function RootLayout({ children }: LayoutProps<"/">) {
  return (
    <html lang="pt-BR" suppressHydrationWarning className={`${geistSans.variable} ${geistMono.variable}`}>
      <body className="min-h-dvh bg-background font-sans text-foreground antialiased"><AppProviders>{children}</AppProviders></body>
    </html>
  )
}
