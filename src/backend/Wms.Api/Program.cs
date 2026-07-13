using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;
using Serilog;
using Wms.Api;
using Wms.BuildingBlocks;
using Wms.Inbound;
using Wms.Integration;
using Wms.Inventory;
using Wms.Layout;
using Wms.MasterData;
using Wms.MobileSync;
using Wms.Outbound;
using Wms.SecurityAudit;
using Wms.TaskExecution;
using Wms.Tenancy;

var builder = WebApplication.CreateBuilder(args);
builder.Host.UseSerilog((context, configuration) => configuration.ReadFrom.Configuration(context.Configuration).Enrich.FromLogContext().WriteTo.Console());
var connectionString = builder.Configuration.GetConnectionString("Wms") ?? "Host=localhost;Port=5432;Database=wms;Username=wms_app;Password=wms_dev";

builder.Services.AddScoped<TenantContext>();
builder.Services.AddScoped<ITenantContext>(sp => sp.GetRequiredService<TenantContext>());
builder.Services.AddScoped<TenantRlsConnectionInterceptor>();
builder.Services.AddDbContext<TenancyDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<SecurityAuditDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<LayoutDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<MasterDataDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<InventoryDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<InboundDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<OutboundDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<TaskExecutionDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<IntegrationDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddDbContext<MobileSyncDbContext>((sp, o) => ConfigureTenantDatabase(o, sp, connectionString));
builder.Services.AddScoped<IAuditWriter, AuditWriter>();
builder.Services.AddScoped<IInventoryCommands, InventoryService>();
builder.Services.AddScoped<IInboundImporter, InboundImporter>();
builder.Services.AddScoped<IOutboundImporter, OutboundImporter>();
builder.Services.AddScoped<IOfflineTaskExecutor, OfflineTaskExecutor>();
builder.Services.AddScoped<IOfflineTaskCommandHandler, PhysicalTaskCommandHandler>();
builder.Services.AddScoped<IIntegrationIngestionService, IntegrationIngestionService>();
builder.Services.AddScoped<IMobileCommandProcessor, MobileCommandProcessor>();
builder.Services.AddScoped<IOutboundOperations, OutboundOperationsService>();
builder.Services.ConfigureHttpJsonOptions(options => options.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

builder.Services.AddAuthentication(options =>
{
    options.DefaultScheme = "smart";
    options.DefaultChallengeScheme = "smart";
}).AddPolicyScheme("smart", "Bearer or development headers", options =>
{
    options.ForwardDefaultSelector = context => context.Request.Headers.ContainsKey("Authorization") ? JwtBearerDefaults.AuthenticationScheme : "dev-headers";
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Authentication:Authority"];
    options.Audience = builder.Configuration["Authentication:Audience"] ?? "wms-api";
    options.RequireHttpsMetadata = !builder.Environment.IsDevelopment();
}).AddScheme<AuthenticationSchemeOptions, DevelopmentHeaderAuthenticationHandler>("dev-headers", _ => { });

builder.Services.AddAuthorization(options =>
{
    static bool HasScope(ClaimsPrincipal user, string scope) => user.FindAll("scope").SelectMany(x => x.Value.Split(' ')).Contains(scope, StringComparer.Ordinal);
    options.AddPolicy("integration.ingest", p => p.RequireAssertion(c => HasScope(c.User, "wms.integration.ingest")));
    options.AddPolicy("integration.read", p => p.RequireAssertion(c => HasScope(c.User, "wms.integration.read")));
    options.AddPolicy("mobile.read", p => p.RequireAssertion(c => HasScope(c.User, "wms.task.read_assigned")));
    options.AddPolicy("mobile.execute", p => p.RequireAssertion(c => HasScope(c.User, "wms.task.execute")));
    options.AddPolicy("inventory.read", p => p.RequireAssertion(c => HasScope(c.User, "wms.inventory.read")));
    options.AddPolicy("supervisor.read", p => p.RequireAssertion(c => HasScope(c.User, "wms.supervisor.read")));
    options.AddPolicy("outbound.release", p => p.RequireAssertion(c => HasScope(c.User, "wms.outbound.release")));
    options.AddPolicy("outbound.pack", p => p.RequireAssertion(c => HasScope(c.User, "wms.outbound.pack")));
    options.AddPolicy("outbound.dispatch", p => p.RequireAssertion(c => HasScope(c.User, "wms.outbound.dispatch")));
    options.AddPolicy("outbound.read", p => p.RequireAssertion(c => HasScope(c.User, "wms.outbound.read")));
    options.AddPolicy("inbound.read", p => p.RequireAssertion(c => HasScope(c.User, "wms.inbound.read")));
});
builder.Services.AddProblemDetails();
builder.Services.AddHealthChecks().AddCheck("self", () => Microsoft.Extensions.Diagnostics.HealthChecks.HealthCheckResult.Healthy(), tags: ["ready"]);
builder.Services.AddOpenTelemetry().ConfigureResource(r => r.AddService("wms-api"))
    .WithTracing(t => t.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter())
    .WithMetrics(m => m.AddAspNetCoreInstrumentation().AddHttpClientInstrumentation().AddOtlpExporter());

var app = builder.Build();
app.UseSerilogRequestLogging();
app.UseExceptionHandler(errorApp => errorApp.Run(async context =>
{
    var error = context.Features.Get<IExceptionHandlerFeature>()?.Error;
    var wms = error as WmsProblemException;
    var status = wms?.StatusCode ?? 500;
    var correlationId = context.Items["CorrelationId"]?.ToString() ?? Guid.NewGuid().ToString();
    context.Response.StatusCode = status;
    await context.Response.WriteAsJsonAsync(new ProblemDetails
    {
        Type = $"https://wms.local/problems/{(wms?.Code ?? "INTERNAL_ERROR").ToLowerInvariant()}",
        Title = status == 500 ? "Unexpected server error" : wms!.Message,
        Status = status,
        Extensions = { ["code"] = wms?.Code ?? "INTERNAL_ERROR", ["correlationId"] = correlationId, ["errors"] = wms?.Errors }
    });
}));
app.UseAuthentication();
app.Use(async (context, next) =>
{
    if (context.User.Identity?.IsAuthenticated == true)
    {
        var tenantValue = context.User.FindFirstValue("tenant_id");
        if (!Guid.TryParse(tenantValue, out var tenantId)) throw new WmsProblemException("TENANT_REQUIRED", "Authenticated token has no valid tenant.", 401);
        var correlation = context.Request.Headers.TryGetValue("X-Correlation-Id", out var value) && Guid.TryParse(value, out var parsed) ? parsed : Guid.NewGuid();
        context.Items["CorrelationId"] = correlation;
        context.Response.Headers["X-Correlation-Id"] = correlation.ToString();
        context.RequestServices.GetRequiredService<TenantContext>().Resolve(tenantId, context.User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "unknown", correlation, context.User.FindFirstValue("warehouse"));
    }
    await next();
});
app.UseAuthorization();

app.MapHealthChecks("/health/live", new() { Predicate = _ => false });
app.MapHealthChecks("/health/ready", new() { Predicate = r => r.Tags.Contains("ready") });

app.MapPost("/api/v1/integration/asns", async (CanonicalEnvelope<AdvanceShippingNotice> envelope, IIntegrationIngestionService service, CancellationToken ct) =>
{
    ValidateAsn(envelope.Payload);
    var result = await service.AcceptAsync(envelope, "AdvanceShippingNotice", ct);
    return result.AlreadyProcessed ? Results.Ok(result) : Results.Accepted($"/api/v1/integration/messages/{result.MessageId}", result);
}).RequireAuthorization("integration.ingest");

app.MapPost("/api/v1/integration/sales-orders", async (CanonicalEnvelope<SalesOrder> envelope, IIntegrationIngestionService service, CancellationToken ct) =>
{
    ValidateOrder(envelope.Payload);
    var result = await service.AcceptAsync(envelope, "SalesOrder", ct);
    return result.AlreadyProcessed ? Results.Ok(result) : Results.Accepted($"/api/v1/integration/messages/{result.MessageId}", result);
}).RequireAuthorization("integration.ingest");

app.MapGet("/api/v1/integration/messages/{messageId:guid}", async (Guid messageId, IntegrationDbContext db, CancellationToken ct) =>
{
    var inbox = await db.Inbox.SingleOrDefaultAsync(x => x.MessageId == messageId, ct) ?? throw new WmsProblemException("NOT_FOUND", "Message was not found.", 404);
    var outbox = await db.Outbox.OrderByDescending(x => x.CreatedAt).FirstOrDefaultAsync(x => x.CausationId == messageId, ct);
    return Results.Ok(new IntegrationMessageDto(messageId, inbox.MessageType, outbox?.Status ?? inbox.Status, outbox?.Attempts ?? 0, outbox?.LastErrorCode ?? inbox.ErrorCode, inbox.CorrelationId, inbox.CreatedAt, outbox?.DeliveredAt));
}).RequireAuthorization("integration.read");

app.MapGet("/api/v1/mobile/bootstrap", async (HttpContext http, MobileSyncDbContext db, IConfiguration config, ITenantContext tenant, CancellationToken ct) =>
{
    var deviceId = http.Request.Headers["X-Device-Id"].FirstOrDefault() ?? throw new WmsProblemException("DEVICE_REQUIRED", "X-Device-Id is required.");
    var warehouseId = Guid.TryParse(http.Request.Headers["X-Warehouse-Id"].FirstOrDefault(), out var parsed) ? parsed : Guid.Parse(config["Pilot:WarehouseId"] ?? "22222222-2222-2222-2222-222222222222");
    var checkpoint = await db.Checkpoints.Where(x => x.DeviceId == deviceId && x.UserId == tenant.ActorId).Select(x => (long?)x.Value).SingleOrDefaultAsync(ct) ?? 0;
    return Results.Ok(new MobileBootstrapDto(tenant.TenantId, warehouseId, config["Pilot:WarehouseCode"] ?? "WH01", deviceId, tenant.ActorId, checkpoint, DateTimeOffset.UtcNow, http.User.FindAll("scope").Select(x => x.Value).ToArray()));
}).RequireAuthorization("mobile.read");

app.MapGet("/api/v1/mobile/tasks", async (HttpContext http, long? since, TaskExecutionDbContext db, ITenantContext tenant, CancellationToken ct) =>
{
    var device = http.Request.Headers["X-Device-Id"].FirstOrDefault();
    var checkpoint = since.GetValueOrDefault();
    var tasks = await db.Tasks.AsNoTracking().Include(x => x.Steps)
        .Where(x => x.AssigneeId == tenant.ActorId && x.DeviceId == device && x.Version > checkpoint)
        .OrderBy(x => x.Priority).ThenBy(x => x.CreatedAt).ToListAsync(ct);
    var items = tasks.Select(x =>
    {
        var steps = x.Steps.OrderBy(s => s.Sequence).ToArray();
        return new
        {
            taskId = x.Id,
            tenantId = x.TenantId,
            warehouseId = x.WarehouseId,
            type = x.Type,
            status = x.Status,
            entityVersion = x.Version,
            title = $"{x.Type} · {x.Reference}",
            instruction = string.Join(" · ", steps.Select(s => s.Action)),
            sourceLocation = steps.FirstOrDefault(s => s.Action.Contains("Source", StringComparison.OrdinalIgnoreCase))?.LocationBarcode,
            destinationLocation = steps.FirstOrDefault(s => s.Action.Contains("Destination", StringComparison.OrdinalIgnoreCase))?.LocationBarcode,
            sku = steps.Select(s => s.SkuBarcode).FirstOrDefault(value => !string.IsNullOrWhiteSpace(value)) ?? string.Empty,
            expectedQuantity = steps.Select(s => s.Quantity).DefaultIfEmpty(0).Max(),
            updatedAt = x.UpdatedAt
        };
    }).ToArray();
    return Results.Ok(new { tasks = items, checkpoint = tasks.Select(x => x.Version).DefaultIfEmpty(checkpoint).Max() });
}).RequireAuthorization("mobile.read");

app.MapPost("/api/v1/mobile/commands:batch", async (MobileCommandBatch batch, IMobileCommandProcessor processor, CancellationToken ct) => Results.Ok(new { results = await processor.ProcessBatchAsync(batch, ct) }))
    .RequireAuthorization("mobile.execute");

app.MapGet("/api/v1/inventory/stock", async (Guid? warehouseId, Guid? ownerId, string? sku, Guid? locationId, string? status, InventoryDbContext db, CancellationToken ct) =>
{
    var query = db.StockBalances.AsNoTracking();
    if (warehouseId.HasValue) query = query.Where(x => x.WarehouseId == warehouseId);
    if (ownerId.HasValue) query = query.Where(x => x.OwnerId == ownerId);
    if (!string.IsNullOrWhiteSpace(sku)) query = query.Where(x => x.Sku == sku);
    if (locationId.HasValue) query = query.Where(x => x.LocationId == locationId);
    if (!string.IsNullOrWhiteSpace(status)) query = query.Where(x => x.Status == status);
    return Results.Ok(await query.OrderBy(x => x.Sku).ThenBy(x => x.LocationId).Take(500).Select(x => new StockDto(x.Id, x.WarehouseId, x.OwnerId, x.Sku, x.LocationId, x.Status, x.OnHand, x.Reserved, x.Blocked, x.OnHand - x.Reserved - x.Blocked, x.Version)).ToListAsync(ct));
}).RequireAuthorization("inventory.read");

app.MapPost("/api/v1/outbound/orders/{orderId:guid}/release", async (Guid orderId, ReleaseOrderRequest request, IOutboundOperations operations, CancellationToken ct) =>
    Results.Ok(await operations.ReleaseAsync(orderId, request, ct))).RequireAuthorization("outbound.release");

app.MapPost("/api/v1/outbound/orders/{orderId:guid}/pack", async (Guid orderId, OnlineOrderCommand request, IOutboundOperations operations, CancellationToken ct) =>
    Results.Ok(await operations.PackAsync(orderId, request, ct))).RequireAuthorization("outbound.pack");

app.MapPost("/api/v1/outbound/orders/{orderId:guid}/dispatch", async (Guid orderId, OnlineOrderCommand request, IOutboundOperations operations, CancellationToken ct) =>
    Results.Ok(await operations.DispatchAsync(orderId, request, ct))).RequireAuthorization("outbound.dispatch");

app.MapPost("/api/v1/outbound/orders/{orderId:guid}/short-picks/{lineId:guid}/decision", async (Guid orderId, Guid lineId, ShortPickDecisionRequest request, IOutboundOperations operations, CancellationToken ct) =>
    Results.Ok(await operations.DecideShortPickAsync(orderId, lineId, request, ct))).RequireAuthorization("outbound.release");

app.MapGet("/api/v1/outbound/orders/by-external/{externalId}", async (string externalId, OutboundDbContext db, CancellationToken ct) =>
{
    var order = await db.SalesOrders.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.ExternalId == externalId, ct)
        ?? throw new WmsProblemException("ORDER_NOT_FOUND", "Sales order was not found.", 404);
    return Results.Ok(new { id = order.Id, order.ExternalId, order.Status, entityVersion = order.Version, order.WarehouseCode, order.OwnerCode, lines = order.Lines.Select(x => new { id = x.Id, x.ExternalLineId, x.Sku, x.OrderedQuantity, x.AllocatedQuantity, x.PickedQuantity, x.ShortPickedQuantity, x.ShortPickReason, x.Uom }) });
}).RequireAuthorization("outbound.read");

