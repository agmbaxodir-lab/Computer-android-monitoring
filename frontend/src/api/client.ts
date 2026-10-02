const BASE = import.meta.env.VITE_API_URL || "http://localhost:8080";

function getTokens() {
  return { access: localStorage.getItem("access_token"), refresh: localStorage.getItem("refresh_token") };
}
function setTokens(access: string, refresh: string) {
  localStorage.setItem("access_token", access); localStorage.setItem("refresh_token", refresh);
}
export function clearTokens() {
  localStorage.removeItem("access_token"); localStorage.removeItem("refresh_token");
}

async function refresh(): Promise<boolean> {
  const { refresh } = getTokens();
  if (!refresh) return false;
  const r = await fetch(`${BASE}/api/v1/auth/refresh`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ refreshToken: refresh }),
  });
  if (!r.ok) return false;
  const d = await r.json();
  setTokens(d.accessToken, d.refreshToken);
  return true;
}

export async function api<T = any>(path: string, opts: RequestInit = {}): Promise<T> {
  const { access } = getTokens();
  const doFetch = (token: string | null) =>
    fetch(`${BASE}${path}`, {
      ...opts,
      headers: { "Content-Type": "application/json", ...(token ? { Authorization: `Bearer ${token}` } : {}), ...(opts.headers || {}) },
    });
  let res = await doFetch(access);
  if (res.status === 401 && (await refresh())) res = await doFetch(getTokens().access);
  if (res.status === 401) { clearTokens(); window.location.href = "/login"; throw new Error("Unauthorized"); }
  if (!res.ok) { const body = await res.text(); throw new Error(body || `HTTP ${res.status}`); }
  return res.status === 204 ? (undefined as T) : res.json();
}

export async function login(username: string, password: string) {
  const r = await fetch(`${BASE}/api/v1/auth/login`, {
    method: "POST", headers: { "Content-Type": "application/json" }, body: JSON.stringify({ username, password }),
  });
  if (!r.ok) throw new Error("Invalid credentials");
  const d = await r.json();
  setTokens(d.accessToken, d.refreshToken);
}
