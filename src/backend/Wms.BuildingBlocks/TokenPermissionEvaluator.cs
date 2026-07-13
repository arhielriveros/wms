using System.Security.Claims;
using System.Text.Json;

namespace Wms.BuildingBlocks;

public static class TokenPermissionEvaluator
{
    public static bool HasPermission(ClaimsPrincipal user, string permission, string audience)
    {
        if (user.FindAll("scope")
            .SelectMany(x => x.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(permission, StringComparer.Ordinal))
            return true;

        foreach (var claim in user.FindAll("resource_access"))
        {
            try
            {
                using var document = JsonDocument.Parse(claim.Value);
                if (!document.RootElement.TryGetProperty(audience, out var client) ||
                    !client.TryGetProperty("roles", out var roles) ||
                    roles.ValueKind != JsonValueKind.Array)
                    continue;

                if (roles.EnumerateArray().Any(role =>
                        string.Equals(role.GetString(), permission, StringComparison.Ordinal)))
                    return true;
            }
            catch (JsonException)
            {
                // A malformed authorization claim grants no permission.
            }
        }

        return false;
    }
}
