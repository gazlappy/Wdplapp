# MVVM Migration Template - VenuesPage Example

## ?? Current State Analysis

**VenuesPage.xaml.cs** is a typical code-behind page with:
- ? Simple CRUD operations (perfect for MVVM)
- ? ObservableCollection already used
- ? Season-aware filtering
- ? Search functionality
- ?? Mixed concerns (UI + business logic)
- ?? Hard to test
- ?? Direct DataStore access

## ?? Migration Plan

### Step 1: Create VenuesViewModel (30 minutes)

```csharp
// wdpl2/ViewModels/VenuesViewModel.cs
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Wdpl2.Models;
using Wdpl2.Services;

namespace Wdpl2.ViewModels;

public partial class VenuesViewModel : ObservableObject
{
    private readonly IDataStore _dataStore;
    
    [ObservableProperty]
    private ObservableCollection<Venue> _venues = new();
    
    [ObservableProperty]
    private Venue? _selectedVenue;
    
    [ObservableProperty]
    private string _searchText = "";
    
    [ObservableProperty]
    private string _statusMessage = "";
    
    [ObservableProperty]
    private Guid? _currentSeasonId;
    
    [ObservableProperty]
    private bool _isLoading;
    
    // Editor properties
    [ObservableProperty]
    private string _venueName = "";
    
    [ObservableProperty]
    private string _address = "";
    
    [ObservableProperty]
    private string _notes = "";
    
    [ObservableProperty]
    private ObservableCollection<VenueTable> _tables = new();
    
    [ObservableProperty]
    private string _newTableName = "";

    public VenuesViewModel(IDataStore dataStore)
    {
        _dataStore = dataStore;
        SeasonService.SeasonChanged += OnSeasonChanged;
        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        _currentSeasonId = SeasonService.CurrentSeasonId;
        await LoadVenuesAsync();
    }

    private void OnSeasonChanged(object? sender, SeasonChangedEventArgs e)
    {
        _currentSeasonId = e.NewSeasonId;
        _ = LoadVenuesAsync();
        _statusMessage = $"Season: {e.NewSeason?.Name ?? "None"}";
    }

    [RelayCommand]
    private async Task LoadVenuesAsync()
    {
        _isLoading = true;
        
        try
        {
            if (!_currentSeasonId.HasValue)
            {
                _statusMessage = "No season selected";
                _venues.Clear();
                return;
            }

            var allVenues = _dataStore.GetData().Venues
                .Where(v => v.SeasonId == _currentSeasonId)
                .OrderBy(v => v.Name)
                .ToList();

            if (!string.IsNullOrWhiteSpace(_searchText))
            {
                var lower = _searchText.ToLower();
                allVenues = allVenues
                    .Where(v => (v.Name ?? "").ToLower().Contains(lower))
                    .ToList();
            }

            _venues.Clear();
            foreach (var venue in allVenues)
                _venues.Add(venue);

            _statusMessage = $"{_venues.Count} venue(s)";
        }
        finally
        {
            _isLoading = false;
        }
    }

    [RelayCommand]
    private async Task SearchVenuesAsync(string searchText)
    {
        _searchText = searchText ?? "";
        await LoadVenuesAsync();
    }

    [RelayCommand]
    private async Task AddVenueAsync()
    {
        if (string.IsNullOrWhiteSpace(_venueName))
        {
            _statusMessage = "Venue name required";
            return;
        }

        if (!_currentSeasonId.HasValue)
        {
            _statusMessage = "Please select a season first";
            return;
        }

        var venue = new Venue
        {
            SeasonId = _currentSeasonId.Value,
            Name = _venueName.Trim(),
            Address = _address?.Trim(),
            Notes = _notes?.Trim(),
            Tables = new System.Collections.Generic.List<VenueTable>()
        };

        await _dataStore.AddVenueAsync(venue);
        await _dataStore.SaveAsync();
        await LoadVenuesAsync();

        ClearEditor();
        _statusMessage = $"Added: {venue.Name}";
    }

    [RelayCommand]
    private async Task UpdateVenueAsync()
    {
        if (_selectedVenue == null)
        {
            _statusMessage = "No venue selected";
            return;
        }

        _selectedVenue.Name = _venueName?.Trim() ?? "";
        _selectedVenue.Address = _address?.Trim();
        _selectedVenue.Notes = _notes?.Trim();

        await _dataStore.SaveAsync();
        await LoadVenuesAsync();

        _statusMessage = $"Updated: {_selectedVenue.Name}";
    }

    [RelayCommand]
    private async Task DeleteVenueAsync(Venue? venue)
    {
        if (venue == null)
        {
            _statusMessage = "No venue selected";
            return;
        }

        await _dataStore.DeleteVenueAsync(venue);
        await _dataStore.SaveAsync();
        await LoadVenuesAsync();

        ClearEditor();
        _statusMessage = "Deleted venue";
    }

    [RelayCommand]
    private void AddTable()
    {
        if (_selectedVenue == null || string.IsNullOrWhiteSpace(_newTableName))
        {
            _statusMessage = "Please select a venue and enter table name";
            return;
        }

        var table = new VenueTable
        {
            Label = _newTableName.Trim(),
            MaxTeams = 2
        };

        _selectedVenue.Tables.Add(table);
        _tables.Add(table);
        _newTableName = "";
        _statusMessage = $"Added table: {table.Label}";
    }

    [RelayCommand]
    private void RemoveTable(VenueTable? table)
    {
        if (table == null || _selectedVenue == null)
        {
            _statusMessage = "Please select a table to remove";
            return;
        }

        _selectedVenue.Tables.Remove(table);
        _tables.Remove(table);
        _statusMessage = $"Removed table: {table.Label}";
    }

    partial void OnSelectedVenueChanged(Venue? value)
    {
        if (value == null)
        {
            ClearEditor();
        }
        else
        {
            LoadEditor(value);
        }
    }

    private void LoadEditor(Venue venue)
    {
        _venueName = venue.Name ?? "";
        _address = venue.Address ?? "";
        _notes = venue.Notes ?? "";

        _tables.Clear();
        foreach (var table in venue.Tables)
            _tables.Add(table);
    }

    private void ClearEditor()
    {
        _venueName = "";
        _address = "";
        _notes = "";
        _newTableName = "";
        _tables.Clear();
    }

    public void Cleanup()
    {
        SeasonService.SeasonChanged -= OnSeasonChanged;
    }
}
```

