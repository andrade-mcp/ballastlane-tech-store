import { useState } from "react";
import { Link } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { store } from "@/lib/api";
import {
  customerStatusFromInt, orderStatusFromInt, orderStatusToInt, orderStatuses,
  type CustomerDto, type OrderStatus, type OrderSummaryDto, type PagedResult,
} from "@/lib/types";
import { formatCurrency, formatDate } from "@/lib/format";
import { OrderStatusBadge } from "@/components/Badges";
import { Modal } from "@/components/Modal";
import { BrandButton } from "@/components/BrandButton";

interface RawSummary extends Omit<OrderSummaryDto, "status"> { status: number }
interface RawCustomer extends Omit<CustomerDto, "status"> { status: number }
const mapSummary = (r: RawSummary): OrderSummaryDto => ({ ...r, status: orderStatusFromInt[r.status] });

export function OrdersPage() {
  const qc = useQueryClient();
  const [filter, setFilter] = useState<OrderStatus | "All">("All");
  const [creating, setCreating] = useState(false);

  const list = useQuery({
    queryKey: ["orders", filter],
    queryFn: async () => {
      const params = filter === "All" ? {} : { status: orderStatusToInt[filter] };
      const { data } = await store.get<PagedResult<RawSummary>>("/api/orders", { params });
      return { ...data, items: data.items.map(mapSummary) };
    },
  });

  const customers = useQuery({
    queryKey: ["customers-for-orders"],
    queryFn: async () => {
      const { data } = await store.get<PagedResult<RawCustomer>>("/api/customers", { params: { take: 200 } });
      return data.items.map<CustomerDto>((r) => ({ ...r, status: customerStatusFromInt[r.status] }));
    },
  });

  const createMut = useMutation({
    mutationFn: (customerId: string) => store.post("/api/orders", { customerId }),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["orders"] }); setCreating(false); },
  });

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-semibold">Orders</h1>
        <BrandButton onClick={() => setCreating(true)}>New order</BrandButton>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Chip active={filter === "All"} onClick={() => setFilter("All")}>All</Chip>
        {orderStatuses.map((s) => <Chip key={s} active={filter === s} onClick={() => setFilter(s)}>{s}</Chip>)}
        <span className="ml-auto text-sm text-muted-foreground">{list.data ? `${list.data.total} total` : ""}</span>
      </div>

      <div className="card overflow-x-auto">
        <table className="table">
          <thead>
            <tr><th>Number</th><th>Customer</th><th>Status</th>
                <th className="text-right">Total</th><th>Created</th><th /></tr>
          </thead>
          <tbody>
            {list.isLoading && <tr><td colSpan={6} className="text-muted-foreground">Loading…</td></tr>}
            {list.data?.items.map((o) => (
              <tr key={o.id}>
                <td className="font-mono text-xs">{o.number}</td>
                <td>{o.customerCompany}</td>
                <td><OrderStatusBadge status={o.status} /></td>
                <td className="text-right">{formatCurrency(o.total)}</td>
                <td className="text-muted-foreground">{formatDate(o.createdAt)}</td>
                <td className="text-right">
                  <Link className="btn-ghost" to={`/orders/${o.id}`}>Open</Link>
                </td>
              </tr>
            ))}
            {list.data && list.data.items.length === 0 && (
              <tr><td colSpan={6} className="text-muted-foreground">No orders in this status.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      <NewOrderModal open={creating} customers={customers.data ?? []}
                     onClose={() => setCreating(false)}
                     onSubmit={(id) => createMut.mutate(id)} submitting={createMut.isPending} />
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

function NewOrderModal({ open, customers, onClose, onSubmit, submitting }:
  { open: boolean; customers: CustomerDto[]; onClose: () => void; onSubmit: (customerId: string) => void; submitting: boolean }) {
  const { register, handleSubmit, reset } = useForm<{ customerId: string }>();
  return (
    <Modal open={open} title="New draft order"
           onClose={() => { reset(); onClose(); }}
           footer={
             <>
               <button className="btn-ghost" onClick={() => { reset(); onClose(); }}>Cancel</button>
               <BrandButton type="button" disabled={submitting}
                       onClick={handleSubmit((d) => onSubmit(d.customerId))}>
                 {submitting ? "Creating…" : "Create"}
               </BrandButton>
             </>
           }>
      <div>
        <label className="label">Customer</label>
        <select className="input" {...register("customerId", { required: true })}>
          <option value="">Select…</option>
          {customers.map((c) => <option key={c.id} value={c.id}>{c.company}</option>)}
        </select>
      </div>
    </Modal>
  );
}
