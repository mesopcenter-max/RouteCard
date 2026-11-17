using System.Security.Claims;
using HALRouteCardApp.Data;
using HALRouteCardApp.Models;
using HALRouteCardApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using System.IO;

namespace HALRouteCardApp.Controllers;

[ApiController]
[Route("api/route-cards")]
[Microsoft.AspNetCore.Authorization.Authorize]
public sealed class RouteCardsController(IRouteCardService routeCards, IOpcenterLookupService opcenter, ILogger<RouteCardsController> logger, RouteCardDbContext db, IWebHostEnvironment environment) : ControllerBase
{
    private const int MaxUploadSize = 10 * 1024 * 1024;
    private static readonly HashSet<string> AllowedExtensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".txt", ".png", ".jpg", ".jpeg"
    };

    [HttpPost]
    public async Task<ActionResult<RouteCard>> Save([FromBody] RouteCardSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId))
                return Unauthorized();
            int? serialNumber = null;
            var serialClaim = User.FindFirstValue("serial_number");
            if (serialClaim is not null)
            {
                if (!int.TryParse(serialClaim, out var parsedSerial)) return Unauthorized();
                serialNumber = parsedSerial;
            }
            var card = await routeCards.SaveAsync(request, userId, serialNumber, cancellationToken);
            return CreatedAtAction(nameof(Get), new { id = card.Id }, card);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Failed to save route card {PartNo} to the database.", request.Card.PartNo);
            return Problem("The route card could not be saved to the database.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<RouteCard>> Get(long id, CancellationToken cancellationToken)
    {
        var card = await routeCards.GetAsync(id, cancellationToken);
        return card is null ? NotFound() : Ok(card);
    }

    [HttpGet("lookup")]
    public async Task<ActionResult<RouteCardLookupResult>> Lookup([FromQuery] RouteCardLookupRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var result = await opcenter.FindAsync(request, cancellationToken);
            return result is null ? NotFound(new { error = "No matching data was found in HALFNF, HALNEW, or OpcenterAPSHAL." }) : Ok(result);
        }
        catch (ArgumentException exception)
        {
            return BadRequest(new { error = exception.Message });
        }
        catch (InvalidOperationException exception)
        {
            return Problem(exception.Message, statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (SqlException exception)
        {
            return Problem($"HALFNF/HALNEW/OpcenterAPSHAL lookup failed: {exception.Message}", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
    }

    [HttpPost("{routeCardId:long}/operations/{operationNo}/attachment")]
    [RequestSizeLimit(11 * 1024 * 1024)]
    public async Task<ActionResult> UploadAttachment([FromRoute] long routeCardId, [FromRoute] string operationNo, [FromForm] IFormFile? file, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(operationNo) || operationNo.Length > 100)
            return BadRequest(new { error = "Invalid operation number." });
        if (file is null || file.Length == 0 || file.Length > MaxUploadSize)
            return BadRequest(new { error = "File is required and must not exceed 10 MiB." });
        var fileName = Path.GetFileName(file.FileName.Replace('\\', '/'));
        if (string.IsNullOrEmpty(fileName) || fileName.Length > 255 || fileName.Any(char.IsControl))
            return BadRequest(new { error = "Invalid file name." });
        var extension = Path.GetExtension(fileName).ToLowerInvariant();
        if (!AllowedExtensions.Contains(extension))
            return BadRequest(new { error = "Invalid file type." });

        var card = await routeCards.GetAsync(routeCardId, cancellationToken);
        if (card is null)
            return NotFound();

        operationNo = operationNo.Trim();
        var attachment = await db.OperationAttachments.SingleOrDefaultAsync(value => value.RouteCardId == routeCardId && value.OperationNo == operationNo, cancellationToken);
        var previousStoredFileName = attachment?.StoredFileName;
        var storageDirectory = Path.Combine(environment.ContentRootPath, "App_Data", "RouteCardAttachments");
        var storedFileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(storageDirectory, storedFileName);

        try
        {
            Directory.CreateDirectory(storageDirectory);
            await using (var stream = new FileStream(filePath, FileMode.CreateNew, FileAccess.Write, FileShare.None, 81920, FileOptions.Asynchronous))
                await file.CopyToAsync(stream, cancellationToken);

            if (attachment is null)
            {
                attachment = new RouteCardOperationAttachment { RouteCardId = routeCardId, OperationNo = operationNo };
                await db.OperationAttachments.AddAsync(attachment, cancellationToken);
            }
            attachment.OriginalFileName = fileName;
            attachment.StoredFileName = storedFileName;
            attachment.CreatedUtc = DateTime.UtcNow;
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException exception)
        {
            logger.LogError(exception, "Failed to save attachment metadata for route card {RouteCardId}, operation {OperationNo}.", routeCardId, operationNo);
            DeleteStoredFile(filePath);
            return Problem("Failed to save attachment metadata to the database.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (IOException exception)
        {
            logger.LogError(exception, "Failed to write attachment for route card {RouteCardId}, operation {OperationNo}.", routeCardId, operationNo);
            DeleteStoredFile(filePath);
            return Problem("Failed to save the attachment file.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }
        catch (UnauthorizedAccessException exception)
        {
            logger.LogError(exception, "Access denied while writing attachment for route card {RouteCardId}, operation {OperationNo}.", routeCardId, operationNo);
            DeleteStoredFile(filePath);
            return Problem("The attachment could not be written to storage.", statusCode: StatusCodes.Status503ServiceUnavailable);
        }

        if (!string.IsNullOrEmpty(previousStoredFileName))
            DeleteStoredFile(Path.Combine(storageDirectory, Path.GetFileName(previousStoredFileName)));

        var previewUrl = $"/api/route-cards/{routeCardId}/operation-attachments/{attachment.Id}/preview";
        var response = new
        {
            id = attachment.Id,
            fileName = attachment.OriginalFileName,
            url = $"/api/route-cards/{routeCardId}/operation-attachments/{attachment.Id}",
            previewUrl,
            previewSupported = extension is ".pdf" or ".png" or ".jpg" or ".jpeg" or ".txt"
        };
        return Ok(response);
    }

    private void DeleteStoredFile(string path)
    {
        if (System.IO.File.Exists(path))
        {
            try
            {
                System.IO.File.Delete(path);
            }
            catch (IOException ioEx)
            {
                logger.LogWarning(ioEx, "IO error while deleting stored file.");
            }
            catch (UnauthorizedAccessException authEx)
            {
                logger.LogWarning(authEx, "Unauthorized access while deleting stored file.");
            }
        }
    }

    [HttpGet("{routeCardId:long}/operation-attachments/{attachmentId:long}")]
    public async Task<ActionResult> DownloadAttachment([FromRoute] long routeCardId, [FromRoute] long attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await db.OperationAttachments.SingleOrDefaultAsync(value => value.RouteCardId == routeCardId && value.Id == attachmentId, cancellationToken);
        if (attachment is null)
            return NotFound();
        var filePath = Path.Combine(environment.ContentRootPath, "App_Data", "RouteCardAttachments", Path.GetFileName(attachment.StoredFileName));
        if (!System.IO.File.Exists(filePath))
            return NotFound();
        return PhysicalFile(filePath, "application/octet-stream", attachment.OriginalFileName, enableRangeProcessing: true);
    }

    [HttpGet("{routeCardId:long}/operation-attachments/{attachmentId:long}/preview")]
    public async Task<ActionResult> PreviewAttachment([FromRoute] long routeCardId, [FromRoute] long attachmentId, CancellationToken cancellationToken)
    {
        var attachment = await db.OperationAttachments.SingleOrDefaultAsync(value => value.RouteCardId == routeCardId && value.Id == attachmentId, cancellationToken);
        if (attachment is null)
            return NotFound();
        var filePath = Path.Combine(environment.ContentRootPath, "App_Data", "RouteCardAttachments", Path.GetFileName(attachment.StoredFileName));
        if (!System.IO.File.Exists(filePath))
            return NotFound();
        var extension = Path.GetExtension(attachment.StoredFileName).ToLowerInvariant();
        string contentType;
        switch (extension)
        {
            case ".pdf":
                contentType = "application/pdf";
                break;
            case ".png":
                contentType = "image/png";
                break;
            case ".jpg":
            case ".jpeg":
                contentType = "image/jpeg";
                break;
            case ".txt":
                contentType = "text/plain; charset=utf-8";
                break;
            default:
                return BadRequest(new { error = "Preview is not supported for this file type." });
        }
        Response.Headers["X-Content-Type-Options"] = "nosniff";
        return PhysicalFile(filePath, contentType, enableRangeProcessing: true);
    }
}
