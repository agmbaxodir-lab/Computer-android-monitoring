using System.Text;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.RateLimiting;
using FileMonitoring.Api.Data;
using FileMonitoring.Api.Domain;
using FileMonitoring.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using Serilog;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration).WriteTo.Console());

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseNpgsql(builder.Configuration.GetConnectionString("Default")).UseSnakeCaseNamingConvention());
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = "File Monitoring API", Version = "v1" });
    var scheme = new OpenApiSecurityScheme { Name = "Authorization", Type = SecuritySchemeType.Http, Scheme = "bearer",
        BearerFormat = "JWT", In = ParameterLocation.Header, Description = "Bearer {token}" };
    c.AddSecurityDefinition("Bearer", scheme);
    c.AddSecurityRequirement(new OpenApiSecurityRequirement { { scheme, Array.Empty<string>() } });
});
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
builder.Services.AddScoped<AuditLogger>();
builder.Services.AddScoped<PolicyEngine>();

// CORS: Cors:AllowedOrigins bo'sh yoki "*" bo'lsa istalgan origin (LAN uchun qulay) ruxsat etiladi.
// Cheklash uchun: Cors__AllowedOrigins__0=http://192.168.1.31:5173
var corsOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
builder.Services.AddCors(options =>
{
    options.AddPolicy("admin", policy =>
    {
        if (corsOrigins.Length == 0 || corsOrigins.Contains("*")) policy.SetIsOriginAllowed(_ => true);
        else policy.WithOrigins(corsOrigins);
        policy.AllowAnyHeader().AllowAnyMethod();
    });
});

var secret = builder.Configuration["Jwt:Secret"];
if (string.IsNullOrWhiteSpace(secret) || secret.Length < 32)
    throw new InvalidOperationException("Jwt:Secret must be set (>= 32 chars).");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer(o =>
    o.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = false, ValidateAudience = false, ValidateLifetime = true,
        ValidateIssuerSigningKey = true, ClockSkew = TimeSpan.FromSeconds(30),
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret))
    });
builder.Services.AddAuthorization();
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = 429;
    // Har bir IP / har bir qurilma uchun alohida limit (avval hamma uchun umumiy edi: 120/min butun tizimga).
    o.AddPolicy("login", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 10, Window = TimeSpan.FromMinutes(1) }));
    o.AddPolicy("agent", ctx =>
    {
        var key = ctx.Request.Headers["X-Device-Id"].ToString();
        if (key.Length == 0) key = ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown";
        return RateLimitPartition.GetFixedWindowLimiter(key,
            _ => new FixedWindowRateLimiterOptions { PermitLimit = 300, Window = TimeSpan.FromMinutes(1) });
    });
});

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (db.Database.IsNpgsql())
    {
        // database/migrations/002 qo'llanmagan eski bazalarda "column platform does not exist" xatosi hodisalarni
        // saqlashga yo'l qo'ymasdi. Quyidagi buyruqlar idempotent: bor narsaga tegmaydi.
        try { await db.Database.ExecuteSqlRawAsync(SchemaFix.Sql); }
        catch (Exception ex) { app.Logger.LogError(ex, "Schema tekshiruvi bajarilmadi (database/migrations ni qo'lda qo'llang)"); }
    }
    var hasher = scope.ServiceProvider.GetRequiredService<IPasswordHasher<User>>();
    var name = builder.Configuration["Admin:Username"]; var pass = builder.Configuration["Admin:Password"];
    if (!string.IsNullOrEmpty(name) && !string.IsNullOrEmpty(pass) && !await db.Users.AnyAsync(u => u.Username == name))
    {
        var u = new User { Id = Guid.NewGuid(), Username = name, Role = "Admin" };
        u.PasswordHash = hasher.HashPassword(u, pass);
        db.Users.Add(u); await db.SaveChangesAsync();
    }
}

app.UseSerilogRequestLogging();
app.UseSwagger(); app.UseSwaggerUI();
app.UseCors("admin");
app.UseRateLimiter();
app.UseAuthentication(); app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program { }

static class SchemaFix
{
    // Diqqat: ExecuteSqlRaw ichida figurali qavs {} ishlatmang (format placeholder deb o'qiladi) — shuning uchun ARRAY[...].
    public const string Sql = @"
ALTER TABLE devices ADD COLUMN IF NOT EXISTS platform text NOT NULL DEFAULT 'Windows';
ALTER TABLE devices ADD COLUMN IF NOT EXISTS device_model text;
ALTER TABLE file_events ADD COLUMN IF NOT EXISTS platform text NOT NULL DEFAULT 'Windows';
ALTER TABLE file_events ADD COLUMN IF NOT EXISTS file_path text;
CREATE INDEX IF NOT EXISTS ix_fe_platform ON file_events(platform);
CREATE INDEX IF NOT EXISTS ix_fe_event_type ON file_events(event_type);
INSERT INTO applications(name, process_names)
SELECT v.n, v.p FROM (VALUES
  ('Telegram', ARRAY['Telegram.exe']),
  ('WhatsApp', ARRAY['WhatsApp.exe','WhatsApp.Root.exe']),
  ('imo', ARRAY['imo.exe']),
  ('Microsoft Teams', ARRAY['ms-teams.exe','Teams.exe']),
  ('Discord', ARRAY['Discord.exe'])
) AS v(n, p)
WHERE NOT EXISTS (SELECT 1 FROM applications);
";
} // integration testlar uchun (WebApplicationFactory)
