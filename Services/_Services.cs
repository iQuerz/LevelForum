using Microsoft.AspNetCore.Authentication.Cookies;

namespace LevelForum.Data.Services;

public static class Services
{
    public static void Register(WebApplicationBuilder builder)
    {
        builder.Services.AddScoped<SafeExecutor>();

        builder.Services.AddScoped<AppUserService>();
        builder.Services.AddScoped<TopicService>();
        builder.Services.AddScoped<PostService>();
        builder.Services.AddScoped<CommentService>();
        builder.Services.AddScoped<VoteService>();
        builder.Services.AddScoped<TopicFollowService>();
        builder.Services.AddScoped<ReportService>();
        builder.Services.AddScoped<NotificationService>();
    }
}