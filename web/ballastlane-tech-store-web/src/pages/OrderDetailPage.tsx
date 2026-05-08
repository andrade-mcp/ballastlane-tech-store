import { useState } from "react";
import { Link, useParams } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { store } from "@/lib/api";
import {
  orderStatusFromInt, orderStatusToInt, productCategoryFromInt,
  type OrderDto, type OrderStatus, type PagedResult, type ProductDto,
} from "@/lib/types";
import { formatCurrency, formatDate } from "@/lib/format";
import { OrderStatusBadge } from "@/components/Badges";

interface RawOrder extends Omit<OrderDto, "status"> { status: number }
interface RawProduct extends Omit<ProductDto, "category"> { category: number }

export function OrderDetailPage() {
  const { id = "" } = useParams<{ id: string }>();
  const qc = useQueryClient();
  const [error, setError] = useState<string | null>(null);

  const order = useQuery({
    queryKey: ["order", id],
    queryFn: async () => {
      const { data } = await store.get<RawOrder>(`/api/orders/${id}`);
      return { ...data, status: orderStatusFromInt[data.status] } as OrderDto;
    },
  });

  const products = useQuery({
    queryKey: ["products-for-order"],
    queryFn: async () => {
      const { data } = await store.get<PagedResult<RawProduct>>("/api/products", { params: { take: 200 } });
      return data.items.map<ProductDto>((r) => ({ ...r, category: productCategoryFromInt[r.category] }));
    },
  });

  const invalidateAll = () => {
    qc.invalidateQueries({ queryKey: ["order", id] });
    qc.invalidateQueries({ queryKey: ["orders"] });
    qc.invalidateQueries({ queryKey: ["products"] });
    qc.invalidateQueries({ queryKey: ["products-for-order"] });
    qc.invalidateQueries({ queryKey: ["pipeline"] });
    qc.invalidateQueries({ queryKey: ["low-stock"] });
  };

  const addItem = useMutation({
    mutationFn: (b: { productId: string; quantity: number }) => store.post(`/api/orders/${id}/items`, b),
    onSuccess: () => invalidateAll(),
    onError: (e: Error) => setError(e.message),
  });
  const changeQty = useMutation({
    mutationFn: ({ itemId, quantity }: { itemId: string; quantity: number }) =>
      store.put(`/api/orders/${id}/items/${itemId}`, { quantity }),
    onSuccess: () => invalidateAll(),
  });
  const removeItem = useMutation({
    mutationFn: (itemId: string) => store.delete(`/api/orders/${id}/items/${itemId}`),
    onSuccess: () => invalidateAll(),
  });
  const changeStatus = useMutation({
    mutationFn: (status: OrderStatus) => store.patch(`/api/orders/${id}/status`, { status: orderStatusToInt[status] }),
    onSuccess: () => invalidateAll(),
    onError: (e: Error) => setError(e.message),
  });
  const deleteOrder = useMutation({
    mutationFn: () => store.delete(`/api/orders/${id}`),
    onSuccess: () => { invalidateAll(); window.history.back(); },
    onError: (e: Error) => setError(e.message),
  });

  if (order.isLoading) return <p className="text-muted-foreground">Loading…</p>;
  if (!order.data) return <p>Order not found.</p>;
  const o = order.data;
  const editable = o.status === "Draft";

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <Link to="/orders" className="text-xs text-muted-foreground hover:underline">← Back to orders</Link>
          <h1 className="text-2xl font-semibold">{o.number}</h1>
          <p className="text-sm text-muted-foreground">
            {o.customerCompany} · created {formatDate(o.createdAt)}
          </p>
        </div>
        <div className="flex items-center gap-2">
          <OrderStatusBadge status={o.status} />
          {o.status === "Draft" && (
            <>
              <button className="btn-primary" onClick={() => { setError(null); changeStatus.mutate("Confirmed"); }}>Confirm</button>
              <button className="btn-outline" onClick={() => changeStatus.mutate("Cancelled")}>Cancel</button>
            </>
          )}
          {o.status === "Confirmed" && (
            <>
              <button className="btn-primary" onClick={() => changeStatus.mutate("Fulfilled")}>Fulfill</button>
              <button className="btn-outline" onClick={() => changeStatus.mutate("Cancelled")}>Cancel</button>
            </>
          )}
          {o.status === "Draft" && (
            <button className="btn-ghost text-destructive"
                    onClick={() => { if (confirm("Delete this draft?")) deleteOrder.mutate(); }}>Delete</button>
          )}
        </div>
      </div>

      {error && <div className="card border-destructive p-4 text-sm text-destructive">{error}</div>}

      <div className="card overflow-x-auto">
        <table className="table">
          <thead>
            <tr><th>Product</th><th className="text-right">Qty</th><th className="text-right">Unit</th>
                <th className="text-right">Line total</th>{editable && <th className="text-right">Actions</th>}</tr>
          </thead>
          <tbody>
            {o.items.map((line) => (
              <tr key={line.id}>
                <td>
                  <div className="font-medium">{line.productName}</div>
                  <div className="font-mono text-xs text-muted-foreground">{line.productSku}</div>
                </td>
                <td className="text-right">
                  {editable ? (
                    <input className="input w-20 text-right" type="number" min={1} defaultValue={line.quantity}
                           onBlur={(e) => {
                             const q = Number(e.currentTarget.value);
                             if (q !== line.quantity && q >= 1) changeQty.mutate({ itemId: line.id, quantity: q });
                           }} />
                  ) : line.quantity}
                </td>
                <td className="text-right">{formatCurrency(line.unitPrice)}</td>
                <td className="text-right font-medium">{formatCurrency(line.lineTotal)}</td>
                {editable && (
                  <td className="text-right">
                    <button className="btn-ghost text-destructive" onClick={() => removeItem.mutate(line.id)}>Remove</button>
                  </td>
                )}
              </tr>
            ))}
            {o.items.length === 0 && (
              <tr><td colSpan={editable ? 5 : 4} className="text-muted-foreground">No items yet.</td></tr>
            )}
          </tbody>
          <tfoot>
            <tr>
              <td colSpan={editable ? 4 : 3} className="text-right text-muted-foreground">Subtotal</td>
              <td className="text-right font-medium">{formatCurrency(o.subtotal)}</td>
            </tr>
            <tr>
              <td colSpan={editable ? 4 : 3} className="text-right text-muted-foreground">Tax</td>
              <td className="text-right">{formatCurrency(o.tax)}</td>
            </tr>
            <tr>
              <td colSpan={editable ? 4 : 3} className="text-right font-semibold">Total</td>
              <td className="text-right font-semibold">{formatCurrency(o.total)}</td>
            </tr>
          </tfoot>
        </table>
      </div>

      {editable && (
        <AddItemForm products={products.data ?? []}
                     onAdd={(b) => { setError(null); addItem.mutate(b); }} submitting={addItem.isPending} />
      )}
    </div>
  );
}

function AddItemForm({ products, onAdd, submitting }:
  { products: ProductDto[]; onAdd: (b: { productId: string; quantity: number }) => void; submitting: boolean }) {
  const { register, handleSubmit, reset } = useForm<{ productId: string; quantity: number }>({
    defaultValues: { productId: "", quantity: 1 },
  });
  return (
    <form className="card flex flex-wrap items-end gap-3 p-4"
          onSubmit={handleSubmit((d) => { onAdd(d); reset({ productId: "", quantity: 1 }); })}>
      <div className="grow min-w-[12rem]">
        <label className="label">Add product</label>
        <select className="input" {...register("productId", { required: true })}>
          <option value="">Select…</option>
          {products.map((p) => (
            <option key={p.id} value={p.id}>
              {p.sku} — {p.name} ({p.stockOnHand} in stock · {p.price})
            </option>
          ))}
        </select>
      </div>
      <div className="w-28">
        <label className="label">Qty</label>
        <input className="input" type="number" min={1} {...register("quantity", { valueAsNumber: true, required: true, min: 1 })} />
      </div>
      <button className="btn-primary" disabled={submitting}>{submitting ? "Adding…" : "Add line"}</button>
    </form>
  );
}
