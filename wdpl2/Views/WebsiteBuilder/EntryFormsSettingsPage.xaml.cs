using System.Collections.ObjectModel;
using Microsoft.Maui.Controls;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.Views.WebsiteBuilder;

public partial class EntryFormsSettingsPage : ContentPage
{
    private static LeagueData League => DataStore.Data;
    private readonly ObservableCollection<FormDisplayItem> _forms = new();
    private readonly ObservableCollection<EntryDisplayItem> _entries = new();
    private readonly ObservableCollection<CrossRefItem> _crossRefItems = new();
    private Guid? _selectedFormId;
    private Guid? _selectedEntryId;

    public EntryFormsSettingsPage()
    {
        InitializeComponent();
        FormsCollection.ItemsSource = _forms;
        EntriesCollection.ItemsSource = _entries;
        CrossRefList.ItemsSource = _crossRefItems;
        ShowEntryFormsSwitch.IsToggled = League.WebsiteSettings.ShowEntryForms;
        LoadForms();
    }

    // ── Display items ───────────────────────────────────────────────────

    private sealed class FormDisplayItem
    {
        public Guid Id { get; set; }
        public string Title { get; set; } = "";
        public string FormType { get; set; } = "";
        public string TypeLabel => FormType switch
        {
            "team-entry" => "Team",
            "competition-entry" => "Comp",
            _ => "Custom"
        };
        public Color TypeBadgeColor => FormType switch
        {
            "team-entry" => Color.FromArgb("#10B981"),
            "competition-entry" => Color.FromArgb("#8B5CF6"),
            _ => Color.FromArgb("#3B82F6")
        };
        public bool IsPublished { get; set; }
        public bool IsClosed { get; set; }
        public string StatusIcon => IsClosed ? "\u26AA" : IsPublished ? "\u2705" : "\U0001F4DD";
        public string DateLabel { get; set; } = "";
        public string FieldCountLabel { get; set; } = "";

        public static FormDisplayItem FromModel(EntryForm form) => new()
        {
            Id = form.Id,
            Title = form.Title,
            FormType = form.FormType,
            IsPublished = form.IsPublished,
            IsClosed = form.IsClosed,
            DateLabel = $"Created {form.DateCreated:dd MMM yyyy}",
            FieldCountLabel = $"{form.Fields.Count} field{(form.Fields.Count == 1 ? "" : "s")}"
        };
    }

    private sealed class EntryDisplayItem
    {
        public Guid Id { get; set; }
        public string EntryName { get; set; } = "";
        public string Status { get; set; } = "pending";
        public string StatusLabel => Status switch
        {
            "confirmed" => "Confirmed",
            "rejected" => "Rejected",
            _ => "Pending"
        };
        public Color StatusColor => Status switch
        {
            "confirmed" => Color.FromArgb("#10B981"),
            "rejected" => Color.FromArgb("#EF4444"),
            _ => Color.FromArgb("#F59E0B")
        };
        public string DateLabel { get; set; } = "";
        public string LinkedTeamLabel { get; set; } = "";
    }

    private sealed class CrossRefItem
    {
        public Guid TeamId { get; set; }
        public string TeamName { get; set; } = "";
        public bool HasEntry { get; set; }
        public string StatusIcon => HasEntry ? "\u2705" : "\u274C";
        public string EntryLabel { get; set; } = "";
        public Color EntryLabelColor => HasEntry ? Color.FromArgb("#10B981") : Color.FromArgb("#EF4444");
    }

    // ── Loading ─────────────────────────────────────────────────────────

    private void LoadForms()
    {
        _forms.Clear();
        foreach (var form in League.WebsiteSettings.EntryForms.OrderBy(f => f.SortOrder).ThenByDescending(f => f.DateCreated))
            _forms.Add(FormDisplayItem.FromModel(form));
    }

    // ── Form list events ────────────────────────────────────────────────

    private void OnShowToggled(object? sender, ToggledEventArgs e)
    {
        League.WebsiteSettings.ShowEntryForms = e.Value;
        DataStore.Save();
    }

    private void OnAddTeamEntryClicked(object? sender, EventArgs e)
        => AddFormAndSelect(EntryForm.CreateTeamEntryForm());

    private void OnAddCompEntryClicked(object? sender, EventArgs e)
        => AddFormAndSelect(EntryForm.CreateCompetitionEntryForm());

