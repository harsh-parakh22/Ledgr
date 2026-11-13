using System.Net;
using System.Net.Http.Json;
using FluentAssertions;

namespace Ledgr.Tests.Api;

public sealed class HealthTests
{
    [Fact]
    public async Task Health_ReturnsOkAndSetsSecurityHeaders()
    {
        await using var factory = new LedgrApiFactory();
        using var client = factory.CreateClient();

        var response = await client.GetAsync("/health");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        response.Headers.GetValues("X-Content-Type-Options").Should().ContainSingle().Which.Should().Be("nosniff");
        response.Headers.GetValues("Content-Security-Policy").Should().ContainSingle();
    }
}

