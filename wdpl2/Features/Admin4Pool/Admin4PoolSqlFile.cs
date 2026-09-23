using System.Globalization;
using System.Text;

namespace Wdpl2.Services.Admin4Pool;

/// <summary>
/// The tables of one season exported from Admin4Pool, the league's Delphi
/// program of 1998-2013, by its File > Export Season to SQL.
/// </summary>
/// <remarks>
/// Only reads what that export writes: backtick-quoted INSERT statements with
/// NULL, numbers and single-quoted strings (quotes doubled, line breaks kept).
/// CREATE/DROP statements and comments are skipped; a table with no rows simply
/// isn't there. Text is Windows-1252, as the old program wrote it.
/// </remarks>
public sealed class Admin4PoolSqlFile
{
    public const string Header = "-- Admin4Pool season export";

    private readonly Dictionary<string, List<Dictionary<string, object?>>> _tables =
        new(StringComparer.OrdinalIgnoreCase);

    public string? SourceName { get; private init; }

    public IReadOnlyList<Dictionary<string, object?>> Rows(string table) =>
        _tables.TryGetValue(table, out var rows) ? rows : [];

    public bool HasTable(string table) => _tables.ContainsKey(table);

    public static Admin4PoolSqlFile Read(Stream stream, string? sourceName = null)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = new StreamReader(stream, Encoding.GetEncoding(1252));
        return Parse(reader.ReadToEnd(), sourceName);
    }

    public static Admin4PoolSqlFile Parse(string sql, string? sourceName = null)
    {
        if (!sql.TrimStart('﻿').StartsWith(Header, StringComparison.Ordinal))
            throw new InvalidDataException("This isn't an Admin4Pool season export (it should start with \"" + Header + "\").");
        var file = new Admin4PoolSqlFile { SourceName = sourceName };
        var p = new Cursor(sql);
        while (!p.End)
        {
            p.SkipSpaceAndComments();
            if (p.End) break;
            if (p.TryKeyword("INSERT"))
                file.ReadInsert(p);
            else
                p.SkipStatement();
        }
        if (!file.HasTable("League") || !file.HasTable("Match"))
            throw new InvalidDataException("The export has no League or Match rows, so there is no season in it.");
        return file;
    }

    private void ReadInsert(Cursor p)
    {
        p.ExpectKeyword("INTO");
        var table = p.Identifier();
        p.Expect('(');
        var columns = new List<string>();
        do columns.Add(p.Identifier()); while (p.TryChar(','));
        p.Expect(')');
        p.ExpectKeyword("VALUES");
        if (!_tables.TryGetValue(table, out var rows))
            _tables[table] = rows = [];
        do
        {
            p.Expect('(');
            var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
            var i = 0;
            do
            {
                if (i >= columns.Count) throw p.Error("more values than columns");
                row[columns[i++]] = p.Value();
            } while (p.TryChar(','));
            if (i != columns.Count) throw p.Error("fewer values than columns");
            p.Expect(')');
            rows.Add(row);
        } while (p.TryChar(','));
        p.Expect(';');
    }

    private sealed class Cursor(string text)
    {
        private int _pos;
        public bool End => _pos >= text.Length;

        public void SkipSpaceAndComments()
        {
            while (!End)
            {
                if (char.IsWhiteSpace(text[_pos])) _pos++;
                else if (At("--")) { while (!End && text[_pos] != '\n') _pos++; }
                else if (At("/*"))
                {
                    var close = text.IndexOf("*/", _pos + 2, StringComparison.Ordinal);
                    _pos = close < 0 ? text.Length : close + 2;
                }
                else break;
            }
        }

        public void SkipStatement()
        {
            while (!End)
            {
                var c = text[_pos];
                if (c == '\'') ReadString();
                else if (c == '`') Identifier();
                else if (At("--") || At("/*")) SkipSpaceAndComments();
                else { _pos++; if (c == ';') return; }
            }
        }

        public bool TryKeyword(string word)
        {
            SkipSpaceAndComments();
            if (_pos + word.Length > text.Length ||
                string.Compare(text, _pos, word, 0, word.Length, StringComparison.OrdinalIgnoreCase) != 0)
                return false;
            var after = _pos + word.Length;
            if (after < text.Length && (char.IsLetterOrDigit(text[after]) || text[after] == '_')) return false;
            _pos = after;
            return true;
        }

        public void ExpectKeyword(string word)
        {
            if (!TryKeyword(word)) throw Error($"expected {word}");
        }

        public bool TryChar(char c)
        {
            SkipSpaceAndComments();
            if (End || text[_pos] != c) return false;
            _pos++;
            return true;
        }

        public void Expect(char c)
        {
            if (!TryChar(c)) throw Error($"expected '{c}'");
        }

        public string Identifier()
        {
            SkipSpaceAndComments();
            if (End || text[_pos] != '`') throw Error("expected a `name`");
            var close = text.IndexOf('`', _pos + 1);
            if (close < 0) throw Error("unclosed `name`");
            var name = text.Substring(_pos + 1, close - _pos - 1);
            _pos = close + 1;
            return name;
        }

        public object? Value()
        {
            SkipSpaceAndComments();
            if (End) throw Error("expected a value");
            if (text[_pos] == '\'') return ReadString();
            if (TryKeyword("NULL")) return null;
            var start = _pos;
            if (text[_pos] is '-' or '+') _pos++;
            while (!End && (char.IsDigit(text[_pos]) || text[_pos] is '.' or 'e' or 'E' or '-' or '+')) _pos++;
            var token = text[start.._pos];
            if (long.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var whole)) return whole;
            if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var number)) return number;
            throw Error($"can't read value '{token}'");
        }

        private string ReadString()
        {
            var sb = new StringBuilder();
            _pos++;
            while (true)
            {
                if (End) throw Error("unclosed string");
                var c = text[_pos++];
                if (c != '\'') { sb.Append(c); continue; }
                if (!End && text[_pos] == '\'') { sb.Append('\''); _pos++; continue; }
                return sb.ToString();
            }
        }

        private bool At(string s) => string.CompareOrdinal(text, _pos, s, 0, s.Length) == 0;

        public InvalidDataException Error(string what)
        {
            var line = 1;
            for (var i = 0; i < Math.Min(_pos, text.Length); i++) if (text[i] == '\n') line++;
            return new InvalidDataException($"Line {line}: {what}.");
        }
    }
}