    private void OnAddCustomClicked(object? sender, EventArgs e)
    {
        AddFormAndSelect(new EntryForm
        {
            Title = "New Entry Form",
            Description = "Please complete the form below.",
        });
    }

    private void AddFormAndSelect(EntryForm form)
    {
        form.SortOrder = League.WebsiteSettings.EntryForms.Count;
        League.WebsiteSettings.EntryForms.Add(form);
        DataStore.Save();

        var item = FormDisplayItem.FromModel(form);
        _forms.Add(item);
        FormsCollection.SelectedItem = item;
        SelectForm(form.Id);
    }

    private void OnFormSelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is FormDisplayItem item)
            SelectForm(item.Id);
    }

    private async void OnDeleteFormClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        if (!await DisplayAlert("Delete Form", $"Delete '{form.Title}'? This will also delete all {form.Submissions.Count} logged entries.", "Delete", "Cancel"))
            return;

        League.WebsiteSettings.EntryForms.Remove(form);
        DataStore.Save();
        _selectedFormId = null;
        _selectedEntryId = null;
        LoadForms();
        EditorForm.IsVisible = false;
        EditorPlaceholder.IsVisible = true;
        DeleteFormBtn.IsEnabled = false;
        DuplicateFormBtn.IsEnabled = false;
    }

    private void OnDuplicateFormClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var original = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (original == null) return;

        var copy = new EntryForm
        {
            Title = $"{original.Title} (Copy)",
            Description = original.Description,
            FormType = original.FormType,
            SubmitButtonText = original.SubmitButtonText,
            ConfirmationMessage = original.ConfirmationMessage,
            Fields = original.Fields.Select(f => new EntryFormField
            {
                Label = f.Label,
                FieldType = f.FieldType,
                IsRequired = f.IsRequired,
                Placeholder = f.Placeholder,
                Options = f.Options,
                SortOrder = f.SortOrder,
            }).ToList(),
        };
        AddFormAndSelect(copy);
    }

    // ── Form editor ─────────────────────────────────────────────────────

    private void SelectForm(Guid formId)
    {
        _selectedFormId = formId;
        _selectedEntryId = null;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == formId);
        if (form == null) return;

        TitleEntry.Text = form.Title;
        DescriptionEditor.Text = form.Description;
        SubmitButtonEntry.Text = form.SubmitButtonText;
        IsPublishedSwitch.IsToggled = form.IsPublished;
        IsClosedSwitch.IsToggled = form.IsClosed;

        if (form.ClosingDate.HasValue)
            ClosingDatePicker.Date = form.ClosingDate.Value;
        else
            ClosingDatePicker.Date = DateTime.Now.AddDays(30);

        EditorPlaceholder.IsVisible = false;
        EditorForm.IsVisible = true;
        DeleteFormBtn.IsEnabled = true;
        DuplicateFormBtn.IsEnabled = true;

        RebuildFieldsPanel(form);
        RefreshEntries(form);
        RefreshCrossReference(form);
    }

    private void RebuildFieldsPanel(EntryForm form)
    {
        FieldsPanel.Children.Clear();
        NoFieldsLabel.IsVisible = form.Fields.Count == 0;

        foreach (var field in form.Fields.OrderBy(f => f.SortOrder))
        {
            var card = new Frame
            {
                BorderColor = Color.FromArgb("#E2E8F0"),
                Padding = new Thickness(12),
                CornerRadius = 8,
                BackgroundColor = Colors.White,
                HasShadow = false,
            };

            var grid = new Grid
            {
                ColumnDefinitions = new ColumnDefinitionCollection
                {
                    new ColumnDefinition(GridLength.Star),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Auto),
                },
                ColumnSpacing = 8,
                RowDefinitions = new RowDefinitionCollection
                {
                    new RowDefinition(GridLength.Auto),
                    new RowDefinition(GridLength.Auto),
                },
                RowSpacing = 6,
            };

            var labelEntry = new Entry
            {
                Text = field.Label,
                Placeholder = "Field label",
                FontSize = 13,
                FontAttributes = FontAttributes.Bold,
            };
            var capturedField = field;
            labelEntry.TextChanged += (_, args) => capturedField.Label = args.NewTextValue;
            grid.Add(labelEntry, 0, 0);

            var typePicker = new Picker
            {
                FontSize = 12,
                WidthRequest = 110,
                ItemsSource = new[] { "text", "email", "phone", "number", "date", "textarea", "select", "checkbox" },
                SelectedItem = field.FieldType,
            };
            typePicker.SelectedIndexChanged += (_, _) =>
            {
                if (typePicker.SelectedItem is string t)
                    capturedField.FieldType = t;
            };
            grid.Add(typePicker, 1, 0);

            var requiredSwitch = new Switch { IsToggled = field.IsRequired };
            requiredSwitch.Toggled += (_, args) => capturedField.IsRequired = args.Value;
            var requiredStack = new HorizontalStackLayout { Spacing = 4 };
            requiredStack.Add(new Label { Text = "Req", FontSize = 11, TextColor = Color.FromArgb("#94A3B8"), VerticalOptions = LayoutOptions.Center });
            requiredStack.Add(requiredSwitch);
            grid.Add(requiredStack, 2, 0);

            var deleteBtn = new Button
            {
                Text = "\u2716",
                BackgroundColor = Colors.Transparent,
                TextColor = Color.FromArgb("#EF4444"),
                FontSize = 14,
                Padding = new Thickness(4),
                WidthRequest = 36,
                HeightRequest = 36,
            };
            deleteBtn.Clicked += (_, _) =>
            {
                form.Fields.Remove(capturedField);
                RebuildFieldsPanel(form);
            };
            grid.Add(deleteBtn, 3, 0);

            var row1 = new Grid { ColumnDefinitions = new ColumnDefinitionCollection { new(GridLength.Star), new(GridLength.Star) }, ColumnSpacing = 8 };
            var placeholderEntry = new Entry
            {
                Text = field.Placeholder,
                Placeholder = "Placeholder text",
                FontSize = 12,
            };
            placeholderEntry.TextChanged += (_, args) => capturedField.Placeholder = args.NewTextValue;
            row1.Add(placeholderEntry, 0, 0);

            var optionsEntry = new Entry
            {
                Text = field.Options,
                Placeholder = "Options (comma separated)",
                FontSize = 12,
                IsEnabled = field.FieldType == "select",
            };
            optionsEntry.TextChanged += (_, args) => capturedField.Options = args.NewTextValue;
            typePicker.SelectedIndexChanged += (_, _) =>
            {
                optionsEntry.IsEnabled = capturedField.FieldType == "select";
            };
            row1.Add(optionsEntry, 1, 0);

            Grid.SetColumnSpan(row1, 4);
            grid.Add(row1, 0, 1);

            card.Content = grid;
            FieldsPanel.Children.Add(card);
        }
    }

    private void OnAddFieldClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        form.Fields.Add(new EntryFormField
        {
            Label = "",
            FieldType = "text",
            IsRequired = false,
            SortOrder = form.Fields.Count,
        });
        RebuildFieldsPanel(form);
    }

    private void OnClearClosingDate(object? sender, EventArgs e)
    {
        ClosingDatePicker.Date = DateTime.Now.AddDays(30);
        if (_selectedFormId.HasValue)
        {
            var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
            if (form != null)
                form.ClosingDate = null;
        }
    }

    private void OnSaveFormClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        form.Title = TitleEntry.Text?.Trim() ?? "";
        form.Description = DescriptionEditor.Text?.Trim() ?? "";
        form.SubmitButtonText = SubmitButtonEntry.Text?.Trim() ?? "Submit Entry";
        form.IsPublished = IsPublishedSwitch.IsToggled;
        form.IsClosed = IsClosedSwitch.IsToggled;

        var pickerDate = ClosingDatePicker.Date;
        if (form.ClosingDate.HasValue || pickerDate.Date != DateTime.Now.AddDays(30).Date)
            form.ClosingDate = pickerDate;

        DataStore.Save();
        LoadForms();

        var item = _forms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (item != null)
            FormsCollection.SelectedItem = item;
    }

    // ── Entry (submission) management ───────────────────────────────────

    private void RefreshEntries(EntryForm form)
    {
        _entries.Clear();
        _selectedEntryId = null;
        EntryDetailPanel.IsVisible = false;
        DeleteEntryBtn.IsEnabled = false;

        var teamLookup = GetSeasonTeamLookup();

        foreach (var sub in form.Submissions.OrderByDescending(s => s.SubmittedDate))
        {
            var linkedLabel = "";
            if (sub.LinkedTeamId.HasValue && teamLookup.TryGetValue(sub.LinkedTeamId.Value, out var teamName))
                linkedLabel = $"\U0001F517 {teamName}";

            _entries.Add(new EntryDisplayItem
            {
                Id = sub.Id,
                EntryName = string.IsNullOrWhiteSpace(sub.EntryName) ? "(unnamed)" : sub.EntryName,
                Status = sub.Status,
                DateLabel = sub.SubmittedDate.ToString("dd MMM yyyy HH:mm"),
                LinkedTeamLabel = linkedLabel,
            });
        }

        EntriesHeaderLabel.Text = $"ENTRIES ({form.Submissions.Count})";
    }

    private void OnAddEntryClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        var submission = new EntryFormSubmission();

        // Pre-populate field values from form fields
        foreach (var field in form.Fields.OrderBy(f => f.SortOrder))
            submission.FieldValues[field.Label] = "";

        form.Submissions.Add(submission);
        DataStore.Save();
        RefreshEntries(form);
        RefreshCrossReference(form);

        // Select the new entry
        var item = _entries.FirstOrDefault(e => e.Id == submission.Id);
        if (item != null)
            EntriesCollection.SelectedItem = item;
        LoadEntryDetail(form, submission);
    }

    private void OnEntrySelected(object? sender, SelectionChangedEventArgs e)
    {
        if (e.CurrentSelection.FirstOrDefault() is not EntryDisplayItem item) return;
        if (!_selectedFormId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;

        var sub = form.Submissions.FirstOrDefault(s => s.Id == item.Id);
        if (sub == null) return;

        LoadEntryDetail(form, sub);
    }

    private void LoadEntryDetail(EntryForm form, EntryFormSubmission sub)
    {
        _selectedEntryId = sub.Id;
        DeleteEntryBtn.IsEnabled = true;
        EntryDetailPanel.IsVisible = true;

        EntryNameEntry.Text = sub.EntryName;
        EntryNotesEditor.Text = sub.Notes;

        // Status picker
        EntryStatusPicker.SelectedItem = sub.Status;

        // Team picker
        var seasonId = SeasonService.Current.CurrentSeasonId;
        var teams = League.Teams?
            .Where(t => t != null && (seasonId == null || t.SeasonId == seasonId))
            .OrderBy(t => t.Name ?? "")
            .ToList() ?? [];

        EntryTeamPicker.ItemsSource = teams;
        EntryTeamPicker.ItemDisplayBinding = new Binding("Name");
        EntryTeamPicker.SelectedItem = sub.LinkedTeamId.HasValue
            ? teams.FirstOrDefault(t => t.Id == sub.LinkedTeamId)
            : null;

        // Dynamic field values
        RebuildEntryFieldsPanel(form, sub);
    }

    private void RebuildEntryFieldsPanel(EntryForm form, EntryFormSubmission sub)
    {
        EntryFieldsPanel.Children.Clear();

        if (form.Fields.Count == 0) return;

        EntryFieldsPanel.Children.Add(new Label
        {
            Text = "FIELD VALUES",
            FontSize = 11,
            FontAttributes = FontAttributes.Bold,
            TextColor = Color.FromArgb("#94A3B8"),
            CharacterSpacing = 1.5,
        });

        foreach (var field in form.Fields.OrderBy(f => f.SortOrder))
        {
            var capturedLabel = field.Label;
            var currentValue = sub.FieldValues.TryGetValue(capturedLabel, out var val) ? val : "";

            var stack = new VerticalStackLayout { Spacing = 3 };
            stack.Add(new Label
            {
                Text = field.Label,
                FontSize = 11,
                FontAttributes = FontAttributes.Bold,
                TextColor = Color.FromArgb("#475569"),
            });

            var entry = new Entry
            {
                Text = currentValue,
                Placeholder = field.Placeholder,
                FontSize = 12,
            };
            entry.TextChanged += (_, args) => sub.FieldValues[capturedLabel] = args.NewTextValue ?? "";
            stack.Add(entry);
            EntryFieldsPanel.Children.Add(stack);
        }
    }

    private void OnSaveEntryClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue || !_selectedEntryId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;
        var sub = form.Submissions.FirstOrDefault(s => s.Id == _selectedEntryId);
        if (sub == null) return;

        sub.EntryName = EntryNameEntry.Text?.Trim() ?? "";
        sub.Status = EntryStatusPicker.SelectedItem as string ?? "pending";
        sub.Notes = EntryNotesEditor.Text?.Trim() ?? "";
        sub.LinkedTeamId = (EntryTeamPicker.SelectedItem as Team)?.Id;

        DataStore.Save();
        RefreshEntries(form);
        RefreshCrossReference(form);

        // Re-select
        var item = _entries.FirstOrDefault(e => e.Id == _selectedEntryId);
        if (item != null)
            EntriesCollection.SelectedItem = item;
    }

    private async void OnDeleteEntryClicked(object? sender, EventArgs e)
    {
        if (!_selectedFormId.HasValue || !_selectedEntryId.HasValue) return;
        var form = League.WebsiteSettings.EntryForms.FirstOrDefault(f => f.Id == _selectedFormId);
        if (form == null) return;
        var sub = form.Submissions.FirstOrDefault(s => s.Id == _selectedEntryId);
        if (sub == null) return;

        var name = string.IsNullOrWhiteSpace(sub.EntryName) ? "this entry" : $"'{sub.EntryName}'";
        if (!await DisplayAlert("Delete Entry", $"Delete {name}?", "Delete", "Cancel"))
            return;

        form.Submissions.Remove(sub);
        _selectedEntryId = null;
        DataStore.Save();
        RefreshEntries(form);
        RefreshCrossReference(form);
    }

    // ── Team cross-reference ────────────────────────────────────────────

    private void RefreshCrossReference(EntryForm form)
    {
        _crossRefItems.Clear();

        var seasonId = SeasonService.Current.CurrentSeasonId;
        var teams = League.Teams?
            .Where(t => t != null && (seasonId == null || t.SeasonId == seasonId))
            .OrderBy(t => t.Name ?? "")
            .ToList() ?? [];

        if (teams.Count == 0)
        {
            CrossRefSummaryLabel.Text = "No teams in current season";
            return;
        }

        // Build set of team IDs that have a linked submission
        var linkedTeamIds = form.Submissions
            .Where(s => s.LinkedTeamId.HasValue && s.Status != "rejected")
            .Select(s => s.LinkedTeamId!.Value)
            .ToHashSet();

        // Also try name-matching for unlinked entries
        var entryNamesByTeam = new Dictionary<Guid, string>();
        foreach (var team in teams)
        {
            if (linkedTeamIds.Contains(team.Id))
            {
                var sub = form.Submissions.First(s => s.LinkedTeamId == team.Id && s.Status != "rejected");
                entryNamesByTeam[team.Id] = sub.Status == "confirmed" ? "Confirmed" : "Pending";
            }
            else
            {
                // Try name matching (entry name vs team name)
                var nameMatch = form.Submissions.FirstOrDefault(s =>
                    s.Status != "rejected" &&
                    !s.LinkedTeamId.HasValue &&
                    !string.IsNullOrWhiteSpace(s.EntryName) &&
                    string.Equals(s.EntryName.Trim(), team.Name?.Trim(), StringComparison.OrdinalIgnoreCase));

                if (nameMatch != null)
                    entryNamesByTeam[team.Id] = $"Name match ({(nameMatch.Status == "confirmed" ? "Confirmed" : "Pending")})";
            }
        }

        int matched = 0;
        foreach (var team in teams)
        {
            var hasEntry = entryNamesByTeam.ContainsKey(team.Id);
            if (hasEntry) matched++;

            _crossRefItems.Add(new CrossRefItem
            {
                TeamId = team.Id,
                TeamName = team.Name ?? "Unknown",
                HasEntry = hasEntry,
                EntryLabel = hasEntry ? entryNamesByTeam[team.Id] : "No entry",
            });
        }

        CrossRefSummaryLabel.Text = $"{matched} of {teams.Count} teams have entries";
    }

    private Dictionary<Guid, string> GetSeasonTeamLookup()
    {
        var seasonId = SeasonService.Current.CurrentSeasonId;
        return League.Teams?
            .Where(t => t != null && (seasonId == null || t.SeasonId == seasonId))
            .ToDictionary(t => t.Id, t => t.Name ?? "Unknown") ?? new Dictionary<Guid, string>();
    }
}
