using Microsoft.AspNetCore.Components;

namespace LevelForum.Infrastructure;

public static class NavExtensions
{
    public static void Login(this NavigationManager nav)
    {
        nav.NavigateTo("/Identity/Account/Login", forceLoad:true);
    }

    public static void Register(this NavigationManager nav)
    {
        nav.NavigateTo("/Identity/Account/Register", forceLoad:true);
    }

    public static void Logout(this NavigationManager nav)
    {
        nav.NavigateTo("/Identity/Account/Logout", forceLoad:true);
    }
}