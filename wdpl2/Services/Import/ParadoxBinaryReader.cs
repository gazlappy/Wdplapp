using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Wdpl2.Services.Import;

/// <summary>
/// Low-level binary reader for Paradox database files (.DB format).
/// 
/// Paradox file format (7.x):
/// - Block size: 2048 bytes
/// - Block 0: Header (contains metadata)
/// - Block 1+: Data blocks
/// 
/// Header structure (key offsets):
/// - Bytes 0-1: Record size (Int16)
/// - Bytes 6-9: Total record count (Int32)
/// - Byte 33: Number of fields
/// - Offset 78: Field types array (numFields bytes)
/// - Offset 78+numFields: Field sizes array (numFields bytes)
/// - Offset ~200+: Field names (null-terminated strings)
/// 
/// Each data block has a 6-byte header before records.
/// </summary>
public static class ParadoxBinaryReader
{
    private const int BLOCK_SIZE = 2048;
    private const int DATA_START = 2048; // Block 1
    private const int BLOCK_HEADER_SIZE = 6;

    public class ParadoxHeader
    {
        public int RecordSize { get; set; }
        public int RecordCount { get; set; }
        public int FieldCount { get; set; }
        public List<byte> FieldTypes { get; set; } = new();
        public List<byte> FieldSizes { get; set; } = new();
        public List<string> FieldNames { get; set; } = new();
    }

    /// <summary>
    /// Read header information from Paradox file bytes
    /// </summary>
    public static ParadoxHeader ReadHeader(byte[] bytes)
    {
        var header = new ParadoxHeader
        {
            RecordSize = BitConverter.ToInt16(bytes, 0),
            RecordCount = BitConverter.ToInt32(bytes, 6),
            FieldCount = bytes[33]
        };

        // Read field types (at offset 78)
        int typeOffset = 78;
        for (int i = 0; i < header.FieldCount; i++)
        {
            if (typeOffset + i < bytes.Length)
                header.FieldTypes.Add(bytes[typeOffset + i]);
        }

        // Field sizes follow types
        for (int i = 0; i < header.FieldCount; i++)
        {
            if (typeOffset + header.FieldCount + i < bytes.Length)
                header.FieldSizes.Add(bytes[typeOffset + header.FieldCount + i]);
        }

        // Find field names in header (usually after offset 200)
        // Scan for readable ASCII text patterns
        var nameBytes = new StringBuilder();
        for (int i = 200; i < Math.Min(bytes.Length, DATA_START); i++)
        {
            if (bytes[i] >= 32 && bytes[i] < 127)
            {
                nameBytes.Append((char)bytes[i]);
            }
            else if (nameBytes.Length > 0)
            {
                nameBytes.Append('\0');
            }
        }

        // Split on nulls and filter
        var allText = nameBytes.ToString();
        var parts = allText.Split('\0', StringSplitOptions.RemoveEmptyEntries)
            .Where(s => s.Length >= 2 && s.Length <= 30 && 
                        !s.Contains("ascii", StringComparison.OrdinalIgnoreCase) && 
                        !s.All(char.IsDigit))
            .ToList();

        // Skip table name (usually first if ends with .DB) and take field names
        if (parts.Count > 1)
        {
            var skipFirst = parts.FirstOrDefault()?.EndsWith(".DB", StringComparison.OrdinalIgnoreCase) == true;
            header.FieldNames = (skipFirst ? parts.Skip(1) : parts).Take(header.FieldCount).ToList();
        }

        return header;
    }

    /// <summary>
    /// Read all records from Paradox file bytes using header info
    /// </summary>
    public static List<Dictionary<string, object?>> ReadRecords(byte[] bytes, ParadoxHeader header)
    {
        var records = new List<Dictionary<string, object?>>();

        if (header.RecordSize <= 0 || header.RecordCount <= 0)
            return records;

        // Calculate records per block
        int recordsPerBlock = (BLOCK_SIZE - BLOCK_HEADER_SIZE) / header.RecordSize;
        if (recordsPerBlock <= 0) recordsPerBlock = 1;

        for (int rec = 0; rec < header.RecordCount; rec++)
        {
            // Calculate which block this record is in
            int blockNum = rec / recordsPerBlock;
            int recInBlock = rec % recordsPerBlock;

            // Calculate offset
            int blockStart = DATA_START + (blockNum * BLOCK_SIZE);
            int recOffset = blockStart + BLOCK_HEADER_SIZE + (recInBlock * header.RecordSize);

            if (recOffset + header.RecordSize > bytes.Length)
                break;

            var record = new Dictionary<string, object?>();
            int fieldOffset = recOffset;

            for (int f = 0; f < header.FieldTypes.Count && f < header.FieldSizes.Count; f++)
            {
                var fType = header.FieldTypes[f];
                var fSize = header.FieldSizes[f];
                var fName = f < header.FieldNames.Count ? header.FieldNames[f] : $"Field{f + 1}";

                if (fieldOffset + fSize > bytes.Length)
                    break;

                var fieldBytes = new byte[fSize];
                Array.Copy(bytes, fieldOffset, fieldBytes, 0, fSize);

                record[fName] = DecodeField(fieldBytes, fType);
                fieldOffset += fSize;
            }

            records.Add(record);
        }

        return records;
    }

