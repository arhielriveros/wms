using System.Security.Claims;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Options;

namespace Wms.Api;

public sealed class DevelopmentHeaderAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IWebHostEnvironment environment) : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!environment.IsDevelopment()) return Task.FromResult(AuthenticateResult.NoResult());
        if (!Request.Headers.TryGetValue("X-Tenant-Id", out var tenant) || !Guid.TryParse(tenant, out _))
            return Task.FromResult(AuthenticateResult.Fail("X-Tenant-Id is required in development."));
        var claims = new List<Claim>
        {
            new("tenant_id", tenant.ToString()),
            new(ClaimTypes.NameIdentifier, Request.Headers["X-User-Id"].FirstOrDefault() ?? "dev-user")
        };
        var scopes = Request.Headers["X-Scopes"].FirstOrDefault() ?? "wms.integration.ingest wms.integration.read wms.task.read_assigned wms.task.execute wms.inventory.read wms.supervisor.read wms.inbound.read wms.outbound.read wms.outbound.release wms.outbound.pack wms.outbound.dispatch";
        claims.AddRange(scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries).Select(x => new Claim("scope", x)));
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), Scheme.Name)));
    }
}
