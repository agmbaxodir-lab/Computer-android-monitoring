import { FormEvent, useState } from "react";
import { useAuth } from "../context/AuthContext";

export default function Login() {
  const { login } = useAuth();
  const [u, setU] = useState(""); const [p, setP] = useState(""); const [err, setErr] = useState("");

  async function submit(e: FormEvent) {
    e.preventDefault();
    try { await login(u, p); } catch { setErr("Login yoki parol noto'g'ri"); }
  }

  return (
    <div style={{ display: "flex", height: "100vh", alignItems: "center", justifyContent: "center" }}>
      <form onSubmit={submit} className="card" style={{ width: 320 }}>
        <h2>Admin kirish</h2>
        <div style={{ marginBottom: 10 }}><input placeholder="Username" value={u} onChange={e => setU(e.target.value)} style={{ width: "100%" }} /></div>
        <div style={{ marginBottom: 10 }}><input type="password" placeholder="Password" value={p} onChange={e => setP(e.target.value)} style={{ width: "100%" }} /></div>
        {err && <div style={{ color: "#f87171", marginBottom: 10 }}>{err}</div>}
        <button type="submit" style={{ width: "100%" }}>Kirish</button>
      </form>
    </div>
  );
}