### Step 2: Update IDataStore (10 minutes)

Add venue methods to IDataStore interface:

```csharp
// wdpl2/Services/IDataStore.cs
public interface IDataStore
{
    // ... existing methods ...
    
    Task<List<Venue>> GetVenuesAsync(Guid? seasonId);
    Task AddVenueAsync(Venue venue);
    Task UpdateVenueAsync(Venue venue);
    Task DeleteVenueAsync(Venue venue);
}

// wdpl2/Services/DataStoreService.cs
public class DataStoreService : IDataStore
{
    // ... existing methods ...
    
    public Task<List<Venue>> GetVenuesAsync(Guid? seasonId)
    {
        if (!seasonId.HasValue)
            return Task.FromResult(new List<Venue>());
        
        var venues = DataStore.Data.Venues
            .Where(v => v.SeasonId == seasonId)
            .OrderBy(v => v.Name)
            .ToList();
        
        return Task.FromResult(venues);
    }

    public Task AddVenueAsync(Venue venue)
    {
        DataStore.Data.Venues.Add(venue);
        return Task.CompletedTask;
    }

    public Task UpdateVenueAsync(Venue venue)
    {
        // Already in list by reference
        return Task.CompletedTask;
    }

    public Task DeleteVenueAsync(Venue venue)
    {
        DataStore.Data.Venues.Remove(venue);
        return Task.CompletedTask;
    }
}
```

### Step 3: Update XAML (20 minutes)

