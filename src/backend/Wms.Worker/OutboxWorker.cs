using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Wms.BuildingBlocks;
using Wms.Inbound;
using Wms.Integration;
using Wms.Outbound;
using Wms.Tenancy;

namespace Wms.Worker;

public sealed class OutboxWorker(IServiceScopeFactory scopeFactory, ILogger<OutboxWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("WMS outbox worker started");
        while (!stoppingToken.IsCancellationRequested)
        {
            var started = Stopwatch.GetTimestamp();
            using var activity = WmsTelemetry.WorkerActivitySource.StartActivity("outbox.poll", ActivityKind.Internal);
            try
            {
                var claimed = await DispatchBatchAsync(stoppingToken);
                activity?.SetTag("wms.outbox.claimed", claimed);
                WmsTelemetry.WorkerOutboxClaimed.Add(claimed);
            }
            catch (Exception exception)
            {
                activity?.SetStatus(ActivityStatusCode.Error, exception.GetType().Name);
                logger.LogError(exception, "Outbox dispatch batch failed");
            }
            finally
            {
                WmsTelemetry.WorkerOutboxPolls.Add(1);
                WmsTelemetry.WorkerOutboxPollDuration.Record(Stopwatch.GetElapsedTime(started).TotalMilliseconds);
            }
            await Task.Delay(TimeSpan.FromSeconds(2), stoppingToken);
        }
    }

    private async Task<int> DispatchBatchAsync(CancellationToken ct)
    {
        var claims = await ClaimAsync(ct);
        foreach (var claim in claims)
        {
            using var scope = scopeFactory.CreateScope();
            scope.ServiceProvider.GetRequiredService<TenantContext>().Resolve(claim.TenantId, "wms-worker", claim.CorrelationId, null);
            var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
            var message = await db.Outbox.SingleAsync(x => x.Id == claim.Id && x.Status == "Delivering", ct);
            var attempt = new DeliveryAttempt
            {
                TenantId = message.TenantId, CreatedBy = "wms-worker", CorrelationId = message.CorrelationId,
                OutboxMessageId = message.Id, AttemptNumber = message.Attempts, StartedAt = DateTimeOffset.UtcNow
            };
            db.DeliveryAttempts.Add(attempt);
            await db.SaveChangesAsync(ct);
            using var activity = WmsTelemetry.WorkerActivitySource.StartActivity("outbox.dispatch", ActivityKind.Internal);
            activity?.SetTag("messaging.operation.type", "process");
            try
            {
                activity?.SetTag("messaging.message.type", message.MessageType);
                activity?.SetTag("wms.delivery.kind", message.DeliveryKind);
                if (message.DeliveryKind == "Internal") await DispatchInternalAsync(scope.ServiceProvider, message, ct);
                else await DispatchWebhookAsync(scope.ServiceProvider, message, attempt, ct);
                message.Status = "Delivered";
                message.DeliveredAt = DateTimeOffset.UtcNow;
                message.NextAttemptAt = null;
                message.LastErrorCode = null;
                attempt.Result = "Delivered";
                attempt.CompletedAt = DateTimeOffset.UtcNow;
                WmsTelemetry.WorkerOutboxDelivered.Add(1, new KeyValuePair<string, object?>("messaging.message.type", message.MessageType));
            }
            catch (PermanentDeliveryException exception)
            {
                message.Status = "RequiresReview";
                message.LastErrorCode = exception.Code;
                message.NextAttemptAt = null;
                attempt.Result = "RequiresReview";
                attempt.ErrorCode = exception.Code;
                attempt.CompletedAt = DateTimeOffset.UtcNow;
                activity?.SetStatus(ActivityStatusCode.Error, "requires_review");
                WmsTelemetry.WorkerOutboxFailed.Add(1,
                    new KeyValuePair<string, object?>("wms.delivery.outcome", "requires_review"),
                    new KeyValuePair<string, object?>("messaging.message.type", message.MessageType));
            }
            catch (Exception exception)
            {
                message.LastErrorCode = exception.GetType().Name;
                message.Status = message.Attempts >= 8 ? "DeadLettered" : "RetryScheduled";
                message.NextAttemptAt = message.Status == "RetryScheduled" ? RetrySchedule.Next(message.Attempts, DateTimeOffset.UtcNow) : null;
                attempt.Result = message.Status;
                attempt.ErrorCode = message.LastErrorCode;
                attempt.CompletedAt = DateTimeOffset.UtcNow;
                activity?.SetStatus(ActivityStatusCode.Error, message.Status);
                WmsTelemetry.WorkerOutboxFailed.Add(1,
                    new KeyValuePair<string, object?>("wms.delivery.outcome", message.Status),
                    new KeyValuePair<string, object?>("messaging.message.type", message.MessageType));
                logger.LogWarning(exception, "Outbox message {MessageId} failed attempt {Attempt}", message.MessageId, message.Attempts);
            }
            message.Version++;
            await db.SaveChangesAsync(ct);
        }
        return claims.Count;
    }

    private async Task<IReadOnlyList<MessageClaim>> ClaimAsync(CancellationToken ct)
    {
        using var scope = scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<IntegrationDbContext>();
        var now = DateTimeOffset.UtcNow;
        var candidates = await db.Outbox.IgnoreQueryFilters().AsNoTracking()
            .Where(x => ((x.Status == "Pending" || x.Status == "RetryScheduled") && (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                        (x.Status == "Delivering" && x.NextAttemptAt <= now))
            .OrderBy(x => x.CreatedAt).Take(25)
            .Select(x => new MessageClaim(x.Id, x.TenantId, x.CorrelationId))
            .ToListAsync(ct);
        var claimed = new List<MessageClaim>();
        foreach (var candidate in candidates)
        {
            var affected = await db.Outbox.IgnoreQueryFilters().Where(x => x.Id == candidate.Id &&
                    (((x.Status == "Pending" || x.Status == "RetryScheduled") && (x.NextAttemptAt == null || x.NextAttemptAt <= now)) ||
                     (x.Status == "Delivering" && x.NextAttemptAt <= now)))
                .ExecuteUpdateAsync(update => update
                    .SetProperty(x => x.Status, "Delivering")
                    .SetProperty(x => x.Attempts, x => x.Attempts + 1)
                    .SetProperty(x => x.NextAttemptAt, now.AddMinutes(5))
                    .SetProperty(x => x.Version, x => x.Version + 1), ct);
            if (affected == 1) claimed.Add(candidate);
        }
        return claimed;
    }

    private static async Task DispatchInternalAsync(IServiceProvider services, OutboxMessage message, CancellationToken ct)
    {
        if (!message.CausationId.HasValue) throw new PermanentDeliveryException("CAUSATION_ID_REQUIRED");
        var planner = services.GetRequiredService<IImportTaskPlanner>();
        if (message.MessageType == "ImportAdvanceShippingNotice")
        {
            var payload = message.Payload.RootElement.Deserialize<AdvanceShippingNotice>() ?? throw new PermanentDeliveryException("PAYLOAD_INVALID");
            var documentId = await services.GetRequiredService<IInboundImporter>().ImportAsync(message.CausationId.Value, message.TenantId, message.CorrelationId, "wms-worker", payload, ct);
            await planner.PlanInboundAsync(documentId, message.TenantId, message.CorrelationId, ct);
            return;
        }
        if (message.MessageType == "ImportSalesOrder")
        {
            var payload = message.Payload.RootElement.Deserialize<SalesOrder>() ?? throw new PermanentDeliveryException("PAYLOAD_INVALID");
            await services.GetRequiredService<IOutboundImporter>().ImportAsync(message.CausationId.Value, message.TenantId, message.CorrelationId, "wms-worker", payload, ct);
            return;
        }
        throw new PermanentDeliveryException("MESSAGE_TYPE_UNSUPPORTED");
    }

    private static async Task DispatchWebhookAsync(IServiceProvider services, OutboxMessage message, DeliveryAttempt attempt, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(message.Destination)) throw new PermanentDeliveryException("DESTINATION_REQUIRED");
        var configuration = services.GetRequiredService<IConfiguration>();
        var secret = configuration["Integration:WebhookSecret"] ?? throw new PermanentDeliveryException("WEBHOOK_SECRET_REQUIRED");
        var body = message.Payload.RootElement.GetRawText();
        var timestamp = DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString();
        var signature = Convert.ToHexString(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secret), Encoding.UTF8.GetBytes(timestamp + "." + body))).ToLowerInvariant();
        using var request = new HttpRequestMessage(HttpMethod.Post, message.Destination) { Content = new StringContent(body, Encoding.UTF8, "application/json") };
        request.Headers.Add("WMS-Message-Id", message.MessageId.ToString());
        request.Headers.Add("WMS-Timestamp", timestamp);
        request.Headers.Add("WMS-Signature", "v1=" + signature);
        var response = await services.GetRequiredService<IHttpClientFactory>().CreateClient().SendAsync(request, ct);
        attempt.HttpStatus = (int)response.StatusCode;
        if (response.IsSuccessStatusCode) return;
        if ((int)response.StatusCode is 408 or 429 || (int)response.StatusCode >= 500) throw new HttpRequestException("Transient webhook response " + (int)response.StatusCode);
        throw new PermanentDeliveryException("WEBHOOK_NON_TRANSIENT_" + (int)response.StatusCode);
    }

    private sealed record MessageClaim(Guid Id, Guid TenantId, Guid CorrelationId);
    private sealed class PermanentDeliveryException(string code) : Exception(code) { public string Code { get; } = code; }
}
