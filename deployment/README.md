# Deployment

## Backend + DB + Frontend (Docker)
```
cp .env.example .env   # qiymatlarni to'ldiring
docker compose up --build -d
docker compose logs -f api
```
Backend: http://localhost:8080/swagger
Frontend: http://localhost:5173

## Windows Agent
1. Windows build mashinasida (yoki CI runner'da): `./deployment/publish-agent.ps1`
2. Maqsadli kompyuterda Administrator PowerShell'da:
   ```
   ./deployment/install-agent.ps1 -ServerUrl "https://filemon.company.local" -EnrollmentToken "<.env dagi ENROLLMENT_TOKEN>"
   ```
3. O'chirish: `./deployment/install-agent.ps1 -Uninstall`

## Production eslatmalari
- `ServerUrl` doim HTTPS bo'lishi kerak; haqiqiy muhitda to'g'ri CA tomonidan imzolangan sertifikat ishlating.
- `ENROLLMENT_TOKEN`ni faqat o'rnatish vaqtida ishlating va keyin maxfiy saqlang/almashtiring (bir martalik ro'yxatdan o'tish uchun kifoya).
- Har bir qurilma registratsiyadan keyin o'ziga xos `deviceId` + `deviceSecret` oladi; keyingi barcha so'rovlar shu orqali autentifikatsiya qilinadi.
- PostgreSQL uchun muntazam backup (`pg_dump`) sozlang; `pgdata` volume'ini ham saqlang.
