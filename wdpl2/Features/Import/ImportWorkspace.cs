using System.Text.Json;
using Wdpl2.Models;

namespace Wdpl2.Services.Import;

public sealed class ImportWorkspace
{
    private static readonly JsonSerializerOptions ComparisonOptions = new()
    {
        Converters = { new TimestampComparer() }
    };
    private readonly IDataStore _store;
    private LeagueData? _baseline;
    private LeagueData? _data;
    private LeagueData? _checkpoint;

    public ImportWorkspace(IDataStore store) => _store = store;

    public LeagueData GetData()
    {
        if (_data == null)
        {
            _baseline = Clone(_store.GetData());
            _data = Clone(_baseline);
        }
        return _data;
    }

    public async Task SaveAsync(CancellationToken ct = default)
    {
        if (_data == null || _baseline == null) return;
        await _store.CommitImportAsync(_baseline, _data, ct);
        _baseline = Clone(_data);
        _checkpoint = null;
    }

    public void Reset() { _data = null; _baseline = null; _checkpoint = null; }
    public bool CreatePreImportSnapshot() { _checkpoint = Clone(GetData()); return true; }
    public void ClearPreImportSnapshot() { _checkpoint = null; }
    public bool RestorePreImportSnapshot()
    {
        if ((_checkpoint ?? _baseline) is { } snapshot) _data = Clone(snapshot);
        return true;
    }

    public static T Clone<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    public static bool Equal<T>(T left, T right) =>
        JsonSerializer.Serialize(left, ComparisonOptions) == JsonSerializer.Serialize(right, ComparisonOptions);

    private sealed class TimestampComparer : System.Text.Json.Serialization.JsonConverter<DateTime>
    {
        public override DateTime Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options) => reader.GetDateTime();
        public override void Write(Utf8JsonWriter writer, DateTime value, JsonSerializerOptions options) =>
            writer.WriteNumberValue(value.Ticks);
    }
}
