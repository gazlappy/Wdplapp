using System.Text.Json;
using System.Text.RegularExpressions;
using Wdpl2.Models;
using Wdpl2.Services;

namespace wdpl2.Tests;

public class EntryFormsTests
{
    private static (WebsiteGenerator Generator, WebsiteSettings Settings, EntryForm Form) Setup()
    {
        var league = new LeagueData();
        league.Seasons.Add(new Season { Name = "Winter 2025/26", IsActive = true });
        var form = EntryForm.CreateTeamEntryForm();
        var settings = new WebsiteSettings { ShowEntryForms = true, EntryForms = [form] };
        return (new WebsiteGenerator(league, settings), settings, form);
    }

    [Fact]
    public void Draft_IsolatesFieldsAndLogoAndDoesNotCopyRecords()
    {
        var form = EntryForm.CreateTeamEntryForm();
        form.LogoImageData = [1, 2, 3];
        form.Submissions.Add(new EntryFormSubmission { EntryName = "Existing" });
        form.ImportedExternalIds.Add("remote-1");
        var draft = EntryFormRules.CreateDraft(form);
        draft.Fields[0].Label = "Changed";
        draft.LogoImageData![0] = 9;
        draft.Fields.RemoveAt(1);
        Assert.Equal("Team Name", form.Fields[0].Label);
        Assert.Equal(8, form.Fields.Count);
        Assert.Equal(1, form.LogoImageData[0]);
        Assert.Equal(form.Id, draft.Id);
        Assert.Equal(form.Fields[0].Id, draft.Fields[0].Id);
        Assert.Empty(draft.Submissions);
        Assert.Empty(draft.ImportedExternalIds);
        Assert.Single(form.Submissions);
    }

    [Fact]
    public void ClosingDate_IsInclusive_AndDraftStatusTakesPrecedence()
    {
        var day = new DateTime(2026, 1, 20);
        var form = new EntryForm { ClosingDate = day };
        Assert.False(EntryFormRules.IsClosed(form, day.AddHours(23)));
        Assert.True(EntryFormRules.IsClosed(form, day.AddDays(1)));
        form.IsClosed = true;
        form.IsPublished = false;
        Assert.Equal("Draft", EntryFormRules.Status(form, day));
    }

    [Fact]
    public void Validation_RejectsEmptyDuplicateReservedAndUnconfiguredFields()
    {
        var form = new EntryForm
        {
            Fields = [new() { Label = "Name" }, new() { Label = " name " }, new() { Label = "_formId" },
                new() { Label = "Choice", FieldType = "select", Options = " , " }, new() { Label = "", FieldType = "script" }]
        };
        var errors = EntryFormRules.Validate(form);
        Assert.Contains(errors, e => e.Contains("title"));
        Assert.Contains(errors, e => e.Contains("unique"));
        Assert.Contains(errors, e => e.Contains("reserved"));
        Assert.Contains(errors, e => e.Contains("dropdown"));
        Assert.Contains(errors, e => e.Contains("label"));
        Assert.Contains(errors, e => e.Contains("supported"));
        Assert.Empty(EntryFormRules.Validate(EntryForm.CreateTeamEntryForm()));
        Assert.Empty(EntryFormRules.Validate(EntryForm.CreateCompetitionEntryForm()));
    }

    [Theory]
    [InlineData("")]
    [InlineData("http://example.test/entries")]
    [InlineData("javascript:alert(1)")]
    [InlineData("https://user:password@example.test/entries")]
    [InlineData("https://api.jsonbin.io/v3/b/abc")]
    [InlineData("https://JSONBIN.IO/abc")]
    [InlineData("https://example.test/entries#token")]
    public void PublicEndpoint_RejectsUnsafeOrLegacyConfiguration(string url) => Assert.Null(EntryFormRules.PublicEndpoint(url));

    [Fact]
    public void PublicEndpoint_AcceptsHttps() => Assert.Equal("https://example.test/entries", EntryFormRules.PublicEndpoint(" https://example.test/entries "));

    [Fact]
    public void ImportIdentity_UsesExternalIdOrExactStablePayload()
    {
        var date = new DateTime(2026, 1, 20);
        var first = new Dictionary<string, string> { ["Name"] = "A", ["Team"] = "B" };
        var reordered = new Dictionary<string, string> { ["Team"] = "B", ["Name"] = "A" };
        string Key(Dictionary<string, string> values, DateTime? at = null) => EntryFormImportIdentity.GetKey(null, "form-1", "A", at ?? date, values);
        Assert.Equal(Key(first), Key(reordered));
        Assert.NotEqual(Key(first), Key(first, date.AddMinutes(1)));
        reordered["Team"] = "C";
        Assert.NotEqual(Key(first), Key(reordered));
        Assert.Equal("remote-id", EntryFormImportIdentity.GetKey("remote-id", null, null, null, null));
    }

