# Arxitektura

```
Windows Agent (C#/.NET, Windows Service)
     │ HTTPS (device auth: X-Device-Id/X-Device-Secret)
     ▼
Backend API (ASP.NET Core, JWT+RBAC, rate limiting)
     │
     ▼
PostgreSQL  (+ Redis — ixtiyoriy, hozircha ishlatilmaydi)
     ▲
     │ JWT (Admin/User)
React Admin Panel (Vite + TS)
```

## Komponentlar
- **Agent**: Windows Service, ETW asosida detection, SQLite shifrlangan lokal navbat, retry/backoff,
  heartbeat, config-poll, named-pipe orqali alohida foydalanuvchi-sessiya jarayoniga (Notifier) toast
  yuborish.
- **Backend**: Auth/Devices/Users/FileEvents/Applications/Policies/Alerts/Notifications/Dashboard/
  AuditLogs modullari, barchasi bitta ASP.NET Core loyihasida controller sifatida.
- **Frontend**: JWT bilan login, devices/file-events/policies/alerts/notifications/audit-logs sahifalari.

## Ma'lumotlar oqimi (FILE_SENT)
1. ETW hodisalari → `Correlator` xotirada to'planadi.
2. 2 soniyalik "idle" dan keyin `Correlator.FlushAsync` nomzodni baholaydi, SHA-256 hisoblaydi.
3. Natija `LocalQueue` (SQLite, DPAPI-shifrlangan) ga yoziladi.
4. `SyncWorker` navbatni batch qilib `/api/v1/agent/events` ga yuboradi.
5. Backend `event_id` bo'yicha idempotentlik tekshiradi, saqlaydi, `PolicyEngine` ishga tushadi.
6. Qoida mos kelsa `Alert` yaratiladi; admin kerak bo'lsa `Notification` yuboradi.
7. Notification keyingi heartbeat orqali agentga yetadi → named pipe → Notifier → Windows toast.

## Nega bu tarzda
- **Device-secret per device**: bitta kompromis butun tizimni ochib qo'ymaydi; `Status=Suspended`
  bilan bitta qurilmani darhol bloklash mumkin.
- **SQLite lokal navbat**: internet uzilganda ham hodisalar yo'qolmaydi, DPAPI bilan shifrlangani
  uchun disk orqali o'qib bo'lmaydi.
- **event_id unique**: tarmoq qayta urinishlari yoki agent qayta ishga tushishi serverda dublikat
  yaratmaydi.
