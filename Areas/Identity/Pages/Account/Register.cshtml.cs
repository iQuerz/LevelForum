using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;
using System.Security.Claims;
using LevelForum.Data;
using LevelForum.Data.Entities;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;

namespace LevelForum.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class RegisterModel : PageModel
    {
        private readonly ApplicationDbContext _context;

        public RegisterModel(ApplicationDbContext context)
        {
            _context = context;
        }

        [BindProperty]
        public string Username { get; set; } = string.Empty;

        [BindProperty]
        public string Email { get; set; } = string.Empty;

        [BindProperty]
        public string Password { get; set; } = string.Empty;

        [BindProperty]
        public string ConfirmPassword { get; set; } = string.Empty;

        public string? ErrorMessage { get; set; }
        public string? SuccessMessage { get; set; }

        public void OnGet()
        {
            // samo render stranice
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (string.IsNullOrWhiteSpace(Username) ||
                string.IsNullOrWhiteSpace(Email) ||
                string.IsNullOrWhiteSpace(Password) ||
                string.IsNullOrWhiteSpace(ConfirmPassword))
            {
                ErrorMessage = "All fields are required.";
                return Page();
            }

            if (!Email.Contains('@') || Email.StartsWith("@") || Email.EndsWith("@"))
            {
                ErrorMessage = "Please enter a valid email address.";
                return Page();
            }

            if (!string.Equals(Password, ConfirmPassword))
            {
                ErrorMessage = "Passwords do not match.";
                return Page();
            }

            // provera jedinstvenosti username/email
            var exists = await _context.AppUsers
                .AnyAsync(u => u.Username == Username || u.Email == Email);

            if (exists)
            {
                ErrorMessage = "Username or email already in use.";
                return Page();
            }

            // kreiranje korisnika
            var newUser = new AppUser
            {
                Username = Username.Trim(),
                Email = Email.Trim(),
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(Password),
                GlobalRole = AppRole.User,
            };

            _context.AppUsers.Add(newUser);
            await _context.SaveChangesAsync();

            // automatski login nakon registracije
            var principal = GetIdentity(newUser);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                principal,
                AuthenticationProperties);

            // posle sign-in-a idi na home
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
}
