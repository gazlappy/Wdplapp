using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Wdpl2.Services;

/// <summary>
/// Analyzer for Paradox database binary format (.DB files)
/// Based on the Paradox file format specification
/// </summary>
public static class ParadoxBinaryAnalyzer
{
    /// <summary>
    /// Paradox file header structure (first 88 bytes)
    /// </summary>
    public class ParadoxHeader
    {
        public int RecordSize { get; set; }           // Bytes 0-1: Size of each record
        public int HeaderSize { get; set; }            // Bytes 2-3: Size of header block
        public byte FileType { get; set; }             // Byte 4: File type (0=indexed, 2=non-indexed)
        public byte MaxTableSize { get; set; }         // Byte 5: Max table size
        public int RecordCount { get; set; }           // Bytes 6-9: Number of records
        public int NextBlock { get; set; }             // Bytes 10-11: Next block to use
        public int FileBlocks { get; set; }            // Bytes 12-13: Total file blocks
        public int FirstBlock { get; set; }            // Bytes 14-15: First data block
        public int LastBlock { get; set; }             // Bytes 16-17: Last data block
        public int FieldCount { get; set; }            // Bytes 33: Number of fields
        public int PrimaryKeyFields { get; set; }      // Bytes 34: Primary key fields
        public int CodePage { get; set; }              // Bytes 38-41: Code page
        public int SortOrder { get; set; }             // Bytes 42-43: Sort order
        public byte FileVersionId { get; set; }        // Byte 57: File version
        public List<FieldDefinition> Fields { get; set; } = new();
    }

    /// <summary>
    /// Field definition from Paradox header
    /// </summary>
    public class FieldDefinition
    {
        public byte FieldType { get; set; }
        public byte FieldSize { get; set; }
        public string FieldName { get; set; } = "";
        public string TypeName => FieldType switch
        {
            0x01 => "Alpha",        // String
            0x02 => "Date",         // Date (4 bytes)
            0x03 => "Short",        // Short integer (2 bytes)
            0x04 => "Long",         // Long integer (4 bytes)
            0x05 => "Currency",     // Currency (8 bytes)
            0x06 => "Number",       // Double (8 bytes)
            0x09 => "Logical",      // Boolean (1 byte)
            0x0C => "Memo",         // Memo (variable)
            0x0D => "BLOB",         // Binary large object
            0x14 => "Time",         // Time (4 bytes)
            0x15 => "Timestamp",    // Timestamp (8 bytes)
            0x16 => "AutoInc",      // Auto increment (4 bytes)
            _ => $"Unknown(0x{FieldType:X2})"
        };
    }

