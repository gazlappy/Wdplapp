using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace Wdpl2.Services;

/// <summary>
/// Multi-format document parser for Word, Excel, PowerPoint, PDF, and more
/// Extracts tables and text from various document formats
/// </summary>
public static class DocumentParser
{
    public enum DocumentFormat
    {
        Unknown,
        Word,           // .docx, .doc
        Excel,          // .xlsx, .xls
        PowerPoint,     // .pptx, .ppt
        PDF,            // .pdf
        Text,           // .txt
        RichText,       // .rtf
        OpenDocument    // .odt, .ods, .odp
    }

    public class ParsedDocument
    {
        public DocumentFormat Format { get; set; }
        public string FileName { get; set; } = "";
        public List<string> TextContent { get; set; } = new();
        public List<TableData> Tables { get; set; } = new();
        public Dictionary<string, string> Metadata { get; set; } = new();
        public bool Success { get; set; }
        public List<string> Errors { get; set; } = new();
    }

    public class TableData
    {
        public string Name { get; set; } = "";
        public List<List<string>> Rows { get; set; } = new();
        public int RowCount => Rows.Count;
        public int ColumnCount => Rows.FirstOrDefault()?.Count ?? 0;
    }

    /// <summary>
    /// Detect document format from file extension
    /// </summary>
    public static DocumentFormat DetectFormat(string filePath)
    {
        var extension = Path.GetExtension(filePath).ToLower();
        
        return extension switch
        {
            ".docx" or ".doc" => DocumentFormat.Word,
            ".xlsx" or ".xls" => DocumentFormat.Excel,
            ".pptx" or ".ppt" => DocumentFormat.PowerPoint,
            ".pdf" => DocumentFormat.PDF,
            ".txt" => DocumentFormat.Text,
            ".rtf" => DocumentFormat.RichText,
            ".odt" => DocumentFormat.OpenDocument,
            ".ods" => DocumentFormat.OpenDocument,
            ".odp" => DocumentFormat.OpenDocument,
            _ => DocumentFormat.Unknown
        };
    }

