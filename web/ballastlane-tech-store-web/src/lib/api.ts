import axios, { AxiosError, type AxiosInstance } from "axios";

const TOKEN_KEY = "bts.token";

export const tokenStore = {
  get: () => localStorage.getItem(TOKEN_KEY),
  set: (v: string) => localStorage.setItem(TOKEN_KEY, v),
  clear: () => localStorage.removeItem(TOKEN_KEY),
};

function withAuth(instance: AxiosInstance) {
  instance.interceptors.request.use((cfg) => {
    const t = tokenStore.get();
    if (t) cfg.headers.set("Authorization", `Bearer ${t}`);
    return cfg;
  });
  instance.interceptors.response.use(
    (r) => r,
    (err: AxiosError) => {
      // Bubble validation messages from problem+json so forms can render them.
      const data = err.response?.data as { detail?: string; title?: string } | undefined;
      if (data?.detail || data?.title) err.message = data.detail ?? data.title ?? err.message;
      return Promise.reject(err);
    },
  );
  return instance;
}

export const auth = withAuth(axios.create({ baseURL: import.meta.env.VITE_AUTH_API ?? "http://localhost:5101" }));
export const store = withAuth(axios.create({ baseURL: import.meta.env.VITE_STORE_API ?? "http://localhost:5102" }));
