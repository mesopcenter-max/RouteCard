using System.IO.Compression;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using System.Xml;
using System.Xml.Linq;
using ExcelDataReader;
using HALRouteCardApp.Data;
using HALRouteCardApp.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Controllers;

[ApiController]
[Route("api/process-records")]
[Authorize]
public sealed class ProcessRecordsController(RouteCardDbContext db, ILogger<ProcessRecordsController> logger) : ControllerBase
{
    private const long MaximumFileSize = 25 * 1024 * 1024;
    private static readonly IReadOnlyDictionary<string, string> SupportedTypes = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
    {
        [".pdf"] = "application/pdf",
        [".doc"] = "application/msword",
        [".docx"] = "application/vnd.openxmlformats-officedocument.wordprocessingml.document",
        [".xls"] = "application/vnd.ms-excel",
        [".xlsx"] = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet"
    };

    [HttpGet]
    public async Task<IActionResult> List(
        [FromQuery] string? search,
        [FromQuery] string? operationNo,
        [FromQuery] string? departmentId,
        [FromQuery] string? sort = "date",
        [FromQuery] bool descending = true,
        CancellationToken cancellationToken = default)
    {
        var query = db.ProcessRecordAttachments.AsNoTracking().Include(record => record.RouteCard).AsQueryable();
        if (!string.IsNullOrWhiteSpace(operationNo))
            query = query.Where(record => record.OperationNo.Contains(operationNo.Trim()));
        if (!string.IsNullOrWhiteSpace(departmentId))
            query = query.Where(record => record.DepartmentId.Contains(departmentId.Trim()));
        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.Trim();
            query = query.Where(record => record.FileName.Contains(term) || record.OperationNo.Contains(term) ||
                record.DepartmentId.Contains(term) || record.AttachedByName.Contains(term) ||
                (record.RouteCard.RouteCardNo != null && record.RouteCard.RouteCardNo.Contains(term)));
        }

        query = sort?.ToLowerInvariant() switch
        {
            "operation" => descending ? query.OrderByDescending(record => record.OperationNo) : query.OrderBy(record => record.OperationNo),
            "department" => descending ? query.OrderByDescending(record => record.DepartmentId) : query.OrderBy(record => record.DepartmentId),
            "name" => descending ? query.OrderByDescending(record => record.FileName) : query.OrderBy(record => record.FileName),
            "person" => descending ? query.OrderByDescending(record => record.AttachedByName) : query.OrderBy(record => record.AttachedByName),
            _ => descending ? query.OrderByDescending(record => record.AttachedAtUtc) : query.OrderBy(record => record.AttachedAtUtc)
        };

