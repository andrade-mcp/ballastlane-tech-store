import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useForm } from "react-hook-form";
import { store } from "@/lib/api";
import {
  customerStatusFromInt, customerStatusToInt, customerStatuses,
  type CustomerDto, type CustomerStatus, type PagedResult,
} from "@/lib/types";
import { formatDate } from "@/lib/format";
import { CustomerStatusBadge } from "@/components/Badges";
import { Modal } from "@/components/Modal";
import { StatusPicker } from "@/components/StatusPicker";

// Forward-only lifecycle: anything → Churned, otherwise only later statuses.
function nextStatusesFor(current: CustomerStatus): CustomerStatus[] {
  if (current === "Churned") return [];
  return customerStatuses.filter((s) => s !== current && (s === "Churned" || customerStatuses.indexOf(s) > customerStatuses.indexOf(current)));
}

interface CustomerForm { company: string; contactName: string; email: string; phone: string; }
interface RawCustomer extends Omit<CustomerDto, "status"> { status: number }
const mapCustomer = (r: RawCustomer): CustomerDto => ({ ...r, status: customerStatusFromInt[r.status] });

export function CustomersPage() {
  const qc = useQueryClient();
  const [filter, setFilter] = useState<CustomerStatus | "All">("All");
  const [creating, setCreating] = useState(false);
  const [editing, setEditing] = useState<CustomerDto | null>(null);

  const list = useQuery({
    queryKey: ["customers", filter],
    queryFn: async () => {
      const params = filter === "All" ? {} : { status: customerStatusToInt[filter] };
      const { data } = await store.get<PagedResult<RawCustomer>>("/api/customers", { params });
      return { ...data, items: data.items.map(mapCustomer) };
    },
  });

  const createMut = useMutation({
    mutationFn: (body: CustomerForm) => store.post("/api/customers", body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["customers"] }); setCreating(false); },
  });
  const updateMut = useMutation({
    mutationFn: ({ id, body }: { id: string; body: CustomerForm }) => store.put(`/api/customers/${id}`, body),
    onSuccess: () => { qc.invalidateQueries({ queryKey: ["customers"] }); setEditing(null); },
  });
  const promoteMut = useMutation({
    mutationFn: ({ id, status }: { id: string; status: CustomerStatus }) =>
      store.patch(`/api/customers/${id}/status`, { status: customerStatusToInt[status] }),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["customers"] }),
  });
  const deleteMut = useMutation({
    mutationFn: (id: string) => store.delete(`/api/customers/${id}`),
    onSuccess: () => qc.invalidateQueries({ queryKey: ["customers"] }),
  });

  return (
    <div className="space-y-5">
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-semibold">Customers</h1>
          <p className="text-sm text-muted-foreground">Leads, prospects, and active accounts.</p>
        </div>
        <button className="btn-primary" onClick={() => setCreating(true)}>New customer</button>
      </div>

      <div className="flex flex-wrap items-center gap-2">
        <Chip active={filter === "All"} onClick={() => setFilter("All")}>All</Chip>
        {customerStatuses.map((s) => <Chip key={s} active={filter === s} onClick={() => setFilter(s)}>{s}</Chip>)}
        <span className="ml-auto text-sm text-muted-foreground">{list.data ? `${list.data.total} total` : ""}</span>
      </div>

      <div className="card overflow-x-auto">
        <table className="table">
          <thead>
            <tr><th>Company</th><th>Contact</th><th>Email</th><th>Status</th><th>Updated</th><th className="text-right">Actions</th></tr>
          </thead>
          <tbody>
            {list.isLoading && <tr><td colSpan={6} className="text-muted-foreground">Loading…</td></tr>}
            {list.data?.items.map((c) => (
              <tr key={c.id}>
                <td className="font-medium">{c.company}</td>
                <td>{c.contactName}</td>
                <td className="text-muted-foreground">{c.email}</td>
                <td>
                  <StatusPicker
                    current={c.status}
                    options={nextStatusesFor(c.status)}
                    onPick={(s) => promoteMut.mutate({ id: c.id, status: s })}
                    trigger={<CustomerStatusBadge status={c.status} />}
                  />
                </td>
                <td className="text-muted-foreground">{formatDate(c.updatedAt)}</td>
                <td className="text-right">
                  <button className="btn-ghost" onClick={() => setEditing(c)}>Edit</button>
                  <button className="btn-ghost text-destructive"
                          onClick={() => { if (confirm(`Delete ${c.company}?`)) deleteMut.mutate(c.id); }}>
                    Delete
                  </button>
                </td>
              </tr>
            ))}
            {list.data && list.data.items.length === 0 && (
              <tr><td colSpan={6} className="text-muted-foreground">No customers match this filter.</td></tr>
            )}
          </tbody>
        </table>
      </div>

      <CustomerFormModal open={creating} title="New customer"
        defaults={{ company: "", contactName: "", email: "", phone: "" }}
        onClose={() => setCreating(false)} onSubmit={(b) => createMut.mutate(b)} submitting={createMut.isPending} />
      <CustomerFormModal open={!!editing} title="Edit customer"
        defaults={editing ? { company: editing.company, contactName: editing.contactName, email: editing.email, phone: editing.phone ?? "" } : null}
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

function CustomerFormModal({ open, title, defaults, onClose, onSubmit, submitting }:
  { open: boolean; title: string; defaults: CustomerForm | null; onClose: () => void; onSubmit: (b: CustomerForm) => void; submitting: boolean }) {
  const { register, handleSubmit, reset } = useForm<CustomerForm>({ values: defaults ?? undefined });
  return (
    <Modal open={open && !!defaults} title={title}
           onClose={() => { reset(); onClose(); }}
           footer={
             <>
               <button className="btn-ghost" onClick={() => { reset(); onClose(); }}>Cancel</button>
               <button className="btn-primary" disabled={submitting} onClick={handleSubmit(onSubmit)}>
                 {submitting ? "Saving…" : "Save"}
               </button>
             </>
           }>
      <div><label className="label">Company</label><input className="input" {...register("company", { required: true })} /></div>
      <div><label className="label">Contact name</label><input className="input" {...register("contactName", { required: true })} /></div>
      <div><label className="label">Email</label><input className="input" type="email" {...register("email", { required: true })} /></div>
      <div><label className="label">Phone</label><input className="input" {...register("phone")} /></div>
    </Modal>
  );
}