    [Fact]
    public void PublicPages_DoNotContainPrivateCredentialsOrMasterKeyProtocol()
    {
        var (generator, settings, _) = Setup();
        settings.FormServiceUrl = "https://api.jsonbin.io/v3/b/legacy";
        settings.FormServiceFetchUrl = "https://private.example.test/secret-collection";
        settings.FormServiceApiToken = "PRIVATE-TOKEN-DO-NOT-PUBLISH";
        var files = generator.GenerateWebsite();
        foreach (var content in files.Values)
        {
            Assert.DoesNotContain(settings.FormServiceApiToken, content);
            Assert.DoesNotContain(settings.FormServiceFetchUrl, content);
            Assert.DoesNotContain("X-Master-Key", content);
        }
        Assert.Contains("Downloading does not submit an entry", files["entry-forms.html"]);
        Assert.DoesNotContain("localStorage.setItem", files["entry-forms.html"]);
    }

    [Fact]
    public void PublicForm_HostingSelectionRequiresAcknowledgedDelivery()
    {
        var (generator, settings, _) = Setup();
        settings.FormServiceUrl = "https://backend.example.test/api/entry-forms/submit.php";
        settings.FormServiceFetchUrl = "https://backend.example.test/api/admin/entry-form-submissions.php";
        EntryFormRules.SetHostedMode(settings, true);
        var pending = generator.GenerateWebsite()["entry-forms.html"];
        Assert.DoesNotContain(settings.FormServiceUrl, pending);
        Assert.Contains("Downloading does not submit an entry", pending);
        settings.HostedEntryFormsPublished = true;
        var published = generator.GenerateWebsite()["entry-forms.html"];
        Assert.Contains(settings.FormServiceUrl, published);
        Assert.DoesNotContain(settings.FormServiceFetchUrl, published);
    }

    [Fact]
    public void PublicForm_UsesLabelsRequiredControlsScopedIdsAndReview()
    {
        var (generator, _, form) = Setup();
        var html = generator.GenerateWebsite()["entry-forms.html"];
        var id = $"form-{form.Id:N}-field-{form.Fields[0].Id:N}";
        Assert.Contains($"for=\"{id}\"", html);
        Assert.Contains($"id=\"{id}\" name=\"Team Name\" data-entry-field=\"true\" required", html);
        Assert.Contains("autocomplete=\"email\"", html);
        Assert.Contains("role=\"status\"", html);
        Assert.Contains("entry-form-review", html);
        Assert.Contains("disabled>Review entry</button>", html);
        Assert.Contains("noscript", html);
    }

    [Fact]
    public void Config_SerializesSpecialCharactersWithoutScriptInjection()
    {
        var (generator, _, form) = Setup();
        form.ConfirmationMessage = "Thanks \"O'Brien\" & team\n</script><script>alert(1)</script>";
        form.Fields[0].Label = "Captain's \"name\" & team";
        var html = generator.GenerateWebsite()["entry-forms.html"];
        var match = Regex.Match(html, @"window.entryFormConfig\['form-[^']+'\] = (\{.*?\});</script>");
        Assert.True(match.Success);
        using var json = JsonDocument.Parse(match.Groups[1].Value);
        Assert.Equal(form.ConfirmationMessage, json.RootElement.GetProperty("confirmation").GetString());
        Assert.DoesNotContain("</script><script>alert(1)</script>", html);
        Assert.Contains("&amp; team", html);
    }

    [Fact]
    public void Preview_IsNonSubmittingAndDoesNotMutateDraft()
    {
        var (generator, _, form) = Setup();
        form.IsPublished = false;
        var before = JsonSerializer.Serialize(form);
        var html = generator.GenerateEntryFormPreview(form);
        Assert.Contains("data-preview=\"true\"", html);
        Assert.Contains("Nothing was sent or saved", html);
        Assert.Contains("@media (max-width: 640px)", html);
        Assert.Equal(before, JsonSerializer.Serialize(form));
    }

    [Theory]
    [InlineData("")]
    [InlineData("https://example.test/entries")]
    public void WebsitePreview_DisablesEveryForm_WithoutChangingPublicGeneration(string endpoint)
    {
        var (generator, settings, form) = Setup();
        settings.FormServiceUrl = endpoint;
        settings.FormServiceFetchUrl = "https://private.example.test/collection";
        settings.FormServiceApiToken = "PRIVATE-COLLECTION-TOKEN";
        settings.EntryForms.Add(EntryForm.CreateCompetitionEntryForm());
        form.Submissions.Add(new EntryFormSubmission { EntryName = "Existing entry" });
        form.ImportedExternalIds.Add("existing-id");
        var before = JsonSerializer.Serialize(settings.EntryForms);

        var preview = generator.GenerateWebsite(previewEntryForms: true);
        var html = preview["entry-forms.html"];
        Assert.Equal(2, Regex.Matches(html, "data-preview=\"true\"").Count);
        Assert.Equal(2, Regex.Matches(html, "data-endpoint=\"\"").Count);
        Assert.DoesNotContain("data-preview=\"false\"", html);
        Assert.Contains("Preview only — entries will not be sent, downloaded or saved", html);
        Assert.False(preview.ContainsKey("_submissions.html"));
        Assert.DoesNotContain("localStorage", html);
        foreach (var content in preview.Values)
        {
            Assert.DoesNotContain(settings.FormServiceFetchUrl, content);
            Assert.DoesNotContain(settings.FormServiceApiToken, content);
        }

        var published = generator.GenerateWebsite();
        Assert.Equal(2, Regex.Matches(published["entry-forms.html"], "data-preview=\"false\"").Count);
        Assert.Contains($"data-endpoint=\"{endpoint}\"", published["entry-forms.html"]);
        Assert.DoesNotContain("Preview only —", published["entry-forms.html"]);
        Assert.True(published.ContainsKey("_submissions.html"));
        Assert.Equal(before, JsonSerializer.Serialize(settings.EntryForms));
    }

