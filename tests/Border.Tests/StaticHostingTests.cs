using System.Net;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Border.Tests;

public sealed class StaticHostingTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    [Fact]
    public async Task UnknownApiRoute_ReturnsPlain404InsteadOfFrontendHtml()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/api/not-a-real-endpoint");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.NotEqual("text/html", response.Content.Headers.ContentType?.MediaType);
    }

    [Fact]
    public async Task UnknownUiRoute_ReturnsHtml404()
    {
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        var response = await client.GetAsync("/not-a-real-page");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        Assert.Equal("text/html", response.Content.Headers.ContentType?.MediaType);
    }
}
