using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Serilog.Sinks.OpenTelemetry;
using Wms.BuildingBlocks;
using Wms.Inbound;
using Wms.Integration;
using Wms.Layout;
using Wms.MasterData;
using Wms.Outbound;
using Wms.TaskExecution;
using Wms.Tenancy;
using Wms.Worker;

var builder = Host.CreateApplicationBuilder(args);
Log.Logger = new LoggerConfiguration()
    .Enrich.FromLogContext()
    .WriteTo.Console()
    .WriteTo.OpenTelemetry(options =>
    {
        options.Endpoint = builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"] ?? "http://localhost:4317";
        options.Protocol = OtlpProtocol.Grpc;
        options.ResourceAttributes = new Dictionary<string, object>
        {
            ["service.name"] = "wms-worker",
            ["deployment.environment"] = builder.Environment.EnvironmentName
        };
    })
    .CreateLogger();
builder.Services.AddSerilog();
var connectionString = builder.Configuration.GetConnectionString("Wms") ?? "Host=localhost;Port=5432;Database=wms;Username=wms_worker;Password=wms_dev";
builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddDbContext<IntegrationDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<InboundDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<OutboundDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<LayoutDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<MasterDataDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddDbContext<TaskExecutionDbContext>(o => o.UseNpgsql(connectionString));
builder.Services.AddScoped<IInboundImporter, InboundImporter>();
builder.Services.AddScoped<IOutboundImporter, OutboundImporter>();
builder.Services.AddScoped<IImportTaskPlanner, ImportTaskPlanner>();
builder.Services.AddHttpClient();
builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("wms-worker"))
    .WithTracing(t => t.AddSource(WmsTelemetry.WorkerActivitySourceName).AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddMeter(WmsTelemetry.WorkerMeterName).AddHttpClientInstrumentation().AddOtlpExporter());
builder.Services.AddHostedService<OutboxWorker>();
await builder.Build().RunAsync();
