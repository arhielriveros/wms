using System.Security.Claims;
using FluentAssertions;
using Wms.BuildingBlocks;
using Xunit;

namespace Wms.Backend.Tests;

public sealed class TokenPermissionEvaluatorTests
{
    [Fact]
    public void GrantsPermissionFromOAuthScope()
    {
        var user = Principal(new Claim("scope", "openid wms.inventory.read"));

        TokenPermissionEvaluator.HasPermission(user, "wms.inventory.read", "wms-api").Should().BeTrue();
    }

    [Fact]
    public void GrantsPermissionFromKeycloakClientRole()
    {
        var user = Principal(new Claim("resource_access", "{\"wms-api\":{\"roles\":[\"wms.inventory.read\"]}}"));

        TokenPermissionEvaluator.HasPermission(user, "wms.inventory.read", "wms-api").Should().BeTrue();
    }

    [Theory]
    [InlineData("{malformed")]
    [InlineData("{\"another-client\":{\"roles\":[\"wms.inventory.read\"]}}")]
    [InlineData("{\"wms-api\":{\"roles\":[\"wms.task.execute\"]}}")]
    public void DeniesMalformedWrongAudienceOrMissingRole(string resourceAccess)
    {
        var user = Principal(new Claim("resource_access", resourceAccess));

        TokenPermissionEvaluator.HasPermission(user, "wms.inventory.read", "wms-api").Should().BeFalse();
    }

    private static ClaimsPrincipal Principal(params Claim[] claims) => new(new ClaimsIdentity(claims, "test"));
}
