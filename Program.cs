using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Services.Implementation;
using VoxTrade.Services.Interface;
using VoxTrade.Services;

var builder = WebApplication.CreateBuilder(args);

// =====================
// SERVICES
// =====================

builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IRolesRepository, RolesRepository>();
builder.Services.AddScoped<IUIThemesRepository, UIThemesRepository>();

// ✅ Add SignalR
builder.Services.AddSignalR();

// ✅ Add Finnhub WebSocket Background Service
builder.Services.AddHostedService<VoxTradeBackEnd.Services.FinnhubWebSocketService>();

// ✅ (Recommended) Add CORS if frontend is separate
builder.Services.AddCors(options =>
{
    options.AddPolicy("AllowAll",
        policy =>
        {
            policy.AllowAnyHeader()
                  .AllowAnyMethod()
                  .AllowCredentials()
                  .SetIsOriginAllowed(_ => true);
        });
});

var app = builder.Build();

// =====================
// PIPELINE
// =====================

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

// Must come before MapHub if using cross-origin frontend
app.UseCors("AllowAll");

app.UseAuthorization();

app.MapControllers();

// ✅ Map SignalR Hub
app.MapHub<VoxTradeBackEnd.hubs.MarketHub>("/marketHub");

app.Run();