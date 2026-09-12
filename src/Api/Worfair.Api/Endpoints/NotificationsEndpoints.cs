namespace Worfair.Api.Endpoints;

using Worfair.Api.Features.Notifications;
using Worfair.Api.Authorization;
using Worfair.Api.Infrastructure;
using Worfair.BuildingBlocks.Application.Security;
using Microsoft.EntityFrameworkCore;

public static class NotificationsEndpoints
{
    public static IEndpointRouteBuilder MapNotificationsEndpoints(this IEndpointRouteBuilder app)
    {
        var notifications = app.MapGroup("/api/notifications").WithTags("Notifications");

        notifications.MapGet(string.Empty, async (FinancialDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            var list = await db.Notifications.Where(x => x.UserId == userId)
                .OrderByDescending(x => x.CreatedAtUtc).ToListAsync(ct);
            return Results.Ok(list.Select(ToResponse).ToList());
        })
            .WithName("ListNotifications")
            .RequireAuthorization(SecurityPolicies.NotificationRead);

        notifications.MapPost("/{notificationId:guid}/read", async (Guid notificationId, FinancialDbContext db, ICurrentUser user, CancellationToken ct) =>
        {
            if (user.UserId is not { } userId) return Results.Unauthorized();
            var notification = await db.Notifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId, ct);
            if (notification is null) return Results.NotFound(new { message = "Notification not found." });
            notification.MarkAsRead();
            await db.SaveChangesAsync(ct);
            return Results.Ok(ToResponse(notification));
        })
            .WithName("MarkNotificationAsRead")
            .RequireAuthorization(SecurityPolicies.NotificationRead);

        return app;
    }

    private static NotificationResponse ToResponse(UserNotification notification) => new(
        notification.Id, notification.UserId, notification.Title, notification.Message,
        notification.Category, notification.Priority.ToString(), notification.IsRead, notification.CreatedAtUtc);
}
