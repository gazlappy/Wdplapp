using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>Shared rules for private editor drafts and static website forms.</summary>
public static class EntryFormRules
{
    public static EntryForm CreateDraft(EntryForm source) => new()
    {
        Id = source.Id,
        Title = source.Title,
        Description = source.Description,
        FormType = source.FormType,
        DateCreated = source.DateCreated,
        IsPublished = source.IsPublished,
        IsClosed = source.IsClosed,
        ClosingDate = source.ClosingDate,
        SubmitButtonText = source.SubmitButtonText,
        ConfirmationMessage = source.ConfirmationMessage,
        SortOrder = source.SortOrder,
        LogoImageData = source.LogoImageData?.ToArray(),
        Fields = source.Fields.Select(f => new EntryFormField
        {
            Id = f.Id, Label = f.Label, FieldType = f.FieldType, IsRequired = f.IsRequired,
            Placeholder = f.Placeholder, Options = f.Options, SortOrder = f.SortOrder
        }).ToList()
        // Submissions and imported IDs are deliberately not part of an editable draft.
    };

    public static bool IsClosed(EntryForm form, DateTime today) =>
        form.IsClosed || (form.ClosingDate.HasValue && form.ClosingDate.Value.Date < today.Date);

    public static string Status(EntryForm form, DateTime today) =>
        !form.IsPublished ? "Draft" : IsClosed(form, today) ? "Closed" : "Open";

    public static IReadOnlyList<string> Validate(EntryForm form)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(form.Title)) errors.Add("Give the form a title.");
        if (form.Fields.Count == 0) errors.Add("Add at least one field.");
        var labels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var field in form.Fields.OrderBy(f => f.SortOrder))
        {
            var label = field.Label.Trim();
            if (string.IsNullOrWhiteSpace(label)) errors.Add("Every field needs a label.");
            else if (!labels.Add(label)) errors.Add($"Field labels must be unique: {label}.");
            if (label.StartsWith('_')) errors.Add($"Field label '{label}' cannot start with an underscore (reserved for submission metadata).");
            if (field.FieldType is not ("text" or "email" or "phone" or "number" or "date" or "textarea" or "select" or "checkbox"))
                errors.Add($"Choose a supported field type for '{label}'.");
            if (field.FieldType == "select" && string.IsNullOrWhiteSpace(field.Options.Trim(' ', ',')))
                errors.Add($"Add at least one dropdown option for '{label}'.");
        }
        return errors;
    }

    public static void SetHostedMode(WebsiteSettings settings, bool enabled)
    {
        settings.HostedEntryFormsPublished ??= IsHostedDeliveryReady(settings);
        settings.UseHostedEntryForms = enabled;
    }

    public static bool IsHostedDeliveryReady(WebsiteSettings settings)
    {
        if (!settings.UseHostedEntryForms || settings.HostedEntryFormsPublished == false ||
            PublicEndpoint(settings.FormServiceUrl) is not { } endpoint) return false;
        var submission = new Uri(endpoint);
        if (!string.IsNullOrEmpty(submission.Query) ||
            !submission.AbsolutePath.EndsWith("/entry-forms/submit.php", StringComparison.Ordinal)) return false;
        return string.Equals(new Uri(submission, "../admin/entry-form-submissions.php").AbsoluteUri,
            settings.FormServiceFetchUrl, StringComparison.Ordinal);
    }

    public static string? DeliveryEndpoint(WebsiteSettings settings) =>
        settings.UseHostedEntryForms && !IsHostedDeliveryReady(settings) ? null : PublicEndpoint(settings.FormServiceUrl);

    /// <summary>Public pages must never embed a private collection token or use JSONBin read/modify/write.</summary>
    public static string? PublicEndpoint(string? url)
    {
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Fragment) ||
            uri.Host.Equals("jsonbin.io", StringComparison.OrdinalIgnoreCase) ||
            uri.Host.EndsWith(".jsonbin.io", StringComparison.OrdinalIgnoreCase)) return null;
        return uri.AbsoluteUri;
    }
}
