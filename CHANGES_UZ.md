# Nima tuzatildi (qisqacha)

## 1. Fayllar bazaga tushmasligi / admin panelda ko'rinmasligi
| # | Muammo | Tuzatish |
|---|--------|----------|
| 1 | Agent faqat **yuborilgan** fayllarni (FILE_SENT) aniqlardi, **qabul qilinganlari** umuman yo'q edi | Yangi `Detection/ReceiveWatcher.cs`: qabul qilingan fayllar `DOWNLOADED` sifatida qayd etiladi (ETW yozish hodisasi + `Telegram Desktop`/`WhatsApp` kabi papkalar kuzatuvi) |
| 2 | Ro'yxatdan o'tishdan oldin yaratilgan eventlarda `DeviceId = 000...` bo'lardi → server **butun batchni 400 bilan rad etardi**, agent esa uni navbatdan **o'chirib yuborardi** | Agent yuborish paytida joriy DeviceId qo'yadi; server esa autentifikatsiyadan o'tgan qurilmani ishonchli deb oladi |
| 3 | Server `DbUpdateException` ni **jim yutib**, `200 OK` qaytarardi (hodisa saqlanmasa ham) | Faqat "unique event_id" e'tiborsiz qoldiriladi, boshqa xatolar 500 beradi → agent qayta urinadi |
| 4 | Bitta yaroqsiz event butun batchni yiqitardi; soati noto'g'ri kompyuterlarning eventlari rad etilardi | Yaroqsizlari alohida rad etiladi, qolganlari saqlanadi; kelajak vaqti `hozir` ga tenglanadi |
| 5 | Fayl yo'li (`Path`) agentdan yuborilmasdi | `FileInfoDto.Path` qo'shildi |
| 6 | Server `applications` ro'yxati bo'sh bo'lsa agent o'z ro'yxatini o'chirib, hech narsani kuzatmay qolardi | Bo'sh ro'yxat e'tiborsiz qoldiriladi; API ishga tushganda DB sxemasi va standart ilovalar avtomatik tekshiriladi |
| 7 | Baza qayta yaratilsa agent eski `deviceId` bilan 401 olib, abadiy qotib qolardi | 401 → agent qayta ro'yxatdan o'tadi (to'xtatilgan qurilma uchun 403 — qayta ro'yxatdan o'tmaydi) |
| 8 | ETW sessiyasi bir marta yiqilsa, agent qayta urinmasdi | 30 soniyada qayta urinadi |
| 9 | CORS faqat `192.168.1.40:5173` ga qattiq yozilgan edi, `docker-compose.yml` ham buzuq edi (`volumes`) | CORS sozlanadi (standart: hammasi), compose tuzatildi, panel API manzilini o'zi aniqlaydi |
| 10 | Rate limit butun tizim uchun umumiy edi (120/min) | Har bir qurilma/IP uchun alohida |

Admin panel: File Events sahifasida **Qurilma nomi**, **Yuborilgan / Qabul qilingan** nomlari, hajm (KB/MB), avto-yangilanish (10 s), kengaytma filtri (`pdf` ham, `.pdf` ham).

## 2. Bildirishnoma (Notifications)
| # | Muammo | Tuzatish |
|---|--------|----------|
| 1 | Panel `targetId` ga qurilma **nomini** ("LOGISTIKA") yuborardi, server esa **GUID** kutardi → `400 ... could not be converted to Guid` | Qurilma **ro'yxatdan tanlanadi** (dropdown), GUID avtomatik yuboriladi |
| 2 | "All" xabari birinchi heartbeat qilgan qurilmaga berilib, boshqalarga yetmasdi | "All" → har bir faol qurilmaga alohida yozuv |
| 3 | "User" turi hech qachon yetkazilmasdi | Olib tashlandi (faqat: Barcha qurilmalar / Bitta qurilma) |
| 4 | `Notifier` loyihasi **kompilyatsiya bo'lmasdi** (`NotifyIcon.BeginInvoke` yo'q) — shuning uchun `publish-agent` ichida `Notifier.exe` ham yo'q edi, toast hech qachon chiqmasdi | Tuzatildi; build skripti Notifier.exe yaratilmasa xato beradi; o'rnatish skripti uni darhol ishga tushiradi |
| 5 | Heartbeat har 60 s | 15 s (xabar ~15 soniyada yetadi) |
| 6 | Pipe'da xabar yo'qolishi mumkin edi | Uzilsa qayta navbatga qo'yiladi |

Holatlar: **Kutilmoqda** → **Yetkazildi** (5 soniyada o'zi yangilanadi).

## 3. Boshqa
- Testlar allaqachon kompilyatsiya bo'lmasdi (`DevicesController` konstruktori) — tuzatildi, yangi testlar qo'shildi.
- Dashboard: "bugungi hodisalar" hisobi UTC bo'yicha to'g'rilandi, hodisa turi bo'yicha statistika qo'shildi.
