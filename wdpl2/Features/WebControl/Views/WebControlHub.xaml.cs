using System.Text;
using System.Text.Json;
using Wdpl2.Services.Web;

namespace Wdpl2.Views.WebControl;

/// <summary>
/// Entry point for everything online. Renders one tile per registered
/// <see cref="IWebModule"/>, so adding a module adds a tile without editing this
/// page's layout.
/// </summary>
public partial class WebControlHub : ContentPage
{
    private readonly WebModuleRegistry _registry;
    private readonly IServiceProvider _services;
    private bool _built;

    public WebControlHub(WebModuleRegistry registry, IServiceProvider services)
    {
        InitializeComponent();
        _registry = registry;
        _services = services;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();
        if (!_built)
        {
            BuildModuleTiles();
            _built = true;
        }
        _ = ShowEndpointAsync();
    }

    /// <summary>
    /// Which page a tile opens. Modules that have no page of their own yet are
    /// still listed, so the tab reflects what is actually deployed.
    /// </summary>
    private Page? PageFor(IWebModule module) => module.Id switch
    {
        "system" => _services.GetService<ConnectionPage>(),
        _ => null,
    };

    private void BuildModuleTiles()
    {
        ModuleList.Children.Clear();

        foreach (var module in _registry.Modules)
        {
            var heading = new Label
            {
                Text = module.Title,
                FontAttributes = FontAttributes.Bold,
                FontSize = 14,
                TextColor = Color.FromArgb("#1E293B"),
            };

            var detail = new Label
            {
                Text = module.Description,
                FontSize = 11,
                TextColor = Color.FromArgb("#64748B"),
            };

            var icon = new Label
            {
                Text = module.Icon,
                FontSize = 20,
                VerticalOptions = LayoutOptions.Center,
                Margin = new Thickness(0, 0, 12, 0),
            };

            var grid = new Grid
            {
                ColumnDefinitions =
                {
                    new ColumnDefinition(GridLength.Auto),
                    new ColumnDefinition(GridLength.Star),
                },
                Padding = new Thickness(14, 12),
            };
            grid.Add(icon);
            grid.Add(new VerticalStackLayout { Spacing = 2, Children = { heading, detail } }, 1);

            var frame = new Frame
            {
                BorderColor = Color.FromArgb("#E2E8F0"),
                BackgroundColor = Colors.White,
                CornerRadius = 10,
                Padding = 0,
                HasShadow = false,
                Content = grid,
            };

            var captured = module;
            var tap = new TapGestureRecognizer();
            tap.Tapped += async (_, _) => await OpenModuleAsync(captured);
            grid.GestureRecognizers.Add(tap);

            ModuleList.Children.Add(frame);
        }
    }

    private async Task OpenModuleAsync(IWebModule module)
    {
        var page = PageFor(module);
        if (page is null)
        {
            await DisplayAlert(module.Title, "This module has no settings page yet.", "OK");
            return;
        }
        await Navigation.PushAsync(page);
    }

    private async Task ShowEndpointAsync()
    {
        var connection = await WebConnection.LoadAsync();
        if (!connection.IsConfigured)
        {
            EndpointLabel.Text = "Not configured";
            StatusLabel.Text = "Open Connection & Deploy to set the API address and administrator sign-in.";
            return;
        }

        try
        {
            EndpointLabel.Text = connection.ResolveEndpoint().AbsoluteUri;
        }
        catch (InvalidOperationException ex)
        {
            EndpointLabel.Text = ex.Message;
        }
    }

    private async void OnCheckClicked(object? sender, EventArgs e)
    {
        CheckButton.IsEnabled = false;
        DetailFrame.IsVisible = false;
        StatusLabel.Text = "Checking...";
        StatusLabel.TextColor = Color.FromArgb("#64748B");

        try
        {
            var connection = await WebConnection.LoadAsync();
            using var client = new WebApiClient(connection);

            var health = await client.AdminAsync("system", "health");
            StatusLabel.Text = "Backend reachable and signed in.";
            StatusLabel.TextColor = Color.FromArgb("#10B981");
            DetailLabel.Text = Describe(health);
            DetailFrame.IsVisible = true;
        }
        catch (WebApiException ex)
        {
            StatusLabel.Text = ex.Message;
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        catch (InvalidOperationException ex)
        {
            StatusLabel.Text = ex.Message;
            StatusLabel.TextColor = Color.FromArgb("#EF4444");
        }
        finally
        {
            CheckButton.IsEnabled = true;
        }
    }

    private static string Describe(JsonElement health)
    {
        var text = new StringBuilder();

        if (health.TryGetProperty("php", out var php))
            text.AppendLine($"PHP {php.GetString()}");
        if (health.TryGetProperty("mysql", out var mysql))
            text.AppendLine($"MySQL {mysql.GetString()}");

        if (health.TryGetProperty("schema", out var schema) && schema.ValueKind == JsonValueKind.Array)
        {
            text.AppendLine();
            foreach (var entry in schema.EnumerateArray())
            {
                var id = entry.TryGetProperty("id", out var i) ? i.GetString() : "?";
                var expected = entry.TryGetProperty("expected", out var e) ? e.GetInt32() : 0;
                var applied = entry.TryGetProperty("applied", out var a) && a.ValueKind == JsonValueKind.Number
                    ? a.GetInt32().ToString()
                    : "not installed";
                var current = entry.TryGetProperty("current", out var c) && c.ValueKind == JsonValueKind.True;

                text.AppendLine($"{(current ? "✓" : "⚠")} {id}: schema {applied} (app expects {expected})");
            }
        }

        return text.ToString().TrimEnd();
    }
}
