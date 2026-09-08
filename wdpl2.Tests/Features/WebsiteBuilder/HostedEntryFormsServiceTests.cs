using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services;
using Wdpl2.Services.Cloud;
using Wdpl2.Services.Inbox;

namespace wdpl2.Tests;

public class HostedEntryFormsServiceTests
{
    private static WebInboxSettings Connection() => new()
    {
        BaseUrl = "https://backend.example.test/api/", AdminUser = "secretary", AdminPassword = "private-password"
    };

    private static WebsiteSettings Website() => new()
    {
        WebsiteUrl = "https://league.example.test/season/", ShowEntryForms = true,
        EntryForms = [EntryForm.CreateTeamEntryForm()], UseHostedEntryForms = true,
        FormServiceUrl = "https://backend.example.test/api/entry-forms/submit.php",
        FormServiceFetchUrl = "https://backend.example.test/api/admin/entry-form-submissions.php"
    };

    private sealed class Handler(Func<HttpRequestMessage, Task<HttpResponseMessage>> send) : HttpMessageHandler
    {
        public int Calls { get; private set; }
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return send(request);
        }
    }

    private static HttpResponseMessage Response(string body, HttpStatusCode status = HttpStatusCode.OK) => new(status)
    {
        Content = new StringContent(body, Encoding.UTF8, "application/json")
    };

    [Theory]
    [InlineData("")]
    [InlineData("http://backend.example.test/api/")]
    [InlineData("ftp://backend.example.test/")]
    [InlineData("https://user:password@backend.example.test/api/")]
    [InlineData("https://backend.example.test/api/?token=secret")]
    [InlineData("https://backend.example.test/api/#fragment")]
    public void BaseUri_RejectsUnsafeDestinations(string url) =>
        Assert.Throws<InvalidOperationException>(() => HostedEntryFormsService.ValidateBaseUri(url));

    [Theory]
    [InlineData("")]
    [InlineData("http://league.example.test/")]
    [InlineData("https://user:password@league.example.test/")]
    [InlineData("https://league.example.test/?token=secret")]
    [InlineData("https://league.example.test/#fragment")]
    public void Definitions_ReportPublicWebsiteUrlRatherThanApiSettings(string url)
    {
        var settings = Website();
        settings.WebsiteUrl = url;
        var error = Assert.Throws<InvalidOperationException>(() => HostedEntryFormsService.BuildDefinitionPayload(settings));
        Assert.Contains("public Website URL", error.Message);
        Assert.DoesNotContain("Set a valid HTTPS API base URL", error.Message);
        var apiError = Assert.Throws<InvalidOperationException>(() => HostedEntryFormsService.ValidateBaseUri(url));
        Assert.Contains("Web Inbox settings", apiError.Message);
    }

    [Fact]
    public void HostingSelection_RoundTripsWithoutEnablingUnconfirmedDelivery()
    {
        var settings = Website();
        settings.UseHostedEntryForms = false;
        EntryFormRules.SetHostedMode(settings, true);
        var reloaded = JsonSerializer.Deserialize<WebsiteSettings>(JsonSerializer.Serialize(settings))!;
        Assert.True(reloaded.UseHostedEntryForms);
        Assert.False(reloaded.HostedEntryFormsPublished);
        Assert.Null(EntryFormRules.DeliveryEndpoint(reloaded));
        EntryFormRules.SetHostedMode(reloaded, false);
        Assert.False(reloaded.UseHostedEntryForms);
        EntryFormRules.SetHostedMode(reloaded, true);
        Assert.False(EntryFormRules.IsHostedDeliveryReady(reloaded));
        reloaded.HostedEntryFormsPublished = true;
        Assert.Equal(reloaded.FormServiceUrl, EntryFormRules.DeliveryEndpoint(reloaded));
    }

    [Fact]
    public void HostingSelection_PreservesLegacyAcknowledgedConfiguration()
    {
        var settings = Website();
        Assert.Null(settings.HostedEntryFormsPublished);
        Assert.True(EntryFormRules.IsHostedDeliveryReady(settings));
        EntryFormRules.SetHostedMode(settings, false);
        Assert.True(settings.HostedEntryFormsPublished);
        EntryFormRules.SetHostedMode(settings, true);
        Assert.True(EntryFormRules.IsHostedDeliveryReady(settings));
    }

    [Fact]
    public async Task Fetch_RejectsUnconfirmedSelectionWithoutNetworkRequest()
    {
        var settings = Website();
        settings.HostedEntryFormsPublished = false;
        using var handler = new Handler(_ => throw new InvalidOperationException("Must not send"));
        using var service = new HostedEntryFormsService(Connection(), handler);
        var error = await Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchAsync(settings));
        Assert.Contains("Publish saved forms", error.Message);
        Assert.Equal(0, handler.Calls);
    }

    [Fact]
    public void Constructor_RejectsMissingCredentialsAndCertificateBypass()
    {
        var connection = Connection();
        connection.IgnoreSslErrors = true;
        Assert.Throws<InvalidOperationException>(() => new HostedEntryFormsService(connection));
        connection.IgnoreSslErrors = false;
        connection.AdminPassword = "";
        Assert.Throws<InvalidOperationException>(() => new HostedEntryFormsService(connection));
    }

    [Fact]
    public void Definitions_IncludeOnlySavedPublishedFieldsAndNoPrivateData()
    {
        var settings = Website();
        settings.FormServiceApiToken = "DO-NOT-PUBLISH";
        settings.EntryForms[0].Submissions.Add(new EntryFormSubmission { EntryName = "PRIVATE-ENTRY" });
        settings.EntryForms[0].IsClosed = true;
        settings.EntryForms[0].ClosingDate = new DateTime(2026, 12, 31);
        var draft = EntryForm.CreateCompetitionEntryForm();
        draft.IsPublished = false;
        settings.EntryForms.Add(draft);
        var before = JsonSerializer.Serialize(settings.EntryForms);
        var json = HostedEntryFormsService.BuildDefinitionPayload(settings);
        using var doc = JsonDocument.Parse(json);
        Assert.Equal("https://league.example.test", doc.RootElement.GetProperty("origin").GetString());
        Assert.Equal("Europe/London", doc.RootElement.GetProperty("timeZone").GetString());
        var form = Assert.Single(doc.RootElement.GetProperty("forms").EnumerateArray());
        Assert.Equal($"form-{settings.EntryForms[0].Id:N}", form.GetProperty("id").GetString());
        Assert.True(form.GetProperty("closed").GetBoolean());
        Assert.Equal("2026-12-31", form.GetProperty("closingDate").GetString());
        Assert.DoesNotContain("DO-NOT-PUBLISH", json);
        Assert.DoesNotContain("PRIVATE-ENTRY", json);
        Assert.DoesNotContain(draft.Id.ToString("N"), json);
        Assert.Equal(before, JsonSerializer.Serialize(settings.EntryForms));
        settings.ShowEntryForms = false;
        using var disabled = JsonDocument.Parse(HostedEntryFormsService.BuildDefinitionPayload(settings));
        Assert.Empty(disabled.RootElement.GetProperty("forms").EnumerateArray());
    }

    [Fact]
    public void Definitions_RejectInvalidLabelsAndOptions()
    {
        var settings = Website();
        settings.EntryForms[0].Fields[0].Label = new string('A', 201);
        Assert.Throws<InvalidOperationException>(() => HostedEntryFormsService.BuildDefinitionPayload(settings));
        settings.EntryForms[0].Fields[0].Label = "Team";
        settings.EntryForms[0].Fields[0].FieldType = "select";
        settings.EntryForms[0].Fields[0].Options = "A,A";
        Assert.Throws<InvalidOperationException>(() => HostedEntryFormsService.BuildDefinitionPayload(settings));
    }

    [Fact]
    public async Task Publish_SendsDefinitionAndPrivateAuthOnlyToConfiguredAdminEndpoint()
    {
        var settings = Website();
        var payload = HostedEntryFormsService.BuildDefinitionPayload(settings);
        using var handler = new Handler(async request =>
        {
            Assert.Equal("https://backend.example.test/api/admin/entry-form-definitions.php", request.RequestUri!.AbsoluteUri);
            Assert.Equal(HttpMethod.Post, request.Method);
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            Assert.Equal("secretary:private-password", Encoding.UTF8.GetString(Convert.FromBase64String(request.Headers.Authorization.Parameter!)));
            Assert.Equal(payload, await request.Content!.ReadAsStringAsync());
            return Response("{\"protocol\":1,\"published\":true,\"count\":1}");
        });
        using var service = new HostedEntryFormsService(Connection(), handler);
        await service.PublishAsync(payload);
        Assert.Equal(1, handler.Calls);
    }

    [Theory]
    [InlineData("{}")]
    [InlineData("{\"protocol\":1,\"published\":false,\"count\":1}")]
    [InlineData("{\"protocol\":1,\"published\":true,\"count\":0}")]
    [InlineData("{\"protocol\":2,\"published\":true,\"count\":1}")]
    public async Task Publish_RequiresCompleteAcknowledgement(string response)
    {
        using var handler = new Handler(_ => Task.FromResult(Response(response)));
        using var service = new HostedEntryFormsService(Connection(), handler);
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PublishAsync(HostedEntryFormsService.BuildDefinitionPayload(Website())));
    }

    [Theory]
    [InlineData(HttpStatusCode.Redirect)]
    [InlineData(HttpStatusCode.Unauthorized)]
    [InlineData(HttpStatusCode.Forbidden)]
    [InlineData(HttpStatusCode.InternalServerError)]
    public async Task Publish_RejectsHttpErrorsWithoutAnotherRequest(HttpStatusCode status)
    {
        using var handler = new Handler(_ => Task.FromResult(Response("{}", status)));
        using var service = new HostedEntryFormsService(Connection(), handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.PublishAsync(HostedEntryFormsService.BuildDefinitionPayload(Website())));
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Fetch_CollectsAllPagesWithinFixedSnapshot()
    {
        var calls = 0;
        using var handler = new Handler(request =>
        {
            calls++;
            Assert.Equal("Basic", request.Headers.Authorization!.Scheme);
            Assert.Equal(calls == 1 ? "?after=0" : "?after=1&through=2", request.RequestUri!.Query);
            return Task.FromResult(Response(calls == 1
                ? "{\"protocol\":1,\"submissions\":[{\"id\":\"one\"}],\"nextAfter\":1,\"through\":2}"
                : "{\"protocol\":1,\"submissions\":[{\"id\":\"two\"}],\"nextAfter\":null,\"through\":2}"));
        });
        using var service = new HostedEntryFormsService(Connection(), handler);
        using var result = JsonDocument.Parse(await service.FetchAsync(Website()));
        Assert.Equal(2, result.RootElement.GetArrayLength());
        Assert.Equal(2, calls);
    }

    [Fact]
    public async Task Fetch_RejectsChangedDestinationBeforeSendingCredentials()
    {
        using var handler = new Handler(_ => throw new InvalidOperationException("Must not send"));
        using var service = new HostedEntryFormsService(Connection(), handler);
        var settings = Website();
        settings.FormServiceFetchUrl = "https://other.example.test/collection";
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.FetchAsync(settings));
        Assert.Equal(0, handler.Calls);
    }

    [Theory]
    [InlineData("{\"protocol\":1,\"submissions\":[{}],\"nextAfter\":0,\"through\":2}")]
    [InlineData("{\"protocol\":1,\"submissions\":[],\"nextAfter\":1,\"through\":2}")]
    [InlineData("{\"protocol\":1,\"submissions\":[{}],\"nextAfter\":3,\"through\":2}")]
    public async Task Fetch_RejectsInvalidPagination(string response)
    {
        using var handler = new Handler(_ => Task.FromResult(Response(response)));
        using var service = new HostedEntryFormsService(Connection(), handler);
        await Assert.ThrowsAsync<JsonException>(() => service.FetchAsync(Website()));
    }

    [Fact]
    public async Task Fetch_FailedLaterPageDoesNotReturnPartialCollection()
    {
        var count = 0;
        using var handler = new Handler(_ => Task.FromResult(++count == 1
            ? Response("{\"protocol\":1,\"submissions\":[{}],\"nextAfter\":1,\"through\":2}")
            : Response("{}", HttpStatusCode.InternalServerError)));
        using var service = new HostedEntryFormsService(Connection(), handler);
        await Assert.ThrowsAsync<HttpRequestException>(() => service.FetchAsync(Website()));
    }

    [Fact]
    public void DeploymentBundle_ContainsAllEntryFormEndpoints()
    {
        foreach (var path in new[] { "api/_entry_forms.php", "api/_entry_form_rules.php", "api/entry-forms/submit.php",
            "api/admin/entry-form-definitions.php", "api/admin/entry-form-submissions.php" })
            Assert.Contains(path, BackendDeployService.BundledFiles);
    }
}
