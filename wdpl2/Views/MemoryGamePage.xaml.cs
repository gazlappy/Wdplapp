namespace Wdpl2.Views;

public partial class MemoryGamePage : ContentPage
{
    public MemoryGamePage()
    {
        InitializeComponent();
        LoadGame();
    }

    private void LoadGame()
    {
        var html = GenerateMemoryGameHtml();
        GameWebView.Source = new HtmlWebViewSource { Html = html };
    }

    private void OnNewGameClicked(object? sender, EventArgs e)
    {
        LoadGame();
    }

    private static string GenerateMemoryGameHtml()
    {
        return @"<!DOCTYPE html>
<html>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0, user-scalable=no'>
    <style>
        * { margin: 0; padding: 0; box-sizing: border-box; }
        body { 
            background: linear-gradient(135deg, #1a1a2e 0%, #16213e 100%); 
            font-family: Arial, sans-serif;
            display: flex;
            flex-direction: column;
            align-items: center;
            min-height: 100vh;
            padding: 20px;
        }
        #stats {
            display: flex;
            gap: 30px;
            margin-bottom: 20px;
            color: white;
        }
        .stat {
            text-align: center;
        }
        .stat-value {
            font-size: 32px;
            font-weight: bold;
            color: #8b5cf6;
        }
        .stat-label {
            font-size: 14px;
            color: #94a3b8;
        }
        #gameBoard {
            display: grid;
            grid-template-columns: repeat(4, 1fr);
            gap: 10px;
            max-width: 500px;
            width: 100%;
            padding: 20px;
            background: rgba(255,255,255,0.05);
            border-radius: 15px;
        }
        .card {
            aspect-ratio: 1;
            background: linear-gradient(135deg, #374151 0%, #1f2937 100%);
            border-radius: 10px;
            cursor: pointer;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 40px;
            transition: transform 0.3s, background 0.3s;
            user-select: none;
            border: 3px solid #4b5563;
        }
        .card:hover:not(.flipped):not(.matched) {
            transform: scale(1.05);
            border-color: #8b5cf6;
        }
        .card.flipped {
            background: linear-gradient(135deg, #8b5cf6 0%, #6366f1 100%);
            border-color: #a78bfa;
        }
        .card.matched {
            background: linear-gradient(135deg, #22c55e 0%, #16a34a 100%);
            border-color: #4ade80;
            animation: matchPulse 0.5s ease;
        }
        .card .front {
            display: none;
        }
        .card.flipped .front,
        .card.matched .front {
            display: block;
        }
        .card .back {
            font-size: 30px;
        }
        .card.flipped .back,
        .card.matched .back {
            display: none;
        }
        @keyframes matchPulse {
            0%, 100% { transform: scale(1); }
            50% { transform: scale(1.1); }
        }
        #winScreen {
            position: fixed;
            top: 0;
            left: 0;
            width: 100%;
            height: 100%;
            background: rgba(0,0,0,0.9);
            display: none;
            align-items: center;
            justify-content: center;
            z-index: 100;
        }
        #winContent {
            text-align: center;
            color: white;
            padding: 40px;
        }
        #winContent h2 {
            font-size: 48px;
            color: #22c55e;
            margin-bottom: 20px;
        }
        #winContent p {
            font-size: 20px;
            margin-bottom: 10px;
        }
        #playAgainBtn {
            margin-top: 30px;
            padding: 15px 40px;
            background: #8b5cf6;
            color: white;
            border: none;
            border-radius: 10px;
            font-size: 20px;
            cursor: pointer;
            font-weight: bold;
        }
        #playAgainBtn:hover {
            background: #7c3aed;
        }
        #difficulty {
            margin-bottom: 20px;
            display: flex;
            gap: 10px;
        }
        .diff-btn {
            padding: 10px 20px;
            background: #374151;
            color: white;
            border: none;
            border-radius: 8px;
            cursor: pointer;
            font-size: 14px;
        }
        .diff-btn.active {
            background: #8b5cf6;
        }
    </style>
