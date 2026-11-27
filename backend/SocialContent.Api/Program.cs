using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SocialContent.Api.Data;
using SocialContent.Api.Hubs;
using SocialContent.Api.Middleware;
using SocialContent.Api.Services;
using SocialContent.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

// ---------- Konfiguracja JWT ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key ?? "") < 32)
    throw new InvalidOperationException(
        "Konfiguracja 'Jwt:Key' musi mieć co najmniej 32 bajty. Ustaw ją w appsettings.json lub zmienną środowiskową Jwt__Key.");
if (!builder.Environment.IsDevelopment() && jwt.Key!.StartsWith("DEV-ONLY", StringComparison.Ordinal))
    throw new InvalidOperationException(
        "Poza środowiskiem Development nie wolno używać deweloperskiego klucza JWT. Ustaw własny 'Jwt:Key' (np. zmienna Jwt__Key).");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// ---------- Bezpieczeństwo przesyłu (sekcja "Security" w appsettings) ----------
// RequireHttps: przekierowanie HTTP -> HTTPS + HSTS. Wymaga skonfigurowanego punktu HTTPS (Kestrel:Endpoints:Https albo reverse proxy).
// AllowedOrigins: lista adresów, z których przeglądarka może wołać API (CORS z poświadczeniami nie dopuszcza '*').
var requireHttps = builder.Configuration.GetValue<bool>("Security:RequireHttps");
var allowedOrigins = builder.Configuration.GetSection("Security:AllowedOrigins").Get<string[]>() ?? Array.Empty<string>();
builder.Services.AddHttpsRedirection(o => o.HttpsPort = builder.Configuration.GetValue("Security:HttpsPort", 443));

// ---------- Dane i serwisy ----------
// Interceptor wysyła powiadomienia (SignalR) po każdym udanym zapisie, więc działa dla wszystkich miejsc, które je tworzą.
builder.Services.AddSingleton<NotificationPushInterceptor>();
builder.Services.AddDbContext<AppDbContext>((sp, o) =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default"))
        .AddInterceptors(sp.GetRequiredService<NotificationPushInterceptor>()));

builder.Services.AddScoped<IManagerUserService, ManagerUserService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ISocialAccountService, SocialAccountService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IClientUserService, ClientUserService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IPostEditLockService, PostEditLockService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPostPublisher, PostPublisher>();
builder.Services.AddScoped<IChatService, ChatService>();
builder.Services.AddSingleton<IRealtimeNotifier, SignalRRealtimeNotifier>();
// SignalR jest częścią platformy ASP.NET Core (bez dodatkowego pakietu NuGet). Enumy jako tekst, jak w REST.
builder.Services.AddSignalR()
    .AddJsonProtocol(o => o.PayloadSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
// Przesłane pliki leżą w folderze "uploads" obok aplikacji (podfolder na firmę).
builder.Services.AddSingleton<IMediaStorage>(
    new MediaStorage(Path.Combine(builder.Environment.ContentRootPath, "uploads")));
builder.Services.AddHostedService<ScheduledPostWorker>();
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddScoped<ITwoFactorService, TwoFactorService>();
// Deweloperska wysyłka e-mail: treść (z kodem 2FA) tylko do logu. Na produkcji podmień na implementację SMTP.
builder.Services.AddSingleton<IEmailSender, LoggingEmailSender>();

// Limit żądań na adres IP dla logowania i weryfikacji kodu (ochrona przed zgadywaniem haseł i kodów).
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("auth", http => RateLimitPartition.GetFixedWindowLimiter(
        http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions { PermitLimit = 20, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
});

// ---------- Uwierzytelnianie ----------
builder.Services
    .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.MapInboundClaims = false; // claimy zostają pod nazwami z tokenu: "sub", "role"
        o.TokenValidationParameters = new TokenValidationParameters
        {
            NameClaimType = "sub",
            RoleClaimType = "role",
            ValidateIssuer = true,
            ValidIssuer = jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Key!)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1)
        };

        // Token jest ważny tylko dopóki konto istnieje i nie jest zablokowane
        // (blokada i anonimizacja działają od razu, bez czekania na wygaśnięcie tokenu).
        o.Events = new JwtBearerEvents
        {
            // Odtwarzacze wideo/audio i Image.network nie zawsze potrafią wysłać nagłówek Authorization, więc dla
            // pobierania plików (GET /api/Media/...) dopuszczamy token w query (?access_token=). Reszta API tego nie robi,
            // a token nadal jest w pełni walidowany (podpis, ważność, OnTokenValidated poniżej).
            OnMessageReceived = context =>
            {
                // Hub SignalR: WebSocket i długie odpytywanie nie wysyłają nagłówka Authorization, więc token idzie w ?access_token=
                // (dla każdej metody HTTP używanej przez transport, ale tylko pod ścieżką huba).
                if (context.Request.Path.StartsWithSegments("/hubs/chat")
                    && context.Request.Query.TryGetValue("access_token", out var hubToken)
                    && !string.IsNullOrWhiteSpace(hubToken))
                {
                    context.Token = hubToken;
                    return Task.CompletedTask;
                }

                if (context.Request.Method == HttpMethods.Get
                    && context.Request.Path.StartsWithSegments("/api/Media")
                    && context.Request.Query.TryGetValue("access_token", out var token)
                    && !string.IsNullOrWhiteSpace(token))
                    context.Token = token;
                return Task.CompletedTask;
            },
            OnTokenValidated = async context =>
            {
                CurrentUser user;
                try
                {
                    user = context.Principal!.ToCurrentUser();
                }
                catch (Exception)
                {
                    context.Fail("Nieprawidłowy token.");
                    return;
                }

                var auth = context.HttpContext.RequestServices.GetRequiredService<IAuthService>();
                if (!await auth.IsSessionValidAsync(user, context.HttpContext.RequestAborted))
                    context.Fail("Konto nie istnieje lub jest zablokowane.");
            }
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

// ---------- Swagger / OpenAPI ----------
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(o =>
{
    o.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Social Content API",
        Version = "v1",
        Description = "API do zarządzania tworzeniem treści do mediów społecznościowych przez content managerów: " +
                      "firmy (CompanyUser), managerowie, klienci, konta społecznościowe, posty (ogólne oraz Facebook, Instagram, TikTok) " +
                      "oraz rejestracja i logowanie (JWT) z rolami."
    });

    // Opisy endpointów i modeli pochodzą z komentarzy XML (<summary>, <remarks>, <response>).
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    var xmlPath = Path.Combine(AppContext.BaseDirectory, xmlFile);
    if (File.Exists(xmlPath)) o.IncludeXmlComments(xmlPath, includeControllerXmlComments: true);

    o.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Wklej token JWT otrzymany z /api/Auth/login, /api/Auth/register-company lub /api/Auth/register-client (bez słowa Bearer)."
    });
    o.OperationFilter<AuthorizeOperationFilter>();
});

