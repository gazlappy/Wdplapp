using Microsoft.Maui.Controls;
using System.IO;
using System.Text;

namespace Wdpl2.Views;

public partial class PoolGamePage : ContentPage
{
    private string GenerateModularGameHtml()
    {
        return $@"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, user-scalable=no'>
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
    </style>
</head>
<body>
    <div id='status'>?? Loading Pool Game...</div>
    <canvas id='canvas' width='1000' height='500'></canvas>
    <div id='controls'>
        <button onclick='game.stopBalls()'>?? Stop All Balls</button>
        <button onclick='game.resetRack()'>?? Reset Rack</button>
    </div>
    
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
    {Services.PoolGameModule.GenerateJavaScript()}
    </script>
</body>
</html>";
    }

    public PoolGamePage()
    {
        InitializeComponent();
        LoadGame();
        
        ResetBtn.Clicked += (s, e) => LoadGame();
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
