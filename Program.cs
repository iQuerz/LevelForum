using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using LevelForum.Components;
using LevelForum.Data;
using LevelForum.Data.Services;
using Microsoft.AspNetCore.Authentication.Cookies;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();

builder.Services.AddRazorPages();
builder.Services.AddAuthorization();

builder.Services.AddCascadingAuthenticationState();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Identity/Account/Login"; // Optional: Redirect path if user is not authenticated
        options.LogoutPath = "/Identity/Account/Logout";
        options.ExpireTimeSpan = TimeSpan.FromMinutes(30); // Set cookie expiration
        options.SlidingExpiration = false; // Renew cookie automatically on active session
        options.Cookie.SecurePolicy = CookieSecurePolicy.None; // Disable Secure in dev
        options.Cookie.Path = "/";
        options.Cookie.SameSite = SameSiteMode.Strict;
    });
builder.Services.AddAuthorizationCore();

Database.Register(builder);
Services.Register(builder);

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();
}
else
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();

app.UseRouting();
app.UseAuthentication();
app.UseAuthorization();

app.UseStaticFiles();
app.UseAntiforgery();

app.MapRazorPages(); 
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();

app.Run();