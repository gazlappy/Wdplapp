using System.Text.Json;
using Wdpl2.Models;
using Wdpl2.Services.Inbox;

namespace Wdpl2;

public static partial class DataStore
{
    internal static void ApplyEntryReview(string backendId, AdminSyncReviewItem item)
    {
        var next = JsonSerializer.Deserialize<LeagueData>(JsonSerializer.Serialize(Data, JsonOpts), JsonOpts)!;
        var draft = AdminEntryReviewMapper.PrepareApplication(next, backendId, item);
        var entry = AdminEntryReviewMapper.FindMapped(next, backendId, item.Change);
        entry.Status = draft.Status;
        entry.Notes = draft.Notes;
        entry.SourceReviewRequestId = draft.SourceReviewRequestId;
        entry.SourceReviewIntentHash = draft.SourceReviewIntentHash;
        EnsureDataDirectory();
        var temporary = DataPath + "." + Guid.NewGuid().ToString("N") + ".tmp";
        try
        {
            using (var stream = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                JsonSerializer.Serialize(stream, next, JsonOpts);
                stream.Flush(flushToDisk: true);
            }
            if (File.Exists(DataPath)) File.Replace(temporary, DataPath, BackupPath);
            else File.Move(temporary, DataPath);
            Data = next;
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
