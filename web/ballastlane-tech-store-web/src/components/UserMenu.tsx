import { useEffect, useRef, useState } from "react";
import clsx from "clsx";
import { useAuth } from "@/features/auth/AuthProvider";
import { useTheme } from "@/features/theme/ThemeProvider";

// Initials for the avatar bubble: first letter of each whitespace-separated word, max 2.
function initialsOf(name: string | undefined): string {
  if (!name) return "?";
  const parts = name.trim().split(/\s+/).filter(Boolean);
  if (parts.length === 0) return "?";
  if (parts.length === 1) return parts[0]!.slice(0, 2).toUpperCase();
  return (parts[0]![0]! + parts[parts.length - 1]![0]!).toUpperCase();
}

export function UserMenu() {
  const { user, logout } = useAuth();
  const { theme, toggle } = useTheme();
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);

  // Close on outside click + Escape.
  useEffect(() => {
    if (!open) return;
    const onClick = (e: MouseEvent) => {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    };
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") setOpen(false); };
    document.addEventListener("mousedown", onClick);
    document.addEventListener("keydown", onKey);
    return () => {
      document.removeEventListener("mousedown", onClick);
      document.removeEventListener("keydown", onKey);
    };
  }, [open]);

  if (!user) return null;

  return (
    <div ref={ref} className="relative">
      <button
        type="button"
        aria-label="Account menu"
        aria-haspopup="menu"
        aria-expanded={open}
        onClick={() => setOpen((v) => !v)}
        className={clsx(
          "grid h-9 w-9 place-items-center rounded-full bg-primary text-sm font-semibold text-primary-foreground",
          "ring-offset-background transition focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2",
          "hover:brightness-110",
        )}
      >
        {initialsOf(user.displayName)}
      </button>

      {open && (
        <div
          role="menu"
          className="absolute right-0 z-50 mt-2 w-64 overflow-hidden rounded-lg border bg-popover text-popover-foreground shadow-lg"
        >
          <div className="flex items-center gap-3 border-b px-4 py-3">
            <div className="grid h-9 w-9 shrink-0 place-items-center rounded-full bg-primary text-sm font-semibold text-primary-foreground">
              {initialsOf(user.displayName)}
            </div>
            <div className="min-w-0 leading-tight">
              <div className="truncate font-medium">{user.displayName}</div>
              <div className="truncate text-xs text-muted-foreground">{user.email}</div>
              <div className="mt-0.5 text-[10px] uppercase tracking-wide text-muted-foreground">{user.role}</div>
            </div>
          </div>
          <button role="menuitem" className="flex w-full items-center justify-between px-4 py-2 text-sm hover:bg-accent hover:text-accent-foreground"
                  onClick={() => { toggle(); setOpen(false); }}>
            <span>Theme</span>
            <span className="text-xs text-muted-foreground">{theme === "dark" ? "Dark · switch to light" : "Light · switch to dark"}</span>
          </button>
          <div className="border-t" />
          <button role="menuitem" className="block w-full px-4 py-2 text-left text-sm text-destructive hover:bg-destructive/10"
                  onClick={() => { setOpen(false); logout(); }}>
            Sign out
          </button>
        </div>
      )}
    </div>
  );
}
