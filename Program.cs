using Microsoft.EntityFrameworkCore;
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

builder.Services.AddControllers();

builder.Services.AddCors(options =>
{
    options.AddPolicy("flutter", policy =>
    {
        policy
            .AllowAnyHeader()
            .AllowAnyMethod()
            .AllowCredentials()
            .SetIsOriginAllowed(_ => true);
    });
});

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));

builder.Services.AddScoped<IRolesRepository, RolesRepository>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseCors("flutter");

app.UseHttpsRedirection();
app.UseAuthorization();

app.MapControllers();
app.MapHub<MarketHub>("/hubs/market");

app.Run();