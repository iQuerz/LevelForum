using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using LevelForum.Data;
using LevelForum.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LevelForum.Areas.Identity.Pages.Account;

[AllowAnonymous]
public class LoginModel : PageModel
{
	private readonly ApplicationDbContext _context;

	public LoginModel(ApplicationDbContext context)
	{
		_context = context;
	}

	[BindProperty]
	public string Username { get; set; }

	[BindProperty]
	public string Password { get; set; }

	public string? ErrorMessage { get; set; }

	public async Task<IActionResult> OnPostAsync()
	{
		var matchingUser = await _context.AppUsers
			.FirstOrDefaultAsync((u => u.Username == Username
				 || u.Email == Username));

		if (matchingUser == null ||
			!BCrypt.Net.BCrypt.Verify(Password, matchingUser.PasswordHash))
		{
			ErrorMessage = "Invalid username/email or password.";
			return Page();
		}

		var userClaims = GetIdentity(matchingUser);

		await HttpContext.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme,
										userClaims,
										AuthenticationProperties);

		return Redirect("/");
	}

	private static ClaimsPrincipal GetIdentity(AppUser user) =>
		new(
			new ClaimsIdentity(new List<Claim>()
			{
				new(ClaimTypes.Name, user.Username),
				new(ClaimTypes.NameIdentifier, $"{user.Id}"),
				new(ClaimTypes.Role, user.GlobalRole.ToString()),
				new(ClaimTypes.Email, user.Email),
			}, CookieAuthenticationDefaults.AuthenticationScheme
		));

	private static AuthenticationProperties AuthenticationProperties =>
        new()
        {
            IsPersistent = true,
            ExpiresUtc = DateTime.UtcNow.AddHours(8)
        };
}
