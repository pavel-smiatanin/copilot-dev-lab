using Serilog;
using UrlShortener.Adapter.Worker.Startup;
using UrlShortener.Adapter.Worker.Workers;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

AppDomain.CurrentDomain.UnhandledException += (sender, eventArgs) =>
{
    Log.Fatal((Exception)eventArgs.ExceptionObject, "Unhandled exception");
    Log.CloseAndFlush();
};

HostApplicationBuilder builder = Host.CreateApplicationBuilder(args);

builder.Services.AddSerilog((services, lc) =>
    lc.ReadFrom.Configuration(builder.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

builder.Services.AddBackingServices(builder.Configuration);

builder.Services.AddHostedService<Worker>();

IHost host = builder.Build();
host.Run();

Log.CloseAndFlush();