    /// <summary>
    /// Analyze a Paradox .DB file and return its structure
    /// </summary>
    public static ParadoxHeader? AnalyzeFile(string filePath)
    {
        if (!File.Exists(filePath))
            return null;

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            if (bytes.Length < 88)
                return null;

            var header = new ParadoxHeader
            {
                RecordSize = BitConverter.ToInt16(bytes, 0),
                HeaderSize = BitConverter.ToInt16(bytes, 2),
                FileType = bytes[4],
                MaxTableSize = bytes[5],
                RecordCount = BitConverter.ToInt32(bytes, 6),
                NextBlock = BitConverter.ToInt16(bytes, 10),
                FileBlocks = BitConverter.ToInt16(bytes, 12),
                FirstBlock = BitConverter.ToInt16(bytes, 14),
                LastBlock = BitConverter.ToInt16(bytes, 16),
                FieldCount = bytes[33],
                PrimaryKeyFields = bytes[34],
                CodePage = BitConverter.ToInt32(bytes, 38),
                SortOrder = BitConverter.ToInt16(bytes, 42),
                FileVersionId = bytes[57]
            };

            // Read field definitions
            // Field types start at offset 78
            // Field sizes follow field types
            // Field names are after the sizes
            
            int fieldTypeOffset = 78;
            int fieldSizeOffset = fieldTypeOffset + header.FieldCount;
            
            // Field names are stored after a table name at offset around header.HeaderSize
            // They're null-terminated strings
            
            for (int i = 0; i < header.FieldCount; i++)
            {
                if (fieldTypeOffset + i >= bytes.Length || fieldSizeOffset + i >= bytes.Length)
                    break;

                var field = new FieldDefinition
                {
                    FieldType = bytes[fieldTypeOffset + i],
                    FieldSize = bytes[fieldSizeOffset + i]
                };
                header.Fields.Add(field);
            }

            // Try to extract field names
            // They're located after the field type/size arrays, as null-terminated strings
            int nameOffset = fieldSizeOffset + header.FieldCount;
            
            // Skip table name first (it's before field names)
            // Table name starts at offset 78 + fieldCount*2 + 4 (or thereabouts)
            // Let's scan for the field names
            int currentField = 0;
            var currentName = new StringBuilder();
            
            for (int i = nameOffset; i < Math.Min(bytes.Length, header.HeaderSize * 2048) && currentField < header.Fields.Count; i++)
            {
                if (bytes[i] == 0)
                {
                    if (currentName.Length > 0)
                    {
                        header.Fields[currentField].FieldName = currentName.ToString();
                        currentField++;
                        currentName.Clear();
                    }
                }
                else if (bytes[i] >= 32 && bytes[i] < 127)
                {
                    currentName.Append((char)bytes[i]);
                }
                else
                {
                    // Non-printable, might be end of names section
                    if (currentName.Length > 0)
                    {
                        header.Fields[currentField].FieldName = currentName.ToString();
                        currentField++;
                        currentName.Clear();
                    }
                }
            }

            return header;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Read all records from a Paradox file
    /// </summary>
    public static List<Dictionary<string, object?>> ReadRecords(string filePath)
    {
        var records = new List<Dictionary<string, object?>>();
        
        var header = AnalyzeFile(filePath);
        if (header == null || header.RecordCount == 0 || header.Fields.Count == 0)
            return records;

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            
            // Data blocks start after header
            // Block size is typically 2048 bytes (MaxTableSize * 0x400)
            int blockSize = header.MaxTableSize * 0x400;
            if (blockSize == 0) blockSize = 2048;
            
            // First data block
            int dataStart = header.HeaderSize * blockSize;
            if (dataStart >= bytes.Length)
                dataStart = blockSize; // Fallback
            
            // Read records
            int recordsRead = 0;
            int offset = dataStart;
            
            while (recordsRead < header.RecordCount && offset + header.RecordSize <= bytes.Length)
            {
                var record = ReadRecord(bytes, offset, header);
                if (record != null && record.Any())
                {
                    records.Add(record);
                    recordsRead++;
                }
                offset += header.RecordSize;
                
                // Check for block boundary
                int blockOffset = (offset - dataStart) % blockSize;
                if (blockOffset + header.RecordSize > blockSize)
                {
                    // Move to next block
                    offset = dataStart + ((offset - dataStart) / blockSize + 1) * blockSize;
                }
            }
        }
        catch
        {
            // Return what we have
        }

        return records;
    }

    /// <summary>
    /// Read a single record at the given offset
    /// </summary>
    private static Dictionary<string, object?>? ReadRecord(byte[] bytes, int offset, ParadoxHeader header)
    {
        var record = new Dictionary<string, object?>();
        int fieldOffset = offset;

        foreach (var field in header.Fields)
        {
            if (fieldOffset + field.FieldSize > bytes.Length)
                break;

            var fieldBytes = new byte[field.FieldSize];
            Array.Copy(bytes, fieldOffset, fieldBytes, 0, field.FieldSize);

            object? value = field.FieldType switch
            {
                0x01 => ReadAlphaField(fieldBytes),           // Alpha (string)
                0x02 => ReadDateField(fieldBytes),            // Date
                0x03 => ReadShortField(fieldBytes),           // Short
                0x04 => ReadLongField(fieldBytes),            // Long
                0x05 => ReadCurrencyField(fieldBytes),        // Currency
                0x06 => ReadNumberField(fieldBytes),          // Number (double)
                0x09 => ReadLogicalField(fieldBytes),         // Logical
                0x16 => ReadAutoIncField(fieldBytes),         // AutoInc
                _ => ReadAlphaField(fieldBytes)               // Default to string
            };

            var fieldName = string.IsNullOrEmpty(field.FieldName) 
                ? $"Field{header.Fields.IndexOf(field) + 1}" 
                : field.FieldName;
            
            record[fieldName] = value;
            fieldOffset += field.FieldSize;
        }

        return record;
    }

