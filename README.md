# File Monitoring System (Multi-Platform: Windows & Android)

Korxona tomonidan boshqariladigan **Windows** va **Android** qurilmalarda messenger/fayl-almashish ilovalari
(Telegram, WhatsApp, imo, Microsoft Teams, Discord) hamda umumiy xotira bo'ylab fayl operatsiyalarini (yuklash, yaratish, o'zgartirish, nomini o'zgartirish, o'chirish, ulashish) kuzatuvchi, faqat metadata yig'uvchi monitoring platformasi.

> **Muhim**: Tizim keylogger, screenshot/mikrofon/kamera kuzatuvi yoki xabar mazmunini o'qish bilan shug'ullanmaydi.
> Faqat fayl metadata'si (nomi, yo'li, hajmi, hash'i, ilova, hodisa turi, vaqt) yig'iladi.
> Faqat tashkilot nazoratidagi va qonuniy monitoring qilinishi lozim bo'lgan qurilmalarda ishlatish uchun mo'ljallangan.

---

## Arxitektura

```
Windows Agent (.NET 8, ETW) ──────┐
                                   ├──> HTTPS ──> Backend API (ASP.NET Core) ──> PostgreSQL
Android Agent (Native Kotlin) ────┘                      │
                                                         └──> React Admin Dashboard
```

- **Windows Agent**: Windows Service, Kernel ETW (`FileIO` + `TcpIpSend`), DPAPI bilan shifrlangan SQLite navbat.
- **Android Agent**: Native Kotlin, Android KeyStore (AES-GCM), Scoped Storage / `FileObserver` / `MediaStore`, Room SQLite offline navbat, WorkManager retry.
- **Backend API**: ASP.NET Core 8, JWT + Refresh Token, Device Secret hash auth, Rate Limiting, Policy Engine, Audit Logger.
- **Database**: PostgreSQL 16 (Migrationlar: `001_init.sql`, `002_add_platform_and_path.sql`).
- **Dashboard**: React + TypeScript + Vite, Windows/Android filter, live status, search, device detail.

---

## 1. Talablar (Requirements)

- **Docker & Docker Compose** (Server va Database uchun)
- **.NET 8 SDK** (Backend va Windows Agent ishlab chiqish uchun)
- **Android Studio (Giraffe+ / Koala / Ladybug)** yoki **JDK 17 + Android SDK API 34** (Android Agent uchun)
- **Node.js 18+ & npm** (Frontendni alohida yurgazish uchun)

---

## 2. Environment Variables

Loyihaning `.env.example` faylidan `.env` nusxasini yarating:

```bash
cp .env.example .env
```

Asosiy o'zgaruvchilar:
```ini
POSTGRES_DB=filemon
POSTGRES_USER=filemon
POSTGRES_PASSWORD=filemon
JWT_SECRET=super-secret-jwt-key-at-least-32-chars-long
ENROLLMENT_TOKEN=corporate-enrollment-token-2026
ADMIN_USERNAME=admin
ADMIN_PASSWORD=ChangeMe123!
VITE_API_URL=http://localhost:8080
```

---

## 3. Backend va Dashboard'ni Ishga Tushirish

### Usul A: Docker Compose (Tavsiya etiladi)

```bash
docker compose up --build -d
```
- **Backend API & Swagger**: `http://localhost:8080/swagger`
- **React Admin Dashboard**: `http://localhost:5173` (Login: `ADMIN_USERNAME` / `ADMIN_PASSWORD`)
- PostgreSQL konteyneri `database/migrations/` papkasidagi `001_init.sql` va `002_add_platform_and_path.sql` migratsiyalarini avtomatik qo'llaydi.

### Usul B: Lokal (Docker'siz)

**1. Database Setup:**
```bash
psql -U filemon -d filemon -f database/migrations/001_init.sql
psql -U filemon -d filemon -f database/migrations/002_add_platform_and_path.sql
```

**2. Backend:**
```bash
cd backend/FileMonitoring.Api
dotnet restore
export ConnectionStrings__Default="Host=localhost;Database=filemon;Username=filemon;Password=filemon"
export Jwt__Secret="super-secret-jwt-key-at-least-32-chars-long"
export Agent__EnrollmentToken="corporate-enrollment-token-2026"
export Admin__Username="admin"
export Admin__Password="ChangeMe123!"
dotnet run
```

**3. Frontend:**
```bash
cd frontend
npm install
npm run dev
```

---

## 4. Testlarni Bajarish

Backend va barcha platformalar (Windows, Android, Multi-Platform integration) testlari:

