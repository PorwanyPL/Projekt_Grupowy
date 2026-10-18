using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using SocialContent.Api.Data;
using SocialContent.Api.Middleware;
using SocialContent.Api.Services;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseSqlite(builder.Configuration.GetConnectionString("Default")));

builder.Services.AddScoped<IManagerUserService, ManagerUserService>();
builder.Services.AddScoped<IPostService, PostService>();

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddCors(o => o.AddDefaultPolicy(p =>
    p.AllowAnyOrigin().AllowAnyHeader().AllowAnyMethod()));

var app = builder.Build();

using (var scope = app.Services.CreateScope())
{
    scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.EnsureCreated();
}

app.UseMiddleware<ExceptionMiddleware>();
app.UseCors();
app.UseSwagger();
app.UseSwaggerUI();
app.MapControllers();
app.Run();
