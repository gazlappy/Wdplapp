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

    private async void OnReviewWebsiteChangesClicked(object sender, EventArgs e)
    {
        var button = (Button)sender;
        button.IsEnabled = false;
        try { await Navigation.PushAsync(new AdminReviewPage()); }
        catch (Exception ex) { await DisplayAlert("Website review", ex.Message, "OK"); }
        finally { button.IsEnabled = true; }
    }
}
