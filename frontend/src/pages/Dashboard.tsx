import { useEffect, useState } from "react";
import { api } from "../api/client";

export default function Dashboard() {
  const [s, setS] = useState<any>(null);
  const [platformFilter, setPlatformFilter] = useState<string>("all");

  useEffect(() => { api("/api/v1/dashboard/statistics").then(setS).catch(console.error); }, []);
  if (!s) return <p>Yuklanmoqda...</p>;

  const filteredEvents = s.recentEvents.filter((e: any) => {
    if (platformFilter === "all") return true;
    return (e.platform || "Windows").toLowerCase() === platformFilter.toLowerCase();
  });

  return (
    <div>
      <h2>Monitoring Dashboard</h2>
      <div className="grid" style={{ gridTemplateColumns: "repeat(auto-fit, minmax(160px, 1fr))", marginBottom: 20 }}>
        <Stat label="Jami Qurilmalar" value={s.totalDevices} />
        <Stat label="Windows Agentlar" value={s.windowsDevices ?? 0} />
        <Stat label="Android Agentlar" value={s.androidDevices ?? 0} />
        <Stat label="Online Qurilmalar" value={s.onlineDevices} />
        <Stat label="Bugungi hodisalar" value={s.eventsToday} />
        <Stat label="Haftalik hodisalar" value={s.eventsWeek} />
      </div>

      <div className="grid" style={{ gridTemplateColumns: "1fr 1fr", marginBottom: 20 }}>
        <div className="card">
          <h4>Ilova bo'yicha</h4>
          <table><tbody>{s.byApplication.map((r: any) => <tr key={r.application}><td>{r.application}</td><td>{r.count}</td></tr>)}</tbody></table>
        </div>
        <div className="card">
          <h4>Fayl turi bo'yicha</h4>
          <table><tbody>{s.byExtension.map((r: any) => <tr key={r.extension}><td>{r.extension}</td><td>{r.count}</td></tr>)}</tbody></table>
        </div>
      </div>

      <div className="card">
        <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
          <h4 style={{ margin: 0 }}>So'nggi hodisalar</h4>
          <div className="filters" style={{ margin: 0 }}>
            <select value={platformFilter} onChange={e => setPlatformFilter(e.target.value)}>
              <option value="all">Barcha platformalar</option>
              <option value="Windows">Windows</option>
              <option value="Android">Android</option>
            </select>
          </div>
        </div>
        <table>
          <thead>
            <tr>
              <th>Vaqt</th>
              <th>Platforma</th>
              <th>Hodisa turi</th>
              <th>Fayl</th>
              <th>Confidence</th>
            </tr>
          </thead>
          <tbody>
            {filteredEvents.map((e: any) => (
              <tr key={e.id}>
                <td>{new Date(e.timestamp).toLocaleString()}</td>
                <td>
                  <span className={`badge ${(e.platform || "Windows").toLowerCase()}`}>
                    {e.platform || "Windows"}
                  </span>
                </td>
                <td><span className="badge event">{e.eventType}</span></td>
                <td>{e.fileName}</td>
                <td>{e.confidence}</td>
              </tr>
            ))}
            {filteredEvents.length === 0 && (
              <tr><td colSpan={5} style={{ textAlign: "center", color: "#8a8f98" }}>Hodisalar topilmadi</td></tr>
            )}
          </tbody>
        </table>
      </div>
    </div>
  );
}
function Stat({ label, value }: { label: string; value: number }) {
  return <div className="card"><div style={{ color: "#8a8f98", fontSize: 13 }}>{label}</div><div style={{ fontSize: 28, fontWeight: 700 }}>{value}</div></div>;
}
