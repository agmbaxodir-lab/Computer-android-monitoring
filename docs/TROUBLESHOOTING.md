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
- CORS: `backend/appsettings.json` → `Cors:AllowedOrigins` frontend manzilini o'z ichiga olishi kerak.
