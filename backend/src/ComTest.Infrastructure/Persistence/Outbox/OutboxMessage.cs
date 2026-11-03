namespace ComTest.Infrastructure.Persistence.Outbox;

public sealed class OutboxMessage
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public required string Type { get; init; }

    public required string Payload { get; init; }

    public Guid? TenantId { get; init; }

    public DateTimeOffset OccurredAt { get; init; }

    public DateTimeOffset NextAttemptAt { get; set; }

    public DateTimeOffset? ProcessedAt { get; set; }

    public DateTimeOffset? DeadLetteredAt { get; set; }

    public int Attempts { get; set; }

    public string? LastError { get; set; }
}
