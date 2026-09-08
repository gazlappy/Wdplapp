using System.Text;
using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services;

public partial class WebsiteGenerator
{
    /// <summary>A non-submitting preview using the same markup and styles as the public website.</summary>
    public string GenerateEntryFormPreview(EntryForm draft)
    {
        var html = new StringBuilder("<!doctype html><html lang=\"en\"><head><meta charset=\"utf-8\"><meta name=\"viewport\" content=\"width=device-width, initial-scale=1\"><style>");
        html.Append(_settings.SelectedTemplate switch
        {
            "dark" => GenerateDarkModeCSS(), "sport" => GenerateSportCSS(),
            "minimalist" => GenerateMinimalistCSS(), _ => GenerateModernCSS()
        });
        html.AppendLine("</style></head><body><main class=\"container\"><p class=\"entry-form-notice\">Preview only — nothing will be sent or saved.</p>");
        AppendEntryFormCard(html, draft, preview: true);
        AppendEntryFormSubmitScript(html, "");
        html.Append("</main></body></html>");
        return html.ToString();
    }

    private void AppendEntryFormCard(StringBuilder html, EntryForm form, bool preview = false)
    {
        var id = $"form-{form.Id:N}";
        var closed = EntryFormRules.IsClosed(form, DateTime.Today);
        var endpoint = EntryFormRules.DeliveryEndpoint(_settings);
        var titleId = $"{id}-title";
        html.AppendLine($"<section class=\"section entry-form-card\" id=\"{id}\" aria-labelledby=\"{titleId}\">");
        html.AppendLine("<div class=\"entry-form-intro\">");
        if (form.LogoImageData is { Length: > 0 })
        {
            var image = new ImageOptimizationService();
            html.AppendLine($"<div class=\"entry-form-logo\"><img src=\"{image.ToDataUrl(form.LogoImageData, image.GetMimeType("logo.png"))}\" alt=\"\"></div>");
        }
        var category = form.FormType switch { "team-entry" => "Team registration", "competition-entry" => "Competition entry", _ => "League form" };
        html.AppendLine($"<p class=\"entry-form-eyebrow\">{category}</p><div class=\"entry-form-header\"><h3 id=\"{titleId}\">{Esc(form.Title)}</h3><span class=\"entry-form-badge {(closed ? "closed" : "open")}\">{(closed ? "Closed" : "Open")}</span></div>");
        if (!string.IsNullOrWhiteSpace(form.Description)) html.AppendLine($"<p class=\"entry-form-desc\">{Esc(form.Description)}</p>");
        if (form.ClosingDate.HasValue) html.AppendLine($"<p class=\"entry-form-deadline\">Entries close after <strong>{form.ClosingDate:dddd dd MMMM yyyy}</strong></p>");
        html.AppendLine("</div>");
        if (closed)
        {
            html.AppendLine("<p class=\"entry-form-closed-msg\">This form is now closed for entries. Contact the league secretary if you need help.</p>");
        }
        else if (EntryFormRules.Validate(form).Count != 0)
        {
            html.AppendLine("<p class=\"entry-form-notice\">This form is not ready to accept entries. Please contact the league secretary.</p>");
        }
        else
        {
            html.AppendLine($"<form class=\"entry-form\" id=\"{id}-form\" data-form-id=\"{id}\" data-preview=\"{preview.ToString().ToLowerInvariant()}\" data-endpoint=\"{Esc(preview ? "" : endpoint ?? "")}\" data-closing-date=\"{form.ClosingDate:yyyy-MM-dd}\" onsubmit=\"return handleEntrySubmit(this)\">");
            html.AppendLine($"<ol class=\"entry-form-steps\" aria-label=\"Entry process\"><li data-entry-step=\"1\" aria-current=\"step\"><span>01</span> Complete</li><li data-entry-step=\"2\"><span>02</span> Review</li><li data-entry-step=\"3\"><span>03</span> {(preview ? "Test" : endpoint == null ? "Download &amp; send" : "Send")}</li></ol>");
            html.AppendLine("<div class=\"entry-form-completion\"><label for=\"" + id + "-progress\" data-completion-label>Complete the required details</label><progress id=\"" + id + "-progress\" max=\"100\" value=\"0\">0%</progress></div>");
            html.AppendLine("<p class=\"entry-form-hint\">Fields marked <span class=\"required\">*</span> are required. Review your details before sending.</p>");
            if (endpoint == null) html.AppendLine("<p class=\"entry-form-notice\">Online delivery is not configured. Download your completed entry and send the file to the league secretary. Downloading does not submit an entry.</p>");
            html.AppendLine("<noscript>JavaScript is required to review and send this form. Contact the league secretary for an alternative.</noscript>");
            html.AppendLine($"<fieldset class=\"entry-form-fields\"><legend class=\"entry-form-sr-only\">{Esc(form.Title)} details</legend>");
            foreach (var field in form.Fields.OrderBy(f => f.SortOrder))
            {
                var fieldId = $"{id}-field-{field.Id:N}";
                var required = field.IsRequired ? " required" : "";
                var marker = field.IsRequired ? " <span class=\"required\" aria-hidden=\"true\">*</span>" : " <span class=\"entry-form-optional\">(optional)</span>";
                var attributes = $"id=\"{fieldId}\" name=\"{Esc(field.Label)}\" data-entry-field=\"true\"{required}";
                html.AppendLine($"<div class=\"form-group{(field.FieldType is "textarea" or "checkbox" ? " entry-form-wide" : "")}\">");
                if (field.FieldType != "checkbox") html.AppendLine($"<label for=\"{fieldId}\">{Esc(field.Label)}{marker}</label>");
                switch (field.FieldType)
                {
                    case "textarea":
                        html.AppendLine($"<textarea {attributes} rows=\"4\" maxlength=\"4000\" placeholder=\"{Esc(field.Placeholder)}\"></textarea>");
                        break;
                    case "select":
                        html.AppendLine($"<select {attributes}><option value=\"\">Choose an option</option>");
                        foreach (var option in field.Options.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                            html.AppendLine($"<option value=\"{Esc(option)}\">{Esc(option)}</option>");
                        html.AppendLine("</select>");
                        break;
                    case "checkbox":
                        html.AppendLine($"<label for=\"{fieldId}\" class=\"checkbox-label\"><input type=\"checkbox\" {attributes}><span>{Esc(field.Label)}{marker}</span></label>");
                        break;
                    default:
                        var type = field.FieldType switch { "phone" => "tel", "email" => "email", "number" => "number", "date" => "date", _ => "text" };
                        var autocomplete = type switch { "email" => " autocomplete=\"email\"", "tel" => " autocomplete=\"tel\"", _ => "" };
                        html.AppendLine($"<input type=\"{type}\" {attributes}{autocomplete} maxlength=\"500\" placeholder=\"{Esc(field.Placeholder)}\">");
                        break;
                }
                html.AppendLine("</div>");
            }
            html.AppendLine("</fieldset><div class=\"entry-form-review\" hidden tabindex=\"-1\"><h4>Check your entry</h4><p>Please check the details below. Nothing has been sent yet.</p><dl></dl></div>");
            html.AppendLine("<p class=\"entry-form-feedback\" role=\"status\" aria-live=\"polite\" tabindex=\"-1\"></p><div class=\"entry-form-actions\"><button type=\"submit\" class=\"entry-form-submit\" disabled>Review entry</button><button type=\"button\" class=\"entry-form-secondary\" data-edit hidden onclick=\"editEntryForm(this.form)\">Back to edit</button></div>");
            html.AppendLine("<p class=\"entry-form-hint\">Entries are reviewed by the league secretary; sending is not confirmation of a place. Details are not automatically stored in this browser.</p></form>");
            // JSON serialization escapes script delimiters, quotes, ampersands and newlines correctly.
            html.AppendLine($"<script>window.entryFormConfig = window.entryFormConfig || {{}}; window.entryFormConfig['{id}'] = {JsonSerializer.Serialize(new { submitText = string.IsNullOrWhiteSpace(form.SubmitButtonText) ? "Submit entry" : form.SubmitButtonText, confirmation = form.ConfirmationMessage })};</script>");
        }
        if (!string.IsNullOrWhiteSpace(_settings.ContactEmail) || !string.IsNullOrWhiteSpace(_settings.ContactPhone))
        {
            html.AppendLine("<aside class=\"entry-form-contact\"><strong>Need a hand?</strong><p>Contact the league secretary.</p>");
            if (!string.IsNullOrWhiteSpace(_settings.ContactEmail)) html.AppendLine($"<p><a href=\"mailto:{Esc(_settings.ContactEmail)}\">{Esc(_settings.ContactEmail)}</a></p>");
            if (!string.IsNullOrWhiteSpace(_settings.ContactPhone)) html.AppendLine($"<p><a href=\"tel:{Esc(_settings.ContactPhone)}\">{Esc(_settings.ContactPhone)}</a></p>");
            html.AppendLine("</aside>");
        }
        html.AppendLine("</section>");
    }

    private static void AppendEntryFormSubmitScript(StringBuilder html, string indent)
    {
        html.AppendLine("""
<script>
function entryStage(form, stage) {
    form.querySelectorAll('[data-entry-step]').forEach(function(item) {
        var step = Number(item.dataset.entryStep);
        if (step === stage) item.setAttribute('aria-current', 'step');
        else item.removeAttribute('aria-current');
        item.classList.toggle('is-complete', step < stage);
    });
}
function entryCompletion(form) {
    var required = Array.from(form.querySelectorAll('[data-entry-field]')).filter(function(el) { return el.required; });
    var completed = required.filter(function(el) {
        return (el.type === 'checkbox' ? el.checked : el.value.trim().length > 0) && el.validity.valid;
    }).length;
    var progress = form.querySelector('progress');
    progress.value = required.length ? Math.round(completed * 100 / required.length) : 100;
    form.querySelector('[data-completion-label]').textContent = required.length
        ? completed + ' of ' + required.length + ' required fields complete'
        : 'All fields are optional — review before sending';
}
function entryFeedback(form, message, error) {
    var feedback = form.querySelector('.entry-form-feedback');
    feedback.textContent = message;
    feedback.classList.toggle('is-error', !!error);
    feedback.focus();
}
function editEntryForm(form) {
    if (form.dataset.busy === 'true' || form.dataset.attempted === 'true') return;
    delete form.dataset.submissionId;
    delete form.dataset.submittedAt;
    form.dataset.reviewed = 'false';
    entryStage(form, 1);
    form.querySelector('fieldset').disabled = false;
    form.querySelector('fieldset').hidden = false;
    form.querySelector('.entry-form-review').hidden = true;
    form.querySelector('[data-edit]').hidden = true;
    form.querySelector('[type=submit]').textContent = 'Review entry';
    form.querySelector('.entry-form-feedback').textContent = '';
    var first = form.querySelector('[data-entry-field]');
    if (first) first.focus();
}
function handleEntrySubmit(form) {
    if (form.dataset.busy === 'true' || form.dataset.sent === 'true') return false;
    var deadline = form.dataset.closingDate;
    var now = new Date();
    var today = now.getFullYear() + '-' + String(now.getMonth() + 1).padStart(2, '0') + '-' + String(now.getDate()).padStart(2, '0');
    if (deadline && today > deadline) { entryFeedback(form, 'This form is now closed. Please contact the league secretary.', true); return false; }
    var fields = Array.from(form.querySelectorAll('[data-entry-field]'));
    if (form.dataset.reviewed !== 'true') {
        fields.forEach(function(el) { if (el.type !== 'checkbox' && el.tagName !== 'SELECT') el.value = el.value.trim(); });
        if (!form.reportValidity()) return false;
        var list = form.querySelector('.entry-form-review dl');
        list.replaceChildren();
        fields.forEach(function(el) {
            var term = document.createElement('dt'); term.textContent = el.name;
            var value = document.createElement('dd'); value.textContent = el.type === 'checkbox' ? (el.checked ? 'Yes' : 'No') : (el.value || 'Not provided');
            list.append(term, value);
        });
        form.querySelector('fieldset').disabled = true;
        form.querySelector('fieldset').hidden = true;
        var review = form.querySelector('.entry-form-review'); review.hidden = false; review.focus();
        form.querySelector('[data-edit]').hidden = false;
        var config = window.entryFormConfig[form.dataset.formId];
        form.querySelector('[type=submit]').textContent = form.dataset.preview === 'true' ? 'Test confirmation' : (form.dataset.endpoint ? config.submitText : 'Download entry');
        form.dataset.reviewed = 'true';
        entryStage(form, 2);
        entryCompletion(form);
        return false;
    }
    if (form.dataset.preview === 'true') {
        entryStage(form, 3);
        entryFeedback(form, 'Preview complete. Nothing was sent or saved.', false);
        return false;
    }
    var values = Object.create(null);
    fields.forEach(function(el) { values[el.name] = el.type === 'checkbox' ? (el.checked ? 'Yes' : 'No') : el.value; });
    // Keep the same identity across retries and repeated downloads; never persist personal data automatically.
    if (!form.dataset.submissionId) form.dataset.submissionId = typeof crypto !== 'undefined' && crypto.randomUUID ? crypto.randomUUID() : Date.now().toString(36) + Math.random().toString(36).slice(2);
    if (!form.dataset.submittedAt) form.dataset.submittedAt = new Date().toISOString();
    var submission = { id: form.dataset.submissionId, formId: form.dataset.formId, name: fields.length ? values[fields[0].name] : '', values: values, submittedAt: form.dataset.submittedAt };
    if (!form.dataset.endpoint) {
        try {
            var url = URL.createObjectURL(new Blob([JSON.stringify([submission], null, 2)], { type: 'application/json' }));
            var link = document.createElement('a'); link.href = url; link.download = 'league-entry-' + submission.id + '.json';
            document.body.appendChild(link); link.click(); link.remove();
            setTimeout(function() { URL.revokeObjectURL(url); }, 1000);
            entryFeedback(form, 'Download requested — your entry has NOT been submitted. Send the downloaded JSON file to the league secretary.', false);
            entryStage(form, 3);
        } catch (error) { entryFeedback(form, 'The download could not be started. Your details are still here; please try again.', true); }
        return false;
    }
    form.dataset.busy = 'true'; form.dataset.attempted = 'true'; form.setAttribute('aria-busy', 'true');
    entryStage(form, 3);
    var button = form.querySelector('[type=submit]'); var originalText = button.textContent;
    button.disabled = true; button.textContent = 'Sending…';
    form.querySelector('[data-edit]').disabled = true;
    var controller = new AbortController(); var timeout = setTimeout(function() { controller.abort(); }, 30000);
    fetch(form.dataset.endpoint, {
        method: 'POST', credentials: 'omit', referrerPolicy: 'no-referrer', signal: controller.signal,
        headers: { 'Content-Type': 'application/json', 'Accept': 'application/json' }, body: JSON.stringify(submission)
    }).then(function(response) {
        if (!response.ok) throw new Error('Delivery failed');
        return response.json();
    }).then(function(receipt) {
        if (!receipt || receipt.accepted !== true || receipt.id !== submission.id) throw new Error('Delivery was not acknowledged');
        form.dataset.sent = 'true';
        entryStage(form, 4);
        button.hidden = true; form.querySelector('[data-edit]').hidden = true;
        var config = window.entryFormConfig[form.dataset.formId];
        entryFeedback(form, (config.confirmation || 'Thank you for your entry!') + ' Received for review. Reference: ' + submission.id, false);
    }).catch(function() {
        entryFeedback(form, 'Delivery could not be confirmed. Your details are still here. Retry with the same reference, or contact the league secretary. Reference: ' + submission.id, true);
    }).finally(function() {
        clearTimeout(timeout); form.dataset.busy = 'false'; form.removeAttribute('aria-busy');
        button.disabled = false; button.textContent = originalText;
    });
    return false;
}
document.querySelectorAll('.entry-form').forEach(function(form) {
    if (form.dataset.initialized === 'true') return;
    form.dataset.initialized = 'true';
    form.addEventListener('input', function() { entryCompletion(form); });
    form.addEventListener('change', function() { entryCompletion(form); });
    form.querySelector('[type=submit]').disabled = false;
    entryCompletion(form);
});
</script>
""");
    }
}
