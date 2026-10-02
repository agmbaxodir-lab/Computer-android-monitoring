import { useEffect, useRef, useState } from "react";
import { api, errorMessage } from "../api/client";
import { eventLabel, formatSize } from "../eventLabels";

export default function FileEvents() {
  const [items, setItems] = useState<any[]>([]);
  const [total, setTotal] = useState(0);
  const [error, setError] = useState<string | null>(null);
  const [f, setF] = useState({
    platform: "",
    search: "",
    application: "",
    extension: "",
    eventType: ""
  });
  // Avto-yangilanish (setInterval) har doim eng so'nggi filtrlarni ishlatishi uchun
  const fRef = useRef(f);
  fRef.current = f;

  function load() {
    const cur = fRef.current;
    const q = new URLSearchParams();
    if (cur.platform) q.set("platform", cur.platform);
    if (cur.search) q.set("search", cur.search);
    if (cur.application) q.set("application", cur.application);
    if (cur.extension) q.set("extension", cur.extension);
    if (cur.eventType) q.set("eventType", cur.eventType);
    q.set("pageSize", "100");
    api(`/api/v1/file-events?${q}`)
      .then(d => { setItems(d.items); setTotal(d.total); setError(null); })
      .catch(e => setError(errorMessage(e)));
  }

  useEffect(() => { load(); }, [f.platform, f.eventType]);
  useEffect(() => {
    const t = setInterval(load, 10000); // yangi hodisalar o'zi paydo bo'ladi
    return () => clearInterval(t);
  }, []);

  return (
    <div>
      <h2>Fayl monitoring hodisalari</h2>
      <div className="filters">
        <select value={f.platform} onChange={e => setF({ ...f, platform: e.target.value })}>
          <option value="">Barcha platformalar</option>
          <option value="Windows">Windows</option>
          <option value="Android">Android</option>
        </select>
        <input
          placeholder="Qidiruv (Fayl nomi, yo'li)..."
          value={f.search}
          onChange={e => setF({ ...f, search: e.target.value })}
          onKeyDown={e => e.key === "Enter" && load()}
          style={{ minWidth: 200 }}
        />
        <input
          placeholder="Ilova (Telegram, WhatsApp)..."
          value={f.application}
          onChange={e => setF({ ...f, application: e.target.value })}
          onKeyDown={e => e.key === "Enter" && load()}
        />
        <input
          placeholder="Kengaytma (pdf, docx)"
          value={f.extension}
          onChange={e => setF({ ...f, extension: e.target.value })}
          onKeyDown={e => e.key === "Enter" && load()}
          style={{ width: 140 }}
        />
        <select value={f.eventType} onChange={e => setF({ ...f, eventType: e.target.value })}>
          <option value="">Barcha hodisa turlari</option>
          <option value="FILE_SENT">Yuborilgan (FILE_SENT)</option>
          <option value="DOWNLOADED">Qabul qilingan (DOWNLOADED)</option>
          <option value="UPLOADED">Browser upload (UPLOADED)</option>
          <option value="COPIED_IN">USB/Network → Computer</option>
          <option value="COPIED_OUT">Computer → USB/Network</option>
          <option value="CREATED">CREATED</option>
          <option value="MODIFIED">MODIFIED</option>
          <option value="RENAMED">RENAMED</option>
          <option value="DELETED">DELETED</option>
          <option value="SHARED">SHARED</option>
          <option value="OPENED">OPENED</option>
        </select>
        <button onClick={load}>Filtrlash</button>
      </div>
      <p style={{ color: "#8a8f98", fontSize: 13, marginTop: -8 }}>Jami: {total} ta hodisa (oxirgi 100 tasi ko'rsatilgan, har 10 soniyada yangilanadi)</p>
      {error && <p style={{ color: "#f87171" }}>Xatolik: {error}</p>}

      <table>
        <thead>
          <tr>
            <th>Date/Time</th>
            <th>Device</th>
            <th>Platform</th>
            <th>Event Type</th>
            <th>File Name</th>
            <th>Path</th>
            <th>Source</th>
            <th>Destination</th>
            <th>Size</th>
            <th>Application</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {items.map(e => (
            <tr key={e.id}>
              <td>{new Date(e.timestamp).toLocaleString()}</td>
              <td title={e.deviceId}>{e.deviceName ?? `${String(e.deviceId).slice(0, 8)}...`}</td>
              <td>
                <span className={`badge ${(e.platform || "Windows").toLowerCase()}`}>
                  {e.platform || "Windows"}
                </span>
              </td>
              <td><span className="badge event" title={e.eventType}>{eventLabel(e.eventType)}</span></td>
              <td><strong>{e.fileName}</strong></td>
              <td style={{ color: "#8a8f98", fontSize: 12, maxWidth: 240, overflow: "hidden", textOverflow: "ellipsis" }} title={e.filePath}>
                {e.filePath ?? "-"}
              </td>
              <td style={{ color: "#8a8f98", fontSize: 12, maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis" }} title={e.source}>
                {e.source ?? "-"}
              </td>
              <td style={{ color: "#8a8f98", fontSize: 12, maxWidth: 220, overflow: "hidden", textOverflow: "ellipsis" }} title={e.destination}>
                {e.destination ?? "-"}
              </td>
              <td>{formatSize(e.fileSize)}</td>
              <td>{e.application ?? e.processName ?? "-"}</td>
              <td>{e.status}</td>
            </tr>
          ))}
          {items.length === 0 && (
            <tr><td colSpan={11} style={{ textAlign: "center", color: "#8a8f98" }}>Hodisalar topilmadi</td></tr>
          )}
        </tbody>
      </table>
    </div>
  );
}
