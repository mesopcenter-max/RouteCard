using System.Security.Claims;
using HALRouteCardApp.Data;
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
        var user = await db.AppUsers.SingleOrDefaultAsync(account => account.Username == Username.Trim().ToLowerInvariant(), cancellationToken);
        if (user is null || !PasswordService.Verify(Password, user.PasswordHash))
        {
            ErrorMessage = "The username or password is incorrect.";
            return Page();
        }

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim(ClaimTypes.Email, user.Username),
            new Claim("photo", user.PhotoUrl)
        };
        await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)));
        return LocalRedirect(string.IsNullOrWhiteSpace(ReturnUrl) ? "/Index" : ReturnUrl);
    }
}
