using System.Windows.Input;
using Microsoft.Maui.Controls;

namespace Wdpl2.Behaviors;

/// <summary>
/// Behavior to add pull-to-refresh capability to CollectionView or ListView
/// </summary>
public class PullToRefreshBehavior : Behavior<RefreshView>
{
    public static readonly BindableProperty CommandProperty =
        BindableProperty.Create(
            nameof(Command),
            typeof(ICommand),
            typeof(PullToRefreshBehavior),
            null);

    public static readonly BindableProperty IsRefreshingProperty =
        BindableProperty.Create(
            nameof(IsRefreshing),
            typeof(bool),
            typeof(PullToRefreshBehavior),
            false,
            BindingMode.TwoWay);

    public ICommand? Command
    {
        get => (ICommand?)GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public bool IsRefreshing
    {
        get => (bool)GetValue(IsRefreshingProperty);
        set => SetValue(IsRefreshingProperty, value);
    }

    protected override void OnAttachedTo(RefreshView bindable)
    {
        base.OnAttachedTo(bindable);
        bindable.Refreshing += OnRefreshing;
    }

    protected override void OnDetachingFrom(RefreshView bindable)
    {
        base.OnDetachingFrom(bindable);
        bindable.Refreshing -= OnRefreshing;
    }

    private async void OnRefreshing(object? sender, EventArgs e)
    {
        if (Command?.CanExecute(null) == true)
        {
            Command.Execute(null);
            
            // Wait for IsRefreshing to be set to false by the command
            // or timeout after 5 seconds
            var timeout = Task.Delay(5000);
            var waitTask = Task.Run(async () =>
            {
                while (IsRefreshing)
                {
                    await Task.Delay(100);
                }
            });
            
            await Task.WhenAny(timeout, waitTask);
            
            if (sender is RefreshView refreshView)
            {
                refreshView.IsRefreshing = false;
            }
        }
    }
}
