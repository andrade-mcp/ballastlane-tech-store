import { useState } from "react";
import { Link, useNavigate } from "react-router-dom";
import { useAuth } from "@/features/auth/AuthProvider";
import { BrandButton } from "@/components/BrandButton";

export function RegisterPage() {
  const { register } = useAuth();
  const nav = useNavigate();
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [error, setError] = useState<string | null>(null);
  const [loading, setLoading] = useState(false);

  const onSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError(null); setLoading(true);
    try { await register(email, password, displayName); nav("/", { replace: true }); }
    catch (err) { setError(err instanceof Error ? err.message : "Registration failed"); }
    finally { setLoading(false); }
  };

  return (
    <div className="grid min-h-screen place-items-center p-4">
      <div className="card w-full max-w-md p-6">
        <div className="mb-6 flex items-center gap-3">
          <img src="/logo.png" alt="Ballastlane" className="h-12 w-auto" />
          <div className="leading-tight">
            <h1 className="text-xl font-semibold">Create an account</h1>
            <p className="text-sm text-muted-foreground">BallastlaneTechStore</p>
          </div>
        </div>
        <form onSubmit={onSubmit} className="space-y-4">
          <div>
            <label className="label">Display name</label>
            <input className="input" required value={displayName} onChange={(e) => setDisplayName(e.target.value)} />
          </div>
          <div>
            <label className="label">Email</label>
            <input className="input" type="email" required value={email} onChange={(e) => setEmail(e.target.value)} />
          </div>
          <div>
            <label className="label">Password</label>
            <input className="input" type="password" required minLength={8} value={password} onChange={(e) => setPassword(e.target.value)} />
            <p className="mt-1 text-xs text-muted-foreground">Min 8 characters.</p>
          </div>
          {error && <p className="text-sm text-destructive">{error}</p>}
          <BrandButton type="submit" disabled={loading} className="w-full">
            {loading ? "Creating…" : "Create account"}
          </BrandButton>
        </form>
        <p className="mt-4 text-center text-sm text-muted-foreground">
          Already have an account? <Link className="underline" to="/login">Sign in</Link>
        </p>
      </div>
    </div>
  );
}
