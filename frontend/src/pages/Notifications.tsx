import { useEffect, useState } from "react";
import { api } from "../api/client";

export default function Notifications() {
  const [items, setItems] = useState<any[]>([]);
  const [targetType, setTargetType] = useState("All");
  const [targetId, setTargetId] = useState("");
  const [title, setTitle] = useState("");
  const [message, setMessage] = useState("");

  function load() { api("/api/v1/notifications?pageSize=100").then(d => setItems(d.items)); }
  useEffect(load, []);

  async function send() {
    await api("/api/v1/notifications", {
      method: "POST",
      body: JSON.stringify({ targetType, targetId: targetType === "All" ? null : targetId, title, message }),
    });
    setTitle(""); setMessage(""); load();
  }

  return (
    <div>
      <h2>Bildirishnomalar</h2>
      <div className="card" style={{ marginBottom: 20 }}>
        <div className="filters">
          <select value={targetType} onChange={e => setTargetType(e.target.value)}>
            <option>All</option><option>Device</option><option>User</option>
          </select>
          {targetType !== "All" && <input placeholder="Target ID (guid)" value={targetId} onChange={e => setTargetId(e.target.value)} />}
          <input placeholder="Sarlavha" value={title} onChange={e => setTitle(e.target.value)} />
          <input placeholder="Xabar" value={message} onChange={e => setMessage(e.target.value)} style={{ width: 300 }} />
          <button onClick={send}>Yuborish</button>
        </div>
        <p style={{ color: "#8a8f98", fontSize: 13 }}>Offline qurilmalarga yuborilgan xabar "Pending" holatda qoladi va qurilma online bo'lganda Windows Toast sifatida yetkaziladi.</p>
      </div>
      <table>
        <thead><tr><th>Vaqt</th><th>Target</th><th>Sarlavha</th><th>Xabar</th><th>Status</th></tr></thead>
        <tbody>{items.map((n: any) => (
          <tr key={n.id}><td>{new Date(n.createdAt).toLocaleString()}</td><td>{n.targetType}</td><td>{n.title}</td><td>{n.message}</td><td>{n.status}</td></tr>
        ))}</tbody>
      </table>
    </div>
  );
}
