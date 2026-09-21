# Player Rating Calculation - VBA Bug Replication

## Overview
The rating calculation in this application **intentionally replicates a bug** from the original VBA Access database to maintain compatibility with historical ratings.

## VBA Constants (from original modGeneral module)
- `RATINGSTART = 1000` - Starting rating for new players
- `Rating = 240` - Base weighting constant (RatingWeighting)
- `RatingBias = 4` - Weight increment per frame
- `RATINGWIN = 1.25` - Win factor multiplier
- `RATINGLOSE = 0.75` - Loss factor multiplier
- `RATING8BALL = 1.35` - 8-ball win factor multiplier

## Weight Calculation Formula

### Initial Weight (BiasX)
```
BiasX = 240 - (4 × (TotalFrames - 1))
```

**Example for 24 frames:**
- Initial BiasX = 240 - (4 × 23) = 240 - 92 = **148** (starts LOW)

### Per-Frame Weight Progression
The weight INCREASES by 4 for each frame:
- Frame 1: weight = 148 ? uses **152** in calculation (bug!)
- Frame 2: weight = 152 ? uses **156**
- Frame 3: weight = 156 ? uses **160**
- ...
- Frame 24: weight = 240 ? uses **244**

## The VBA Bug

### What the VBA Code Does (with bug)
```vba
' Initialization:
BiasX = RatingWeighting - (4 * (RecordCount - 1))  ' Starts at 148 for 24 frames

' For each frame:
WeightingTot = WeightingTot + BiasX' Add current weight
BiasX = BiasX + RatingBias   ' Increment weight
ValueTot = ValueTot + (RatingAttn * BiasX)  ' ? Uses NEXT (higher) weight!
```

### What It SHOULD Do (mathematically correct)
```vba
' For each frame:
WeightingTot = WeightingTot + BiasX    ' Add current weight
ValueTot = ValueTot + (RatingAttn * BiasX)  ' Use CURRENT weight
BiasX = BiasX + RatingBias             ' Increment AFTER
```

### The Effect
Because BiasX is incremented BEFORE being used in ValueTot:
- Each frame uses the **next frame's higher weight** instead of its own weight
- Frame 1 should use 148 but uses 152
- Frame 24 should use 240 but uses 244
- This systematically overweights later frames and underweights early frames
- Makes ratings slightly **higher** overall than they mathematically should be

## C# Implementation

### Current Implementation (Matches VBA Bug)
```csharp
// Start with LOW weight
int biasX = Settings.RatingWeighting - (Settings.RatingsBias * (frames.Count - 1));
// For 24 frames: biasX = 240 - (4 × 23) = 148

foreach (var frame in frames.OrderBy(f => f.FrameNumber))
{
    double ratingAttn = CalculateRatingAttn(frame);
    
    weightingTot += biasX; // Uses current weight
    biasX += Settings.RatingsBias;   // Increments BEFORE valueTot (THE BUG!)
    valueTot += ratingAttn * biasX;    // Uses already-incremented weight
}

return (int)Math.Round(valueTot / weightingTot);
```

### To Fix the Bug (if desired)
Simply swap the last two lines:
```csharp
weightingTot += biasX;
valueTot += ratingAttn * biasX;     // Use current weight BEFORE incrementing
biasX += Settings.RatingsBias;      // Increment AFTER
```

**Warning:** Fixing this bug will change ALL player ratings throughout history and break compatibility with the original VBA ratings.
