import { useEffect, useState } from "react";
import { api } from "../api/client";

export default function AuditLogs() {
  const [items, setItems] = useState<any[]>([]);
  useEffect(() => { api("/api/v1/audit-logs?pageSize=100").then(d => setItems(d.items)); }, []);
  return (
    <div>
      <h2>Audit Logs</h2>
      <table>
        <thead><tr><th>Vaqt</th><th>Actor</th><th>Action</th><th>Entity</th><th>IP</th></tr></thead>
        <tbody>{items.map((a: any) => (
          <tr key={a.id}><td>{new Date(a.createdAt).toLocaleString()}</td><td>{a.actor}</td><td>{a.action}</td><td>{a.entity} {a.entityId?.slice(0, 8)}</td><td>{a.ipAddress ?? "-"}</td></tr>
        ))}</tbody>
      </table>
    </div>
  );
}
