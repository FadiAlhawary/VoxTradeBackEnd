using Microsoft.EntityFrameworkCore;
using VoxTrade.Api.Data;
using VoxTrade.Services.Implementation;
using VoxTrade.Services.Interface;

var builder = WebApplication.CreateBuilder(args);


// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring Swagger/OpenAPI at https://aka.ms/aspnetcore/swashbuckle
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

builder.Services.AddDbContext<TradingDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Postgres")));
var cs = builder.Configuration.GetConnectionString("Postgres");
Console.WriteLine(cs);
builder.Services.AddScoped<IRolesRepository, RolesRepository>();
builder.Services.AddScoped<IUIThemesRepository ,UIThemesRepository>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
