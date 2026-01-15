using Microsoft.Maui.Controls;
using System.IO;
using System.Text;

#if ANDROID
using Android.Webkit;
#endif

#if IOS || MACCATALYST
using WebKit;
#endif

namespace Wdpl2.Views;

public partial class PoolGamePage : ContentPage
{
    private string GenerateModularGameHtml()
    {
        // Load saved settings to inject into the page
        var savedSettings = LoadSettings();
        var savedSettingsJs = string.IsNullOrEmpty(savedSettings) 
            ? "null" 
            : savedSettings;
        
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, user-scalable=no'>
    <script>
        // Inject saved settings from MAUI Preferences
        window.MAUI_SAVED_SETTINGS = {savedSettingsJs};
        console.log('MAUI saved settings injected:', window.MAUI_SAVED_SETTINGS ? 'found' : 'none');
    </script>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; }}
        body {{ 
            background: #1e3c72; 
            font-family: Arial, sans-serif;
            overflow: hidden;
            display: flex;
            flex-direction: column;
            align-items: center;
            padding: 20px;
        }}
        #status {{
            color: white;
            background: rgba(0,0,0,0.9);
            padding: 15px 20px;
            border-radius: 8px;
            margin-bottom: 10px;
            font-size: 18px;
            font-weight: bold;
            text-align: center;
            width: 100%;
            max-width: 900px;
            box-shadow: 0 4px 12px rgba(0,0,0,0.5);
        }}
        canvas {{ 
            background: #1a7f37;
            border: 15px solid #8B4513;
            border-radius: 8px;
            cursor: crosshair;
            display: block;
            width: 100%;
            max-width: 1000px;
            height: auto;
            box-shadow: 0 8px 24px rgba(0,0,0,0.3);
        }}
        #controls {{
            margin-top: 15px;
            display: flex;
            gap: 10px;
            flex-wrap: wrap;
            justify-content: center;
        }}
        button {{
            padding: 12px 24px;
            background: #3B82F6;
            color: white;
            border: none;
            border-radius: 8px;
            font-size: 16px;
            font-weight: bold;
            cursor: pointer;
        }}
        button:hover {{ background: #2563EB; }}
        button:active {{ transform: scale(0.95); }}
        
        /* Ball Return Window */
        .ball-return-window {{
            background: linear-gradient(135deg, #2c3e50 0%, #34495e 100%);
            border-radius: 12px;
            padding: 15px;
            margin-top: 15px;
            box-shadow: inset 0 4px 8px rgba(0,0,0,0.3), 0 4px 12px rgba(0,0,0,0.2);
            border: 3px solid #8B4513;
            max-width: 1000px;
            width: 100%;
        }}
        
        .ball-return-header {{
            text-align: center;
            color: #ecf0f1;
            font-weight: bold;
            font-size: 1.1rem;
            margin-bottom: 12px;
            text-shadow: 2px 2px 4px rgba(0,0,0,0.5);
        }}
        
        .ball-return-tray {{
            display: flex;
            flex-wrap: wrap;
            gap: 8px;
            justify-content: center;
            min-height: 80px;
            background: linear-gradient(180deg, #1a1a1a 0%, #2d2d2d 100%);
            border-radius: 8px;
            padding: 12px;
            box-shadow: inset 0 2px 6px rgba(0,0,0,0.5);
        }}
        
        .potted-ball {{
            width: 40px;
            height: 40px;
            border-radius: 50%;
            display: flex;
            align-items: center;
            justify-content: center;
            font-weight: bold;
            font-size: 14px;
            box-shadow: 
                0 4px 8px rgba(0,0,0,0.4),
                inset -2px -2px 4px rgba(0,0,0,0.3),
                inset 2px 2px 4px rgba(255,255,255,0.3);
            animation: ballDrop 0.5s ease-out;
            position: relative;
        }}
        
        @keyframes ballDrop {{
            0% {{ transform: translateY(-100px) scale(0.5); opacity: 0; }}
            50% {{ transform: translateY(10px) scale(1.1); }}
            100% {{ transform: translateY(0) scale(1); opacity: 1; }}
        }}
        
        .potted-ball.red {{
            background: radial-gradient(circle at 30% 30%, #ff7777, #e63946, #780000);
        }}
        
        .potted-ball.yellow {{
            background: radial-gradient(circle at 30% 30%, #ffe066, #ffd43b, #a67c00);
        }}
        
        .potted-ball.black {{
            background: radial-gradient(circle at 30% 30%, #555555, #2a2a2a, #000000);
        }}
        
        .potted-ball.white {{
            background: radial-gradient(circle at 30% 30%, #ffffff, #f0f0f0, #a0a0a0);
        }}
        
        .potted-ball-number {{
            color: white;
            text-shadow: 1px 1px 2px rgba(0,0,0,0.8);
            z-index: 1;
            background: rgba(255,255,255,0.9);
            color: #1a1a1a;
            border-radius: 50%;
            width: 20px;
            height: 20px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 11px;
        }}
        
        .ball-return-empty {{
            color: #7f8c8d;
            font-style: italic;
            text-align: center;
            padding: 25px;
            font-size: 0.9rem;
        }}
        
        .ball-return-stats {{
            display: flex;
            justify-content: space-around;
            margin-top: 12px;
            padding-top: 12px;
            border-top: 1px solid rgba(255,255,255,0.1);
        }}
        
        .ball-stat {{
            text-align: center;
            color: #ecf0f1;
        }}
        
        .ball-stat-label {{
            font-size: 0.75rem;
            color: #95a5a6;
            text-transform: uppercase;
        }}
        
        .ball-stat-value {{
            font-size: 1.2rem;
            font-weight: bold;
            margin-top: 4px;
        }}
        
        .ball-stat-value.red {{ color: #e63946; }}
        .ball-stat-value.yellow {{ color: #ffd43b; }}
        .ball-stat-value.black {{ color: #ffffff; }}
    </style>
</head>
<body>
    <div id='status'>Loading Pool Game...</div>
    <canvas id='canvas' width='1000' height='500'></canvas>
    <div id='controls'>
        <button onclick='game.stopBalls()'>Stop All Balls</button>
        <button onclick='game.resetRack()'>Reset Rack</button>
        <button onclick='if(typeof PoolDevSettings !== ""undefined"") PoolDevSettings.toggle()'>Dev Settings (F2)</button>
    </div>
    
    <!-- Ball Return Window -->
    <div class='ball-return-window'>
        <div class='ball-return-header'>
            ?? BALL RETURN TRAY
        </div>
        <div class='ball-return-tray' id='ballReturnTray'>
            <div class='ball-return-empty'>No balls potted yet</div>
        </div>
        <div class='ball-return-stats'>
            <div class='ball-stat'>
                <div class='ball-stat-label'>Reds</div>
                <div class='ball-stat-value red' id='redsPotted'>0/7</div>
            </div>
            <div class='ball-stat'>
                <div class='ball-stat-label'>Yellows</div>
                <div class='ball-stat-value yellow' id='yellowsPotted'>0/7</div>
            </div>
            <div class='ball-stat'>
                <div class='ball-stat-label'>Black</div>
                <div class='ball-stat-value black' id='blackPotted'>0/1</div>
            </div>
        </div>
    </div>
    
    <script>
    {Services.PoolAudioModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolPhysicsModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolPocketModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolRenderingModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolSpinControlModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolInputModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolShotControlModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolDevSettingsModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.Pool3DViewModule.GenerateJavaScript()}
    </script>
    
    <script>
    {Services.PoolGameModule.GenerateJavaScript()}
    </script>
    
    <script>
    // Initialize 3D view after game loads
    window.addEventListener('load', () => {{
        setTimeout(() => {{
            if (typeof Pool3DView !== 'undefined' && typeof game !== 'undefined') {{
                Pool3DView.init(game);
            }}
        }}, 500);
    }});
    </script>
</body>
</html>";
    }

    public PoolGamePage()
    {
        InitializeComponent();
        
        // Configure WebView for audio support
        ConfigureWebView();
        
        // Handle WebView navigation for settings persistence
        GameWebView.Navigating += OnWebViewNavigating;
        
        LoadGame();
        
        // Reset button should only reset the game frame, not reload the entire page
        ResetBtn.Clicked += async (s, e) => await ResetGame();
    }
    
    private async void OnWebViewNavigating(object? sender, WebNavigatingEventArgs e)
    {
        // Intercept custom URL scheme for settings persistence
        if (e.Url.StartsWith("poolsettings://"))
        {
            e.Cancel = true;
            
            try
            {
                var uri = new Uri(e.Url);
                var action = uri.Host;
                
                if (action == "save")
                {
                    // Get the JSON from the query string
                    var json = Uri.UnescapeDataString(uri.Query.TrimStart('?'));
                    SaveSettings(json);
                    
                    // Notify JavaScript that save was successful
                    await GameWebView.EvaluateJavaScriptAsync("if(typeof PoolDevSettings !== 'undefined') PoolDevSettings.showNotification('Settings saved!', 'success');");
                }
                else if (action == "clear")
                {
                    ClearSettings();
                    await GameWebView.EvaluateJavaScriptAsync("if(typeof PoolDevSettings !== 'undefined') PoolDevSettings.showNotification('Settings cleared!', 'success');");
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Settings navigation error: {ex.Message}");
            }
        }
    }
    
    
    private async Task ResetGame()
    {
        // Call the JavaScript newGame() function instead of reloading the entire page
        // This preserves dev settings
        try
        {
            await GameWebView.EvaluateJavaScriptAsync("if(typeof game !== 'undefined') game.newGame();");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Reset game error: {ex.Message}");
        }
    }
    
    private void ConfigureWebView()
    {
        // Enable JavaScript (required for audio)
        // This is already enabled by default in MAUI, but we'll be explicit
        
#if ANDROID
        // Android-specific WebView configuration
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("AudioConfig", (handler, view) =>
        {
            if (handler.PlatformView is Android.Webkit.WebView webView)
            {
                var settings = webView.Settings;
                settings.JavaScriptEnabled = true;
                settings.MediaPlaybackRequiresUserGesture = false; // Allow autoplay
                settings.DomStorageEnabled = true;
                settings.DatabaseEnabled = true;
                
                // Set WebChromeClient to handle permissions
                webView.SetWebChromeClient(new Android.Webkit.WebChromeClient());
                
                System.Diagnostics.Debug.WriteLine("Android WebView configured for audio");
            }
        });
#endif

#if IOS || MACCATALYST
        // iOS-specific WebView configuration
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("AudioConfig", (handler, view) =>
        {
            if (handler.PlatformView is WebKit.WKWebView webView)
            {
                webView.Configuration.AllowsInlineMediaPlayback = true;
                webView.Configuration.MediaTypesRequiringUserActionForPlayback = WebKit.WKAudiovisualMediaTypes.None;
                
                System.Diagnostics.Debug.WriteLine("iOS WebView configured for audio");
            }
        });
#endif

#if WINDOWS
        // Windows-specific WebView configuration  
        Microsoft.Maui.Handlers.WebViewHandler.Mapper.AppendToMapping("AudioConfig", (handler, view) =>
        {
            System.Diagnostics.Debug.WriteLine("Windows WebView configured");
        });
#endif
    }
    
    // Save settings to MAUI Preferences (persists between sessions)
    public static void SaveSettings(string json)
    {
        try
        {
            Preferences.Set("poolGameDefaults", json);
            System.Diagnostics.Debug.WriteLine("Settings saved to MAUI Preferences");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to save settings: {ex.Message}");
        }
    }
    
    // Load settings from MAUI Preferences
    public static string LoadSettings()
    {
        try
        {
            var settings = Preferences.Get("poolGameDefaults", "");
            System.Diagnostics.Debug.WriteLine($"Settings loaded from MAUI Preferences: {(string.IsNullOrEmpty(settings) ? "none" : "found")}");
            return settings;
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to load settings: {ex.Message}");
            return "";
        }
    }
    
    // Clear settings from MAUI Preferences
    public static void ClearSettings()
    {
        try
        {
            Preferences.Remove("poolGameDefaults");
            System.Diagnostics.Debug.WriteLine("Settings cleared from MAUI Preferences");
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Failed to clear settings: {ex.Message}");
        }
    }

    private void LoadGame()
    {
        try
        {
            System.Diagnostics.Debug.WriteLine("=== PoolGamePage.LoadGame() ===");
            
            // Generate modular HTML
            var html = GenerateModularGameHtml();
            System.Diagnostics.Debug.WriteLine($"HTML Length: {html.Length} chars");
            
            // Use HtmlWebViewSource for inline HTML
            var htmlSource = new HtmlWebViewSource
            {
                Html = html
            };
            
            GameWebView.Source = htmlSource;
            
            System.Diagnostics.Debug.WriteLine("WebView source set successfully");
        }
        catch (System.Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"ERROR in LoadGame: {ex.Message}");
            System.Diagnostics.Debug.WriteLine($"Stack trace: {ex.StackTrace}");
            
            MainThread.BeginInvokeOnMainThread(async () =>
            {
                await DisplayAlert("Load Error", 
                    $"Failed to load pool game:\n\n{ex.Message}\n\nCheck Debug Output for details.", 
                    "OK");
            });
        }
    }
}
