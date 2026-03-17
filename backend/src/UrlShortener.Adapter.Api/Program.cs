using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Serilog;
using UrlShortener.Adapter.BackingServices;
using UrlShortener.Adapter.Worker.Startup;
using UrlShortener.Application;

// Bootstrap logger for startup-time errors before full Serilog configuration loads
Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
{
    Log.Fatal((Exception)eventArgs.ExceptionObject, "Unhandled exception");
    Log.CloseAndFlush();
};

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, services, configuration) =>
    configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services.AddApplication();
builder.Services.AddBackingServices(builder.Configuration);

builder.Services.AddControllers();

string sqlConnection = builder.Configuration.GetConnectionString("DefaultConnection")!;
string redisConnection = builder.Configuration.GetConnectionString("Redis")!;

builder.Services
    .AddHealthChecks()
        .AddSqlServer(sqlConnection, name: "sql-server", tags: ["db"])
        .AddRedis(redisConnection, name: "redis", tags: ["cache"]);

var application = builder.Build();

application
    .UseHttpsRedirection()
    .UseSerilogRequestLogging()
    .UseAuthorization();

application.MapControllers();

application.MapHealthChecks("/health", new HealthCheckOptions
{
    ResponseWriter = async (context, report) =>
    {
        context.Response.ContentType = "application/json";
        var json = System.Text.Json.JsonSerializer.Serialize(new
        {
            status = report.Status.ToString(),
            checks = report.Entries.Select(e => new
            {
                name = e.Key,
                status = e.Value.Status.ToString(),
                description = e.Value.Description
            })
        });
        await context.Response.WriteAsync(json);
    }
});

application.Run();

Log.CloseAndFlush();