```xml
<!-- wdpl2/Views/VenuesPage.xaml -->
<ContentPage xmlns:viewmodels="clr-namespace:Wdpl2.ViewModels"
             x:DataType="viewmodels:VenuesViewModel">
    
    <!-- Search box binding -->
    <Entry Text="{Binding SearchText}"
           Placeholder="Search venues...">
        <Entry.Behaviors>
            <toolkit:EventToCommandBehavior 
                EventName="TextChanged"
                Command="{Binding SearchVenuesCommand}"
                CommandParameter="{Binding SearchText}" />
        </Entry.Behaviors>
    </Entry>
    
    <!-- Venues list binding -->
    <CollectionView ItemsSource="{Binding Venues}"
                    SelectedItem="{Binding SelectedVenue}">
        <!-- ... item template ... -->
    </CollectionView>
    
    <!-- Editor fields binding -->
    <Entry Text="{Binding VenueName}" Placeholder="Venue Name" />
    <Entry Text="{Binding Address}" Placeholder="Address" />
    <Entry Text="{Binding Notes}" Placeholder="Notes" />
    
    <!-- Tables list binding -->
    <CollectionView ItemsSource="{Binding Tables}">
        <!-- ... table template ... -->
    </CollectionView>
    
    <Entry Text="{Binding NewTableName}" Placeholder="New Table" />
    
    <!-- Command bindings -->
    <Button Text="Add Venue" Command="{Binding AddVenueCommand}" />
    <Button Text="Update" Command="{Binding UpdateVenueCommand}" />
    <Button Text="Delete" 
            Command="{Binding DeleteVenueCommand}"
            CommandParameter="{Binding SelectedVenue}" />
    <Button Text="Add Table" Command="{Binding AddTableCommand}" />
    
    <!-- Status message binding -->
    <Label Text="{Binding StatusMessage}" />
</ContentPage>
```

### Step 4: Update Code-Behind (15 minutes)

```csharp
// wdpl2/Views/VenuesPage.xaml.cs
public partial class VenuesPage : ContentPage
{
    private readonly VenuesViewModel _viewModel;

    public VenuesPage() : this(null) { }

    public VenuesPage(VenuesViewModel? viewModel)
    {
        InitializeComponent();
        
        if (viewModel == null)
        {
            var dataStore = new DataStoreService();
            _viewModel = new VenuesViewModel(dataStore);
        }
        else
        {
            _viewModel = viewModel;
        }
        
        BindingContext = _viewModel;
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Cleanup();
    }

    // Keep import/export methods in code-behind (they're UI-specific)
    private async void OnExportClicked(object sender, EventArgs e)
    {
        // Export logic stays here
    }

    private async void OnImportClicked(object sender, EventArgs e)
    {
        // Import logic stays here
    }
}
```

### Step 5: Register in DI (5 minutes)

```csharp
// MauiProgram.cs
builder.Services.AddTransient<VenuesViewModel>();
builder.Services.AddTransient<VenuesPage>();
```

## ?? Migration Effort Estimate

| Page | Complexity | Time Estimate | Priority |
|------|-----------|---------------|----------|
| **VenuesPage** | Low | 1.5 hours | High |
| **DivisionsPage** | Low | 1.5 hours | High |
| **PlayersPage** | Medium | 2 hours | High |
| **TeamsPage** | Medium | 2 hours | High |
| **SeasonsPage** | Medium | 2.5 hours | Medium |
| **FixturesPage** | High | 3 hours | Medium |
| **LeagueTablesPage** | High | 3 hours | Medium |
| **SettingsPage** | Low | 1 hour | Low |
| **ImportPage** | Medium | 2 hours | Low |

**Total:** ~18.5 hours (2-3 days)

## ?? Benefits Per Page

### VenuesPage Benefits:
```csharp
// ? Before: Hard to test
public void OnAddVenue(object? sender, EventArgs e)
{
    // Tightly coupled to UI
    var name = VenueNameEntry.Text?.Trim();
    if (string.IsNullOrEmpty(name)) { ... }
    DataStore.Data.Venues.Add(new Venue { ... });
}

// ? After: Easy to test
[Fact]
public async Task AddVenue_AddsToList()
{
    var mockStore = new Mock<IDataStore>();
    var vm = new VenuesViewModel(mockStore.Object);
    vm.VenueName = "Test Venue";
    
    await vm.AddVenueCommand.ExecuteAsync(null);
    
    mockStore.Verify(x => x.AddVenueAsync(It.IsAny<Venue>()), Times.Once);
}
```

## ?? Reusable Patterns for All Pages

