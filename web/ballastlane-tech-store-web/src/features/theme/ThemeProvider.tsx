import { createContext, useCallback, useContext, useEffect, useState, type ReactNode } from "react";

type Theme = "light" | "dark";
// New key on purpose: the original "theme" key was written eagerly during the
// light-default era, so users had a stale "light" pinned. Fresh key resets the policy.
const KEY = "blc.theme";

interface ThemeCtx { theme: Theme; toggle: () => void; }

const Ctx = createContext<ThemeCtx | null>(null);

// Mirror the inline pre-paint script in index.html: dark unless the user explicitly opted in to light.
function readInitial(): Theme {
  if (typeof document === "undefined") return "dark";
  return document.documentElement.classList.contains("dark") ? "dark" : "light";
}

export function ThemeProvider({ children }: { children: ReactNode }) {
  const [theme, setTheme] = useState<Theme>(readInitial);

  // Apply class to <html>. Persistence happens only inside toggle() — never write
  // here, otherwise we'd pin a brand-default visitor to a value they never picked.
  useEffect(() => {
    const root = document.documentElement;
    if (theme === "dark") root.classList.add("dark"); else root.classList.remove("dark");
  }, [theme]);

  const toggle = useCallback(() => {
    setTheme((current) => {
      const next = current === "dark" ? "light" : "dark";
      try { localStorage.setItem(KEY, next); } catch (_) { /* private mode etc. */ }
      return next;
    });
  }, []);

  return <Ctx.Provider value={{ theme, toggle }}>{children}</Ctx.Provider>;
}

export function useTheme() {
  const v = useContext(Ctx);
  if (!v) throw new Error("useTheme must be used inside <ThemeProvider>");
  return v;
}
