using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Border.Tests;

public sealed class RenderForwardedHeadersTests(StudentApiFactory factory) : IClassFixture<StudentApiFactory>
{
    [Fact]
    public async Task RenderForwardedHttps_SupportsHealthCsrfAndLoginAntiforgery()
    {
        await factory.ResetAsync();
        using var renderFactory = factory.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("RENDER", "true");
            builder.UseSetting("Security:RequireSecureCookies", "true");
            builder.UseSetting("Security:UseHttpsRedirection", "false");
        });
        using var client = renderFactory.CreateClient(new WebApplicationFactoryClientOptions
        {
            AllowAutoRedirect = false,
            HandleCookies = false
        });

        using var healthRequest = ForwardedHttps(HttpMethod.Get, "/health");
        using var healthResponse = await client.SendAsync(healthRequest);
        Assert.Equal(HttpStatusCode.OK, healthResponse.StatusCode);

        using var csrfRequest = ForwardedHttps(HttpMethod.Get, "/api/auth/csrf");
        using var csrfResponse = await client.SendAsync(csrfRequest);
        csrfResponse.EnsureSuccessStatusCode();

        var csrf = await csrfResponse.Content.ReadFromJsonAsync<JsonElement>();
        var token = csrf.GetProperty("token").GetString();
        Assert.False(string.IsNullOrWhiteSpace(token));

        var antiforgeryCookie = Assert.Single(
            csrfResponse.Headers.GetValues("Set-Cookie"),
            value => value.StartsWith("border.xsrf=", StringComparison.Ordinal));
        Assert.Contains("; secure", antiforgeryCookie, StringComparison.OrdinalIgnoreCase);

        using var loginRequest = ForwardedHttps(HttpMethod.Post, "/api/auth/login");
        loginRequest.Headers.Add("X-XSRF-TOKEN", token);
        loginRequest.Headers.Add("Cookie", antiforgeryCookie.Split(';', 2)[0]);
        loginRequest.Content = JsonContent.Create(new
        {
            email = "missing@example.test",
            password = "Not-A-Real-Password-1!",
            rememberMe = false
        });

        using var loginResponse = await client.SendAsync(loginRequest);
        Assert.Equal(HttpStatusCode.Unauthorized, loginResponse.StatusCode);
    }

    private static HttpRequestMessage ForwardedHttps(HttpMethod method, string path)
    {
        var request = new HttpRequestMessage(method, path);
        request.Headers.Add("X-Forwarded-Proto", "https");
        return request;
    }
}
