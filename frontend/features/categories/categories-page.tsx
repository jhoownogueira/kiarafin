"use client"

import { useState } from "react"
import { zodResolver } from "@hookform/resolvers/zod"
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query"
import { Edit3, LoaderCircle, Plus, Power, PowerOff, Tags } from "lucide-react"
import { useForm } from "react-hook-form"
import { toast } from "sonner"
import { z } from "zod"
import { ConfirmDialog } from "@/components/shared/confirm-dialog"
import { FormDialog } from "@/components/shared/form-dialog"
import { Pagination } from "@/components/shared/pagination"
import { Button } from "@/components/ui/button"
import { Card, CardContent } from "@/components/ui/card"
import { Input } from "@/components/ui/input"
import { Skeleton } from "@/components/ui/skeleton"
import { categoryService, financeKeys } from "@/features/finance/services"
import { useFinanceContext } from "@/features/finance/selectors"
import type { Category, CategoryInput, CategoryType } from "@/features/finance/types"
import { getApiErrorMessage } from "@/lib/api/errors"

const schema = z.object({ name: z.string().trim().min(2, "Informe ao menos 2 caracteres."), type: z.enum(["Income", "Expense"]), color: z.string().min(4), icon: z.string().trim().min(1, "Informe um ícone.") })
type FormData = z.infer<typeof schema>

export function CategoriesPage() {
  const { householdId } = useFinanceContext()
  const [page, setPage] = useState(1), [type, setType] = useState<CategoryType | "">(""), [active, setActive] = useState<boolean | undefined>(true)
  const [editing, setEditing] = useState<Category | null>(null), [open, setOpen] = useState(false)
  const queryClient = useQueryClient()
  const categories = useQuery({ queryKey: financeKeys.categories(householdId, page, type, active), queryFn: () => categoryService.list(householdId, page, type, active), enabled: !!householdId })
  const toggle = useMutation({ mutationFn: ({ category, value }: { category: Category; value: boolean }) => categoryService.setActive(householdId, category.id, value), onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["categories", householdId] }); toast.success("Status da categoria atualizado.") }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  return <div><div className="flex flex-col justify-between gap-4 sm:flex-row sm:items-end"><div><p className="text-sm font-semibold text-brand-green">Organização</p><h1 className="mt-1 font-heading text-3xl font-semibold">Categorias</h1><p className="mt-2 text-muted-foreground">Personalize como receitas e despesas são organizadas.</p></div><Button onClick={() => { setEditing(null); setOpen(true) }}><Plus />Nova categoria</Button></div>
    <div className="mt-6 flex flex-wrap gap-2"><select aria-label="Tipo" className="h-10 rounded-xl border bg-background px-3 text-sm" value={type} onChange={(e) => { setPage(1); setType(e.target.value as CategoryType | "") }}><option value="">Todos os tipos</option><option value="Income">Receitas</option><option value="Expense">Despesas</option></select><select aria-label="Status" className="h-10 rounded-xl border bg-background px-3 text-sm" value={active === undefined ? "all" : String(active)} onChange={(e) => { setPage(1); setActive(e.target.value === "all" ? undefined : e.target.value === "true") }}><option value="true">Ativas</option><option value="false">Inativas</option><option value="all">Todas</option></select></div>
    {categories.isPending ? <div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{Array.from({ length: 6 }).map((_, i) => <Skeleton key={i} className="h-24" />)}</div> : categories.isError ? <p className="mt-6 rounded-xl border p-5 text-destructive">{getApiErrorMessage(categories.error)}</p> : categories.data.items.length === 0 ? <Card className="mt-6 border-dashed"><CardContent className="flex min-h-64 flex-col items-center justify-center"><Tags className="size-10 text-muted-foreground" /><p className="mt-4 font-semibold">Nenhuma categoria encontrada</p></CardContent></Card> : <><div className="mt-6 grid gap-3 sm:grid-cols-2 lg:grid-cols-3">{categories.data.items.map((category) => <Card key={category.id} className={!category.isActive ? "opacity-60" : ""}><CardContent className="flex items-center gap-3 p-4"><span className="size-4 rounded-full" style={{ backgroundColor: category.color }} /><div className="min-w-0 flex-1"><p className="truncate font-semibold">{category.name}</p><p className="text-xs text-muted-foreground">{category.type === "Income" ? "Receita" : "Despesa"}{category.isSystem ? " · Padrão" : ""}</p></div><Button variant="ghost" size="icon" onClick={() => { setEditing(category); setOpen(true) }} aria-label={`Editar ${category.name}`}><Edit3 /></Button><ConfirmDialog trigger={<Button variant="ghost" size="icon" aria-label={category.isActive ? "Desativar" : "Ativar"}>{category.isActive ? <PowerOff /> : <Power />}</Button>} title={`${category.isActive ? "Desativar" : "Ativar"} categoria?`} description="Lançamentos históricos continuarão vinculados." confirmLabel={category.isActive ? "Desativar" : "Ativar"} destructive={category.isActive} pending={toggle.isPending} onConfirm={() => toggle.mutate({ category, value: !category.isActive })} /></CardContent></Card>)}</div><Pagination page={categories.data.page} totalPages={categories.data.totalPages} totalCount={categories.data.totalCount} onPageChange={setPage} /></>}
    <CategoryForm open={open} onOpenChange={setOpen} householdId={householdId} category={editing} />
  </div>
}

function CategoryForm({ open, onOpenChange, householdId, category }: { open: boolean; onOpenChange: (v: boolean) => void; householdId: string; category: Category | null }) {
  const queryClient = useQueryClient()
  const form = useForm<FormData>({ resolver: zodResolver(schema), values: category ? { name: category.name, type: category.type, color: category.color, icon: category.icon } : { name: "", type: "Expense", color: "#028A61", icon: "Tag" } })
  const mutation = useMutation({ mutationFn: (values: FormData) => category ? categoryService.update(householdId, category.id, values as CategoryInput) : categoryService.create(householdId, values as CategoryInput), onSuccess: () => { queryClient.invalidateQueries({ queryKey: ["categories", householdId] }); toast.success(category ? "Categoria atualizada." : "Categoria criada."); onOpenChange(false) }, onError: (error) => toast.error(getApiErrorMessage(error)) })
  return <FormDialog open={open} onOpenChange={onOpenChange} title={category ? "Editar categoria" : "Nova categoria"} description="A categoria ficará disponível nos lançamentos."><form className="space-y-4" onSubmit={form.handleSubmit((v) => mutation.mutate(v))}><Field label="Nome" error={form.formState.errors.name?.message}><Input {...form.register("name")} /></Field><Field label="Tipo"><select disabled={!!category} className="h-11 w-full rounded-xl border bg-background px-3 text-sm" {...form.register("type")}><option value="Expense">Despesa</option><option value="Income">Receita</option></select></Field><div className="grid grid-cols-2 gap-4"><Field label="Cor"><Input type="color" className="p-1" {...form.register("color")} /></Field><Field label="Ícone"><Input placeholder="Tag" {...form.register("icon")} /></Field></div><div className="flex justify-end"><Button type="submit" disabled={mutation.isPending}>{mutation.isPending && <LoaderCircle className="animate-spin" />}Salvar</Button></div></form></FormDialog>
}
function Field({ label, error, children }: { label: string; error?: string; children: React.ReactNode }) { return <label className="block space-y-2 text-sm font-medium"><span>{label}</span>{children}{error && <span className="block text-xs text-destructive">{error}</span>}</label> }
