import { NavLink, Outlet } from "react-router-dom";
import clsx from "clsx";
import { useAuth } from "@/features/auth/AuthProvider";
import { useTheme } from "@/features/theme/ThemeProvider";

const nav = [
  { to: "/", label: "Dashboard", end: true },
  { to: "/customers", label: "Customers" },
  { to: "/products", label: "Products" },
  { to: "/orders", label: "Orders" },
];

export function AppLayout() {
  const { user, logout } = useAuth();
  const { theme, toggle } = useTheme();

  return (
    <div className="grid min-h-screen grid-cols-1 md:grid-cols-[240px_1fr]">
      <aside className="border-b border-sidebar-border bg-sidebar text-sidebar-foreground p-4 md:border-b-0 md:border-r">
        <div className="mb-6 flex items-center gap-2">
          <div className="grid h-8 w-8 place-items-center rounded-md bg-primary text-primary-foreground font-semibold">BT</div>
          <div className="font-semibold">TechStore</div>
        </div>
        <nav className="flex md:flex-col gap-1">
          {nav.map((n) => (
            <NavLink
              key={n.to}
              to={n.to}
              end={n.end}
              className={({ isActive }) =>
                clsx("rounded-md px-3 py-2 text-sm transition-colors",
                     isActive ? "bg-accent text-accent-foreground font-medium" : "hover:bg-accent")
              }
            >
              {n.label}
            </NavLink>
          ))}
        </nav>
      </aside>
      <div className="flex min-w-0 flex-col">
        <header className="flex items-center justify-between border-b px-6 py-3">
          <div className="text-sm text-muted-foreground">
            Signed in as <span className="font-medium text-foreground">{user?.displayName}</span>
          </div>
          <div className="flex items-center gap-2">
            <button className="btn-ghost" onClick={toggle}>{theme === "dark" ? "Light" : "Dark"}</button>
            <button className="btn-outline" onClick={logout}>Sign out</button>
          </div>
        </header>
        <main className="min-w-0 flex-1 p-6"><Outlet /></main>
      </div>
    </div>
  );
}
