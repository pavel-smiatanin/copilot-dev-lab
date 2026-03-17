using Microsoft.EntityFrameworkCore;
using UrlShortener.Adapter.BackingServices.Persistence;
using UrlShortener.Application.Abstract.Secondary;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services
    .AddDbContext<MsSqlAppDbContext>(options =>
        options.UseSqlServer(
            builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services
    .AddTransient<AppDbContext>(provider => provider.GetRequiredService<MsSqlAppDbContext>());

builder.Services.AddControllers();

var app = builder.Build();

// Configure the HTTP request pipeline.

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