// CORS dotyczy tylko przeglądarek (Flutter web); aplikacje natywne go nie używają. AllowCredentials jest potrzebne dla SignalR,
// więc nigdy nie łączymy go z dowolnym originem: tylko lista z "Security:AllowedOrigins", a w Development dodatkowo localhost.
builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
{
    p.AllowAnyHeader().AllowAnyMethod().AllowCredentials();
    p.SetIsOriginAllowed(origin =>
    {
        if (allowedOrigins.Contains(origin, StringComparer.OrdinalIgnoreCase)) return true;
        return builder.Environment.IsDevelopment()
               && Uri.TryCreate(origin, UriKind.Absolute, out var uri)
               && (uri.Host == "localhost" || uri.Host == "127.0.0.1");
    });
}));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DatabaseInitializer.Initialize(db, app.Logger);
    // Dane demo tylko lokalnie: środowisko Development ustawia Properties/launchSettings.json (dotnet run).
    if (app.Environment.IsDevelopment())
        DatabaseSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), app.Logger);
}

// Za reverse proxy (terminacja TLS) adres klienta i schemat pochodzą z nagłówków X-Forwarded-*.
app.UseForwardedHeaders(new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
});
if (requireHttps)
{
    app.UseHsts();
    app.UseHttpsRedirection();
}

// Nagłówki bezpieczeństwa. Referrer-Policy i no-store chronią tokeny w ?access_token= (Media, hub) przed wyciekiem
// przez nagłówek Referer i pamięć podręczną. CSP pomijamy dla Swaggera, bo korzysta z własnych skryptów.
app.Use(async (context, next) =>
{
    var headers = context.Response.Headers;
    headers["X-Content-Type-Options"] = "nosniff";
    headers["X-Frame-Options"] = "DENY";
    headers["Referrer-Policy"] = "no-referrer";
    headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    if (!context.Request.Path.StartsWithSegments("/swagger"))
        headers["Content-Security-Policy"] = "default-src 'none'; frame-ancestors 'none'";
    if (context.Request.Path.StartsWithSegments("/api/Auth") || context.Request.Query.ContainsKey("access_token"))
        headers["Cache-Control"] = "no-store";
    await next();
});

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.MapControllers();
app.MapHub<ChatHub>("/hubs/chat");
app.Run();
