namespace iko_host.Exceptions;

using iko_host.Models;

public class PlatformApiException : Exception
{
    public Platform Platform { get; }
    public int? StatusCode { get; }

    public PlatformApiException(Platform platform, string message, int? statusCode = null)
        : base(message)
    {
        Platform = platform;
        StatusCode = statusCode;
    }
}