app.MapGet("/api/v1/inbound/asns/by-external/{externalId}", async (string externalId, InboundDbContext db, CancellationToken ct) =>
{
    var asn = await db.Asns.AsNoTracking().Include(x => x.Lines).SingleOrDefaultAsync(x => x.ExternalId == externalId, ct)
        ?? throw new WmsProblemException("ASN_NOT_FOUND", "ASN was not found.", 404);
    return Results.Ok(new { id = asn.Id, asn.ExternalId, asn.Status, entityVersion = asn.Version, asn.WarehouseCode, asn.OwnerCode, lines = asn.Lines.Select(x => new { id = x.Id, x.ExternalLineId, x.Sku, x.ExpectedQuantity, x.ReceivedQuantity, x.PutawayQuantity, x.Uom }) });
}).RequireAuthorization("inbound.read");

app.MapGet("/api/v1/integration/correlations/{correlationId:guid}/deliveries", async (Guid correlationId, IntegrationDbContext db, CancellationToken ct) =>
    Results.Ok(await db.Outbox.AsNoTracking().Where(x => x.CorrelationId == correlationId).OrderBy(x => x.CreatedAt)
        .Select(x => new { x.MessageId, x.MessageType, x.Status, x.Attempts, x.LastErrorCode, x.DeliveredAt }).ToListAsync(ct)))
    .RequireAuthorization("integration.read");

