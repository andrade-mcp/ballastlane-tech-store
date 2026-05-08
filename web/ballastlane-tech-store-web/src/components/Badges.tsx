import clsx from "clsx";
import type { CustomerStatus, OrderStatus } from "@/lib/types";

const customerColors: Record<CustomerStatus, string> = {
  Lead:     "bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30",
  Prospect: "bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-500/30",
  Active:   "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",
  Churned:  "bg-zinc-500/10 text-zinc-600 dark:text-zinc-400 border-zinc-500/30",
};

const orderColors: Record<OrderStatus, string> = {
  Draft:     "bg-zinc-500/10 text-zinc-700 dark:text-zinc-300 border-zinc-500/30",
  Confirmed: "bg-blue-500/10 text-blue-700 dark:text-blue-300 border-blue-500/30",
  Fulfilled: "bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30",
  Cancelled: "bg-rose-500/10 text-rose-700 dark:text-rose-300 border-rose-500/30",
};

export const CustomerStatusBadge = ({ status }: { status: CustomerStatus }) =>
  <span className={clsx("badge", customerColors[status])}>{status}</span>;

export const OrderStatusBadge = ({ status }: { status: OrderStatus }) =>
  <span className={clsx("badge", orderColors[status])}>{status}</span>;
