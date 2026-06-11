namespace iko_host.Exceptions;

using iko_host.Models;

public class UnsupportedPlatformException : Exception
{
    public Platform Platform { get; }

    public UnsupportedPlatformException(Platform platform)
        : base($"Platform {platform} is not supported")
    {
        Platform = platform;
    }
}
