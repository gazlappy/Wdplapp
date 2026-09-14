using System.Net;
using System.Text;
using Wdpl2.Services.Web;

namespace wdpl2.Tests;

/// <summary>
/// Exercises the response envelope and auth handling against a fake transport.
/// The important cases are the ones the old backend handled inconsistently:
/// an HTML error page, a redirect, and a backend error that must surface its
/// own message rather than a generic failure.
/// </summary>
public class WebApiClientTests
{
    private sealed class FakeHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _status;
        private readonly string _body;
        private readonly string _contentType;

        public FakeHandler(HttpStatusCode status, string body, string contentType = "application/json")
        {
            _status = status;
            _body = body;
            _contentType = contentType;
        }

        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(_status)
            {
                Content = new StringContent(_body, Encoding.UTF8, _contentType),
            });
        }
    }

    private static WebConnection Connection() => new()
    {
        BaseUrl = "https://wdpl.uk/api/",
        AdminUser = "admin",
        AdminPassword = "pw",
    };

    [Fact]
    public async Task Unwraps_TheDataPayload()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"ok":true,"data":{"php":"8.2.0"}}""");
        using var client = new WebApiClient(Connection(), handler);

        var result = await client.AdminAsync("system", "health");

        Assert.Equal("8.2.0", result.GetProperty("php").GetString());
    }

    [Fact]
    public async Task RoutesToTheModuleAndActionAsQueryParameters()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"ok":true,"data":null}""");
        using var client = new WebApiClient(Connection(), handler);

        await client.AdminAsync("teams", "push");

        Assert.Equal("https://wdpl.uk/api/index.php?m=teams&a=push", handler.LastRequest!.RequestUri!.AbsoluteUri);
    }

    [Fact]
    public async Task SendsBasicCredentialsOnlyForAdminCalls()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"ok":true,"data":null}""");
        using var client = new WebApiClient(Connection(), handler);

        await client.AdminAsync("system", "health");
        Assert.Equal("Basic", handler.LastRequest!.Headers.Authorization!.Scheme);

        await client.PublicAsync("system", "ping");
        Assert.Null(handler.LastRequest!.Headers.Authorization);
    }

    [Fact]
    public async Task SurfacesTheBackendsOwnErrorCodeAndMessage()
    {
        var handler = new FakeHandler(HttpStatusCode.ServiceUnavailable,
            """{"ok":false,"error":{"code":"not_configured","message":"Backend is not configured."}}""");
        using var client = new WebApiClient(Connection(), handler);

        var ex = await Assert.ThrowsAsync<WebApiException>(() => client.AdminAsync("system", "health"));

        Assert.Equal("not_configured", ex.Code);
        Assert.Equal("Backend is not configured.", ex.Message);
    }

    [Fact]
    public async Task ReportsHtmlAsNotJson_RatherThanCrashing()
    {
        // The classic shared-hosting failure: PHP is off, so the host serves a page.
        var handler = new FakeHandler(HttpStatusCode.OK, "<html><body>Index of /api</body></html>", "text/html");
        using var client = new WebApiClient(Connection(), handler);

        var ex = await Assert.ThrowsAsync<WebApiException>(() => client.AdminAsync("system", "health"));

        Assert.Equal("not_json", ex.Code);
    }

    [Fact]
    public async Task TreatsARedirectAsAnError_SoCredentialsAreNotReSent()
    {
        var handler = new FakeHandler(HttpStatusCode.Found, "");
        using var client = new WebApiClient(Connection(), handler);

        var ex = await Assert.ThrowsAsync<WebApiException>(() => client.AdminAsync("system", "health"));

        Assert.Equal("redirected", ex.Code);
    }

    [Fact]
    public async Task RejectsAnUnexpectedEnvelopeShape()
    {
        var handler = new FakeHandler(HttpStatusCode.OK, """{"result":"fine"}""");
        using var client = new WebApiClient(Connection(), handler);

        var ex = await Assert.ThrowsAsync<WebApiException>(() => client.AdminAsync("system", "health"));

        Assert.Equal("bad_envelope", ex.Code);
    }

    [Fact]
    public async Task RefusesAnAdminCallWithNoSavedPassword()
    {
        var connection = Connection();
        connection.AdminPassword = "";

        var handler = new FakeHandler(HttpStatusCode.OK, """{"ok":true,"data":null}""");
        using var client = new WebApiClient(connection, handler);

        var ex = await Assert.ThrowsAsync<WebApiException>(() => client.AdminAsync("system", "health"));

        Assert.Equal("no_credentials", ex.Code);
    }

    [Fact]
    public void ConstructorRejectsAnInsecureBaseUrl()
    {
        var connection = Connection();
        connection.BaseUrl = "http://wdpl.uk/api/";

        Assert.Throws<InvalidOperationException>(() => new WebApiClient(connection, new FakeHandler(HttpStatusCode.OK, "{}")));
    }
}
