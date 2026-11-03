using System.Text.Json;
using ComTest.Application.Abstractions;
using ComTest.Infrastructure.Persistence.Outbox;
using ComTest.Shared.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ComTest.Infrastructure.Persistence.Interceptors;

/// <summary>
/// Applies the rules every write must follow: audit stamps, soft delete, tenant ownership,
/// and writing domain events to the outbox in the same transaction as the change itself.
/// </summary>
public sealed class AuditingInterceptor(
    ICurrentUser currentUser,
    ITenantContext tenantContext,
    TimeProvider timeProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Apply(eventData.Context);

        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData,
        InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);

        Apply(eventData.Context);

        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Apply(DbContext? context)
    {
        if (context is null)
        {
            return;
        }

        var now = timeProvider.GetUtcNow();
        var userId = currentUser.UserId;

        context.ChangeTracker.DetectChanges();

        foreach (var entry in context.ChangeTracker.Entries().ToList())
        {
            ApplySoftDelete(entry, now, userId);
            ApplyAudit(entry, now, userId);
            ApplyTenantRules(entry);
        }

        WriteDomainEventsToOutbox(context, now);
    }

    private static void ApplySoftDelete(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        if (entry is { State: EntityState.Deleted, Entity: ISoftDeletable deletable })
        {
            // Unchanged, not Modified: Modified would rewrite every column, including the tenant.
            // EF detects the fields set below when it continues with the save.
            entry.State = EntityState.Unchanged;
            deletable.MarkDeleted(now, userId);
        }
    }

    private static void ApplyAudit(EntityEntry entry, DateTimeOffset now, Guid? userId)
    {
        if (entry.Entity is not IAuditable auditable)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            auditable.MarkCreated(now, userId);
        }
        else if (entry.State == EntityState.Modified)
        {
            auditable.MarkUpdated(now, userId);
        }
    }

    private void ApplyTenantRules(EntityEntry entry)
    {
        if (entry.Entity is not ITenantOwned owned)
        {
            return;
        }

        if (entry.State == EntityState.Added)
        {
            if (owned.TenantId == Guid.Empty)
            {
                owned.AssignTenant(tenantContext.TenantId
                    ?? throw new InvalidOperationException($"Cannot create {entry.Metadata.ClrType.Name} without a tenant."));
            }
            else if (!tenantContext.IsPlatformAccess && owned.TenantId != tenantContext.TenantId)
            {
                throw new InvalidOperationException($"Cannot create {entry.Metadata.ClrType.Name} for another tenant.");
            }
        }
        else if (entry.State == EntityState.Modified
            && entry.Property(nameof(ITenantOwned.TenantId)).IsModified)
        {
            throw new InvalidOperationException($"The tenant of {entry.Metadata.ClrType.Name} cannot be changed.");
        }
    }

    private void WriteDomainEventsToOutbox(DbContext context, DateTimeOffset now)
    {
        var entities = context.ChangeTracker.Entries<Entity>()
            .Where(entry => entry.Entity.DomainEvents.Count > 0)
            .Select(entry => entry.Entity)
            .ToList();

        foreach (var entity in entities)
        {
            foreach (var domainEvent in entity.DomainEvents)
            {
                var type = domainEvent.GetType();

                context.Set<OutboxMessage>().Add(new OutboxMessage
                {
                    Type = type.FullName!,
                    Payload = JsonSerializer.Serialize(domainEvent, type),
                    TenantId = (entity as ITenantOwned)?.TenantId ?? tenantContext.TenantId,
                    OccurredAt = domainEvent.OccurredAt,
                    NextAttemptAt = now,
                });
            }

            entity.ClearDomainEvents();
        }
    }
}
