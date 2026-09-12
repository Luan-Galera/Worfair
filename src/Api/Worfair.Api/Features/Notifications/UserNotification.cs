namespace Worfair.Api.Features.Notifications;

using Worfair.BuildingBlocks.Domain.Tenancy;
using Worfair.BuildingBlocks.Domain.ValueObjects;

public enum NotificationPriority { Low = 0, Normal = 1, High = 2 }

public sealed class UserNotification : ITenantEntity
{
    private UserNotification() { }

    public Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; } = string.Empty;
    public string Message { get; private set; } = string.Empty;
    public string Category { get; private set; } = string.Empty;
    public NotificationPriority Priority { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAtUtc { get; private set; }

    public void SetTenantId(TenantId tenantId) => TenantId = tenantId;
    public void MarkAsRead() => IsRead = true;

    public static UserNotification Create(Guid userId, string title, string message, string category, NotificationPriority priority = NotificationPriority.Normal) => new()
    {
        Id = Guid.NewGuid(), UserId = userId, Title = title.Trim(), Message = message.Trim(),
        Category = category.Trim(), Priority = priority, CreatedAtUtc = DateTime.UtcNow
    };
}

public sealed record NotificationResponse(Guid Id, Guid UserId, string Title, string Message, string Category, string Priority, bool IsRead, DateTime CreatedAtUtc);
