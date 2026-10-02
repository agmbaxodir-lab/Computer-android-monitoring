# Troubleshooting

## Backend ishga tushmayapti
- `Jwt:Secret` bo'sh yoki 32 belgidan qisqa bo'lsa, ilova ataylab xato beradi (`Program.cs`).
  `.env` dagi `JWT_SECRET`ni tekshiring.
- PostgreSQL'ga ulanib bo'lmasa: `docker compose logs postgres`, `ConnectionStrings__Default`ni
  tekshiring.

## Agent serverga ulana olmayapti
- `ServerUrl` HTTPS bo'lishi shart (development uchungina `AllowInsecureHttp: true`).
- `EnrollmentToken` noto'g'ri bo'lsa `register` 401 qaytaradi — Event Viewer'dagi
  "FileMonitoringAgent" manbasidan loglarni tekshiring.
- Qurilma `Suspended` holatiga o'tkazilgan bo'lsa (`PATCH /devices/{id}/status`), barcha
  so'rovlar 401 qaytaradi.

## ETW sessiyasi ishlamayapti ("NT Kernel Logger" xatosi)
- Boshqa dastur (masalan boshqa monitoring/EDR vositasi) allaqachon kernel session'ni band
  qilgan bo'lishi mumkin — bir vaqtda faqat bitta "NT Kernel Logger" sessiyasi bo'lishi mumkin.
- Agent LocalSystem/Administrator huquqida ishlashi kerak (Windows Service standart holatda
  shunday ishlaydi).

## Toast ko'rinmayapti
- Notifier alohida foydalanuvchi sessiyasida ishlaydi (Scheduled Task, "At log on"). Foydalanuvchi
  hali tizimga kirmagan bo'lsa, xabar heartbeat orqali keyingi safar yetkaziladi (`Pending` holatda).
- Named pipe ulanishi: Notifier va Service bir xil mashinada bo'lishi kerak (pipe local).

## Dublikat hodisalar ko'rinyapti
- Bo'lishi mumkin emas: `file_events.event_id` UNIQUE. Agar ko'rsangiz, bu ikki xil `eventId` bilan
  bir xil faylni bildiradi (masalan fayl ikki marta chinakam yuborilgan) — bu xato emas.

## React panel login qila olmayapti
- `.env`dagi `ADMIN_USERNAME`/`ADMIN_PASSWORD` faqat birinchi ishga tushishda seed qilinadi
  (foydalanuvchilar jadvali bo'sh bo'lsa). Keyinroq o'zgartirish uchun to'g'ridan-to'g'ri DB yoki
  keyingi versiyada qo'shiladigan UsersController orqali boshqariladi.
- CORS: standart holatda istalgan manzil ruxsat etilgan (`Cors:AllowedOrigins = ["*"]`). Cheklash: `.env` da `CORS_ORIGIN=http://192.168.1.31:5173`.
- Panel API manzilini brauzer manzilidan oladi (`http://<panel-host>:8080`). Boshqacha bo'lsa `.env` da `VITE_API_URL` ni belgilang.


## Fayllar admin panelda ko'rinmayapti (File Events bo'sh)
Tekshirish tartibi:
1. **Agent logi** (Event Viewer → Windows Logs → Application → "FileMonitoringAgent", yoki konsolda `FileMonitoring.Agent.exe`):
   - `Registered as <id>` — agent serverga ro'yxatdan o'tgan.
   - `ETW sessiyasi ishga tushdi` — yuborilgan fayllarni aniqlash ishlayapti. Aks holda `ETW session failed` xatosiga qarang
     (administrator/LocalSystem huquqi kerak; agent 30 soniyada qayta urinadi).
   - `Qabul qilingan fayllar kuzatilmoqda: C:\Users\...\Downloads` — qabul qilingan fayllar kuzatuvi ishlayapti.
   - `DOWNLOADED ...` / `FILE_SENT candidate ...` — hodisa aniqlandi, `N ta hodisa serverga yuborildi` — serverga yetib bordi.
   - `Server rejected payload (400): ...` — server sababini ko'rsatadi.
   - `Server qurilmani tanimadi (401). Qayta ro'yxatdan o'tiladi.` — baza qayta yaratilgan, agent o'zi qayta ro'yxatdan o'tadi.
2. **Server logi** (`docker compose logs -f api`): `column "platform" does not exist` kabi xato bo'lsa — API ishga tushganda sxemani
   o'zi to'g'rilaydi; to'g'rilanmasa `database/migrations/002_add_platform_and_path.sql` ni qo'lda qo'llang.
3. **Qurilma holati**: Devices sahifasida qurilma `Suspended` bo'lsa server 403 qaytaradi va hodisalarni qabul qilmaydi.
4. **Qaysi fayllar aniqlanadi**: faqat `Applications` ro'yxatidagi ilovalar (Telegram, WhatsApp, imo, Teams, Discord).
   - Yuborilgan (FILE_SENT): ilova foydalanuvchi faylini (AppData/Windows/Program Files dan tashqarida) deyarli to'liq o'qigan.
   - Qabul qilingan (DOWNLOADED): ilova foydalanuvchi papkasiga fayl yozgan yoki fayl `Telegram Desktop`, `WhatsApp` kabi papkada paydo bo'lgan.
   - Boshqa papkalarni kuzatish uchun `appsettings.json` → `Agent:WatchPaths` ga yo'l qo'shing (masalan `"D:\\Telegram Desktop"`).

## Bildirishnoma (toast) kompyuterda chiqmayapti
- Admin panel → Notifications: xabar holati `Kutilmoqda` → ~15 soniyada `Yetkazildi` ga o'tishi kerak. O'tmasa — agent heartbeat yubormayapti (qurilma offline yoki agent to'xtagan).
- `Yetkazildi` bo'lsa-yu ekranda ko'rinmasa: `FileMonitoring.Agent.Notifier.exe` ishlayaptimi? (Task Manager). Notifier `agent\publish` ichida bo'lishi shart —
  `publish-agent.ps1` Notifier.exe yaratilmasa endi xato beradi. `install-agent.ps1` uni foydalanuvchi sessiyasida darhol ishga tushiradi.
- Windows "Focus assist / Bezovta qilmang" rejimi toastlarni yashiradi (Action Center'da ko'rinadi).