    /// <summary>
    /// Read an Alpha (string) field
    /// </summary>
    private static string? ReadAlphaField(byte[] bytes)
    {
        // Find null terminator or end
        int length = Array.IndexOf(bytes, (byte)0);
        if (length < 0) length = bytes.Length;
        
        if (length == 0)
            return null;

        return Encoding.ASCII.GetString(bytes, 0, length).Trim();
    }

    /// <summary>
    /// Read a Date field (Paradox date format)
    /// Paradox stores dates as days since January 1, year 1 (Julian day offset)
    /// </summary>
    private static DateTime? ReadDateField(byte[] bytes)
    {
        if (bytes.Length < 4)
            return null;

        // Paradox date is stored as big-endian integer with high bit set
        // The format is: 0x80 | (value >> 24), followed by the other bytes
        if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0)
            return null;

        // Check for Paradox format (high bit set)
        if ((bytes[0] & 0x80) == 0x80)
        {
            // Clear high bit and read as big-endian
            int value = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
            
            if (value > 0)
            {
                // Paradox epoch is January 1, year 1 (Julian day 1721426)
                // Convert to .NET DateTime
                try
                {
                    // value is days since Paradox epoch
                    // Paradox epoch corresponds to Dec 31, year 0 (Julian)
                    return new DateTime(1, 1, 1).AddDays(value - 1);
                }
                catch
                {
                    return null;
                }
            }
        }

