using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;

namespace Wdpl2.Services;

/// <summary>
/// Universal Word document parser supporting both .doc and .docx formats
/// Automatically converts .doc to .docx for parsing
/// </summary>
public static class WordDocumentParser
{
    public class WordParseResult
    {
        public bool Success { get; set; }
        public List<WordTable> Tables { get; set; } = new();
        public List<string> Paragraphs { get; set; } = new();
        public List<string> Errors { get; set; } = new();
        public List<string> Warnings { get; set; } = new();
        public bool WasConverted { get; set; }
        public string OriginalFormat { get; set; } = "";
    }

    public class WordTable
    {
        public string Name { get; set; } = "";
        public List<List<string>> Rows { get; set; } = new();
        public int RowCount => Rows.Count;
        public int ColumnCount => Rows.FirstOrDefault()?.Count ?? 0;
        
        public bool HasHeaders => Rows.Count > 0 && 
                                  Rows[0].Any(cell => !string.IsNullOrWhiteSpace(cell));
    }

    /// <summary>
    /// Parse any Word document (.doc or .docx) with automatic conversion
    /// </summary>
    public static async Task<WordParseResult> ParseWordDocumentAsync(string filePath)
    {
        var result = new WordParseResult();
        var extension = Path.GetExtension(filePath).ToLower();
        result.OriginalFormat = extension;

        try
        {
            if (extension == ".docx")
            {
                // Modern format - parse directly
                return await ParseDocxAsync(filePath);
            }
            else if (extension == ".doc")
            {
                // Legacy format - convert then parse
                result.Warnings.Add("Legacy .doc format detected - converting to .docx");
                
                var convertedPath = await ConvertDocToDocxAsync(filePath);
                if (convertedPath != null)
                {
                    result = await ParseDocxAsync(convertedPath);
                    result.WasConverted = true;
                    result.OriginalFormat = ".doc";
                    
                    // Clean up temporary file
                    try
                    {
                        File.Delete(convertedPath);
                    }
                    catch
                    {
                        // Ignore cleanup errors
                    }
                    
                    return result;
                }
                else
                {
                    // Conversion failed - try basic text extraction
                    result.Warnings.Add("Automatic conversion failed - using basic text extraction");
                    return await ExtractTextFromLegacyDocAsync(filePath);
                }
            }
            else
            {
                result.Errors.Add($"Unsupported format: {extension}");
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
    /// Parse modern .docx file using OpenXml
    /// </summary>
    private static async Task<WordParseResult> ParseDocxAsync(string filePath)
    {
        var result = new WordParseResult
        {
            OriginalFormat = ".docx"
        };

        try
        {
            using var document = WordprocessingDocument.Open(filePath, false);
            var body = document.MainDocumentPart?.Document?.Body;

            if (body == null)
            {
                result.Errors.Add("Document body not found");
                return result;
            }

            // Extract paragraphs
            var paragraphs = body.Elements<Paragraph>();
            foreach (var para in paragraphs)
            {
                var text = GetParagraphText(para);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    result.Paragraphs.Add(text);
                }
            }

            // Extract tables
            var tables = body.Elements<Table>();
            int tableIndex = 1;
            foreach (var table in tables)
            {
                var wordTable = ExtractTable(table);
                wordTable.Name = $"Table {tableIndex}";
                
                if (wordTable.Rows.Any())
                {
                    result.Tables.Add(wordTable);
                    tableIndex++;
                }
            }

            result.Success = true;
            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"DOCX parse error: {ex.Message}");
            return result;
        }
    }

    /// <summary>
    /// Convert legacy .doc to .docx format
    /// Uses multi-strategy approach for cross-platform support
    /// </summary>
    private static async Task<string?> ConvertDocToDocxAsync(string docPath)
    {
        try
        {
            // Strategy 1: Try using built-in ZIP extraction (works for some .doc files)
            var tempDocxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
            
            // Some .doc files are actually just renamed .docx files
            try
            {
                File.Copy(docPath, tempDocxPath, true);
                
                // Try to open as .docx
                using (var test = WordprocessingDocument.Open(tempDocxPath, false))
                {
                    // If we get here, it's actually a .docx file!
                    return tempDocxPath;
                }
            }
            catch
            {
                // Not a .docx file, continue with other strategies
            }

            // Strategy 2: Use LibreOffice/OpenOffice if available (cross-platform)
            var convertedPath = await TryLibreOfficeConversionAsync(docPath);
            if (convertedPath != null)
                return convertedPath;

            // Strategy 3: Extract text and create new .docx with content
            return await CreateDocxFromTextAsync(docPath);
        }
        catch (Exception ex)
        {
            System.Diagnostics.Debug.WriteLine($"Conversion error: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Try to convert using LibreOffice/OpenOffice command line
    /// </summary>
    private static async Task<string?> TryLibreOfficeConversionAsync(string docPath)
    {
        try
        {
            var possiblePaths = new[]
            {
                @"C:\Program Files\LibreOffice\program\soffice.exe",
                @"C:\Program Files (x86)\LibreOffice\program\soffice.exe",
                @"C:\Program Files\OpenOffice 4\program\soffice.exe",
                "/usr/bin/libreoffice",
                "/usr/bin/soffice",
                "/Applications/LibreOffice.app/Contents/MacOS/soffice"
            };

            string? officePath = null;
            foreach (var path in possiblePaths)
            {
                if (File.Exists(path))
                {
                    officePath = path;
                    break;
                }
            }

            if (officePath == null)
                return null;

            var outputDir = Path.GetTempPath();
            var outputFile = Path.Combine(outputDir, 
                Path.GetFileNameWithoutExtension(docPath) + ".docx");

            // LibreOffice command line conversion
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = officePath,
                Arguments = $"--headless --convert-to docx \"{docPath}\" --outdir \"{outputDir}\"",
                UseShellExecute = false,
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                RedirectStandardError = true
            };

            using var process = System.Diagnostics.Process.Start(startInfo);
            if (process == null)
                return null;

            await process.WaitForExitAsync();

            if (process.ExitCode == 0 && File.Exists(outputFile))
            {
                return outputFile;
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Create a new .docx with extracted text from .doc
    /// </summary>
    private static async Task<string?> CreateDocxFromTextAsync(string docPath)
    {
        try
        {
            // Extract raw text from .doc file
            var text = await ExtractRawTextFromDocAsync(docPath);
            
            if (string.IsNullOrWhiteSpace(text))
                return null;

            // Create new .docx with this content
            var tempDocxPath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid()}.docx");
            
            using (var document = WordprocessingDocument.Create(tempDocxPath, 
                DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
            {
                var mainPart = document.AddMainDocumentPart();
                mainPart.Document = new Document();
                var body = mainPart.Document.AppendChild(new Body());

                // Add text as paragraphs
                var lines = text.Split('\n');
                foreach (var line in lines)
                {
                    if (!string.IsNullOrWhiteSpace(line))
                    {
                        var paragraph = body.AppendChild(new Paragraph());
                        var run = paragraph.AppendChild(new Run());
                        run.AppendChild(new Text(line.Trim()));
                    }
                }
            }

            return tempDocxPath;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Extract raw text from .doc file (basic method)
    /// </summary>
    private static async Task<string> ExtractRawTextFromDocAsync(string docPath)
    {
        try
        {
            // Read as binary and look for text patterns
            var bytes = await File.ReadAllBytesAsync(docPath);
            var sb = new StringBuilder();

            // .doc files store text as ASCII/Unicode
            // This is a simplified extraction - may not get everything
            for (int i = 0; i < bytes.Length - 1; i++)
            {
                // Look for printable ASCII characters
                if (bytes[i] >= 32 && bytes[i] <= 126)
                {
                    sb.Append((char)bytes[i]);
                }
                else if (bytes[i] == 13 || bytes[i] == 10)
                {
                    sb.Append('\n');
                }
            }

            // Clean up extracted text
            var text = sb.ToString();
            text = System.Text.RegularExpressions.Regex.Replace(text, @"\n{3,}", "\n\n");
            
            return text;
        }
        catch
        {
            return "";
        }
    }

    /// <summary>
    /// Fallback: Extract basic text from legacy .doc
    /// </summary>
    private static async Task<WordParseResult> ExtractTextFromLegacyDocAsync(string filePath)
    {
        var result = new WordParseResult
        {
            OriginalFormat = ".doc",
            WasConverted = false
        };

        try
        {
            var text = await ExtractRawTextFromDocAsync(filePath);
            
            if (!string.IsNullOrWhiteSpace(text))
            {
                // Split into lines
                var lines = text.Split('\n')
                    .Select(l => l.Trim())
                    .Where(l => !string.IsNullOrWhiteSpace(l))
                    .ToList();

                result.Paragraphs.AddRange(lines);

                // Try to detect tables from text
                var table = DetectTableInText(lines);
                if (table.Rows.Any())
                {
                    result.Tables.Add(table);
                }

                result.Success = true;
                result.Warnings.Add("Extracted text from legacy .doc - tables may need manual verification");
            }
            else
            {
                result.Errors.Add("Could not extract text from .doc file");
                result.Errors.Add("Please save as .docx in Microsoft Word or convert using online converter");
            }

            return result;
        }
        catch (Exception ex)
        {
            result.Errors.Add($"Legacy .doc extraction error: {ex.Message}");
            return result;
        }
    }

    // ========== HELPER METHODS ==========

    private static string GetParagraphText(Paragraph paragraph)
    {
        var sb = new StringBuilder();
        
        foreach (var run in paragraph.Elements<Run>())
        {
            foreach (var text in run.Elements<Text>())
            {
                sb.Append(text.Text);
            }
        }
        
        return sb.ToString().Trim();
    }

    private static WordTable ExtractTable(Table table)
    {
        var wordTable = new WordTable();

        foreach (var row in table.Elements<TableRow>())
        {
            var rowData = new List<string>();
            
            foreach (var cell in row.Elements<TableCell>())
            {
                var cellText = new StringBuilder();
                
                foreach (var paragraph in cell.Elements<Paragraph>())
                {
                    var text = GetParagraphText(paragraph);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        if (cellText.Length > 0)
                            cellText.Append(" ");
                        cellText.Append(text);
                    }
                }
                
                rowData.Add(cellText.ToString().Trim());
            }
            
            if (rowData.Any(cell => !string.IsNullOrWhiteSpace(cell)))
            {
                wordTable.Rows.Add(rowData);
            }
        }

        return wordTable;
    }

    private static WordTable DetectTableInText(List<string> lines)
    {
        var table = new WordTable { Name = "Detected Table" };
        
        // Look for lines with consistent separators (tabs, multiple spaces, |)
        foreach (var line in lines)
        {
            List<string> cells;
            
            if (line.Contains('\t'))
            {
                cells = line.Split('\t').Select(c => c.Trim()).ToList();
            }
            else if (line.Contains('|'))
            {
                cells = line.Split('|').Select(c => c.Trim()).Where(c => !string.IsNullOrEmpty(c)).ToList();
            }
            else if (System.Text.RegularExpressions.Regex.IsMatch(line, @"\s{2,}"))
            {
                cells = System.Text.RegularExpressions.Regex
                    .Split(line, @"\s{2,}")
                    .Select(c => c.Trim())
                    .Where(c => !string.IsNullOrEmpty(c))
                    .ToList();
            }
            else
            {
                continue;
            }

            if (cells.Count >= 2)
            {
                table.Rows.Add(cells);
            }
        }

        return table;
    }

    /// <summary>
    /// Detect if content looks like a league table
    /// </summary>
    public static bool IsLikelyLeagueTable(WordTable table)
    {
        if (table.Rows.Count < 2)
            return false;

        var headerRow = string.Join(" ", table.Rows.First()).ToLower();
        return (headerRow.Contains("team") || headerRow.Contains("position")) &&
               (headerRow.Contains("points") || headerRow.Contains("pts")) &&
               (headerRow.Contains("played") || headerRow.Contains("p"));
    }

    /// <summary>
    /// Detect if content looks like match results
    /// </summary>
    public static bool IsLikelyResultsTable(WordTable table)
    {
        if (table.Rows.Count < 2)
            return false;

        var headerRow = string.Join(" ", table.Rows.First()).ToLower();
        return (headerRow.Contains("home") && headerRow.Contains("away")) ||
               (headerRow.Contains("score") && headerRow.Contains("result")) ||
               headerRow.Contains("fixture");
    }

    /// <summary>
    /// Detect if content looks like player list
    /// </summary>
    public static bool IsLikelyPlayerList(WordTable table)
    {
        if (table.Rows.Count < 2)
            return false;

        var headerRow = string.Join(" ", table.Rows.First()).ToLower();
        return headerRow.Contains("player") && 
               (headerRow.Contains("team") || headerRow.Contains("name"));
    }
}
