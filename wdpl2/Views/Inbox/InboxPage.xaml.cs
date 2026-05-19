using Wdpl2.ViewModels.Inbox;

namespace Wdpl2.Views.Inbox;

public partial class InboxPage : ContentPage
{
    private readonly InboxViewModel _vm;
    private bool _initialized;

    public InboxPage(InboxViewModel viewModel)
    {
        InitializeComponent();
        _vm = viewModel;
        BindingContext = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        if (_initialized) return;
        _initialized = true;
        await _vm.InitializeAsync();
    }
}
