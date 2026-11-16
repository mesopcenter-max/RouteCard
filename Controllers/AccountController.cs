using System.Globalization;
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
        if (int.TryParse(User.FindFirstValue("serial_number"), NumberStyles.None, CultureInfo.InvariantCulture, out var serialNumber))
        {
            var users = await db.AppUsers.AsNoTracking().Include(account => account.Stamps)
                .Where(account => account.SerialNumber == serialNumber).OrderBy(account => account.Id).ToListAsync(cancellationToken);
            if (users.Count == 0) return Unauthorized();

            return Ok(new
            {
                displayName = string.Join(", ", users.Select(account => account.DisplayName)),
                photoUrl = users[0].PhotoUrl,
                multipleUsers = users.Count > 1,
                stamps = users.SelectMany(account => account.Stamps.Select(stamp => new
                {
                    stamp.Id,
                    stamp.StampValue,
                    stamp.SignatureValue,
                    displayName = account.DisplayName
                }))
            });
        }

        if (!int.TryParse(User.FindFirstValue(ClaimTypes.NameIdentifier), out var userId)) return Unauthorized();
        var user = await db.AppUsers.AsNoTracking().Include(account => account.Stamps).SingleOrDefaultAsync(account => account.Id == userId, cancellationToken);
        return user is null ? Unauthorized() : Ok(new
        {
            user.DisplayName,
            user.PhotoUrl,
            multipleUsers = false,
            stamps = user.Stamps.Select(stamp => new { stamp.Id, stamp.StampValue, stamp.SignatureValue, displayName = user.DisplayName })
        });
    }

    [HttpPost("logout")]
    public async Task<IActionResult> Logout()
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToPage("/Account/Login");
    }
}
