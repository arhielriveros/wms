using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Wms.BuildingBlocks;

namespace Wms.Tenancy;

/// <summary>
/// Propagates the authenticated tenant to PostgreSQL before EF executes a command.
/// Npgsql resets session state when a pooled connection is returned to the pool.
/// </summary>
public sealed class TenantRlsConnectionInterceptor(ITenantContext tenant) : DbConnectionInterceptor
{
    public override async Task ConnectionOpenedAsync(
        DbConnection connection,
        ConnectionEndEventData eventData,
        CancellationToken cancellationToken = default)
    {
        if (!tenant.IsResolved) return;

        await using var command = connection.CreateCommand();
        command.CommandText = "SELECT set_config('app.tenant_id', @tenant_id, false)";
        var parameter = command.CreateParameter();
        parameter.ParameterName = "tenant_id";
        parameter.Value = tenant.TenantId.ToString();
        command.Parameters.Add(parameter);
        await command.ExecuteScalarAsync(cancellationToken);
    }
}
