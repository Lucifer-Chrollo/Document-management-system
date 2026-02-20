using DocumentFormat.OpenXml.Packaging;
using DocumentFormat.OpenXml.Wordprocessing;
using PdfSharp.Drawing;
using PdfSharp.Pdf;
using System.Text;

namespace DocumentManagementSystem.Services;

/// <summary>
/// Converts Word documents (.docx) to PDF for inline preview.
/// Uses Open XML SDK to parse Word content and PDFsharp to generate the PDF.
/// </summary>
public class DocumentConversionService : IDocumentConversionService
{
    private readonly ILogger<DocumentConversionService> _logger;
    
    // Page layout constants
    private const double PageWidth = 595;   // A4 width in points
    private const double PageHeight = 842;  // A4 height in points
    private const double MarginLeft = 50;
    private const double MarginTop = 50;
    private const double MarginRight = 50;
    private const double MarginBottom = 50;
    private const double UsableWidth = PageWidth - MarginLeft - MarginRight;

    public DocumentConversionService(ILogger<DocumentConversionService> logger)
    {
        _logger = logger;
    }

    public bool CanConvertToPreview(string extension)
    {
        var ext = extension?.ToLowerInvariant()?.TrimStart('.') ?? "";
        return ext == "docx" || ext == "doc";
    }

    public async Task<Stream?> ConvertWordToPdfAsync(Stream inputStream, string extension)
    {
        var ext = extension?.ToLowerInvariant()?.TrimStart('.') ?? "";
        
        if (ext == "doc")
        {
            // Legacy .doc format cannot be parsed by Open XML SDK
            return GenerateFallbackPdf("This .doc file format is not supported for preview.\nPlease download the file to view it.");
        }

        if (ext != "docx")
        {
            return null;
        }

        try
        {
            // Buffer the input stream so Open XML can seek
            var ms = new MemoryStream();
            await inputStream.CopyToAsync(ms);
            ms.Position = 0;

            return ConvertDocxToPdf(ms);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error converting Word document to PDF");
            return GenerateFallbackPdf($"Could not convert this document for preview.\nError: {ex.Message}\n\nPlease download the file to view it.");
        }
    }

