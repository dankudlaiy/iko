namespace iko_host.Tests.Integration;

using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

public class AuthApiTests : IClassFixture<IkoApiFactory>
{
    private readonly HttpClient _client;

    public AuthApiTests(IkoApiFactory factory) => _client = factory.CreateClient();

    private static async Task<JsonElement> Body(HttpResponseMessage response) =>
        await response.Content.ReadFromJsonAsync<JsonElement>();

    private async Task<string> RegisterAndGetToken(string email)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email, password = "password123" });
        response.EnsureSuccessStatusCode();
        return (await Body(response)).GetProperty("data").GetProperty("token").GetString()!;
    }

    [Fact]
    public async Task Register_returns_token()
    {
        var token = await RegisterAndGetToken("reg@test.com");
        Assert.False(string.IsNullOrWhiteSpace(token));
    }

    [Fact]
    public async Task Register_with_duplicate_email_returns_409()
    {
        await RegisterAndGetToken("dup@test.com");
        var response = await _client.PostAsJsonAsync("/api/auth/register",
            new { email = "dup@test.com", password = "password123" });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Theory]
    [InlineData("not-an-email", "password123")]
    [InlineData("ok@test.com", "short")]
    [InlineData("", "password123")]
    public async Task Register_with_invalid_input_returns_400(string email, string password)
    {
        var response = await _client.PostAsJsonAsync("/api/auth/register", new { email, password });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_returns_401()
    {
        await RegisterAndGetToken("login@test.com");
        var response = await _client.PostAsJsonAsync("/api/auth/login",
            new { email = "login@test.com", password = "wrong-password" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Me_requires_and_accepts_bearer_token()
    {
        var anonymous = await _client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);

        var token = await RegisterAndGetToken("me@test.com");
        var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        var response = await _client.SendAsync(request);

        response.EnsureSuccessStatusCode();
        Assert.Equal("me@test.com",
            (await Body(response)).GetProperty("data").GetProperty("email").GetString());
    }
}
