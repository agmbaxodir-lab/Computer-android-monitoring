import { NavLink, Navigate, Route, Routes } from "react-router-dom";
import { useAuth } from "./context/AuthContext";
import Login from "./pages/Login";
import Dashboard from "./pages/Dashboard";
import Devices from "./pages/Devices";
import DeviceDetail from "./pages/DeviceDetail";
import FileEvents from "./pages/FileEvents";
import Policies from "./pages/Policies";
import Notifications from "./pages/Notifications";
import Alerts from "./pages/Alerts";
import AuditLogs from "./pages/AuditLogs";

function Private({ children }: { children: JSX.Element }) {
  const { isAuthed } = useAuth();
  return isAuthed ? children : <Navigate to="/login" replace />;
}

export default function App() {
  const { isAuthed, logout } = useAuth();
  if (!isAuthed) {
    return (
      <Routes>
        <Route path="*" element={<Login />} />
      </Routes>
    );
  }
  return (
    <div className="layout">
      <aside className="sidebar">
        <h3 style={{ padding: "0 12px" }}>File Monitoring</h3>
        <NavLink to="/" end>Dashboard</NavLink>
        <NavLink to="/devices">Devices</NavLink>
        <NavLink to="/file-events">File Events</NavLink>
        <NavLink to="/alerts">Alerts</NavLink>
        <NavLink to="/policies">Policies</NavLink>
        <NavLink to="/notifications">Notifications</NavLink>
        <NavLink to="/audit-logs">Audit Logs</NavLink>
        <button className="secondary" style={{ marginTop: 16, width: "100%" }} onClick={logout}>Log out</button>
      </aside>
      <main className="content">
        <Routes>
          <Route path="/" element={<Private><Dashboard /></Private>} />
          <Route path="/devices" element={<Private><Devices /></Private>} />
          <Route path="/devices/:id" element={<Private><DeviceDetail /></Private>} />
          <Route path="/file-events" element={<Private><FileEvents /></Private>} />
          <Route path="/alerts" element={<Private><Alerts /></Private>} />
          <Route path="/policies" element={<Private><Policies /></Private>} />
          <Route path="/notifications" element={<Private><Notifications /></Private>} />
          <Route path="/audit-logs" element={<Private><AuditLogs /></Private>} />
          <Route path="*" element={<Navigate to="/" replace />} />
        </Routes>
      </main>
    </div>
  );
}