```bash
dotnet test backend/FileMonitoring.Api.Tests
```
*(Faqat Windows'da Windows Agent testlarini yurgazish)*:
```powershell
dotnet test agent/FileMonitoring.Agent.Tests
```

---

## 5. Android Agent — Build va O'rnatish

Android agent kodi `android-agent/` papkasida joylashgan.

### Build (Android Studio yoki CLI)
```powershell
cd android-agent
.\gradlew.bat assembleDebug
```
Natijaviy APK: `android-agent/app/build/outputs/apk/debug/app-debug.apk`

Unit testlarni ishga tushirish:
```powershell
.\gradlew.bat test
```

### O'rnatish
Qurilmangizni (yoki Android Emulyatorni) ulang va buyruqni bering:
```bash
adb install android-agent/app/build/outputs/apk/debug/app-debug.apk
```

---

## 6. Android Qurilmani Sozlash va Ruxsatlar

Android xavfsizlik modeli doirasida to'liq monitoring ishlashi uchun quyidagi ruxsatlarni tasdiqlang:

1. **All Files Access (Umumiy xotiradagi fayllarni kuzatish uchun)**:
   - UI: `Settings` → `Apps` → `Special app access` → `All files access` → `File Monitoring Agent` → **Allow**.
   - Yoki ADB orqali:
     ```bash
     adb shell appops set com.filemonitoring.agent MANAGE_EXTERNAL_STORAGE allow
     ```

2. **Usage Access (Fayl ochilganda Telegram, WhatsApp va h.k. ilovalarni aniqlash uchun)**:
   - UI: `Settings` → `Apps` → `Special app access` → `Usage access` → `File Monitoring Agent` → **Allow**.
   - Yoki ADB orqali:
     ```bash
     adb shell appops set com.filemonitoring.agent GET_USAGE_STATS allow
     ```

3. **Korporativ boshqaruv (Android Enterprise / Device Owner - ixtiyoriy)**:
   ```bash
   adb shell dpm set-device-owner com.filemonitoring.agent/.service.DeviceAdminReceiver
   ```

---

## 7. Enrollment Token Olish va Agentni Serverga Ulash

1. `.env` faylidagi `ENROLLMENT_TOKEN` qiymatidan nusxa oling (masalan, `corporate-enrollment-token-2026`).
2. Android qurilmada **File Monitoring Agent** ilovasini oching:
   - **Server URL**:
     - Android Emulyatorda: `http://10.0.2.2:8080`
     - Lokal tarmoqdagi real telefonda: `http://<KOMPYUTER_IP>:8080` (masalan, `http://192.168.1.50:8080`)
     - Production: `https://filemon.company.com`
   - **Enrollment Token**: Olingan tokenni kiriting.
3. **"Enroll Device"** tugmasini bosing:
   - Agent serverga xavfsiz so'rov yuboradi.
   - Server har bir qurilma uchun maxsus `deviceId` va 256-bit `deviceSecret` generatsiya qiladi.
   - Secret va identifikator Android apparat darajasidagi **Android KeyStore** (AES-GCM 256) ichida shifrlab saqlanadi. Manba kodida secret qolmaydi.
4. **"Start Monitoring"** tugmasini bosing:
   - Background Foreground Service (`AgentForegroundService`) ishga tushadi va bildirishnoma panelida holat ko'rinadi.

---

## 8. Monitoringni Tekshirish

1. **Test hodisasini hosil qilish**:
   - Ilovada to'g'ridan-to'g'ri **"Send Test Event"** tugmasini bosing.
   - Yoki telefonda biror faylni yuklab oling (Download papkasiga) yoki Telegram/WhatsApp orqali fayl qabul qiling.
2. **Dashboard orqali tekshirish**:
   - Brauzerda `http://localhost:5173` ga kiring.
   - **Dashboard**: "Android Agentlar" va "Online" soni oshganini ko'rasiz.
   - **Qurilmalar**: Platforma filtri orqali "Android" ni tanlang — yangi ulangan telefon modeli, OS va oxirgi ko'rilgan vaqti chiqadi.
   - **Fayl hodisalari**: "Platforma: Android" va "Event turi: CREATED / DOWNLOADED / SHARED" bo'yicha hodisalarni ko'rishingiz mumkin.

---

## 9. Android Xavfsizlik va Maxfiylik Cheklovlari (Security & Constraints)

Android operatsion tizimining Scoped Storage arxitekturasi quyidagi xavfsizlik cheklovlariga ega:
- **Private App Storage (`/data/data/<package>/`)**: Oddiy Android ilovalari boshqa ilovalarning shaxsiy papkalariga kirishi Android OS yadro darajasida taqiqlangan. Tizim xavfsizlik cheklovlarini buzmaydi (bypass qilmaydi).
- **Kuzatiladigan hududlar**: Umumiy xotira (Public Shared Storage) — `Download`, `Documents`, `Pictures`, `DCIM`, `Movies` va umumiy tashqi xotiradagi barcha amallar (`CREATED`, `MODIFIED`, `RENAMED`, `DELETED`, `DOWNLOADED`, `SHARED`, `OPENED`).
- **Ilovani aniqlash**: Android `UsageStatsManager` orqali fayl o'zgarishi paytidagi faol foreground ilova (masalan, Telegram yoki WhatsApp) aniqlanadi va hodisa `confidence` ko'rsatkichi bilan biriktiriladi.
- **Offline Navbat**: Aloqa uzilganda barcha hodisalar Room SQLite bazasida navbatda to'planadi va tarmoq tiklanganda WorkManager / Coroutine orqali eksponensial backoff bilan qayta uzatiladi.

---

## 10. Production va Troubleshooting

### Production Sozlamalari
- Serverda HTTPS sertifikatini (TLS 1.3) majburiy qiling (`network-security-config.xml` da default holatda HTTP bloklangan).
- Har bir qurilma kompromis bo'lganda Admin Dashboard orqali uning holatini darhol `Suspended` ga o'tkazish mumkin (bunda agent tokeni darhol bekor qilinadi).

### Troubleshooting
- **Agent serverga ulanmayapti**:
  - Emulyatorda `localhost` o'rniga `http://10.0.2.2:8080` ishlating.
  - Real telefonda kompyuter va telefon bir xil Wi-Fi tarmog'ida ekanligiga va Windows Firewall 8080 portini bloklamayotganiga ishonch hosil qiling.
- **Hodisalar ko'rinmayapti**:
  - Qurilma sozlamalarida `All files access` va `Usage access` berilganligini tekshiring.
  - Ilovada "Local Queue" sonini ko'ring; agar son oshib ketsa, server bilan aloqa yoki enrollment token to'g'riligini tekshiring.