app.MapGet("/api/v1/supervisor/dashboard", async (LayoutDbContext layout, InboundDbContext inbound, OutboundDbContext outbound, TaskExecutionDbContext tasksDb, InventoryDbContext inventory, MobileSyncDbContext mobile, IntegrationDbContext integration, IConfiguration config, CancellationToken ct) =>
{
    var now = DateTimeOffset.UtcNow;
    var warehouse = await layout.Warehouses.AsNoTracking().Select(x => new { x.Code, x.Name }).FirstOrDefaultAsync(ct);
    var tasks = await tasksDb.Tasks.AsNoTracking().OrderByDescending(x => x.UpdatedAt).Take(20).ToListAsync(ct);
    var messages = await integration.Outbox.AsNoTracking().OrderByDescending(x => x.CreatedAt).Take(20).ToListAsync(ct);
    var inboundPending = await inbound.Asns.CountAsync(x => x.Status != "Completed", ct);
    var putawayPending = await inbound.Asns.CountAsync(x => x.Status == "Received" || x.Status == "PutawayInProgress", ct);
    var ordersAtRisk = await outbound.SalesOrders.CountAsync(x => x.Status != "Shipped" && x.RequestedShipAt <= now.AddHours(4), ct);
    var tasksActive = await tasksDb.Tasks.CountAsync(x => x.Status == "Assigned" || x.Status == "InProgress", ct);
    var stockBlocked = await inventory.StockBalances.SumAsync(x => (decimal?)x.Blocked, ct) ?? 0;
    var stockUnits = await inventory.StockBalances.SumAsync(x => (decimal?)x.OnHand, ct) ?? 0;
    var reservedUnits = await inventory.StockBalances.SumAsync(x => (decimal?)x.Reserved, ct) ?? 0;
    var pickingActive = await tasksDb.Tasks.CountAsync(x => x.Type == "Pick" && (x.Status == "Assigned" || x.Status == "InProgress"), ct);
    var dispatchPending = await outbound.SalesOrders.CountAsync(x => x.Status == "Packed", ct);
    var devicesOffline = await mobile.Devices.CountAsync(x => x.LastSeenAt < now.AddMinutes(-15), ct);
    return Results.Ok(new
    {
        generatedAt = now,
        warehouse = warehouse ?? new { Code = config["Pilot:WarehouseCode"] ?? "WH01", Name = config["Pilot:WarehouseName"] ?? "Pilot Warehouse" },
        metrics = new { inboundPending, putawayPending, ordersAtRisk, tasksActive, stockBlocked, devicesOffline },
        flow = new[]
        {
            new { stage = "ASN", count = (decimal)inboundPending, status = inboundPending > 20 ? "watch" : "normal" },
            new { stage = "Staging", count = (decimal)putawayPending, status = putawayPending > 20 ? "watch" : "normal" },
            new { stage = "Stock", count = stockUnits, status = stockBlocked > 0 ? "watch" : "normal" },
            new { stage = "Reserva", count = reservedUnits, status = ordersAtRisk > 0 ? "watch" : "normal" },
            new { stage = "Picking", count = (decimal)pickingActive, status = "normal" },
            new { stage = "Despacho", count = (decimal)dispatchPending, status = dispatchPending > 30 ? "watch" : "normal" }
        },
        tasks = tasks.Select(x => new { id = x.Id, type = x.Type, reference = x.Reference, assignee = x.AssigneeId, status = x.Status, priority = x.Priority, updatedAt = x.UpdatedAt }),
        integration = messages.Select(x => new { messageId = x.MessageId, type = x.MessageType, externalId = x.CausationId?.ToString(), status = x.Status, attempts = x.Attempts, latencyMs = x.DeliveredAt.HasValue ? (long?)(x.DeliveredAt.Value - x.CreatedAt).TotalMilliseconds : null, correlationId = x.CorrelationId }),
        alerts = messages.Where(x => x.Status is "DeadLettered" or "RequiresReview").Select(x => new { id = x.Id, severity = x.Status == "DeadLettered" ? "critical" : "warning", title = "Integration delivery requires attention", detail = x.LastErrorCode ?? x.Status, ageMinutes = (long)(now - x.CreatedAt).TotalMinutes })
    });
}).RequireAuthorization("supervisor.read");

app.Run();

static void ConfigureTenantDatabase(DbContextOptionsBuilder options, IServiceProvider services, string connectionString) =>
    options.UseNpgsql(connectionString).AddInterceptors(services.GetRequiredService<TenantRlsConnectionInterceptor>());

static void ValidateAsn(AdvanceShippingNotice payload)
{
    if (string.IsNullOrWhiteSpace(payload.ExternalId) || string.IsNullOrWhiteSpace(payload.WarehouseCode) || string.IsNullOrWhiteSpace(payload.OwnerCode) || payload.Lines.Count == 0 || payload.Lines.Any(x => x.Quantity <= 0))
        throw new WmsProblemException("VALIDATION_FAILED", "ASN header and positive lines are required.");
}

static void ValidateOrder(SalesOrder payload)
{
    if (string.IsNullOrWhiteSpace(payload.ExternalId) || string.IsNullOrWhiteSpace(payload.WarehouseCode) || string.IsNullOrWhiteSpace(payload.OwnerCode) || payload.Lines.Count == 0 || payload.Lines.Any(x => x.Quantity <= 0))
        throw new WmsProblemException("VALIDATION_FAILED", "Sales order header and positive lines are required.");
}

public partial class Program;
