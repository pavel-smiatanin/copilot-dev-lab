using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.OpenApi.Models;
using Serilog;
using UrlShortener.Adapter.Api.Middleware;
using UrlShortener.Adapter.Api.Startup;

const string _defaultCorsPolicy = "DefaultCorsPolicy";

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

builder.Services.AddApplication()
    .AddBackingServices(builder.Configuration)
    .AddExceptionHandler<GlobalExceptionHandler>()
    .AddProblemDetails();

builder.Services.AddCors(options =>
{
    options.AddPolicy(_defaultCorsPolicy,
        policy =>
        {
            policy.AllowAnyOrigin()
                .AllowAnyHeader()
                .AllowAnyMethod();
        });
});

builder.Services.AddControllers();

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "URL Shortener API",
        Version = "v1",
        Description = "A lightweight URL shortener REST API."
    });
});

string sqlConnection = builder.Configuration.GetConnectionString("DefaultConnection")!;
string redisConnection = builder.Configuration.GetConnectionString("Redis")!;

builder.Services
    .AddHealthChecks()
        .AddSqlServer(sqlConnection, name: "sql-server", tags: ["db"])
        .AddRedis(redisConnection, name: "redis", tags: ["cache"]);

var application = builder.Build();

application
    .UseExceptionHandler()
    .UseHttpsRedirection()
    .UseCors(_defaultCorsPolicy)
    .UseSwagger()
    .UseSwaggerUI(options =>
        {
            options.SwaggerEndpoint("/swagger/v1/swagger.json", "URL Shortener API v1");
            options.RoutePrefix = "swagger";
        })   
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
