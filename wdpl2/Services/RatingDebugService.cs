using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Wdpl2.Models;

namespace Wdpl2.Services;

/// <summary>
/// Debug service to trace rating calculations frame-by-frame for comparison with VBA Access database.
/// </summary>
public static class RatingDebugService
{
    public class FrameCalculationDebug
    {
     public int FrameNumber { get; set; }
public DateTime MatchDate { get; set; }
  public string OpponentName { get; set; } = "";
        public int OpponentRating { get; set; }
  public bool Won { get; set; }
   public bool EightBall { get; set; }
 public int BiasX { get; set; }
        public double RatingAttn { get; set; }
        public double ValueTot { get; set; }
  public double WeightingTot { get; set; }
     public int CalculatedRating { get; set; }
    }

    /// <summary>
    /// Calculate rating for a single player with detailed debug output.
/// </summary>
    public static List<FrameCalculationDebug> CalculateWithDebug(
        Guid playerId,
  List<Fixture> allFixtures,
      AppSettings settings)
    {
        var debug = new List<FrameCalculationDebug>();
   var playerFrames = new List<(DateTime date, Guid oppId, bool won, bool eightBall)>();
        var allPlayerRatings = new Dictionary<Guid, int>();

        // Build frame history for ALL players (to get opponent ratings)
     foreach (var fixture in allFixtures.OrderBy(f => f.Date))
        {
            foreach (var frame in fixture.Frames.OrderBy(fr => fr.Number))
 {
   // Track target player's frames
        if (frame.HomePlayerId == playerId && frame.AwayPlayerId.HasValue)
      {
            var oppRating = allPlayerRatings.GetValueOrDefault(frame.AwayPlayerId.Value, settings.RatingStartValue);
        playerFrames.Add((fixture.Date, frame.AwayPlayerId.Value, 
             frame.Winner == FrameWinner.Home, 
       frame.EightBall && frame.Winner == FrameWinner.Home));
    }
        else if (frame.AwayPlayerId == playerId && frame.HomePlayerId.HasValue)
         {
        var oppRating = allPlayerRatings.GetValueOrDefault(frame.HomePlayerId.Value, settings.RatingStartValue);
        playerFrames.Add((fixture.Date, frame.HomePlayerId.Value, 
   frame.Winner == FrameWinner.Away, 
        frame.EightBall && frame.Winner == FrameWinner.Away));
     }

    // Update ratings for both players after each frame
          if (frame.HomePlayerId.HasValue)
    {
        var homeFrames = GetFramesUpTo(allFixtures, frame.HomePlayerId.Value, fixture.Date, frame.Number);
       allPlayerRatings[frame.HomePlayerId.Value] = CalculateRating(homeFrames, allPlayerRatings, settings);
          }
 if (frame.AwayPlayerId.HasValue)
        {
       var awayFrames = GetFramesUpTo(allFixtures, frame.AwayPlayerId.Value, fixture.Date, frame.Number);
 allPlayerRatings[frame.AwayPlayerId.Value] = CalculateRating(awayFrames, allPlayerRatings, settings);
     }
            }
        }

        // Now calculate with debug for target player
        double valueTot = 0;
    double weightingTot = 0;
        int totalFrames = playerFrames.Count;

 int frameNum = 0;
        foreach (var (date, oppId, won, eightBall) in playerFrames)
        {
     frameNum++;
    var oppRating = allPlayerRatings.GetValueOrDefault(oppId, settings.RatingStartValue);

          double ratingAttn;
      if (won)
     {
         if (eightBall && settings.UseEightBallFactor)
      ratingAttn = oppRating * settings.EightBallFactor;
       else
ratingAttn = oppRating * settings.WinFactor;
 }
     else
            {
     ratingAttn = oppRating * settings.LossFactor;
     }

       // Calculate weight: newest frame gets base, older frames decrement
            int framesAgo = totalFrames - frameNum;
            int biasX = settings.RatingWeighting - (settings.RatingsBias * framesAgo);
            if (biasX < 1) biasX = 1;

            weightingTot += biasX;
          valueTot += ratingAttn * biasX;
            int currentRating = (int)Math.Round(valueTot / weightingTot);

 debug.Add(new FrameCalculationDebug
      {
              FrameNumber = frameNum,
   MatchDate = date,
         OpponentName = oppId.ToString(),
         OpponentRating = oppRating,
          Won = won,
       EightBall = eightBall,
        BiasX = biasX,
    RatingAttn = ratingAttn,
  ValueTot = valueTot,
    WeightingTot = weightingTot,
 CalculatedRating = currentRating
   });

        biasX += settings.RatingsBias;
        }

        return debug;
    }

    private static List<(DateTime date, Guid oppId, int oppRating, bool won, bool eightBall)> GetFramesUpTo(
        List<Fixture> fixtures, Guid playerId, DateTime upToDate, int upToFrameNum)
    {
        var frames = new List<(DateTime, Guid, int, bool, bool)>();
  // Implementation would mirror main logic
        return frames;
    }

    private static int CalculateRating(
   List<(DateTime date, Guid oppId, int oppRating, bool won, bool eightBall)> frames,
        Dictionary<Guid, int> ratings,
        AppSettings settings)
    {
        if (frames.Count == 0) return settings.RatingStartValue;

        double valueTot = 0;
        double weightingTot = 0;
        int totalFrames = frames.Count;

        // Process frames in chronological order (oldest to newest)
        for (int i = 0; i < totalFrames; i++)
        {
            var (date, oppId, oppRating, won, eightBall) = frames[i];
            
            // Calculate weight: newest frame gets base weight, older frames decrement
            int framesAgo = totalFrames - 1 - i;
            int biasX = settings.RatingWeighting - (settings.RatingsBias * framesAgo);
            if (biasX < 1) biasX = 1;

            double ratingAttn;
            if (won)
          {
          if (eightBall && settings.UseEightBallFactor)
ratingAttn = oppRating * settings.EightBallFactor;
         else
  ratingAttn = oppRating * settings.WinFactor;
            }
            else
            {
           ratingAttn = oppRating * settings.LossFactor;
  }

    weightingTot += biasX;
            valueTot += ratingAttn * biasX;
    }

 return (int)Math.Round(valueTot / weightingTot);
    }

    /// <summary>
    /// Export debug calculation to CSV format.
    /// </summary>
    public static string ExportToCsv(List<FrameCalculationDebug> debug, string playerName)
    {
     var sb = new StringBuilder();
        sb.AppendLine($"=== RATING CALCULATION DEBUG: {playerName} ===");
        sb.AppendLine("Frame,Date,Opponent,OppRating,Won,8Ball,BiasX,RatingAttn,ValueTot,WeightingTot,CalcRating");

    foreach (var frame in debug)
     {
            sb.AppendLine($"{frame.FrameNumber},{frame.MatchDate:yyyy-MM-dd},{frame.OpponentName}," +
      $"{frame.OpponentRating},{frame.Won},{frame.EightBall},{frame.BiasX}," +
       $"{frame.RatingAttn:F2},{frame.ValueTot:F2},{frame.WeightingTot:F2},{frame.CalculatedRating}");
   }

        return sb.ToString();
    }
}
