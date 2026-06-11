namespace iko_host.Tests.TestHelpers;

/// <summary>Platform client constructors read credentials from env vars; set fakes once.</summary>
public static class FakePlatformEnv
{
    public static void Set()
    {
        Environment.SetEnvironmentVariable("SPOTIFY_CLIENT_ID", "test-spotify-id");
        Environment.SetEnvironmentVariable("SPOTIFY_CLIENT_SECRET", "test-spotify-secret");
        Environment.SetEnvironmentVariable("YOUTUBE_CLIENT_ID", "test-youtube-id");
        Environment.SetEnvironmentVariable("YOUTUBE_CLIENT_SECRET", "test-youtube-secret");
        Environment.SetEnvironmentVariable("YOUTUBE_API_KEY", "test-youtube-key");
        Environment.SetEnvironmentVariable("APPLE_DEVELOPER_TOKEN", "test-apple-token");
    }
}
