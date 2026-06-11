namespace iko_host.Tests.Unit;

using System.Text.Json;
using iko_host.Exceptions;
using iko_host.Infrastructure;
using iko_host.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;

public class GlobalExceptionHandlerTests
{
    private static async Task<(int Status, JsonElement Body)> Handle(Exception exception)
    {
        var handler = new GlobalExceptionHandler(NullLogger<GlobalExceptionHandler>.Instance);
        var context = new DefaultHttpContext();
        context.Response.Body = new MemoryStream();

        var handled = await handler.TryHandleAsync(context, exception, CancellationToken.None);
        Assert.True(handled);

        context.Response.Body.Position = 0;
        var body = await JsonDocument.ParseAsync(context.Response.Body);
        return (context.Response.StatusCode, body.RootElement.Clone());
    }

    [Fact]
    public async Task Unsupported_platform_maps_to_400()
    {
        var (status, body) = await Handle(new UnsupportedPlatformException(Platform.Deezer));

        Assert.Equal(400, status);
        Assert.Contains("Deezer", body.GetProperty("error").GetString());
        Assert.Equal(JsonValueKind.Null, body.GetProperty("data").ValueKind);
    }

    [Fact]
    public async Task Platform_api_error_maps_to_502_with_platform_name()
    {
        var (status, body) = await Handle(new PlatformApiException(Platform.Spotify, "rate limited", 429));

        Assert.Equal(502, status);
        Assert.Contains("Spotify", body.GetProperty("error").GetString());
    }

    [Fact]
    public async Task Unknown_exception_maps_to_500_without_details()
    {
        var (status, body) = await Handle(new InvalidOperationException("secret internals"));

        Assert.Equal(500, status);
        Assert.DoesNotContain("secret", body.GetProperty("error").GetString());
    }
}
