using System.Security.Claims;
using HALRouteCardApp.Data;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Controllers;

[Route("account")]
public sealed class AccountController(RouteCardDbContext db) : Controller
{
    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile(CancellationToken cancellationToken)
    {
        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var user = await db.AppUsers.AsNoTracking().Include(account => account.Stamps).SingleOrDefaultAsync(account => account.Id == userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(new { user.DisplayName, user.PhotoUrl, stamps = user.Stamps.Select(stamp => new { stamp.Id, stamp.StampValue, stamp.SignatureValue }) });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
