# Fayl yuborish (FILE_SENT) hodisasini aniqlash — texnik cheklovlar

## Nega oddiy FileSystemWatcher yetarli emas
`FileSystemWatcher` faqat fayl tizimidagi o'zgarishlarni (create/modify/delete/rename) ko'radi.
Messenger orqali fayl yuborish odatda **hech qanday yangi fayl yaratmaydi** — dastur foydalanuvchi
tanlagan mavjud faylni o'qiydi va tarmoqqa yuboradi. Demak, voqea fayl tizimida emas, balki
process I/O va tarmoq faoliyatida ko'rinadi.

## Ishlatilgan yondashuv
1. **Application detection** — faqat config'dagi ro'yxatga (`Telegram.exe`, `WhatsApp.exe`, ...) mos
   process'lar kuzatiladi.
2. **File activity (ETW FileIO)** — shu process'lar qaysi foydalanuvchi fayllarini o'qiyapti, qancha
   bayt o'qilgani.
3. **Network activity (ETW TCP send)** — xuddi shu vaqt oralig'ida tarmoqqa qancha ma'lumot ketgani
   (shifrlangan trafik ichini ko'rmaydi — faqat hajm signalini oladi).
4. **Event correlation** — fayl deyarli to'liq o'qilgan (≥90%) + mos keluvchi hajmda tarmoq trafigi
   bo'lsa → `confidence` yuqori bo'ladi va `FILE_SENT` kandidati hosil bo'ladi.

Bu **evristika**, protokol darajasidagi dalil emas. Natija har doim ehtimollik (`confidence`)
bilan beriladi, 100% deb da'vo qilinmaydi.

## Ilova bo'yicha holat

| Ilova | Usul | Ishonchlilik | Asosiy cheklov |
|---|---|---|---|
| Telegram (desktop) | ETW file-read + net-send korrelyatsiyasi | O'rta-yuqori | Rasm/fayl preview'lari false positive berishi mumkin |
| WhatsApp (desktop) | Xuddi shunday | O'rta-yuqori | UWP konteyner sandboxi ba'zan ETW ko'rinishini cheklaydi |
| WhatsApp Web | **Qo'llab-quvvatlanmaydi ishonchli tarzda** | Past | Brauzer jarayoni ichida ishlaydi — process darajasida WhatsApp Web'ni boshqa tab'lardan ajratib bo'lmaydi |
| imo | ETW korrelyatsiya | O'rta | Kam hujjatlashtirilgan protokol, trafik naqshlari o'zgarishi mumkin |
| Microsoft Teams | ETW korrelyatsiya + (tavsiya: Microsoft Graph/Purview) | O'rta | Korporativ muhitda eng ishonchli yo'l — Microsoft Purview DLP yoki Graph API orqali fayl ulashish loglarini olish. ETW faqat qo'shimcha signal |
| Discord | ETW korrelyatsiya | O'rta | Fayl yuklash CDN orqali ketadi, hajm bo'yicha moslik taxminiy |

## Qasddan amalga oshirilmagan narsalar
- Brauzer ichidagi (WhatsApp Web, Telegram Web) yuborishni process darajasida 100% aniqlash — texnik
  jihatdan ishonchsiz, shuning uchun past confidence bilan yoki umuman chiqarilmaydi.
- Fayl mazmunini o'qish yoki tarmoq paketlarini deshifrlash — buni qilish botunlay boshqa xavfsizlik
  va maxfiylik muammolarini keltirib chiqaradi va talablarga zid.

## False positive / false negative
- **False positive:** messenger katta rasmning thumbnail'ini generatsiya qilish uchun uni deyarli
  to'liq o'qishi mumkin, lekin hech kimga yubormagan bo'lishi mumkin.
- **False negative:** fayl avval ilovaning cache papkasiga nusxalanib, keyin o'sha yerdan o'qilsa
  (ba'zi mijozlar shunday qiladi), "user file" filtri uni e'tiborsiz qoldirishi mumkin.
- Ikkala holat ham `confidence` maydonida aks etadi; admin panelda past-ishonchli hodisalarni
  ko'rish/filtrlash mumkin.

## Kengaytirish yo'li
Korporativ muhitda eng ishonchli signal — ilovaning o'z audit/compliance API'lari (masalan,
Microsoft Teams/Graph, Purview DLP). Bular mavjud bo'lsa, ularni alohida "connector" sifatida
qo'shish tavsiya etiladi; bu hujjat shu maqsadda policy/qo'shimcha detector qo'shish nuqtasini
ochiq qoldiradi (`Detection/` papkasi).

## USB va network/share nusxalash

Windows agent ETW `FileIORead` + `FileIOWrite` signallarini bir process ichida korrelyatsiya qiladi:
- removable drive → local user file: `COPIED_IN`;
- local user file → removable drive: `COPIED_OUT`;
- `\\server\share` → local user file: `COPIED_IN`;
- local user file → `\\server\share`: `COPIED_OUT`.

Event metadata'sida `source` va `destination` saqlanadi. Nusxa olishni Windows ETW process I/O darajasida aniqlab bo'lmaydigan holatlarda event sun'iy yaratilmaydi.

## Browser download/upload

Chrome/Edge/Firefox/Brave/Opera desktop processlari mavjud katalogga qo'shilgan:
- browser foydalanuvchi papkasiga yozsa va fayl yakunlansa: `DOWNLOADED`;
- browser foydalanuvchi faylini o'qib, shu vaqt oralig'ida mos TCP send signali bo'lsa: `UPLOADED`.

WhatsApp Web/Telegram Web kabi sayt ichidagi konkret servisni browser processidan ajratish Windows ETW orqali ishonchli emas; shuning uchun agent bunday holatni Telegram/WhatsApp deb uydirmaydi.

