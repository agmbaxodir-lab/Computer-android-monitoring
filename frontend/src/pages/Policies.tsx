import { useEffect, useState } from "react";
import { api } from "../api/client";

const FIELDS = ["application", "extension", "size", "confidence", "process"];
const OPS = ["eq", "neq", "gt", "gte", "lt", "lte", "contains"];

export default function Policies() {
  const [items, setItems] = useState<any[]>([]);
  const [name, setName] = useState("");
  const [rules, setRules] = useState([{ field: "extension", operator: "eq", value: ".exe" }]);

  function load() { api("/api/v1/policies").then(setItems); }
  useEffect(load, []);

  async function create() {
    await api("/api/v1/policies", { method: "POST", body: JSON.stringify({ name, enabled: true, action: "Alert", rules }) });
    setName(""); load();
  }
  async function remove(id: string) { await api(`/api/v1/policies/${id}`, { method: "DELETE" }); load(); }

  return (
    <div>
      <h2>Policies</h2>
      <div className="card" style={{ marginBottom: 20 }}>
        <h4>Yangi policy</h4>
        <input placeholder="Nomi" value={name} onChange={e => setName(e.target.value)} style={{ marginBottom: 10, width: 300 }} />
        {rules.map((r, i) => (
          <div key={i} className="filters">
            <select value={r.field} onChange={e => { const c = [...rules]; c[i].field = e.target.value; setRules(c); }}>
              {FIELDS.map(f => <option key={f}>{f}</option>)}
            </select>
            <select value={r.operator} onChange={e => { const c = [...rules]; c[i].operator = e.target.value; setRules(c); }}>
              {OPS.map(o => <option key={o}>{o}</option>)}
            </select>
            <input value={r.value} onChange={e => { const c = [...rules]; c[i].value = e.target.value; setRules(c); }} />
          </div>
        ))}
        <button className="secondary" onClick={() => setRules([...rules, { field: "extension", operator: "eq", value: "" }])}>+ Qoida</button>
        <button onClick={create} style={{ marginLeft: 8 }}>Yaratish</button>
      </div>
      <table>
        <thead><tr><th>Nomi</th><th>Yoqilgan</th><th>Qoidalar</th><th></th></tr></thead>
        <tbody>{items.map((p: any) => (
          <tr key={p.id}>
            <td>{p.name}</td><td>{p.enabled ? "ha" : "yo'q"}</td>
            <td>{p.rules.map((r: any) => `${r.field} ${r.operator} ${r.value}`).join(" AND ")}</td>
            <td><button className="secondary" onClick={() => remove(p.id)}>O'chirish</button></td>
          </tr>
        ))}</tbody>
      </table>
    </div>
  );
}
