using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Wms.BuildingBlocks;

public static class WmsTelemetry
{
    public const string ApiMeterName = "Wms.Api";
    public const string WorkerMeterName = "Wms.Worker";
    public const string WorkerActivitySourceName = "Wms.Worker";

    private static readonly Meter ApiMeter = new(ApiMeterName);
    private static readonly Meter WorkerMeter = new(WorkerMeterName);

    public static readonly Counter<long> ApiRequests = ApiMeter.CreateCounter<long>(
        "wms.api.requests",
        description: "Completed WMS API requests.");

    public static readonly Histogram<double> ApiRequestDuration = ApiMeter.CreateHistogram<double>(
        "wms.api.request.duration",
        unit: "ms",
        description: "WMS API request duration in milliseconds.");

    public static readonly ActivitySource WorkerActivitySource = new(WorkerActivitySourceName);

    public static readonly Counter<long> WorkerOutboxPolls = WorkerMeter.CreateCounter<long>(
        "wms.worker.outbox.polls",
        description: "Completed outbox polling cycles.");

    public static readonly Counter<long> WorkerOutboxClaimed = WorkerMeter.CreateCounter<long>(
        "wms.worker.outbox.claimed",
        description: "Outbox messages claimed for delivery.");

    public static readonly Counter<long> WorkerOutboxDelivered = WorkerMeter.CreateCounter<long>(
        "wms.worker.outbox.delivered",
        description: "Outbox messages delivered successfully.");

    public static readonly Counter<long> WorkerOutboxFailed = WorkerMeter.CreateCounter<long>(
        "wms.worker.outbox.failed",
        description: "Outbox messages that did not complete successfully.");

    public static readonly Histogram<double> WorkerOutboxPollDuration = WorkerMeter.CreateHistogram<double>(
        "wms.worker.outbox.poll.duration",
        unit: "ms",
        description: "Outbox polling cycle duration in milliseconds.");
}