    /// <summary>
    /// Parse any supported document format
    /// </summary>
    public static async Task<ParsedDocument> ParseDocumentAsync(string filePath)
    {
        var format = DetectFormat(filePath);
        var result = new ParsedDocument
        {
            Format = format,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            switch (format)
            {
                case DocumentFormat.Word:
                    return await ParseWordDocumentAsync(filePath);
                
                case DocumentFormat.Excel:
                    return await ParseExcelDocumentAsync(filePath);
                
                case DocumentFormat.PowerPoint:
                    return await ParsePowerPointAsync(filePath);
                
                case DocumentFormat.PDF:
                    return await ParsePDFAsync(filePath);
                
                case DocumentFormat.Text:
                    return await ParseTextFileAsync(filePath);
                
                case DocumentFormat.RichText:
                    return await ParseRTFAsync(filePath);
                
                case DocumentFormat.OpenDocument:
                    return await ParseOpenDocumentAsync(filePath);
                
                default:
                    result.Errors.Add($"Unsupported format: {format}");
                    return result;
            }
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse Word document (.docx, .doc)
    /// NOTE: Uses DocumentFormat.OpenXml with automatic .doc conversion
    /// </summary>
    private static async Task<ParsedDocument> ParseWordDocumentAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.Word,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            // Use the new WordDocumentParser for both .doc and .docx
            var wordResult = await WordDocumentParser.ParseWordDocumentAsync(filePath);

            if (wordResult.Success)
            {
                // Convert WordTable to TableData
                foreach (var wordTable in wordResult.Tables)
                {
                    var tableData = new TableData
                    {
                        Name = wordTable.Name,
                        Rows = wordTable.Rows
                    };
                    result.Tables.Add(tableData);
                }

                // Add paragraphs as text content
                result.TextContent.AddRange(wordResult.Paragraphs);

                result.Success = true;

                // Add conversion notice if applicable
                if (wordResult.WasConverted)
                {
                    result.Metadata["Converted"] = "Legacy .doc format was automatically converted to .docx";
                    result.Metadata["OriginalFormat"] = wordResult.OriginalFormat;
                }

                // Add warnings
                foreach (var warning in wordResult.Warnings)
                {
                    result.Metadata[$"Warning_{result.Metadata.Count}"] = warning;
                }
            }
            else
            {
                result.Errors.AddRange(wordResult.Errors);
                
                // Provide helpful guidance
                if (wordResult.OriginalFormat == ".doc")
                {
                    result.Errors.Add("SOLUTION: Save your .doc file as .docx in Microsoft Word:");
                    result.Errors.Add("1. Open file in Word");
                    result.Errors.Add("2. File ? Save As");
                    result.Errors.Add("3. Choose 'Word Document (.docx)'");
                    result.Errors.Add("4. Import the .docx file");
                }
            }
            
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Word parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse Excel document (.xlsx, .xls)
    /// </summary>
    private static async Task<ParsedDocument> ParseExcelDocumentAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.Excel,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            if (extension == ".xlsx")
            {
                result = await ParseXlsxAsync(filePath);
            }
            else if (extension == ".xls")
            {
                result.Errors.Add("Legacy .xls format requires external library");
                result.TextContent.Add("Please convert to .xlsx or save as CSV");
            }
            
            result.Success = result.Errors.Count == 0;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Excel parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse modern Excel (.xlsx)
    /// </summary>
    private static async Task<ParsedDocument> ParseXlsxAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.Excel,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            
            // Excel stores data in xl/worksheets/sheet*.xml
            var sheetEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("xl/worksheets/sheet") && e.FullName.EndsWith(".xml"))
                .ToList();

            foreach (var sheetEntry in sheetEntries)
            {
                using var stream = sheetEntry.Open();
                using var reader = new StreamReader(stream);
                var xml = await reader.ReadToEndAsync();
                
                var table = ExtractTableFromExcelXml(xml, sheetEntry.Name);
                if (table.Rows.Any())
                    result.Tables.Add(table);
            }
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"XLSX parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse PowerPoint (.pptx, .ppt)
    /// </summary>
    private static async Task<ParsedDocument> ParsePowerPointAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.PowerPoint,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var extension = Path.GetExtension(filePath).ToLower();
            
            if (extension == ".pptx")
            {
                result = await ParsePptxAsync(filePath);
            }
            else
            {
                result.Errors.Add("Legacy .ppt format requires external library");
                result.TextContent.Add("Please convert to .pptx");
            }
            
            result.Success = result.Errors.Count == 0;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"PowerPoint parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse modern PowerPoint (.pptx)
    /// </summary>
    private static async Task<ParsedDocument> ParsePptxAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.PowerPoint,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            
            // PowerPoint slides are in ppt/slides/slide*.xml
            var slideEntries = archive.Entries
                .Where(e => e.FullName.StartsWith("ppt/slides/slide") && e.FullName.EndsWith(".xml"))
                .ToList();

            int slideNum = 1;
            foreach (var slideEntry in slideEntries)
            {
                using var stream = slideEntry.Open();
                using var reader = new StreamReader(stream);
                var xml = await reader.ReadToEndAsync();
                
                result.TextContent.Add($"--- Slide {slideNum} ---");
                var text = ExtractTextFromPowerPointXml(xml);
                result.TextContent.AddRange(text);
                
                var tables = ExtractTablesFromPowerPointXml(xml);
                result.Tables.AddRange(tables);
                
                slideNum++;
            }
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"PPTX parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse PDF document
    /// NOTE: Full PDF support requires iTextSharp or PdfSharp library
    /// </summary>
    private static async Task<ParsedDocument> ParsePDFAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.PDF,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            // Basic PDF text extraction without external library
            // For production, use iTextSharp or PdfPig
            
            var bytes = await File.ReadAllBytesAsync(filePath);
            var text = ExtractTextFromPdfBytes(bytes);
            
            if (!string.IsNullOrWhiteSpace(text))
            {
                result.TextContent.AddRange(text.Split('\n'));
                result.Success = true;
            }
            else
            {
                result.Errors.Add("PDF parsing requires iTextSharp library for full support");
                result.TextContent.Add("Please install iTextSharp NuGet package or convert to text/CSV");
            }
            
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"PDF parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse plain text file
    /// </summary>
    private static async Task<ParsedDocument> ParseTextFileAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.Text,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var lines = await File.ReadAllLinesAsync(filePath);
            result.TextContent.AddRange(lines);
            
            // Try to detect tables in text (tab or space-separated)
            var table = DetectTableInText(lines);
            if (table.Rows.Any())
                result.Tables.Add(table);
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Text parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse RTF document
    /// </summary>
    private static async Task<ParsedDocument> ParseRTFAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.RichText,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            var content = await File.ReadAllTextAsync(filePath);
            
            // Strip RTF formatting (basic)
            var text = StripRTFFormatting(content);
            result.TextContent.AddRange(text.Split('\n'));
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"RTF parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Parse OpenDocument format (.odt, .ods, .odp)
    /// </summary>
    private static async Task<ParsedDocument> ParseOpenDocumentAsync(string filePath)
    {
        var result = new ParsedDocument
        {
            Format = DocumentFormat.OpenDocument,
            FileName = Path.GetFileName(filePath)
        };

        try
        {
            // OpenDocument is also ZIP-based
            using var archive = System.IO.Compression.ZipFile.OpenRead(filePath);
            
            var contentEntry = archive.GetEntry("content.xml");
            if (contentEntry != null)
            {
                using var stream = contentEntry.Open();
                using var reader = new StreamReader(stream);
                var xml = await reader.ReadToEndAsync();
                
                var text = ExtractTextFromOpenDocumentXml(xml);
                result.TextContent.AddRange(text);
            }
            
            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"OpenDocument parse error: {ex.Message}");
            return result;
        }
    }

