using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Wdpl2.Services;

/// <summary>
/// Deep dive analysis of Paradox database files to understand actual data structure
/// </summary>
public static class ParadoxDeepDive
{
    /// <summary>
    /// Analyze a folder of Paradox files and return detailed diagnostic info
    /// </summary>
    public static string AnalyzeAll(string folderPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("????????????????????????????????????????????????????????????????????");
        sb.AppendLine("?         PARADOX DATABASE DEEP DIVE ANALYSIS                      ?");
        sb.AppendLine("????????????????????????????????????????????????????????????????????");
        sb.AppendLine();
        sb.AppendLine($"Folder: {folderPath}");
        sb.AppendLine($"Analysis Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();

        // Analyze key files in order of importance
        var filesToAnalyze = new[] { "Division.DB", "Team.DB", "Player.DB", "Venue.DB", "Match.DB", "Single.DB", "Dbls.DB" };
        
        foreach (var fileName in filesToAnalyze)
        {
            var filePath = Path.Combine(folderPath, fileName);
            if (File.Exists(filePath))
            {
                sb.AppendLine(AnalyzeFile(filePath));
                sb.AppendLine();
            }
        }

        return sb.ToString();
    }

    /// <summary>
    /// Detailed analysis of a single Paradox file
    /// </summary>
    public static string AnalyzeFile(string filePath)
    {
        var sb = new StringBuilder();
        var fileName = Path.GetFileName(filePath);
        
        sb.AppendLine($"???????????????????????????????????????????????????????????????????");
        sb.AppendLine($"FILE: {fileName}");
        sb.AppendLine($"???????????????????????????????????????????????????????????????????");

        try
        {
            var bytes = File.ReadAllBytes(filePath);
            sb.AppendLine($"File Size: {bytes.Length:N0} bytes ({bytes.Length / 1024.0:F1} KB)");
            sb.AppendLine();

            // Parse header
            if (bytes.Length < 88)
            {
                sb.AppendLine("ERROR: File too small for Paradox header");
                return sb.ToString();
            }

            // Paradox 7 header structure
            var recordSize = BitConverter.ToInt16(bytes, 0);
            var headerSize = BitConverter.ToInt16(bytes, 2);
            var fileType = bytes[4];
            var maxTableSize = bytes[5];
            var numRecords = BitConverter.ToInt32(bytes, 6);
            var usedBlocks = BitConverter.ToInt16(bytes, 10);
            var totalBlocks = BitConverter.ToInt16(bytes, 12);
            var firstBlock = BitConverter.ToInt16(bytes, 14);
            var lastBlock = BitConverter.ToInt16(bytes, 16);
            var numFields = bytes[33];
            var numKeyFields = bytes[34];
            var encryption = BitConverter.ToInt32(bytes, 37);
            var sortOrder = bytes[41];
            var modified1 = bytes[42];
            var modified2 = bytes[43];
            var indexFieldNum = bytes[44];
            var primaryIndexWorkspace = BitConverter.ToInt32(bytes, 45);
            var writeProtected = bytes[55];
            var fileVersionId = bytes[57];
            var maxBlocks = BitConverter.ToInt16(bytes, 58);
            var auxPasswords = bytes[61];
            var cryptInfoStartPtr = BitConverter.ToInt32(bytes, 62);
            var cryptInfoEndPtr = BitConverter.ToInt32(bytes, 66);
            var autoInc = BitConverter.ToInt32(bytes, 73);
            var firstFreeBlock = BitConverter.ToInt16(bytes, 77);
            var indexRootBlock = bytes[79];
            var numIndexLevels = bytes[80];
            var fieldCount32 = BitConverter.ToInt16(bytes, 81);

            sb.AppendLine("HEADER ANALYSIS:");
            sb.AppendLine($"  Record Size:     {recordSize} bytes");
            sb.AppendLine($"  Header Size:     {headerSize} blocks (block = {maxTableSize * 0x400} bytes)");
            sb.AppendLine($"  File Type:       {fileType} ({(fileType == 0 ? "Indexed" : fileType == 2 ? "Non-indexed" : "Unknown")})");
            sb.AppendLine($"  Max Table Size:  {maxTableSize} (x 0x400 = {maxTableSize * 0x400} bytes/block)");
            sb.AppendLine($"  Num Records:     {numRecords}");
            sb.AppendLine($"  Used Blocks:     {usedBlocks}");
            sb.AppendLine($"  Total Blocks:    {totalBlocks}");
            sb.AppendLine($"  First Block:     {firstBlock}");
            sb.AppendLine($"  Last Block:      {lastBlock}");
            sb.AppendLine($"  Num Fields:      {numFields}");
            sb.AppendLine($"  Key Fields:      {numKeyFields}");
            sb.AppendLine($"  File Version:    {fileVersionId}");
            sb.AppendLine($"  Auto Inc:        {autoInc}");
            sb.AppendLine();

            // Field definitions start at offset 78
            sb.AppendLine("FIELD DEFINITIONS:");
            var fieldTypes = new List<byte>();
            var fieldSizes = new List<byte>();
            
            // Field types at offset 78
            for (int i = 0; i < numFields; i++)
            {
                if (78 + i < bytes.Length)
                    fieldTypes.Add(bytes[78 + i]);
            }
            
            // Field sizes immediately follow types
            for (int i = 0; i < numFields; i++)
            {
                if (78 + numFields + i < bytes.Length)
                    fieldSizes.Add(bytes[78 + numFields + i]);
            }

            // Field names are null-terminated strings after the sizes
            var fieldNames = new List<string>();
            int nameStart = 78 + numFields * 2;
            
            // Skip table name first
            while (nameStart < bytes.Length && bytes[nameStart] != 0)
                nameStart++;
            nameStart++; // Skip null
            
            // Read field names
            var currentName = new StringBuilder();
            for (int i = nameStart; i < Math.Min(bytes.Length, headerSize * maxTableSize * 0x400) && fieldNames.Count < numFields; i++)
            {
                if (bytes[i] == 0)
                {
                    if (currentName.Length > 0)
                    {
                        fieldNames.Add(currentName.ToString());
                        currentName.Clear();
                    }
                }
                else if (bytes[i] >= 32 && bytes[i] < 127)
                {
                    currentName.Append((char)bytes[i]);
                }
            }

            int totalFieldSize = 0;
            for (int i = 0; i < numFields; i++)
            {
                var fType = i < fieldTypes.Count ? fieldTypes[i] : (byte)0;
                var fSize = i < fieldSizes.Count ? fieldSizes[i] : (byte)0;
                var fName = i < fieldNames.Count ? fieldNames[i] : $"Field{i + 1}";
                
                var typeName = GetFieldTypeName(fType);
                sb.AppendLine($"  [{i + 1}] {fName,-20} Type: 0x{fType:X2} ({typeName,-10}) Size: {fSize,3} bytes");
                totalFieldSize += fSize;
            }
            sb.AppendLine($"  TOTAL: {totalFieldSize} bytes (record size in header: {recordSize})");
            sb.AppendLine();

            // Calculate data start
            int blockSize = maxTableSize * 0x400;
            if (blockSize == 0) blockSize = 2048;
            int dataStart = headerSize * blockSize;
            if (dataStart == 0) dataStart = blockSize;

            sb.AppendLine($"DATA ANALYSIS:");
            sb.AppendLine($"  Block Size:      {blockSize} bytes");
            sb.AppendLine($"  Data Start:      offset {dataStart} (0x{dataStart:X})");
            sb.AppendLine($"  Expected End:    offset {dataStart + numRecords * recordSize}");
            sb.AppendLine();

            // Read and display sample records
            sb.AppendLine("SAMPLE RECORDS (first 5):");
            int recordsToShow = Math.Min(5, numRecords);
            
            for (int rec = 0; rec < recordsToShow; rec++)
            {
                int recOffset = dataStart + (rec * recordSize);
                
                // Check for block boundary
                int blockNum = (recOffset - dataStart) / blockSize;
                int posInBlock = (recOffset - dataStart) % blockSize;
                
                // Each block has a small header (typically 6 bytes)
                // Adjust for block headers
                int actualOffset = dataStart + (blockNum * blockSize) + 6 + (rec * recordSize) - (blockNum * 6);
                
                if (actualOffset + recordSize > bytes.Length)
                {
                    sb.AppendLine($"  Record {rec + 1}: [Beyond file end at offset {actualOffset}]");
                    continue;
                }

                sb.AppendLine($"  Record {rec + 1} at offset {actualOffset} (0x{actualOffset:X}):");
                
                int fieldOffset = actualOffset;
                for (int f = 0; f < numFields && f < fieldTypes.Count && f < fieldSizes.Count; f++)
                {
                    var fType = fieldTypes[f];
                    var fSize = fieldSizes[f];
                    var fName = f < fieldNames.Count ? fieldNames[f] : $"Field{f + 1}";
                    
                    if (fieldOffset + fSize > bytes.Length)
                    {
                        sb.AppendLine($"    {fName}: [Beyond file end]");
                        break;
                    }

                    var fieldBytes = new byte[fSize];
                    Array.Copy(bytes, fieldOffset, fieldBytes, 0, fSize);
                    
                    var value = DecodeFieldValue(fieldBytes, fType, fSize);
                    var hexPreview = BitConverter.ToString(fieldBytes.Length > 8 ? fieldBytes[0..8] : fieldBytes).Replace("-", " ");
                    if (fieldBytes.Length > 8) hexPreview += "...";
                    
                    sb.AppendLine($"    {fName,-20}: {value,-30} [{hexPreview}]");
                    
                    fieldOffset += fSize;
                }
                sb.AppendLine();
            }

            // Show raw hex of first data block
            sb.AppendLine("RAW DATA (first 256 bytes of data section):");
            for (int i = dataStart; i < Math.Min(dataStart + 256, bytes.Length); i += 16)
            {
                var hex = new StringBuilder();
                var ascii = new StringBuilder();
                
                for (int j = 0; j < 16 && (i + j) < bytes.Length; j++)
                {
                    var b = bytes[i + j];
                    hex.Append($"{b:X2} ");
                    ascii.Append(b >= 32 && b < 127 ? (char)b : '.');
                }
                
                sb.AppendLine($"  {i:X4}: {hex,-48} {ascii}");
            }
        }
        catch (Exception ex)
        {
            sb.AppendLine($"ERROR: {ex.Message}");
        }

        return sb.ToString();
    }

    private static string GetFieldTypeName(byte fieldType)
    {
        return fieldType switch
        {
            0x01 => "Alpha",
            0x02 => "Date",
            0x03 => "Short",
            0x04 => "Long",
            0x05 => "Currency",
            0x06 => "Number",
            0x09 => "Logical",
            0x0C => "Memo",
            0x0D => "BLOB",
            0x14 => "Time",
            0x15 => "Timestamp",
            0x16 => "AutoInc",
            0x17 => "BCD",
            0x18 => "Bytes",
            _ => $"Unknown"
        };
    }

    private static string DecodeFieldValue(byte[] bytes, byte fieldType, int size)
    {
        if (bytes.All(b => b == 0))
            return "(null/empty)";

        try
        {
            switch (fieldType)
            {
                case 0x01: // Alpha (string)
                    int len = Array.IndexOf(bytes, (byte)0);
                    if (len < 0) len = bytes.Length;
                    var str = Encoding.ASCII.GetString(bytes, 0, len).Trim();
                    return string.IsNullOrEmpty(str) ? "(empty)" : $"\"{str}\"";

                case 0x02: // Date
                    if (size >= 4)
                    {
                        // Paradox date: big-endian, high bit set for valid
                        if ((bytes[0] & 0x80) == 0x80)
                        {
                            int days = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                            if (days > 0 && days < 3000000)
                            {
                                try
                                {
                                    var date = new DateTime(1, 1, 1).AddDays(days - 1);
                                    return date.ToString("yyyy-MM-dd");
                                }
                                catch { }
                            }
                            return $"days={days}";
                        }
                        return "(no date)";
                    }
                    break;

                case 0x03: // Short (2 bytes)
                    if (size >= 2)
                    {
                        if ((bytes[0] & 0x80) == 0x80)
                        {
                            // Positive
                            int val = ((bytes[0] & 0x7F) << 8) | bytes[1];
                            return val.ToString();
                        }
                        else if (bytes[0] != 0 || bytes[1] != 0)
                        {
                            // Negative
                            int val = -((((bytes[0] ^ 0x7F)) << 8) | (bytes[1] ^ 0xFF));
                            return val.ToString();
                        }
                        return "0";
                    }
                    break;

                case 0x04: // Long (4 bytes)
                case 0x16: // AutoInc (4 bytes)
                    if (size >= 4)
                    {
                        if ((bytes[0] & 0x80) == 0x80)
                        {
                            // Positive
                            int val = ((bytes[0] & 0x7F) << 24) | (bytes[1] << 16) | (bytes[2] << 8) | bytes[3];
                            return val.ToString();
                        }
                        else if (bytes[0] != 0 || bytes[1] != 0 || bytes[2] != 0 || bytes[3] != 0)
                        {
                            // Negative
                            int val = -(((bytes[0] ^ 0x7F) << 24) | ((bytes[1] ^ 0xFF) << 16) | ((bytes[2] ^ 0xFF) << 8) | (bytes[3] ^ 0xFF));
                            return val.ToString();
                        }
                        return "0";
                    }
                    break;

                case 0x06: // Number (double, 8 bytes)
                    if (size >= 8)
                    {
                        // Paradox double: first byte high bit XOR for sign
                        var modBytes = new byte[8];
                        if ((bytes[0] & 0x80) == 0x80)
                        {
                            // Positive: XOR first byte with 0x80
                            modBytes[0] = (byte)(bytes[0] ^ 0x80);
                            Array.Copy(bytes, 1, modBytes, 1, 7);
                        }
                        else
                        {
                            // Negative: XOR all bytes with 0xFF
                            for (int i = 0; i < 8; i++)
                                modBytes[i] = (byte)(bytes[i] ^ 0xFF);
                        }
                        Array.Reverse(modBytes);
                        double val = BitConverter.ToDouble(modBytes, 0);
                        return val.ToString("F2");
                    }
                    break;

                case 0x09: // Logical (1 byte)
                    return bytes[0] switch
                    {
                        0x00 => "(null)",
                        0x80 => "false",
                        0x81 => "true",
                        _ => $"0x{bytes[0]:X2}"
                    };

                default:
                    // Show hex for unknown types
                    return $"[{BitConverter.ToString(bytes).Replace("-", " ")}]";
            }
        }
        catch (Exception ex)
        {
            return $"(error: {ex.Message})";
        }

        return $"[{BitConverter.ToString(bytes).Replace("-", " ")}]";
    }

    /// <summary>
    /// Scan a file to find text patterns (useful for finding field names)
    /// </summary>
    public static string FindTextPatterns(string filePath)
    {
        var sb = new StringBuilder();
        var bytes = File.ReadAllBytes(filePath);
        
        sb.AppendLine($"Text patterns in {Path.GetFileName(filePath)}:");
        
        var currentText = new StringBuilder();
        int startOffset = 0;
        
        for (int i = 0; i < bytes.Length; i++)
        {
            if (bytes[i] >= 32 && bytes[i] < 127)
            {
                if (currentText.Length == 0)
                    startOffset = i;
                currentText.Append((char)bytes[i]);
            }
            else
            {
                if (currentText.Length >= 3)
                {
                    sb.AppendLine($"  0x{startOffset:X4}: \"{currentText}\"");
                }
                currentText.Clear();
            }
        }
        
        return sb.ToString();
    }
}
