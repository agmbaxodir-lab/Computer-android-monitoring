# API qisqacha

To'liq interaktiv hujjat: backend ishga tushgach `/swagger` da.

## Autentifikatsiya
- `POST /api/v1/auth/login` → `{ accessToken, refreshToken, expiresIn }`
- `POST /api/v1/auth/refresh` → yangi juftlik (eski refresh token bir martalik, rotation bilan)
- Himoyalangan endpoint'lar: `Authorization: Bearer <accessToken>`

## Agent endpoint'lari (device auth: `X-Device-Id`, `X-Device-Secret` header)
- `POST /api/v1/agent/register` — enrollment token bilan bir martalik ro'yxatdan o'tish
- `POST /api/v1/agent/heartbeat` — CPU/RAM/navbat holati, javobda pending notification'lar
- `POST /api/v1/agent/events` — `FILE_SENT` hodisalari batch (max 500), idempotent
- `GET  /api/v1/agent/config` — yoqilgan ilovalar ro'yxati, poll intervallari

## Admin endpoint'lari (JWT, RBAC)
- `GET /api/v1/devices`, `GET /api/v1/devices/{id}`, `GET /api/v1/devices/{id}/events`,
  `PATCH /api/v1/devices/{id}/status` (Admin)
- `GET /api/v1/file-events` (filter: from/to/deviceId/application/extension/minSize/maxSize/
  eventType/minConfidence), `GET /api/v1/file-events/{id}`
- `GET /api/v1/alerts`, `POST /api/v1/alerts` (Admin), `PATCH /api/v1/alerts/{id}/status` (Admin)
- `GET/POST/PUT/DELETE /api/v1/policies` (Admin)
- `GET /api/v1/notifications`, `POST /api/v1/notifications` (Admin)
- `GET /api/v1/dashboard/statistics`
- `GET /api/v1/audit-logs` (Admin)

Barcha ro'yxat endpoint'lari `page`/`pageSize` paginatsiyasini qo'llab-quvvatlaydi.