        return null;
    }

    /// <summary>
    /// Read a Short (16-bit integer) field
    /// </summary>
    private static short? ReadShortField(byte[] bytes)
    {
        if (bytes.Length < 2)
            return null;

        // Check for Paradox format (high bit indicates sign)
        if (bytes[0] == 0 && bytes[1] == 0)
            return null;

        // Paradox stores shorts as big-endian with high bit XOR for sign
        if ((bytes[0] & 0x80) == 0x80)
        {
            // Positive value
            return (short)(((bytes[0] & 0x7F) << 8) | bytes[1]);
        }
        else
        {
            // Negative value
            return (short)-(((bytes[0] ^ 0x7F) << 8) | (bytes[1] ^ 0xFF));
        }
    }

    /// <summary>
    /// Read a Long (32-bit integer) field
    /// </summary>
    private static int? ReadLongField(byte[] bytes)
    {
        if (bytes.Length < 4)
            return null;

        if (bytes[0] == 0 && bytes[1] == 0 && bytes[2] == 0 && bytes[3] == 0)
            return null;

        // Paradox stores longs as big-endian with high bit for sign
        if ((bytes[0] & 0x80) == 0x80)
        {
            // Positive value - clear high bit and read big-endian
            return ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
        }
        else
        {
            // Negative value - XOR and negate
            int value = ((bytes[0] ^ 0x7F) << 24) | ((bytes[1] ^ 0xFF) << 16) | 
                       ((bytes[2] ^ 0xFF) << 8) | (bytes[3] ^ 0xFF);
            return -value;
        }
    }

    /// <summary>
    /// Read an AutoInc field (same as Long)
    /// </summary>
    private static int? ReadAutoIncField(byte[] bytes)
    {
        return ReadLongField(bytes);
    }

    /// <summary>
    /// Read a Number (double) field
    /// </summary>
    private static double? ReadNumberField(byte[] bytes)
    {
        if (bytes.Length < 8)
            return null;

        // Check for null/zero
        bool allZero = true;
        for (int i = 0; i < 8; i++)
        {
            if (bytes[i] != 0)
            {
                allZero = false;
                break;
            }
        }
        if (allZero)
            return null;

        // Paradox stores doubles in a modified IEEE format
        // The sign bit and exponent bits are XORed
        var modified = new byte[8];
        
        // First byte has special handling
        if ((bytes[0] & 0x80) == 0x80)
        {
            // Positive number - XOR first byte with 0x80
            modified[0] = (byte)(bytes[0] ^ 0x80);
            for (int i = 1; i < 8; i++)
                modified[i] = bytes[i];
        }
        else
        {
            // Negative number - XOR all bytes with 0xFF
            for (int i = 0; i < 8; i++)
                modified[i] = (byte)(bytes[i] ^ 0xFF);
        }

        // Reverse for little-endian
        Array.Reverse(modified);

        return BitConverter.ToDouble(modified, 0);
    }

    /// <summary>
    /// Read a Currency field
    /// </summary>
    private static decimal? ReadCurrencyField(byte[] bytes)
    {
        var number = ReadNumberField(bytes);
        return number.HasValue ? (decimal)number.Value : null;
    }

    /// <summary>
    /// Read a Logical (boolean) field
    /// </summary>
    private static bool? ReadLogicalField(byte[] bytes)
    {
        if (bytes.Length < 1)
            return null;

        return bytes[0] switch
        {
            0x00 => null,        // Unknown/null
            0x80 => false,       // False
            0x81 => true,        // True
            _ => bytes[0] != 0   // Fallback
        };
    }

    /// <summary>
    /// Get a diagnostic report of a Paradox file
    /// </summary>
    public static string GetDiagnosticReport(string filePath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Paradox File Analysis: {Path.GetFileName(filePath)} ===");
        sb.AppendLine();

        var header = AnalyzeFile(filePath);
        if (header == null)
        {
            sb.AppendLine("ERROR: Could not parse file header");
            return sb.ToString();
        }

        sb.AppendLine("HEADER INFO:");
        sb.AppendLine($"  Record Size: {header.RecordSize} bytes");
        sb.AppendLine($"  Header Size: {header.HeaderSize} blocks");
        sb.AppendLine($"  File Type: {header.FileType} ({(header.FileType == 0 ? "Indexed" : "Non-indexed")})");
        sb.AppendLine($"  Max Table Size: {header.MaxTableSize} ({header.MaxTableSize * 0x400} bytes/block)");
        sb.AppendLine($"  Record Count: {header.RecordCount}");
        sb.AppendLine($"  File Blocks: {header.FileBlocks}");
        sb.AppendLine($"  First Data Block: {header.FirstBlock}");
        sb.AppendLine($"  Last Data Block: {header.LastBlock}");
        sb.AppendLine($"  Field Count: {header.FieldCount}");
        sb.AppendLine($"  Primary Key Fields: {header.PrimaryKeyFields}");
        sb.AppendLine($"  File Version: {header.FileVersionId}");
        sb.AppendLine();

        sb.AppendLine("FIELD DEFINITIONS:");
        foreach (var field in header.Fields)
        {
            sb.AppendLine($"  [{header.Fields.IndexOf(field) + 1}] {field.FieldName,-20} Type: {field.TypeName,-12} Size: {field.FieldSize} bytes");
        }
        sb.AppendLine();

        // Read some sample records
        var records = ReadRecords(filePath);
        sb.AppendLine($"RECORDS READ: {records.Count}");
        
        if (records.Any())
        {
            sb.AppendLine();
            sb.AppendLine("SAMPLE DATA (first 5 records):");
            foreach (var record in records.Take(5))
            {
                sb.AppendLine("  ---");
                foreach (var kvp in record)
                {
                    var displayValue = kvp.Value switch
                    {
                        null => "(null)",
                        string s when string.IsNullOrWhiteSpace(s) => "(empty)",
                        DateTime dt => dt.ToString("yyyy-MM-dd"),
                        _ => kvp.Value.ToString()
                    };
                    sb.AppendLine($"    {kvp.Key}: {displayValue}");
                }
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Analyze all .DB files in a folder
    /// </summary>
    public static string AnalyzeFolder(string folderPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"=== Paradox Database Folder Analysis ===");
        sb.AppendLine($"Folder: {folderPath}");
        sb.AppendLine();

        var dbFiles = Directory.GetFiles(folderPath, "*.DB", SearchOption.TopDirectoryOnly)
            .Where(f => !Path.GetFileName(f).StartsWith("a_")) // Skip archive copies
            .OrderBy(f => f)
            .ToList();

        sb.AppendLine($"Found {dbFiles.Count} .DB files (excluding a_* archive files)");
        sb.AppendLine();

        foreach (var file in dbFiles)
        {
            sb.AppendLine(GetDiagnosticReport(file));
            sb.AppendLine();
            sb.AppendLine(new string('=', 60));
            sb.AppendLine();
        }

        return sb.ToString();
    }
}
