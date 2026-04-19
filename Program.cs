using System.Text;
using Microsoft.AspNetCore.Connections;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using VoxTrade.Api.Data;
using VoxTrade.MarketHubs;
using VoxTrade.Services.Implementation;
using VoxTrade.Services.Interface;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSignalR()
    .AddJsonProtocol(options =>
    {
        options.PayloadSerializerOptions.PropertyNamingPolicy =
            System.Text.Json.JsonNamingPolicy.CamelCase;
    });

builder.Services.AddSingleton<FinnhubWebSocketService>();
builder.Services.AddSingleton<MarketSubscriptionService>();
builder.Services.AddHostedService(sp => sp.GetRequiredService<FinnhubWebSocketService>());
builder.Services.AddScoped<VoxTrade.Data.IDbConnectionFactory, VoxTrade.Data.DbConnectionFactory>();
builder.Services.AddScoped<IInstrumentRepository, InstrumentRepository>();

builder.Services.AddControllers();
builder.Services.AddMemoryCache();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IRolesRepository, RolesRepository>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IUserRepository, UserRepository>();
builder.Services.AddScoped<IHistoryRepository, HistoryRepository>();

// Add JWT Authentication
var jwtKey = builder.Configuration["Jwt:Key"] ?? "your-256-bit-secret-key-that-is-very-long-and-secure";
var jwtIssuer = builder.Configuration["Jwt:Issuer"] ?? "VoxTrade";
var jwtAudience = builder.Configuration["Jwt:Audience"] ?? "VoxTrade";

builder.Services.AddAuthentication("Bearer")
    .AddJwtBearer("Bearer", options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = jwtIssuer,
            ValidAudience = jwtAudience,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtKey))
        };
    });

builder.Services.AddAuthorization();

// Add CORS
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowFrontend", policy =>
    {
        policy
            .SetIsOriginAllowed(_ => true)
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials();
    });
});
var app = builder.Build();

app.UseCors("AllowFrontend");

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}


app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MarketHub>("/hubs/market");


app.Run();