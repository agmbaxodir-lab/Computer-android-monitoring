import { useEffect, useState } from "react";
import { Link } from "react-router-dom";
import { api } from "../api/client";

export default function Devices() {
  const [items, setItems] = useState<any[]>([]);
  const [search, setSearch] = useState("");
  const [platform, setPlatform] = useState("");
  const [status, setStatus] = useState("");

  function loadDevices() {
    const q = new URLSearchParams();
    if (search) q.set("search", search);
    if (platform) q.set("platform", platform);
    if (status) q.set("status", status);
    q.set("pageSize", "100");
    api(`/api/v1/devices?${q}`).then(d => setItems(d.items));
  }

  useEffect(() => {
    loadDevices();
  }, [platform, status]);

  return (
    <div>
      <h2>Qurilmalar ro'yxati</h2>
      <div className="filters">
        <input
          placeholder="Qidiruv (Hostname, User, IP)..."
          value={search}
          onChange={e => setSearch(e.target.value)}
          onKeyDown={e => e.key === "Enter" && loadDevices()}
          style={{ minWidth: 240 }}
        />
        <select value={platform} onChange={e => setPlatform(e.target.value)}>
          <option value="">Barcha platformalar</option>
          <option value="Windows">Windows</option>
          <option value="Android">Android</option>
        </select>
        <select value={status} onChange={e => setStatus(e.target.value)}>
          <option value="">Barcha holatlar</option>
          <option value="Active">Active</option>
          <option value="Suspended">Suspended</option>
        </select>
        <button onClick={loadDevices}>Qidirish</button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Platforma</th>
            <th>Hostname / Qurilma</th>
            <th>Model</th>
            <th>Username</th>
            <th>IP Manzil</th>
            <th>OS Versiya</th>
            <th>Agent</th>
            <th>Holat</th>
            <th>Oxirgi ko'rilgan (Last Seen)</th>
          </tr>
        </thead>
        <tbody>
          {items.map(d => (
            <tr key={d.id}>
              <td>
                <span className={`badge ${(d.platform || "Windows").toLowerCase()}`}>
                  {d.platform || "Windows"}
                </span>
              </td>
              <td><Link to={`/devices/${d.id}`} style={{ fontWeight: 600 }}>{d.hostname}</Link></td>
              <td>{d.deviceModel ?? "-"}</td>
              <td>{d.username ?? "-"}</td>
              <td>{d.ipAddress ?? "-"}</td>
              <td>{d.osVersion ?? "-"}</td>
              <td>{d.agentVersion ?? "-"}</td>
              <td>
                <span className={`badge ${d.online ? "online" : "offline"}`}>
                  {d.online ? "Online" : "Offline"}
                </span>
                {d.status === "Suspended" && <span className="badge offline" style={{ marginLeft: 4 }}>Suspended</span>}
              </td>
              <td>{d.lastHeartbeatAt ? new Date(d.lastHeartbeatAt).toLocaleString() : "-"}</td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr><td colSpan={9} style={{ textAlign: "center", color: "#8a8f98" }}>Mos qurilmalar topilmadi</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
