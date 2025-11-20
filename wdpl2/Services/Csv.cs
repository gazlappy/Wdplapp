using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wdpl2.Services
{
    /// <summary>
    /// CSV reader/writer:
    ///   • Read: header row + quoted fields; auto-detects delimiter.
    ///   • Write: strongly-typed builder with per-column selectors.
    /// </summary>
    public static class Csv
    {
        // ------------------------- READ -------------------------

        public static List<Dictionary<string, string>> Read(Stream stream, Encoding? enc = null)
        {
            enc ??= Encoding.UTF8;
            using var sr = new StreamReader(stream, enc, detectEncodingFromByteOrderMarks: true, leaveOpen: true);

            string? headerLine = sr.ReadLine();
            if (headerLine == null) return new();

            // Auto-detect delimiter by highest count among candidates
            char delimiter = DetectDelimiter(headerLine);

            var headers = Split(headerLine, delimiter);
            for (int i = 0; i < headers.Count; i++) headers[i] = headers[i].Trim();

            var rows = new List<Dictionary<string, string>>();

            string? line;
            while ((line = sr.ReadLine()) != null)
            {
                // allow empty lines
                if (string.IsNullOrWhiteSpace(line)) continue;

                var fields = Split(line, delimiter);
                var row = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                for (int i = 0; i < headers.Count; i++)
                {
                    string key = headers[i];
                    string val = i < fields.Count ? fields[i] : "";
                    row[key] = val;
                }
                rows.Add(row);
            }
            return rows;
        }

        private static char DetectDelimiter(string header)
        {
            (char c, int count) best = (',', Count(header, ','));
            int semi = Count(header, ';');
            if (semi > best.count) best = (';', semi);
            int tab = Count(header, '\t');
            if (tab > best.count) best = ('\t', tab);
            int pipe = Count(header, '|');
            if (pipe > best.count) best = ('|', pipe);
            return best.c;
        }

        private static int Count(string s, char ch)
        {
            int n = 0;
            foreach (var c in s) if (c == ch) n++;
            return n;
        }

        // Splits a line into fields supporting quotes and escaped quotes ("")
        private static List<string> Split(string line, char delimiter)
        {
            var res = new List<string>();
            var sb = new StringBuilder();
            bool inQuotes = false;

            for (int i = 0; i < line.Length; i++)
            {
                char c = line[i];
                if (inQuotes)
                {
                    if (c == '"')
                    {
                        // double quote -> escaped quote
                        if (i + 1 < line.Length && line[i + 1] == '"') { sb.Append('"'); i++; }
                        else inQuotes = false;
                    }
                    else sb.Append(c);
                }
                else
                {
                    if (c == delimiter) { res.Add(sb.ToString()); sb.Clear(); }
                    else if (c == '"') inQuotes = true;
                    else sb.Append(c);
                }
            }
            res.Add(sb.ToString());
            return res;
        }

        // ------------------------- WRITE -------------------------

        /// <summary>
        /// Build a CSV string for any sequence using column selectors.
        /// Example:
        /// Csv.ToCsv(teams,
        ///   ("Name", t => t.Name),
        ///   ("Division", t => ...));
        /// </summary>
        public static string ToCsv<T>(IEnumerable<T> rows, params (string Header, Func<T, object?> Select)[] cols)
        {
            var sb = new StringBuilder();

            // Header
            sb.AppendLine(string.Join(",", cols.Select(c => Escape(c.Header))));

            // Rows
            foreach (var r in rows)
            {
                var values = cols.Select(c => Escape(c.Select(r)));
                sb.AppendLine(string.Join(",", values));
            }

            return sb.ToString();
        }

        private static string Escape(object? value)
        {
            var s = value?.ToString() ?? string.Empty;
            // Quote if needed and escape quotes
            if (s.Contains('"') || s.Contains(',') || s.Contains('\n') || s.Contains('\r'))
                return $"\"{s.Replace("\"", "\"\"")}\"";
            return s;
        }
    }
}
