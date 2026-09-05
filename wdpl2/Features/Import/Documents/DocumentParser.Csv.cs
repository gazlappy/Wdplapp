using System.Text;

namespace Wdpl2.Services;

public static partial class DocumentParser
{
    private static async Task<ParsedDocument> ParseCsvDocumentAsync(string path)
    {
        var result = new ParsedDocument { Format = DocumentFormat.Excel, FileName = Path.GetFileName(path) };
        try
        {
            var text = await File.ReadAllTextAsync(path);
            var delimiter = DetectCsvDelimiter(text);
            var rows = ParseCsvRows(text, delimiter);
            if (rows.Count < 2) throw new InvalidDataException("CSV must contain a header and at least one data row.");
            var headers = rows[0].Select(s => s.Trim()).ToList();
            if (headers.Any(string.IsNullOrEmpty) || headers.Distinct(StringComparer.OrdinalIgnoreCase).Count() != headers.Count)
                throw new InvalidDataException("CSV headers must be non-empty and unique.");
            for (var i = 1; i < rows.Count; i++)
                if (rows[i].Count != headers.Count)
                    throw new InvalidDataException($"CSV row {i + 1} has {rows[i].Count} fields; expected {headers.Count}.");
            rows[0] = headers;
            result.Tables.Add(new TableData { Name = Path.GetFileNameWithoutExtension(path), Rows = rows });
            result.Success = true;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"CSV parse error: {ex.Message}");
        }
        return result;
    }

    private static char DetectCsvDelimiter(string text)
    {
        var counts = new Dictionary<char, int> { [','] = 0, [';'] = 0, ['\t'] = 0, ['|'] = 0 };
        bool quoted = false;
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (c == '"')
            {
                if (quoted && i + 1 < text.Length && text[i + 1] == '"') { i++; continue; }
                quoted = !quoted;
            }
            if (!quoted && (c == '\r' || c == '\n')) break;
            if (!quoted && counts.ContainsKey(c)) counts[c]++;
        }
        return counts.OrderByDescending(p => p.Value).First().Key;
    }

    private static List<List<string>> ParseCsvRows(string text, char delimiter)
    {
        var rows = new List<List<string>>();
        var row = new List<string>();
        var field = new StringBuilder();
        bool quoted = false;
        bool closed = false;
        void EndRow()
        {
            row.Add(field.ToString());
            field.Clear();
            if (row.Any(v => !string.IsNullOrWhiteSpace(v))) rows.Add(row);
            row = new List<string>();
            closed = false;
        }
        for (var i = 0; i < text.Length; i++)
        {
            char c = text[i];
            if (quoted)
            {
                if (c == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else { quoted = false; closed = true; }
                }
                else field.Append(c);
            }
            else if (c == delimiter)
            {
                row.Add(field.ToString()); field.Clear(); closed = false;
            }
            else if (c == '\r' || c == '\n')
            {
                if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++;
                EndRow();
            }
            else if (c == '"' && field.Length == 0 && !closed) quoted = true;
            else if (c == '"' || closed) throw new InvalidDataException("Unexpected text or quote after a CSV field.");
            else field.Append(c);
        }
        if (quoted) throw new InvalidDataException("Unterminated quoted CSV field.");
        if (field.Length > 0 || row.Count > 0 || closed) EndRow();
        return rows;
    }
}
