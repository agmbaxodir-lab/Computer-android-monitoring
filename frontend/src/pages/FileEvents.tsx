import { useEffect, useState } from "react";
import { api } from "../api/client";

export default function FileEvents() {
  const [items, setItems] = useState<any[]>([]);
  const [f, setF] = useState({
    platform: "",
    search: "",
    application: "",
    extension: "",
    eventType: ""
  });

  function load() {
    const q = new URLSearchParams();
    if (f.platform) q.set("platform", f.platform);
    if (f.search) q.set("search", f.search);
    if (f.application) q.set("application", f.application);
    if (f.extension) q.set("extension", f.extension);
    if (f.eventType) q.set("eventType", f.eventType);
    q.set("pageSize", "100");
    api(`/api/v1/file-events?${q}`).then(d => setItems(d.items));
  }

  useEffect(() => {
    load();
  }, [f.platform, f.eventType]);

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
        />
        <input
          placeholder="Kengaytma (.pdf, .docx)"
          value={f.extension}
          onChange={e => setF({ ...f, extension: e.target.value })}
          style={{ width: 140 }}
        />
        <select value={f.eventType} onChange={e => setF({ ...f, eventType: e.target.value })}>
          <option value="">Barcha event turlari</option>
          <option value="FILE_SENT">FILE_SENT</option>
          <option value="CREATED">CREATED</option>
          <option value="MODIFIED">MODIFIED</option>
          <option value="RENAMED">RENAMED</option>
          <option value="DELETED">DELETED</option>
          <option value="SHARED">SHARED</option>
          <option value="DOWNLOADED">DOWNLOADED</option>
          <option value="OPENED">OPENED</option>
        </select>
        <button onClick={load}>Filtrlash</button>
      </div>

      <table>
        <thead>
          <tr>
            <th>Vaqt</th>
            <th>Platforma</th>
            <th>Turi</th>
            <th>Foydalanuvchi</th>
            <th>Qurilma ID</th>
            <th>Ilova</th>
            <th>Fayl</th>
            <th>Fayl Yo'li</th>
            <th>Hajm</th>
            <th>SHA-256</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          {items.map(e => (
            <tr key={e.id}>
              <td>{new Date(e.timestamp).toLocaleString()}</td>
              <td>
                <span className={`badge ${(e.platform || "Windows").toLowerCase()}`}>
                  {e.platform || "Windows"}
                </span>
              </td>
              <td><span className="badge event">{e.eventType}</span></td>
              <td>{e.osUsername ?? "-"}</td>
              <td><span style={{ fontFamily: "monospace", fontSize: 11 }}>{e.deviceId.slice(0, 8)}...</span></td>
              <td>{e.application ?? "-"}</td>
              <td><strong>{e.fileName}</strong></td>
              <td style={{ color: "#8a8f98", fontSize: 12, maxWidth: 240, overflow: "hidden", textOverflow: "ellipsis" }} title={e.filePath}>
                {e.filePath ?? "-"}
              </td>
              <td>{e.fileSize} B</td>
              <td style={{ fontFamily: "monospace", fontSize: 11 }}>{e.sha256?.slice(0, 10)}...</td>
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
