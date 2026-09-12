namespace Worfair.Modules.Jobs.Domain.Aggregates.Message;

using Worfair.BuildingBlocks.Domain.Abstractions;
using Worfair.BuildingBlocks.Domain.Errors;
using Worfair.Modules.Jobs.Domain.Abstractions;
using Worfair.Modules.Jobs.Domain.Errors;

public sealed class Message : TenantAggregateRoot<MessageId>
{
    private Message() { }

    private Message(MessageId id, Guid? jobPostingId, Guid? serviceProjectId, Guid senderUserId, string body, DateTime utcNow)
    {
        Id = id;
        JobPostingId = jobPostingId;
        ServiceProjectId = serviceProjectId;
        SenderUserId = senderUserId;
        Body = body.Trim();
        CreatedAtUtc = utcNow;
    }

    public Guid? JobPostingId { get; private set; }
    public Guid? ServiceProjectId { get; private set; }
    public Guid SenderUserId { get; private set; }
    public string Body { get; private set; } = default!;
    public DateTime CreatedAtUtc { get; private set; }

    public static Result<Message> Create(Guid? jobPostingId, Guid? serviceProjectId, Guid senderUserId, string body, DateTime? utcNow = null)
    {
        if (jobPostingId is null && serviceProjectId is null)
            return Result.Failure<Message>(JobsErrors.MessageTargetRequired);
        if (string.IsNullOrWhiteSpace(body))
            return Result.Failure<Message>(JobsErrors.MessageBodyRequired);
        if (body.Trim().Length > 2000)
            return Result.Failure<Message>(JobsErrors.MessageTooLong);

        return new Message(new MessageId(Guid.NewGuid()), jobPostingId, serviceProjectId, senderUserId, body, utcNow ?? DateTime.UtcNow);
    }
}

public readonly record struct MessageId(Guid Value)
{
    public static implicit operator Guid(MessageId id) => id.Value;
    public override string ToString() => Value.ToString();
}
