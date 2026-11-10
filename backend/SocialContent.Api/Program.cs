using System.Reflection;
using System.Text;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi.Models;
using SocialContent.Api.Data;
using SocialContent.Api.Middleware;
using SocialContent.Api.Services;
using SocialContent.Api.Swagger;

var builder = WebApplication.CreateBuilder(args);

// ---------- Konfiguracja JWT ----------
var jwt = builder.Configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>() ?? new JwtOptions();
if (Encoding.UTF8.GetByteCount(jwt.Key ?? "") < 32)
    throw new InvalidOperationException(
        "Konfiguracja 'Jwt:Key' musi mieć co najmniej 32 bajty. Ustaw ją w appsettings.json lub zmienną środowiskową Jwt__Key.");
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection(JwtOptions.SectionName));

// ---------- Dane i serwisy ----------
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IManagerUserService, ManagerUserService>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ISocialAccountService, SocialAccountService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IClientUserService, ClientUserService>();
builder.Services.AddScoped<IMediaService, MediaService>();
builder.Services.AddScoped<IPostEditLockService, PostEditLockService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddScoped<IPostPublisher, PostPublisher>();
// Przesłane pliki leżą w folderze "uploads" obok aplikacji (podfolder na firmę).
builder.Services.AddSingleton<IMediaStorage>(
    new MediaStorage(Path.Combine(builder.Environment.ContentRootPath, "uploads")));
builder.Services.AddHostedService<ScheduledPostWorker>();
builder.Services.AddAutoMapper(typeof(Program).Assembly);
builder.Services.AddSingleton<IPasswordHasher, Pbkdf2PasswordHasher>();
builder.Services.AddSingleton<ITokenService, JwtTokenService>();

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

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    DatabaseInitializer.Initialize(db, app.Logger);
    // Dane demo tylko lokalnie (dotnet run domyślnie działa jako Development).
    if (app.Environment.IsDevelopment())
        DatabaseSeeder.Seed(db, scope.ServiceProvider.GetRequiredService<IPasswordHasher>(), app.Logger);
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();


