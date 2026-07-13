using System.Text.Json;

namespace Wms.Api;

public sealed class KeycloakTokenIntrospector(IHttpClientFactory clients, IConfiguration configuration)
{
    public async Task<bool> IsActiveAsync(string token, CancellationToken cancellationToken)
    {
        if (!configuration.GetValue<bool>("Authentication:IntrospectionEnabled")) return true;

        var authority = configuration["Authentication:Authority"]?.TrimEnd('/');
        var clientId = configuration["Authentication:IntrospectionClientId"];
        var clientSecret = configuration["Authentication:IntrospectionClientSecret"];
        if (string.IsNullOrWhiteSpace(authority) || string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
            throw new InvalidOperationException("Token introspection is enabled but its authority or client credentials are missing.");

        using var request = new HttpRequestMessage(HttpMethod.Post, $"{authority}/protocol/openid-connect/token/introspect");
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret,
            ["token"] = token,
            ["token_type_hint"] = "access_token"
        });
        using var response = await clients.CreateClient("token-introspection").SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode) return false;
        await using var body = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(body, cancellationToken: cancellationToken);
        return document.RootElement.TryGetProperty("active", out var active) && active.ValueKind == JsonValueKind.True;
    }
}
