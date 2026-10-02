import { useEffect, useState } from "react";
import { useParams } from "react-router-dom";
import { api } from "../api/client";

export default function DeviceDetail() {
  const { id } = useParams();
  const [device, setDevice] = useState<any>(null);
  const [events, setEvents] = useState<any[]>([]);
  useEffect(() => {
    api(`/api/v1/devices/${id}`).then(setDevice);
    api(`/api/v1/devices/${id}/events?pageSize=100`).then(d => setEvents(d.items));
  }, [id]);
  if (!device) return <p>Yuklanmoqda...</p>;
  return (
    <div>
      <div style={{ display: "flex", alignItems: "center", gap: 12, marginBottom: 16 }}>
        <h2 style={{ margin: 0 }}>{device.hostname}</h2>
        <span className={`badge ${(device.platform || "Windows").toLowerCase()}`}>
          {device.platform || "Windows"}
        </span>
        <span className={`badge ${device.online ? "online" : "offline"}`}>
          {device.online ? "Online" : "Offline"}
        </span>
      </div>

      <div className="card" style={{ marginBottom: 20 }}>
        <p>
          <strong>Platforma:</strong> {device.platform || "Windows"} | 
          <strong> Model:</strong> {device.deviceModel ?? "-"} | 
          <strong> OS:</strong> {device.osVersion ?? "-"}
        </p>
        <p>
          <strong>Foydalanuvchi:</strong> {device.username ?? "-"} | 
          <strong> IP:</strong> {device.ipAddress ?? "-"} | 
          <strong> Agent versiyasi:</strong> {device.agentVersion ?? "-"} | 
          <strong> Holat:</strong> {device.status} | 
          <strong> Oxirgi ko'rilgan:</strong> {device.lastHeartbeatAt ? new Date(device.lastHeartbeatAt).toLocaleString() : "-"}
        </p>
      </div>

      <h3>Fayl hodisalari ({events.length})</h3>
      <table>
        <thead>
          <tr>
            <th>Vaqt</th>
            <th>Turi</th>
            <th>Ilova / Process</th>
            <th>Fayl</th>
            <th>Yo'l (Path)</th>
            <th>Hajm</th>
            <th>SHA-256</th>
            <th>Confidence</th>
          </tr>
        </thead>
        <tbody>
          {events.map(e => (
            <tr key={e.id}>
              <td>{new Date(e.timestamp).toLocaleString()}</td>
              <td><span className="badge event">{e.eventType}</span></td>
              <td>{e.processName || "-"}</td>
              <td><strong>{e.fileName}</strong></td>
              <td style={{ color: "#8a8f98", fontSize: 12 }}>{e.filePath || "-"}</td>
              <td>{e.fileSize} B</td>
              <td style={{ fontFamily: "monospace", fontSize: 11 }}>{e.sha256?.slice(0, 12)}...</td>
              <td>{e.confidence}</td>
            </tr>
          ))}
          {events.length === 0 && (
            <tr><td colSpan={8} style={{ textAlign: "center", color: "#8a8f98" }}>Hodisalar yo'q</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