    private Stream ConvertDocxToPdf(MemoryStream docxStream)
    {
        using var wordDoc = WordprocessingDocument.Open(docxStream, false);
        var body = wordDoc.MainDocumentPart?.Document?.Body;

        if (body == null)
        {
            return GenerateFallbackPdf("This document appears to be empty.");
        }

        var pdfDoc = new PdfDocument();
        pdfDoc.Info.Title = "Document Preview";

        var page = pdfDoc.AddPage();
        page.Width = XUnit.FromPoint(PageWidth);
        page.Height = XUnit.FromPoint(PageHeight);
        var gfx = XGraphics.FromPdfPage(page);

        // Font setup
        var normalFont = new XFont("Arial", 11);
        var boldFont = new XFont("Arial", 11, XFontStyleEx.Bold);
        var italicFont = new XFont("Arial", 11, XFontStyleEx.Italic);
        var boldItalicFont = new XFont("Arial", 11, XFontStyleEx.BoldItalic);
        var h1Font = new XFont("Arial", 20, XFontStyleEx.Bold);
        var h2Font = new XFont("Arial", 16, XFontStyleEx.Bold);
        var h3Font = new XFont("Arial", 13, XFontStyleEx.Bold);

        double yPos = MarginTop;

        foreach (var element in body.Elements())
        {
            if (element is Paragraph paragraph)
            {
                // Determine heading style
                var styleId = paragraph.ParagraphProperties?.ParagraphStyleId?.Val?.Value ?? "";
                XFont paragraphFont = normalFont;
                double lineSpacing = 1.4;
                double afterSpacing = 4;

                if (styleId.Contains("Heading1") || styleId.Contains("heading 1"))
                {
                    paragraphFont = h1Font;
                    afterSpacing = 12;
                }
                else if (styleId.Contains("Heading2") || styleId.Contains("heading 2"))
                {
                    paragraphFont = h2Font;
                    afterSpacing = 10;
                }
                else if (styleId.Contains("Heading3") || styleId.Contains("heading 3"))
                {
                    paragraphFont = h3Font;
                    afterSpacing = 8;
                }

                // Extract runs (text segments with formatting)
                var runs = paragraph.Elements<Run>().ToList();
                
                if (!runs.Any())
                {
                    // Empty paragraph = line break
                    yPos += paragraphFont.Height * lineSpacing;
                    if (yPos > PageHeight - MarginBottom)
                    {
                        page = pdfDoc.AddPage();
                        page.Width = XUnit.FromPoint(PageWidth);
                        page.Height = XUnit.FromPoint(PageHeight);
                        gfx = XGraphics.FromPdfPage(page);
                        yPos = MarginTop;
                    }
                    continue;
                }

                // Build the full paragraph text for word-wrapping
                var fullText = new StringBuilder();
                foreach (var run in runs)
                {
                    var text = run.InnerText;
                    fullText.Append(text);
                }

                var paragraphText = fullText.ToString();
                if (string.IsNullOrWhiteSpace(paragraphText))
                {
                    yPos += paragraphFont.Height * lineSpacing;
                    continue;
                }

                // Determine run-level formatting (use first run's formatting for simplicity)
                var firstRunProps = runs.FirstOrDefault()?.RunProperties;
                XFont runFont = paragraphFont;
                
                if (firstRunProps != null && styleId == "")
                {
                    bool isBold = firstRunProps.Bold != null;
                    bool isItalic = firstRunProps.Italic != null;
                    
                    if (isBold && isItalic) runFont = boldItalicFont;
                    else if (isBold) runFont = boldFont;
                    else if (isItalic) runFont = italicFont;
                }
                else if (styleId != "")
                {
                    runFont = paragraphFont; // Headings use their own font
                }

                // Word-wrap the text
                var wrappedLines = WrapText(gfx, paragraphText, runFont, UsableWidth);

                foreach (var line in wrappedLines)
                {
                    if (yPos + runFont.Height > PageHeight - MarginBottom)
                    {
                        page = pdfDoc.AddPage();
                        page.Width = XUnit.FromPoint(PageWidth);
                        page.Height = XUnit.FromPoint(PageHeight);
                        gfx = XGraphics.FromPdfPage(page);
                        yPos = MarginTop;
                    }

                    gfx.DrawString(line, runFont, XBrushes.Black,
                        new XRect(MarginLeft, yPos, UsableWidth, runFont.Height * lineSpacing),
                        XStringFormats.TopLeft);
                    yPos += runFont.Height * lineSpacing;
                }

                yPos += afterSpacing;
            }
            else if (element is Table table)
            {
                // Render tables as simple text rows
                foreach (var row in table.Elements<TableRow>())
                {
                    var cellTexts = new List<string>();
                    foreach (var cell in row.Elements<TableCell>())
                    {
                        cellTexts.Add(cell.InnerText.Trim());
                    }
                    
                    var rowText = string.Join("  |  ", cellTexts);
                    
                    if (yPos + normalFont.Height > PageHeight - MarginBottom)
                    {
                        page = pdfDoc.AddPage();
                        page.Width = XUnit.FromPoint(PageWidth);
                        page.Height = XUnit.FromPoint(PageHeight);
                        gfx = XGraphics.FromPdfPage(page);
                        yPos = MarginTop;
                    }

                    gfx.DrawString(rowText, normalFont, XBrushes.Black,
                        new XRect(MarginLeft, yPos, UsableWidth, normalFont.Height * 1.4),
                        XStringFormats.TopLeft);
                    yPos += normalFont.Height * 1.6;
                }
                yPos += 8;
            }
        }

        // Save to output stream
        var outputStream = new MemoryStream();
        pdfDoc.Save(outputStream);
        outputStream.Position = 0;
        return outputStream;
    }

    private List<string> WrapText(XGraphics gfx, string text, XFont font, double maxWidth)
    {
        var lines = new List<string>();
        var words = text.Split(' ', StringSplitOptions.None);
        var currentLine = new StringBuilder();

        foreach (var word in words)
        {
            var testLine = currentLine.Length == 0 ? word : currentLine + " " + word;
            var size = gfx.MeasureString(testLine, font);

            if (size.Width > maxWidth && currentLine.Length > 0)
            {
                lines.Add(currentLine.ToString());
                currentLine = new StringBuilder(word);
            }
            else
            {
                currentLine = new StringBuilder(testLine);
            }
        }

        if (currentLine.Length > 0)
        {
            lines.Add(currentLine.ToString());
        }

        return lines;
    }

    private Stream GenerateFallbackPdf(string message)
    {
        var pdfDoc = new PdfDocument();
        pdfDoc.Info.Title = "Preview Not Available";

        var page = pdfDoc.AddPage();
        page.Width = XUnit.FromPoint(PageWidth);
        page.Height = XUnit.FromPoint(PageHeight);
        var gfx = XGraphics.FromPdfPage(page);

        var titleFont = new XFont("Arial", 18, XFontStyleEx.Bold);
        var bodyFont = new XFont("Arial", 12);

        // Draw centered title
        gfx.DrawString("Preview Not Available", titleFont, XBrushes.DarkRed,
            new XRect(0, 200, PageWidth, 30), XStringFormats.Center);

        // Draw message lines
        var lines = message.Split('\n');
        double y = 260;
        foreach (var line in lines)
        {
            gfx.DrawString(line.Trim(), bodyFont, XBrushes.Black,
                new XRect(MarginLeft, y, UsableWidth, 20), XStringFormats.TopLeft);
            y += 20;
        }

        var outputStream = new MemoryStream();
        pdfDoc.Save(outputStream);
        outputStream.Position = 0;
        return outputStream;
    }
}