    [Fact]
    public void WebsitePreview_PreservesPublicationAndClosedStatus()
    {
        var (generator, settings, form) = Setup();
        form.IsClosed = true;
        var draft = EntryForm.CreateCompetitionEntryForm();
        draft.IsPublished = false;
        settings.EntryForms.Add(draft);

        var html = generator.GenerateWebsite(previewEntryForms: true)["entry-forms.html"];
        Assert.Contains($"href=\"#form-{form.Id:N}\"", html);
        Assert.DoesNotContain($"form-{draft.Id:N}", html);
        Assert.Contains("now closed for entries", html);
        Assert.DoesNotContain("data-entry-field=", html);

        settings.ShowEntryForms = false;
        Assert.False(generator.GenerateWebsite(previewEntryForms: true).ContainsKey("entry-forms.html"));
    }

    [Fact]
    public void DraftAndWebsitePreviews_UseTheSameFieldsAsPublicForms_WithoutDeliveryEndpoints()
    {
        var (generator, settings, form) = Setup();
        settings.FormServiceUrl = "https://example.test/entries";
        var published = generator.GenerateWebsite()["entry-forms.html"];
        var websitePreview = generator.GenerateWebsite(previewEntryForms: true)["entry-forms.html"];
        var draftPreview = generator.GenerateEntryFormPreview(form);
        string Fields(string html) => Regex.Match(html, "<fieldset.*?</fieldset>", RegexOptions.Singleline).Value;

        Assert.NotEmpty(Fields(published));
        Assert.Equal(Fields(published), Fields(websitePreview));
        Assert.Equal(Fields(published), Fields(draftPreview));
        Assert.DoesNotContain(settings.FormServiceUrl, websitePreview);
        Assert.DoesNotContain(settings.FormServiceUrl, draftPreview);
    }

    [Fact]
    public void ClosedForm_DoesNotRenderInputs_AndShowsContact()
    {
        var (generator, settings, form) = Setup();
        form.IsClosed = true;
        settings.ContactEmail = "secretary@example.test";
        var html = generator.GenerateWebsite()["entry-forms.html"];
        Assert.DoesNotContain("data-entry-field=", html);
        Assert.Contains("now closed for entries", html);
        Assert.Contains("mailto:secretary@example.test", html);
    }

    [Fact]
    public void ConfiguredEndpoint_RequiresAcknowledgementAndPreservesRetryIdentity()
    {
        var (generator, settings, _) = Setup();
        settings.FormServiceUrl = "https://example.test/entries";
        var html = generator.GenerateWebsite()["entry-forms.html"];
        Assert.Contains("data-endpoint=\"https://example.test/entries\"", html);
        Assert.Contains("receipt.accepted !== true", html);
        Assert.Contains("receipt.id !== submission.id", html);
        Assert.Contains("controller.abort()", html);
        Assert.DoesNotContain("Downloading does not submit an entry", html);
    }

    [Fact]
    public void Directory_ListsOnlyPublishedFormsAndEscapesTitles()
    {
        var (generator, settings, form) = Setup();
        form.Title = "Team <entry> & registration";
        var draft = EntryForm.CreateCompetitionEntryForm();
        draft.IsPublished = false;
        settings.EntryForms.Add(draft);
        var html = generator.GenerateWebsite()["entry-forms.html"];
        Assert.Contains("aria-label=\"Choose an entry form\"", html);
        Assert.Contains($"href=\"#form-{form.Id:N}\"", html);
        Assert.DoesNotContain($"href=\"#form-{draft.Id:N}\"", html);
        Assert.Contains("Team &lt;entry&gt; &amp; registration", html);
    }

    [Theory]
    [InlineData("modern")]
    [InlineData("dark")]
    [InlineData("sport")]
    [InlineData("minimalist")]
    public void Preview_RendersScopedProgressAndResponsiveStyles(string template)
    {
        var (generator, settings, form) = Setup();
        settings.SelectedTemplate = template;
        var html = generator.GenerateEntryFormPreview(form);
        Assert.Contains($"for=\"form-{form.Id:N}-progress\"", html);
        Assert.Contains("aria-current=\"step\"", html);
        Assert.Contains("data-completion-label", html);
        Assert.Contains("entry-form-completion progress", html);
        Assert.Contains("prefers-reduced-motion", html);
        Assert.Contains("data-preview=\"true\"", html);
    }
}