    /// <summary>
    /// Decode a field value based on Paradox type
    /// </summary>
    private static object? DecodeField(byte[] bytes, byte fieldType)
    {
        if (bytes.All(b => b == 0))
            return null;

        switch (fieldType)
        {
            case 0x01: // Alpha (string)
                int len = Array.IndexOf(bytes, (byte)0);
                if (len < 0) len = bytes.Length;
                return Encoding.ASCII.GetString(bytes, 0, len).Trim();

            case 0x02: // Date
                // Paradox stores dates as days since 1/1/1 with high bit set for positive
                if ((bytes[0] & 0x80) == 0x80)
                {
                    int days = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                    if (days > 0 && days < 3000000) // Sanity check
                    {
                        try { return new DateTime(1, 1, 1).AddDays(days - 1); }
                        catch { return null; }
                    }
                }
                return null;

            case 0x03: // Short (Int16)
                // Paradox uses high-bit encoding for sign
                if ((bytes[0] & 0x80) == 0x80)
                    return (short)(((bytes[0] & 0x7F) << 8) | bytes[1]);
                else if (bytes[0] != 0 || bytes[1] != 0)
                    return (short)-(((bytes[0] ^ 0x7F) << 8) | (bytes[1] ^ 0xFF));
                return (short)0;

            case 0x04: // Long (Int32)
            case 0x16: // AutoInc
                // Paradox uses high-bit encoding for sign
                if ((bytes[0] & 0x80) == 0x80)
                    return ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                else if (bytes.Any(b => b != 0))
                    return -(((bytes[0] ^ 0x7F) << 24) | ((bytes[1] ^ 0xFF) << 16) | ((bytes[2] ^ 0xFF) << 8) | (bytes[3] ^ 0xFF));
                return 0;

            case 0x05: // Currency
            case 0x06: // Number (Double/Float)
                // Paradox stores doubles with XOR'd sign bit and reversed byte order
                var modBytes = new byte[8];
                if (bytes.Length >= 8)
                {
                    if ((bytes[0] & 0x80) == 0x80)
                    {
                        modBytes[0] = (byte)(bytes[0] ^ 0x80);
                        Array.Copy(bytes, 1, modBytes, 1, 7);
                    }
                    else
                    {
                        for (int i = 0; i < 8; i++)
                            modBytes[i] = (byte)(bytes[i] ^ 0xFF);
                    }
                    Array.Reverse(modBytes);
                    return BitConverter.ToDouble(modBytes, 0);
                }
                return null;

            case 0x09: // Logical (Boolean)
                // 0x81 = true, 0x80 or 0x00 = false
                return bytes[0] == 0x81;

            case 0x14: // Time
                // Paradox stores time as milliseconds since midnight
                if ((bytes[0] & 0x80) == 0x80)
                {
                    int ms = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                    return TimeSpan.FromMilliseconds(ms);
                }
                return null;

            case 0x15: // Timestamp
                // Combination of date and time
                // First 4 bytes: date, last 4 bytes: time
                if (bytes.Length >= 8)
                {
                    // Date part
                    DateTime? date = null;
                    if ((bytes[0] & 0x80) == 0x80)
                    {
                        int days = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                        if (days > 0 && days < 3000000)
                        {
                            try { date = new DateTime(1, 1, 1).AddDays(days - 1); }
                            catch { }
                        }
                    }

                    // Time part
                    TimeSpan time = TimeSpan.Zero;
                    if ((bytes[4] & 0x80) == 0x80)
                    {
                        int ms = ((bytes[4] & 0x7F) << 24) | (bytes[5] << 16) | (bytes[6] << 8) | bytes[7];
                        time = TimeSpan.FromMilliseconds(ms);
                    }

                    if (date.HasValue)
                        return date.Value.Add(time);
                }
                return null;

            default:
                // Try as string for unknown types
                int strLen = Array.IndexOf(bytes, (byte)0);
                if (strLen < 0) strLen = bytes.Length;
                var str = Encoding.ASCII.GetString(bytes, 0, strLen).Trim();
                return string.IsNullOrEmpty(str) ? null : str;
        }
    }
}
