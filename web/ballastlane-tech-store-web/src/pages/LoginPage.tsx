import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "@/features/auth/AuthProvider";
import { useTheme } from "@/features/theme/ThemeProvider";

export function LoginPage() {
  const { login } = useAuth();
  const { theme, toggle } = useTheme();
  const nav = useNavigate();
  const [email, setEmail] = useState("demo@ballastlane.dev");
  const [password, setPassword] = useState("Demo!2026");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null); setLoading(true);
    try { await login(email, password); nav("/", { replace: true }); }
    catch (err) { setError(err instanceof Error ? err.message : "Login failed"); }
    finally { setLoading(false); }
  };

  return (
    <div className="grid min-h-screen place-items-center p-4">
      <div className="card w-full max-w-md p-6">
        <div className="mb-6 flex items-center justify-between">
          <div>
            <h1 className="text-xl font-semibold">Sign in</h1>
            <p className="text-sm text-muted-foreground">BallastlaneTechStore</p>
          </div>
          <button className="btn-ghost" onClick={toggle}>{theme === "dark" ? "Light" : "Dark"}</button>
        </div>
        <form onSubmit={onSubmit} className="space-y-4">
          <div>
            <label className="label">Email</label>
            <input className="input" type="email" autoComplete="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div>
            <label className="label">Password</label>
            <input className="input" type="password" autoComplete="current-password" required value={password} onChange={(e) => setPassword(e.target.value)} />
          </div>
          {error && <p className="text-sm text-destructive">{error}</p>}
          <button className="btn-primary w-full" disabled={loading}>{loading ? "Signing in…" : "Sign in"}</button>
        </form>
        <p className="mt-4 text-center text-sm text-muted-foreground">
          No account? <Link className="underline" to="/register">Register</Link>
        </p>
        <p className="mt-2 text-center text-xs text-muted-foreground">
          Demo: <code>demo@ballastlane.dev</code> / <code>Demo!2026</code>
        </p>
      </div>
    </div>
  );
}
