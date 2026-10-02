import { useEffect, useState } from "react";
import { api } from "../api/client";

export default function Alerts() {
  const [items, setItems] = useState<any[]>([]);
  function load() { api("/api/v1/alerts?pageSize=100").then(d => setItems(d.items)); }
  useEffect(load, []);
  async function resolve(id: string) { await api(`/api/v1/alerts/${id}/status`, { method: "PATCH", body: JSON.stringify("Resolved") }); load(); }
  return (
    <div>
      <h2>Alertlar</h2>
      <table>
        <thead><tr><th>Vaqt</th><th>Severity</th><th>Xabar</th><th>Status</th><th></th></tr></thead>
        <tbody>{items.map(a => (
          <tr key={a.id}>
            <td>{new Date(a.createdAt).toLocaleString()}</td>
            <td><span className={`badge ${a.severity}`}>{a.severity}</span></td>
            <td>{a.message}</td>
            <td><span className={`badge ${a.status === "Open" ? "Open" : "online"}`}>{a.status}</span></td>
            <td>{a.status === "Open" && <button className="secondary" onClick={() => resolve(a.id)}>Yopish</button>}</td>
          </tr>
        ))}</tbody>
      </table>
    </div>
  );
}
