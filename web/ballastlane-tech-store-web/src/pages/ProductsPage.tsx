import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { store } from "@/lib/api";
import {
  productCategories, productCategoryFromInt, productCategoryToInt,
  type PagedResult, type ProductCategory, type ProductDto,
} from "@/lib/types";
import { formatCurrency } from "@/lib/format";
import { Modal } from "@/components/Modal";
import { BrandButton } from "@/components/BrandButton";

interface ProductForm {
  sku: string; name: string; category: ProductCategory; brand: string; price: number; stockOnHand: number;
}
interface RawProduct extends Omit<ProductDto, "category"> { category: number }
const mapProduct = (r: RawProduct): ProductDto => ({ ...r, category: productCategoryFromInt[r.category] });

export function ProductsPage() {
  const qc = useQueryClient();
  const [filter, setFilter] = useState<ProductCategory | "All">("All");
  const [lowOnly, setLowOnly] = useState(false);
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<ProductDto | null>(null);

  const list = useQuery({
    queryKey: ["products", filter, lowOnly],
    queryFn: async () => {
      const params: Record<string, unknown> = {};
      if (filter !== "All") params.category = productCategoryToInt[filter];
      if (lowOnly) params.lowStock = true;
      const { data } = await store.get<PagedResult<RawProduct>>("/api/products", { params });
      return { ...data, items: data.items.map(mapProduct) };
    },
  });

  const submitForm = (b: ProductForm) => ({ ...b, category: productCategoryToInt[b.category] });

  const createMut = useMutation({
    mutationFn: (b: ProductForm) => store.post("/api/products", submitForm(b)),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["products"] }); setCreating(false); },
  });
  const updateMut = useMutation({
    mutationFn: ({ id, body }: { id: string; body: ProductForm }) => store.put(`/api/products/${id}`, submitForm(body)),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["products"] }); setEditing(null); },
  });
  const deleteMut = useMutation({
    mutationFn: (id: string) => store.delete(`/api/products/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["products"] }),
  });

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Products</h1>
        <BrandButton onClick={() => setCreating(true)}>New product</BrandButton>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Chip active={filter === "All"} onClick={() => setFilter("All")}>All</Chip>
        {productCategories.map((c) => <Chip key={c} active={filter === c} onClick={() => setFilter(c)}>{c}</Chip>)}
        <Chip active={lowOnly} onClick={() => setLowOnly(!lowOnly)}>Low stock only</Chip>
        <span className="ml-auto text-sm text-muted-foreground">{list.data ? `${list.data.total} total` : ""}</span>
      </div>

      <div className="card overflow-x-auto">
        <table className="table">
          <thead>
            <tr><th>SKU</th><th>Name</th><th>Category</th><th>Brand</th>
                <th className="text-center">Price</th><th className="text-center">Stock</th>
                <th className="text-right">Actions</th></tr>
          </thead>
          <tbody>
            {list.isLoading && <tr><td colSpan={7} className="text-muted-foreground">Loading…</td></tr>}
            {list.data?.items.map((p) => (
              <tr key={p.id}>
                <td className="font-mono text-xs">{p.sku}</td>
                <td className="font-medium">{p.name}</td>
                <td className="text-muted-foreground">{p.category}</td>
                <td>{p.brand}</td>
                <td className="text-center">{formatCurrency(p.price)}</td>
                <td className="text-center">
                  <span className={p.stockOnHand <= 5 ? "badge bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30" : ""}>
                    {p.stockOnHand}
                  </span>
                </td>
                <td className="text-right">
                  <button className="btn-ghost" onClick={() => setEditing(p)}>Edit</button>
                  <button className="btn-ghost text-destructive"
                          onClick={() => { if (confirm(`Delete ${p.sku}?`)) deleteMut.mutate(p.id); }}>Delete</button>
                </td>
              </tr>
            ))}
            {list.data && list.data.items.length === 0 && (
              <tr><td colSpan={7} className="text-muted-foreground">No products match.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      <ProductFormModal open={creating} title="New product"
        defaults={{ sku: "", name: "", category: "Cpu", brand: "", price: 0, stockOnHand: 0 }}
        onClose={() => setCreating(false)} onSubmit={(b) => createMut.mutate(b)} submitting={createMut.isPending} />
      <ProductFormModal open={!!editing} title="Edit product"
        defaults={editing ? { sku: editing.sku, name: editing.name, category: editing.category, brand: editing.brand, price: editing.price, stockOnHand: editing.stockOnHand } : null}
        onClose={() => setEditing(null)} onSubmit={(b) => editing && updateMut.mutate({ id: editing.id, body: b })} submitting={updateMut.isPending} />
    </div>
  );
}

function Chip({ active, onClick, children }: { active: boolean; onClick: () => void; children: React.ReactNode }) {
  return (
    <button onClick={onClick}
            className={`badge cursor-pointer ${active ? "bg-primary text-primary-foreground border-primary" : ""}`}>
      {children}
    </button>
  );
}

function ProductFormModal({ open, title, defaults, onClose, onSubmit, submitting }:
  { open: boolean; title: string; defaults: ProductForm | null; onClose: () => void; onSubmit: (b: ProductForm) => void; submitting: boolean }) {
  const { register, handleSubmit, reset } = useForm<ProductForm>({ values: defaults ?? undefined });
  return (
    <Modal open={open && !!defaults} title={title}
           onClose={() => { reset(); onClose(); }}
           footer={
             <>
               <button className="btn-ghost" onClick={() => { reset(); onClose(); }}>Cancel</button>
               <BrandButton type="button" disabled={submitting} onClick={handleSubmit(onSubmit)}>
                 {submitting ? "Saving…" : "Save"}
               </BrandButton>
             </>
           }>
      <div className="grid grid-cols-2 gap-4">
        <div className="col-span-2"><label className="label">Name</label><input className="input" {...register("name", { required: true })} /></div>
        <div><label className="label">SKU</label><input className="input" {...register("sku", { required: true })} /></div>
        <div><label className="label">Category</label>
          <select className="input" {...register("category", { required: true })}>
            {productCategories.map((c) => <option key={c} value={c}>{c}</option>)}
          </select>
        </div>
        <div><label className="label">Brand</label><input className="input" {...register("brand", { required: true })} /></div>
        <div><label className="label">Price (USD)</label>
          <input className="input" type="number" step="0.01" min="0" {...register("price", { valueAsNumber: true, required: true })} />
        </div>
        <div className="col-span-2"><label className="label">Stock on hand</label>
          <input className="input" type="number" min="0" {...register("stockOnHand", { valueAsNumber: true, required: true })} />
        </div>
      </div>
    </Modal>
  );
}
