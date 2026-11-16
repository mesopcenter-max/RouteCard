using System.Globalization;
using System.Security.Claims;
using HALRouteCardApp.Data;
using HALRouteCardApp.Models;
using HALRouteCardApp.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace HALRouteCardApp.Pages.Account;

public sealed class LoginModel(RouteCardDbContext db) : PageModel
{
    [BindProperty]
    public string Username { get; set; } = string.Empty;

    [BindProperty]
    public string Password { get; set; } = string.Empty;

    [BindProperty(SupportsGet = true)]
    public string? ReturnUrl { get; set; }

    public string? ErrorMessage { get; set; }

    public async Task<IActionResult> OnPostAsync(CancellationToken cancellationToken)
    {
        var login = Username.Trim();
        List<AppUser> users;
        int? serialNumber = null;
        if (int.TryParse(login, NumberStyles.None, CultureInfo.InvariantCulture, out var parsedSerial))
        {
            serialNumber = parsedSerial;
            users = await db.AppUsers.Where(account => account.SerialNumber == parsedSerial).OrderBy(account => account.Id).ToListAsync(cancellationToken);
        }
        else
        {
            var user = await db.AppUsers.SingleOrDefaultAsync(account => account.Username == login.ToLowerInvariant(), cancellationToken);
            users = user is null ? [] : [user];
        }

        if (users.Count == 0 || users.Any(user => !PasswordService.Verify(Password, user.PasswordHash)))
        {
            ErrorMessage = "The username or password is incorrect.";
            return Page();
        }

        var account = users[0];
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, string.Join(", ", users.Select(user => user.DisplayName))),
            new(ClaimTypes.Email, serialNumber.HasValue ? serialNumber.Value.ToString(CultureInfo.InvariantCulture) : account.Username),
            new("photo", account.PhotoUrl)
        };
        if (serialNumber.HasValue)
            claims.Add(new Claim("serial_number", serialNumber.Value.ToString(CultureInfo.InvariantCulture)));

        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(string.IsNullOrWhiteSpace(ReturnUrl) ? "/Index" : ReturnUrl);
    }
}