### 1. Base ViewModel Pattern
```csharp
// Create once, use everywhere
public abstract class BaseViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isLoading;
    
    [ObservableProperty]
    private string _statusMessage = "";
    
    [ObservableProperty]
    private Guid? _currentSeasonId;
    
    protected void SetStatus(string message)
    {
        _statusMessage = $"{DateTime.Now:HH:mm:ss}  {message}";
    }
    
    protected virtual void OnSeasonChanged(SeasonChangedEventArgs e)
    {
        _currentSeasonId = e.NewSeasonId;
    }
}

// Usage
public partial class VenuesViewModel : BaseViewModel
{
    // Automatically has IsLoading, StatusMessage, CurrentSeasonId
}
```

### 2. Standard CRUD Pattern
```csharp
// Every entity page follows same pattern
[RelayCommand]
private async Task LoadItemsAsync() { ... }

[RelayCommand]
private async Task AddItemAsync() { ... }

[RelayCommand]
private async Task UpdateItemAsync() { ... }

[RelayCommand]
private async Task DeleteItemAsync(TEntity item) { ... }
```

### 3. Search Pattern
```csharp
[ObservableProperty]
private string _searchText = "";

[RelayCommand]
private async Task SearchAsync(string searchText)
{
    _searchText = searchText;
    await LoadItemsAsync();
}
```

## ?? Quick Win Strategy

### Week 1: Simple CRUD Pages
**Monday:** VenuesPage (1.5 hours)  
**Tuesday:** DivisionsPage (1.5 hours)  
**Wednesday:** Test & refine base pattern  
**Thursday:** PlayersPage (2 hours)  
**Friday:** TeamsPage (2 hours)

**Result:** 4 pages migrated, pattern established

### Week 2: Complex Pages
**Monday-Tuesday:** FixturesPage (3 hours)  
**Wednesday-Thursday:** LeagueTablesPage (3 hours)  
**Friday:** SeasonsPage (2.5 hours)

**Result:** Major functionality migrated

### Week 3: Remaining Pages
**Monday:** SettingsPage (1 hour)  
**Tuesday:** ImportPage (2 hours)  
**Wednesday-Friday:** Testing, refinement, documentation

**Result:** Complete MVVM migration!

## ?? Pro Tips

### 1. Don't Over-Engineer
Keep complex UI generation in code-behind:
```csharp
// ? Good: Complex dynamic UI in code-behind
private void BuildTournamentBracket()
{
    // Complex layout generation
}

// ? Good: Business logic in ViewModel
[RelayCommand]
private async Task GenerateBracketAsync()
{
    // Logic to create bracket data
}
```

### 2. Leverage Existing Infrastructure
```csharp
// ? Already have:
- IDataStore interface
- DataStoreService implementation
- DI container setup
- CommunityToolkit.Mvvm
- Documentation

// ? Just add new methods
```

### 3. Test As You Go
```csharp
[Fact]
public async Task LoadVenues_FiltersBySeason()
{
    var vm = new VenuesViewModel(mockStore);
    vm.CurrentSeasonId = seasonId;
    
    await vm.LoadVenuesCommand.ExecuteAsync(null);
    
    Assert.All(vm.Venues, v => Assert.Equal(seasonId, v.SeasonId));
}
```

## ?? Resources

### Templates Created:
- ? CompetitionsPage (Complete MVVM example)
- ? IDataStore pattern
- ? ViewModel patterns
- ? XAML binding examples

### Copy From:
- `CompetitionsViewModel.cs` - List management pattern
- `CompetitionEditorViewModel.cs` - Editor pattern
- `CompetitionsPage.xaml` - Binding examples
- `IDataStore.cs` - Interface pattern

## ? Checklist Per Page

- [ ] Create ViewModel with ObservableProperties
- [ ] Add commands for all operations
- [ ] Add methods to IDataStore
- [ ] Implement in DataStoreService
- [ ] Update XAML with bindings
- [ ] Simplify code-behind
- [ ] Register in DI
- [ ] Test load/CRUD operations
- [ ] Write unit tests
- [ ] Update documentation

## ?? Expected Results

After migrating all pages:
- ? **~90% reduction** in code-behind complexity
- ? **100% testable** business logic
- ? **Consistent patterns** across entire app
- ? **Better separation** of concerns
- ? **Easier maintenance** and feature additions
- ? **Professional architecture** ready for production

---

**Next Steps:**
1. Choose first page (recommend VenuesPage)
2. Follow template above
3. Test thoroughly
4. Repeat for other pages
5. Celebrate! ??
