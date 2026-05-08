import { useQuery } from "@tanstack/react-query";
import { store } from "@/lib/api";
import {
  customerStatusToInt,
  orderStatusFromInt,
  type CustomerDto, type CustomerStatus, type PagedResult,
  type PipelineSummary, type ProductDto,
} from "@/lib/types";
import { formatCurrency } from "@/lib/format";
import { OrderStatusBadge } from "@/components/Badges";

interface RawSummary { status: number; count: number; total: number }

export function DashboardPage() {
  const pipeline = useQuery({
    queryKey: ["pipeline"],
    queryFn: async () => {
      const { data } = await store.get<RawSummary[]>("/api/orders/pipeline");
      return data.map<PipelineSummary>((d) => ({
        status: orderStatusFromInt[d.status], count: d.count, total: d.total,
      }));
    },
  });

  const counts = useQuery({
    queryKey: ["customer-counts"],
    queryFn: async () => {
      const out: Record<CustomerStatus, number> = { Lead: 0, Prospect: 0, Active: 0, Churned: 0 };
      for (const s of Object.keys(out) as CustomerStatus[]) {
        const { data } = await store.get<PagedResult<CustomerDto>>("/api/customers", {
          params: { status: customerStatusToInt[s], take: 1 },
        });
        out[s] = data.total;
      }
      return out;
    },
  });

  const lowStock = useQuery({
    queryKey: ["low-stock"],
    queryFn: async () => {
      const { data } = await store.get<PagedResult<ProductDto>>("/api/products", {
        params: { lowStock: true, take: 50 },
      });
      return data;
    },
  });

  const open = pipeline.data?.filter((s) => s.status === "Draft" || s.status === "Confirmed") ?? [];
  const openValue = open.reduce((acc, s) => acc + s.total, 0);
  const openCount = open.reduce((acc, s) => acc + s.count, 0);
  const fulfilled = pipeline.data?.find((s) => s.status === "Fulfilled");

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold">Dashboard</h1>
        <p className="text-sm text-muted-foreground">A snapshot of orders, customers and inventory.</p>
      </div>

      <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
        <Kpi label="Open orders" value={String(openCount)} />
        <Kpi label="Open pipeline" value={formatCurrency(openValue)} />
        <Kpi label="Fulfilled (cumulative)" value={formatCurrency(fulfilled?.total ?? 0)} />
        <Kpi label="Active customers" value={String(counts.data?.Active ?? "—")} />
      </div>

      <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
        <div className="card p-5">
          <h2 className="mb-4 font-semibold">Order pipeline</h2>
          {pipeline.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
          {pipeline.data && (
            <div className="space-y-2">
              {pipeline.data.map((s) => (
                <div key={s.status} className="flex items-center justify-between rounded-md border px-3 py-2">
                  <div className="flex items-center gap-3">
                    <OrderStatusBadge status={s.status} />
                    <span className="text-sm text-muted-foreground">{s.count} order{s.count === 1 ? "" : "s"}</span>
                  </div>
                  <span className="text-sm font-medium">{formatCurrency(s.total)}</span>
                </div>
              ))}
            </div>
          )}
        </div>

        <div className="card p-5">
          <h2 className="mb-4 font-semibold">Low stock</h2>
          {lowStock.isLoading && <p className="text-sm text-muted-foreground">Loading…</p>}
          {lowStock.data && lowStock.data.items.length === 0 && (
            <p className="text-sm text-muted-foreground">All products comfortably stocked.</p>
          )}
          {lowStock.data && lowStock.data.items.length > 0 && (
            <ul className="space-y-2">
              {lowStock.data.items.map((p) => (
                <li key={p.id} className="flex items-center justify-between rounded-md border px-3 py-2">
                  <div>
                    <div className="font-medium">{p.name}</div>
                    <div className="text-xs text-muted-foreground">{p.sku} · {p.brand}</div>
                  </div>
                  <span className="badge bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30">
                    {p.stockOnHand} on hand
                  </span>
                </li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

function Kpi({ label, value }: { label: string; value: string }) {
  return (
    <div className="card p-5">
      <div className="text-xs uppercase tracking-wide text-muted-foreground">{label}</div>
      <div className="mt-1 text-2xl font-semibold">{value}</div>
    </div>
  );
}
