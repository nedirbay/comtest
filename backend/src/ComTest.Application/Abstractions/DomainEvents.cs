using ComTest.Shared.Domain;

namespace ComTest.Application.Abstractions;

/// <summary>
/// Handles a domain event taken from the outbox. Delivery is at-least-once, so handlers
/// must tolerate receiving the same event again.
/// </summary>
public interface IDomainEventSubscriber<in TEvent>
    where TEvent : IDomainEvent
{
    Task HandleAsync(TEvent domainEvent, CancellationToken cancellationToken);
}
