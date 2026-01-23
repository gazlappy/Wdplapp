using System.Text;

namespace Wdpl2.Services
{
    /// <summary>
    /// Generates an embedded UK 8-ball pool game following EPA International Rules
    /// Optimized for smooth 60fps performance
    /// </summary>
    public static class PoolGameGenerator
    {
        public static string GeneratePoolGameHtml(string leagueName)
        {
            return $@"<!DOCTYPE html>
<html lang=""en"">
<head>
    <meta charset=""UTF-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0, maximum-scale=1.0, user-scalable=no"">
    <meta name=""apple-mobile-web-app-capable"" content=""yes"">
    <meta name=""mobile-web-app-capable"" content=""yes"">
    <title>UK 8-Ball Pool - {leagueName}</title>
    <style>
        * {{ margin: 0; padding: 0; box-sizing: border-box; -webkit-tap-highlight-color: transparent; }}
        body {{ 
            font-family: 'Segoe UI', sans-serif; 
            background: linear-gradient(135deg, #1e3c72 0%, #2a5298 100%); 
            min-height: 100vh; 
            padding: 10px;
            overflow-x: hidden;
            touch-action: none;
        }}
        .game-container {{ 
            max-width: 1400px; 
            margin: 0 auto; 
            background: rgba(255,255,255,0.95); 
            border-radius: 16px; 
            padding: 15px; 
            box-shadow: 0 20px 60px rgba(0,0,0,0.3); 
        }}
        .game-header {{ 
            display: flex; 
            justify-content: space-between; 
            align-items: center; 
            margin-bottom: 15px; 
            padding-bottom: 15px; 
            border-bottom: 2px solid #e0e0e0;
            flex-wrap: wrap;
            gap: 10px;
        }}
        .game-header h1 {{ 
            font-size: clamp(1.2rem, 4vw, 2rem); 
            color: #1e3c72; 
            flex: 1 1 100%;
            text-align: center;
        }}
        .controls {{ 
            display: flex; 
            gap: 8px; 
            flex-wrap: wrap;
            justify-content: center;
            width: 100%;
        }}
        .btn {{ 
            padding: 10px 16px; 
            border: none; 
            border-radius: 8px; 
            font-size: clamp(0.8rem, 2vw, 1rem); 
            font-weight: 600; 
            cursor: pointer; 
            transition: all 0.3s; 
            background: #3B82F6; 
            color: white;
            white-space: nowrap;
            flex: 1 1 auto;
            min-width: 100px;
            touch-action: manipulation;
        }}
        .btn:hover {{ background: #2563EB; transform: translateY(-2px); }}
        .btn:active {{ transform: translateY(0); }}
        .btn-small {{ 
            padding: 8px 12px;
            font-size: 0.85rem;
            min-width: 80px;
        }}
        .game-info {{ 
            display: grid; 
            grid-template-columns: 1fr auto 1fr; 
            gap: 15px; 
            margin-bottom: 15px; 
            align-items: center; 
        }}
        .player-panel {{ 
            background: linear-gradient(135deg, #f5f7fa 0%, #e8edf3 100%); 
            padding: 15px; 
            border-radius: 12px; 
            box-shadow: 0 4px 12px rgba(0,0,0,0.1); 
            transition: all 0.3s; 
        }}
        .player-panel.active {{ 
            background: linear-gradient(135deg, #4ade80 0%, #22c55e 100%); 
            color: white; 
            transform: scale(1.05); 
        }}
        .player-panel h3 {{ 
            font-size: clamp(0.9rem, 2.5vw, 1.1rem);
            margin-bottom: 8px;
        }}
        .player-balls {{ 
            font-size: clamp(1rem, 3vw, 1.5rem); 
            font-weight: bold; 
            margin: 8px 0; 
        }}
        .game-status {{ 
            text-align: center; 
            padding: 15px;
            min-width: 200px;
        }}
        .turn-indicator {{ 
            font-size: clamp(1rem, 2.5vw, 1.3rem); 
            font-weight: bold; 
            color: #1e3c72; 
        }}
        #gameMessage {{ 
            font-size: clamp(0.8rem, 2vw, 1rem);
            margin-top: 8px;
        }}
        #shotInfo {{ 
            font-size: clamp(0.75rem, 1.8vw, 0.9rem);
            color: #888;
            margin-top: 5px;
        }}
        .foul-indicator {{ 
            background: #ef4444; 
            color: white; 
            padding: 10px 20px; 
            border-radius: 8px; 
            margin-top: 10px; 
            animation: pulse 1s infinite; 
        }}
        @keyframes pulse {{ 0%, 100% {{ opacity: 1; }} 50% {{ opacity: 0.7; }} }}
        .table-container {{ 
            position: relative; 
            background: #8B4513; 
            padding: clamp(15px, 3vw, 40px); 
            border-radius: 16px; 
            margin-bottom: 15px;
            overflow: hidden;
        }}
        .canvas-wrapper {{
            position: relative;
            width: 100%;
            padding-bottom: 50%;
            overflow: hidden;
            background: #1a7f37;
            border-radius: 8px;
        }}
        #poolTable {{ 
            position: absolute;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            display: block; 
            cursor: crosshair; 
            touch-action: none;
        }}
        .mobile-controls {{
            display: none;
            position: fixed;
            bottom: 20px;
            left: 50%;
            transform: translateX(-50%);
            background: rgba(0,0,0,0.8);
            padding: 15px;
            border-radius: 12px;
            z-index: 1000;
            gap: 10px;
            flex-direction: column;
            align-items: center;
        }}
        .mobile-power-control {{
            display: flex;
            align-items: center;
            gap: 10px;
            width: 100%;
        }}
        .mobile-power-label {{
            color: white;
            font-weight: bold;
            font-size: 14px;
            min-width: 60px;
        }}
        .mobile-power-slider {{
            flex: 1;
            height: 30px;
            -webkit-appearance: none;
            background: rgba(255,255,255,0.2);
            border-radius: 15px;
            outline: none;
        }}
        .mobile-power-slider::-webkit-slider-thumb {{
            -webkit-appearance: none;
            width: 35px;
            height: 35px;
            border-radius: 50%;
            background: #4ade80;
            cursor: pointer;
            border: 3px solid white;
        }}
        .mobile-power-slider::-moz-range-thumb {{
            width: 35px;
            height: 35px;
            border-radius: 50%;
            background: #4ade80;
            cursor: pointer;
            border: 3px solid white;
        }}
        .mobile-shoot-btn {{
            width: 100%;
            padding: 15px;
            background: linear-gradient(135deg, #10b981 0%, #059669 100%);
            border: none;
            border-radius: 10px;
            color: white;
            font-size: 18px;
            font-weight: bold;
            cursor: pointer;
            box-shadow: 0 4px 12px rgba(0,0,0,0.3);
        }}
        .mobile-shoot-btn:active {{
            transform: scale(0.95);
        }}
        .power-bar-container {{ 
            position: absolute; 
            bottom: 10px; 
            left: 50%; 
            transform: translateX(-50%); 
            width: clamp(200px, 50vw, 300px); 
            background: rgba(0,0,0,0.7); 
            padding: 12px; 
            border-radius: 12px;
            display: none;
        }}
        .power-bar {{ 
            width: 100%; 
            height: 20px; 
            background: rgba(255,255,255,0.2); 
            border-radius: 10px; 
            overflow: hidden; 
        }}
        .power-fill {{ 
            height: 100%; 
            background: linear-gradient(90deg, #4ade80 0%, #fbbf24 50%, #ef4444 100%); 
            transition: width 0.05s; 
        }}
        .instructions {{ 
            background: #f8fafc; 
            padding: 15px; 
            border-radius: 12px; 
            border: 2px dashed #cbd5e1;
            margin-top: 15px;
        }}
        .instructions p {{
            font-size: clamp(0.85rem, 2vw, 1rem);
            margin-bottom: 10px;
        }}
        .instructions ul {{ 
            list-style: none; 
            padding-left: 0; 
        }}
        .instructions li {{ 
            padding: 6px 0; 
            color: #475569;
            font-size: clamp(0.8rem, 1.8vw, 0.9rem);
        }}
        .modal {{ 
            display: none; 
            position: fixed; 
            z-index: 1000; 
            left: 0; 
            top: 0; 
            width: 100%; 
            height: 100%; 
            background: rgba(0,0,0,0.7); 
            overflow-y: auto;
            padding: 20px;
        }}
        .modal-content {{ 
            background: white; 
            margin: 20px auto; 
            padding: 25px; 
            border-radius: 16px; 
            width: 90%; 
            max-width: 700px; 
            max-height: 90vh; 
            overflow-y: auto; 
        }}
        .close {{ 
            color: #aaa; 
            float: right; 
            font-size: 28px; 
            font-weight: bold; 
            cursor: pointer;
            line-height: 20px;
        }}
        .close:hover {{ color: #ef4444; }}
        
        /* Mobile-specific styles */
        @media (max-width: 768px) {{ 
            body {{ padding: 5px; }}
            .game-container {{ padding: 10px; border-radius: 12px; }}
            .game-info {{ 
                grid-template-columns: 1fr; 
                gap: 10px;
            }}
            .game-status {{
                order: -1;
                min-width: auto;
            }}
            .player-panel {{ padding: 12px; }}
            .controls {{ gap: 6px; }}
            .btn {{ 
                padding: 10px 12px;
                font-size: 0.85rem;
                min-width: 80px;
            }}
            .table-container {{ padding: 10px; }}
            .mobile-controls {{ display: flex; }}
            .instructions {{ 
                font-size: 0.85rem;
                padding: 12px;
            }}
            .modal-content {{ 
                padding: 20px;
                width: 95%;
                margin: 10px auto;
            }}
            #poolTable {{ cursor: none; }}
        }}
        
        @media (max-width: 480px) {{
            .game-header h1 {{ font-size: 1.2rem; }}
            .btn {{ 
                padding: 8px 10px;
                font-size: 0.75rem;
                min-width: 70px;
            }}
            .player-panel {{ padding: 10px; }}
            .instructions {{ padding: 10px; }}
        }}
        
        /* Landscape mobile */
        @media (max-height: 500px) and (orientation: landscape) {{
            .game-header {{ margin-bottom: 5px; padding-bottom: 5px; }}
            .game-info {{ margin-bottom: 5px; }}
            .table-container {{ padding: 5px; }}
            .instructions {{ display: none; }}
            .mobile-controls {{ bottom: 5px; padding: 8px; }}
        }}
        
        /* Touch feedback */
        .btn:active, .mobile-shoot-btn:active {{
            opacity: 0.8;
        }}
        
        /* Prevent text selection on mobile */
        .game-container {{
            -webkit-user-select: none;
            -moz-user-select: none;
            -ms-user-select: none;
            user-select: none;
        }}
        
        /* Ball Return Window */
        .ball-return-window {{
            background: linear-gradient(135deg, #2c3e50 0%, #34495e 100%);
            border-radius: 12px;
            padding: 15px;
            margin-top: 15px;
            box-shadow: inset 0 4px 8px rgba(0,0,0,0.3), 0 4px 12px rgba(0,0,0,0.2);
            border: 3px solid #8B4513;
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
        
        @media (max-width: 768px) {{
            .ball-return-window {{
                padding: 12px;
                margin-top: 10px;
            }}
            
            .potted-ball {{
                width: 35px;
                height: 35px;
                font-size: 12px;
            }}
            
            .potted-ball-number {{
                width: 18px;
                height: 18px;
                font-size: 10px;
            }}
            
            .ball-return-header {{
                font-size: 1rem;
            }}
        }}
    </style>
</head>
<body>
        <div class=""game-container"">
        <div class=""game-header"">
            <h1>?? UK 8-Ball Pool</h1>
            <div class=""controls"">
                <button id=""newGameBtn"" class=""btn btn-small"">New Game</button>
                <button id=""rulesBtn"" class=""btn btn-small"">EPA Rules</button>
                <button id=""ballInHandBtn"" class=""btn btn-small"">?? Move Cue</button>
                <button id=""toggle3DBtn"" class=""btn btn-small"">?? 3D View</button>
                <button id=""toggleRealisticBtn"" class=""btn btn-small"" title=""Photorealistic 3D (Shift+#)"">? Realistic</button>
                <button id=""devSettingsBtn"" class=""btn btn-small"">?? Dev</button>
                <button class=""btn btn-small"" onclick=""window.location.href='index.html'"">?? Back</button>
            </div>
        </div>
        <div class=""game-info"">
            <div class=""player-panel"" id=""player1Panel"">
                <h3>Player 1</h3>
                <div class=""player-balls"" id=""player1Balls"">-</div>
                <div id=""player1Status""></div>
            </div>
            <div class=""game-status"">
                <div class=""turn-indicator"" id=""turnIndicator"">Player 1's Turn</div>
                <div id=""gameMessage"">Legal Break: 3+ points required</div>
                <div id=""shotInfo"">Touch and drag to aim & shoot</div>
            <div class=""foul-indicator"" id=""foulIndicator"" style=""display:none"">?? FOUL - Ball in Hand</div>
            </div>
            <div class=""player-panel"" id=""player2Panel"">
                <h3>Player 2</h3>
                <div class=""player-balls"" id=""player2Balls"">-</div>
                <div id=""player2Status""></div>
            </div>
        </div>
        <div class=""table-container"">
            <div class=""canvas-wrapper"">
                <canvas id=""poolTable"" width=""1000"" height=""500""></canvas>
            </div>
            <div class=""power-bar-container"" id=""powerBarContainer"">
                <div style=""color:white;font-weight:bold;margin-bottom:8px;text-align:center"">Shot Power</div>
                <div class=""power-bar""><div class=""power-fill"" id=""powerFill""></div></div>
            </div>
        </div>
        
        <!-- Ball Return Window -->
        <div class=""ball-return-window"">
            <div class=""ball-return-header"">
                ?? BALL RETURN TRAY
            </div>
            <div class=""ball-return-tray"" id=""ballReturnTray"">
                <div class=""ball-return-empty"">No balls potted yet</div>
            </div>
            <div class=""ball-return-stats"">
                <div class=""ball-stat"">
                    <div class=""ball-stat-label"">Reds</div>
                    <div class=""ball-stat-value red"" id=""redsPotted"">0/7</div>
                </div>
                <div class=""ball-stat"">
                    <div class=""ball-stat-label"">Yellows</div>
                    <div class=""ball-stat-value yellow"" id=""yellowsPotted"">0/7</div>
                </div>
                <div class=""ball-stat"">
                    <div class=""ball-stat-label"">Black</div>
                    <div class=""ball-stat-value black"" id=""blackPotted"">0/1</div>
                </div>
            </div>
        </div>
        
        <!-- Mobile Controls -->
        <div class=""mobile-controls"" id=""mobileControls"">
            <div class=""mobile-power-control"">
                <span class=""mobile-power-label"">Power:</span>
                <input type=""range"" class=""mobile-power-slider"" id=""mobilePowerSlider"" min=""0"" max=""100"" value=""50"">
                <span class=""mobile-power-label"" id=""mobilePowerValue"">50%</span>
            </div>
            <button class=""mobile-shoot-btn"" id=""mobileShootBtn"">?? SHOOT</button>
        </div>
        
        <div class=""instructions"">
            <p><strong>EPA International Rules (Simplified):</strong></p>
            <ul>
                <li><strong>Legal Break:</strong> 3+ points (1 per ball potted, 1 per ball past center)</li>
                <li><strong>Table Open:</strong> First pot decides colors (reds/yellows)</li>
                <li><strong>Legal Shot:</strong> Hit your color first, then pot OR hit cushion</li>
                <li><strong>Fouls:</strong> In-off, no cushion, wrong ball = Ball in Hand</li>
                <li><strong>Win:</strong> Pot all your colors, then legally pot black</li>
            </ul>
        </div>
    </div>
    <div id=""rulesModal"" class=""modal"">
        <div class=""modal-content"">
            <span class=""close"" id=""closeRules"">&times;</span>
            <h2>EPA International 8-Ball Rules</h2>
            <div style=""margin-top:20px;clear:both"">
                <h3 style=""color:#1e3c72"">The Break</h3>
                <p><strong>Legal Break:</strong> Score 3+ points (1 per ball potted/past center)</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Table Open</h3>
                <p>After break, first legal pot decides groups</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Legal Shot</h3>
                <p>Hit your color first, then pot a ball OR hit cushion</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Fouls</h3>
                <p>In-off, no cushion hit, wrong ball first = Ball in Hand anywhere</p>
                <h3 style=""color:#1e3c72;margin-top:15px"">Winning</h3>
                <p>Clear all your colors, then legally pot the black 8-ball</p>
                <p style=""margin-top:15px;padding:10px;background:#f0f9ff;border-left:4px solid #3B82F6""><em>Full rules: <a href=""https://www.epa.org.uk/rules/international_2b.php"" target=""_blank"">www.epa.org.uk</a></em></p>
            </div>
        </div>
    </div>
    <script>{GetPoolGameJS()}</script>
    <script>
    // Mobile-specific enhancements
    (function() {{
        const isMobile = /Android|webOS|iPhone|iPad|iPod|BlackBerry|IEMobile|Opera Mini/i.test(navigator.userAgent) || window.innerWidth < 768;
        
        if (isMobile) {{
            document.getElementById('shotInfo').textContent = 'Touch and drag to aim & shoot';
            
            // Show mobile controls
            const mobileControls = document.getElementById('mobileControls');
            const mobilePowerSlider = document.getElementById('mobilePowerSlider');
            const mobilePowerValue = document.getElementById('mobilePowerValue');
            const mobileShootBtn = document.getElementById('mobileShootBtn');
            
            if (mobileControls) {{
                mobileControls.style.display = 'flex';
            }}
            
            // Mobile power slider
            if (mobilePowerSlider) {{
                mobilePowerSlider.addEventListener('input', (e) => {{
                    const power = e.target.value;
                    mobilePowerValue.textContent = power + '%';
                    if (window.game && window.game.cueBall) {{
                        window.game.shotPower = (power / 100) * window.game.maxPower;
                    }}
                }});
            }}
            
            // Mobile shoot button
            if (mobileShootBtn) {{
                mobileShootBtn.addEventListener('click', () => {{
                    if (window.game && window.game.cueBall && !window.game.cueBall.potted) {{
                        const power = (mobilePowerSlider.value / 100) * window.game.maxPower;
                        const angle = window.game.aimAngle || 0;
                        window.game.cueBall.vx = Math.cos(angle) * power;
                        window.game.cueBall.vy = Math.sin(angle) * power;
                        window.game.isShooting = false;
                        window.game.isAiming = false;
                    }}
                }});
            }}
        }}
        
        // Dev settings button
        const devBtn = document.getElementById('devSettingsBtn');
        if (devBtn) {{
            devBtn.addEventListener('click', () => {{
                if (typeof PoolDevSettings !== 'undefined') {{
                    PoolDevSettings.toggle();
                }}
            }});
        }}
        
        // 3D View toggle button
        const toggle3DBtn = document.getElementById('toggle3DBtn');
        if (toggle3DBtn) {{
            toggle3DBtn.addEventListener('click', () => {{
                if (typeof Pool3DRenderer !== 'undefined') {{
                    Pool3DRenderer.toggle();
                    // Update button text based on state
                    setTimeout(() => {{
                        toggle3DBtn.textContent = Pool3DRenderer.enabled ? '?? 2D View' : '?? 3D View';
                    }}, 100);
                }}
            }});
        }}
        
        // Photorealistic 3D toggle button
        const toggleRealisticBtn = document.getElementById('toggleRealisticBtn');
        if (toggleRealisticBtn) {{
            toggleRealisticBtn.addEventListener('click', async () => {{
                if (typeof PoolThreeJS !== 'undefined') {{
                    await PoolThreeJS.toggle();
                    // Update button text based on state
                    setTimeout(() => {{
                        toggleRealisticBtn.textContent = PoolThreeJS.enabled ? '?? Standard' : '? Realistic';
                        toggleRealisticBtn.style.background = PoolThreeJS.enabled ? '#8b5cf6' : '';
                    }}, 100);
                }} else {{
                    console.warn('PoolThreeJS not available');
                }}
            }});
        }}
        
        // Prevent zoom on double-tap for iOS
        let lastTouchEnd = 0;
        document.addEventListener('touchend', (e) => {{
            const now = Date.now();
            if (now - lastTouchEnd <= 300) {{
                e.preventDefault();
            }}
            lastTouchEnd = now;
        }}, false);
        
        // Prevent pull-to-refresh
        document.body.addEventListener('touchmove', (e) => {{
            if (e.touches.length > 1) {{
                e.preventDefault();
            }}
        }}, {{ passive: false }});
        
        // Initialize game settings after game loads (same as app)
        setTimeout(() => {{
            if (typeof PoolGameSettings !== 'undefined' && typeof game !== 'undefined') {{
                PoolGameSettings.init(game);
                PoolGameSettings.applySettings();
            }}
        }}, 300);
    }})();
    </script>
    <div id=""debugInfo"" style=""position:fixed;top:10px;right:10px;background:rgba(0,0,0,0.8);color:lime;padding:10px;border-radius:8px;font-family:monospace;font-size:12px;z-index:10000;max-width:200px;display:none;"">
        Initializing...
    </div>
</body>
</html>";
        }
        
        private static string GetPoolGameJS()
        {
            var sb = new StringBuilder();
            
            // Add embedded Three.js first (before 3D renderer)
            sb.AppendLine(PoolThreeJSModule.GenerateJavaScript());
                    
            // Add all module JavaScript (same order as app)
            sb.AppendLine(PoolAudioModule.GenerateJavaScript());
            sb.AppendLine(PoolBallRotationModule.GenerateJavaScript());  // Ball rotation - must be before Physics
            sb.AppendLine(PoolPhysicsModule.GenerateJavaScript());
            sb.AppendLine(PoolPocketModule.GenerateJavaScript());
            sb.AppendLine(PoolRenderingModule.GenerateJavaScript());
            sb.AppendLine(PoolSpinControlModule.GenerateJavaScript());
            sb.AppendLine(PoolInputModule.GenerateJavaScript());
            sb.AppendLine(PoolShotControlModule.GenerateJavaScript());
            sb.AppendLine(PoolDevSettingsModule.GenerateJavaScript());
            sb.AppendLine(PoolGameSettingsModule.GenerateJavaScript());  // Added: same as app
            sb.AppendLine(Pool3DRendererModule.GenerateJavaScript());    // 3D Renderer POC - press '3' to toggle
            sb.AppendLine(PoolGameModule.GenerateJavaScript());
            
            // Add 3D render loop integration
            sb.AppendLine(Get3DIntegrationJS());
            
            return sb.ToString();
        }
        
                /// <summary>
                /// Integration code to hook 3D renderer into the game loop
                /// </summary>
                private static string Get3DIntegrationJS()
                {
                    return @"
        // ============================================
        // 3D RENDERER INTEGRATION
        // Hooks Pool3DRenderer into the game loop
        // ============================================
        (function() {
            // Wait for game to initialize
            const waitForGame = setInterval(() => {
                if (typeof game !== 'undefined' && game.balls) {
                    clearInterval(waitForGame);
            
                    // Store original render function
                    const originalRender = game.render ? game.render.bind(game) : null;
            
                    // Override the game loop to include 3D rendering
                    const originalGameLoop = game.gameLoop ? game.gameLoop.bind(game) : null;
            
                    if (originalGameLoop) {
                        game.gameLoop = function() {
                            originalGameLoop();
                    
                            // Update 3D renderer if enabled
                            if (Pool3DRenderer.enabled && Pool3DRenderer.initialized) {
                                Pool3DRenderer.updateBalls(
                                    game.balls, 
                                    game.canvas.width, 
                                    game.canvas.height
                                );
                                Pool3DRenderer.render();
                            }
                        };
                    }
            
                    // Show mode indicator on load
                    Pool3DRenderer.updateModeIndicator();
            
                    console.log('[3D Integration] Ready! Press ""3"" to toggle 3D mode.');
                }
            }, 100);
        })();
        ";
                }
            }
        }
