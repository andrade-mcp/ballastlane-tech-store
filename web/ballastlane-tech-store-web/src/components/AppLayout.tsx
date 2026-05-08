import { NavLink, Outlet } from "react-router-dom";
import clsx from "clsx";
import { UserMenu } from "@/components/UserMenu";

const nav = [
  { to: "/", label: "Dashboard", end: true },
  { to: "/customers", label: "Customers" },
  { to: "/products", label: "Products" },
  { to: "/orders", label: "Orders" },
];

export function AppLayout() {
  return (
    <div className="grid min-h-screen grid-cols-1 md:grid-cols-[240px_1fr]">
      <aside className="border-b border-sidebar-border bg-sidebar text-sidebar-foreground p-5 md:border-b-0 md:border-r">
        <div className="mb-8 flex items-center gap-3">
          <div className="grid h-9 w-9 place-items-center rounded-md bg-primary font-bold text-primary-foreground">B</div>
          <div className="leading-tight">
            <div className="font-semibold">Ballastlane</div>
            <div className="text-xs text-muted-foreground">TechStore</div>
          </div>
        </div>
        <nav className="flex md:flex-col gap-1">
          {nav.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.end}
              className={({ isActive }) =>
                clsx(
                  "rounded-md px-3 py-2 text-sm transition-colors",
                  isActive
                    ? "bg-accent text-accent-foreground font-medium"
                    : "text-foreground/80 hover:bg-accent hover:text-accent-foreground",
                )
              }
            >
              {n.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-col">
        <header className="flex items-center justify-end gap-3 border-b px-6 py-3">
          <UserMenu />
        </header>
        <main className="min-w-0 flex-1 p-6"><Outlet /></main>
      </div>
    </div>
  );
}