</head>
<body>
    <div id='difficulty'>
        <button class='diff-btn active' onclick='setDifficulty(4)'>Easy (4x4)</button>
        <button class='diff-btn' onclick='setDifficulty(6)'>Hard (6x6)</button>
    </div>
    
    <div id='stats'>
        <div class='stat'>
            <div class='stat-value' id='moves'>0</div>
            <div class='stat-label'>Moves</div>
        </div>
        <div class='stat'>
            <div class='stat-value' id='pairs'>0/8</div>
            <div class='stat-label'>Pairs Found</div>
        </div>
        <div class='stat'>
            <div class='stat-value' id='time'>0:00</div>
            <div class='stat-label'>Time</div>
        </div>
    </div>
    
    <div id='gameBoard'></div>
    
    <div id='winScreen'>
        <div id='winContent'>
            <h2>?? You Won!</h2>
            <p>Moves: <span id='finalMoves'>0</span></p>
            <p>Time: <span id='finalTime'>0:00</span></p>
            <p id='bestTime' style='color:#fbbf24;'></p>
            <button id='playAgainBtn' onclick='initGame()'>Play Again</button>
        </div>
    </div>

    <script>
        const emojis = ['??', '??', '??', '??', '??', '??', '??', '??', 
                       '??', '??', '??', '??', '??', '??', '??', '??',
                       '??', '?'];
        
        let gridSize = 4;
        let cards = [];
        let flippedCards = [];
        let matchedPairs = 0;
        let moves = 0;
        let totalPairs = 0;
        let gameStarted = false;
        let timer = null;
        let seconds = 0;
        
        function setDifficulty(size) {
            gridSize = size;
            document.querySelectorAll('.diff-btn').forEach((btn, i) => {
                btn.classList.toggle('active', (i === 0 && size === 4) || (i === 1 && size === 6));
            });
            initGame();
        }
        
        function initGame() {
            document.getElementById('winScreen').style.display = 'none';
            
            // Reset state
            cards = [];
            flippedCards = [];
            matchedPairs = 0;
            moves = 0;
            gameStarted = false;
            seconds = 0;
            
            if (timer) clearInterval(timer);
            
            totalPairs = (gridSize * gridSize) / 2;
            
            // Create pairs
            const gameEmojis = emojis.slice(0, totalPairs);
            const cardPairs = [...gameEmojis, ...gameEmojis];
            
            // Shuffle
            for (let i = cardPairs.length - 1; i > 0; i--) {
                const j = Math.floor(Math.random() * (i + 1));
                [cardPairs[i], cardPairs[j]] = [cardPairs[j], cardPairs[i]];
            }
            
            cards = cardPairs.map((emoji, index) => ({
                id: index,
                emoji: emoji,
                flipped: false,
                matched: false
            }));
            
            // Update grid
            const board = document.getElementById('gameBoard');
            board.style.gridTemplateColumns = `repeat(${gridSize}, 1fr)`;
            
            renderBoard();
            updateStats();
        }
        
        function renderBoard() {
            const board = document.getElementById('gameBoard');
            board.innerHTML = '';
            
            cards.forEach(card => {
                const cardEl = document.createElement('div');
                cardEl.className = 'card';
                if (card.flipped) cardEl.classList.add('flipped');
                if (card.matched) cardEl.classList.add('matched');
                
                cardEl.innerHTML = `
                    <span class='front'>${card.emoji}</span>
                    <span class='back'>?</span>
                `;
                
                cardEl.onclick = () => flipCard(card.id);
                board.appendChild(cardEl);
            });
        }
        
        function flipCard(id) {
            const card = cards[id];
            
            // Ignore if already flipped/matched or two cards are being checked
            if (card.flipped || card.matched || flippedCards.length >= 2) return;
            
            // Start timer on first move
            if (!gameStarted) {
                gameStarted = true;
                timer = setInterval(() => {
                    seconds++;
                    updateStats();
                }, 1000);
            }
            
            // Flip card
            card.flipped = true;
            flippedCards.push(card);
            renderBoard();
            
            // Check for match
            if (flippedCards.length === 2) {
                moves++;
                updateStats();
                
                if (flippedCards[0].emoji === flippedCards[1].emoji) {
                    // Match!
                    flippedCards[0].matched = true;
                    flippedCards[1].matched = true;
                    matchedPairs++;
                    flippedCards = [];
                    renderBoard();
                    
                    // Check win
                    if (matchedPairs === totalPairs) {
                        clearInterval(timer);
                        showWin();
                    }
                } else {
                    // No match - flip back after delay
                    setTimeout(() => {
                        flippedCards[0].flipped = false;
                        flippedCards[1].flipped = false;
                        flippedCards = [];
                        renderBoard();
                    }, 1000);
                }
            }
        }
        
        function updateStats() {
            document.getElementById('moves').textContent = moves;
            document.getElementById('pairs').textContent = `${matchedPairs}/${totalPairs}`;
            
            const mins = Math.floor(seconds / 60);
            const secs = seconds % 60;
            document.getElementById('time').textContent = `${mins}:${secs.toString().padStart(2, '0')}`;
        }
        
        function showWin() {
            const key = `memoryBest_${gridSize}`;
            const bestTime = localStorage.getItem(key);
            
            document.getElementById('finalMoves').textContent = moves;
            document.getElementById('finalTime').textContent = document.getElementById('time').textContent;
            
            if (!bestTime || seconds < parseInt(bestTime)) {
                localStorage.setItem(key, seconds.toString());
                document.getElementById('bestTime').textContent = '?? New Best Time!';
            } else {
                const bestMins = Math.floor(parseInt(bestTime) / 60);
                const bestSecs = parseInt(bestTime) % 60;
                document.getElementById('bestTime').textContent = 
                    `Best Time: ${bestMins}:${bestSecs.toString().padStart(2, '0')}`;
            }
            
            document.getElementById('winScreen').style.display = 'flex';
        }
        
        // Initialize
        initGame();
    </script>
</body>
</html>";
    }
}
