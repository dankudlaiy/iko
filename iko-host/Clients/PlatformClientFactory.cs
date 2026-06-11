namespace iko_host.Clients;

using iko_host.Exceptions;
using iko_host.Models;

public class PlatformClientFactory
{
    private readonly IEnumerable<IPlatformClient> _clients;

    public PlatformClientFactory(IEnumerable<IPlatformClient> clients)
    {
        _clients = clients;
    }

    public IPlatformClient Get(Platform platform) =>
        _clients.FirstOrDefault(c => c.Platform == platform)
        ?? throw new UnsupportedPlatformException(platform);
}
