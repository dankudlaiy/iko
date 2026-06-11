namespace iko_host.Tests.Unit;

using iko_host.Clients;
using iko_host.Exceptions;
using iko_host.Models;
using Moq;

public class PlatformClientFactoryTests
{
    private static IPlatformClient ClientFor(Platform platform)
    {
        var mock = new Mock<IPlatformClient>();
        mock.SetupGet(c => c.Platform).Returns(platform);
        return mock.Object;
    }

    private static PlatformClientFactory Factory() => new(new[]
    {
        ClientFor(Platform.Spotify), ClientFor(Platform.YouTube), ClientFor(Platform.AppleMusic)
    });

    [Theory]
    [InlineData(Platform.Spotify)]
    [InlineData(Platform.YouTube)]
    [InlineData(Platform.AppleMusic)]
    public void Get_returns_client_for_supported_platform(Platform platform)
    {
        Assert.Equal(platform, Factory().Get(platform).Platform);
    }

    [Theory]
    [InlineData(Platform.SoundCloud)]
    [InlineData(Platform.Deezer)]
    public void Get_throws_for_unsupported_platform(Platform platform)
    {
        var ex = Assert.Throws<UnsupportedPlatformException>(() => Factory().Get(platform));
        Assert.Equal(platform, ex.Platform);
    }
}