    // ========== HELPER METHODS ==========

    private static List<string> ExtractTextFromWordXml(string xml)
    {
        var text = new List<string>();
        
        // Extract text from <w:t> tags
        var matches = Regex.Matches(xml, @"<w:t[^>]*>(.*?)</w:t>");
        foreach (Match match in matches)
        {
            var content = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(content))
                text.Add(content);
        }
        
        return text;
    }

    private static List<TableData> ExtractTablesFromWordXml(string xml)
    {
        var tables = new List<TableData>();
        
        // Find <w:tbl> elements
        var tableMatches = Regex.Matches(xml, @"<w:tbl>(.*?)</w:tbl>", RegexOptions.Singleline);
        
        foreach (Match tableMatch in tableMatches)
        {
            var table = new TableData { Name = $"Table {tables.Count + 1}" };
            var tableXml = tableMatch.Groups[1].Value;
            
            // Extract rows <w:tr>
            var rowMatches = Regex.Matches(tableXml, @"<w:tr[^>]*>(.*?)</w:tr>", RegexOptions.Singleline);
            
            foreach (Match rowMatch in rowMatches)
            {
                var row = new List<string>();
                var rowXml = rowMatch.Groups[1].Value;
                
                // Extract cells <w:tc>
                var cellMatches = Regex.Matches(rowXml, @"<w:tc>(.*?)</w:tc>", RegexOptions.Singleline);
                
                foreach (Match cellMatch in cellMatches)
                {
                    var cellXml = cellMatch.Groups[1].Value;
                    var cellText = Regex.Matches(cellXml, @"<w:t>(.*?)</w:t>");
                    var cellContent = string.Join(" ", cellText.Cast<Match>().Select(m => m.Groups[1].Value));
                    row.Add(cellContent);
                }
                
                if (row.Any())
                    table.Rows.Add(row);
            }
            
            if (table.Rows.Any())
                tables.Add(table);
        }
        
        return tables;
    }

    private static TableData ExtractTableFromExcelXml(string xml, string sheetName)
    {
        var table = new TableData { Name = sheetName };
        
        // Simplified Excel parsing - would need shared strings lookup for production
        var rowMatches = Regex.Matches(xml, @"<row[^>]*>(.*?)</row>", RegexOptions.Singleline);
        
        foreach (Match rowMatch in rowMatches)
        {
            var row = new List<string>();
            var rowXml = rowMatch.Groups[1].Value;
            
            var cellMatches = Regex.Matches(rowXml, @"<c[^>]*><v>(.*?)</v></c>");
            foreach (Match cellMatch in cellMatches)
            {
                row.Add(cellMatch.Groups[1].Value);
            }
            
            if (row.Any())
                table.Rows.Add(row);
        }
        
        return table;
    }

    private static List<string> ExtractTextFromPowerPointXml(string xml)
    {
        var text = new List<string>();
        
        // Extract text from <a:t> tags (PowerPoint text runs)
        var matches = Regex.Matches(xml, @"<a:t>(.*?)</a:t>");
        foreach (Match match in matches)
        {
            var content = match.Groups[1].Value;
            if (!string.IsNullOrWhiteSpace(content))
                text.Add(content);
        }
        
        return text;
    }

    private static List<TableData> ExtractTablesFromPowerPointXml(string xml)
    {
        // Similar to Word table extraction but using PowerPoint schema
        return new List<TableData>();
    }

    private static List<string> ExtractTextFromOpenDocumentXml(string xml)
    {
        var text = new List<string>();
        
        // Extract from <text:p> tags
        var matches = Regex.Matches(xml, @"<text:p[^>]*>(.*?)</text:p>", RegexOptions.Singleline);
        foreach (Match match in matches)
        {
            var content = Regex.Replace(match.Groups[1].Value, @"<[^>]+>", "");
            if (!string.IsNullOrWhiteSpace(content))
                text.Add(content);
        }
        
        return text;
    }

    private static string ExtractTextFromPdfBytes(byte[] bytes)
    {
        // Very basic PDF text extraction
        // For production use iTextSharp or PdfPig
        var text = Encoding.UTF8.GetString(bytes);
        
        // PDF stores text between BT/ET operators
        var matches = Regex.Matches(text, @"BT(.*?)ET", RegexOptions.Singleline);
        var sb = new StringBuilder();
        
        foreach (Match match in matches)
        {
            var content = match.Groups[1].Value;
            // Extract text from Tj operators
            var textMatches = Regex.Matches(content, @"\((.*?)\)\s*Tj");
            foreach (Match textMatch in textMatches)
            {
                sb.AppendLine(textMatch.Groups[1].Value);
            }
        }
        
        return sb.ToString();
    }

    private static string StripRTFFormatting(string rtf)
    {
        // Remove RTF control words
        var text = Regex.Replace(rtf, @"\\[a-z]+\d*\s?", "");
        text = Regex.Replace(text, @"[{}]", "");
        return text.Trim();
    }

    private static TableData DetectTableInText(string[] lines)
    {
        var table = new TableData { Name = "Text Table" };
        
        // Try to detect tab or space-separated values
        foreach (var line in lines)
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            
            // Try tab-separated
            if (line.Contains('\t'))
            {
                table.Rows.Add(line.Split('\t').Select(s => s.Trim()).ToList());
            }
            // Try multiple spaces
            else if (Regex.IsMatch(line, @"\s{2,}"))
            {
                table.Rows.Add(Regex.Split(line, @"\s{2,}").Select(s => s.Trim()).ToList());
            }
        }
        
        return table;
    }
}