        var records = await query.Select(record => new
        {
            record.Id,
            record.FileName,
            record.OperationNo,
            record.DepartmentId,
            record.AttachedByName,
            record.AttachedAtUtc,
            record.ContentType,
            record.RouteCardId,
            record.RouteCard.RouteCardNo
        }).ToListAsync(cancellationToken);
        return Ok(records);
    }

    [HttpPost("{routeCardId:long}/attachments")]
    [RequestSizeLimit(MaximumFileSize + 1024 * 1024)]
    public async Task<IActionResult> Upload(
        long routeCardId,
        [FromForm] string operationNo,
        [FromForm] string departmentId,
        [FromForm] IFormFile file,
        CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
            return Unauthorized();
        if (string.IsNullOrWhiteSpace(operationNo) || operationNo.Length > 100)
            return BadRequest(new { error = "An operation number of up to 100 characters is required." });
        if (string.IsNullOrWhiteSpace(departmentId) || departmentId.Trim().Length > 100)
            return BadRequest(new { error = "A department ID / WCR of up to 100 characters is required for each attachment." });
        if (file is null || file.Length == 0)
            return BadRequest(new { error = "Select a non-empty PDF, Word, or Excel file." });
        if (file.Length > MaximumFileSize)
            return BadRequest(new { error = "Each attachment must be 25 MB or smaller." });

        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        var extension = Path.GetExtension(fileName);
        if (fileName.Length is 0 or > 255 || !SupportedTypes.TryGetValue(extension, out var contentType))
            return BadRequest(new { error = "Only PDF, Word (.doc/.docx), and Excel (.xls/.xlsx) files are supported." });

        byte[] content;
        await using (var stream = new MemoryStream())
        {
            await file.CopyToAsync(stream, cancellationToken);
            content = stream.ToArray();
        }
        if (!HasExpectedSignature(extension, content))
            return BadRequest(new { error = "The selected file does not match its file type." });

        var routeCardExists = await db.RouteCards.AnyAsync(card => card.Id == routeCardId, cancellationToken);
        if (!routeCardExists)
            return NotFound(new { error = "Save the route card before uploading its process records." });
        var user = await db.AppUsers.AsNoTracking().SingleOrDefaultAsync(account => account.Id == userId, cancellationToken);
        if (user is null)
            return Unauthorized();

        var record = new ProcessRecordAttachment
        {
            RouteCardId = routeCardId,
            OperationNo = operationNo.Trim(),
            DepartmentId = departmentId.Trim(),
            FileName = fileName,
            ContentType = contentType,
            Content = content,
            AttachedByUserId = userId,
            AttachedByName = user.DisplayName,
            AttachedAtUtc = DateTime.UtcNow
        };
        db.ProcessRecordAttachments.Add(record);
        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Failed to store process record {FileName} for route card {RouteCardId}.", fileName, routeCardId);
            return Problem("The process record could not be stored.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        return CreatedAtAction(nameof(GetFile), new { id = record.Id }, new { record.Id, record.FileName, record.OperationNo, record.DepartmentId, record.AttachedByName, record.AttachedAtUtc });
    }

    [HttpGet("{id:long}/preview")]
    public async Task<IActionResult> Preview(long id, CancellationToken cancellationToken)
    {
        var record = await db.ProcessRecordAttachments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is null)
            return NotFound();

        Response.Headers["X-Content-Type-Options"] = "nosniff";
        Response.Headers["Content-Security-Policy"] = "default-src 'none'; style-src 'unsafe-inline'; frame-src 'self';";
        var extension = Path.GetExtension(record.FileName).ToLowerInvariant();
        string preview;
        try
        {
            preview = extension switch
            {
                ".pdf" => $"<iframe class=\"pdf-preview\" title=\"{HtmlEncoder.Default.Encode(record.FileName)}\" src=\"/api/process-records/{record.Id}/file\"></iframe>",
                ".xlsx" => BuildExcelPreview(record.Content),
                ".xls" => BuildBinaryExcelPreview(record.Content),
                ".docx" => BuildWordPreview(record.Content),
                ".doc" => "<p class=\"preview-note\">This older Office format cannot be previewed in the browser. Download the file to open it.</p>",
                _ => "<p class=\"preview-note\">This file type cannot be previewed in the browser. Download the file to open it.</p>"
            };
        }
        catch (Exception exception) when (exception is InvalidDataException or XmlException or IOException or FormatException or ArgumentException or NotSupportedException or ExcelDataReader.Exceptions.HeaderException or ExcelDataReader.Exceptions.InvalidPasswordException)
        {
            logger.LogWarning(exception, "Could not preview process record {ProcessRecordId} ({FileName}).", record.Id, record.FileName);
            preview = "<p class=\"preview-note\">A preview could not be generated for this file. Download it to open it.</p>";
        }

        return Content(BuildPreviewPage(record, preview), "text/html; charset=utf-8");
    }

    [HttpGet("{id:long}/file")]
    public async Task<IActionResult> GetFile(long id, [FromQuery] bool download = false, CancellationToken cancellationToken = default)
    {
        var record = await db.ProcessRecordAttachments.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (record is null)
            return NotFound();
        if (download)
            return File(record.Content, record.ContentType, record.FileName);
        Response.Headers.ContentDisposition = "inline";
        return File(record.Content, record.ContentType);
    }

    private static string BuildPreviewPage(ProcessRecordAttachment record, string preview)
    {
        var encoder = HtmlEncoder.Default;
        var attachedAt = DateTime.SpecifyKind(record.AttachedAtUtc, DateTimeKind.Utc).ToString("dd MMM yyyy HH:mm 'UTC'", System.Globalization.CultureInfo.InvariantCulture);
        return $$"""
        <!doctype html>
        <html lang="en"><head><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>{{encoder.Encode(record.FileName)}} - Process record preview</title>
        <style>body{margin:0;background:#f4f6fa;color:#252c36;font:15px Arial,sans-serif}header{padding:16px 24px;background:#08488f;color:#fff}header h1{margin:0;font-size:20px}main{max-width:1400px;margin:20px auto;padding:0 20px}.metadata{display:flex;flex-wrap:wrap;gap:8px 24px;margin-bottom:16px;padding:14px;background:#fff;border:1px solid #d5dbe4;border-radius:8px}.metadata span{color:#465367}.preview{min-height:70vh;padding:16px;background:#fff;border:1px solid #d5dbe4;border-radius:8px;overflow:auto}.pdf-preview{width:100%;height:78vh;border:0}.sheet{margin:20px 0;overflow:auto}table{border-collapse:collapse;font-size:13px}td,th{min-width:80px;padding:6px 9px;border:1px solid #d5dbe4;text-align:left;white-space:pre-wrap;overflow-wrap:anywhere}th{background:#e8f0fc}.preview-note{padding:18px;background:#fff7e6;border:1px solid #eed9a3;border-radius:6px}.actions a{display:inline-block;margin:0 12px 12px 0;padding:9px 14px;border:1px solid #08488f;border-radius:5px;color:#08488f;font-weight:700;text-decoration:none}</style></head>
        <body><header><h1>Process record preview</h1></header><main><div class="metadata"><strong>{{encoder.Encode(record.FileName)}}</strong><span>Operation: {{encoder.Encode(record.OperationNo)}}</span><span>Dept ID / WCR: {{encoder.Encode(record.DepartmentId)}}</span><span>Attached by: {{encoder.Encode(record.AttachedByName)}}</span><span>Attached: {{attachedAt}}</span></div><div class="actions"><a href="/api/process-records/{{record.Id}}/file?download=true">Download file</a><a href="/Index">Back to Route Card</a></div><div class="preview">{{preview}}</div></main></body></html>
        """;
    }

    private static string BuildExcelPreview(byte[] content)
    {
        const int maximumRows = 200;
        const int maximumColumns = 30;
        XNamespace spreadsheet = "http://schemas.openxmlformats.org/spreadsheetml/2006/main";
        XNamespace relationships = "http://schemas.openxmlformats.org/officeDocument/2006/relationships";
        XNamespace packageRelationships = "http://schemas.openxmlformats.org/package/2006/relationships";
        using var stream = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var workbookEntry = archive.GetEntry("xl/workbook.xml") ?? throw new InvalidDataException("The workbook entry is missing.");
        var workbook = LoadXml(workbookEntry);
        var relationshipEntry = archive.GetEntry("xl/_rels/workbook.xml.rels") ?? throw new InvalidDataException("The workbook relationships are missing.");
        var relationshipDocument = LoadXml(relationshipEntry);
        var targets = relationshipDocument.Root?.Elements(packageRelationships + "Relationship")
            .ToDictionary(item => (string?)item.Attribute("Id") ?? string.Empty, item => (string?)item.Attribute("Target") ?? string.Empty)
            ?? throw new InvalidDataException("The workbook relationships are invalid.");
        var sharedStringsEntry = archive.GetEntry("xl/sharedStrings.xml");
        var sharedStrings = sharedStringsEntry is null
            ? []
            : LoadXml(sharedStringsEntry).Root?.Elements(spreadsheet + "si")
                .Select(item => string.Concat(item.Descendants(spreadsheet + "t").Select(text => text.Value))).ToArray() ?? [];
        var sheets = workbook.Descendants(spreadsheet + "sheet").ToArray();
        if (sheets.Length == 0)
            return "<p class=\"preview-note\">This workbook contains no worksheets.</p>";

        var html = new StringBuilder();
        foreach (var sheet in sheets)
        {
            var relationId = (string?)sheet.Attribute(relationships + "id");
            if (relationId is null || !targets.TryGetValue(relationId, out var target))
                continue;
            var sheetEntry = archive.GetEntry(ResolveWorkbookTarget(target)) ?? throw new InvalidDataException("A worksheet entry is missing.");
            var worksheet = LoadXml(sheetEntry);
            html.Append("<section class=\"sheet\"><h2>").Append(HtmlEncoder.Default.Encode((string?)sheet.Attribute("name") ?? "Worksheet"))
                .Append("</h2><table><tbody>");
            var rows = worksheet.Descendants(spreadsheet + "row").Take(maximumRows).ToArray();
            foreach (var row in rows)
            {
                var cells = new string[maximumColumns];
                var lastColumn = -1;
                foreach (var cell in row.Elements(spreadsheet + "c"))
                {
                    var column = GetColumnIndex((string?)cell.Attribute("r"));
                    if (column < 0 || column >= maximumColumns)
                        continue;
                    var type = (string?)cell.Attribute("t");
                    var value = type == "inlineStr"
                        ? string.Concat(cell.Descendants(spreadsheet + "t").Select(text => text.Value))
                        : cell.Element(spreadsheet + "v")?.Value ?? string.Empty;
                    if (type == "s" && int.TryParse(value, out var sharedIndex) && sharedIndex >= 0 && sharedIndex < sharedStrings.Length)
                        value = sharedStrings[sharedIndex];
                    cells[column] = value;
                    lastColumn = Math.Max(lastColumn, column);
                }
                html.Append("<tr><th>").Append(HtmlEncoder.Default.Encode((string?)row.Attribute("r") ?? string.Empty)).Append("</th>");
                for (var index = 0; index <= lastColumn; index++)
                    html.Append("<td>").Append(HtmlEncoder.Default.Encode(cells[index] ?? string.Empty)).Append("</td>");
                html.Append("</tr>");
            }
            html.Append("</tbody></table>");
            if (rows.Length == maximumRows)
                html.Append("<p class=\"preview-note\">Preview limited to the first 200 rows and 30 columns.</p>");
            html.Append("</section>");
        }
        return html.Length == 0 ? "<p class=\"preview-note\">No worksheets could be displayed.</p>" : html.ToString();
    }

    private static string BuildBinaryExcelPreview(byte[] content)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var stream = new MemoryStream(content, writable: false);
        using var reader = ExcelReaderFactory.CreateReader(stream);
        var html = new StringBuilder();
        do
        {
            html.Append("<section class=\"sheet\"><h2>").Append(HtmlEncoder.Default.Encode(reader.Name)).Append("</h2><table><tbody>");
            var rowNumber = 0;
            while (rowNumber < 200 && reader.Read())
            {
                html.Append("<tr><th>").Append(++rowNumber).Append("</th>");
                var columnCount = Math.Min(reader.FieldCount, 30);
                for (var column = 0; column < columnCount; column++)
                {
                    var value = reader.GetValue(column);
                    var text = value switch
                    {
                        DateTime date => date.ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture),
                        IFormattable formattable => formattable.ToString(null, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                        _ => Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty
                    };
                    html.Append("<td>").Append(HtmlEncoder.Default.Encode(text)).Append("</td>");
                }
                html.Append("</tr>");
            }
            html.Append("</tbody></table><p class=\"preview-note\">Preview limited to the first 200 rows and 30 columns.</p></section>");
        } while (reader.NextResult());
        return html.Length == 0 ? "<p class=\"preview-note\">This workbook contains no worksheets.</p>" : html.ToString();
    }

    private static string BuildWordPreview(byte[] content)
    {
        XNamespace word = "http://schemas.openxmlformats.org/wordprocessingml/2006/main";
        using var stream = new MemoryStream(content, writable: false);
        using var archive = new ZipArchive(stream, ZipArchiveMode.Read);
        var documentEntry = archive.GetEntry("word/document.xml") ?? throw new InvalidDataException("The Word document entry is missing.");
        var document = LoadXml(documentEntry);
        var paragraphs = document.Descendants(word + "p")
            .Select(paragraph => string.Concat(paragraph.Descendants(word + "t").Select(text => text.Value)))
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .Take(1000)
            .ToArray();
        if (paragraphs.Length == 0)
            return "<p class=\"preview-note\">This document contains no previewable text.</p>";
        var html = new StringBuilder();
        foreach (var paragraph in paragraphs)
            html.Append("<p>").Append(HtmlEncoder.Default.Encode(paragraph)).Append("</p>");
        if (paragraphs.Length == 1000)
            html.Append("<p class=\"preview-note\">Preview limited to the first 1,000 paragraphs.</p>");
        return html.ToString();
    }

    private static XDocument LoadXml(ZipArchiveEntry entry)
    {
        using var stream = entry.Open();
        using var reader = XmlReader.Create(stream, new XmlReaderSettings
        {
            DtdProcessing = DtdProcessing.Prohibit,
            XmlResolver = null,
            MaxCharactersInDocument = 8 * 1024 * 1024
        });
        return XDocument.Load(reader);
    }

    private static string ResolveWorkbookTarget(string target)
    {
        var normalized = target.Replace('\\', '/').TrimStart('/');
        var segments = new List<string>();
        var path = normalized.StartsWith("xl/", StringComparison.OrdinalIgnoreCase) ? normalized : $"xl/{normalized}";
        foreach (var segment in path.Split('/'))
        {
            if (segment == "..")
            {
                if (segments.Count == 0)
                    throw new InvalidDataException("The worksheet path is invalid.");
                segments.RemoveAt(segments.Count - 1);
            }
            else if (segment.Length > 0 && segment != ".")
            {
                segments.Add(segment);
            }
        }
        var resolved = string.Join('/', segments);
        if (!resolved.StartsWith("xl/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("The worksheet path is invalid.");
        return resolved;
    }

    private static int GetColumnIndex(string? cellReference)
    {
        if (string.IsNullOrEmpty(cellReference))
            return -1;
        var column = 0;
        foreach (var character in cellReference)
        {
            if (!char.IsLetter(character))
                break;
            column = column * 26 + char.ToUpperInvariant(character) - 'A' + 1;
        }
        return column - 1;
    }

    private static bool HasExpectedSignature(string extension, byte[] content)
    {
        ReadOnlySpan<byte> oleHeader = [0xD0, 0xCF, 0x11, 0xE0, 0xA1, 0xB1, 0x1A, 0xE1];
        var pdfHeaderLength = Math.Min(content.Length, 1024);
        return extension.ToLowerInvariant() switch
        {
            ".pdf" => content.AsSpan(0, pdfHeaderLength).IndexOf("%PDF-"u8) >= 0,
            ".doc" or ".xls" => content.AsSpan().StartsWith(oleHeader),
            ".docx" or ".xlsx" => content.AsSpan().StartsWith("PK\u0003\u0004"u8),
            _ => false
        };
    }
}
