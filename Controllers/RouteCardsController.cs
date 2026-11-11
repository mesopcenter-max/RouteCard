using HALRouteCardApp.Models;
using HALRouteCardApp.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Controllers;

[ApiController]
[Route("api/route-cards")]
public sealed class RouteCardsController(IRouteCardService routeCards, IOpcenterLookupService opcenter, ILogger<RouteCardsController> logger) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<RouteCard>> Save([FromBody] RouteCardSaveRequest request, CancellationToken cancellationToken)
    {
        try
        {
            var card = await routeCards.SaveAsync(request, cancellationToken);
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
}
