import { useEffect, useState } from "react";
import { api, errorMessage } from "../api/client";

type Device = { id: string; hostname: string; platform: string; online: boolean; status: string };

export default function Notifications() {
  const [items, setItems] = useState<any[]>([]);
  const [devices, setDevices] = useState<Device[]>([]);
  const [target, setTarget] = useState("all"); // "all" yoki qurilma ID si
  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");
  const [busy, setBusy] = useState(false);
  const [result, setResult] = useState<{ ok: boolean; text: string } | null>(null);

  function load() {
    api("/api/v1/notifications?pageSize=100").then(d => setItems(d.items)).catch(() => { /* keyingi yangilanishda qayta uriniladi */ });
  }

  useEffect(() => {
    api("/api/v1/devices?pageSize=200").then(d => setDevices(d.items)).catch(e => setResult({ ok: false, text: errorMessage(e) }));
    load();
    const t = setInterval(load, 5000); // holat (Kutilmoqda → Yetkazildi) o'zi yangilanadi
    return () => clearInterval(t);
  }, []);

  async function send() {
    if (!title.trim() || !message.trim()) {
      setResult({ ok: false, text: "Sarlavha va xabar matnini kiriting." });
      return;
    }
    setBusy(true); setResult(null);
    try {
      const r = await api("/api/v1/notifications", {
        method: "POST",
        body: JSON.stringify({
          targetType: target === "all" ? "All" : "Device",
          targetId: target === "all" ? null : target,
          title: title.trim(),
          message: message.trim(),
        }),
      });
      setResult({ ok: true, text: `Yuborildi: ${r.created} ta qurilmaga navbatga qo'yildi.` });
      setTitle(""); setMessage(""); load();
    } catch (e) {
      setResult({ ok: false, text: errorMessage(e) });
    } finally {
      setBusy(false);
    }
  }

  return (
    <div>
      <h2>Bildirishnomalar</h2>

      <div className="card" style={{ marginBottom: 20 }}>
        <h4 style={{ marginTop: 0 }}>Yangi xabar yuborish</h4>
        <div style={{ display: "grid", gap: 10, maxWidth: 560 }}>
          <label>
            <div style={{ color: "#8a8f98", fontSize: 13, marginBottom: 4 }}>1. Kimga?</div>
            <select value={target} onChange={e => setTarget(e.target.value)} style={{ width: "100%" }}>
              <option value="all">Barcha qurilmalarga</option>
              {devices.map(d => (
                <option key={d.id} value={d.id}>
                  {d.hostname} ({d.platform}) — {d.online ? "online" : "offline"}{d.status === "Suspended" ? ", to'xtatilgan" : ""}
                </option>
              ))}
            </select>
          </label>
          <label>
            <div style={{ color: "#8a8f98", fontSize: 13, marginBottom: 4 }}>2. Sarlavha</div>
            <input style={{ width: "100%" }} placeholder="Masalan: Diqqat" value={title} maxLength={200} onChange={e => setTitle(e.target.value)} />
          </label>
          <label>
            <div style={{ color: "#8a8f98", fontSize: 13, marginBottom: 4 }}>3. Xabar matni</div>
            <textarea style={{ width: "100%" }} rows={3} placeholder="Masalan: Iltimos, fayllarni faqat ish uchun yuboring." value={message} maxLength={2000} onChange={e => setMessage(e.target.value)} />
          </label>
          <div style={{ display: "flex", gap: 12, alignItems: "center" }}>
            <button onClick={send} disabled={busy}>{busy ? "Yuborilmoqda..." : "Yuborish"}</button>
            {result && <span style={{ color: result.ok ? "#4ade80" : "#f87171" }}>{result.text}</span>}
          </div>
        </div>
        <p style={{ color: "#8a8f98", fontSize: 13, marginBottom: 0 }}>
          Xabar kompyuterga taxminan 15 soniya ichida yetib boradi va Windows bildirishnomasi sifatida chiqadi.
          Qurilma offline bo'lsa, xabar <b>Kutilmoqda</b> holatida saqlanadi va qurilma online bo'lganda yetkaziladi.
        </p>
      </div>

      <table>
        <thead><tr><th>Vaqt</th><th>Kimga</th><th>Sarlavha</th><th>Xabar</th><th>Holat</th></tr></thead>
        <tbody>
          {items.map((n: any) => (
            <tr key={n.id}>
              <td>{new Date(n.createdAt).toLocaleString()}</td>
              <td>{n.targetName ?? (n.targetType === "All" ? "Barcha qurilmalar" : n.targetId)}</td>
              <td>{n.title}</td>
              <td style={{ whiteSpace: "normal", maxWidth: 360 }}>{n.message}</td>
              <td>
                {n.status === "Delivered"
                  ? <span className="badge online" title={n.deliveredAt ? new Date(n.deliveredAt).toLocaleString() : ""}>Yetkazildi</span>
                  : <span className="badge Medium">Kutilmoqda</span>}
              </td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr><td colSpan={5} style={{ textAlign: "center", color: "#8a8f98" }}>Hali xabar yuborilmagan</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
