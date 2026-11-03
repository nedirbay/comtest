namespace ComTest.Shared.Domain;

public interface IDomainEvent
{
    DateTimeOffset OccurredAt { get; }
}

public abstract class Entity
{
    private readonly List<IDomainEvent> _domainEvents = [];

    /// <summary>Time-ordered (v7) so inserts stay index-friendly; never reveals a sequence.</summary>
    public Guid Id { get; protected init; } = Guid.CreateVersion7();

    public IReadOnlyCollection<IDomainEvent> DomainEvents => _domainEvents;

    public void ClearDomainEvents() => _domainEvents.Clear();

    protected void Raise(IDomainEvent domainEvent) => _domainEvents.Add(domainEvent);
}

public interface IAuditable
{
    DateTimeOffset CreatedAt { get; }

    Guid? CreatedBy { get; }

    DateTimeOffset? UpdatedAt { get; }

    Guid? UpdatedBy { get; }

    void MarkCreated(DateTimeOffset at, Guid? by);

    void MarkUpdated(DateTimeOffset at, Guid? by);
}

public abstract class AuditableEntity : Entity, IAuditable
{
    public DateTimeOffset CreatedAt { get; private set; }

    public Guid? CreatedBy { get; private set; }

    public DateTimeOffset? UpdatedAt { get; private set; }

    public Guid? UpdatedBy { get; private set; }

    public void MarkCreated(DateTimeOffset at, Guid? by)
    {
        CreatedAt = at;
        CreatedBy = by;
    }

    public void MarkUpdated(DateTimeOffset at, Guid? by)
    {
        UpdatedAt = at;
        UpdatedBy = by;
    }
}

/// <summary>Deletes become updates, and a named query filter hides the row.</summary>
public interface ISoftDeletable
{
    bool IsDeleted { get; }

    DateTimeOffset? DeletedAt { get; }

    Guid? DeletedBy { get; }

    void MarkDeleted(DateTimeOffset at, Guid? by);
}

/// <summary>Maps to PostgreSQL's <c>xmin</c>, so conflicting updates fail instead of overwriting.</summary>
public interface IVersioned
{
    uint Version { get; }
}

/// <summary>Row belongs to one tenant: stamped on insert, filtered on read, enforced by RLS.</summary>
public interface ITenantOwned
{
    Guid TenantId { get; }

    void AssignTenant(Guid tenantId);
}

/// <summary>One row per language for a translatable entity.</summary>
public interface ITranslation
{
    string Language { get; }
}

/// <summary>Base for rows that belong to one tenant.</summary>
public abstract class TenantOwnedEntity : AuditableEntity, ITenantOwned, IVersioned
{
    public Guid TenantId { get; private set; }

    public uint Version { get; private set; }

    public void AssignTenant(Guid tenantId)
    {
        if (TenantId != Guid.Empty && TenantId != tenantId)
        {
            throw new InvalidOperationException("The tenant of an entity cannot be changed.");
        }

        TenantId = tenantId;
    }
}

/// <summary>Base for tenant rows that are hidden rather than removed when deleted.</summary>
public abstract class SoftDeletableEntity : TenantOwnedEntity, ISoftDeletable
{
    public bool IsDeleted { get; private set; }

    public DateTimeOffset? DeletedAt { get; private set; }

    public Guid? DeletedBy { get; private set; }

    public void MarkDeleted(DateTimeOffset at, Guid? by)
    {
        IsDeleted = true;
        DeletedAt = at;
        DeletedBy = by;
    }
}
